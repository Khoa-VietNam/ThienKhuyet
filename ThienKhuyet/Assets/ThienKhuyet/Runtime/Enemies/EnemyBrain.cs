using System.Collections.Generic;
using ThienKhuyet.Audio;
using ThienKhuyet.Characters;
using ThienKhuyet.Combat;
using ThienKhuyet.Core;
using ThienKhuyet.Data;
using UnityEngine;

namespace ThienKhuyet.Enemies
{
    public enum EState { Idle = 0, Wander, Alert, Chase, Circle, Attack, Stagger, Dead, Return }

    /// <summary>
    /// Enemy AI: perception (sight cone, hearing, pack alerting), chase, strafing, data-driven attack patterns
    /// (melee, projectile, barrage, slam, charge, leap, summon), stagger windows, boss phases and leashing.
    /// </summary>
    public sealed class EnemyBrain : MonoBehaviour, IKnockbackable
    {
        static readonly List<EnemyBrain> all = new List<EnemyBrain>();
        static readonly HashSet<EnemyBrain> alerted = new HashSet<EnemyBrain>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            all.Clear();
            alerted.Clear();
        }

        public static IReadOnlyList<EnemyBrain> All => all;
        public static int AlertedCount => alerted.Count;

        public EnemyDef def;
        public Vitals vitals;
        public CharacterController cc;
        public string spawnId;
        public Vector3 home;
        public int phase;
        public bool isMinion;

        ICharacterAnimation anim;
        HumanoidAnimator hum;
        QuadrupedAnimator quad;
        Transform target;
        Vitals targetVitals;
        Vector3 velocity, external, externalDv;
        float vertVel;
        float stateTime;
        float[] cdEnd;
        float globalCd;
        AttackDef cur;
        float atkTime;
        bool swung, landed;
        readonly HashSet<Vitals> hitSet = new HashSet<Vitals>();
        readonly List<Vitals> tmp = new List<Vitals>(4);
        Vector3 wanderPoint;
        float idleDuration = 2f;
        float strafeSign = 1f, strafeTime;
        float lostTimer, deathTimer;
        Vector3 leapVel, chargeDir;
        float leapT;
        float speedMul = 1f;
        float alertDelay;
        float lastNoticeTime = -99f;
        float ringTimer;

        public EState State { get; private set; } = EState.Idle;
        public bool IsStaggered => State == EState.Stagger;
        public bool IsAggro => target != null && State != EState.Dead && State != EState.Idle && State != EState.Wander && State != EState.Return;
        public Transform Target => target;
        public bool HasTarget => target != null;

        // ------------------------------------------------------------------ setup
        public void Init(EnemyDef d, ICharacterAnimation animation, string spawn)
        {
            def = d;
            spawnId = spawn;
            anim = animation;
            hum = animation as HumanoidAnimator;
            quad = animation as QuadrupedAnimator;
            cdEnd = new float[d.attacks.Length];
            home = transform.position;
            idleDuration = Random.Range(1f, 3f);
            vitals.Died += OnDied;
            vitals.Staggered += OnStaggered;
            vitals.Damaged += OnDamaged;
            all.Add(this);
        }

        void OnDestroy()
        {
            all.Remove(this);
            alerted.Remove(this);
        }

