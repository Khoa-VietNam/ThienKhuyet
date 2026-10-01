using System;
using System.Collections.Generic;
using ThienKhuyet.Core;
using UnityEngine;

namespace ThienKhuyet.World
{
    /// <summary>
    /// Deterministic procedural world definition (pure managed, unit-tested): terrain height, river and lake carving,
    /// plateaus for settlements, roads, biomes and terrain layer weights. Everything derives from the seed and <see cref="WorldLayout"/>.
    /// </summary>
    public sealed class WorldGen
    {
        public readonly int seed;
        readonly float[] poiBase;
        readonly List<Vector2[]> roadPaths = new List<Vector2[]>();

        public WorldGen(int seed)
        {
            this.seed = seed;
            poiBase = new float[WorldLayout.Pois.Length];
            for (int i = 0; i < poiBase.Length; i++)
            {
                PoiDef p = WorldLayout.Pois[i];
                poiBase[i] = NaturalHeight(p.pos.x, p.pos.y);
            }
            BuildRoads();
        }

        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        // ------------------------------------------------------------------ terrain height
        /// <summary>Height before rivers, lakes and plateaus (large scale landscape).</summary>
        public float NaturalHeight(float x, float z)
        {
            float rolling = Noise.Fbm(x * 0.0032f, z * 0.0032f, 5, 2f, 0.5f, seed) * 16f;
            float h = 46f + rolling;
            // northern mountain range
            float mtn = Smooth(520f, 900f, z);
            float ridge = Noise.Ridged(x * 0.0042f + 7.3f, z * 0.0042f + 2.1f, 5, 2.05f, 0.5f, seed + 11);
            float peaks = Noise.Fbm(x * 0.012f, z * 0.012f, 3, 2f, 0.5f, seed + 21) * 0.5f + 0.5f;
            h += mtn * (95f + ridge * 120f + peaks * 24f);
            // eastern uplands rising towards the tomb
            h += Smooth(780f, 1000f, x) * Smooth(300f, 860f, z) * 46f * (0.6f + 0.4f * ridge);
            // western hills
            h += Smooth(150f, 0f, x) * 26f * (0.5f + 0.5f * ridge);
            // rim walls keep the player inside the map
            float edge = Mathf.Max(Mathf.Max(Smooth(70f, 0f, x), Smooth(WorldLayout.Size - 70f, WorldLayout.Size, x)),
                Mathf.Max(Smooth(70f, 0f, z), Smooth(WorldLayout.Size - 40f, WorldLayout.Size, z)));
            h += edge * edge * 110f;
            return Mathf.Clamp(h, 6f, WorldLayout.MaxHeight - 4f);
        }

        /// <summary>Final terrain height (metres).</summary>
        public float Height(float x, float z)
        {
            float h = NaturalHeight(x, z);

            // lake basin (smooth ellipse)
            Vector2 lc = WorldLayout.LakeCenter, lr = WorldLayout.LakeRadius;
            float ex = (x - lc.x) / lr.x, ez = (z - lc.y) / lr.y;
            float ed = Mathf.Sqrt(ex * ex + ez * ez);
            if (ed < 1.55f)
            {
                float noiseEdge = Noise.Fbm(x * 0.03f, z * 0.03f, 3, 2f, 0.5f, seed + 5) * 0.14f;
                float d = ed + noiseEdge;
                float basin = WorldLayout.LakeLevel - 4.5f * (1f - Smooth(0f, 1f, d)) - 0.8f;
                float shore = WorldLayout.LakeLevel + 0.4f + 6f * Mathf.Pow(Mathf.Max(0f, d - 1f), 1.4f);
                float lakeH = d < 1f ? basin : shore;
                float w = 1f - Smooth(1.0f, 1.55f, d);
                h = Mathf.Lerp(h, lakeH, w);
            }

            // settlement plateaus
            for (int i = 0; i < WorldLayout.Pois.Length; i++)
            {
                PoiDef p = WorldLayout.Pois[i];
                if (p.flatten <= 0f) continue;
                float dx = x - p.pos.x, dz = z - p.pos.y;
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d > p.flatten * 1.35f) continue;
                float target = poiBase[i] + p.heightOffset;
                float w = 1f - Smooth(p.flatten * 0.62f, p.flatten * 1.3f, d);
                h = Mathf.Lerp(h, target, w);
            }
            // river channel
            if (RiverQuery(x, z, out float surface, out float dist, out _))
            {
                float R = WorldLayout.RiverInfluence, half = WorldLayout.RiverHalfWidth;
                if (dist < R)
                {
                    float bank = Smooth(half, R, dist);
                    float bed = surface - 1.5f * (1f - Smooth(0f, half, dist));
                    float target = bed + bank * 7f;
                    h = Mathf.Min(h, Mathf.Lerp(target, h, Smooth(R * 0.6f, R, dist)));
                }
            }

