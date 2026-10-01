using System.Collections.Generic;
using UnityEngine;

namespace ThienKhuyet.Core
{
    /// <summary>Deterministic gradient noise (pure managed, seedable, optionally tileable). Used by world and texture generation.</summary>
    public static class Noise
    {
        static uint Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        static float Grad(uint h, float x, float y)
        {
            switch (h & 7u)
            {
                case 0: return x + y;
                case 1: return -x + y;
                case 2: return x - y;
                case 3: return -x - y;
                case 4: return x * 1.4142f;
                case 5: return -x * 1.4142f;
                case 6: return y * 1.4142f;
                default: return -y * 1.4142f;
            }
        }

        static float Fade(float t)
        {
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        static float L(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        /// <summary>Gradient noise in roughly [-1,1].</summary>
        public static float Perlin(float x, float y, int seed)
        {
            int x0 = FloorI(x), y0 = FloorI(y);
            float fx = x - x0, fy = y - y0;
            float u = Fade(fx), v = Fade(fy);
            float n00 = Grad(Hash(x0, y0, seed), fx, fy);
            float n10 = Grad(Hash(x0 + 1, y0, seed), fx - 1f, fy);
            float n01 = Grad(Hash(x0, y0 + 1, seed), fx, fy - 1f);
            float n11 = Grad(Hash(x0 + 1, y0 + 1, seed), fx - 1f, fy - 1f);
            float r = L(L(n00, n10, u), L(n01, n11, u), v) * 0.7f;
            return r < -1f ? -1f : (r > 1f ? 1f : r);
        }

        /// <summary>Periodic gradient noise: result repeats every 'period' lattice cells (for seamless textures).</summary>
        public static float PerlinTile(float x, float y, int period, int seed)
        {
            int x0 = FloorI(x), y0 = FloorI(y);
            float fx = x - x0, fy = y - y0;
            int xa = Mod(x0, period), xb = Mod(x0 + 1, period);
            int ya = Mod(y0, period), yb = Mod(y0 + 1, period);
            float u = Fade(fx), v = Fade(fy);
            float n00 = Grad(Hash(xa, ya, seed), fx, fy);
            float n10 = Grad(Hash(xb, ya, seed), fx - 1f, fy);
            float n01 = Grad(Hash(xa, yb, seed), fx, fy - 1f);
            float n11 = Grad(Hash(xb, yb, seed), fx - 1f, fy - 1f);
            float r = L(L(n00, n10, u), L(n01, n11, u), v) * 0.7f;
            return r < -1f ? -1f : (r > 1f ? 1f : r);
        }

        public static float Fbm(float x, float y, int octaves, float lacunarity, float gain, int seed)
        {
            float sum = 0f, amp = 1f, norm = 0f, f = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Perlin(x * f, y * f, seed + i * 131) * amp;
                norm += amp;
                amp *= gain;
                f *= lacunarity;
            }
            return sum / norm;
        }

        public static float Ridged(float x, float y, int octaves, float lacunarity, float gain, int seed)
        {
            float sum = 0f, amp = 1f, norm = 0f, f = 1f;
            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - Mathf.Abs(Perlin(x * f, y * f, seed + i * 137));
                sum += n * n * amp;
                norm += amp;
                amp *= gain;
                f *= lacunarity;
            }
            return sum / norm;
        }

        /// <summary>Seamless fBm over u,v in [0,1) with 'baseFreq' cells per tile.</summary>
        public static float FbmTile(float u, float v, int baseFreq, int octaves, float gain, int seed)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            int freq = baseFreq;
            for (int i = 0; i < octaves; i++)
            {
                sum += PerlinTile(u * freq, v * freq, freq, seed + i * 71) * amp;
                norm += amp;
                amp *= gain;
                freq *= 2;
            }
            return sum / norm;
        }

        /// <summary>Cellular distance (F1) for stone/crack patterns, tileable.</summary>
        public static float Worley(float u, float v, int cells, int seed, out float f2)
        {
            float x = u * cells, y = v * cells;
            int cx = FloorI(x), cy = FloorI(y);
            float best = 9f, second = 9f;
            for (int j = -1; j <= 1; j++)
            {
                for (int i = -1; i <= 1; i++)
                {
                    int gx = cx + i, gy = cy + j;
                    int wx = Mod(gx, cells), wy = Mod(gy, cells);
                    float px = gx + Mathx.Hash01(wx, wy, seed);
                    float py = gy + Mathx.Hash01(wx, wy, seed + 977);
                    float dx = px - x, dy = py - y;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d < best) { second = best; best = d; }
                    else if (d < second) second = d;
                }
            }
            f2 = second;
            return best;
        }

        static int FloorI(float v)
        {
            int i = (int)v;
            return v < i ? i - 1 : i;
        }

        static int Mod(int a, int m)
        {
            int r = a % m;
            return r < 0 ? r + m : r;
        }
    }

    /// <summary>Small deterministic RNG (xorshift32) that can be created per chunk / per object for reproducible worlds.</summary>
    public struct Rng
    {
        uint s;

        public Rng(int seed)
        {
            unchecked
            {
                s = (uint)seed * 2654435761u + 0x9E3779B9u;
                if (s == 0) s = 0x1234567u;
                for (int i = 0; i < 3; i++) Next();
            }
        }

        public Rng(int a, int b, int seed) : this(unchecked(a * 73856093) ^ unchecked(b * 19349663) ^ unchecked(seed * 83492791))
        {
        }

        public uint Next()
        {
            s ^= s << 13;
            s ^= s >> 17;
            s ^= s << 5;
            return s;
        }

        public float Value()
        {
            return (Next() & 0xFFFFFF) / (float)0x1000000;
        }

        public float Range(float min, float max)
        {
            return min + (max - min) * Value();
        }

        /// <summary>Integer in [min, max] inclusive.</summary>
        public int Int(int min, int max)
        {
            if (max <= min) return min;
            return min + (int)(Next() % (uint)(max - min + 1));
        }

        public bool Chance(float p)
        {
            return Value() < p;
        }

        public float Signed()
        {
            return Value() * 2f - 1f;
        }

        public Vector2 InsideCircle()
        {
            float a = Value() * Mathf.PI * 2f;
            float r = Mathf.Sqrt(Value());
            return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }

        public T Pick<T>(IList<T> list)
        {
            return list[Int(0, list.Count - 1)];
        }

        /// <summary>Index chosen proportionally to non-negative weights.</summary>
        public int Weighted(IList<float> weights)
        {
            float total = 0f;
            for (int i = 0; i < weights.Count; i++) total += Mathf.Max(0f, weights[i]);
            if (total <= 0f) return 0;
            float r = Value() * total;
            for (int i = 0; i < weights.Count; i++)
            {
                r -= Mathf.Max(0f, weights[i]);
                if (r <= 0f) return i;
            }
            return weights.Count - 1;
        }
    }
}