        // ------------------------------------------------------------------ update
        void Update()
        {
            if (State == EState.Dead)
            {
                deathTimer += Time.deltaTime;
                if (deathTimer > 7f) Sink();
                return;
            }
            float dt = Time.deltaTime;
            if (dt <= 0f || def == null) return;
            stateTime += dt;
            if (!cc.enabled) return;

            Vitals pv = Game.PlayerVitals;
            bool playerAlive = pv != null && pv.IsAlive && Game.PlayerObject != null;
            if (target != null && (!playerAlive || Game.Mode == GameMode.Cutscene)) { Disengage(); }

            bool stunned = vitals.status.Stunned && State != EState.Stagger;
            if (stunned) { Idle(dt, true); return; }

            switch (State)
            {
                case EState.Idle: UpdateIdle(dt, playerAlive); break;
                case EState.Wander: UpdateWander(dt, playerAlive); break;
                case EState.Alert: UpdateAlert(dt); break;
                case EState.Chase: UpdateChase(dt, playerAlive); break;
                case EState.Circle: UpdateCircle(dt, playerAlive); break;
                case EState.Attack: UpdateAttack(dt); break;
                case EState.Stagger: UpdateStagger(dt); break;
                case EState.Return: UpdateReturn(dt, playerAlive); break;
            }
            if (quad != null) quad.alert = IsAggro;
        }

        void SetState(EState s)
        {
            if (State == s) return;
            State = s;
            stateTime = 0f;
            bool a = s == EState.Alert || s == EState.Chase || s == EState.Circle || s == EState.Attack;
            if (a) alerted.Add(this); else alerted.Remove(this);
        }

        // ------------------------------------------------------------------ perception
        void UpdateIdle(float dt, bool playerAlive)
        {
            Idle(dt, false);
            if (playerAlive) Perceive();
            if (stateTime > idleDuration && State == EState.Idle)
            {
                if (def.walkSpeed > 0.1f && Random.value < 0.8f)
                {
                    Vector2 r = Random.insideUnitCircle * 7f;
                    wanderPoint = home + new Vector3(r.x, 0f, r.y);
                    SetState(EState.Wander);
                }
                else { stateTime = 0f; idleDuration = Random.Range(1.5f, 4f); }
            }
        }

        void UpdateWander(float dt, bool playerAlive)
        {
            if (playerAlive) Perceive();
            if (State != EState.Wander) return;
            if (Mathx.FlatDistance(transform.position, wanderPoint) < 1f || stateTime > 9f)
            {
                idleDuration = Random.Range(2f, 5f);
                SetState(EState.Idle);
                return;
            }
            MoveToward(wanderPoint, def.walkSpeed, dt, 6f);
        }

        void Idle(float dt, bool stunned)
        {
            velocity = Vector3.MoveTowards(velocity, Vector3.zero, 30f * dt);
            Move(dt);
        }

        void Perceive()
        {
            Transform p = Game.PlayerObject.transform;
            Vector3 to = p.position - transform.position;
            float flat = new Vector2(to.x, to.z).magnitude;
            if (flat > def.sightRange)
            {
                if (flat <= def.hearRange && PlayerIsNoisy()) Aggro(p);
                return;
            }
            float angle = Vector3.Angle(transform.forward, new Vector3(to.x, 0f, to.z));
            if (angle > def.fov * 0.5f && flat > 4.5f) return;
            if (Game.Session != null && Game.Session.flags.Has("stealth_blessing") && flat > def.sightRange * 0.4f) return;
            Vector3 eye = transform.position + Vector3.up * 1.2f * Mathf.Max(0.6f, def.scale);
            Vector3 dest = p.position + Vector3.up * 1.2f;
            Vector3 dir = dest - eye;
            if (Physics.Raycast(eye, dir.normalized, dir.magnitude, GameLayers.ObstacleMask, QueryTriggerInteraction.Ignore)) return;
            Aggro(p);
        }

        bool PlayerIsNoisy()
        {
            var pc = Game.Player;
            return pc != null && (pc.IsSprinting || pc.State == Player.PState.Attack || pc.State == Player.PState.Cast);
        }

        public void Notice(Transform t)
        {
            if (State == EState.Dead || target != null) return;
            if (State == EState.Idle || State == EState.Wander || State == EState.Return) Aggro(t);
        }

