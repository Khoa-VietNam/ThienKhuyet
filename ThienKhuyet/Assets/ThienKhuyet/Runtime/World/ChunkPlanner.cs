using System.Collections.Generic;
using ThienKhuyet.Core;
using UnityEngine;

namespace ThienKhuyet.World
{
    public struct Inst
    {
        public Vector3 pos;
        public float yaw;
        public float scale;
        public byte variant;

        public Matrix4x4 Matrix => Matrix4x4.TRS(pos, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale);
    }

    public struct SpawnPlan
    {
        public string id;
        public string enemy;
        public Vector3 pos;
    }

    public struct NodePlan
    {
        public string id;
        public bool ore;
        public Vector3 pos;
    }

    public struct ChestPlan
    {
        public string id;
        public Vector3 pos;
        public float yaw;
    }

    /// <summary>Deterministic content of one 64 m chunk: vegetation, rocks, resource nodes, chests and enemy groups.</summary>
    public sealed class ChunkPlan
    {
        public int cx, cz;
        public Vector3 center;
        public readonly List<Inst>[] inst = new List<Inst>[PropLibrary.Count];
        public readonly List<SpawnPlan> spawns = new List<SpawnPlan>();
        public readonly List<NodePlan> nodes = new List<NodePlan>();
        public readonly List<ChestPlan> chests = new List<ChestPlan>();
        public int InstanceCount;

        public ChunkPlan()
        {
            for (int i = 0; i < inst.Length; i++) inst[i] = new List<Inst>();
        }
    }

    public static class ChunkPlanner
    {
        public static ChunkPlan Plan(int cx, int cz, WorldGen gen, HeightField hf, PropLibrary props, float density)
        {
            var plan = new ChunkPlan { cx = cx, cz = cz };
            float x0 = cx * WorldLayout.ChunkSize, z0 = cz * WorldLayout.ChunkSize;
            plan.center = new Vector3(x0 + WorldLayout.ChunkSize * 0.5f, hf.Sample(x0 + 32f, z0 + 32f), z0 + WorldLayout.ChunkSize * 0.5f);
            var rng = new Rng(cx * 7349 + 13, cz * 9151 + 7, gen.seed);

            const float spacing = 5.2f;
            int n = Mathf.CeilToInt(WorldLayout.ChunkSize / spacing);
            for (int gz = 0; gz < n; gz++)
            {
                for (int gx = 0; gx < n; gx++)
                {
                    float px = x0 + (gx + rng.Value()) * spacing, pz = z0 + (gz + rng.Value()) * spacing;
                    if (px < 6f || pz < 6f || px > WorldLayout.Size - 6f || pz > WorldLayout.Size - 6f) continue;
                    float h = hf.Sample(px, pz);
                    float grad = hf.Gradient(px, pz);
                    float jitter = rng.Value();
                    float species = rng.Value();
                    if (gen.RoadMask(px, pz, 3.8f) > 0.08f) continue;
                    int poi = gen.NearestStructure(px, pz, out float sd);
                    if (poi >= 0 && sd < Clearance(WorldLayout.Pois[poi])) continue;
                    float water = gen.WaterLevel(px, pz);
                    Biome biome = gen.BiomeAt(px, pz, h, grad);
                    float d = WorldGen.TreeDensity(biome) * density;
                    d *= 0.55f + 0.9f * (Noise.Fbm(px * 0.021f, pz * 0.021f, 3, 2f, 0.5f, gen.seed + 17) * 0.5f + 0.5f);
                    if (water > -500f)
                    {
                        // shallows: only reeds
                        if (h > water - 0.3f && h < water + 1.4f && jitter < 0.55f) Add(plan, PropLibrary.Reed, px, h, pz, rng, props);
                        continue;
                    }
                    if (grad > 0.78f) continue;
                    if (biome == Biome.Wetland && h < WorldLayout.LakeLevel + 1.6f && jitter < 0.4f) { Add(plan, PropLibrary.Reed, px, h, pz, rng, props); continue; }
                    if (jitter > d) continue;
                    int sp = PickSpecies(biome, species);
                    if (sp < 0) continue;
                    Add(plan, sp, px, h, pz, rng, props);
                }
            }

            // boulders on slopes and in rocky biomes
            int rocks = rng.Int(1, 4);
            for (int i = 0; i < rocks; i++)
            {
                float px = x0 + rng.Value() * WorldLayout.ChunkSize, pz = z0 + rng.Value() * WorldLayout.ChunkSize;
                float h = hf.Sample(px, pz), grad = hf.Gradient(px, pz);
                if (gen.RoadMask(px, pz, 3f) > 0.05f || gen.WaterLevel(px, pz) > -500f) continue;
                int poi = gen.NearestStructure(px, pz, out float sd);
                if (poi >= 0 && sd < 4f) continue;
                Biome b = gen.BiomeAt(px, pz, h, grad);
                float chance = b == Biome.Rocky || b == Biome.Alpine || b == Biome.Snow ? 0.9f : (grad > 0.35f ? 0.55f : 0.25f);
                if (rng.Value() > chance) continue;
                float roll = rng.Value();
                int sp = (b == Biome.Rocky || b == Biome.Alpine) && roll > 0.55f ? PropLibrary.RockBig : (roll > 0.35f ? PropLibrary.RockMid : PropLibrary.RockSmall);
                Add(plan, sp, px, h, pz, rng, props);
            }

            PlanGameplay(plan, x0, z0, rng, gen, hf);
            return plan;
        }

