using System.Collections;
using System.Collections.Generic;
using ThienKhuyet.Audio;
using ThienKhuyet.Characters;
using ThienKhuyet.Combat;
using ThienKhuyet.Core;
using ThienKhuyet.Data;
using ThienKhuyet.Enemies;
using UnityEngine;

namespace ThienKhuyet.Player
{
    /// <summary>
    /// Player combat: weapon combo chains, heavy attacks, block and parry, lock-on, technique casting, input buffering and cancel windows.
    /// Attack timings come from <see cref="AttackDef"/> data so every weapon family feels different without code changes.
    /// </summary>
    public sealed class PlayerCombat : MonoBehaviour
    {
        enum Buffered { None, Light, Heavy }

        public PlayerController pc;
        public Vitals vitals;
        public HumanoidAnimator anim;
        public readonly LockOn lockOn = new LockOn();

        AttackDef cur;
        float atkTime;
        bool swung;
        int comboIdx;
        float comboTimer;
        Buffered buffered = Buffered.None;
        readonly HashSet<Vitals> hitSet = new HashSet<Vitals>();
        readonly List<Vitals> tmp = new List<Vitals>(8);

        bool blocking;
        float blockStart;
        WeaponFamilyDef blockFamily;

        SkillDef castSkill;
        float castT;
        bool castFired;
        readonly Dictionary<string, float> cooldownEnd = new Dictionary<string, float>();
        float combatTimer;
        float heavyHold;

        PlayerState PS => Game.Session != null ? Game.Session.player : null;
        public bool InCombatStance => combatTimer > 0f || EnemyBrain.AlertedCount > 0;
        public bool IsBlocking => blocking;
        public AttackDef CurrentAttack => cur;
        public float MoveMultiplier => pc.State == PState.Cast ? 0.25f : (cur != null ? cur.moveSpeedMul : 0.3f);
        public bool CanCancelIntoDodge => (pc.State == PState.Attack && cur != null && atkTime >= cur.cancelFrom * 0.9f) || (pc.State == PState.Cast && castSkill != null && castT >= castSkill.duration * 0.5f);

        public float CooldownRemaining(string skillId)
        {
            return cooldownEnd.TryGetValue(skillId, out float t) ? Mathf.Max(0f, t - Time.time) : 0f;
        }

        public float CooldownFraction(SkillDef s)
        {
            if (s == null) return 0f;
            float total = PS != null ? PS.Cooldown(s) : s.cooldown;
            return total <= 0f ? 0f : Mathf.Clamp01(CooldownRemaining(s.id) / total);
        }

        public void ResetCooldowns()
        {
            cooldownEnd.Clear();
        }

        public void NotifyCombat(float seconds = 6f)
        {
            combatTimer = Mathf.Max(combatTimer, seconds);
        }

        // ------------------------------------------------------------------ update
        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || PS == null || Game.Mode != GameMode.Playing && pc.State != PState.Dead) { if (Game.Mode != GameMode.Playing) EndBlockIfNeeded(); return; }
            if (combatTimer > 0f) combatTimer -= dt;
            if (comboTimer > 0f) comboTimer -= dt;
            else comboIdx = 0;

            GameInput input = Game.Input;
            bool open = input != null && !input.IsLocked;
            if (pc.State == PState.Dead) return;

            if (open)
            {
                if (input.LockOnPressed && Game.Camera != null)
                {
                    lockOn.Toggle(transform, Game.Camera.cam);
                    EventBus.Publish(new PlayerActionEvent { action = "lockon" });
                }
                Vector2 look = Game.Input.Look;
                lockOn.Tick(transform, Game.Camera != null ? Game.Camera.cam : null, dt, look);
            }
            Game.Camera?.SetLockTarget(lockOn.Active ? lockOn.Target.transform : null);

