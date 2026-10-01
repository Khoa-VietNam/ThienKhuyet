using System.Collections.Generic;
using UnityEngine;

namespace ThienKhuyet.Gfx
{
    /// <summary>
    /// Colour atlas shared by every procedural character: each colour owns a 4x4 texel cell and meshes sample the cell centre.
    /// One material then renders an entire character (and thousands of props) in a single SRP-batched state, with no imported art.
    /// </summary>
    public static class Palette
    {
        const int CellsPerRow = 32;
        const int CellPixels = 4;
        const int Size = CellsPerRow * CellPixels;

        static Texture2D tex;
        static Color32[] pixels;
        static readonly Dictionary<uint, Vector2> map = new Dictionary<uint, Vector2>();
        static int next;
        static bool dirty;
        static Material standard, metal;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            tex = null;
            pixels = null;
            map.Clear();
            next = 0;
            dirty = false;
            standard = metal = null;
        }

        static void Ensure()
        {
            if (tex != null) return;
            tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false) { name = "palette", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            pixels = new Color32[Size * Size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 0, 255, 255);
            map.Clear();
            next = 0;
        }

        static uint Pack(Color c)
        {
            // 6 bits per channel keeps the atlas small while hiding no visible variation
            uint r = (uint)Mathf.Clamp(Mathf.RoundToInt(c.r * 63f), 0, 63);
            uint g = (uint)Mathf.Clamp(Mathf.RoundToInt(c.g * 63f), 0, 63);
            uint b = (uint)Mathf.Clamp(Mathf.RoundToInt(c.b * 63f), 0, 63);
            return (r << 12) | (g << 6) | b;
        }

        /// <summary>UV (cell centre) of a colour, allocating a new cell if needed.</summary>
        public static Vector2 Uv(Color c)
        {
            Ensure();
            uint key = Pack(c);
            if (map.TryGetValue(key, out Vector2 uv)) return uv;
            if (next >= CellsPerRow * CellsPerRow)
            {
                Debug.LogWarning("[Palette] atlas full, reusing the first cell");
                return map.Count > 0 ? new Vector2(0.5f / CellsPerRow, 0.5f / CellsPerRow) : Vector2.zero;
            }
            int cx = next % CellsPerRow, cy = next / CellsPerRow;
            next++;
            Color32 baseC = new Color32((byte)(((key >> 12) & 63) * 255 / 63), (byte)(((key >> 6) & 63) * 255 / 63), (byte)((key & 63) * 255 / 63), 255);
            for (int y = 0; y < CellPixels; y++)
            {
                for (int x = 0; x < CellPixels; x++)
                {
                    pixels[(cy * CellPixels + y) * Size + cx * CellPixels + x] = baseC;
                }
            }
            uv = new Vector2((cx + 0.5f) / CellsPerRow, (cy + 0.5f) / CellsPerRow);
            map[key] = uv;
            dirty = true;
            return uv;
        }

        public static Texture2D Texture
        {
            get
            {
                Ensure();
                Flush();
                return tex;
            }
        }

        /// <summary>Uploads newly registered colours. Call once after building a batch of meshes.</summary>
        public static void Flush()
        {
            if (!dirty || tex == null) return;
            tex.SetPixels32(pixels);
            tex.Apply(false);
            dirty = false;
        }

        /// <summary>Matte material for skin, cloth, hair, wood and stone.</summary>
        public static Material Standard
        {
            get
            {
                if (standard == null)
                {
                    standard = Mats.Lit(Color.white, 0.22f, 0f, new TexSet { albedo = Texture });
                    standard.name = "Palette_Standard";
                    standard.EnableKeyword("_EMISSION");
                    standard.SetColor("_EmissionColor", Color.black);
                }
                return standard;
            }
        }

        /// <summary>Shiny material for blades, armour plates and jewellery.</summary>
        public static Material Metal
        {
            get
            {
                if (metal == null)
                {
                    metal = Mats.Lit(Color.white, 0.82f, 0.85f, new TexSet { albedo = Texture });
                    metal.name = "Palette_Metal";
                    metal.EnableKeyword("_EMISSION");
                    metal.SetColor("_EmissionColor", Color.black);
                }
                return metal;
            }
        }
    }
}