        void Aggro(Transform t)
        {
            if (State == EState.Dead) return;
            target = t;
            targetVitals = t.GetComponent<Vitals>();
            lostTimer = 0f;
            alertDelay = def.boss ? 0.2f : Random.Range(0.25f, 0.7f);
            SetState(EState.Alert);
            if (Time.time - lastNoticeTime > 4f)
            {
                lastNoticeTime = Time.time;
                AudioManager.Instance?.Sfx(def.rig == EnemyRigKind.Humanoid ? "enemy_hurt" : "growl", transform.position, 0.8f);
            }
            // pack behaviour: alert nearby allies
            var cols = Physics.OverlapSphere(transform.position, 16f, 1 << GameLayers.Enemy, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < cols.Length; i++)
            {
                var b = cols[i].GetComponentInParent<EnemyBrain>();
                if (b != null && b != this) b.Notice(t);
            }
            if (def.boss) Game.UI?.ShowBoss(this);
        }

        void Disengage()
        {
            target = null;
            targetVitals = null;
            if (State != EState.Dead && State != EState.Stagger) SetState(EState.Return);
            if (def != null && def.boss) Game.UI?.HideBoss(this);
        }

        // ------------------------------------------------------------------ combat movement
        void UpdateAlert(float dt)
        {
            if (target == null) { SetState(EState.Idle); return; }
            Face(target.position - transform.position, 10f, dt);
            velocity = Vector3.MoveTowards(velocity, Vector3.zero, 20f * dt);
            Move(dt);
            if (stateTime >= alertDelay) SetState(EState.Chase);
        }

        void UpdateChase(float dt, bool playerAlive)
        {
            if (target == null) { SetState(EState.Return); return; }
            float dist = Mathx.FlatDistance(transform.position, target.position);
            float fromHome = Mathx.FlatDistance(transform.position, home);
            bool visible = CanSeeTarget();
            lostTimer = visible ? 0f : lostTimer + dt;
            if (fromHome > def.leashRange || lostTimer > 9f) { Disengage(); return; }

            globalCd -= dt;
            bool ranged = def.preferredRange > 6f;
            if (globalCd <= 0f)
            {
                AttackDef a = Pick(dist, visible);
                if (a != null) { StartAttack(a); return; }
            }

            float runSpeed = def.runSpeed * speedMul * vitals.status.MoveMultiplier;
            if (ranged)
            {
                if (dist < def.preferredRange - 2.5f)
                {
                    // back away while facing the player
                    Vector3 away = (transform.position - target.position);
                    away.y = 0f;
                    MoveDir(away.normalized, def.walkSpeed * 1.5f * vitals.status.MoveMultiplier, dt, false);
                    Face(target.position - transform.position, 8f, dt);
                }
                else if (dist > def.preferredRange + 3f || !visible) MoveToward(target.position, runSpeed, dt, 8f);
                else EnterCircle();
            }
            else
            {
                float want = Mathf.Max(def.preferredRange, 1.4f);
                if (dist > want + 0.4f) MoveToward(target.position, runSpeed, dt, 9f);
                else EnterCircle();
            }
        }

        void EnterCircle()
        {
            strafeSign = Random.value < 0.5f ? -1f : 1f;
            strafeTime = Random.Range(1.2f, 2.4f);
            SetState(EState.Circle);
        }

        void UpdateCircle(float dt, bool playerAlive)
        {
            if (target == null) { SetState(EState.Return); return; }
            float dist = Mathx.FlatDistance(transform.position, target.position);
            bool visible = CanSeeTarget();
            lostTimer = visible ? 0f : lostTimer + dt;
            if (Mathx.FlatDistance(transform.position, home) > def.leashRange || lostTimer > 9f) { Disengage(); return; }
            globalCd -= dt;
            if (globalCd <= 0f)
            {
                AttackDef a = Pick(dist, visible);
                if (a != null) { StartAttack(a); return; }
            }
            Vector3 to = target.position - transform.position;
            to.y = 0f;
            Vector3 side = Vector3.Cross(Vector3.up, to.normalized) * strafeSign;
            float radialCorrection = Mathf.Clamp(dist - Mathf.Max(def.preferredRange, 1.6f), -1f, 1f);
            Vector3 dir = (side + to.normalized * radialCorrection * 0.7f).normalized;
            MoveDir(dir, Mathf.Max(def.walkSpeed, 1.4f) * 1.25f * speedMul * vitals.status.MoveMultiplier, dt, false);
            Face(to, 9f, dt);
            strafeTime -= dt;
            if (strafeTime <= 0f || dist > Mathf.Max(def.preferredRange, 2f) + 3.5f) SetState(EState.Chase);
        }