        static float Clearance(PoiDef p)
        {
            switch (p.kind)
            {
                case PoiKind.Village: return 7f;
                case PoiKind.Camp:
                case PoiKind.Lodge:
                case PoiKind.Scholar:
                case PoiKind.Ruin: return 5f;
                case PoiKind.Clearing:
                case PoiKind.Lair: return 3f;
                default: return 2f;
            }
        }

        static int PickSpecies(Biome b, float r)
        {
            switch (b)
            {
                case Biome.Forest: return r < 0.62f ? PropLibrary.Broad : (r < 0.72f ? PropLibrary.Pine : (r < 0.9f ? PropLibrary.Bush : PropLibrary.Dead));
                case Biome.Pine: return r < 0.78f ? PropLibrary.Pine : (r < 0.86f ? PropLibrary.Broad : (r < 0.97f ? PropLibrary.Bush : PropLibrary.Dead));
                case Biome.Bamboo: return r < 0.88f ? PropLibrary.Bamboo : PropLibrary.Bush;
                case Biome.Blossom: return r < 0.5f ? PropLibrary.Blossom : (r < 0.76f ? PropLibrary.Broad : PropLibrary.Bush);
                case Biome.Meadow: return r < 0.3f ? PropLibrary.Broad : (r < 0.46f ? PropLibrary.Blossom : (r < 0.55f ? PropLibrary.Dead : PropLibrary.Bush));
                case Biome.Wetland: return r < 0.5f ? PropLibrary.Broad : PropLibrary.Bush;
                case Biome.Alpine: return r < 0.5f ? PropLibrary.Pine : (r < 0.75f ? PropLibrary.Dead : PropLibrary.Bush);
                default: return -1;
            }
        }

        static void Add(ChunkPlan plan, int speciesIndex, float x, float h, float z, Rng rng, PropLibrary props)
        {
            Species s = props.species[speciesIndex];
            plan.inst[speciesIndex].Add(new Inst
            {
                pos = new Vector3(x, h - 0.12f, z),
                yaw = rng.Value() * 360f,
                scale = Mathf.Lerp(s.scaleRange.x, s.scaleRange.y, rng.Value()),
                variant = (byte)rng.Int(0, s.Variants - 1)
            });
            plan.InstanceCount++;
        }

