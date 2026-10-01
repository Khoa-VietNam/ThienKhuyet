using System;
using System.Collections;
using System.Collections.Generic;
using ThienKhuyet.Combat;
using ThienKhuyet.Core;
using ThienKhuyet.Data;
using ThienKhuyet.Gfx;
using UnityEngine;

namespace ThienKhuyet.World
{
    public struct Anchor
    {
        public Vector3 pos;
        public float yaw;
    }

    public struct NpcPlacement
    {
        public string npcId;
        public string anchor;
        public Vector3 pos;
        public float yaw;
    }

    public struct FixedSpawn
    {
        public string id;
        public string enemy;
        public Vector3 pos;
    }

    public sealed class PoiInstance
    {
        public PoiDef def;
        public GameObject root;
        public Vector3 center;
        public readonly List<GameObject> streamed = new List<GameObject>();
        public bool active = true;
    }

    /// <summary>Builds the hand-authored settlements and landmarks of the world from procedural parts, and registers anchors, NPC spots and fixed encounters.</summary>
    public sealed class PoiBuilder
    {
        readonly WorldGen gen;
        readonly HeightField hf;
        readonly WorldAssets A;
        readonly PropLibrary props;
        readonly Transform parent;

        public readonly List<PoiInstance> instances = new List<PoiInstance>();
        public readonly Dictionary<string, Anchor> anchors = new Dictionary<string, Anchor>();
        public readonly List<NpcPlacement> npcs = new List<NpcPlacement>();
        public readonly List<FixedSpawn> spawns = new List<FixedSpawn>();
        public readonly List<MeditationSpot> meditationSpots = new List<MeditationSpot>();
        public readonly List<StoneDoor> stoneDoors = new List<StoneDoor>();
        public readonly List<TestingStone> testingStones = new List<TestingStone>();
        public readonly List<Shrine> shrines = new List<Shrine>();
        public Bed hutBed;

        public PoiBuilder(WorldGen gen, HeightField hf, WorldAssets assets, PropLibrary props, Transform parent)
        {
            this.gen = gen;
            this.hf = hf;
            A = assets;
            this.props = props;
            this.parent = parent;
        }

        float G(float x, float z) { return hf.Sample(x, z); }

        void SetAnchor(string id, Vector3 pos, float yaw = 0f)
        {
            anchors[id] = new Anchor { pos = pos, yaw = yaw };
        }

        /// <summary>World position of a point given in a structure's local (x east, z north) frame.</summary>
        Vector3 W(Vector2 center, float lx, float lz, float yOffset = 0f)
        {
            float x = center.x + lx, z = center.y + lz;
            return new Vector3(x, G(x, z) + yOffset, z);
        }

        static float FaceYaw(float lx, float lz)
        {
            return Mathf.Atan2(-lx, -lz) * Mathf.Rad2Deg;
        }

        // ------------------------------------------------------------------ driver
        public IEnumerator BuildAll(Action<float, string> report)
        {
            var root = new GameObject("Pois");
            root.transform.SetParent(parent, false);
            int n = WorldLayout.Pois.Length;
            for (int i = 0; i < n; i++)
            {
                PoiDef p = WorldLayout.Pois[i];
                var holder = new GameObject("POI_" + p.id);
                holder.transform.SetParent(root.transform, false);
                var inst = new PoiInstance { def = p, root = holder, center = new Vector3(p.pos.x, G(p.pos.x, p.pos.y), p.pos.y) };
                instances.Add(inst);
                SetAnchor(p.id, inst.center, p.yaw);
                switch (p.id)
                {
                    case "spawn": BuildSpawn(inst); break;
                    case "wolf_den": BuildWolfDen(inst); break;
                    case "riverside_hut": BuildHut(inst); break;
                    case "thanh_ha": BuildVillage(inst); break;
                    case "training_ground": BuildTraining(inst); break;
                    case "vein_terrace": BuildTerrace(inst); break;
                    case "bandit_camp": BuildBanditCamp(inst); break;
                    case "alpha_lair": BuildAlphaLair(inst); break;
                    case "jade_lodge": BuildLodge(inst); break;
                    case "scholar_camp": BuildScholarCamp(inst); break;
                    case "ruin": BuildRuin(inst); break;
                    case "cold_cave": BuildCaveMouth(inst); break;
                    case "memory_road": BuildMemoryRoad(inst); break;
                    case "spirit_spring": BuildSpring(inst); break;
                    case "sword_tomb": BuildSwordTomb(inst); break;
                    case "heart": BuildHeart(inst); break;
                    case "rift_valley": BuildRiftValley(inst); break;
                }
                report?.Invoke((float)(i + 1) / n, "poi");
                yield return null;
            }
            BuildBridges(root.transform);
        }

        // ------------------------------------------------------------------ helpers
        void Scatter(Transform t, Vector2 c, float r, int count, int speciesIndex, int seed, float minR = 0f)
        {
            var rng = new Rng(seed);
            Species sp = props.species[speciesIndex];
            var mf = new List<CombineInstance>();
            for (int i = 0; i < count; i++)
            {
                float a = rng.Value() * Mathf.PI * 2f, d = Mathf.Lerp(minR, r, Mathf.Sqrt(rng.Value()));
                float x = c.x + Mathf.Cos(a) * d, z = c.y + Mathf.Sin(a) * d;
                var go = new GameObject(sp.id);
                go.transform.SetParent(t, false);
                go.transform.SetPositionAndRotation(new Vector3(x, G(x, z) - 0.1f, z), Quaternion.Euler(0f, rng.Value() * 360f, 0f));
                go.transform.localScale = Vector3.one * Mathf.Lerp(sp.scaleRange.x, sp.scaleRange.y, rng.Value());
                go.AddComponent<MeshFilter>().sharedMesh = sp.lod[0];
                var mr = go.AddComponent<MeshRenderer>();
                var mats = new Material[sp.SubmeshCount];
                for (int s = 0; s < mats.Length; s++) mats[s] = sp.materials[s][Mathf.Min(rng.Int(0, sp.Variants - 1), sp.materials[s].Length - 1)];
                mr.sharedMaterials = mats;
                if (sp.colliderRadius > 0f)
                {
                    go.layer = GameLayers.Environment;
                    var cc = go.AddComponent<CapsuleCollider>();
                    cc.radius = sp.colliderRadius; cc.height = sp.colliderHeight; cc.center = new Vector3(0f, sp.colliderHeight * 0.5f, 0f);
                }
            }
        }

