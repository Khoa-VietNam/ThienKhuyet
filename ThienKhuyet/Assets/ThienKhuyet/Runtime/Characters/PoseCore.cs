using System;
using System.Collections.Generic;
using ThienKhuyet.Core;
using UnityEngine;

namespace ThienKhuyet.Characters
{
    /// <summary>Joint layout of a rig type. Joint codes are used by the pose script format.</summary>
    public sealed class RigSpec
    {
        public readonly string[] codes;
        public readonly float[] mirror; // +1 for centre/right joints, -1 for left joints (y and z euler are negated)
        readonly Dictionary<string, int> index = new Dictionary<string, int>();

        public RigSpec(string[] codes, string[] leftCodes)
        {
            this.codes = codes;
            mirror = new float[codes.Length];
            var left = new HashSet<string>(leftCodes);
            for (int i = 0; i < codes.Length; i++)
            {
                index[codes[i]] = i;
                mirror[i] = left.Contains(codes[i]) ? -1f : 1f;
            }
        }

        public int Count => codes.Length;

        public int IndexOf(string code)
        {
            return index.TryGetValue(code, out int i) ? i : -1;
        }
    }

    public static class Rigs
    {
        // Joint order shared by builder, animator and pose scripts.
        public const int Hp = 0, Sp = 1, Ch = 2, Nk = 3, Hd = 4, ArR = 5, FoR = 6, HaR = 7, ArL = 8, FoL = 9, HaL = 10,
            ThR = 11, KnR = 12, AnR = 13, ThL = 14, KnL = 15, AnL = 16;

        public static readonly RigSpec Humanoid = new RigSpec(
            new[] { "Hp", "Sp", "Ch", "Nk", "Hd", "ArR", "FoR", "HaR", "ArL", "FoL", "HaL", "ThR", "KnR", "AnR", "ThL", "KnL", "AnL" },
            new[] { "ArL", "FoL", "HaL", "ThL", "KnL", "AnL" });

        // Quadruped: body, spine, chest, neck, head, jaw, tail1-3, front legs (upper, lower, paw) R/L, hind legs R/L
        public const int QBody = 0, QSpine = 1, QChest = 2, QNeck = 3, QHead = 4, QJaw = 5, QTail1 = 6, QTail2 = 7, QTail3 = 8,
            QFlR = 9, QFlRl = 10, QFlRp = 11, QFlL = 12, QFlLl = 13, QFlLp = 14,
            QHlR = 15, QHlRl = 16, QHlRp = 17, QHlL = 18, QHlLl = 19, QHlLp = 20;

        public static readonly RigSpec Quadruped = new RigSpec(
            new[] { "Bd", "Sp", "Ch", "Nk", "Hd", "Jw", "T1", "T2", "T3", "FuR", "FlR", "FpR", "FuL", "FlL", "FpL", "HuR", "HlR", "HpR", "HuL", "HlL", "HpL" },
            new[] { "FuL", "FlL", "FpL", "HuL", "HlL", "HpL" });
    }

    /// <summary>Pure-managed quaternion helpers (identical results in the Editor and in headless unit tests).</summary>
    public static class Q
    {
        public static Quaternion EulerZXY(float xDeg, float yDeg, float zDeg)
        {
            const float h = Mathf.Deg2Rad * 0.5f;
            float sx = (float)Math.Sin(xDeg * h), cx = (float)Math.Cos(xDeg * h);
            float sy = (float)Math.Sin(yDeg * h), cy = (float)Math.Cos(yDeg * h);
            float sz = (float)Math.Sin(zDeg * h), cz = (float)Math.Cos(zDeg * h);
            // q = qy * qx * qz
            float ax = sx * cz, ay = 0f, az = 0f, aw = cx * cz;           // qx * qz
            az = cx * sz;
            ay = -sx * sz;
            // Hamilton product qy * (qx*qz) with qy = (0, sy, 0, cy)
            float x = cy * ax + sy * az;
            float y = cy * ay + sy * aw;
            float z = cy * az - sy * ax;
            float w = cy * aw - sy * ay;
            return new Quaternion(x, y, z, w);
        }

        public static Quaternion Nlerp(Quaternion a, Quaternion b, float t)
        {
            float dot = a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
            float s = dot < 0f ? -1f : 1f;
            float x = a.x + (b.x * s - a.x) * t, y = a.y + (b.y * s - a.y) * t, z = a.z + (b.z * s - a.z) * t, w = a.w + (b.w * s - a.w) * t;
            float m = (float)Math.Sqrt(x * x + y * y + z * z + w * w);
            if (m < 1e-8f) return a;
            return new Quaternion(x / m, y / m, z / m, w / m);
        }
    }

    /// <summary>One keyframed full/partial-body pose clip. Joints not mentioned in a clip keep the locomotion pose.</summary>
    public sealed class PoseClip
    {
        public string id;
        public float length = 1f;
        public bool loop;
        public bool hold;            // keep the last pose after the clip ends (death, kneel...)
        public float blendIn = 0.08f;
        public float blendOut = 0.14f;
        public EaseKind ease = EaseKind.Out;
        public float[] times = new float[0];
        public Vector3[][] eulers = new Vector3[0][];   // [key][joint]
        public Vector3[] hips = new Vector3[0];          // hips position offset per key
        public bool[] mask = new bool[0];                // joints controlled by this clip
        public EaseKind[] segEase = new EaseKind[0];     // ease of the segment ENDING at key i

