using System;
using System.Collections;
using ThienKhuyet.Core;
using ThienKhuyet.Gfx;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThienKhuyet.World
{
    /// <summary>CPU copy of the terrain heights (metres) for fast gameplay queries and chunk planning.</summary>
    public sealed class HeightField
    {
        public const int Res = WorldLayout.HeightRes;
        public readonly float[] h = new float[Res * Res];
        const float Cell = WorldLayout.Size / (Res - 1);

        public float At(int x, int z)
        {
            x = Mathf.Clamp(x, 0, Res - 1);
            z = Mathf.Clamp(z, 0, Res - 1);
            return h[z * Res + x];
        }

        public float Sample(float wx, float wz)
        {
            float fx = Mathf.Clamp(wx / Cell, 0f, Res - 1.001f), fz = Mathf.Clamp(wz / Cell, 0f, Res - 1.001f);
            int x = (int)fx, z = (int)fz;
            float tx = fx - x, tz = fz - z;
            float a = h[z * Res + x], b = h[z * Res + x + 1], c = h[(z + 1) * Res + x], d = h[(z + 1) * Res + x + 1];
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }

        /// <summary>Steepness as rise over run (1 = 45 degrees).</summary>
        public float Gradient(float wx, float wz)
        {
            float dx = Sample(wx + 1.5f, wz) - Sample(wx - 1.5f, wz);
            float dz = Sample(wx, wz + 1.5f) - Sample(wx, wz - 1.5f);
            return Mathf.Sqrt(dx * dx + dz * dz) / 3f;
        }
    }

    public sealed class TerrainResult
    {
        public Terrain terrain;
        public HeightField heights;
    }

    /// <summary>Builds the Unity Terrain (heights, layers, splat maps) from <see cref="WorldGen"/> over several frames.</summary>
    public static class TerrainBuilder
    {
        public static IEnumerator Build(WorldGen gen, WorldAssets assets, Transform parent, TerrainResult result, Action<float, string> report)
        {
            const int Res = HeightField.Res;
            const int CoarseRes = 513;
            var hf = new HeightField();
            result.heights = hf;

            // ---- coarse heights (every 2 m) through the full world function
            var coarse = new float[CoarseRes * CoarseRes];
            const int rowsPerFrame = 20;
            for (int zi = 0; zi < CoarseRes; zi++)
            {
                float wz = zi * (WorldLayout.Size / (CoarseRes - 1));
                for (int xi = 0; xi < CoarseRes; xi++)
                    coarse[zi * CoarseRes + xi] = gen.Height(xi * (WorldLayout.Size / (CoarseRes - 1)), wz);
                if (zi % rowsPerFrame == rowsPerFrame - 1)
                {
                    report?.Invoke(0.25f * zi / CoarseRes, "terrain");
                    yield return null;
                }
            }

            // ---- fine heights: bilinear + small detail noise (kept flat on plateaus and in water)
            var normalized = new float[Res, Res];
            float inv = 1f / WorldLayout.MaxHeight;
            for (int z = 0; z < Res; z++)
            {
                float wz = z * (WorldLayout.Size / (Res - 1));
                float cz = wz / (WorldLayout.Size / (CoarseRes - 1));
                int z0 = Mathf.Min(CoarseRes - 2, (int)cz);
                float tz = cz - z0;
                for (int x = 0; x < Res; x++)
                {
                    float wx = x * (WorldLayout.Size / (Res - 1));
                    float cx = wx / (WorldLayout.Size / (CoarseRes - 1));
                    int x0 = Mathf.Min(CoarseRes - 2, (int)cx);
                    float tx = cx - x0;
                    float a = coarse[z0 * CoarseRes + x0], b = coarse[z0 * CoarseRes + x0 + 1], c = coarse[(z0 + 1) * CoarseRes + x0], d = coarse[(z0 + 1) * CoarseRes + x0 + 1];
                    float h = Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
                    float detail = Noise.Perlin(wx * 0.11f, wz * 0.11f, gen.seed + 91) * 0.55f + Noise.Perlin(wx * 0.37f, wz * 0.37f, gen.seed + 92) * 0.14f;
                    float flat = PlateauWeight(wx, wz);
                    h += detail * (1f - flat);
                    hf.h[z * Res + x] = h;
                    normalized[z, x] = Mathf.Clamp01(h * inv);
                }
                if (z % 96 == 95)
                {
                    report?.Invoke(0.25f + 0.1f * z / Res, "terrain");
                    yield return null;
                }
            }

            // ---- terrain object
            var data = new TerrainData { name = "ThienKhuyetTerrain" };
            data.heightmapResolution = Res;
            data.size = new Vector3(WorldLayout.Size, WorldLayout.MaxHeight, WorldLayout.Size);
            data.SetHeights(0, 0, normalized);
            data.baseMapResolution = 512;
            data.alphamapResolution = 1024;
            data.terrainLayers = assets.terrainLayers;
            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            go.layer = GameLayers.Terrain;
            go.transform.SetParent(parent, false);
            go.transform.position = Vector3.zero;
            Terrain terrain = go.GetComponent<Terrain>();
            terrain.materialTemplate = Mats.Terrain();
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 5f;
            terrain.basemapDistance = 450f;
            terrain.shadowCastingMode = ShadowCastingMode.On;
            terrain.reflectionProbeUsage = ReflectionProbeUsage.Off;
            terrain.drawTreesAndFoliage = false;
            terrain.allowAutoConnect = false;
            result.terrain = terrain;
            yield return null;

            // ---- splat map: coarse weights (4 m) + per-texel slope rock
            const int Coarse = 256, Alpha = 1024;
            var cw = new float[Coarse * Coarse * 6];
            var w = new float[6];
            for (int zi = 0; zi < Coarse; zi++)
            {
                float wz = (zi + 0.5f) * (WorldLayout.Size / Coarse);
                for (int xi = 0; xi < Coarse; xi++)
                {
                    float wx = (xi + 0.5f) * (WorldLayout.Size / Coarse);
                    gen.LayerWeights(wx, wz, hf.Sample(wx, wz), 0f, w);
                    int o = (zi * Coarse + xi) * 6;
                    for (int k = 0; k < 6; k++) cw[o + k] = w[k];
                }
                if (zi % 12 == 11)
                {
                    report?.Invoke(0.35f + 0.15f * zi / Coarse, "splat");
                    yield return null;
                }
            }

            const int slice = 32;
            for (int y0 = 0; y0 < Alpha; y0 += slice)
            {
                var map = new float[slice, Alpha, 6];
                for (int yy = 0; yy < slice; yy++)
                {
                    int ay = y0 + yy;
                    float wz = (ay + 0.5f) * (WorldLayout.Size / Alpha);
                    float cz = Mathf.Clamp(wz / (WorldLayout.Size / Coarse) - 0.5f, 0f, Coarse - 1.001f);
                    int z0 = (int)cz;
                    float tz = cz - z0;
                    for (int ax = 0; ax < Alpha; ax++)
                    {
                        float wx = (ax + 0.5f) * (WorldLayout.Size / Alpha);
                        float cx = Mathf.Clamp(wx / (WorldLayout.Size / Coarse) - 0.5f, 0f, Coarse - 1.001f);
                        int x0 = (int)cx;
                        float tx = cx - x0;
                        float sum = 0f;
                        for (int k = 0; k < 6; k++)
                        {
                            float a = cw[(z0 * Coarse + x0) * 6 + k], b = cw[(z0 * Coarse + x0 + 1) * 6 + k];
                            float c = cw[((z0 + 1) * Coarse + x0) * 6 + k], d = cw[((z0 + 1) * Coarse + x0 + 1) * 6 + k];
                            w[k] = Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
                        }
                        // fine detail: noise breaks up the 4 m grid, slope adds sharp cliff rock
                        float n = Noise.Perlin(wx * 0.09f, wz * 0.09f, gen.seed + 101);
                        w[0] *= 1f + n * 0.25f;
                        w[1] *= 1f - n * 0.25f;
                        float grad = hf.Gradient(wx, wz);
                        float slopeRock = Mathf.Clamp01((grad - 0.55f) / 0.4f);
                        if (slopeRock > w[2])
                        {
                            float scale = (1f - slopeRock) / Mathf.Max(0.0001f, 1f - w[2]);
                            for (int k = 0; k < 6; k++) w[k] *= scale;
                            w[2] = slopeRock;
                        }
                        for (int k = 0; k < 6; k++) sum += w[k];
                        float invSum = 1f / Mathf.Max(0.0001f, sum);
                        for (int k = 0; k < 6; k++) map[yy, ax, k] = w[k] * invSum;
                    }
                }
                data.SetAlphamaps(0, y0, map);
                report?.Invoke(0.5f + 0.2f * y0 / Alpha, "splat");
                yield return null;
            }
            terrain.Flush();
        }

        /// <summary>0..1 how much a point lies on a settlement plateau or in water (areas where fine detail noise is suppressed).</summary>
        static float PlateauWeight(float x, float z)
        {
            float best = 0f;
            for (int i = 0; i < WorldLayout.Pois.Length; i++)
            {
                PoiDef p = WorldLayout.Pois[i];
                if (p.flatten <= 0f) continue;
                float dx = x - p.pos.x, dz = z - p.pos.y;
                float d2 = dx * dx + dz * dz;
                float r = p.flatten * 1.3f;
                if (d2 > r * r) continue;
                float d = Mathf.Sqrt(d2);
                float t = 1f - Mathf.Clamp01((d - p.flatten * 0.6f) / (p.flatten * 0.7f));
                best = Mathf.Max(best, t);
            }
            return best;
        }
    }
}
