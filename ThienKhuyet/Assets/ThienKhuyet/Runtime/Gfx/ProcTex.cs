using System;
using ThienKhuyet.Core;
using UnityEngine;

namespace ThienKhuyet.Gfx
{
    public sealed class TexSet
    {
        public Texture2D albedo;
        public Texture2D normal;
        public Texture2D emission;
    }

    /// <summary>
    /// Procedural, seamlessly tiling textures (albedo + normal map derived from a height field). No imported art.
    /// All patterns are driven by the deterministic noise in <see cref="Noise"/>.
    /// </summary>
    public static class ProcTex
    {
        public delegate void Shade(float u, float v, out Color color, out float height);

        public static TexSet Bake(int size, Shade shade, float normalStrength = 2.5f, bool withNormal = true, string name = "proc")
        {
            var cols = new Color32[size * size];
            var hs = new float[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    shade(u, v, out Color c, out float h);
                    cols[y * size + x] = c;
                    hs[y * size + x] = h;
                }
            }
            var set = new TexSet();
            set.albedo = new Texture2D(size, size, TextureFormat.RGBA32, true, false) { name = name + "_albedo", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
            set.albedo.SetPixels32(cols);
            set.albedo.Apply(true);
            if (withNormal) set.normal = NormalFromHeight(hs, size, normalStrength, name + "_normal");
            return set;
        }

        public static Texture2D NormalFromHeight(float[] h, int size, float strength, string name)
        {
            var cols = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                int y0 = (y - 1 + size) % size, y1 = (y + 1) % size;
                for (int x = 0; x < size; x++)
                {
                    int x0 = (x - 1 + size) % size, x1 = (x + 1) % size;
                    float dx = (h[y * size + x0] - h[y * size + x1]) * strength;
                    float dy = (h[y0 * size + x] - h[y1 * size + x]) * strength;
                    float len = Mathf.Sqrt(dx * dx + dy * dy + 1f);
                    cols[y * size + x] = new Color32(
                        (byte)Mathf.Clamp((dx / len * 0.5f + 0.5f) * 255f, 0, 255),
                        (byte)Mathf.Clamp((dy / len * 0.5f + 0.5f) * 255f, 0, 255),
                        (byte)Mathf.Clamp((1f / len * 0.5f + 0.5f) * 255f, 0, 255), 255);
                }
            }
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
            t.SetPixels32(cols);
            t.Apply(true);
            return t;
        }

        static Color Mix(Color a, Color b, float t)
        {
            return Color.Lerp(a, b, Mathf.Clamp01(t));
        }

