using System;
using System.Collections;
using System.Collections.Generic;
using ThienKhuyet.Core;
using ThienKhuyet.Gfx;
using UnityEngine;

namespace ThienKhuyet.World
{
    /// <summary>
    /// Owns the generated open world: terrain, water, props, settlements, streaming and environment, and answers gameplay
    /// queries (ground height, water, surface type, anchors, zones, fog of war). Built over many frames behind the loading screen.
    /// </summary>
    public sealed class WorldManager : MonoBehaviour
    {
        public const int FogRes = 64;

        public WorldGen Gen { get; private set; }
        public HeightField Heights { get; private set; }
        public Terrain Terrain { get; private set; }
        public WorldAssets Assets { get; private set; }
        public PropLibrary Props { get; private set; }
        public PoiBuilder Pois { get; private set; }
        public EnvironmentController Env { get; private set; }
        public ChunkStreamer Streamer { get; private set; }
        public Texture2D MapTexture { get; private set; }
        public byte[] Fog { get; private set; } = new byte[FogRes * FogRes];
        public bool FogDirty { get; set; } = true;
        public bool Ready { get; private set; }
        public string CurrentZone { get; private set; } = "";
        public string CurrentPoiId { get; private set; } = "";

        float scanTimer;
        Transform worldRoot;

        // ------------------------------------------------------------------ build
        public IEnumerator Build(int seed, GameSettings settings, Camera cam, Action<float, string> report)
        {
            Ready = false;
            worldRoot = new GameObject("World").transform;
            worldRoot.SetParent(transform, false);
            Gen = new WorldGen(seed);

            Assets = new WorldAssets();
            yield return Assets.Build((p, n) => report?.Invoke(0.18f * p, n));
            Props = new PropLibrary(Assets);
            report?.Invoke(0.2f, "props");
            yield return null;

            var result = new TerrainResult();
            yield return TerrainBuilder.Build(Gen, Assets, worldRoot, result, (p, n) => report?.Invoke(0.2f + 0.5f * p, n));
            Terrain = result.terrain;
            Heights = result.heights;

            WaterSystem.Build(Gen, Assets, worldRoot);
            Env = EnvironmentController.Create(transform, cam);
            report?.Invoke(0.72f, "environment");
            yield return null;

            Pois = new PoiBuilder(Gen, Heights, Assets, Props, worldRoot);
            yield return Pois.BuildAll((p, n) => report?.Invoke(0.72f + 0.18f * p, n));

            Streamer = ChunkStreamer.Create(transform, Gen, Heights, Props, Assets, settings, Pois.spawns);
            BuildBounds();
            yield return BuildMap(p => report?.Invoke(0.92f + 0.06f * p, "map"));
            Ready = true;
            report?.Invoke(1f, "ready");
        }

        void BuildBounds()
        {
            var go = new GameObject("WorldBounds");
            go.transform.SetParent(worldRoot, false);
            go.layer = GameLayers.Environment;
            float s = WorldLayout.Size, t = 20f, h = 400f;
            AddWall(go.transform, new Vector3(s * 0.5f, h * 0.5f, -t * 0.5f + 4f), new Vector3(s + t * 2f, h, t));
            AddWall(go.transform, new Vector3(s * 0.5f, h * 0.5f, s + t * 0.5f - 4f), new Vector3(s + t * 2f, h, t));
            AddWall(go.transform, new Vector3(-t * 0.5f + 4f, h * 0.5f, s * 0.5f), new Vector3(t, h, s + t * 2f));
            AddWall(go.transform, new Vector3(s + t * 0.5f - 4f, h * 0.5f, s * 0.5f), new Vector3(t, h, s + t * 2f));
        }

        static void AddWall(Transform parent, Vector3 center, Vector3 size)
        {
            var w = new GameObject("Wall");
            w.layer = GameLayers.Environment;
            w.transform.SetParent(parent, false);
            w.transform.position = center;
            w.AddComponent<BoxCollider>().size = size;
        }

