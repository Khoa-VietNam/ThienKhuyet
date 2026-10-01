using ThienKhuyet.Audio;
using ThienKhuyet.Characters;
using ThienKhuyet.Core;
using UnityEngine;

namespace ThienKhuyet.Combat
{
    /// <summary>
    /// Central "combat feel": light hit-stop, restrained camera shake, sparks, sounds and flashes triggered by every damage event,
    /// so any damage source (melee, projectile, skill, boss slam) gives consistent feedback.
    /// </summary>
    public static class HitFeedback
    {
        static bool subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            subscribed = false;
        }

        public static void Init()
        {
            if (subscribed) return;
            subscribed = true;
            EventBus.Subscribe<DamageEvent>(OnDamage);
        }

        static void OnDamage(DamageEvent e)
        {
            if (e.info.silent) return;
            var audio = AudioManager.Instance;
            var cam = Game.Camera;
            DamageResult r = e.result;
            DamageInfo i = e.info;
            bool victimIsPlayer = e.victim != null && Game.PlayerObject == e.victim;
            Vector3 pos = e.position;
            Quaternion facing = i.direction.sqrMagnitude > 0.001f ? Quaternion.LookRotation(i.direction) : Quaternion.identity;

            if (r.parried)
            {
                Vfx.Play("parry_flash", pos, facing, 1f);
                if (audio != null) audio.Sfx("parry", pos, 1f, 1f, 0.02f);
                GameFeel.SlowMo(0.18f, 0.16f);
                if (cam != null) { cam.Shake(0.06f, 0.15f); cam.PunchFov(2.5f); }
                return;
            }
            if (r.blocked)
            {
                Vfx.Play("block_spark", pos, facing, 1f);
                if (audio != null) audio.Sfx("block", pos, 0.9f);
                GameFeel.HitStop(0.035f);
                if (cam != null) cam.Shake(0.05f, 0.12f);
                if (victimIsPlayer) Flash(e.victim, new Color(0.6f, 0.8f, 1f), 0.1f);
                return;
            }
            if (r.dealt <= 0f) return;

            bool fromPlayer = i.team == Team.Player;
            string vfx = i.heavy || r.crit ? "impact" : "hit_spark";
            Vfx.Play(vfx, pos, facing, r.crit ? 1.4f : 1f);
            if (fromPlayer)
            {
                if (audio != null) audio.Sfx(i.heavy ? "hit_heavy" : "hit", pos, 0.95f);
                float stop = i.heavy ? 0.085f : 0.05f;
                if (r.crit) stop += 0.025f;
                if (r.killed) stop += 0.04f;
                GameFeel.HitStop(stop);
                if (cam != null) cam.Shake(i.heavy ? 0.13f : 0.06f, i.heavy ? 0.22f : 0.12f);
                Flash(e.victim, Color.white * (r.crit ? 1.6f : 1.1f), 0.13f);
            }
            else
            {
                if (victimIsPlayer)
                {
                    if (audio != null) audio.Sfx2D("hurt_player", 0.9f);
                    GameFeel.HitStop(0.06f);
                    if (cam != null) cam.Shake(Mathf.Min(0.25f, 0.08f + r.dealt * 0.002f), 0.2f);
                    Flash(e.victim, new Color(1f, 0.25f, 0.2f), 0.18f);
                    Game.PostFx?.HitPulse(Mathf.Clamp01(r.dealt / 60f + 0.25f));
                }
                else if (audio != null) audio.Sfx(i.heavy ? "hit_heavy" : "enemy_hurt", pos, 0.6f);
            }
        }

        public static void Flash(GameObject victim, Color color, float duration)
        {
            if (victim == null) return;
            var h = victim.GetComponent<HumanoidAnimator>();
            if (h != null) { h.Flash(color, duration); return; }
            var q = victim.GetComponent<QuadrupedAnimator>();
            if (q != null) q.Flash(color, duration);
        }
    }
}