        static Color Vary(Color c, float amount, float n)
        {
            float k = 1f + n * amount;
            return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);
        }

        static float Smooth(float a, float b, float x)
        {
            return Mathx.SmoothStep01((x - a) / (b - a));
        }

        // ------------------------------------------------------------------ surfaces
        public static TexSet Stone(int size, Color tint, int seed, float crackDepth = 0.6f)
        {
            return Bake(size, (float u, float v, out Color c, out float h) =>
            {
                float n = Noise.FbmTile(u, v, 4, 5, 0.55f, seed);
                float n2 = Noise.FbmTile(u, v, 12, 3, 0.5f, seed + 3);
                float w = Noise.Worley(u, v, 5, seed + 11, out float f2);
                float edge = Smooth(0f, 0.09f, f2 - w);
                float cell = Mathx.Hash01(Mathf.FloorToInt(u * 5), Mathf.FloorToInt(v * 5), seed);
                Color baseC = Vary(tint, 0.35f, n * 0.8f + (cell - 0.5f) * 0.4f);
                c = Mix(baseC * 0.45f, baseC, edge * 0.7f + 0.3f);
                c = Vary(c, 0.12f, n2);
                c.a = 1f;
                h = n * 0.35f + n2 * 0.1f - (1f - edge) * crackDepth * 0.5f;
            }, 3f, true, "stone");
        }

        public static TexSet Cobble(int size, Color tint, int seed)
        {
            return Bake(size, (float u, float v, out Color c, out float h) =>
            {
                float w = Noise.Worley(u, v, 7, seed, out float f2);
                float gap = Smooth(0.02f, 0.12f, f2 - w);
                float cell = Mathx.Hash01(Mathf.FloorToInt(u * 7 + w * 3), Mathf.FloorToInt(v * 7), seed + 5);
                float n = Noise.FbmTile(u, v, 10, 3, 0.5f, seed + 9);
                Color stone = Vary(tint, 0.4f, cell - 0.5f + n * 0.4f);
                c = Mix(tint * 0.25f, stone, gap);
                c.a = 1f;
                h = gap * 0.7f + n * 0.12f;
            }, 4f, true, "cobble");
        }

        public static TexSet Bricks(int size, Color tint, Color mortar, int seed, int rows = 8, int cols = 4)
        {
            return Bake(size, (float u, float v, out Color c, out float h) =>
            {
                int row = Mathf.FloorToInt(v * rows);
                float uu = u * cols + (row % 2) * 0.5f;
                float fu = uu - Mathf.Floor(uu), fv = v * rows - row;
                float mu = Mathf.Min(fu, 1f - fu), mv = Mathf.Min(fv, 1f - fv);
                float inside = Smooth(0.02f, 0.08f, Mathf.Min(mu * 2.2f, mv * 4f));
                float cell = Mathx.Hash01(Mathf.FloorToInt(uu), row, seed);
                float n = Noise.FbmTile(u, v, 10, 3, 0.5f, seed + 2);
                Color brick = Vary(tint, 0.35f, (cell - 0.5f) + n * 0.5f);
                c = Mix(mortar, brick, inside);
                c.a = 1f;
                h = inside * 0.6f + n * 0.12f;
            }, 4f, true, "bricks");
        }

        public static TexSet Planks(int size, Color tint, int seed, int count = 6)
        {
            return Bake(size, (float u, float v, out Color c, out float h) =>
            {
                float pu = u * count;
                int plank = Mathf.FloorToInt(pu);
                float fu = pu - plank;
                float edge = Smooth(0.0f, 0.06f, Mathf.Min(fu, 1f - fu));
                float grain = Noise.FbmTile(pu * 0.5f / count + plank * 0.13f, v, 2, 4, 0.5f, seed + plank * 3);
                float rings = Mathf.Sin((grain * 9f + plank * 2.1f) * Mathf.PI) * 0.5f + 0.5f;
                float shift = (Mathx.Hash01(plank, 0, seed) - 0.5f) * 0.35f;
                Color wood = Vary(tint, 0.3f, shift + (rings - 0.5f) * 0.35f);
                c = Mix(tint * 0.2f, wood, edge);
                c.a = 1f;
                h = edge * 0.5f + rings * 0.08f;
            }, 3f, true, "planks");
        }

        public static TexSet Bark(int size, Color tint, int seed)
        {
            return Bake(size, (float u, float v, out Color c, out float h) =>
            {
                float n = Noise.FbmTile(u * 1f, v * 0.25f + 0.0f, 3, 4, 0.55f, seed);
                float ridge = Noise.FbmTile(u, v, 8, 3, 0.5f, seed + 4);
                float stripes = Mathf.Abs(Noise.PerlinTile(u * 14f, v * 3f, 14, seed + 8));
                c = Vary(tint, 0.45f, (n * 0.5f + (stripes - 0.4f)) * 0.8f);
                c.a = 1f;
                h = stripes * 0.8f + ridge * 0.15f;
            }, 3.5f, true, "bark");
        }

        public static TexSet Ground(int size, Color a, Color b, int seed, float pebbles = 0.4f)
        {
            return Bake(size, (float u, float v, out Color c, out float h) =>
            {
                float n = Noise.FbmTile(u, v, 4, 5, 0.55f, seed);
                float n2 = Noise.FbmTile(u, v, 16, 3, 0.5f, seed + 1);
                float w = Noise.Worley(u, v, 18, seed + 5, out float f2);
                float peb = 1f - Smooth(0.0f, 0.28f, w);
                c = Mix(a, b, n * 0.5f + 0.5f);
                c = Vary(c, 0.2f, n2);
                c = Mix(c, Vary(b * 1.15f, 0.2f, Mathx.Hash01(Mathf.FloorToInt(u * 18), Mathf.FloorToInt(v * 18), seed) - 0.5f), peb * pebbles);
                c.a = 1f;
                h = n * 0.3f + n2 * 0.1f + peb * pebbles * 0.25f;
            }, 2.2f, true, "ground");
        }

        public static TexSet Grass(int size, Color dark, Color light, int seed)
        {
            return Bake(size, (float u, float v, out Color c, out float h) =>
            {
                float n = Noise.FbmTile(u, v, 4, 4, 0.55f, seed);
                float blades = Noise.PerlinTile(u * 48f, v * 48f, 48, seed + 3) * 0.5f + 0.5f;
                float patches = Noise.FbmTile(u, v, 2, 3, 0.5f, seed + 6);
                c = Mix(dark, light, n * 0.5f + 0.5f + patches * 0.25f);
                c = Vary(c, 0.3f, blades - 0.5f);
                c.a = 1f;
                h = blades * 0.25f + n * 0.15f;
            }, 1.6f, true, "grass");
        }

        public static TexSet Snow(int size, int seed)
        {
            return Bake(size, (float u, float v, out Color c, out float h) =>
            {
                float n = Noise.FbmTile(u, v, 5, 4, 0.5f, seed);
                float sparkle = Mathx.Hash01(Mathf.FloorToInt(u * size), Mathf.FloorToInt(v * size), seed) > 0.985f ? 0.12f : 0f;
                c = Mix(new Color(0.82f, 0.88f, 0.95f), new Color(0.97f, 0.98f, 1f), n * 0.5f + 0.5f) + new Color(sparkle, sparkle, sparkle, 0f);
                c.a = 1f;
                h = n * 0.2f;
            }, 1.5f, true, "snow");
        }

        public static TexSet RoofTiles(int size, Color tint, int seed, int rows = 10, int cols = 10)
        {
            return Bake(size, (float u, float v, out Color c, out float h) =>
            {
                float rv = v * rows;
                int row = Mathf.FloorToInt(rv);
                float fv = rv - row;
                float uu = u * cols + (row % 2) * 0.5f;
                int col = Mathf.FloorToInt(uu);
                float fu = uu - col;
                float curve = Mathf.Sin(fu * Mathf.PI);
                float lower = Smooth(0f, 0.3f, fv);
                float cell = Mathx.Hash01(col, row, seed);
                float n = Noise.FbmTile(u, v, 8, 3, 0.5f, seed + 1);
                Color t = Vary(tint, 0.25f, (cell - 0.5f) * 0.6f + n * 0.3f);
                c = Mix(t * 0.35f, t, lower * (0.55f + curve * 0.45f));
                c.a = 1f;
                h = curve * 0.5f + lower * 0.4f;
            }, 4f, true, "roof");
        }

        public static TexSet Plaster(int size, Color tint, int seed)
        {
            return Bake(size, (float u, float v, out Color c, out float h) =>
            {
                float n = Noise.FbmTile(u, v, 6, 4, 0.5f, seed);
                float n2 = Noise.FbmTile(u, v, 24, 2, 0.5f, seed + 2);
                c = Vary(tint, 0.12f, n * 0.6f + n2 * 0.3f);
                c.a = 1f;
                h = n * 0.25f + n2 * 0.1f;
            }, 1.5f, true, "plaster");
        }

        public static TexSet Cloth(int size, Color tint, int seed, int threads = 48)
        {
            return Bake(size, (float u, float v, out Color c, out float h) =>
            {
                float wu = Mathf.Sin(u * threads * Mathf.PI * 2f) * 0.5f + 0.5f;
                float wv = Mathf.Sin(v * threads * Mathf.PI * 2f) * 0.5f + 0.5f;
                bool over = (Mathf.FloorToInt(u * threads) + Mathf.FloorToInt(v * threads)) % 2 == 0;
                float weave = over ? wu : wv;
                float n = Noise.FbmTile(u, v, 4, 3, 0.5f, seed);
                c = Vary(tint, 0.18f, (weave - 0.5f) * 0.7f + n * 0.4f);
                c.a = 1f;
                h = weave * 0.3f;
            }, 1.5f, true, "cloth");
        }

        public static TexSet Metal(int size, Color tint, int seed)
        {
            return Bake(size, (float u, float v, out Color c, out float h) =>
            {
                float n = Noise.PerlinTile(u * 2f, v * 60f, 60, seed) * 0.5f + 0.5f;
                float n2 = Noise.FbmTile(u, v, 3, 3, 0.5f, seed + 1);
                c = Vary(tint, 0.25f, (n - 0.5f) * 0.8f + n2 * 0.3f);
                c.a = 1f;
                h = n * 0.1f;
            }, 1f, true, "metal");
        }

        public static TexSet Marble(int size, Color baseColor, Color vein, int seed)
        {
            return Bake(size, (float u, float v, out Color c, out float h) =>
            {
                float n = Noise.FbmTile(u, v, 3, 5, 0.55f, seed);
                float veins = 1f - Mathf.Abs(Noise.FbmTile(u + n * 0.08f, v, 4, 4, 0.5f, seed + 3));
                veins = Mathf.Pow(Mathf.Clamp01(veins), 6f);
                c = Mix(baseColor, vein, veins * 0.8f);
                c = Vary(c, 0.08f, n);
                c.a = 1f;
                h = n * 0.1f;
            }, 1f, true, "marble");
        }

        public static TexSet Foliage(int size, Color dark, Color light, int seed)
        {
            return Bake(size, (float u, float v, out Color c, out float h) =>
            {
                float w = Noise.Worley(u, v, 9, seed, out float f2);
                float cell = Mathx.Hash01(Mathf.FloorToInt(u * 9 + w), Mathf.FloorToInt(v * 9), seed + 4);
                float n = Noise.FbmTile(u, v, 6, 3, 0.5f, seed + 1);
                float rim = Smooth(0.05f, 0.5f, w);
                c = Mix(dark, light, cell * 0.65f + n * 0.35f + 0.1f);
                c = Mix(c, dark * 0.7f, rim * 0.5f);
                c.a = 1f;
                h = (1f - rim) * 0.4f + n * 0.1f;
            }, 2.5f, true, "foliage");
        }

        public static Texture2D WaterNormal(int size, int seed)
        {
            float[] hs = new float[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                    hs[y * size + x] = Noise.FbmTile(u, v, 6, 4, 0.5f, seed) * 0.8f + Noise.FbmTile(u, v, 14, 2, 0.5f, seed + 3) * 0.3f;
                }
            }
            return NormalFromHeight(hs, size, 2.2f, "water_normal");
        }

        /// <summary>City-night facade: concrete with a grid of windows (lit ones go into the emission map).</summary>
        public static TexSet Facade(int size, Color wall, int seed, int cols = 8, int rows = 16)
        {
            var emis = new Color32[size * size];
            var set = Bake(size, (float u, float v, out Color c, out float h) =>
            {
                float n = Noise.FbmTile(u, v, 8, 3, 0.5f, seed);
                c = Vary(wall, 0.15f, n);
                c.a = 1f;
                h = n * 0.1f;
                float cu = u * cols, cv = v * rows;
                int ix = Mathf.FloorToInt(cu), iy = Mathf.FloorToInt(cv);
                float fu = cu - ix, fv = cv - iy;
                if (fu > 0.18f && fu < 0.82f && fv > 0.22f && fv < 0.8f)
                {
                    bool lit = Mathx.Hash01(ix, iy, seed) > 0.55f;
                    c = lit ? new Color(1f, 0.85f, 0.55f) : new Color(0.06f, 0.08f, 0.1f);
                    h = -0.2f;
                }
            }, 2f, true, "facade");
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float cu = u * cols, cv = v * rows;
                    int ix = Mathf.FloorToInt(cu), iy = Mathf.FloorToInt(cv);
                    float fu = cu - ix, fv = cv - iy;
                    bool win = fu > 0.18f && fu < 0.82f && fv > 0.22f && fv < 0.8f;
                    bool lit = win && Mathx.Hash01(ix, iy, seed) > 0.55f;
                    float warm = Mathx.Hash01(ix, iy, seed + 9);
                    emis[y * size + x] = lit ? new Color32(255, (byte)(190 + warm * 50), (byte)(110 + warm * 90), 255) : new Color32(0, 0, 0, 255);
                }
            }
            set.emission = new Texture2D(size, size, TextureFormat.RGBA32, true, false) { name = "facade_emission", wrapMode = TextureWrapMode.Repeat };
            set.emission.SetPixels32(emis);
            set.emission.Apply(true);
            return set;
        }

        // ------------------------------------------------------------------ sprites / FX textures (no tiling)
        public static Texture2D Sprite(int size, Func<float, float, Color> f, string name)
        {
            var cols = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    cols[y * size + x] = f(u, v);
                }
            }
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            t.SetPixels32(cols);
            t.Apply(true);
            return t;
        }

        public static Texture2D SoftCircle(int size = 64, float hardness = 0.35f)
        {
            return Sprite(size, (u, v) =>
            {
                float d = Mathf.Sqrt(u * u + v * v);
                float a = 1f - Smooth(hardness, 1f, d);
                return new Color(1f, 1f, 1f, a);
            }, "soft_circle");
        }

        public static Texture2D Ring(int size = 128, float radius = 0.85f, float thickness = 0.08f)
        {
            return Sprite(size, (u, v) =>
            {
                float d = Mathf.Sqrt(u * u + v * v);
                float a = 1f - Smooth(0f, thickness, Mathf.Abs(d - radius));
                float fade = 1f - Smooth(0.95f, 1f, d);
                return new Color(1f, 1f, 1f, a * fade);
            }, "ring");
        }

        public static Texture2D Disc(int size = 128, float edge = 0.04f)
        {
            return Sprite(size, (u, v) =>
            {
                float d = Mathf.Sqrt(u * u + v * v);
                return new Color(1f, 1f, 1f, 1f - Smooth(1f - edge, 1f, d));
            }, "disc");
        }

        /// <summary>Crescent used by sword waves and slashes.</summary>
        public static Texture2D Crescent(int size = 128)
        {
            return Sprite(size, (u, v) =>
            {
                float d1 = Mathf.Sqrt(u * u + v * v);
                float d2 = Mathf.Sqrt(u * u + (v + 0.45f) * (v + 0.45f));
                float outer = 1f - Smooth(0.92f, 1f, d1);
                float inner = Smooth(0.82f, 0.9f, d2);
                float a = outer * inner * (1f - Mathf.Abs(u) * 0.4f);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }, "crescent");
        }

        public static Texture2D Streak(int size = 128)
        {
            return Sprite(size, (u, v) =>
            {
                float a = (1f - Smooth(0f, 1f, Mathf.Abs(v))) * (1f - Smooth(0.6f, 1f, Mathf.Abs(u)));
                return new Color(1f, 1f, 1f, a * a);
            }, "streak");
        }

        public static Texture2D Star(int size = 64)
        {
            return Sprite(size, (u, v) =>
            {
                float d = Mathf.Sqrt(u * u + v * v);
                float cross = Mathf.Max(1f - Smooth(0f, 0.12f, Mathf.Abs(u)), 1f - Smooth(0f, 0.12f, Mathf.Abs(v))) * (1f - Smooth(0.2f, 1f, d));
                float core = 1f - Smooth(0f, 0.35f, d);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(cross + core));
            }, "star");
        }

        public static Texture2D Smoke(int size = 128, int seed = 1)
        {
            return Sprite(size, (u, v) =>
            {
                float d = Mathf.Sqrt(u * u + v * v);
                float n = Noise.Fbm(u * 2.5f + 3f, v * 2.5f + 7f, 4, 2f, 0.5f, seed) * 0.5f + 0.5f;
                float a = (1f - Smooth(0.3f, 1f, d)) * Mathf.Clamp01(n * 1.3f);
                return new Color(1f, 1f, 1f, a);
            }, "smoke");
        }

        /// <summary>Rotating magic-array pattern (concentric rings, polygon, tick marks and glyph-like strokes).</summary>
        public static Texture2D Rune(int size = 512, int seed = 7, int sides = 6)
        {
            return Sprite(size, (u, v) =>
            {
                float d = Mathf.Sqrt(u * u + v * v);
                float ang = Mathf.Atan2(v, u);
                float a = 0f;
                a = Mathf.Max(a, 1f - Smooth(0f, 0.012f, Mathf.Abs(d - 0.96f)));
                a = Mathf.Max(a, 1f - Smooth(0f, 0.008f, Mathf.Abs(d - 0.88f)));
                a = Mathf.Max(a, 1f - Smooth(0f, 0.01f, Mathf.Abs(d - 0.6f)));
                // polygon
                float sector = Mathf.PI * 2f / sides;
                float pa = Mathf.Repeat(ang, sector) - sector * 0.5f;
                float poly = d * Mathf.Cos(pa) / Mathf.Cos(sector * 0.5f);
                a = Mathf.Max(a, 1f - Smooth(0f, 0.01f, Mathf.Abs(poly - 0.74f)));
                float inner = Mathf.Min(d * Mathf.Cos(pa) / Mathf.Cos(sector * 0.5f), 2f);
                a = Mathf.Max(a, 1f - Smooth(0f, 0.008f, Mathf.Abs(inner - 0.34f)));
                // ticks between r=0.88..0.96
                float ticks = Mathf.Abs(Mathf.Repeat(ang * 24f / (Mathf.PI * 2f), 1f) - 0.5f);
                if (d > 0.88f && d < 0.96f) a = Mathf.Max(a, (1f - Smooth(0.06f, 0.12f, ticks)) * 0.8f);
                // glyph strokes: random short arcs on the band between 0.6 and 0.88
                if (d > 0.64f && d < 0.84f)
                {
                    int slot = Mathf.FloorToInt((ang + Mathf.PI) / (Mathf.PI * 2f) * 18f);
                    float f = Mathf.Repeat((ang + Mathf.PI) / (Mathf.PI * 2f) * 18f, 1f);
                    float rr = (d - 0.64f) / 0.2f;
                    float h = Mathx.Hash01(slot, 3, seed);
                    float stroke = h > 0.5f ? 1f - Smooth(0f, 0.09f, Mathf.Abs(f - 0.5f)) : 1f - Smooth(0f, 0.1f, Mathf.Abs(rr - (0.3f + h)));
                    float cap = Smooth(0.05f, 0.2f, f) * (1f - Smooth(0.8f, 0.95f, f));
                    a = Mathf.Max(a, stroke * cap * 0.9f);
                }
                a *= 1f - Smooth(0.97f, 1f, d);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }, "rune");
        }

        public static Texture2D Stars(int width = 1024, int height = 512, int seed = 3)
        {
            var cols = new Color32[width * height];
            var rng = new Rng(seed);
            for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(0, 0, 0, 255);
            for (int i = 0; i < 1800; i++)
            {
                int x = rng.Int(0, width - 1), y = rng.Int(0, height - 1);
                float b = Mathf.Pow(rng.Value(), 3f);
                byte g = (byte)(60 + b * 195);
                cols[y * width + x] = new Color32(g, (byte)(g * 0.95f), (byte)Mathf.Min(255, g * 1.05f), 255);
                if (b > 0.7f && x + 1 < width) cols[y * width + x + 1] = new Color32((byte)(g * 0.5f), (byte)(g * 0.5f), (byte)(g * 0.55f), 255);
            }
            // faint milky band
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float v = (float)y / height;
                    float band = Mathf.Exp(-Mathf.Pow((v - (0.5f + 0.18f * Mathf.Sin(x / (float)width * Mathf.PI * 2f))) * 7f, 2f));
                    float n = Noise.Fbm(x * 0.012f, y * 0.03f, 4, 2f, 0.5f, seed) * 0.5f + 0.5f;
                    byte add = (byte)Mathf.Clamp(band * n * 34f, 0f, 60f);
                    Color32 c = cols[y * width + x];
                    cols[y * width + x] = new Color32((byte)Mathf.Min(255, c.r + add * 0.8f), (byte)Mathf.Min(255, c.g + add * 0.85f), (byte)Mathf.Min(255, c.b + add), 255);
                }
            }
            var t = new Texture2D(width, height, TextureFormat.RGBA32, true, false) { name = "stars", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            t.SetPixels32(cols);
            t.Apply(true);
            return t;
        }

        public static Texture2D SolidColor(Color c, string name = "solid")
        {
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false, false) { name = name };
            var px = new Color32[16];
            Color32 cc = c;
            for (int i = 0; i < 16; i++) px[i] = cc;
            t.SetPixels32(px);
            t.Apply(false);
            return t;
        }
    }
}