        // ------------------------------------------------------------------ gameplay content
        static void PlanGameplay(ChunkPlan plan, float x0, float z0, Rng rng, WorldGen gen, HeightField hf)
        {
            Vector2 c = new Vector2(plan.center.x, plan.center.z);
            Vector2 village = WorldLayout.Poi("thanh_ha").pos, spawn = WorldLayout.Poi("spawn").pos;
            float dVillage = Vector2.Distance(c, village);
            float dSpawn = Vector2.Distance(c, spawn);
            bool safe = dVillage < 100f || dSpawn < 48f;

            // herb / ore nodes
            Vector3 p;
            float hh = plan.center.y;
            Biome cb = gen.BiomeAt(c.x, c.y, hh, hf.Gradient(c.x, c.y));
            if (cb != Biome.Rocky && cb != Biome.Snow && cb != Biome.Alpine && rng.Value() < 0.5f)
            {
                int count = rng.Int(1, 2);
                for (int i = 0; i < count; i++)
                    if (FreeSpot(rng, x0, z0, gen, hf, out p)) plan.nodes.Add(new NodePlan { id = "herb_" + plan.cx + "_" + plan.cz + "_" + i, ore = false, pos = p });
            }
            if ((cb == Biome.Alpine || cb == Biome.Rocky || cb == Biome.Pine) && rng.Value() < 0.3f && FreeSpot(rng, x0, z0, gen, hf, out p))
                plan.nodes.Add(new NodePlan { id = "ore_" + plan.cx + "_" + plan.cz, ore = true, pos = p });
            if (!safe && rng.Value() < 0.05f && FreeSpot(rng, x0, z0, gen, hf, out p))
                plan.chests.Add(new ChestPlan { id = "chest_" + plan.cx + "_" + plan.cz, pos = p, yaw = rng.Value() * 360f });

            if (safe) return;
            // enemy groups depend on the region
            float roll = rng.Value();
            float z = c.y, x = c.x;
            bool banditCamp = Vector2.Distance(c, WorldLayout.Poi("bandit_camp").pos) < 70f;
            if (banditCamp) return; // the camp is populated by its own fixed encounter
            if (z < 640f && x < 560f)
            {
                if (roll < 0.26f) AddGroup(plan, rng, gen, hf, x0, z0, "wolf", rng.Int(2, 3));
                else if (roll < 0.32f) AddGroup(plan, rng, gen, hf, x0, z0, "wolf_pup", 2);
            }
            else if (z < 640f)
            {
                if (roll < 0.14f) AddGroup(plan, rng, gen, hf, x0, z0, "boar", rng.Int(1, 2));
                else if (roll < 0.24f && x > 700f) { AddGroup(plan, rng, gen, hf, x0, z0, "bandit", 2); AddGroup(plan, rng, gen, hf, x0, z0, "bandit_archer", 1); }
                else if (roll < 0.32f) AddGroup(plan, rng, gen, hf, x0, z0, "wolf", 2);
            }
            else
            {
                if (roll < 0.14f) AddGroup(plan, rng, gen, hf, x0, z0, "cultist", rng.Int(1, 2));
                else if (roll < 0.2f) AddGroup(plan, rng, gen, hf, x0, z0, "wolf", 2);
                else if (roll < 0.23f) AddGroup(plan, rng, gen, hf, x0, z0, "stone_guardian", 1);
            }
        }

        static bool FreeSpot(Rng rng, float x0, float z0, WorldGen gen, HeightField hf, out Vector3 p)
        {
            for (int tries = 0; tries < 8; tries++)
            {
                float px = x0 + 4f + rng.Value() * (WorldLayout.ChunkSize - 8f), pz = z0 + 4f + rng.Value() * (WorldLayout.ChunkSize - 8f);
                float h = hf.Sample(px, pz);
                if (hf.Gradient(px, pz) > 0.5f || gen.WaterLevel(px, pz) > -500f || h < WorldLayout.LakeLevel + 1f) continue;
                if (gen.RoadMask(px, pz, 2f) > 0.3f) continue;
                int poi = gen.NearestStructure(px, pz, out float sd);
                if (poi >= 0 && sd < 3f) continue;
                p = new Vector3(px, h, pz);
                return true;
            }
            p = Vector3.zero;
            return false;
        }

        static void AddGroup(ChunkPlan plan, Rng rng, WorldGen gen, HeightField hf, float x0, float z0, string enemy, int count)
        {
            if (!FreeSpot(rng, x0, z0, gen, hf, out Vector3 center)) return;
            for (int i = 0; i < count; i++)
            {
                Vector2 o = rng.InsideCircle() * 4f;
                float px = Mathf.Clamp(center.x + o.x, 4f, WorldLayout.Size - 4f), pz = Mathf.Clamp(center.z + o.y, 4f, WorldLayout.Size - 4f);
                plan.spawns.Add(new SpawnPlan { id = "sp_" + plan.cx + "_" + plan.cz + "_" + enemy + "_" + i, enemy = enemy, pos = new Vector3(px, hf.Sample(px, pz), pz) });
            }
        }
    }
}
