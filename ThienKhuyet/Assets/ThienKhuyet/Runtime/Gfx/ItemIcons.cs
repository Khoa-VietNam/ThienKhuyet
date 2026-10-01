using System;
using System.Collections.Generic;
using ThienKhuyet.Data;
using UnityEngine;

namespace ThienKhuyet.Gfx
{
    /// <summary>Procedurally drawn 64x64 item and skill icons (vector-like shapes composited per pixel, with a dark outline).</summary>
    public static class ItemIcons
    {
        const int Size = 64;
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cache.Clear();
        }

        public static Sprite Get(ItemDef item)
        {
            return Get(item.icon, item.iconColor);
        }

        public static Sprite Get(IconShape shape, Color color)
        {
            string key = shape + ":" + ColorUtility.ToHtmlStringRGB(color);
            if (cache.TryGetValue(key, out Sprite s) && s != null) return s;
            Texture2D tex = Draw(shape, color);
            s = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            s.name = key;
            cache[key] = s;
            return s;
        }

        // ------------------------------------------------------------------ shape coverage helpers (u,v in [-1,1], y up)
        static float Cov(float d, float soft = 0.045f)
        {
            return Mathf.Clamp01(0.5f - d / soft);
        }

        static float SegD(float u, float v, float ax, float ay, float bx, float by)
        {
            float px = u - ax, py = v - ay, dx = bx - ax, dy = by - ay;
            float t = Mathf.Clamp01((px * dx + py * dy) / Mathf.Max(1e-5f, dx * dx + dy * dy));
            float cx = ax + dx * t - u, cy = ay + dy * t - v;
            return Mathf.Sqrt(cx * cx + cy * cy);
        }

        static float Seg(float u, float v, float ax, float ay, float bx, float by, float r)
        {
            return Cov(SegD(u, v, ax, ay, bx, by) - r);
        }

