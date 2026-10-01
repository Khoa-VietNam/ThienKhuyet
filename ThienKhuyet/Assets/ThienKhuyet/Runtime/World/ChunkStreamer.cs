using System.Collections;
using System.Collections.Generic;
using ThienKhuyet.Core;
using ThienKhuyet.Data;
using ThienKhuyet.Enemies;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThienKhuyet.World
{
    /// <summary>
    /// Streams the open world in 64 m chunks around the player: instanced vegetation and rocks (LOD by distance, frustum culled),
    /// a small pool of colliders that follows the player, resource nodes, chests and enemy groups. Chunks outside the radius are released.
    /// </summary>
    public sealed class ChunkStreamer : MonoBehaviour
    {
        sealed class Chunk
        {
            public ChunkPlan plan;
            public GameObject holder;
            public readonly List<EnemyBrain> enemies = new List<EnemyBrain>();
            public Bounds bounds;
        }

        sealed class Batch
        {
            public Species species;
            public int variant, lod;
            public Matrix4x4[] arr = new Matrix4x4[128];
            public int count;

            public void Add(in Matrix4x4 m)
            {
                if (count == arr.Length) System.Array.Resize(ref arr, arr.Length * 2);
                arr[count++] = m;
            }
        }

        WorldGen gen;
        HeightField hf;
        PropLibrary props;
        WorldAssets assets;
        Transform root;
        GameSettings settings;

        readonly Dictionary<int, Chunk> chunks = new Dictionary<int, Chunk>();
        readonly Dictionary<int, List<FixedSpawn>> fixedByChunk = new Dictionary<int, List<FixedSpawn>>();
        readonly Dictionary<int, Batch> batches = new Dictionary<int, Batch>();
        readonly List<EnemyBrain> orphans = new List<EnemyBrain>();
        readonly Plane[] frustum = new Plane[6];
        CapsuleCollider[] treeCols;
        SphereCollider[] rockCols;
        Transform focus;
        float updateTimer, batchTimer, colliderTimer, orphanTimer;
        int loadedThisFrame;
        Vector3 lastBatchPos;
        bool forceRebuild = true;

        public int ActiveChunks => chunks.Count;
        public int InstanceCount { get; private set; }

        public static ChunkStreamer Create(Transform parent, WorldGen gen, HeightField hf, PropLibrary props, WorldAssets assets, GameSettings settings, List<FixedSpawn> fixedSpawns)
        {
            var go = new GameObject("ChunkStreamer");
            go.transform.SetParent(parent, false);
            var cs = go.AddComponent<ChunkStreamer>();
            cs.gen = gen;
            cs.hf = hf;
            cs.props = props;
            cs.assets = assets;
            cs.root = go.transform;
            cs.settings = settings;
            foreach (FixedSpawn f in fixedSpawns)
            {
                int key = Key((int)(f.pos.x / WorldLayout.ChunkSize), (int)(f.pos.z / WorldLayout.ChunkSize));
                if (!cs.fixedByChunk.TryGetValue(key, out var list)) cs.fixedByChunk[key] = list = new List<FixedSpawn>();
                list.Add(f);
            }
            cs.BuildColliderPool();
            return cs;
        }

        static int Key(int cx, int cz) { return cz * 64 + cx; }

        void BuildColliderPool()
        {
            var holder = new GameObject("VegetationColliders");
            holder.transform.SetParent(transform, false);
            holder.layer = GameLayers.Environment;
            treeCols = new CapsuleCollider[72];
            for (int i = 0; i < treeCols.Length; i++)
            {
                var go = new GameObject("t" + i);
                go.layer = GameLayers.Environment;
                go.transform.SetParent(holder.transform, false);
                treeCols[i] = go.AddComponent<CapsuleCollider>();
                go.SetActive(false);
            }
            rockCols = new SphereCollider[40];
            for (int i = 0; i < rockCols.Length; i++)
            {
                var go = new GameObject("r" + i);
                go.layer = GameLayers.Environment;
                go.transform.SetParent(holder.transform, false);
                rockCols[i] = go.AddComponent<SphereCollider>();
                go.SetActive(false);
            }
        }

        public void SetFocus(Transform t)
        {
            focus = t;
        }

        Vector3 FocusPos
        {
            get
            {
                if (focus != null) return focus.position;
                if (Camera.main != null) return Camera.main.transform.position;
                return Vector3.zero;
            }
        }

        /// <summary>Loads every chunk around a position immediately (spread over frames); used by the loading screen.</summary>
        public IEnumerator Preload(Vector3 pos, int radius, System.Action<float> progress)
        {
            int cx = Mathf.FloorToInt(pos.x / WorldLayout.ChunkSize), cz = Mathf.FloorToInt(pos.z / WorldLayout.ChunkSize);
            int total = (radius * 2 + 1) * (radius * 2 + 1), done = 0;
            for (int dz = -radius; dz <= radius; dz++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    LoadChunk(cx + dx, cz + dz);
                    done++;
                    progress?.Invoke((float)done / total);
                    if (done % 2 == 0) yield return null;
                }
            forceRebuild = true;
        }

        void Update()
        {
            if (gen == null) return;
            float dt = Time.unscaledDeltaTime;
            updateTimer -= dt;
            batchTimer -= dt;
            colliderTimer -= dt;
            orphanTimer -= dt;
            loadedThisFrame = 0;

            if (updateTimer <= 0f)
            {
                updateTimer = 0.2f;
                UpdateChunks();
            }
            Vector3 cam = Camera.main != null ? Camera.main.transform.position : FocusPos;
            if (forceRebuild || batchTimer <= 0f || (cam - lastBatchPos).sqrMagnitude > 36f)
            {
                batchTimer = 0.2f;
                lastBatchPos = cam;
                forceRebuild = false;
                RebuildBatches(cam);
            }
            if (colliderTimer <= 0f)
            {
                colliderTimer = 0.25f;
                UpdateColliders(FocusPos);
            }
            if (orphanTimer <= 0f)
            {
                orphanTimer = 2f;
                CullOrphans();
            }
            RenderBatches(cam);
        }

        // ------------------------------------------------------------------ chunk lifetime
        void UpdateChunks()
        {
            Vector3 p = FocusPos;
            int cx = Mathf.FloorToInt(p.x / WorldLayout.ChunkSize), cz = Mathf.FloorToInt(p.z / WorldLayout.ChunkSize);
            int r = settings != null ? settings.ChunkRadius : 3;
            // load nearest missing chunks first, a couple per update
            int loaded = 0;
            for (int ring = 0; ring <= r && loaded < 2; ring++)
                for (int dz = -ring; dz <= ring && loaded < 2; dz++)
                    for (int dx = -ring; dx <= ring && loaded < 2; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != ring) continue;
                        int x = cx + dx, z = cz + dz;
                        if (x < 0 || z < 0 || x >= WorldLayout.ChunkCount || z >= WorldLayout.ChunkCount) continue;
                        if (chunks.ContainsKey(Key(x, z))) continue;
                        LoadChunk(x, z);
                        loaded++;
                        forceRebuild = true;
                    }
            // unload chunks beyond radius + 1
            var remove = new List<int>();
            foreach (var kv in chunks)
            {
                Chunk c = kv.Value;
                if (Mathf.Max(Mathf.Abs(c.plan.cx - cx), Mathf.Abs(c.plan.cz - cz)) > r + 1) remove.Add(kv.Key);
            }
            for (int i = 0; i < remove.Count; i++) UnloadChunk(remove[i]);
        }

        void LoadChunk(int cx, int cz)
        {
            if (cx < 0 || cz < 0 || cx >= WorldLayout.ChunkCount || cz >= WorldLayout.ChunkCount) return;
            int key = Key(cx, cz);
            if (chunks.ContainsKey(key)) return;
            float density = settings != null ? settings.VegetationDensity : 1f;
            ChunkPlan plan = ChunkPlanner.Plan(cx, cz, gen, hf, props, density);
            var c = new Chunk { plan = plan, holder = new GameObject("Chunk_" + cx + "_" + cz) };
            c.holder.transform.SetParent(root, false);
            c.bounds = new Bounds(new Vector3(plan.center.x, plan.center.y + 30f, plan.center.z), new Vector3(WorldLayout.ChunkSize + 16f, 140f, WorldLayout.ChunkSize + 16f));
            chunks[key] = c;
            GameSession s = Game.Session;
            for (int i = 0; i < plan.nodes.Count; i++)
            {
                NodePlan n = plan.nodes[i];
                Gatherable.Create(c.holder.transform, n.id, n.ore, n.pos, assets);
            }
            for (int i = 0; i < plan.chests.Count; i++)
            {
                ChestPlan ch = plan.chests[i];
                Container.Create(c.holder.transform, ch.id, ch.pos, ch.yaw, "chest_common", assets);
            }
            SpawnEnemies(c, plan.spawns, s);
            if (fixedByChunk.TryGetValue(key, out List<FixedSpawn> fixedList))
            {
                var list = new List<SpawnPlan>();
                foreach (FixedSpawn f in fixedList) list.Add(new SpawnPlan { id = f.id, enemy = f.enemy, pos = f.pos });
                SpawnEnemies(c, list, s);
            }
            InstanceCount += plan.InstanceCount;
        }

        void SpawnEnemies(Chunk c, List<SpawnPlan> plans, GameSession s)
        {
            for (int i = 0; i < plans.Count; i++)
            {
                SpawnPlan sp = plans[i];
                if (s != null && s.killedSpawns.Contains(sp.id)) continue;
                EnemyDef def = ContentDB.Enemy(sp.enemy);
                if (def == null) continue;
                EnemyBrain b = EnemyFactory.Create(def, sp.pos, sp.id);
                if (b != null) c.enemies.Add(b);
            }
        }

        void UnloadChunk(int key)
        {
            if (!chunks.TryGetValue(key, out Chunk c)) return;
            for (int i = 0; i < c.enemies.Count; i++)
            {
                EnemyBrain b = c.enemies[i];
                if (b == null) continue;
                if (b.IsAggro && b.State != EState.Dead) orphans.Add(b);
                else Destroy(b.gameObject);
            }
            InstanceCount -= c.plan.InstanceCount;
            Destroy(c.holder);
            chunks.Remove(key);
            forceRebuild = true;
        }

        void CullOrphans()
        {
            Vector3 p = FocusPos;
            for (int i = orphans.Count - 1; i >= 0; i--)
            {
                EnemyBrain b = orphans[i];
                if (b == null) { orphans.RemoveAt(i); continue; }
                if (Vector3.Distance(b.transform.position, p) > 150f || b.State == EState.Dead && Vector3.Distance(b.transform.position, p) > 60f)
                {
                    Destroy(b.gameObject);
                    orphans.RemoveAt(i);
                }
            }
        }

        /// <summary>Destroys all live enemies and re-creates the ones that are not permanently dead (after resting at a shrine).</summary>
        public void RespawnEnemies()
        {
            foreach (Chunk c in chunks.Values)
            {
                for (int i = 0; i < c.enemies.Count; i++) if (c.enemies[i] != null) Destroy(c.enemies[i].gameObject);
                c.enemies.Clear();
            }
            for (int i = 0; i < orphans.Count; i++) if (orphans[i] != null) Destroy(orphans[i].gameObject);
            orphans.Clear();
            foreach (var kv in chunks)
            {
                Chunk c = kv.Value;
                SpawnEnemies(c, c.plan.spawns, Game.Session);
                if (fixedByChunk.TryGetValue(kv.Key, out List<FixedSpawn> fixedList))
                {
                    var list = new List<SpawnPlan>();
                    foreach (FixedSpawn f in fixedList) list.Add(new SpawnPlan { id = f.id, enemy = f.enemy, pos = f.pos });
                    SpawnEnemies(c, list, Game.Session);
                }
            }
        }

        // ------------------------------------------------------------------ instanced rendering
        void RebuildBatches(Vector3 cam)
        {
            foreach (Batch b in batches.Values) b.count = 0;
            Camera mc = Camera.main;
            bool useFrustum = mc != null;
            if (useFrustum) GeometryUtility.CalculateFrustumPlanes(mc, frustum);
            foreach (Chunk c in chunks.Values)
            {
                if (useFrustum)
                {
                    Vector3 toChunk = c.plan.center - cam;
                    bool near = new Vector2(toChunk.x, toChunk.z).sqrMagnitude < 110f * 110f;
                    if (!near && !GeometryUtility.TestPlanesAABB(frustum, c.bounds)) continue;
                }
                for (int s = 0; s < c.plan.inst.Length; s++)
                {
                    List<Inst> list = c.plan.inst[s];
                    if (list.Count == 0) continue;
                    Species sp = props.species[s];
                    float maxD2 = sp.maxDrawDistance * sp.maxDrawDistance;
                    for (int i = 0; i < list.Count; i++)
                    {
                        Inst inst = list[i];
                        float dx = inst.pos.x - cam.x, dz = inst.pos.z - cam.z;
                        float d2 = dx * dx + dz * dz;
                        if (d2 > maxD2) continue;
                        int lod = d2 < 85f * 85f ? 0 : 1;
                        int key = ((s * 8 + inst.variant) * 2 + lod);
                        if (!batches.TryGetValue(key, out Batch b))
                        {
                            b = new Batch { species = sp, variant = inst.variant, lod = lod };
                            batches[key] = b;
                        }
                        Matrix4x4 m = inst.Matrix;
                        b.Add(in m);
                    }
                }
            }
        }

        void RenderBatches(Vector3 cam)
        {
            var bounds = new Bounds(cam, new Vector3(2400f, 800f, 2400f));
            foreach (Batch b in batches.Values)
            {
                if (b.count == 0) continue;
                Species sp = b.species;
                Mesh mesh = sp.lod[b.lod];
                for (int sub = 0; sub < sp.SubmeshCount; sub++)
                {
                    Material[] variants = sp.materials[sub];
                    Material mat = variants[Mathf.Min(b.variant, variants.Length - 1)];
                    var rp = new RenderParams(mat)
                    {
                        shadowCastingMode = sp.castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                        receiveShadows = true,
                        worldBounds = bounds,
                        layer = 0
                    };
                    for (int start = 0; start < b.count; start += 1023)
                    {
                        int n = Mathf.Min(1023, b.count - start);
                        Graphics.RenderMeshInstanced(rp, mesh, sub, b.arr, n, start);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ colliders that follow the player
        struct Cand
        {
            public Vector3 pos;
            public float radius, height, d2;
            public bool rock;
        }

        readonly List<Cand> cands = new List<Cand>(256);

        void UpdateColliders(Vector3 p)
        {
            cands.Clear();
            int cx = Mathf.FloorToInt(p.x / WorldLayout.ChunkSize), cz = Mathf.FloorToInt(p.z / WorldLayout.ChunkSize);
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (!chunks.TryGetValue(Key(cx + dx, cz + dz), out Chunk c)) continue;
                    for (int s = 0; s < c.plan.inst.Length; s++)
                    {
                        Species sp = props.species[s];
                        if (sp.colliderRadius <= 0f) continue;
                        List<Inst> list = c.plan.inst[s];
                        bool rock = s == PropLibrary.RockMid || s == PropLibrary.RockBig;
                        for (int i = 0; i < list.Count; i++)
                        {
                            Inst inst = list[i];
                            float ddx = inst.pos.x - p.x, ddz = inst.pos.z - p.z;
                            float d2 = ddx * ddx + ddz * ddz;
                            if (d2 > 26f * 26f) continue;
                            cands.Add(new Cand { pos = inst.pos, radius = sp.colliderRadius * inst.scale, height = sp.colliderHeight * inst.scale, d2 = d2, rock = rock });
                        }
                    }
                }
            cands.Sort((a, b) => a.d2.CompareTo(b.d2));
            int ti = 0, ri = 0;
            for (int i = 0; i < cands.Count; i++)
            {
                Cand c = cands[i];
                if (c.rock)
                {
                    if (ri >= rockCols.Length) continue;
                    var sc = rockCols[ri++];
                    sc.gameObject.SetActive(true);
                    sc.transform.position = c.pos + new Vector3(0f, c.radius * 0.5f, 0f);
                    sc.radius = c.radius;
                }
                else
                {
                    if (ti >= treeCols.Length) continue;
                    var cc = treeCols[ti++];
                    cc.gameObject.SetActive(true);
                    cc.transform.position = c.pos + new Vector3(0f, c.height * 0.5f, 0f);
                    cc.radius = c.radius;
                    cc.height = c.height;
                }
            }
            for (; ti < treeCols.Length; ti++) if (treeCols[ti].gameObject.activeSelf) treeCols[ti].gameObject.SetActive(false);
            for (; ri < rockCols.Length; ri++) if (rockCols[ri].gameObject.activeSelf) rockCols[ri].gameObject.SetActive(false);
        }
    }
}