        void UpdateStagger(float dt)
        {
            velocity = Vector3.MoveTowards(velocity, Vector3.zero, 30f * dt);
            Move(dt);
            if (stateTime >= 1.25f)
            {
                anim.StopAction(0.15f);
                SetState(target != null ? EState.Chase : EState.Idle);
            }
        }

        void UpdateReturn(float dt, bool playerAlive)
        {
            if (playerAlive) Perceive();
            if (State != EState.Return) return;
            vitals.Heal(vitals.MaxHp * 0.08f * dt);
            if (Mathx.FlatDistance(transform.position, home) < 1.8f || stateTime > 25f)
            {
                idleDuration = 1.5f;
                SetState(EState.Idle);
                return;
            }
            MoveToward(home, def.runSpeed * 0.7f, dt, 6f);
        }

        bool CanSeeTarget()
        {
            if (target == null) return false;
            Vector3 eye = transform.position + Vector3.up * 1.2f * Mathf.Max(0.6f, def.scale);
            Vector3 dest = target.position + Vector3.up * 1.2f;
            Vector3 d = dest - eye;
            return !Physics.Raycast(eye, d.normalized, d.magnitude, GameLayers.ObstacleMask, QueryTriggerInteraction.Ignore);
        }

        // ------------------------------------------------------------------ attack selection and execution
        AttackDef Pick(float dist, bool visible)
        {
            float total = 0f;
            int count = def.attacks.Length;
            for (int i = 0; i < count; i++)
            {
                AttackDef a = def.attacks[i];
                if (!Available(i, a, dist, visible)) continue;
                total += Mathf.Max(0.01f, a.weight);
            }
            if (total <= 0f) return null;
            float r = Random.value * total;
            for (int i = 0; i < count; i++)
            {
                AttackDef a = def.attacks[i];
                if (!Available(i, a, dist, visible)) continue;
                r -= Mathf.Max(0.01f, a.weight);
                if (r <= 0f) { pickedIndex = i; return a; }
            }
            return null;
        }

        int pickedIndex;

        bool Available(int i, AttackDef a, float dist, bool visible)
        {
            if (a.minPhase > phase || Time.time < cdEnd[i]) return false;
            if (dist < a.minRange || dist > a.maxRange) return false;
            if (a.kind == AttackKind.Summon && CountMinions() >= 6) return false;
            if (a.kind != AttackKind.Melee && a.kind != AttackKind.Slam && a.kind != AttackKind.Summon && !visible) return false;
            return true;
        }

        int CountMinions()
        {
            int n = 0;
            for (int i = 0; i < all.Count; i++) if (all[i] != null && all[i].isMinion && all[i].State != EState.Dead) n++;
            return n;
        }

