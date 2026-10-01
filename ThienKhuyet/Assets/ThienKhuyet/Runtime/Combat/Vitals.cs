using System;
using System.Collections.Generic;
using ThienKhuyet.Core;
using ThienKhuyet.Data;
using UnityEngine;

namespace ThienKhuyet.Combat
{
    public enum GuardOutcome { None = 0, Blocked, Parried, Dodged, GuardBroken }

    public struct GuardResult
    {
        public GuardOutcome outcome;
        public float reduction;   // 0..1 damage reduction for Blocked
        public static GuardResult None => new GuardResult { outcome = GuardOutcome.None };
    }

    public interface IKnockbackable
    {
        void Knockback(Vector3 velocity);
    }

    /// <summary>Timed status effects: burn (DOT), slow, stun, shield (absorbs damage), weak (takes more damage).</summary>
    public sealed class StatusSet
    {
        struct Entry
        {
            public string id;
            public float remaining;
            public float power;
            public float tick;
        }

        readonly List<Entry> list = new List<Entry>(4);

        public bool Any => list.Count > 0;

        public void Apply(string id, float duration, float power)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].id != id) continue;
                Entry e = list[i];
                e.remaining = Mathf.Max(e.remaining, duration);
                e.power = Mathf.Max(e.power, power);
                list[i] = e;
                return;
            }
            list.Add(new Entry { id = id, remaining = duration, power = power });
        }

        public bool Has(string id)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].id == id) return true;
            return false;
        }

        public float Power(string id)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].id == id) return list[i].power;
            return 0f;
        }

        public float MoveMultiplier
        {
            get
            {
                float m = 1f;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].id == "slow") m *= 1f - Mathf.Clamp01(list[i].power);
                    else if (list[i].id == "stun") m = 0f;
                }
                return m;
            }
        }

        public bool Stunned => Has("stun");

        /// <summary>Absorbs damage into the shield; returns the remaining damage.</summary>
        public float AbsorbShield(float damage)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].id != "shield") continue;
                Entry e = list[i];
                float absorbed = Mathf.Min(e.power, damage);
                e.power -= absorbed;
                damage -= absorbed;
                if (e.power <= 0.01f) e.remaining = 0f;
                list[i] = e;
                break;
            }
            return damage;
        }

        public void Clear()
        {
            list.Clear();
        }

        public void Update(float dt, Vitals owner)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Entry e = list[i];
                e.remaining -= dt;
                if (e.id == "burn")
                {
                    e.tick -= dt;
                    if (e.tick <= 0f)
                    {
                        e.tick += 0.5f;
                        owner.DamageDirect(e.power * 6f, DamageType.Fire);
                    }
                }
                if (e.remaining <= 0f) list.RemoveAt(i);
                else list[i] = e;
            }
        }
    }

    /// <summary>Health, stamina, Qi and poise of any combatant (player or enemy) and the damage resolution pipeline.</summary>
    public sealed class Vitals : MonoBehaviour
    {
        struct Buff
        {
            public string id;
            public float remaining;
        }

        public Team team = Team.Enemy;
        public StatBlock stats = new StatBlock();
        public int realm;
        public float hp = 100f, stamina = 100f, qi = 50f, poise = 40f;
        public bool invulnerable;
        public bool staminaRegenBlocked;
        public float damageTakenMultiplier = 1f;
        public float stunResist;
        public readonly StatusSet status = new StatusSet();
        public Func<DamageInfo, GuardResult> guard;
        public Transform aimPoint;

        public event Action<DamageInfo, DamageResult> Damaged;
        public event Action<DamageInfo> Died;
        public event Action<DamageInfo> Staggered;

        readonly List<Buff> buffs = new List<Buff>();
        IKnockbackable knock;
        float iFrameUntil;
        float staminaDelay;
        float lastHitTime = -10f;
        float lastStaggerTime = -10f;
        bool dead;
        Collider col;

        public float MaxHp => Mathf.Max(1f, stats.Get(StatId.MaxHp));
        public float MaxStamina => Mathf.Max(1f, stats.Get(StatId.MaxStamina));
        public float MaxQi => Mathf.Max(0f, stats.Get(StatId.MaxQi));
        public float MaxPoise => Mathf.Max(1f, stats.Get(StatId.Poise));
        public bool IsAlive => !dead && hp > 0f;
        public bool IsDead => dead;
        public bool HasIFrames => Time.time < iFrameUntil || invulnerable;
        public float Attack => stats.Get(StatId.Attack);
        public float SpellPower => stats.Get(StatId.SpellPower);
        public float Defense => stats.Get(StatId.Defense);
        public float TimeSinceHit => Time.time - lastHitTime;

        public Vector3 Center
        {
            get
            {
                if (aimPoint != null) return aimPoint.position;
                if (col != null) return col.bounds.center;
                return transform.position + Vector3.up * 1f;
            }
        }

        public float Radius => col != null ? Mathf.Max(col.bounds.extents.x, col.bounds.extents.z) : 0.4f;

        void Awake()
        {
            knock = GetComponent<IKnockbackable>();
            col = GetComponent<Collider>();
        }

        public void SetFull()
        {
            dead = false;
            hp = MaxHp;
            stamina = MaxStamina;
            qi = Mathf.Min(MaxQi, Mathf.Max(qi, MaxQi * 0.5f));
            poise = MaxPoise;
            status.Clear();
        }

        public void Revive(float hpFraction)
        {
            dead = false;
            hp = Mathf.Max(1f, MaxHp * hpFraction);
            stamina = MaxStamina;
            poise = MaxPoise;
            status.Clear();
        }

        public void GrantIFrames(float seconds)
        {
            iFrameUntil = Mathf.Max(iFrameUntil, Time.time + seconds);
        }

        public void CancelIFrames()
        {
            iFrameUntil = 0f;
        }

        // ------------------------------------------------------------------ resources
        public void Heal(float amount)
        {
            if (dead) return;
            hp = Mathf.Min(MaxHp, hp + amount);
        }

        public void RestoreQi(float amount)
        {
            qi = Mathf.Clamp(qi + amount, 0f, MaxQi);
        }

        public void RestoreStamina(float amount)
        {
            stamina = Mathf.Clamp(stamina + amount, 0f, MaxStamina);
        }

        public bool SpendStamina(float amount)
        {
            if (amount <= 0f) return true;
            if (stamina < Mathf.Min(amount, 8f)) return false;   // may dip below zero for the last action
            stamina = Mathf.Max(0f, stamina - amount);
            staminaDelay = 0.75f;
            return true;
        }

        public bool SpendQi(float amount)
        {
            if (qi < amount) return false;
            qi -= amount;
            return true;
        }

        public void AddTimedBuff(string id, IList<StatMod> mods, float duration)
        {
            stats.SetSource("buff:" + id, mods);
            for (int i = 0; i < buffs.Count; i++)
            {
                if (buffs[i].id != id) continue;
                buffs[i] = new Buff { id = id, remaining = duration };
                return;
            }
            buffs.Add(new Buff { id = id, remaining = duration });
        }

        public bool HasBuff(string id)
        {
            for (int i = 0; i < buffs.Count; i++) if (buffs[i].id == id) return true;
            return false;
        }

        // ------------------------------------------------------------------ damage
        /// <summary>Damage that bypasses guard, i-frames, poise and knockback (burn, falling, environment).</summary>
        public void DamageDirect(float amount, DamageType type)
        {
            if (dead || amount <= 0f) return;
            float left = status.AbsorbShield(amount);
            hp -= left;
            if (left > 0f)
            {
                Damaged?.Invoke(new DamageInfo { power = amount, type = type, silent = true, point = Center }, new DamageResult { dealt = left });
                EventBus.Publish(new DamageEvent { info = new DamageInfo { power = amount, type = type, silent = true, point = Center }, result = new DamageResult { dealt = left }, position = Center, victim = gameObject });
            }
            if (hp <= 0f) Die(new DamageInfo { type = type, silent = true });
        }

        public DamageResult TakeDamage(DamageInfo info)
        {
            var result = new DamageResult();
            if (dead) { result.ignored = true; return result; }
            if (HasIFrames) { result.dodged = true; return result; }

            float guardReduction = 0f;
            if (guard != null)
            {
                GuardResult g = guard(info);
                switch (g.outcome)
                {
                    case GuardOutcome.Dodged: result.dodged = true; return result;
                    case GuardOutcome.Parried: result.parried = true; return Finish(info, result, true);
                    case GuardOutcome.Blocked: result.blocked = true; guardReduction = g.reduction; break;
                    case GuardOutcome.GuardBroken: result.blocked = true; guardReduction = 0.35f; info.poiseDamage += MaxPoise; break;
                }
            }

            bool crit = info.critChance > 0f && UnityEngine.Random.value < info.critChance;
            float dmg = DamageCalc.Final(info.power, Defense, info.attackerRealm, realm, info.type, 0f, crit, info.critDamage, guardReduction);
            dmg *= damageTakenMultiplier;
            if (status.Has("weak")) dmg *= 1f + status.Power("weak");
            dmg = status.AbsorbShield(dmg);
            result.crit = crit;
            result.dealt = dmg;
            hp -= dmg;
            lastHitTime = Time.time;

            // poise: heavy blows break it and cause a stagger
            float poiseDmg = info.poiseDamage * (1f - Mathf.Clamp01(stunResist));
            if (result.blocked) poiseDmg *= 0.25f;
            poise -= poiseDmg;
            if (poise <= 0f && hp > 0f)
            {
                poise = MaxPoise;
                result.staggered = true;
                lastStaggerTime = Time.time;
            }
            if (info.type == DamageType.Lightning && stunResist < 0.9f && info.heavy) status.Apply("stun", 0.6f, 1f);

            if (knock != null && info.knockback > 0f && !info.silent)
            {
                float k = info.knockback * (result.staggered ? 1.2f : 0.55f) * (result.blocked ? 0.5f : 1f);
                Vector3 dir = info.direction;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f) knock.Knockback(dir.normalized * k);
            }
            return Finish(info, result, false);
        }

        DamageResult Finish(DamageInfo info, DamageResult result, bool parried)
        {
            if (hp <= 0f)
            {
                result.killed = true;
                Damaged?.Invoke(info, result);
                EventBus.Publish(new DamageEvent { info = info, result = result, position = info.point == Vector3.zero ? Center : info.point, victim = gameObject });
                Die(info);
                return result;
            }
            Damaged?.Invoke(info, result);
            EventBus.Publish(new DamageEvent { info = info, result = result, position = info.point == Vector3.zero ? Center : info.point, victim = gameObject });
            if (result.staggered) Staggered?.Invoke(info);
            return result;
        }

        /// <summary>Forces a stagger (parry success, shield break).</summary>
        public void Stagger(DamageInfo cause)
        {
            if (dead) return;
            poise = MaxPoise;
            lastStaggerTime = Time.time;
            Staggered?.Invoke(cause);
        }

        void Die(DamageInfo cause)
        {
            if (dead) return;
            dead = true;
            hp = 0f;
            Died?.Invoke(cause);
        }

        // ------------------------------------------------------------------ regen
        void Update()
        {
            if (dead) return;
            float dt = Time.deltaTime;
            status.Update(dt, this);
            if (dead) return;

            if (!staminaRegenBlocked)
            {
                if (staminaDelay > 0f) staminaDelay -= dt;
                else stamina = Mathf.Min(MaxStamina, stamina + stats.Get(StatId.StaminaRegen) * dt);
            }
            float hr = stats.Get(StatId.HpRegen);
            if (hr > 0f) hp = Mathf.Min(MaxHp, hp + hr * dt);
            float qr = stats.Get(StatId.QiRegen);
            if (qr > 0f) qi = Mathf.Min(MaxQi, qi + qr * dt);
            if (Time.time - lastHitTime > 3f) poise = Mathf.Min(MaxPoise, poise + 22f * dt);

            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                Buff b = buffs[i];
                b.remaining -= dt;
                if (b.remaining <= 0f)
                {
                    stats.RemoveSource("buff:" + b.id);
                    buffs.RemoveAt(i);
                }
                else buffs[i] = b;
            }
            // clamp to maxima when stats change (e.g. unequipping gear)
            if (hp > MaxHp) hp = MaxHp;
            if (stamina > MaxStamina) stamina = MaxStamina;
            if (qi > MaxQi) qi = MaxQi;
        }

        public void ClearBuffs()
        {
            for (int i = 0; i < buffs.Count; i++) stats.RemoveSource("buff:" + buffs[i].id);
            buffs.Clear();
        }
    }

    /// <summary>Shared hit-detection helpers for melee arcs and area effects.</summary>
    public static class MeleeHit
    {
        static readonly Collider[] buffer = new Collider[64];

        /// <summary>Finds living Vitals inside a flat arc in front of 'origin' (range, arc degrees, vertical tolerance).</summary>
        public static int Arc(Vector3 origin, Vector3 forward, float range, float arcDeg, float height, int mask, Vitals self, List<Vitals> results)
        {
            results.Clear();
            Vector3 center = origin + Vector3.up * 0.9f;
            int n = Physics.OverlapSphereNonAlloc(center, range + 1.2f, buffer, mask, QueryTriggerInteraction.Ignore);
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
            forward.Normalize();
            float cosHalf = Mathf.Cos(Mathf.Clamp(arcDeg, 1f, 360f) * 0.5f * Mathf.Deg2Rad);
            for (int i = 0; i < n; i++)
            {
                Vitals v = buffer[i].GetComponentInParent<Vitals>();
                if (v == null || v == self || !v.IsAlive || results.Contains(v)) continue;
                Vector3 to = v.Center - origin;
                float dy = Mathf.Abs(to.y - 0.9f);
                to.y = 0f;
                float dist = to.magnitude;
                float reach = range + v.Radius;
                if (dist > reach || dy > height) continue;
                if (arcDeg < 359f && dist > 0.35f)
                {
                    float cos = Vector3.Dot(forward, to / dist);
                    if (cos < cosHalf) continue;
                }
                results.Add(v);
            }
            return results.Count;
        }

        public static int Sphere(Vector3 center, float radius, int mask, Vitals self, List<Vitals> results)
        {
            results.Clear();
            int n = Physics.OverlapSphereNonAlloc(center, radius, buffer, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Vitals v = buffer[i].GetComponentInParent<Vitals>();
                if (v == null || v == self || !v.IsAlive || results.Contains(v)) continue;
                results.Add(v);
            }
            return results.Count;
        }
    }
}