            return Mathf.Clamp(h, 2f, WorldLayout.MaxHeight);
        }

        // ------------------------------------------------------------------ water
        /// <summary>Nearest river node: water surface height at that point, planar distance to the centreline and its segment parameter.</summary>
        public bool RiverQuery(float x, float z, out float surface, out float dist, out float t)
        {
            Vector3[] r = WorldLayout.River;
            float best = float.MaxValue;
            surface = 0f;
            t = 0f;
            for (int i = 0; i < r.Length - 1; i++)
            {
                Vector2 a = new Vector2(r[i].x, r[i].y), b = new Vector2(r[i + 1].x, r[i + 1].y);
                Vector2 ab = b - a;
                float len2 = ab.sqrMagnitude;
                float u = len2 > 0f ? Mathf.Clamp01(Vector2.Dot(new Vector2(x, z) - a, ab) / len2) : 0f;
                Vector2 p = a + ab * u;
                float d = Vector2.Distance(new Vector2(x, z), p);
                if (d < best)
                {
                    best = d;
                    surface = Mathf.Lerp(r[i].z, r[i + 1].z, u);
                    t = (i + u) / (r.Length - 1);
                }
            }
            dist = best;
            return true;
        }

        /// <summary>Water surface height at x,z or -1000 when there is no water (lake or river).</summary>
        public float WaterLevel(float x, float z)
        {
            Vector2 lc = WorldLayout.LakeCenter, lr = WorldLayout.LakeRadius;
            float ex = (x - lc.x) / lr.x, ez = (z - lc.y) / lr.y;
            float noiseEdge = Noise.Fbm(x * 0.03f, z * 0.03f, 3, 2f, 0.5f, seed + 5) * 0.14f;
            if (Mathf.Sqrt(ex * ex + ez * ez) + noiseEdge < 1.02f) return WorldLayout.LakeLevel;
            RiverQuery(x, z, out float surface, out float dist, out _);
            if (dist < WorldLayout.RiverHalfWidth + 0.8f) return surface;
            return -1000f;
        }

        // ------------------------------------------------------------------ roads
        void BuildRoads()
        {
            for (int c = 0; c < WorldLayout.Roads.Length; c++)
            {
                string[] chain = WorldLayout.Roads[c];
                var pts = new List<Vector2>();
                for (int i = 0; i < chain.Length; i++)
                {
                    PoiDef p = WorldLayout.Poi(chain[i]);
                    if (p != null) pts.Add(p.pos);
                }
                if (pts.Count < 2) continue;
                roadPaths.Add(Curve(pts, c));
            }
        }