        void StartAttack(AttackDef a)
        {
            cur = a;
            cdEnd[pickedIndex] = Time.time + a.cooldown;
            atkTime = 0f;
            swung = false;
            landed = false;
            hitSet.Clear();
            SetState(EState.Attack);
            if (target != null && a.kind != AttackKind.Barrage) FaceInstant(target.position - transform.position);
            float len = anim.ActionLength(a.clip);
            anim.PlayAction(a.clip, len > 0.01f ? len / Mathf.Max(0.2f, a.duration) : 1f, 0.07f);
            float warn = a.telegraph > 0f ? a.telegraph : a.hitStart;
            if (a.kind == AttackKind.Slam)
                Vfx.Ring(transform.position, a.radius, new Color(1f, 0.25f, 0.15f, 0.8f), warn, RingFx.Mode.Telegraph);
            else if (a.kind == AttackKind.Charge && target != null)
                Vfx.Ring(transform.position + transform.forward * 3f, 2.2f, new Color(1f, 0.35f, 0.15f, 0.7f), warn, RingFx.Mode.Pulse);
            if (a.unblockable) Flash(new Color(1f, 0.1f, 0.1f), warn);
            else if (a.poiseDamage >= 35f) Flash(new Color(1f, 0.75f, 0.2f), 0.2f);
            if (def.rig != EnemyRigKind.Humanoid && Random.value < 0.4f) AudioManager.Instance?.Sfx("growl", transform.position, 0.7f);
            if (a.kind == AttackKind.Summon) AudioManager.Instance?.Sfx("howl", transform.position, 1f);
        }

        void Flash(Color c, float d)
        {
            if (hum != null) hum.Flash(c, d);
            else if (quad != null) quad.Flash(c, d);
        }

