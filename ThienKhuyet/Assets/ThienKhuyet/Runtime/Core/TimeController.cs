using System.Collections.Generic;
using UnityEngine;

namespace ThienKhuyet.Core
{
    /// <summary>
    /// Single owner of Time.timeScale. Gameplay hit-stop, parry slow-motion, cinematic time scale and pause are separate
    /// sources combined by taking the minimum, so they never fight each other.
    /// </summary>
    public sealed class TimeController : MonoBehaviour
    {
        struct Pulse { public float scale; public float until; }

        readonly List<Pulse> pulses = new List<Pulse>();
        float cinematic = 1f;
        bool paused;

        public static TimeController Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }

        public static TimeController Create(Transform parent)
        {
            var go = new GameObject("TimeController");
            go.transform.SetParent(parent, false);
            Instance = go.AddComponent<TimeController>();
            return Instance;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        public bool Paused
        {
            get => paused;
            set { paused = value; Apply(); }
        }

        public float CinematicScale
        {
            get => cinematic;
            set { cinematic = Mathf.Clamp(value, 0.0f, 4f); Apply(); }
        }

        /// <summary>Slows time to 'scale' for 'seconds' of real time (hit-stop, parry flash).</summary>
        public void Pulse_(float scale, float seconds)
        {
            pulses.Add(new Pulse { scale = scale, until = Time.unscaledTime + seconds });
            Apply();
        }

        void Update()
        {
            if (pulses.Count > 0)
            {
                float now = Time.unscaledTime;
                for (int i = pulses.Count - 1; i >= 0; i--)
                    if (pulses[i].until <= now) pulses.RemoveAt(i);
            }
            Apply();
        }

        void Apply()
        {
            float s = 1f;
            if (paused) s = 0f;
            else
            {
                s = cinematic;
                for (int i = 0; i < pulses.Count; i++) s = Mathf.Min(s, pulses[i].scale);
            }
            if (!Mathf.Approximately(Time.timeScale, s)) Time.timeScale = s;
        }
    }

    /// <summary>Small static facade for "game feel" effects used by combat.</summary>
    public static class GameFeel
    {
        public static void HitStop(float seconds, float scale = 0.04f)
        {
            if (TimeController.Instance != null && seconds > 0f) TimeController.Instance.Pulse_(scale, seconds);
        }

        public static void SlowMo(float scale, float seconds)
        {
            if (TimeController.Instance != null) TimeController.Instance.Pulse_(scale, seconds);
        }
    }
}