        /// <summary>Smooth, slightly meandering polyline through the control points (Catmull-Rom with noise offsets).</summary>
        Vector2[] Curve(List<Vector2> pts, int salt)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector2 p0 = pts[Mathf.Max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(pts.Count - 1, i + 2)];
                float len = Vector2.Distance(p1, p2);
                int steps = Mathf.Max(4, (int)(len / 12f));
                for (int s = 0; s < steps; s++)
                {
                    float t = (float)s / steps;
                    Vector2 q = 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);
                    float wob = Mathf.Sin(t * Mathf.PI) * 9f;
                    Vector2 dir = (p2 - p1).normalized;
                    Vector2 side = new Vector2(-dir.y, dir.x);
                    q += side * (Noise.Perlin((q.x + salt * 31f) * 0.02f, (q.y + salt * 17f) * 0.02f, seed + 41) * wob);
                    result.Add(q);
                }
            }
            result.Add(pts[pts.Count - 1]);
            return result.ToArray();
        }

        public IReadOnlyList<Vector2[]> RoadPaths => roadPaths;

        /// <summary>0..1 influence of the nearest road at x,z (1 on the road surface).</summary>
        public float RoadMask(float x, float z, float halfWidth = 2.2f)
        {
            float best = float.MaxValue;
            var p = new Vector2(x, z);
            for (int r = 0; r < roadPaths.Count; r++)
            {
                Vector2[] path = roadPaths[r];
                for (int i = 0; i < path.Length - 1; i++)
                {
                    float d = DistToSegment(p, path[i], path[i + 1]);
                    if (d < best) best = d;
                }
            }
            return 1f - Smooth(halfWidth, halfWidth + 2.2f, best);
        }

        static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float l2 = ab.sqrMagnitude;
            float u = l2 > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2) : 0f;
            return Vector2.Distance(p, a + ab * u);
        }

        // ------------------------------------------------------------------ biomes
        public Biome BiomeAt(float x, float z, float height, float slope)
        {
            if (height > 215f) return Biome.Snow;
            if (height > 160f || slope > 0.62f) return slope > 0.7f ? Biome.Rocky : Biome.Alpine;
            float water = WaterLevel(x, z);
            float moist = Noise.Fbm(x * 0.006f + 40f, z * 0.006f + 12f, 3, 2f, 0.5f, seed + 61) * 0.5f + 0.5f;
            float forest = Noise.Fbm(x * 0.009f, z * 0.009f, 4, 2f, 0.5f, seed + 71) * 0.5f + 0.5f;
            // moisture increases towards water
            RiverQuery(x, z, out _, out float rd, out _);
            Vector2 lc = WorldLayout.LakeCenter, lr = WorldLayout.LakeRadius;
            float lakeD = Mathf.Sqrt(Mathf.Pow((x - lc.x) / lr.x, 2f) + Mathf.Pow((z - lc.y) / lr.y, 2f));
            if (lakeD < 1.25f || rd < 14f) return Biome.Wetland;
            moist += (1f - Smooth(10f, 70f, rd)) * 0.3f + (1f - Smooth(1f, 1.9f, lakeD)) * 0.3f;
            if (height > 105f) return forest > 0.38f ? Biome.Pine : Biome.Alpine;
            // blossom groves near the village and the hidden springs
            for (int i = 0; i < WorldLayout.Pois.Length; i++)
            {
                PoiDef p = WorldLayout.Pois[i];
                if (p.kind != PoiKind.Village && p.kind != PoiKind.Spring && p.kind != PoiKind.Lodge) continue;
                if (Vector2.Distance(new Vector2(x, z), p.pos) < p.radius * 1.9f && forest > 0.45f) return Biome.Blossom;
            }
            if (moist > 0.78f && forest > 0.4f) return Biome.Bamboo;
            if (forest > 0.5f) return height > 80f ? Biome.Pine : Biome.Forest;
            if (forest > 0.36f) return Biome.Forest;
            return Biome.Meadow;
        }

        /// <summary>Tree density 0..1 for a biome (before clearing around structures and roads).</summary>
        public static float TreeDensity(Biome b)
        {
            switch (b)
            {
                case Biome.Forest: return 0.85f;
                case Biome.Pine: return 0.8f;
                case Biome.Bamboo: return 1f;
                case Biome.Blossom: return 0.45f;
                case Biome.Meadow: return 0.1f;
                case Biome.Wetland: return 0.18f;
                case Biome.Alpine: return 0.12f;
                case Biome.Rocky: return 0.02f;
                default: return 0f;
            }
        }

        /// <summary>Planar distance to the nearest POI that clears vegetation (settlement, camp...). Returns the POI index or -1.</summary>
        public int NearestStructure(float x, float z, out float dist)
        {
            int best = -1;
            dist = float.MaxValue;
            for (int i = 0; i < WorldLayout.Pois.Length; i++)
            {
                PoiDef p = WorldLayout.Pois[i];
                float d = Vector2.Distance(new Vector2(x, z), p.pos) - p.flatten;
                if (d < dist) { dist = d; best = i; }
            }
            return best;
        }

        /// <summary>Terrain layer weights (grass, dirt, rock, sand/mud, snow, forest floor). Sums to 1.</summary>
        public void LayerWeights(float x, float z, float height, float slope, float[] w)
        {
            float rock = Smooth(0.5f, 0.82f, slope) + Smooth(150f, 205f, height) * 0.55f;
            float snow = Smooth(196f, 232f, height + Noise.Fbm(x * 0.03f, z * 0.03f, 3, 2f, 0.5f, seed + 3) * 12f) * (1f - Smooth(0.5f, 0.9f, slope));
            float waterLevel = WaterLevel(x, z);
            RiverQuery(x, z, out float rs, out float rd, out _);
            Vector2 lc = WorldLayout.LakeCenter, lr = WorldLayout.LakeRadius;
            float lakeD = Mathf.Sqrt(Mathf.Pow((x - lc.x) / lr.x, 2f) + Mathf.Pow((z - lc.y) / lr.y, 2f));
            float nearWater = Mathf.Max(1f - Smooth(WorldLayout.RiverHalfWidth, WorldLayout.RiverHalfWidth + 6f, rd), 1f - Smooth(1.0f, 1.18f, lakeD));
            float sand = nearWater * 0.9f;
            float road = RoadMask(x, z);
            float village = 0f;
            for (int i = 0; i < WorldLayout.Pois.Length; i++)
            {
                PoiDef p = WorldLayout.Pois[i];
                if (p.kind == PoiKind.Village || p.kind == PoiKind.Camp || p.kind == PoiKind.Scholar || p.kind == PoiKind.Lodge || p.kind == PoiKind.Ground || p.kind == PoiKind.Hut)
                    village = Mathf.Max(village, (1f - Smooth(p.flatten * 0.4f, p.flatten * 1.05f, Vector2.Distance(new Vector2(x, z), p.pos))) * 0.75f);
                else if (p.kind == PoiKind.Ruin || p.kind == PoiKind.Terrace || p.kind == PoiKind.Tomb || p.kind == PoiKind.Hidden || p.kind == PoiKind.Crater)
                    rock = Mathf.Max(rock, (1f - Smooth(p.flatten * 0.35f, p.flatten, Vector2.Distance(new Vector2(x, z), p.pos))) * 0.85f);
            }
            float dirtNoise = Smooth(0.62f, 0.8f, Noise.Fbm(x * 0.02f, z * 0.02f, 3, 2f, 0.5f, seed + 8) * 0.5f + 0.5f) * 0.55f;
            float dirt = Mathf.Max(road, Mathf.Max(village, dirtNoise));
            Biome b = BiomeAt(x, z, height, slope);
            float floor = (b == Biome.Forest || b == Biome.Pine || b == Biome.Bamboo || b == Biome.Blossom) ? 0.6f * Smooth(0.35f, 0.7f, Noise.Fbm(x * 0.05f, z * 0.05f, 3, 2f, 0.5f, seed + 9) * 0.5f + 0.5f) + 0.25f : 0f;
            rock = Mathf.Clamp01(rock);
            float grass = Mathf.Max(0.05f, 1f - rock - snow - sand * 0.8f - dirt * 0.8f);
            w[0] = grass;
            w[1] = dirt * (1f - rock);
            w[2] = rock;
            w[3] = sand * (1f - rock);
            w[4] = snow;
            w[5] = floor * grass;
            float sum = w[0] + w[1] + w[2] + w[3] + w[4] + w[5];
            if (sum < 1e-4f) { w[0] = 1f; sum = 1f; }
            for (int i = 0; i < 6; i++) w[i] /= sum;
        }

        // ------------------------------------------------------------------ helpers
        public static Vector2 ToChunk(float x, float z)
        {
            return new Vector2(Mathf.Floor(x / WorldLayout.ChunkSize), Mathf.Floor(z / WorldLayout.ChunkSize));
        }
    }
}