        void UpdateAttack(float dt)
        {
            if (cur == null) { SetState(EState.Chase); return; }
            atkTime += dt;
            Vector3 toT = target != null ? target.position - transform.position : transform.forward;
            bool steer = cur.kind != AttackKind.Charge && cur.kind != AttackKind.Leap;
            if (steer && atkTime < cur.hitStart * 0.85f) Face(toT, cur.kind == AttackKind.Melee ? 7f : 5f, dt);

            bool active = atkTime >= cur.hitStart && atkTime <= cur.hitEnd;
            switch (cur.kind)
            {
                case AttackKind.Melee:
                    {
                        float ls = cur.hitStart * 0.35f;
                        if (cur.lunge > 0f && atkTime >= ls && atkTime <= cur.hitEnd)
                        {
                            float spd = cur.lunge / Mathf.Max(0.05f, cur.hitEnd - ls);
                            float d = target != null ? Mathx.FlatDistance(transform.position, target.position) : 9f;
                            velocity = d > 1.1f ? transform.forward * spd : Vector3.zero;
                        }
                        else velocity = Vector3.MoveTowards(velocity, Vector3.zero, 40f * dt);
                        if (active) { if (!swung) { swung = true; AttackSfx(); } DealMelee(cur); }
                        break;
                    }
                case AttackKind.Projectile:
                    velocity = Vector3.MoveTowards(velocity, Vector3.zero, 40f * dt);
                    if (atkTime >= cur.hitStart && !swung) { swung = true; Fire(cur, 1, 0f); }
                    break;
                case AttackKind.Barrage:
                    velocity = Vector3.MoveTowards(velocity, Vector3.zero, 40f * dt);
                    if (atkTime >= cur.hitStart && !swung) { swung = true; Fire(cur, Mathf.Max(2, cur.count), cur.arc); }
                    break;
                case AttackKind.Slam:
                    velocity = Vector3.MoveTowards(velocity, Vector3.zero, 40f * dt);
                    if (atkTime >= cur.hitStart && !swung)
                    {
                        swung = true;
                        Vfx.Play("shockwave", transform.position + Vector3.up * 0.05f, Quaternion.identity, Mathf.Clamp(cur.radius / 3f, 0.8f, 2f));
                        Game.Camera?.Shake(0.14f, 0.3f);
                        AudioManager.Instance?.Sfx("explosion", transform.position, 0.9f);
                        DamageInfo info = MakeInfo(cur);
                        info.direction = transform.forward;
                        Strike.Area(transform.position, cur.radius, 1 << GameLayers.Player, vitals, info, 0.7f);
                    }
                    break;
                case AttackKind.Charge:
                    if (atkTime < cur.hitStart)
                    {
                        velocity = Vector3.MoveTowards(velocity, Vector3.zero, 40f * dt);
                        if (target != null) Face(toT, 3f, dt);
                        chargeDir = transform.forward;
                    }
                    else if (atkTime <= cur.hitEnd)
                    {
                        velocity = chargeDir * cur.speed;
                        if (!swung) { swung = true; AttackSfx(); }
                        DealMelee(cur);
                    }
                    else velocity = Vector3.MoveTowards(velocity, Vector3.zero, 30f * dt);
                    break;
                case AttackKind.Leap:
                    if (atkTime >= cur.hitStart * 0.7f && !swung)
                    {
                        swung = true;
                        float T = Mathf.Max(0.25f, cur.hitEnd - cur.hitStart * 0.7f);
                        Vector3 flat = target != null ? target.position - transform.position : transform.forward * 4f;
                        flat.y = 0f;
                        float dist = Mathf.Min(flat.magnitude, cur.maxRange);
                        leapVel = flat.normalized * (dist / T);
                        vertVel = 24f * T * 0.5f;
                        leapT = T;
                        AttackSfx();
                    }
                    if (swung && atkTime <= cur.hitEnd + 0.1f) velocity = leapVel;
                    else velocity = Vector3.MoveTowards(velocity, Vector3.zero, 40f * dt);
                    if (swung && !landed && atkTime >= cur.hitEnd * 0.8f && cc.isGrounded && vertVel <= 0f)
                    {
                        landed = true;
                        Vfx.Play("dust", transform.position, Quaternion.identity, 1f);
                        DamageInfo info = MakeInfo(cur);
                        info.direction = transform.forward;
                        Strike.Area(transform.position + transform.forward * 0.8f, cur.range + 0.6f, 1 << GameLayers.Player, vitals, info, 0.9f);
                    }
                    else if (swung && !landed && atkTime >= cur.hitStart * 0.7f + 0.15f) DealMelee(cur);
                    break;
                case AttackKind.Summon:
                    velocity = Vector3.MoveTowards(velocity, Vector3.zero, 40f * dt);
                    if (atkTime >= cur.hitStart && !swung) { swung = true; Summon(cur); }
                    break;
                default:
                    velocity = Vector3.MoveTowards(velocity, Vector3.zero, 40f * dt);
                    break;
            }
            Move(dt);
            if (atkTime >= cur.duration)
            {
                cur = null;
                globalCd = (def.elite ? Random.Range(0.5f, 1.1f) : Random.Range(0.35f, 1.0f)) / Mathf.Max(0.5f, speedMul);
                SetState(State == EState.Attack ? (Random.value < def.circleChance ? EState.Circle : EState.Chase) : State);
                if (State == EState.Circle) { strafeSign = Random.value < 0.5f ? -1f : 1f; strafeTime = Random.Range(1f, 2f); }
            }
        }

        void AttackSfx()
        {
            if (def.rig == EnemyRigKind.Humanoid) AudioManager.Instance?.Sfx(cur.heavy ? "heavy_swing" : "swing", transform.position, 0.7f);
            else AudioManager.Instance?.Sfx("growl", transform.position, 0.6f);
        }

        DamageInfo MakeInfo(AttackDef a)
        {
            return new DamageInfo
            {
                power = vitals.Attack * a.damageMul,
                type = a.damageType,
                team = Team.Enemy,
                source = gameObject,
                direction = transform.forward,
                poiseDamage = a.poiseDamage,
                knockback = a.knockback,
                unblockable = a.unblockable,
                heavy = a.poiseDamage >= 40f,
                critChance = 0.04f,
                critDamage = 0f,
                attackerRealm = def.realm,
                tag = a.id
            };
        }