            switch (pc.State)
            {
                case PState.Locomotion:
                    if (!open) break;
                    if (input.BlockHeld && vitals.stamina > 1f) { StartBlock(); break; }
                    if (input.LightPressed) { StartAttack(NextLight()); break; }
                    if (input.HeavyPressed) { StartAttack(Family().heavy); break; }
                    for (int i = 0; i < PlayerState.SkillSlots; i++)
                        if (input.SkillPressed(i)) { TryCast(i); break; }
                    if (input.QuickUsePressed) Game.Manager?.UseQuickHeal();
                    break;
                case PState.Attack:
                    UpdateAttack(dt, open, input);
                    break;
                case PState.Block:
                    if (!open || !input.BlockHeld) EndBlock();
                    else if (input.LightPressed || input.HeavyPressed) { EndBlock(); if (input.HeavyPressed) StartAttack(Family().heavy); else StartAttack(NextLight()); }
                    break;
                case PState.Cast:
                    UpdateCast(dt);
                    break;
                default:
                    if (blocking) EndBlock();
                    break;
            }
        }

        WeaponFamilyDef Family()
        {
            return ContentDB.Family(PS.WeaponFamilyNow) ?? ContentDB.Family(WeaponFamily.Fist);
        }

        AttackDef NextLight()
        {
            WeaponFamilyDef f = Family();
            int idx = comboTimer > 0f && comboIdx < f.light.Length ? comboIdx : 0;
            comboIdx = (idx + 1) % f.light.Length;
            return f.light[idx];
        }

        // ------------------------------------------------------------------ attacks
        void StartAttack(AttackDef def)
        {
            if (def == null) return;
            if (!vitals.SpendStamina(def.staminaCost)) { Game.UI?.FlashStamina(); buffered = Buffered.None; return; }
            if (blocking) EndBlock();
            cur = def;
            atkTime = 0f;
            swung = false;
            hitSet.Clear();
            buffered = Buffered.None;
            comboTimer = def.duration + 0.5f;
            combatTimer = 6f;
            pc.SetState(PState.Attack);
            float len = anim.ActionLength(def.clip);
            float speed = len > 0.01f ? len / Mathf.Max(0.1f, def.duration) : 1f;
            anim.PlayAction(def.clip, speed, 0.045f);
            Vector3 dir = AimAssist();
            if (dir.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, dir) < 80f) pc.FaceInstant(dir);
            EventBus.Publish(new PlayerActionEvent { action = def.heavy ? "heavy_attack" : "light_attack" });
        }

        Vector3 AimAssist()
        {
            if (lockOn.Active) return lockOn.Target.Center - transform.position;
            GameInput input = Game.Input;
            if (input != null && !input.IsLocked)
            {
                Vector2 mv = input.Move;
                if (mv.sqrMagnitude > 0.1f && Game.Camera != null) return Game.Camera.PlanarForward * mv.y + Game.Camera.PlanarRight * mv.x;
            }
            // nearest enemy in front within a short range
            if (MeleeHit.Arc(transform.position, transform.forward, 4.2f, 100f, 2.5f, 1 << GameLayers.Enemy, vitals, tmp) > 0)
            {
                Vitals best = null; float bd = 99f;
                for (int i = 0; i < tmp.Count; i++) { float d = Vector3.Distance(transform.position, tmp[i].transform.position); if (d < bd) { bd = d; best = tmp[i]; } }
                if (best != null) return best.transform.position - transform.position;
            }
            return Vector3.zero;
        }

        void UpdateAttack(float dt, bool open, GameInput input)
        {
            if (cur == null) { pc.SetState(PState.Locomotion); return; }
            atkTime += dt;

            // input buffering
            if (open)
            {
                if (input.LightPressed) buffered = Buffered.Light;
                else if (input.HeavyPressed) buffered = Buffered.Heavy;
            }

            // steering and lunge
            if (atkTime < cur.hitStart)
            {
                Vector3 want = AimAssist();
                if (want.sqrMagnitude > 0.01f) pc.FaceDirection(want, 18f, dt);
            }
            float ls = cur.hitStart * 0.2f, le = cur.hitEnd;
            if (cur.lunge > 0.01f && atkTime >= ls && atkTime <= le)
            {
                float speed = cur.lunge / Mathf.Max(0.05f, le - ls);
                Vector3 step = transform.forward * (speed * dt);
                if (lockOn.Active)
                {
                    float d = Mathx.FlatDistance(transform.position, lockOn.Target.transform.position);
                    if (d < 1.35f) step = Vector3.zero;
                }
                pc.AddMove(step);
            }

            bool active = atkTime >= cur.hitStart && atkTime <= cur.hitEnd;
            if (active && !swung)
            {
                swung = true;
                AudioManager.Instance?.Sfx(cur.sfx, transform.position, 0.8f);
                Vfx.Play(cur.vfx, transform.position + Vector3.up * 1.1f + transform.forward * 1.0f, VfxRotation(), cur.heavy ? 1.1f : 1f);
            }
            anim.SetTrail(active);
            if (active) DoHits();

            if (buffered != Buffered.None && atkTime >= cur.cancelFrom)
            {
                Buffered b = buffered;
                buffered = Buffered.None;
                StartAttack(b == Buffered.Heavy ? Family().heavy : NextLight());
                return;
            }
            if (atkTime >= cur.duration)
            {
                anim.SetTrail(false);
                cur = null;
                pc.SetState(PState.Locomotion);
            }
        }

        Quaternion VfxRotation()
        {
            // alternate the swing plane between combo steps so slashes do not all look the same
            float roll = (comboIdx % 2 == 0 ? -35f : 35f);
            if (cur != null && cur.heavy) roll = 0f;
            return Quaternion.LookRotation(transform.forward, Vector3.up) * Quaternion.Euler(0f, 0f, roll);
        }

        void DoHits()
        {
            if (MeleeHit.Arc(transform.position, transform.forward, cur.range, cur.arc, 2.2f, 1 << GameLayers.Enemy, vitals, tmp) == 0) return;
            WeaponFamily fam = PS.WeaponFamilyNow;
            var list = new List<Vitals>(tmp);
            for (int i = 0; i < list.Count; i++)
            {
                Vitals v = list[i];
                if (!hitSet.Add(v)) continue;
                var brain = v.GetComponent<EnemyBrain>();
                bool broken = brain != null && brain.IsStaggered;
                float power = vitals.Attack * cur.damageMul * PS.PathWeaponMultiplier(fam);
                var info = new DamageInfo
                {
                    power = power,
                    type = cur.damageType,
                    team = Team.Player,
                    source = gameObject,
                    point = v.Center,
                    direction = (v.Center - transform.position).normalized,
                    poiseDamage = cur.poiseDamage,
                    knockback = cur.knockback,
                    heavy = cur.heavy,
                    critChance = vitals.stats.Get(StatId.CritChance) + (broken ? 0.6f : 0f),
                    critDamage = vitals.stats.Get(StatId.CritDamage) + (broken ? 0.35f : 0f),
                    attackerRealm = PS.Realm,
                    tag = cur.heavy ? "heavy" : "light"
                };
                DamageResult r = Strike.Hit(v, info);
                if (!r.ignored && !r.dodged)
                {
                    if (!r.parried)
                    {
                        vitals.RestoreQi(cur.qiGain);
                        vitals.RestoreStamina(2f);
                    }
                    combatTimer = 6f;
                }
            }
        }

        // ------------------------------------------------------------------ guard
        void StartBlock()
        {
            blockFamily = Family();
            blocking = true;
            blockStart = Time.time;
            pc.SetState(PState.Block);
            anim.PlayAction(blockFamily.guardClip, 1f, 0.07f);
            vitals.guard = Guard;
            vitals.staminaRegenBlocked = true;
            combatTimer = Mathf.Max(combatTimer, 3f);
            EventBus.Publish(new PlayerActionEvent { action = "block" });
        }

        void EndBlock()
        {
            EndBlockIfNeeded();
            if (pc.State == PState.Block) pc.SetState(PState.Locomotion);
        }

        void EndBlockIfNeeded()
        {
            if (!blocking) return;
            blocking = false;
            vitals.guard = null;
            vitals.staminaRegenBlocked = false;
            anim.StopAction(0.12f);
        }

        GuardResult Guard(DamageInfo info)
        {
            if (!blocking || pc.State != PState.Block) return GuardResult.None;
            Vector3 incoming = info.direction;
            incoming.y = 0f;
            if (incoming.sqrMagnitude < 0.0001f) return GuardResult.None;
            float facing = Vector3.Dot(transform.forward, -incoming.normalized);
            if (facing < 0.15f || info.unblockable) return GuardResult.None;

            float age = Time.time - blockStart;
            if (age <= blockFamily.parryWindow)
            {
                // perfect timing: negate the hit, stagger the attacker and reward the player
                anim.PlayAction("parry", 1f, 0.02f);
                vitals.GrantIFrames(0.35f);
                vitals.RestoreQi(10f);
                vitals.RestoreStamina(18f);
                if (info.source != null)
                {
                    var attacker = info.source.GetComponent<Vitals>();
                    if (attacker != null) attacker.Stagger(info);
                }
                blockStart = Time.time;   // chained parries stay possible
                EventBus.Publish(new PlayerActionEvent { action = "parry" });
                return new GuardResult { outcome = GuardOutcome.Parried };
            }
            float cost = Mathf.Max(6f, info.power * 0.5f);
            if (vitals.stamina < cost * 0.6f)
            {
                vitals.stamina = 0f;
                EndBlockDelayed();
                return new GuardResult { outcome = GuardOutcome.GuardBroken };
            }
            vitals.stamina = Mathf.Max(0f, vitals.stamina - cost);
            float reduction = Mathf.Clamp01(blockFamily.blockReduction + (PS.path == CultivationPath.Wu ? 0.1f : 0f));
            return new GuardResult { outcome = GuardOutcome.Blocked, reduction = reduction };
        }

        void EndBlockDelayed()
        {
            blocking = false;
            vitals.guard = null;
        }

        // ------------------------------------------------------------------ techniques
        void TryCast(int slot)
        {
            string id = PS.skillSlots[slot];
            if (string.IsNullOrEmpty(id)) return;
            SkillDef s = ContentDB.Skill(id);
            if (s == null) return;
            if (!PS.SkillUsable(s)) { Game.UI?.Toast(Loc.T("toast.skill_realm", Loc.T("realm." + s.reqRealm + ".name"))); return; }
            if (CooldownRemaining(id) > 0f) { AudioManager.Instance?.Sfx2D("ui_error", 0.5f); return; }
            float cost = PS.QiCost(s);
            if (!vitals.SpendQi(cost)) { Game.UI?.Toast(Loc.T("toast.no_qi")); Game.UI?.FlashQi(); AudioManager.Instance?.Sfx2D("ui_error", 0.6f); return; }
            cooldownEnd[id] = Time.time + PS.Cooldown(s);
            castSkill = s;
            castT = 0f;
            castFired = false;
            combatTimer = 6f;
            pc.SetState(PState.Cast);
            float len = anim.ActionLength(s.clip);
            anim.PlayAction(s.clip, len > 0.01f ? len / Mathf.Max(0.2f, s.duration) : 1f, 0.05f);
            Vector3 dir = AimAssist();
            if (Game.Camera != null && !lockOn.Active) dir = Game.Camera.PlanarForward;
            if (dir.sqrMagnitude > 0.01f) pc.FaceInstant(dir);
            AudioManager.Instance?.Sfx(s.sfx, transform.position, 0.8f);
            EventBus.Publish(new SkillUsedEvent { skillId = s.id });
            EventBus.Publish(new PlayerActionEvent { action = "skill" });
        }

        void UpdateCast(float dt)
        {
            if (castSkill == null) { pc.SetState(PState.Locomotion); return; }
            castT += dt;
            if (!castFired && castT >= castSkill.castTime)
            {
                castFired = true;
                Execute(castSkill);
            }
            if (castT >= castSkill.duration)
            {
                castSkill = null;
                pc.SetState(PState.Locomotion);
            }
        }

        Vector3 CastOrigin()
        {
            return transform.position + Vector3.up * 1.3f + transform.forward * 0.55f;
        }

        Vector3 AimDirection(Vector3 origin)
        {
            if (lockOn.Active) return (lockOn.Target.Center - origin).normalized;
            if (Game.Camera == null) return transform.forward;
            Transform cam = Game.Camera.cam.transform;
            var ray = new Ray(cam.position, cam.forward);
            Vector3 point = Physics.Raycast(ray, out RaycastHit hit, 60f, GameLayers.ObstacleMask | (1 << GameLayers.Enemy), QueryTriggerInteraction.Ignore)
                ? hit.point : ray.GetPoint(45f);
            Vector3 d = point - origin;
            if (d.sqrMagnitude < 1f) d = transform.forward;
            return d.normalized;
        }

        Vitals AimTarget(float range)
        {
            if (lockOn.Active && Vector3.Distance(transform.position, lockOn.Target.transform.position) <= range) return lockOn.Target;
            if (MeleeHit.Arc(transform.position, transform.forward, range, 70f, 4f, 1 << GameLayers.Enemy, vitals, tmp) == 0) return null;
            Vitals best = null; float bd = float.MaxValue;
            for (int i = 0; i < tmp.Count; i++)
            {
                float d = Vector3.Distance(transform.position, tmp[i].transform.position);
                if (d < bd) { bd = d; best = tmp[i]; }
            }
            return best;
        }

        DamageInfo SkillInfo(SkillDef s, float mulOverride = -1f)
        {
            float basePower = s.useSpellPower ? vitals.SpellPower : vitals.Attack;
            return new DamageInfo
            {
                power = basePower * (mulOverride >= 0f ? mulOverride : s.damageMul),
                type = s.element,
                team = Team.Player,
                source = gameObject,
                poiseDamage = s.poiseDamage,
                knockback = s.knockback,
                heavy = s.poiseDamage >= 40f,
                critChance = vitals.stats.Get(StatId.CritChance),
                critDamage = vitals.stats.Get(StatId.CritDamage),
                attackerRealm = PS.Realm,
                tag = "skill:" + s.id
            };
        }

        void Execute(SkillDef s)
        {
            Vector3 origin = CastOrigin();
            int enemies = 1 << GameLayers.Enemy;
            switch (s.kind)
            {
                case SkillKind.Projectile:
                    {
                        Vector3 dir = AimDirection(origin);
                        Projectile p = Projectile.Spawn(origin, dir, SkillInfo(s), s.speed, s.range, s.vfx, s.element == DamageType.Fire ? "explosion_fire" : (s.id == "sword_wave" ? "qi_burst" : "qi_burst"), Mathf.Max(0.35f, s.radius * 0.3f), s.element == DamageType.Fire ? s.radius : 0f, vitals);
                        p.status = s.status; p.statusDuration = s.statusDuration; p.statusPower = s.statusPower;
                        if (s.id == "sword_wave") { p.pierce = true; p.hitRadius = 1.1f; }
                        if (lockOn.Active) { p.homingTarget = lockOn.Target.transform; p.homingStrength = 1.4f; }
                        break;
                    }
                case SkillKind.Cone:
                    {
                        Vfx.Play("shockwave", transform.position + transform.forward * 1.2f, Quaternion.identity, Mathf.Max(0.6f, s.radius / 5f));
                        Game.Camera?.Shake(0.1f, 0.2f);
                        DoConeDamage(s);
                        break;
                    }
                case SkillKind.Nova:
                case SkillKind.Slam:
                    {
                        Vfx.Play(s.vfx, transform.position + Vector3.up * 0.1f, Quaternion.identity, 1f);
                        Vfx.Ring(transform.position, s.radius, new Color(s.color.r, s.color.g, s.color.b, 0.9f), 0.5f);
                        Game.Camera?.Shake(0.09f, 0.2f);
                        Strike.Area(transform.position, s.radius, enemies, vitals, SkillInfo(s), 0.7f, s.status, s.statusDuration, s.statusPower);
                        break;
                    }
                case SkillKind.Strike:
                    {
                        Vitals t = AimTarget(s.range);
                        Vector3 pos = t != null ? t.transform.position : transform.position + transform.forward * Mathf.Min(s.range, 8f);
                        StartCoroutine(DelayedStrike(s, pos, 0.2f));
                        break;
                    }
                case SkillKind.Barrage:
                    {
                        Vitals t = AimTarget(s.range);
                        Vector3 pos = t != null ? t.transform.position : transform.position + transform.forward * 7f;
                        StartCoroutine(Barrage(s, pos));
                        break;
                    }
                case SkillKind.Buff:
                    vitals.AddTimedBuff(s.id, s.buffMods, s.buffDuration);
                    Vfx.Play(s.vfx, transform.position, Quaternion.identity, 1f);
                    break;
                case SkillKind.Heal:
                    vitals.Heal(vitals.MaxHp * s.healAmount);
                    Vfx.Play(s.vfx, transform.position, Quaternion.identity, 1f);
                    AudioManager.Instance?.Sfx2D("heal", 0.8f);
                    break;
                case SkillKind.QiRestore:
                    vitals.RestoreQi(s.healAmount);
                    Vfx.Play(s.vfx, transform.position, Quaternion.identity, 1f);
                    break;
                case SkillKind.Dash:
                    {
                        Vector3 dir = transform.forward;
                        Vector2 mv = Game.Input != null ? Game.Input.Move : Vector2.zero;
                        if (mv.sqrMagnitude > 0.1f && Game.Camera != null) dir = Game.Camera.PlanarForward * mv.y + Game.Camera.PlanarRight * mv.x;
                        Vfx.Play(s.vfx, transform.position + Vector3.up * 0.8f, Quaternion.LookRotation(dir), 1f);
                        pc.Dash(dir, s.range, s.duration);
                        castSkill = null;
                        break;
                    }
            }
        }

        void DoConeDamage(SkillDef s)
        {
            if (MeleeHit.Arc(transform.position, transform.forward, s.radius, 85f, 3f, 1 << GameLayers.Enemy, vitals, tmp) == 0) return;
            var list = new List<Vitals>(tmp);
            for (int i = 0; i < list.Count; i++)
            {
                DamageInfo info = SkillInfo(s);
                info.direction = (list[i].Center - transform.position).normalized;
                Strike.Hit(list[i], info, s.status, s.statusDuration, s.statusPower);
            }
        }

        IEnumerator DelayedStrike(SkillDef s, Vector3 pos, float delay)
        {
            Vfx.Ring(pos, s.radius, new Color(0.85f, 0.8f, 1f, 0.9f), delay, RingFx.Mode.Telegraph);
            yield return new WaitForSeconds(delay);
            Vfx.Play(s.vfx, pos, Quaternion.identity, 1f);
            AudioManager.Instance?.Sfx("thunder", pos, 1f);
            Game.Camera?.Shake(0.12f, 0.25f);
            Strike.Area(pos, s.radius, 1 << GameLayers.Enemy, vitals, SkillInfo(s), 0.7f, s.status, s.statusDuration, s.statusPower);
        }

        IEnumerator Barrage(SkillDef s, Vector3 pos)
        {
            Vfx.Play("sword_rain", pos, Quaternion.identity, 1f);
            Vfx.Ring(pos, s.radius, new Color(0.7f, 0.95f, 1f, 0.7f), 1.0f, RingFx.Mode.Pulse);
            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForSeconds(0.28f);
                DamageInfo info = SkillInfo(s, s.damageMul * (s.count / 3f));
                Strike.Area(pos, s.radius, 1 << GameLayers.Enemy, vitals, info, 0.8f);
                Game.Camera?.Shake(0.05f, 0.1f);
            }
        }

        // ------------------------------------------------------------------ external control
        public void QueueFromDodge(bool heavy)
        {
            // dodge-cancel into an immediate attack
            StartAttack(heavy ? Family().heavy : NextLight());
        }

        public void CancelAll()
        {
            if (anim != null) anim.SetTrail(false);
            EndBlockIfNeeded();
            cur = null;
            castSkill = null;
            buffered = Buffered.None;
            if (pc != null && (pc.State == PState.Attack || pc.State == PState.Cast || pc.State == PState.Block)) pc.SetState(PState.Locomotion);
        }
    }
}