        static float Circle(float u, float v, float cx, float cy, float r)
        {
            return Cov(Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy)) - r);
        }

        static float Ellipse(float u, float v, float cx, float cy, float rx, float ry)
        {
            float x = (u - cx) / rx, y = (v - cy) / ry;
            return Cov((Mathf.Sqrt(x * x + y * y) - 1f) * Mathf.Min(rx, ry));
        }

        static float Rect(float u, float v, float cx, float cy, float hx, float hy)
        {
            float dx = Mathf.Abs(u - cx) - hx, dy = Mathf.Abs(v - cy) - hy;
            float d = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f)) + Mathf.Min(Mathf.Max(dx, dy), 0f);
            return Cov(d);
        }

        static float Poly(float u, float v, float[] pts)
        {
            // convex/concave polygon fill via even-odd with distance-based edge softening
            int n = pts.Length / 2;
            bool inside = false;
            float minD = 9f;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = pts[i * 2], yi = pts[i * 2 + 1], xj = pts[j * 2], yj = pts[j * 2 + 1];
                if (((yi > v) != (yj > v)) && (u < (xj - xi) * (v - yi) / (yj - yi) + xi)) inside = !inside;
                minD = Mathf.Min(minD, SegD(u, v, xi, yi, xj, yj));
            }
            return inside ? Mathf.Clamp01(0.5f + minD / 0.045f) : Cov(minD);
        }

        struct Layer
        {
            public float a;
            public Color c;
        }

        static Texture2D Draw(IconShape shape, Color main)
        {
            Color dark = main * 0.55f; dark.a = 1f;
            Color light = Color.Lerp(main, Color.white, 0.55f);
            Color steel = new Color(0.82f, 0.86f, 0.9f);
            Color gold = new Color(0.95f, 0.78f, 0.35f);
            Color wood = new Color(0.55f, 0.38f, 0.2f);
            var tex = Sprite_(Size, (u, v) =>
            {
                Layer[] layers = Layers(shape, u, v, main, dark, light, steel, gold, wood);
                float outline = 0f;
                for (int i = 0; i < layers.Length; i++) outline = Mathf.Max(outline, layers[i].a);
                // outline = coverage of the union dilated: re-evaluate cheaply by sampling neighbors
                float oa = 0f;
                const float o = 0.06f;
                for (int k = 0; k < 4 && oa < 1f; k++)
                {
                    float du = (k % 2 == 0 ? 1 : -1) * (k < 2 ? o : 0f), dv = (k % 2 == 0 ? 1 : -1) * (k < 2 ? 0f : o);
                    Layer[] nb = Layers(shape, u + du, v + dv, main, dark, light, steel, gold, wood);
                    for (int i = 0; i < nb.Length; i++) oa = Mathf.Max(oa, nb[i].a);
                }
                Color col = new Color(0.04f, 0.04f, 0.06f, oa * 0.9f);
                for (int i = 0; i < layers.Length; i++)
                {
                    float a = layers[i].a;
                    if (a <= 0f) continue;
                    float outA = a + col.a * (1f - a);
                    Color rgb = outA > 0f ? (layers[i].c * a + new Color(col.r, col.g, col.b) * col.a * (1f - a)) / outA : Color.black;
                    col = new Color(rgb.r, rgb.g, rgb.b, outA);
                }
                return col;
            });
            return tex;
        }

        static Texture2D Sprite_(int size, Func<float, float, Color> f)
        {
            Texture2D t = ProcTex.Sprite(size, f, "icon");
            t.filterMode = FilterMode.Bilinear;
            return t;
        }

        static Layer L(float a, Color c)
        {
            return new Layer { a = a, c = c };
        }

        static Layer[] Layers(IconShape shape, float u, float v, Color main, Color dark, Color light, Color steel, Color gold, Color wood)
        {
            switch (shape)
            {
                case IconShape.Sword:
                    return new[]
                    {
                        L(Seg(u, v, -0.15f, -0.15f, 0.62f, 0.62f, 0.1f), steel), L(Seg(u, v, -0.1f, -0.1f, 0.55f, 0.55f, 0.025f), Color.white),
                        L(Seg(u, v, -0.42f, 0.02f, 0.02f, -0.42f, 0.065f), gold), L(Seg(u, v, -0.2f, -0.2f, -0.55f, -0.55f, 0.07f), wood),
                        L(Circle(u, v, -0.62f, -0.62f, 0.09f), gold)
                    };
                case IconShape.Blade:
                    return new[]
                    {
                        L(Seg(u, v, -0.1f, -0.2f, 0.35f, 0.4f, 0.1f), steel), L(Seg(u, v, 0.35f, 0.4f, 0.62f, 0.62f, 0.07f), steel),
                        L(Seg(u, v, -0.38f, 0.0f, 0.08f, -0.42f, 0.06f), dark), L(Seg(u, v, -0.2f, -0.28f, -0.55f, -0.58f, 0.07f), wood)
                    };
                case IconShape.Spear:
                    return new[]
                    {
                        L(Seg(u, v, -0.65f, -0.65f, 0.35f, 0.35f, 0.045f), wood),
                        L(Poly(u, v, new[] { 0.3f, 0.3f, 0.7f, 0.7f, 0.45f, 0.62f, 0.62f, 0.45f }), steel), L(Seg(u, v, 0.38f, 0.38f, 0.64f, 0.64f, 0.02f), Color.white)
                    };
                case IconShape.Fist:
                    return new[]
                    {
                        L(Rect(u, v, 0f, -0.2f, 0.4f, 0.32f), main), L(Circle(u, v, -0.28f, 0.2f, 0.14f), light), L(Circle(u, v, -0.09f, 0.26f, 0.14f), light),
                        L(Circle(u, v, 0.1f, 0.26f, 0.14f), light), L(Circle(u, v, 0.29f, 0.2f, 0.14f), light), L(Rect(u, v, 0f, -0.55f, 0.36f, 0.1f), dark)
                    };
                case IconShape.Staff:
                    return new[]
                    {
                        L(Seg(u, v, -0.6f, -0.65f, 0.35f, 0.4f, 0.055f), wood), L(Circle(u, v, 0.48f, 0.5f, 0.2f), light), L(Circle(u, v, 0.48f, 0.5f, 0.11f), Color.white),
                        L(Seg(u, v, 0.28f, 0.3f, 0.4f, 0.28f, 0.03f), gold)
                    };
                case IconShape.Hood:
                    return new[]
                    {
                        L(Ellipse(u, v, 0f, 0.05f, 0.5f, 0.55f), main), L(Ellipse(u, v, 0f, -0.05f, 0.28f, 0.34f), dark), L(Rect(u, v, 0f, -0.5f, 0.5f, 0.12f), main)
                    };
                case IconShape.Robe:
                    return new[]
                    {
                        L(Poly(u, v, new[] { -0.3f, 0.6f, 0.3f, 0.6f, 0.7f, -0.6f, -0.7f, -0.6f }), main), L(Rect(u, v, 0f, 0.05f, 0.5f, 0.07f), gold),
                        L(Poly(u, v, new[] { -0.3f, 0.6f, 0f, 0.2f, 0.3f, 0.6f }), dark), L(Seg(u, v, 0f, 0.2f, 0f, -0.6f, 0.025f), dark)
                    };
                case IconShape.Boots:
                    return new[]
                    {
                        L(Poly(u, v, new[] { -0.3f, 0.6f, 0.1f, 0.6f, 0.15f, -0.1f, 0.7f, -0.25f, 0.7f, -0.55f, -0.3f, -0.55f }), main),
                        L(Rect(u, v, 0.2f, -0.55f, 0.5f, 0.07f), dark), L(Rect(u, v, -0.1f, 0.5f, 0.22f, 0.08f), light)
                    };
                case IconShape.Ring:
                    return new[]
                    {
                        L(Mathf.Clamp01(Circle(u, v, 0f, -0.1f, 0.5f) - Circle(u, v, 0f, -0.1f, 0.32f)), gold),
                        L(Poly(u, v, new[] { 0f, 0.6f, 0.2f, 0.4f, 0f, 0.2f, -0.2f, 0.4f }), main), L(Poly(u, v, new[] { 0f, 0.5f, 0.09f, 0.4f, 0f, 0.3f, -0.09f, 0.4f }), Color.white)
                    };
                case IconShape.Pendant:
                    return new[]
                    {
                        L(Mathf.Clamp01(Seg(u, v, -0.5f, 0.7f, -0.1f, 0.2f, 0.02f) + Seg(u, v, 0.5f, 0.7f, 0.1f, 0.2f, 0.02f)), dark),
                        L(Poly(u, v, new[] { 0f, 0.3f, 0.32f, -0.1f, 0f, -0.6f, -0.32f, -0.1f }), main), L(Poly(u, v, new[] { 0f, 0.18f, 0.16f, -0.08f, 0f, -0.34f, -0.16f, -0.08f }), light),
                        L(Circle(u, v, 0f, 0.28f, 0.07f), gold)
                    };
                case IconShape.Herb:
                    return new[]
                    {
                        L(Seg(u, v, 0f, -0.65f, 0f, 0.1f, 0.03f), dark), L(Ellipse(u, v, -0.28f, 0.2f, 0.28f, 0.14f), main), L(Ellipse(u, v, 0.28f, 0.28f, 0.28f, 0.14f), light),
                        L(Ellipse(u, v, 0f, 0.55f, 0.13f, 0.3f), main), L(Ellipse(u, v, -0.2f, -0.2f, 0.22f, 0.11f), light)
                    };
                case IconShape.Pill:
                    return new[] { L(Circle(u, v, 0f, 0f, 0.45f), main), L(Circle(u, v, -0.15f, 0.15f, 0.14f), Color.white * 0.9f), L(Circle(u, v, 0.1f, -0.12f, 0.2f), dark) };
                case IconShape.Gem:
                case IconShape.Stone:
                    return new[]
                    {
                        L(Poly(u, v, new[] { 0f, 0.65f, 0.5f, 0.2f, 0.32f, -0.5f, -0.32f, -0.5f, -0.5f, 0.2f }), main),
                        L(Poly(u, v, new[] { 0f, 0.65f, 0.2f, 0.2f, 0f, -0.5f, -0.2f, 0.2f }), light), L(Poly(u, v, new[] { -0.5f, 0.2f, 0f, 0.65f, -0.2f, 0.2f }), dark)
                    };
                case IconShape.Book:
                    return new[]
                    {
                        L(Rect(u, v, 0f, 0f, 0.48f, 0.6f), main), L(Rect(u, v, -0.4f, 0f, 0.08f, 0.6f), dark), L(Rect(u, v, 0.05f, 0.25f, 0.28f, 0.1f), gold),
                        L(Rect(u, v, 0.05f, -0.05f, 0.2f, 0.05f), gold)
                    };
                case IconShape.Pelt:
                    return new[]
                    {
                        L(Poly(u, v, new[] { -0.6f, 0.4f, -0.2f, 0.55f, 0.3f, 0.5f, 0.65f, 0.3f, 0.5f, -0.1f, 0.65f, -0.5f, 0.1f, -0.4f, -0.3f, -0.6f, -0.5f, -0.2f }), main),
                        L(Circle(u, v, -0.12f, 0.1f, 0.12f), dark)
                    };
                case IconShape.Fang:
                    return new[] { L(Poly(u, v, new[] { -0.3f, 0.6f, 0.3f, 0.6f, 0.05f, -0.65f }), main), L(Poly(u, v, new[] { -0.1f, 0.5f, 0.15f, 0.5f, 0.06f, -0.2f }), light) };
                case IconShape.Ore:
                    return new[]
                    {
                        L(Poly(u, v, new[] { -0.6f, -0.4f, -0.5f, 0.1f, -0.1f, 0.5f, 0.4f, 0.35f, 0.65f, -0.1f, 0.5f, -0.5f }), main),
                        L(Poly(u, v, new[] { -0.2f, 0.1f, 0f, 0.4f, 0.15f, 0.1f }), light), L(Poly(u, v, new[] { 0.25f, -0.2f, 0.4f, 0.05f, 0.5f, -0.25f }), gold)
                    };
                case IconShape.Shard:
                    return new[]
                    {
                        L(Poly(u, v, new[] { -0.15f, 0.7f, 0.35f, 0.1f, 0.2f, -0.65f, -0.3f, -0.3f, -0.45f, 0.2f }), main),
                        L(Poly(u, v, new[] { -0.15f, 0.7f, 0.1f, 0.1f, -0.05f, -0.3f, -0.3f, 0.1f }), light)
                    };
                case IconShape.Scroll:
                    return new[]
                    {
                        L(Rect(u, v, 0f, 0f, 0.5f, 0.4f), Color.Lerp(main, Color.white, 0.4f)), L(Rect(u, v, -0.55f, 0f, 0.09f, 0.48f), dark), L(Rect(u, v, 0.55f, 0f, 0.09f, 0.48f), dark),
                        L(Seg(u, v, -0.3f, 0.15f, 0.3f, 0.15f, 0.025f), dark), L(Seg(u, v, -0.3f, -0.05f, 0.3f, -0.05f, 0.025f), dark), L(Seg(u, v, -0.3f, -0.25f, 0.1f, -0.25f, 0.025f), dark)
                    };
                case IconShape.Berry:
                    return new[]
                    {
                        L(Circle(u, v, -0.25f, -0.2f, 0.28f), main), L(Circle(u, v, 0.28f, -0.15f, 0.26f), dark), L(Circle(u, v, 0f, 0.28f, 0.27f), light),
                        L(Seg(u, v, 0f, 0.5f, 0.15f, 0.7f, 0.03f), new Color(0.3f, 0.6f, 0.25f))
                    };
                case IconShape.Paste:
                    return new[] { L(Rect(u, v, 0f, -0.1f, 0.45f, 0.4f), main), L(Rect(u, v, 0f, 0.38f, 0.5f, 0.12f), wood), L(Rect(u, v, 0f, -0.1f, 0.3f, 0.14f), Color.white * 0.85f) };
                case IconShape.Flask:
                    return new[]
                    {
                        L(Poly(u, v, new[] { -0.14f, 0.65f, 0.14f, 0.65f, 0.14f, 0.2f, 0.5f, -0.5f, -0.5f, -0.5f, -0.14f, 0.2f }), Color.Lerp(main, Color.white, 0.3f)),
                        L(Poly(u, v, new[] { -0.36f, -0.15f, 0.36f, -0.15f, 0.5f, -0.5f, -0.5f, -0.5f }), main), L(Rect(u, v, 0f, 0.66f, 0.18f, 0.07f), wood)
                    };
                case IconShape.Orb:
                    return new[] { L(Circle(u, v, 0f, 0f, 0.55f), dark), L(Circle(u, v, 0f, 0f, 0.42f), main), L(Circle(u, v, -0.12f, 0.14f, 0.2f), light), L(Circle(u, v, -0.17f, 0.2f, 0.07f), Color.white) };
                case IconShape.Key:
                    return new[]
                    {
                        L(Mathf.Clamp01(Circle(u, v, -0.3f, 0.3f, 0.3f) - Circle(u, v, -0.3f, 0.3f, 0.14f)), gold), L(Seg(u, v, -0.1f, 0.1f, 0.6f, -0.6f, 0.06f), gold),
                        L(Seg(u, v, 0.35f, -0.35f, 0.5f, -0.2f, 0.05f), gold)
                    };
                case IconShape.Token:
                    return new[] { L(Mathf.Clamp01(Circle(u, v, 0f, 0f, 0.55f) - Circle(u, v, 0f, 0f, 0.1f)), main), L(Mathf.Clamp01(Circle(u, v, 0f, 0f, 0.45f) - Circle(u, v, 0f, 0f, 0.34f)), light) };
                default:
                    return new[] { L(Circle(u, v, 0f, 0f, 0.5f), main) };
            }
        }
    }
}
