using System.Collections.Generic;
using ThienKhuyet.Audio;
using ThienKhuyet.Core;
using ThienKhuyet.Data;
using UnityEngine;

namespace ThienKhuyet.Combat
{
    /// <summary>Shared helpers to deliver damage with status effects.</summary>
    public static class Strike
    {
        static readonly List<Vitals> tmp = new List<Vitals>(16);

        public static DamageResult Hit(Vitals target, DamageInfo info, string status = null, float statusDuration = 0f, float statusPower = 0f)
        {
            if (target == null) return new DamageResult { ignored = true };
            if (info.point == Vector3.zero) info.point = target.Center;
            DamageResult r = target.TakeDamage(info);
            if (!string.IsNullOrEmpty(status) && !r.ignored && !r.dodged && !r.parried && target.IsAlive)
            {
                if (status == "stun" && target.stunResist >= 0.9f) { }
                else target.status.Apply(status, statusDuration * (status == "stun" ? 1f - target.stunResist : 1f), statusPower);
            }
            return r;
        }

        public static int Area(Vector3 center, float radius, int mask, Vitals self, DamageInfo info, float edgeFalloff = 0.65f, string status = null, float statusDuration = 0f, float statusPower = 0f)
        {
            int n = MeleeHit.Sphere(center, radius, mask, self, tmp);
            int hits = 0;
            var copy = new List<Vitals>(tmp);
            for (int i = 0; i < copy.Count; i++)
            {
                Vitals v = copy[i];
                Vector3 to = v.Center - center;
                float d = to.magnitude;
                DamageInfo di = info;
                float k = Mathf.Lerp(1f, edgeFalloff, Mathf.Clamp01(d / Mathf.Max(0.01f, radius)));
                di.power *= k;
                di.direction = d > 0.01f ? to / d : Vector3.forward;
                di.point = v.Center;
                DamageResult r = Hit(v, di, status, statusDuration, statusPower);
                if (!r.ignored && !r.dodged) hits++;
            }
            return hits;
        }

        public static int OpposingMask(Team team)
        {
            return team == Team.Player ? (1 << GameLayers.Enemy) : (team == Team.Enemy ? (1 << GameLayers.Player) : GameLayers.DamageableMask);
        }
    }

    /// <summary>Pooled projectile (qi bolts, fireballs, arrows, sword waves). Sweeps a sphere each frame, explodes in an optional radius.</summary>
    public sealed class Projectile : MonoBehaviour
    {
        public DamageInfo info;
        public float speed = 20f;
        public float maxRange = 25f;
        public float hitRadius = 0.4f;
        public float explosionRadius;
        public string fxId = "qi_bolt";
        public string hitFxId = "qi_burst";
        public string hitSfx = "hit";
        public string status;
        public float statusDuration, statusPower;
        public float homingStrength;
        public Transform homingTarget;
        public bool pierce;
        public Vitals owner;

        Vector3 dir;
        float traveled;
        GameObject fx;
        readonly HashSet<Vitals> pierced = new HashSet<Vitals>();
        bool active;

        static GameObjectPool Pool => Pools.Get("projectile", () =>
        {
            var go = new GameObject("Projectile");
            go.layer = GameLayers.Projectile;
            go.AddComponent<Projectile>();
            return go;
        });

        public static Projectile Spawn(Vector3 pos, Vector3 direction, DamageInfo info, float speed, float range, string fxId, string hitFx, float hitRadius = 0.4f, float explosion = 0f, Vitals owner = null)
        {
            GameObject go = Pool.Get(pos, Quaternion.LookRotation(direction));
            var p = go.GetComponent<Projectile>();
            p.info = info;
            p.speed = speed;
            p.maxRange = range;
            p.fxId = fxId;
            p.hitFxId = hitFx;
            p.hitRadius = hitRadius;
            p.explosionRadius = explosion;
            p.owner = owner;
            p.dir = direction.normalized;
            p.traveled = 0f;
            p.status = null;
            p.statusDuration = p.statusPower = 0f;
            p.homingStrength = 0f;
            p.homingTarget = null;
            p.pierce = false;
            p.hitSfx = "hit";
            p.pierced.Clear();
            p.active = true;
            p.fx = Vfx.Play(fxId, pos, Quaternion.LookRotation(direction));
            return p;
        }

        void Update()
        {
            if (!active) return;
            float dt = Time.deltaTime;
            float step = speed * dt;
            if (step <= 0f) return;
            Vector3 pos = transform.position;
            if (homingTarget != null && homingStrength > 0f)
            {
                Vector3 want = (homingTarget.position + Vector3.up * 1.0f - pos).normalized;
                dir = Vector3.RotateTowards(dir, want, homingStrength * dt, 0f).normalized;
            }
            int mask = GameLayers.ObstacleMask | Strike.OpposingMask(info.team);
            if (Physics.SphereCast(pos, hitRadius, dir, out RaycastHit hit, step, mask, QueryTriggerInteraction.Ignore))
            {
                Vitals v = hit.collider.GetComponentInParent<Vitals>();
                if (v != null && !v.IsAlive) { MoveOn(pos, step); return; }
                if (v != null && pierce && pierced.Contains(v)) { MoveOn(pos, step); return; }
                transform.position = hit.point + hit.normal * 0.05f;
                Impact(v, hit.point);
                return;
            }
            MoveOn(pos, step);
        }

        void MoveOn(Vector3 pos, float step)
        {
            pos += dir * step;
            traveled += step;
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir));
            if (fx != null) fx.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir));
            if (traveled >= maxRange) Finish(pos, false);
        }

        void Impact(Vitals v, Vector3 point)
        {
            DamageInfo di = info;
            di.point = point;
            di.direction = dir;
            if (v != null)
            {
                Strike.Hit(v, di, status, statusDuration, statusPower);
                if (pierce) { pierced.Add(v); transform.position = point + dir * (hitRadius + 0.2f); return; }
            }
            if (explosionRadius > 0f)
            {
                Vector3 c = point;
                DamageInfo area = di;
                area.silent = false;
                Strike.Area(c, explosionRadius, Strike.OpposingMask(info.team), owner, area, 0.6f, status, statusDuration, statusPower);
            }
            Finish(point, true);
        }

        void Finish(Vector3 point, bool impact)
        {
            active = false;
            if (fx != null) { Vfx.Stop(fx); fx = null; }
            if (impact)
            {
                if (!string.IsNullOrEmpty(hitFxId)) Vfx.Play(hitFxId, point, Quaternion.identity, explosionRadius > 0f ? Mathf.Clamp(explosionRadius * 0.45f, 0.8f, 3f) : 1f);
                if (AudioManager.Instance != null) AudioManager.Instance.Sfx(explosionRadius > 0f ? "explosion" : hitSfx, point, 0.8f);
            }
            Pool.Release(gameObject);
        }

        void OnDisable()
        {
            active = false;
            if (fx != null) { Vfx.Stop(fx); fx = null; }
        }
    }
}
