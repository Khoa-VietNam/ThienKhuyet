using UnityEngine;

namespace ThienKhuyet.Core
{
    /// <summary>Small math helpers shared by gameplay, camera and cinematics (frame-rate independent damping, easing).</summary>
    public static class Mathx
    {
        public static float Damp(float current, float target, float lambda, float dt)
        {
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-lambda * dt));
        }

        public static Vector3 Damp(Vector3 current, Vector3 target, float lambda, float dt)
        {
            return Vector3.Lerp(current, target, 1f - Mathf.Exp(-lambda * dt));
        }

        public static Quaternion Damp(Quaternion current, Quaternion target, float lambda, float dt)
        {
            return Quaternion.Slerp(current, target, 1f - Mathf.Exp(-lambda * dt));
        }

        public static float DampAngle(float current, float target, float lambda, float dt)
        {
            return Mathf.LerpAngle(current, target, 1f - Mathf.Exp(-lambda * dt));
        }

        public static float SmoothStep01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        public static float SmootherStep01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        public static float EaseOutCubic(float t)
        {
            t = 1f - Mathf.Clamp01(t);
            return 1f - t * t * t;
        }

        public static float EaseInCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }

        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t) - 1f;
            const float s = 1.70158f;
            return t * t * ((s + 1f) * t + s) + 1f;
        }

        public static float Ease(EaseKind kind, float t)
        {
            switch (kind)
            {
                case EaseKind.In: return EaseInCubic(t);
                case EaseKind.Out: return EaseOutCubic(t);
                case EaseKind.InOut: return SmootherStep01(t);
                case EaseKind.Smooth: return SmoothStep01(t);
                default: return Mathf.Clamp01(t);
            }
        }

        public static float Remap(float v, float a, float b, float c, float d)
        {
            if (Mathf.Approximately(a, b)) return c;
            return c + (d - c) * ((v - a) / (b - a));
        }

        public static float Remap01(float v, float a, float b)
        {
            if (Mathf.Approximately(a, b)) return 0f;
            return Mathf.Clamp01((v - a) / (b - a));
        }

        public static float Wrap180(float a)
        {
            a %= 360f;
            if (a > 180f) a -= 360f;
            else if (a < -180f) a += 360f;
            return a;
        }

        public static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        public static Vector3 FlatDir(Vector3 v)
        {
            v.y = 0f;
            float m = v.sqrMagnitude;
            return m > 1e-8f ? v / Mathf.Sqrt(m) : Vector3.zero;
        }

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Signed planar angle (degrees) from forward direction a to direction b around +Y.</summary>
        public static float SignedAngleFlat(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            if (a.sqrMagnitude < 1e-8f || b.sqrMagnitude < 1e-8f) return 0f;
            return Vector3.SignedAngle(a, b, Vector3.up);
        }

        public static Vector3 YawToDir(float yawDeg)
        {
            float r = yawDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
        }

        public static float DirToYaw(Vector3 d)
        {
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        public static float Hash01(int a, int b, int seed)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393 + b * 668265263 + seed * 362437);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }

    public enum EaseKind
    {
        Linear = 0,
        In = 1,
        Out = 2,
        InOut = 3,
        Smooth = 4
    }
}