        public int KeyCount => times.Length;

        /// <summary>Writes the interpolated pose at time t into euler[] (joint count entries) and returns the hips offset.</summary>
        public Vector3 Sample(float t, Vector3[] euler)
        {
            int n = times.Length;
            if (n == 0) return Vector3.zero;
            if (loop && length > 0f) t = t % length;
            if (t <= times[0] || n == 1)
            {
                CopyKey(0, euler);
                return hips[0];
            }
            if (t >= times[n - 1])
            {
                CopyKey(n - 1, euler);
                return hips[n - 1];
            }
            int i = 1;
            while (i < n - 1 && t > times[i]) i++;
            float span = Mathf.Max(1e-5f, times[i] - times[i - 1]);
            float k = Mathx.Ease(segEase[i], (t - times[i - 1]) / span);
            for (int j = 0; j < euler.Length && j < mask.Length; j++)
            {
                if (!mask[j]) continue;
                euler[j] = Vector3.LerpUnclamped(eulers[i - 1][j], eulers[i][j], k);
            }
            return Vector3.LerpUnclamped(hips[i - 1], hips[i], k);
        }

        void CopyKey(int key, Vector3[] euler)
        {
            for (int j = 0; j < euler.Length && j < mask.Length; j++)
                if (mask[j]) euler[j] = eulers[key][j];
        }
    }

    /// <summary>
    /// Parser for pose scripts:
    /// <code>
    /// clip sword_l1 dur=0.6 ease=out blend=0.06 out=0.15 loop=0 hold=0
    ///   k 0.00 Ch=0,-30,0 ArR=-120,0,30 FoR=-60,0,0 Pos=0,-0.02,0
    ///   k 0.20 ArR=-30,0,10
    /// </code>
    /// Later keys inherit all joint values of the previous key (only changes need to be written).
    /// Joint codes are listed in <see cref="Rigs"/>; the left side uses the same (mirrored) semantic as the right side.
    /// </summary>
    public static class PoseParser
    {
        public static Dictionary<string, PoseClip> Parse(string text, RigSpec rig, string source, List<string> errors = null)
        {
            var result = new Dictionary<string, PoseClip>();
            List<ScriptLine> lines = ScriptReader.Parse(text, source);
            PoseClip cur = null;
            var keyTimes = new List<float>();
            var keyEulers = new List<Vector3[]>();
            var keyHips = new List<Vector3>();
            var keyEase = new List<EaseKind>();
            Vector3[] run = null;
            Vector3 runHips = Vector3.zero;

            void Finish()
            {
                if (cur == null) return;
                cur.times = keyTimes.ToArray();
                cur.eulers = keyEulers.ToArray();
                cur.hips = keyHips.ToArray();
                cur.segEase = keyEase.ToArray();
                if (cur.times.Length > 0 && cur.length < cur.times[cur.times.Length - 1]) cur.length = cur.times[cur.times.Length - 1];
                result[cur.id] = cur;
                cur = null;
            }

            foreach (ScriptLine l in lines)
            {
                if (l.Verb == "clip")
                {
                    Finish();
                    cur = new PoseClip { id = l.Arg(0, "clip"), length = l.GetFloat("dur", 1f), loop = l.GetBool("loop"), hold = l.GetBool("hold") };
                    cur.blendIn = l.GetFloat("blend", 0.08f);
                    cur.blendOut = l.GetFloat("out", 0.14f);
                    cur.ease = ParseEase(l.Get("ease", "out"));
                    cur.mask = new bool[rig.Count];
                    keyTimes.Clear(); keyEulers.Clear(); keyHips.Clear(); keyEase.Clear();
                    run = new Vector3[rig.Count];
                    runHips = Vector3.zero;
                }
                else if (l.Verb == "k" && cur != null)
                {
                    float t = ScriptReader.ParseFloat(l.Arg(0, "0"));
                    foreach (var kv in l.Kv)
                    {
                        if (kv.Key == "e") continue;
                        float[] f = ScriptReader.ParseFloats(kv.Value, 0f, 0f, 0f);
                        if (kv.Key == "Pos")
                        {
                            runHips = new Vector3(f[0], f[1], f[2]);
                            continue;
                        }
                        int j = rig.IndexOf(kv.Key);
                        if (j < 0)
                        {
                            if (errors != null) errors.Add(l.ToString() + ": unknown joint '" + kv.Key + "'");
                            continue;
                        }
                        run[j] = new Vector3(f[0], f[1], f[2]);
                        cur.mask[j] = true;
                    }
                    keyTimes.Add(t);
                    keyEulers.Add((Vector3[])run.Clone());
                    keyHips.Add(runHips);
                    keyEase.Add(l.Has("e") ? ParseEase(l.Get("e")) : cur.ease);
                }
                else if (errors != null)
                {
                    errors.Add(l.ToString() + ": unexpected line");
                }
            }
            Finish();
            return result;
        }

        public static EaseKind ParseEase(string s)
        {
            switch ((s ?? "").ToLowerInvariant())
            {
                case "in": return EaseKind.In;
                case "inout": return EaseKind.InOut;
                case "smooth": return EaseKind.Smooth;
                case "linear": return EaseKind.Linear;
                default: return EaseKind.Out;
            }
        }
    }
}
