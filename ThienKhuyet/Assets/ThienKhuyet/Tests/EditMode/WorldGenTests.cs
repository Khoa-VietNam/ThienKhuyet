using System;
using System.IO;
using NUnit.Framework;
using ThienKhuyet.World;
using UnityEngine;

namespace ThienKhuyet.Tests
{
    public class WorldGenTests
    {
        static readonly WorldGen gen = new WorldGen(20260);

        [Test]
        public void HeightsStayWithinTerrainBounds()
        {
            for (float x = 0; x <= WorldLayout.Size; x += 64f)
            {
                for (float z = 0; z <= WorldLayout.Size; z += 64f)
                {
                    float h = gen.Height(x, z);
                    Assert.GreaterOrEqual(h, 2f);
                    Assert.LessOrEqual(h, WorldLayout.MaxHeight);
                }
            }
        }

        [Test]
        public void IsDeterministic()
        {
            var other = new WorldGen(20260);
            Assert.AreEqual(gen.Height(300.5f, 410.25f), other.Height(300.5f, 410.25f), 1e-4f);
            var different = new WorldGen(5);
            Assert.AreNotEqual(gen.Height(300.5f, 410.25f), different.Height(300.5f, 410.25f));
        }

        [Test]
        public void SettlementsArePlateausAboveWater()
        {
            foreach (PoiDef p in WorldLayout.Pois)
            {
                if (p.flatten < 12f || p.kind == PoiKind.Crater) continue;
                float c = gen.Height(p.pos.x, p.pos.y);
                float maxDev = 0f;
                for (int i = 0; i < 12; i++)
                {
                    float a = i / 12f * Mathf.PI * 2f;
                    float r = p.flatten * 0.45f;
                    float h = gen.Height(p.pos.x + Mathf.Cos(a) * r, p.pos.y + Mathf.Sin(a) * r);
                    maxDev = Mathf.Max(maxDev, Mathf.Abs(h - c));
                }
                Assert.Less(maxDev, 1.6f, p.id + " plateau is not flat (" + maxDev + " m)");
                Assert.Less(gen.WaterLevel(p.pos.x, p.pos.y), -500f, p.id + " is under water");
            }
        }

        [Test]
        public void RiverFlowsDownhillIntoTheLake()
        {
            Vector3[] r = WorldLayout.River;
            for (int i = 1; i < r.Length; i++) Assert.LessOrEqual(r[i].z, r[i - 1].z, "river node " + i + " rises");
            Assert.AreEqual(WorldLayout.LakeLevel, r[r.Length - 1].z, 0.01f);
            // the channel is carved below the surface
            for (int i = 0; i < r.Length; i += 2)
            {
                float h = gen.Height(r[i].x, r[i].y);
                Assert.Less(h, r[i].z + 0.2f, "river bed at node " + i + " is above the water surface");
                Assert.AreEqual(r[i].z, gen.WaterLevel(r[i].x, r[i].y), 0.5f);
            }
        }

        [Test]
        public void LakeHasWaterAndTheShoreIsDry()
        {
            Vector2 c = WorldLayout.LakeCenter;
            Assert.AreEqual(WorldLayout.LakeLevel, gen.WaterLevel(c.x, c.y), 0.01f);
            Assert.Less(gen.Height(c.x, c.y), WorldLayout.LakeLevel);
            Assert.Less(gen.WaterLevel(c.x + WorldLayout.LakeRadius.x * 1.6f, c.y), -500f);
        }

        [Test]
        public void SpawnToVillageRoadExists()
        {
            PoiDef a = WorldLayout.Poi("spawn"), b = WorldLayout.Poi("thanh_ha");
            Assert.Greater(gen.RoadMask(a.pos.x, a.pos.y), 0.5f);
            Assert.Greater(gen.RoadMask(b.pos.x, b.pos.y), 0.5f);
            Assert.Less(gen.RoadMask(700f, 700f), 0.01f);
        }