        IEnumerator BuildMap(Action<float> progress)
        {
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false, false) { name = "WorldMap", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                float wz = (y + 0.5f) / n * WorldLayout.Size;
                for (int x = 0; x < n; x++)
                {
                    float wx = (x + 0.5f) / n * WorldLayout.Size;
                    float h = Heights.Sample(wx, wz);
                    float light = Mathf.Clamp01(0.62f + (Heights.Sample(wx - 6f, wz + 6f) - Heights.Sample(wx + 6f, wz - 6f)) * 0.035f);
                    Color c;
                    if (Gen.WaterLevel(wx, wz) > -500f) c = new Color(0.2f, 0.42f, 0.62f);
                    else if (h > 205f) c = new Color(0.9f, 0.92f, 0.95f);
                    else if (h > 150f) c = Color.Lerp(new Color(0.5f, 0.5f, 0.46f), new Color(0.74f, 0.74f, 0.72f), Mathf.InverseLerp(150f, 205f, h));
                    else if (h > 80f) c = Color.Lerp(new Color(0.34f, 0.46f, 0.26f), new Color(0.5f, 0.5f, 0.42f), Mathf.InverseLerp(80f, 150f, h));
                    else c = Color.Lerp(new Color(0.42f, 0.58f, 0.3f), new Color(0.34f, 0.46f, 0.26f), Mathf.InverseLerp(30f, 80f, h));
                    if (Gen.RoadMask(wx, wz, 2.2f) > 0.5f) c = new Color(0.72f, 0.6f, 0.4f);
                    c *= light;
                    c.a = 1f;
                    px[y * n + x] = c;
                }
                if (y % 16 == 15)
                {
                    progress?.Invoke((float)y / n);
                    yield return null;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            MapTexture = tex;
        }

        // ------------------------------------------------------------------ queries
        public float GroundHeightAt(Vector3 p)
        {
            if (Terrain != null) return Terrain.SampleHeight(p) + Terrain.transform.position.y;
            return Heights != null ? Heights.Sample(p.x, p.z) : p.y;
        }

        public float WaterHeightAt(Vector3 p)
        {
            return Gen != null ? Gen.WaterLevel(p.x, p.z) : -1000f;
        }

        public bool TryGetAnchor(string id, out Anchor a)
        {
            if (Pois != null && Pois.anchors.TryGetValue(id, out a)) return true;
            a = default;
            return false;
        }

        public Vector3 AnchorPos(string id, Vector3 fallback)
        {
            return TryGetAnchor(id, out Anchor a) ? a.pos : fallback;
        }

        public string SurfaceAt(Vector3 p)
        {
            if (Gen == null) return "dirt";
            if (Physics.Raycast(p + Vector3.up * 0.6f, Vector3.down, out RaycastHit hit, 2.2f, GameLayers.GroundMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.gameObject.layer == GameLayers.Environment)
                {
                    Transform t = hit.collider.transform;
                    for (int i = 0; i < 4 && t != null; i++, t = t.parent)
                    {
                        string n = t.name;
                        if (n.Contains("Bridge") || n.Contains("House") || n.Contains("Hut") || n.Contains("Hall") || n.Contains("Tower") || n.Contains("Lodge")) return "wood";
                    }
                    return "stone";
                }
            }
            float h = p.y;
            float slope = Heights != null ? Mathf.Clamp01(Heights.Gradient(p.x, p.z)) : 0f;
            var w = new float[6];
            Gen.LayerWeights(p.x, p.z, h, slope, w);
            int best = 0;
            for (int i = 1; i < 6; i++) if (w[i] > w[best]) best = i;
            switch (best)
            {
                case 1: return "dirt";
                case 2: return "stone";
                case 3: return "dirt";
                case 4: return "dirt";
                default: return "grass";
            }
        }

        public bool IsMeditationSpot(Vector3 p)
        {
            if (Pois == null) return false;
            for (int i = 0; i < Pois.meditationSpots.Count; i++)
            {
                MeditationSpot s = Pois.meditationSpots[i];
                if (s == null) continue;
                if (Mathx.FlatDistance(p, s.transform.position) <= s.radius) return true;
            }
            return false;
        }

        public PoiInstance NearestPoi(Vector3 p, out float distance)
        {
            PoiInstance best = null;
            distance = float.MaxValue;
            if (Pois == null) return null;
            for (int i = 0; i < Pois.instances.Count; i++)
            {
                PoiInstance inst = Pois.instances[i];
                float d = Mathx.FlatDistance(p, inst.center) - inst.def.radius;
                if (d < distance) { distance = d; best = inst; }
            }
            return best;
        }

        // ------------------------------------------------------------------ per-frame world tracking (discovery, zones, fog)
        void Update()
        {
            if (!Ready || Game.PlayerObject == null || Game.Session == null) return;
            scanTimer -= Time.unscaledDeltaTime;
            if (scanTimer > 0f) return;
            scanTimer = 0.5f;
            Vector3 p = Game.PlayerObject.transform.position;
            RevealFog(p, 70f);

            PoiInstance nearest = NearestPoi(p, out float dist);
            string zone = nearest != null && dist < 120f ? nearest.def.zone : "wild";
            if (zone != CurrentZone)
            {
                CurrentZone = zone;
                EventBus.Publish(new ZoneChangedEvent { zoneId = zone });
            }
            string poiId = nearest != null && dist <= 0f ? nearest.def.id : "";
            CurrentPoiId = poiId;
            for (int i = 0; i < Pois.instances.Count; i++)
            {
                PoiInstance inst = Pois.instances[i];
                if (Game.Session.discoveredPois.Contains(inst.def.id)) continue;
                float d = Mathx.FlatDistance(p, inst.center);
                if (d <= Mathf.Max(18f, inst.def.radius * 0.8f))
                {
                    Game.Session.discoveredPois.Add(inst.def.id);
                    EventBus.Publish(new PoiDiscoveredEvent { poiId = inst.def.id });
                }
            }
            // distance streaming of structures: hide far settlements to save rendering
            for (int i = 0; i < Pois.instances.Count; i++)
            {
                PoiInstance inst = Pois.instances[i];
                bool near = Mathx.FlatDistance(p, inst.center) < inst.def.radius + 320f;
                if (near != inst.active)
                {
                    inst.active = near;
                    inst.root.SetActive(near);
                }
            }
        }

        public void RevealFog(Vector3 p, float radius)
        {
            float cell = WorldLayout.Size / FogRes;
            int cx = Mathf.FloorToInt(p.x / cell), cz = Mathf.FloorToInt(p.z / cell);
            int r = Mathf.CeilToInt(radius / cell);
            bool changed = false;
            for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = cx + dx, z = cz + dz;
                    if (x < 0 || z < 0 || x >= FogRes || z >= FogRes) continue;
                    float d = Mathf.Sqrt(dx * dx + dz * dz) * cell;
                    if (d > radius) continue;
                    if (Fog[z * FogRes + x] == 0) { Fog[z * FogRes + x] = 1; changed = true; }
                }
            if (changed) FogDirty = true;
        }

        public void RevealAll()
        {
            for (int i = 0; i < Fog.Length; i++) Fog[i] = 1;
            FogDirty = true;
        }

        public void LoadFog(byte[] data)
        {
            if (data != null && data.Length == Fog.Length) Array.Copy(data, Fog, Fog.Length);
            FogDirty = true;
        }

        public void Shutdown()
        {
            Ready = false;
            if (worldRoot != null) Destroy(worldRoot.gameObject);
            if (Env != null) Destroy(Env.gameObject);
            if (Streamer != null) Destroy(Streamer.gameObject);
        }
    }
}
