using System.Collections.Generic;
using ThienKhuyet.Combat;
using ThienKhuyet.Core;
using UnityEngine;

namespace ThienKhuyet.Player
{
    /// <summary>Target lock: picks the enemy closest to the screen centre, keeps it through occlusion briefly, cycles on a mouse flick.</summary>
    public sealed class LockOn
    {
        static readonly Collider[] buffer = new Collider[48];
        readonly List<Vitals> candidates = new List<Vitals>(16);
        float switchCooldown;
        float lostTimer;

        public Vitals Target { get; private set; }
        public bool Active => Target != null;

        public const float AcquireRange = 28f;
        public const float DropRange = 36f;

        public void Drop()
        {
            Target = null;
            lostTimer = 0f;
        }

        public void Toggle(Transform player, Camera cam)
        {
            if (Target != null) Drop();
            else Target = Best(player, cam, null);
        }

        public void Tick(Transform player, Camera cam, float dt, Vector2 lookDelta)
        {
            if (Target == null) return;
            switchCooldown -= dt;
            bool valid = Target.IsAlive && Target.gameObject.activeInHierarchy;
            if (valid)
            {
                float d = Vector3.Distance(player.position, Target.transform.position);
                valid = d <= DropRange;
                if (valid)
                {
                    bool visible = HasLineOfSight(player, Target);
                    lostTimer = visible ? 0f : lostTimer + dt;
                    if (lostTimer > 2.2f) valid = false;
                }
            }
            if (!valid)
            {
                Vitals next = Best(player, cam, Target);
                Target = next != null && Vector3.Distance(player.position, next.transform.position) < 14f ? next : null;
                return;
            }
            // flick the camera sideways to switch targets
            float flick = lookDelta.x;
            if (switchCooldown <= 0f && Mathf.Abs(flick) > 22f)
            {
                Vitals next = Cycle(player, cam, flick > 0f ? 1 : -1);
                if (next != null) { Target = next; switchCooldown = 0.45f; }
            }
        }

        static bool HasLineOfSight(Transform player, Vitals v)
        {
            Vector3 from = player.position + Vector3.up * 1.4f;
            Vector3 to = v.Center;
            Vector3 d = to - from;
            return !Physics.Raycast(from, d.normalized, d.magnitude - 0.5f, GameLayers.ObstacleMask, QueryTriggerInteraction.Ignore);
        }

        Vitals Best(Transform player, Camera cam, Vitals exclude)
        {
            Gather(player);
            Vitals best = null;
            float bestScore = float.MaxValue;
            Vector3 camFwd = cam != null ? cam.transform.forward : player.forward;
            camFwd.y = 0f;
            camFwd.Normalize();
            for (int i = 0; i < candidates.Count; i++)
            {
                Vitals v = candidates[i];
                if (v == exclude) continue;
                Vector3 to = v.transform.position - player.position;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist < 0.1f) continue;
                float ang = Vector3.Angle(camFwd, to / dist);
                if (ang > 80f && dist > 4f) continue;
                if (!HasLineOfSight(player, v)) continue;
                float score = ang + dist * 0.8f;
                if (score < bestScore) { bestScore = score; best = v; }
            }
            return best;
        }

        Vitals Cycle(Transform player, Camera cam, int dir)
        {
            Gather(player);
            Vitals best = null;
            float bestAng = float.MaxValue;
            Vector3 camFwd = cam.transform.forward;
            camFwd.y = 0f;
            Vector3 curDir = Target.transform.position - player.position;
            curDir.y = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                Vitals v = candidates[i];
                if (v == Target || !HasLineOfSight(player, v)) continue;
                Vector3 to = v.transform.position - player.position;
                to.y = 0f;
                float signed = Vector3.SignedAngle(curDir, to, Vector3.up);
                if (Mathf.Sign(signed) != dir) continue;
                float a = Mathf.Abs(signed);
                if (a < bestAng) { bestAng = a; best = v; }
            }
            return best;
        }

        void Gather(Transform player)
        {
            candidates.Clear();
            int n = Physics.OverlapSphereNonAlloc(player.position, AcquireRange, buffer, 1 << GameLayers.Enemy, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Vitals v = buffer[i].GetComponentInParent<Vitals>();
                if (v != null && v.IsAlive && !candidates.Contains(v)) candidates.Add(v);
            }
        }
    }
}