        [Test]
        public void LayerWeightsAreNormalized()
        {
            var w = new float[6];
            for (float x = 20; x < 1000; x += 97)
            {
                for (float z = 20; z < 1000; z += 89)
                {
                    float h = gen.Height(x, z);
                    float slope = Mathf.Clamp01(Mathf.Abs(gen.Height(x + 1, z) - h) + Mathf.Abs(gen.Height(x, z + 1) - h));
                    gen.LayerWeights(x, z, h, slope, w);
                    float sum = 0f;
                    for (int i = 0; i < 6; i++) { Assert.GreaterOrEqual(w[i], 0f); sum += w[i]; }
                    Assert.AreEqual(1f, sum, 1e-3f);
                }
            }
        }

        [Test]
        public void StartAreaIsWalkable()
        {
            PoiDef s = WorldLayout.Poi("spawn");
            float maxSlope = 0f;
            for (float dx = -20; dx <= 20; dx += 4)
                for (float dz = -20; dz <= 20; dz += 4)
                {
                    float h0 = gen.Height(s.pos.x + dx, s.pos.y + dz);
                    float h1 = gen.Height(s.pos.x + dx + 2f, s.pos.y + dz);
                    maxSlope = Mathf.Max(maxSlope, Mathf.Abs(h1 - h0) / 2f);
                }
            Assert.Less(maxSlope, 0.35f);
        }

        /// <summary>Optional: TK_WORLD_DUMP=/path/world.ppm writes a shaded preview used to review the layout offline.</summary>
        [Test]
        public void DumpPreviewWhenRequested()
        {
            string path = Environment.GetEnvironmentVariable("TK_WORLD_DUMP");
            if (string.IsNullOrEmpty(path)) { Assert.Pass(); return; }
            const int n = 512;
            var rgb = new byte[n * n * 3];
            var w = new float[6];
            for (int py = 0; py < n; py++)
            {
                for (int px = 0; px < n; px++)
                {
                    float x = px / (float)(n - 1) * WorldLayout.Size, z = (n - 1 - py) / (float)(n - 1) * WorldLayout.Size;
                    float h = gen.Height(x, z);
                    float hx = gen.Height(x + 2f, z) - h, hz = gen.Height(x, z + 2f) - h;
                    float slope = Mathf.Clamp01(Mathf.Sqrt(hx * hx + hz * hz) / 2f);
                    float shade = Mathf.Clamp01(0.62f - (hx - hz) * 0.12f);
                    gen.LayerWeights(x, z, h, slope, w);
                    float r = w[0] * 0.35f + w[1] * 0.62f + w[2] * 0.5f + w[3] * 0.78f + w[4] * 0.95f + w[5] * 0.2f;
                    float g = w[0] * 0.6f + w[1] * 0.5f + w[2] * 0.5f + w[3] * 0.72f + w[4] * 0.97f + w[5] * 0.4f;
                    float b = w[0] * 0.25f + w[1] * 0.35f + w[2] * 0.5f + w[3] * 0.5f + w[4] * 1f + w[5] * 0.2f;
                    float water = gen.WaterLevel(x, z);
                    if (water > -500f) { r = 0.15f; g = 0.35f; b = 0.7f; }
                    int i = (py * n + px) * 3;
                    rgb[i] = (byte)(Mathf.Clamp01(r * shade * 1.4f) * 255);
                    rgb[i + 1] = (byte)(Mathf.Clamp01(g * shade * 1.4f) * 255);
                    rgb[i + 2] = (byte)(Mathf.Clamp01(b * shade * 1.4f) * 255);
                }
            }
            foreach (PoiDef p in WorldLayout.Pois)
            {
                int cx = (int)(p.pos.x / WorldLayout.Size * (n - 1)), cy = n - 1 - (int)(p.pos.y / WorldLayout.Size * (n - 1));
                for (int dy = -3; dy <= 3; dy++)
                    for (int dx = -3; dx <= 3; dx++)
                    {
                        int x = cx + dx, y = cy + dy;
                        if (x < 0 || y < 0 || x >= n || y >= n) continue;
                        int i = (y * n + x) * 3;
                        rgb[i] = 255; rgb[i + 1] = p.hidden ? (byte)0 : (byte)40; rgb[i + 2] = 40;
                    }
            }
            using (var fs = new FileStream(path, FileMode.Create))
            {
                byte[] head = System.Text.Encoding.ASCII.GetBytes("P6\n" + n + " " + n + "\n255\n");
                fs.Write(head, 0, head.Length);
                fs.Write(rgb, 0, rgb.Length);
            }
            Assert.Pass();
        }
    }
}