        StructureKit Kit(PoiInstance inst, string name, Vector3 pos, float yaw)
        {
            return new StructureKit(inst.root.transform, name, pos, yaw, A);
        }

        void Campfire(PoiInstance inst, Vector3 pos)
        {
            var go = new GameObject("Campfire");
            go.transform.SetParent(inst.root.transform, false);
            go.transform.position = pos;
            var mb = new MeshBuilder();
            mb.Sub(0).PaletteUv(Palette.Uv(new Color(0.4f, 0.28f, 0.17f)));
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f;
                mb.Limb(new Vector3(Mathf.Cos(a) * 0.6f, 0.08f, Mathf.Sin(a) * 0.6f), new Vector3(Mathf.Cos(a + 0.4f) * 0.1f, 0.35f, Mathf.Sin(a + 0.4f) * 0.1f), 0.07f, 0.05f, 5, false, false);
            }
            mb.Sub(0).PaletteUv(Palette.Uv(new Color(0.45f, 0.45f, 0.47f)));
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                mb.Ellipsoid(new Vector3(Mathf.Cos(a) * 0.85f, 0.1f, Mathf.Sin(a) * 0.85f), new Vector3(0.18f, 0.13f, 0.16f), 6, 4);
            }
            Palette.Flush();
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("campfire", false);
            go.AddComponent<MeshRenderer>().sharedMaterial = Palette.Standard;
            Vfx.Play("campfire", pos + Vector3.up * 0.2f, Quaternion.identity, 1f);
            var l = new GameObject("Light").AddComponent<Light>();
            l.transform.SetParent(go.transform, false);
            l.transform.localPosition = Vector3.up * 1f;
            l.type = LightType.Point; l.range = 12f; l.intensity = 3.5f; l.color = new Color(1f, 0.6f, 0.3f); l.shadows = LightShadows.None;
            var bc = go.AddComponent<SphereCollider>();
            bc.radius = 0.8f;
            go.layer = GameLayers.Environment;
        }

        void Bones(PoiInstance inst, Vector2 c, float r, int count, int seed)
        {
            var kit = Kit(inst, "Bones", new Vector3(c.x, G(c.x, c.y), c.y), 0f);
            var rng = new Rng(seed);
            kit.M(A.matPlaster);
            for (int i = 0; i < count; i++)
            {
                float a = rng.Value() * Mathf.PI * 2f, d = rng.Value() * r;
                Vector3 p = new Vector3(Mathf.Cos(a) * d, G(c.x + Mathf.Cos(a) * d, c.y + Mathf.Sin(a) * d) - G(c.x, c.y) + 0.06f, Mathf.Sin(a) * d);
                Vector3 dir = new Vector3(Mathf.Cos(rng.Value() * 6.28f), 0f, Mathf.Sin(rng.Value() * 6.28f)) * (0.3f + rng.Value() * 0.4f);
                kit.M(A.matPlaster).Limb(p, p + dir, 0.04f, 0.03f, 5, true, true);
            }
            kit.M(A.matPlaster).Ellipsoid(new Vector3(0.3f, 0.12f, 0.2f), new Vector3(0.2f, 0.14f, 0.24f), 7, 5, 1f);
            kit.Finish(false);
        }

        void RockRing(PoiInstance inst, Vector2 c, float r, int n, int seed, float size = 1.6f)
        {
            var rng = new Rng(seed);
            Species sp = props.species[PropLibrary.RockMid];
            for (int i = 0; i < n; i++)
            {
                float a = (i + rng.Value() * 0.4f) / n * Mathf.PI * 2f;
                float x = c.x + Mathf.Cos(a) * r, z = c.y + Mathf.Sin(a) * r;
                var go = new GameObject("Rock");
                go.transform.SetParent(inst.root.transform, false);
                go.transform.SetPositionAndRotation(new Vector3(x, G(x, z) - 0.1f, z), Quaternion.Euler(0f, rng.Value() * 360f, 0f));
                go.transform.localScale = Vector3.one * size * (0.8f + rng.Value() * 0.8f);
                go.layer = GameLayers.Environment;
                go.AddComponent<MeshFilter>().sharedMesh = sp.lod[0];
                go.AddComponent<MeshRenderer>().sharedMaterial = sp.materials[0][0];
                var sc = go.AddComponent<SphereCollider>();
                sc.radius = 1f; sc.center = new Vector3(0f, 0.6f, 0f);
            }
        }

        void Monolith(StructureKit k, Vector3 pos, float h, bool glow)
        {
            k.M(A.matWall).Cylinder(pos, h, 0.7f, 0.45f, 5, true, true, 2f);
            if (glow)
            {
                k.M(A.matGlowCyan).Box(pos + new Vector3(0f, h * 0.55f, 0.46f), new Vector3(0.08f, h * 0.35f, 0.03f), 1f);
                k.M(A.matGlowCyan).Box(pos + new Vector3(0.15f, h * 0.7f, 0.46f), new Vector3(0.3f, 0.06f, 0.03f), 1f);
            }
            k.Capsule(pos, h, 0.6f);
        }

        // ------------------------------------------------------------------ spawn clearing
        void BuildSpawn(PoiInstance inst)
        {
            Vector3 c = inst.center;
            SetAnchor("spawn_player", c, 20f);
            SetAnchor("wake_point", c + new Vector3(0f, 0f, 0f), 20f);
            RuneDecal.Create(inst.root.transform, c, 4.2f, new Color(0.45f, 0.8f, 1f), 0.4f, 4f, 5);
            var kit = Kit(inst, "Fallen", c, 0f);
            // a fallen log and a few moss stones around the clearing
            kit.M(A.matBark).Limb(new Vector3(-6f, 0.35f, 4f), new Vector3(-1.5f, 0.35f, 6.5f), 0.4f, 0.35f, 8, true, true);
            kit.Capsule(new Vector3(-4.2f, 0f, 5f), 0.8f, 1.6f);
            kit.Finish();
            RockRing(inst, new Vector2(c.x, c.z), 9.5f, 5, 11, 1.1f);
        }

        void BuildWolfDen(PoiInstance inst)
        {
            Vector2 c = new Vector2(inst.center.x, inst.center.z);
            RockRing(inst, c, 12f, 9, 21, 1.8f);
            Bones(inst, c, 7f, 14, 5);
            Scatter(inst.root.transform, c, 18f, 5, PropLibrary.Dead, 3, 9f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 1.7f + 0.5f;
                spawns.Add(new FixedSpawn { id = "den_wolf_" + i, enemy = "wolf", pos = new Vector3(c.x + Mathf.Cos(a) * 3.5f, G(c.x, c.y), c.y + Mathf.Sin(a) * 3.5f) });
            }
            spawns.Add(new FixedSpawn { id = "den_pup", enemy = "wolf_pup", pos = new Vector3(c.x + 1f, G(c.x, c.y), c.y - 2f) });
            Container.Create(inst.root.transform, "den_chest", new Vector3(c.x - 4f, G(c.x - 4f, c.y + 3f), c.y + 3f), 140f, "chest_common", A);
            SetAnchor("den_center", inst.center, 0f);
        }

        // ------------------------------------------------------------------ riverside hut (the first shelter)
        void BuildHut(PoiInstance inst)
        {
            Vector3 c = inst.center;
            var kit = Kit(inst, "Hut", c, 200f);
            Structures.House(kit, Vector3.zero, 5.2f, 4.2f, 2.7f, A.matPlaster, A.matRoofDark, true, true);
            kit.Finish();
            // the bed inside is interactive
            var bedGo = new GameObject("Bed");
            bedGo.layer = GameLayers.Interactable;
            bedGo.transform.SetParent(kit.root, false);
            bedGo.transform.localPosition = new Vector3(-1.5f, 0.8f, -0.6f);
            var bc = bedGo.AddComponent<BoxCollider>();
            bc.size = new Vector3(1.6f, 1.2f, 2.6f);
            bc.isTrigger = true;
            hutBed = bedGo.AddComponent<Bed>();
            hutBed.bedId = "hut_bed";
            Vector3 bedWorld = bedGo.transform.position;
            SetAnchor("hut_bed", bedWorld, kit.root.eulerAngles.y);
            SetAnchor("hut_door", kit.root.TransformPoint(new Vector3(0f, 0f, 3.6f)), kit.root.eulerAngles.y + 180f);
            Campfire(inst, kit.root.TransformPoint(new Vector3(4.5f, 0f, 3.5f)));
            var rack = Kit(inst, "Rack", kit.root.TransformPoint(new Vector3(-4.2f, 0f, 3.2f)), kit.root.eulerAngles.y);
            rack.M(A.matDarkWood).Box(new Vector3(-0.8f, 0.9f, 0f), new Vector3(0.1f, 1.8f, 0.1f), 1f);
            rack.M(A.matDarkWood).Box(new Vector3(0.8f, 0.9f, 0f), new Vector3(0.1f, 1.8f, 0.1f), 1f);
            rack.M(A.matDarkWood).Box(new Vector3(0f, 1.7f, 0f), new Vector3(1.8f, 0.08f, 0.08f), 1f);
            rack.M(A.matCloth).Box(new Vector3(0f, 1.3f, 0f), new Vector3(0.5f, 0.7f, 0.04f), 1f);
            rack.Finish();
        }

        // ------------------------------------------------------------------ Thanh Hà village
        void BuildVillage(PoiInstance inst)
        {
            Vector2 C = new Vector2(inst.center.x, inst.center.z);
            Transform root = inst.root.transform;
            float cy = inst.center.y;

            // plaza paving
            var plaza = Kit(inst, "Plaza", new Vector3(C.x, cy, C.y), 0f);
            plaza.M(A.matCobble).Disc(new Vector3(0f, 0.05f, 0f), 15f, 36, true, 3f);
            plaza.M(A.matCobble).Torus(new Vector3(0f, 0.05f, 0f), 15f, 0.12f, 36, 5);
            Structures.Well(plaza, new Vector3(8f, 0.05f, -5f));
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f + 0.2f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 14.5f, 0.05f, Mathf.Sin(a) * 14.5f);
                plaza.M(A.matDarkWood).Cylinder(p, 3f, 0.09f, 0.07f, 6, true, true, 1f);
                plaza.Lantern(p + new Vector3(0f, 3.1f, 0f), 1f);
            }
            plaza.Finish();

            // testing hall (Trắc Linh Đài) on the north side, door facing the plaza
            var hall = Kit(inst, "TestingHall", new Vector3(C.x, G(C.x, C.y + 34f), C.y + 34f), 180f);
            BuildHall(hall, 15f, 10f);
            hall.Finish();
            // testing stone on the paved forecourt between plaza and hall
            Vector3 altarPos = W(C, 0f, 18f);
            testingStones.Add(TestingStone.Create(root, altarPos, A));
            SetAnchor("altar", altarPos, 180f);
            SetAnchor("altar_player", W(C, 0f, 12.5f), 0f);
            SetAnchor("altar_elder", W(C, 3.2f, 18.5f), 235f);
            SetAnchor("altar_crowd1", W(C, -7f, 13f), FaceYaw(-7f, -5f) + 180f);
            SetAnchor("altar_crowd2", W(C, 8f, 12f), 200f);
            RuneDecal.Create(root, altarPos, 5.2f, new Color(0.6f, 0.85f, 1f), 0.35f, 3f, 8);

            // houses around the plaza
            (float x, float z, float w, float d, int roof)[] houses =
            {
                (-27f, -12f, 6f, 5f, 0), (-35f, 6f, 6.5f, 5f, 1), (23f, -18f, 6f, 5f, 0), (31f, 2f, 5.5f, 5f, 1),
                (26f, 21f, 6f, 5f, 0), (-6f, -33f, 5.5f, 5f, 1), (13f, -31f, 6f, 5f, 0), (-19f, -28f, 5.5f, 5f, 1)
            };
            for (int i = 0; i < houses.Length; i++)
            {
                var h = houses[i];
                Vector3 p = new Vector3(C.x + h.x, 0f, C.y + h.z);
                p.y = MaxGround(p.x, p.z, h.w, h.d);
                var kit = Kit(inst, "House" + i, p, FaceYaw(h.x, h.z));
                Structures.House(kit, Vector3.zero, h.w, h.d, 2.8f, i % 3 == 0 ? A.matPlaster : A.matWall, h.roof == 0 ? A.matRoofDark : A.matRoofRed, true, i % 2 == 0);
                kit.Finish();
                if (i == 0) SetAnchor("healer_house", kit.root.TransformPoint(new Vector3(0f, 0f, 4.2f)), FaceYaw(h.x, h.z) + 180f);
                if (i == 4) SetAnchor("merchant_spot", kit.root.TransformPoint(new Vector3(-3.5f, 0f, 4.5f)), FaceYaw(h.x, h.z) + 180f);
            }
            // the elder's larger house with a fenced yard
            {
                Vector3 p = new Vector3(C.x - 31f, 0f, C.y + 24f);
                p.y = MaxGround(p.x, p.z, 8.5f, 6.5f);
                var kit = Kit(inst, "ElderHouse", p, FaceYaw(-31f, 24f));
                Structures.House(kit, Vector3.zero, 8.5f, 6.5f, 3.1f, A.matPlaster, A.matRoofRed, true, true);
                Structures.Gate(kit, new Vector3(0f, 0f, 7.5f), 2.6f, 2.8f, A.matRoofDark);
                kit.Fence(new Vector2(-5.5f, 7.5f), new Vector2(-1.5f, 7.5f));
                kit.Fence(new Vector2(1.5f, 7.5f), new Vector2(5.5f, 7.5f));
                kit.Fence(new Vector2(-5.5f, 7.5f), new Vector2(-5.5f, -4f));
                kit.Fence(new Vector2(5.5f, 7.5f), new Vector2(5.5f, -4f));
                kit.Finish();
                SetAnchor("elder_house", kit.root.TransformPoint(new Vector3(0f, 0f, 9f)), FaceYaw(-31f, 24f) + 180f);
            }
            // market stalls
            for (int i = 0; i < 2; i++)
            {
                var s = Kit(inst, "Stall" + i, W(C, 17f + i * 1.0f, 8f - i * 7f), -90f);
                Structures.Stall(s, Vector3.zero, i == 0 ? new Color(0.7f, 0.25f, 0.2f) : new Color(0.25f, 0.45f, 0.55f));
                s.Finish();
            }
            SetAnchor("market", W(C, 14.5f, 4.5f), 90f);
            // fields with crops and fences
            var fields = Kit(inst, "Fields", W(C, -47f, -12f), 0f);
            fields.M(Mats.Cached("soil", () => Mats.Lit(new Color(0.28f, 0.2f, 0.14f), 0.05f, 0f, A.dirt))).Box(new Vector3(0f, 0.04f, 0f), new Vector3(15f, 0.1f, 11f), 3f);
            for (int r = 0; r < 6; r++)
                fields.M(A.matBush).Limb(new Vector3(-6.5f, 0.35f, -4.5f + r * 1.8f), new Vector3(6.5f, 0.35f, -4.5f + r * 1.8f), 0.28f, 0.28f, 6, false, false);
            fields.Fence(new Vector2(-8f, -6.5f), new Vector2(8f, -6.5f));
            fields.Fence(new Vector2(-8f, 6.5f), new Vector2(8f, 6.5f));
            fields.Fence(new Vector2(-8f, -6.5f), new Vector2(-8f, 6.5f));
            fields.Finish();
            // village shrine (spirit altar: save / rest)
            Vector3 shrinePos = W(C, -9f, 3f);
            shrines.Add(Shrine.Create(root, "thanh_ha", shrinePos, A));
            SetAnchor("shrine_thanh_ha", shrinePos + new Vector3(1.5f, 0f, -1.2f), 0f);
            // a grove of blossoms behind the hall
            Scatter(root, C + new Vector2(0f, 52f), 10f, 7, PropLibrary.Blossom, 31, 4f);
            Scatter(root, C + new Vector2(-48f, 18f), 12f, 7, PropLibrary.Blossom, 32, 3f);

            // anchors and NPCs
            SetAnchor("village_plaza", new Vector3(C.x, cy, C.y), 0f);
            SetAnchor("village_gate", W(C, -52f, -6f), 90f);
            SetAnchor("village_entry", W(C, -48f, 14f), 90f);
            npcs.Add(new NpcPlacement { npcId = "elder_vu", anchor = "altar_elder" });
            npcs.Add(new NpcPlacement { npcId = "healer_lan", anchor = "healer_house" });
            npcs.Add(new NpcPlacement { npcId = "merchant_trinh", anchor = "merchant_spot" });
            npcs.Add(new NpcPlacement { npcId = "villager_tam", pos = W(C, -42f, -8f), yaw = 90f });
            npcs.Add(new NpcPlacement { npcId = "child_bao", pos = W(C, 5f, 4f), yaw = 20f });
            npcs.Add(new NpcPlacement { npcId = "villager_hoa", pos = W(C, -12f, -6f), yaw = 90f });
        }

        float MaxGround(float x, float z, float w, float d)
        {
            float m = float.MinValue;
            for (int i = -1; i <= 1; i += 2)
                for (int j = -1; j <= 1; j += 2) m = Mathf.Max(m, G(x + i * w * 0.5f, z + j * d * 0.5f));
            return Mathf.Max(m, G(x, z));
        }

        void BuildHall(StructureKit k, float w, float d)
        {
            float hx = w * 0.5f, hz = d * 0.5f;
            float t = 0.35f, h = 4.4f;
            k.Block(k.A.matCobble, new Vector3(0f, -0.8f, 0f), new Vector3(w + 3f, 1.8f, d + 3f), true, 3f);
            k.M(A.matPlank).Box(new Vector3(0f, 1.02f, 0f), new Vector3(w, 0.06f, d), 2f);
            float y0 = 1.0f;
            // back and side walls (low screens), open front with columns
            k.WallSeg(A.matPlaster, new Vector2(hx, -hz), new Vector2(-hx, -hz), y0, y0 + h, t);
            k.WallSeg(A.matPlaster, new Vector2(-hx, -hz), new Vector2(-hx, hz * 0.2f), y0, y0 + h, t);
            k.WallSeg(A.matPlaster, new Vector2(hx, -hz), new Vector2(hx, hz * 0.2f), y0, y0 + h, t);
            for (int i = 0; i < 6; i++)
            {
                float x = Mathf.Lerp(-hx + 0.4f, hx - 0.4f, i / 5f);
                k.Pillar(A.matDarkWood, new Vector3(x, y0, hz - 0.5f), h, 0.34f, true);
            }
            k.Pillar(A.matDarkWood, new Vector3(-hx + 0.4f, y0, 0f), h, 0.3f, true);
            k.Pillar(A.matDarkWood, new Vector3(hx - 0.4f, y0, 0f), h, 0.3f, true);
            k.M(A.matDarkWood).Box(new Vector3(0f, y0 + h, hz - 0.5f), new Vector3(w, 0.4f, 0.5f), 1f);
            k.Roof(A.matRoofRed, new Vector3(0f, y0 + h + 0.2f, 0f), w + 4.2f, d + 4.2f, 2.6f, 1.0f);
            k.Steps(A.matMarble, new Vector3(0f, 0f, hz + 3.2f), 6f, 2.2f, 4, 0.25f);
            // altar table and incense inside
            k.Block(A.matDarkWood, new Vector3(0f, y0, -hz + 1.5f), new Vector3(2.6f, 0.9f, 0.9f), true, 1f);
            k.M(A.matGlowGold).Sphere(new Vector3(0f, y0 + 1.1f, -hz + 1.5f), 0.14f, 8, 6);
            k.Banner(new Vector3(-hx + 1.6f, y0, hz - 1f), 3.6f, new Color(0.15f, 0.35f, 0.4f));
            k.Banner(new Vector3(hx - 1.6f, y0, hz - 1f), 3.6f, new Color(0.15f, 0.35f, 0.4f));
            k.AddLight(new Vector3(0f, y0 + 3f, 0f), new Color(1f, 0.75f, 0.45f), 12f, 3f);
        }

        // ------------------------------------------------------------------ training ground
        void BuildTraining(PoiInstance inst)
        {
            Vector2 c = new Vector2(inst.center.x, inst.center.z);
            var kit = Kit(inst, "Yard", inst.center, 0f);
            kit.M(A.matCobble).Disc(new Vector3(0f, 0.04f, 0f), 11f, 28, true, 3f);
            kit.Fence(new Vector2(-12f, -12f), new Vector2(12f, -12f));
            kit.Fence(new Vector2(-12f, 12f), new Vector2(-2.5f, 12f));
            kit.Fence(new Vector2(2.5f, 12f), new Vector2(12f, 12f));
            kit.Fence(new Vector2(-12f, -12f), new Vector2(-12f, 12f));
            kit.Fence(new Vector2(12f, -12f), new Vector2(12f, 12f));
            // weapon rack
            kit.Block(A.matDarkWood, new Vector3(-9f, 0f, -9f), new Vector3(2.4f, 1.2f, 0.4f), true, 1f);
            kit.M(A.matMetal).Limb(new Vector3(-9.6f, 1.25f, -9f), new Vector3(-9.2f, 2.0f, -9f), 0.03f, 0.02f, 4, false, false);
            kit.M(A.matMetal).Limb(new Vector3(-9.0f, 1.25f, -9f), new Vector3(-8.6f, 2.1f, -9f), 0.03f, 0.02f, 4, false, false);
            kit.M(A.matDarkWood).Limb(new Vector3(-8.4f, 1.25f, -9f), new Vector3(-8.0f, 2.3f, -9f), 0.04f, 0.035f, 4, false, false);
            kit.Banner(new Vector3(10f, 0f, 10f), 4.5f, new Color(0.7f, 0.25f, 0.2f), 1.1f);
            kit.Finish();
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = W(c, -5f + i * 5f, 3f);
                TrainingDummy.Create(inst.root.transform, p, 180f, A, false);
            }
            TrainingDummy.Create(inst.root.transform, W(c, 0f, -4f), 0f, A, true);
            SetAnchor("training_hunter", W(c, 0f, 8f), 180f);
            SetAnchor("dummy_area", W(c, 0f, 0f), 0f);
            npcs.Add(new NpcPlacement { npcId = "hunter_luc", anchor = "training_hunter" });
        }

        // ------------------------------------------------------------------ vein terrace (meditation)
        void BuildTerrace(PoiInstance inst)
        {
            Vector3 c = inst.center;
            var kit = Kit(inst, "Terrace", c, 0f);
            kit.M(A.matMarble).Cylinder(new Vector3(0f, -1.2f, 0f), 1.4f, 11.5f, 10.5f, 24, true, true, 3f);
            kit.M(A.matCobble).Disc(new Vector3(0f, 0.22f, 0f), 10.5f, 24, true, 3f);
            kit.Box(new Vector3(0f, -0.5f, 0f), new Vector3(20f, 1.4f, 20f));
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                Monolith(kit, new Vector3(Mathf.Cos(a) * 9.4f, 0.2f, Mathf.Sin(a) * 9.4f), 3.4f + (i % 2) * 0.6f, true);
            }
            kit.M(A.matGlowCyan).Torus(new Vector3(0f, 0.25f, 0f), 4.2f, 0.07f, 40, 6);
            kit.M(A.matGlowCyan).Torus(new Vector3(0f, 0.25f, 0f), 7.2f, 0.06f, 48, 6);
            kit.AddLight(new Vector3(0f, 2.5f, 0f), new Color(0.5f, 0.85f, 1f), 18f, 4.5f);
            kit.Finish();
            RuneDecal.Create(inst.root.transform, c + Vector3.up * 0.2f, 7.4f, new Color(0.5f, 0.85f, 1f), 0.55f, 5f, 6);
            Vfx.Play("qi_gather", c + Vector3.up * 0.5f, Quaternion.identity, 1.5f);
            var spot = new GameObject("MeditationSpot");
            spot.layer = GameLayers.Interactable;
            spot.transform.SetParent(inst.root.transform, false);
            spot.transform.position = c + Vector3.up * 0.25f;
            var sc = spot.AddComponent<SphereCollider>();
            sc.radius = 7f; sc.isTrigger = true;
            var ms = spot.AddComponent<MeditationSpot>();
            ms.radius = 7f;
            meditationSpots.Add(ms);
            SetAnchor("vein_center", c + Vector3.up * 0.25f, 0f);
            SetAnchor("vein_edge", c + new Vector3(-9.5f, 0.25f, 0f), 90f);
            Scatter(inst.root.transform, new Vector2(c.x, c.z), 40f, 10, PropLibrary.Pine, 41, 15f);
        }

        // ------------------------------------------------------------------ bandit camp
        void BuildBanditCamp(PoiInstance inst)
        {
            Vector2 c = new Vector2(inst.center.x, inst.center.z);
            Func<float, float, float> ground = (x, z) => G(x, z) - inst.center.y;
            var kit = Kit(inst, "Camp", inst.center, 0f);
            Structures.Palisade(kit, Vector2.zero, 27f, 200f, 36f, ground);
            Structures.Tower(kit, new Vector3(-20f, ground(c.x - 20f, c.y + 10f), 10f), 6.5f);
            Structures.Tower(kit, new Vector3(18f, ground(c.x + 18f, c.y + 14f), 14f), 6.5f);
            Color[] cols = { new Color(0.55f, 0.2f, 0.18f), new Color(0.3f, 0.3f, 0.28f), new Color(0.5f, 0.35f, 0.2f), new Color(0.4f, 0.18f, 0.16f) };
            for (int i = 0; i < 4; i++)
            {
                float a = 40f + i * 75f;
                Vector3 p = new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * 14f, 0f, Mathf.Cos(a * Mathf.Deg2Rad) * 14f);
                p.y = ground(c.x + p.x, c.y + p.z);
                kit.M(A.matCloth);
                Structures.Tent(kit, p, 4.2f, 5f, cols[i]);
            }
            kit.Banner(new Vector3(-6f, 0f, -22f), 5.5f, new Color(0.6f, 0.1f, 0.1f), 1.3f);
            kit.Banner(new Vector3(6f, 0f, -22f), 5.5f, new Color(0.6f, 0.1f, 0.1f), 1.3f);
            // cage with a captive (side quest)
            kit.M(A.matDarkWood);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                kit.M(A.matDarkWood).Cylinder(new Vector3(-14f + Mathf.Cos(a) * 1.6f, 0f, -12f + Mathf.Sin(a) * 1.6f), 2.6f, 0.07f, 0.07f, 6, true, true, 1f);
            }
            kit.M(A.matDarkWood).Cylinder(new Vector3(-14f, 2.6f, -12f), 0.15f, 1.7f, 1.7f, 12, true, true, 1f);
            kit.Capsule(new Vector3(-14f, 0f, -12f), 2.6f, 1.7f);
            kit.Finish();
            Campfire(inst, inst.center + new Vector3(0f, 0f, 2f));
            Container.Create(inst.root.transform, "camp_chest_1", new Vector3(c.x + 9f, G(c.x + 9f, c.y - 13f), c.y - 13f), 20f, "chest_rare", A);
            Container.Create(inst.root.transform, "camp_chest_2", new Vector3(c.x - 10f, G(c.x - 10f, c.y + 12f), c.y + 12f), 160f, "chest_common", A);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.26f + 0.4f;
                spawns.Add(new FixedSpawn { id = "camp_bandit_" + i, enemy = "bandit", pos = new Vector3(c.x + Mathf.Cos(a) * 8f, G(c.x, c.y), c.y + Mathf.Sin(a) * 8f) });
            }
            spawns.Add(new FixedSpawn { id = "camp_archer_0", enemy = "bandit_archer", pos = W(c, -20f, 10f, 6.5f) });
            spawns.Add(new FixedSpawn { id = "camp_archer_1", enemy = "bandit_archer", pos = W(c, 18f, 14f, 6.5f) });
            SetAnchor("camp_center", inst.center, 0f);
            SetAnchor("camp_cage", W(c, -14f, -12f), 0f);
        }

        // ------------------------------------------------------------------ alpha lair
        void BuildAlphaLair(PoiInstance inst)
        {
            Vector2 c = new Vector2(inst.center.x, inst.center.z);
            RockRing(inst, c, 14f, 12, 51, 2.2f);
            Bones(inst, c, 9f, 20, 7);
            Scatter(inst.root.transform, c, 22f, 6, PropLibrary.Dead, 8, 10f);
            spawns.Add(new FixedSpawn { id = "boss_alpha_wolf", enemy = "alpha_wolf", pos = inst.center + new Vector3(0f, 0.1f, 2f) });
            Container.Create(inst.root.transform, "alpha_chest", new Vector3(c.x + 6f, G(c.x + 6f, c.y - 6f), c.y - 6f), 200f, "chest_rare", A);
            SetAnchor("alpha_arena", inst.center, 0f);
        }

        // ------------------------------------------------------------------ later-phase landmarks (visual shells + anchors)
        void BuildLodge(PoiInstance inst)
        {
            Vector3 c = inst.center;
            var kit = Kit(inst, "Lodge", c, 0f);
            Structures.Gate(kit, new Vector3(0f, 0f, -26f), 6f, 5.2f, A.matRoofDark);
            kit.M(A.matCobble).Box(new Vector3(0f, 0.02f, -2f), new Vector3(9f, 0.1f, 52f), 3f);
            Structures.House(kit, new Vector3(-17f, 0f, 6f), 12f, 8f, 3.6f, A.matPlaster, A.matRoofDark, false, false);
            Structures.House(kit, new Vector3(17f, 0f, 6f), 12f, 8f, 3.6f, A.matPlaster, A.matRoofDark, false, false);
            Structures.Pagoda(kit, new Vector3(0f, 0f, 24f), 7f, 3, A.matPlaster, A.matRoofDark);
            kit.Banner(new Vector3(-6f, 0f, -20f), 6f, new Color(0.25f, 0.45f, 0.7f), 1.2f);
            kit.Banner(new Vector3(6f, 0f, -20f), 6f, new Color(0.25f, 0.45f, 0.7f), 1.2f);
            kit.Finish();
            SetAnchor("lodge_gate", c + new Vector3(0f, 0f, -24f), 0f);
            SetAnchor("lodge_court", c, 0f);
        }

        void BuildScholarCamp(PoiInstance inst)
        {
            Vector3 c = inst.center;
            var kit = Kit(inst, "Scholars", c, 0f);
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f;
                Structures.Tent(kit, new Vector3(Mathf.Cos(a) * 14f, 0f, Mathf.Sin(a) * 14f), 4.5f, 5.2f, i % 2 == 0 ? new Color(0.78f, 0.74f, 0.6f) : new Color(0.35f, 0.5f, 0.55f));
            }
            kit.Block(A.matDarkWood, new Vector3(0f, 0f, 0f), new Vector3(3.4f, 0.95f, 1.5f), true, 1f);
            kit.M(A.matPlaster).Box(new Vector3(0f, 1.0f, 0f), new Vector3(1.2f, 0.04f, 0.9f), 1f);
            kit.M(A.matMetal).Torus(new Vector3(5f, 2.2f, 0f), 1.4f, 0.05f, 24, 5);
            kit.Finish();
            Campfire(inst, c + new Vector3(0f, 0f, -6f));
            SetAnchor("scholar_table", c, 0f);
        }

        void BuildRuin(PoiInstance inst)
        {
            Vector3 c = inst.center;
            var kit = Kit(inst, "Ruin", c, 0f);
            var rng = new Rng(77);
            // broken colonnade leading to the sealed hall
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 6; i++)
                {
                    float h = rng.Value() > 0.45f ? 6.5f : 2f + rng.Value() * 3f;
                    kit.Pillar(A.matWall, new Vector3(side * 6f, 0f, -34f + i * 8f), h, 0.7f, h > 6f);
                }
            // fallen drums
            for (int i = 0; i < 5; i++)
                kit.M(A.matWall).Cylinder(new Vector3(rng.Signed() * 14f, 0.05f, -30f + rng.Value() * 40f), 1.2f, 0.7f, 0.7f, 8, true, true, 2f);
            // facade with the sealed stone door
            kit.Block(A.matWall, new Vector3(-12f, 0f, 24f), new Vector3(12f, 14f, 3f), true, 3f);
            kit.Block(A.matWall, new Vector3(12f, 0f, 24f), new Vector3(12f, 14f, 3f), true, 3f);
            kit.Block(A.matWall, new Vector3(0f, 9.8f, 24f), new Vector3(13f, 4.2f, 3f), false, 3f);
            kit.Block(A.matWall, new Vector3(-18f, 0f, 14f), new Vector3(3f, 12f, 22f), true, 3f);
            kit.Block(A.matWall, new Vector3(18f, 0f, 14f), new Vector3(3f, 12f, 22f), true, 3f);
            kit.Roof(A.matRoofDark, new Vector3(0f, 14f, 22f), 26f, 12f, 3.2f, 1.2f);
            kit.Monolith_(A, new Vector3(-8f, 0f, 17f), 5f);
            kit.Monolith_(A, new Vector3(8f, 0f, 17f), 5f);
            var door = StoneDoor.Create(kit, new Vector3(0f, 0f, 22.9f), 9f, 9.2f, A.matWall, A.matGlowCyan);
            door.openFlag = "ruin_door_open";
            stoneDoors.Add(door);
            kit.Finish();
            RuneDecal.Create(inst.root.transform, c + new Vector3(0f, 0.3f, 8f), 9.5f, new Color(0.5f, 0.85f, 1f), 0.28f, 2f, 8);
            Container.Create(inst.root.transform, "ruin_chest", c + new Vector3(10f, 0f, -12f), 120f, "chest_rare", A);
            SetAnchor("ruin_door", c + new Vector3(0f, 0f, 19f), 180f);
            SetAnchor("ruin_front", c + new Vector3(0f, 0f, 6f), 0f);
            spawns.Add(new FixedSpawn { id = "ruin_guardian_0", enemy = "stone_guardian", pos = c + new Vector3(-9f, 0f, -4f) });
        }

        void BuildCaveMouth(PoiInstance inst)
        {
            Vector2 c = new Vector2(inst.center.x, inst.center.z);
            Transform t = inst.root.transform;
            // rock arch around a dark recess
            var kit = Kit(inst, "CaveMouth", inst.center, 180f);
            kit.M(A.matRock);
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.Lerp(-100f, 100f, i / 8f) * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Sin(a) * 6.5f, Mathf.Cos(a) * 5.2f - 0.4f, 4f);
                int st = kit.M(A.matRock).VertexCount;
                kit.M(A.matRock).Ellipsoid(p, new Vector3(2.4f, 2.3f, 2.8f), 8, 5, 2.5f);
                kit.M(A.matRock).Displace(st, 0.5f, 0.7f, 90 + i);
            }
            kit.M(Mats.Cached("cave_dark", () => Mats.Lit(new Color(0.02f, 0.02f, 0.03f), 0f))).Box(new Vector3(0f, 2.4f, 5.6f), new Vector3(8.4f, 5.2f, 0.2f), 1f);
            kit.Box(new Vector3(0f, 2.5f, 5.9f), new Vector3(9f, 5f, 0.6f));
            kit.M(A.matGlowCyan).Box(new Vector3(0f, 5.4f, 3.9f), new Vector3(0.12f, 0.9f, 0.05f), 1f);
            kit.Finish();
            Container.Create(t, "cave_chest", W(c, 7f, -5f), 200f, "chest_rare", A);
            spawns.Add(new FixedSpawn { id = "cave_guardian_0", enemy = "stone_guardian", pos = W(c, -6f, -7f) });
            SetAnchor("cave_mouth", W(c, 0f, -2f), 0f);
            RockRing(inst, c, 13f, 8, 61, 1.5f);
        }

        void BuildMemoryRoad(PoiInstance inst)
        {
            Vector3 c = inst.center;
            var kit = Kit(inst, "MemoryRoad", c, 20f);
            var rng = new Rng(88);
            for (int i = 0; i < 14; i++)
            {
                float z = -26f + i * 3.8f;
                kit.M(A.matCobble).Box(new Vector3(rng.Signed() * 0.25f, 0.08f, z), new Vector3(4.2f + rng.Value(), 0.16f, 3.4f), 2f);
            }
            for (int i = 0; i < 3; i++)
            {
                kit.Pillar(A.matWall, new Vector3(-3.4f, 0f, -14f + i * 12f), 3.5f + rng.Value() * 2f, 0.5f, false);
                kit.Pillar(A.matWall, new Vector3(3.4f, 0f, -14f + i * 12f), 2f + rng.Value() * 3f, 0.5f, false);
            }
            kit.M(A.matGlowCyan).Cylinder(new Vector3(0f, 0.1f, 14f), 1.8f, 0.45f, 0.18f, 5, true, true, 1f);
            kit.Capsule(new Vector3(0f, 0f, 14f), 1.8f, 0.5f);
            kit.Finish();
            Vfx.Play("mist", c + Vector3.up * 1f, Quaternion.identity, 1f);
            var crystal = new GameObject("MemoryStone");
            crystal.layer = GameLayers.Interactable;
            crystal.transform.SetParent(inst.root.transform, false);
            crystal.transform.position = kit.root.TransformPoint(new Vector3(0f, 1f, 14f));
            var sc = crystal.AddComponent<SphereCollider>();
            sc.radius = 1.2f; sc.isTrigger = true;
            var si = crystal.AddComponent<ScriptedInteract>();
            si.objectId = "memory_stone";
            si.promptKey = "prompt.touch";
            si.effects = "flag+:memory_road_found;toast:toast.memory_stir";
            si.once = false;
            SetAnchor("memory_stone", crystal.transform.position, 0f);
        }

        void BuildSpring(PoiInstance inst)
        {
            Vector3 c = inst.center;
            var kit = Kit(inst, "Spring", c, 0f);
            kit.M(A.matCobble).Torus(new Vector3(0f, 0.15f, 0f), 4.5f, 0.55f, 18, 6);
            kit.M(A.matLake).Disc(new Vector3(0f, 0.28f, 0f), 4.5f, 18, true, 1f);
            kit.M(A.matGlowCyan).Disc(new Vector3(0f, 0.2f, 0f), 2.4f, 16, true, 1f);
            kit.AddLight(new Vector3(0f, 1f, 0f), new Color(0.5f, 0.9f, 1f), 12f, 3f);
            kit.Finish(false);
            Vfx.Play("qi_gather", c + Vector3.up * 0.4f, Quaternion.identity, 1f);
            var spot = new GameObject("MeditationSpot");
            spot.layer = GameLayers.Interactable;
            spot.transform.SetParent(inst.root.transform, false);
            spot.transform.position = c + Vector3.up * 0.2f;
            var sc = spot.AddComponent<SphereCollider>();
            sc.radius = 5f; sc.isTrigger = true;
            var ms = spot.AddComponent<MeditationSpot>();
            ms.radius = 5f;
            meditationSpots.Add(ms);
            Scatter(inst.root.transform, new Vector2(c.x, c.z), 22f, 9, PropLibrary.Blossom, 99, 7f);
        }

        void BuildSwordTomb(PoiInstance inst)
        {
            Vector3 c = inst.center;
            var kit = Kit(inst, "SwordTomb", c, 0f);
            var rng = new Rng(55);
            for (int i = 0; i < 46; i++)
            {
                float a = rng.Value() * Mathf.PI * 2f, d = 3f + rng.Value() * 18f;
                float x = Mathf.Cos(a) * d, z = Mathf.Sin(a) * d;
                float gy = G(c.x + x, c.z + z) - c.y;
                Vector3 b = new Vector3(x, gy - 0.4f, z);
                Vector3 tip = b + new Vector3(rng.Signed() * 0.25f, 2f + rng.Value() * 1.6f, rng.Signed() * 0.25f);
                kit.M(A.matMetal).Limb(b, tip, 0.07f, 0.02f, 4, false, true);
                kit.M(A.matDarkWood).Box(b + new Vector3(0f, 0.9f, 0f), new Vector3(0.5f, 0.07f, 0.09f), 1f);
            }
            kit.Block(A.matWall, new Vector3(0f, 0f, 0f), new Vector3(2f, 4.6f, 0.7f), true, 2f);
            kit.M(A.matGlowCyan).Box(new Vector3(0f, 3f, 0.38f), new Vector3(0.08f, 1.8f, 0.03f), 1f);
            kit.Finish();
            SetAnchor("sword_stele", c + new Vector3(0f, 0f, 1.5f), 180f);
        }

        void BuildHeart(PoiInstance inst)
        {
            Vector3 c = inst.center;
            var kit = Kit(inst, "Heart", c, 0f);
            kit.M(A.matWall).Cylinder(new Vector3(0f, -0.4f, 0f), 0.6f, 24f, 22f, 36, true, true, 4f);
            kit.M(A.matGlowCyan).Torus(new Vector3(0f, 0.25f, 0f), 18f, 0.15f, 64, 6);
            kit.M(A.matGlowCyan).Torus(new Vector3(0f, 0.25f, 0f), 9f, 0.1f, 48, 6);
            kit.Box(new Vector3(0f, 0f, 0f), new Vector3(48f, 0.8f, 48f));
            kit.Finish(false);
            RuneDecal.Create(inst.root.transform, c + Vector3.up * 0.7f, 19f, new Color(0.6f, 0.8f, 1f), 0.35f, 2f, 7);
            FloatingShards.Create(inst.root.transform, c + Vector3.up * 8f, 26, 30f, 4f, 40f, A.matGlowCyan, 5);
            SetAnchor("heart_center", c + Vector3.up * 0.8f, 0f);
        }

        void BuildRiftValley(PoiInstance inst)
        {
            Vector3 c = inst.center;
            FloatingShards.Create(inst.root.transform, c + Vector3.up * 6f, 16, 38f, 3f, 28f, A.matGlowCyan, 15);
            var kit = Kit(inst, "Rift", c, 0f);
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f;
                Monolith(kit, new Vector3(Mathf.Cos(a) * 26f, G(c.x + Mathf.Cos(a) * 26f, c.z + Mathf.Sin(a) * 26f) - c.y, Mathf.Sin(a) * 26f), 5f, true);
            }
            kit.Finish();
            SetAnchor("rift_center", c, 0f);
        }

        // ------------------------------------------------------------------ bridges where roads cross the river
        void BuildBridges(Transform root)
        {
            var placed = new List<Vector2>();
            foreach (Vector2[] path in gen.RoadPaths)
            {
                for (int i = 0; i < path.Length - 1; i++)
                {
                    Vector2 a = path[i], b = path[i + 1];
                    gen.RiverQuery(a.x, a.y, out float surf, out float da, out _);
                    gen.RiverQuery(b.x, b.y, out _, out float db, out _);
                    if (da > WorldLayout.RiverHalfWidth + 1f && db > WorldLayout.RiverHalfWidth + 1f) continue;
                    if (Mathf.Min(da, db) > WorldLayout.RiverHalfWidth) continue;
                    Vector2 m = (a + b) * 0.5f;
                    bool dup = false;
                    for (int k = 0; k < placed.Count; k++) if (Vector2.Distance(placed[k], m) < 28f) dup = true;
                    if (dup) continue;
                    placed.Add(m);
                    Vector2 dir = (b - a).normalized;
                    float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                    var holder = new GameObject("BridgeHolder");
                    holder.transform.SetParent(root, false);
                    var kit = new StructureKit(holder.transform, "Bridge", new Vector3(m.x, 0f, m.y), yaw, A);
                    Structures.Bridge(kit, new Vector3(0f, surf + 1.1f, 0f), WorldLayout.RiverHalfWidth * 2f + 9f, 3.6f);
                    kit.Finish();
                }
            }
        }
    }

    /// <summary>Small extension used by the ruin builder.</summary>
    static class KitExt
    {
        public static void Monolith_(this StructureKit k, WorldAssets A, Vector3 pos, float h)
        {
            k.M(A.matWall).Cylinder(pos, h, 0.7f, 0.45f, 5, true, true, 2f);
            k.M(A.matGlowCyan).Box(pos + new Vector3(0f, h * 0.55f, 0.46f), new Vector3(0.08f, h * 0.35f, 0.03f), 1f);
            k.Capsule(pos, h, 0.6f);
        }
    }
}