        void DealMelee(AttackDef a)
        {
            if (MeleeHit.Arc(transform.position, transform.forward, a.range, a.arc, 2.4f, 1 << GameLayers.Player, vitals, tmp) == 0) return;
            var list = new List<Vitals>(tmp);
            for (int i = 0; i < list.Count; i++)
            {
                if (!hitSet.Add(list[i])) continue;
                DamageInfo info = MakeInfo(a);
                info.point = list[i].Center;
                info.direction = (list[i].Center - transform.position).normalized;
                Strike.Hit(list[i], info);
            }
        }

        void Fire(AttackDef a, int count, float arc)
        {
            Vector3 origin = transform.position + Vector3.up * 1.3f * Mathf.Max(0.7f, def.scale) + transform.forward * 0.5f;
            Vector3 aim = target != null ? (target.position + Vector3.up * 1.2f - origin) : transform.forward;
            aim.Normalize();
            AudioManager.Instance?.Sfx(a.vfx == "arrow" ? "shoot" : "cast", origin, 0.8f);
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) - 0.5f : 0f;
                Vector3 dir = Quaternion.AngleAxis(t * arc, Vector3.up) * aim;
                DamageInfo info = MakeInfo(a);
                Projectile p = Projectile.Spawn(origin, dir, info, a.speed, 32f, string.IsNullOrEmpty(a.vfx) || a.vfx == "slash" ? "shadow_bolt" : a.vfx, a.vfx == "arrow" ? "hit_spark" : "shadow_burst", a.vfx == "arrow" ? 0.18f : 0.35f, 0f, vitals);
                if (a.vfx != "arrow") p.hitSfx = "enemy_hurt";
            }
        }

        void Summon(AttackDef a)
        {
            if (string.IsNullOrEmpty(a.spawn)) return;
            EnemyDef minion = ContentDB.Enemy(a.spawn);
            if (minion == null) return;
            for (int i = 0; i < Mathf.Max(1, a.count); i++)
            {
                float ang = (i + Random.value * 0.4f) / Mathf.Max(1, a.count) * Mathf.PI * 2f;
                Vector3 p = transform.position + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 4f;
                EnemyBrain b = EnemyFactory.Create(minion, p, "minion_" + Time.frameCount + "_" + i, true);
                if (b != null && target != null) b.Notice(target);
                Vfx.Play("shadow_burst", p + Vector3.up * 0.5f, Quaternion.identity, 1f);
            }
            Game.Camera?.Shake(0.08f, 0.3f);
        }

        // ------------------------------------------------------------------ reactions
        void OnDamaged(DamageInfo info, DamageResult r)
        {
            if (State == EState.Dead) return;
            if (r.dealt > 0f && target == null && info.source != null)
            {
                var t = Game.PlayerObject != null ? Game.PlayerObject.transform : null;
                if (t != null && info.team == Team.Player) Aggro(t);
            }
            if (r.dealt > 0f && State != EState.Attack && State != EState.Stagger && !r.parried)
                anim.PlayAction("hit_light", 1.3f, 0.03f);
            UpdatePhase();
        }

        void OnStaggered(DamageInfo info)
        {
            if (State == EState.Dead) return;
            cur = null;
            SetState(EState.Stagger);
            anim.PlayAction(def.rig == EnemyRigKind.Golem || def.boss ? "hit_heavy" : "stagger", 1f, 0.04f);
            if (hum != null) hum.SetTrail(false);
        }

        void UpdatePhase()
        {
            if (def.phaseThresholds == null || def.phaseThresholds.Length == 0) return;
            float frac = vitals.hp / vitals.MaxHp;
            while (phase < def.phaseThresholds.Length && frac <= def.phaseThresholds[phase])
            {
                phase++;
                speedMul = 1f + 0.12f * phase;
                Game.Camera?.Shake(0.12f, 0.5f);
                Game.UI?.Toast(Loc.T("toast.boss_phase", Loc.T(def.nameKey)));
                AudioManager.Instance?.Sfx("roar", transform.position, 1f);
                Vfx.Ring(transform.position, 6f, new Color(1f, 0.3f, 0.2f, 0.8f), 0.8f);
                vitals.GrantIFrames(0.6f);
            }
        }

        void OnDied(DamageInfo info)
        {
            SetState(EState.Dead);
            deathTimer = 0f;
            cur = null;
            velocity = Vector3.zero;
            alerted.Remove(this);
            if (hum != null) hum.SetTrail(false);
            anim.PlayAction("death", 1f, 0.04f);
            AudioManager.Instance?.Sfx("die_enemy", transform.position, 0.9f);
            if (def.boss) Game.UI?.HideBoss(this);
            cc.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            Game.Manager?.OnEnemyKilled(this);
        }

        void Sink()
        {
            transform.position += Vector3.down * 0.35f * Time.deltaTime;
            if (deathTimer > 11f) Destroy(gameObject);
        }

        public void Knockback(Vector3 v)
        {
            if (State == EState.Dead) return;
            float resist = def != null && (def.boss || def.elite) ? 0.35f : 1f;
            external += v * resist;
        }

        // ------------------------------------------------------------------ locomotion helpers
        void Face(Vector3 dir, float rate, float dt)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            float y = Mathx.DampAngle(transform.eulerAngles.y, Mathx.DirToYaw(dir), rate, dt);
            transform.rotation = Quaternion.Euler(0f, y, 0f);
        }

        void FaceInstant(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Euler(0f, Mathx.DirToYaw(dir), 0f);
        }

        void MoveToward(Vector3 goal, float speed, float dt, float turnRate)
        {
            Vector3 d = goal - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) { Idle(dt, false); return; }
            MoveDir(d.normalized, speed, dt, true, turnRate);
        }

        void MoveDir(Vector3 dir, float speed, float dt, bool face, float turnRate = 8f)
        {
            dir = Avoid(dir);
            velocity = Vector3.MoveTowards(velocity, dir * speed, 26f * dt);
            if (face) Face(dir, turnRate, dt);
            Move(dt);
        }

        Vector3 Avoid(Vector3 dir)
        {
            Vector3 origin = transform.position + Vector3.up * 0.7f;
            float look = 2.4f;
            if (Physics.SphereCast(origin, 0.3f, dir, out RaycastHit hit, look, GameLayers.ObstacleMask, QueryTriggerInteraction.Ignore) && hit.normal.y < 0.6f)
            {
                Vector3 left = Quaternion.AngleAxis(-55f, Vector3.up) * dir;
                Vector3 right = Quaternion.AngleAxis(55f, Vector3.up) * dir;
                bool leftBlocked = Physics.Raycast(origin, left, look, GameLayers.ObstacleMask, QueryTriggerInteraction.Ignore);
                bool rightBlocked = Physics.Raycast(origin, right, look, GameLayers.ObstacleMask, QueryTriggerInteraction.Ignore);
                if (!leftBlocked && (rightBlocked || Random.value < 0.5f)) return left;
                if (!rightBlocked) return right;
                return Quaternion.AngleAxis(120f, Vector3.up) * dir;
            }
            if (Game.World != null)
            {
                Vector3 ahead = transform.position + dir * 1.8f;
                float water = Game.World.WaterHeightAt(ahead);
                if (water > -500f && water - Game.World.GroundHeightAt(ahead) > 0.7f)
                    return Quaternion.AngleAxis(Random.value < 0.5f ? 75f : -75f, Vector3.up) * dir;
            }
            return dir;
        }

        void Move(float dt)
        {
            if (!cc.enabled) return;
            if (cc.isGrounded && vertVel < 0f) vertVel = -3f;
            else vertVel = Mathf.Max(-40f, vertVel - 24f * dt);
            external = Vector3.SmoothDamp(external, Vector3.zero, ref externalDv, 0.2f, 100f, dt);
            cc.Move((velocity + external) * dt + Vector3.up * (vertVel * dt));
        }
    }
}
