using System.Collections.Generic;
using ThienKhuyet.Audio;
using ThienKhuyet.Combat;
using ThienKhuyet.Core;
using ThienKhuyet.Data;
using ThienKhuyet.Gfx;
using ThienKhuyet.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThienKhuyet.World
{
    /// <summary>A dropped item. Pooled; floats, glows by rarity, is pulled to the player when close and collected on contact.</summary>
    public sealed class PickupItem : MonoBehaviour
    {
        static readonly List<PickupItem> alive = new List<PickupItem>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            alive.Clear();
        }

        public string itemId;
        public int count = 1;
        Vector3 velocity;
        float age;
        bool pulled;
        GameObject sparkle;
        MeshRenderer orb;

        public static IReadOnlyList<PickupItem> Alive => alive;

        static GameObjectPool Pool => Pools.Get("pickup", () =>
        {
            var go = new GameObject("Pickup");
            go.layer = GameLayers.Pickup;
            var sc = go.AddComponent<SphereCollider>();
            sc.radius = 0.3f;
            sc.isTrigger = true;
            var visual = new GameObject("Orb");
            visual.transform.SetParent(go.transform, false);
            var mb = new MeshBuilder();
            mb.Sphere(Vector3.zero, 0.14f, 10, 7);
            visual.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("orb", false);
            var mr = visual.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            go.AddComponent<PickupItem>().orb = mr;
            return go;
        });

        public static PickupItem Spawn(string itemId, int count, Vector3 position, bool pop = true)
        {
            ItemDef def = ContentDB.Item(itemId);
            if (def == null || count <= 0) return null;
            GameObject go = Pool.Get(position + Vector3.up * 0.4f);
            var p = go.GetComponent<PickupItem>();
            p.itemId = itemId;
            p.count = count;
            p.age = 0f;
            p.pulled = false;
            Vector2 r = Random.insideUnitCircle.normalized * Random.Range(1.5f, 3f);
            p.velocity = pop ? new Vector3(r.x, Random.Range(3.2f, 5f), r.y) : Vector3.zero;
            Color c = def.RarityColor;
            if (def.type == ItemType.Material && def.id == "spirit_stone") c = new Color(0.5f, 0.9f, 1f);
            p.orb.sharedMaterial = Mats.Cached("pickup_" + ColorUtility.ToHtmlStringRGB(c), () => Mats.Glow(c, 2.4f));
            p.orb.transform.localScale = Vector3.one * (def.rarity >= Rarity.Rare ? 1.5f : 1f);
            if (p.sparkle != null) Vfx.Stop(p.sparkle);
            p.sparkle = Vfx.Play("loot_sparkle", go.transform.position, Quaternion.identity, def.rarity >= Rarity.Rare ? 1.6f : 1f);
            return p;
        }

        void OnEnable()
        {
            if (!alive.Contains(this)) alive.Add(this);
        }

        void OnDisable()
        {
            alive.Remove(this);
            if (sparkle != null) { Vfx.Stop(sparkle); sparkle = null; }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (age > 180f) { Release(); return; }
            Vector3 pos = transform.position;
            Transform player = Game.PlayerObject != null ? Game.PlayerObject.transform : null;
            if (velocity.sqrMagnitude > 0.01f || pos.y > GroundY(pos) + 0.35f)
            {
                velocity.y -= 18f * dt;
                pos += velocity * dt;
                float g = GroundY(pos) + 0.35f;
                if (pos.y < g) { pos.y = g; velocity = new Vector3(velocity.x * 0.3f, Mathf.Abs(velocity.y) * 0.25f, velocity.z * 0.3f); if (velocity.y < 0.6f) velocity = Vector3.zero; }
            }
            else pos.y = GroundY(pos) + 0.45f + Mathf.Sin(Time.time * 2.2f + age) * 0.08f;

            if (player != null && age > 0.7f && Game.Mode == GameMode.Playing)
            {
                Vector3 to = player.position + Vector3.up * 1f - pos;
                float d = to.magnitude;
                if (d < 3.2f || pulled)
                {
                    pulled = true;
                    velocity = Vector3.zero;
                    pos += to.normalized * (7f + (3.2f - Mathf.Min(d, 3.2f)) * 5f) * dt;
                    if (d < 0.7f && Collect()) return;
                }
            }
            transform.position = pos;
            if (sparkle != null) sparkle.transform.position = pos;
            transform.Rotate(0f, 120f * dt, 0f);
        }

        static float GroundY(Vector3 p)
        {
            return Game.World != null ? Game.World.GroundHeightAt(p) : p.y - 0.5f;
        }

        bool Collect()
        {
            if (Game.Manager == null) return false;
            if (!Game.Manager.GiveItem(itemId, count, true, false)) { pulled = false; age = Mathf.Min(age, 60f); return false; }
            AudioManager.Instance?.Sfx2D(ContentDB.Item(itemId) != null && ContentDB.Item(itemId).rarity >= Rarity.Rare ? "pickup_rare" : "pickup", 0.7f);
            Release();
            return true;
        }

        void Release()
        {
            Pool.Release(gameObject);
        }
    }

    /// <summary>Herb or ore node: interact to gather loot, respawns after a while.</summary>
    public sealed class Gatherable : MonoBehaviour, IInteractable
    {
        public string nodeId;
        public bool ore;
        GameObject visual;
        GameObject fx;
        bool depleted;
        float checkTimer;

        public string PromptKey => ore ? "prompt.mine" : "prompt.gather";
        public bool CanInteract => !depleted && Game.Mode == GameMode.Playing;
        public Vector3 InteractPoint => transform.position;
        public float InteractRadius => 2.6f;

        public static Gatherable Create(Transform parent, string id, bool ore, Vector3 pos, WorldAssets assets)
        {
            var go = new GameObject((ore ? "Ore_" : "Herb_") + id);
            go.layer = GameLayers.Interactable;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var sc = go.AddComponent<SphereCollider>();
            sc.radius = 1.0f;
            sc.isTrigger = true;
            sc.center = new Vector3(0f, 0.5f, 0f);
            var g = go.AddComponent<Gatherable>();
            g.nodeId = id;
            g.ore = ore;
            g.BuildVisual(assets);
            g.depleted = Game.Session != null && Game.Session.nodeRespawn.TryGetValue(id, out double t) && t > Game.Session.playSeconds;
            g.visual.SetActive(!g.depleted);
            if (g.depleted && g.fx != null) Vfx.Stop(g.fx);
            return g;
        }

        void BuildVisual(WorldAssets assets)
        {
            visual = new GameObject("Visual");
            visual.transform.SetParent(transform, false);
            var mb = new MeshBuilder();
            var rng = new Rng(nodeId.GetHashCode());
            if (ore)
            {
                mb.Sub(0).PaletteUv(Palette.Uv(new Color(0.42f, 0.42f, 0.45f)));
                mb.Ellipsoid(new Vector3(0f, 0.28f, 0f), new Vector3(0.55f, 0.36f, 0.5f), 8, 5);
                mb.Ellipsoid(new Vector3(0.4f, 0.2f, 0.2f), new Vector3(0.3f, 0.22f, 0.28f), 7, 4);
                mb.Sub(1).PaletteUv(Palette.Uv(new Color(0.7f, 0.85f, 0.95f)));
                for (int i = 0; i < 4; i++)
                {
                    float a = i * 1.7f;
                    mb.Limb(new Vector3(Mathf.Cos(a) * 0.25f, 0.3f, Mathf.Sin(a) * 0.25f), new Vector3(Mathf.Cos(a) * 0.3f, 0.7f + rng.Value() * 0.2f, Mathf.Sin(a) * 0.3f), 0.07f, 0.01f, 5, false, true);
                }
            }
            else
            {
                for (int i = 0; i < 6; i++)
                {
                    float a = i / 6f * Mathf.PI * 2f + rng.Value();
                    Vector3 b = new Vector3(Mathf.Cos(a) * 0.12f, 0f, Mathf.Sin(a) * 0.12f);
                    Vector3 t = b + new Vector3(Mathf.Cos(a) * 0.35f, 0.45f + rng.Value() * 0.2f, Mathf.Sin(a) * 0.35f);
                    mb.Sub(0).PaletteUv(Palette.Uv(new Color(0.22f, 0.5f, 0.2f)));
                    mb.Limb(b, t, 0.018f, 0.01f, 4, false, false);
                    mb.Ellipsoid(t, new Vector3(0.14f, 0.045f, 0.09f), 6, 3);
                }
                mb.Sub(1).PaletteUv(Palette.Uv(new Color(0.75f, 1f, 0.95f)));
                for (int i = 0; i < 3; i++) mb.Sphere(new Vector3(Mathf.Cos(i * 2.1f) * 0.2f, 0.52f + i * 0.05f, Mathf.Sin(i * 2.1f) * 0.2f), 0.045f, 6, 4);
            }
            Palette.Flush();
            visual.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("node", false);
            var mr = visual.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { Palette.Standard, ore ? Palette.Metal : assets.matGlowCyan };
            fx = Vfx.Play("loot_sparkle", transform.position + Vector3.up * 0.5f, Quaternion.identity, 0.9f);
        }

        public void ResetForNewGame()
        {
            depleted = false;
            checkTimer = 0f;
            if (visual != null) visual.SetActive(true);
            if (fx == null) fx = Vfx.Play("loot_sparkle", transform.position + Vector3.up * 0.5f, Quaternion.identity, 0.9f);
        }

        void Update()
        {
            if (!depleted) return;
            checkTimer -= Time.deltaTime;
            if (checkTimer > 0f || Game.Session == null) return;
            checkTimer = 2f;
            if (!Game.Session.nodeRespawn.TryGetValue(nodeId, out double t) || t <= Game.Session.playSeconds)
            {
                depleted = false;
                visual.SetActive(true);
                fx = Vfx.Play("loot_sparkle", transform.position + Vector3.up * 0.5f, Quaternion.identity, 0.9f);
            }
        }

        public void Interact(PlayerController player)
        {
            if (depleted) return;
            depleted = true;
            visual.SetActive(false);
            if (fx != null) { Vfx.Stop(fx); fx = null; }
            Game.Session.nodeRespawn[nodeId] = Game.Session.playSeconds + 420.0;
            player.anim?.PlayAction("gather", 1.15f);
            AudioManager.Instance?.Sfx(ore ? "hit" : "pickup", transform.position, 0.6f);
            Game.Manager.RollLoot(ore ? "ore_node" : "herb_node", transform.position, false);
            EventBus.Publish(new InteractEvent { objectId = ore ? "ore" : "herb" });
        }

        void OnDestroy()
        {
            if (fx != null) Vfx.Stop(fx);
        }
    }

    /// <summary>Treasure chest: opens once, loot goes to the inventory.</summary>
    public sealed class Container : MonoBehaviour, IInteractable
    {
        public string containerId;
        public string lootTable = "chest_common";
        public string fixedItems = "";      // "item*n,item*n" guaranteed contents (story chests)
        Transform lid;
        bool opened;
        float openT = 1f;

        public string PromptKey => "prompt.open";
        public bool CanInteract => !opened && Game.Mode == GameMode.Playing;
        public Vector3 InteractPoint => transform.position;
        public float InteractRadius => 2.6f;

        public static Container Create(Transform parent, string id, Vector3 pos, float yaw, string table, WorldAssets assets, string fixedItems = "")
        {
            var go = new GameObject("Chest_" + id);
            go.layer = GameLayers.Interactable;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var bc = go.AddComponent<BoxCollider>();
            bc.size = new Vector3(1.1f, 0.8f, 0.7f);
            bc.center = new Vector3(0f, 0.4f, 0f);
            var c = go.AddComponent<Container>();
            c.containerId = id;
            c.lootTable = table;
            c.fixedItems = fixedItems;
            c.Build(assets);
            c.opened = Game.Session != null && Game.Session.openedContainers.Contains(id);
            if (c.opened) { c.lid.localRotation = Quaternion.Euler(-75f, 0f, 0f); }
            return c;
        }

        void Build(WorldAssets assets)
        {
            var body = new GameObject("Body");
            body.transform.SetParent(transform, false);
            var mb = new MeshBuilder();
            mb.Box(new Vector3(0f, 0.28f, 0f), new Vector3(1f, 0.56f, 0.62f), 0.5f);
            body.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("chest_body", true);
            body.AddComponent<MeshRenderer>().sharedMaterial = assets.matDarkWood;
            var bands = new GameObject("Bands");
            bands.transform.SetParent(transform, false);
            var bb = new MeshBuilder();
            bb.Sub(0).PaletteUv(Palette.Uv(new Color(0.75f, 0.6f, 0.25f)));
            bb.Box(new Vector3(-0.36f, 0.28f, 0f), new Vector3(0.06f, 0.58f, 0.64f));
            bb.Box(new Vector3(0.36f, 0.28f, 0f), new Vector3(0.06f, 0.58f, 0.64f));
            bb.Box(new Vector3(0f, 0.5f, 0.31f), new Vector3(0.12f, 0.14f, 0.03f));
            Palette.Flush();
            bands.AddComponent<MeshFilter>().sharedMesh = bb.ToMesh("chest_bands", false);
            bands.AddComponent<MeshRenderer>().sharedMaterial = Palette.Metal;
            var lidGo = new GameObject("Lid");
            lidGo.transform.SetParent(transform, false);
            lidGo.transform.localPosition = new Vector3(0f, 0.56f, -0.31f);
            lid = lidGo.transform;
            var lb = new MeshBuilder();
            lb.Push(new Vector3(0f, 0f, 0.31f), Quaternion.Euler(0f, 0f, 90f), Vector3.one);
            lb.Cylinder(new Vector3(0f, -0.5f, 0f), 1f, 0.3f, 0.3f, 10, true, true, 0.5f);
            lb.Pop();
            lidGo.AddComponent<MeshFilter>().sharedMesh = lb.ToMesh("chest_lid", true);
            lidGo.AddComponent<MeshRenderer>().sharedMaterial = assets.matDarkWood;
        }

        public void ResetForNewGame()
        {
            opened = false;
            openT = 1f;
            if (lid != null) lid.localRotation = Quaternion.identity;
        }

        void Update()
        {
            if (openT < 1f)
            {
                openT = Mathf.Min(1f, openT + Time.deltaTime * 2.2f);
                lid.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -75f, Mathx.EaseOutBack(openT)), 0f, 0f);
            }
        }

        public void Interact(PlayerController player)
        {
            if (opened) return;
            opened = true;
            openT = 0f;
            Game.Session.openedContainers.Add(containerId);
            AudioManager.Instance?.Sfx("wood_creak", transform.position, 0.8f);
            Vfx.Play("aura_gold", transform.position + Vector3.up * 0.4f, Quaternion.identity, 0.7f);
            if (!string.IsNullOrEmpty(fixedItems))
            {
                foreach (string part in fixedItems.Split(','))
                {
                    int star = part.IndexOf('*');
                    string id = star > 0 ? part.Substring(0, star) : part;
                    int n = 1;
                    if (star > 0) int.TryParse(part.Substring(star + 1), out n);
                    Game.Manager.GiveItem(id.Trim(), Mathf.Max(1, n), true, true);
                }
            }
            if (!string.IsNullOrEmpty(lootTable)) Game.Manager.RollLoot(lootTable, transform.position + Vector3.up * 0.6f, true);
            EventBus.Publish(new InteractEvent { objectId = "chest" });
        }
    }

    /// <summary>Spirit altar: heals, sets the respawn point, refreshes the world's creatures and saves the game.</summary>
    public sealed class Shrine : MonoBehaviour, IInteractable
    {
        public string shrineId;
        public string PromptKey => "prompt.rest";
        public bool CanInteract => Game.Mode == GameMode.Playing;
        public Vector3 InteractPoint => transform.position;
        public float InteractRadius => 3.2f;

        public static Shrine Create(Transform parent, string id, Vector3 pos, WorldAssets assets)
        {
            var go = new GameObject("Shrine_" + id);
            go.layer = GameLayers.Interactable;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var cc = go.AddComponent<CapsuleCollider>();
            cc.radius = 0.7f; cc.height = 1.8f; cc.center = new Vector3(0f, 0.9f, 0f);
            var s = go.AddComponent<Shrine>();
            s.shrineId = id;
            var mb = new MeshBuilder();
            mb.Cylinder(Vector3.zero, 0.25f, 0.9f, 0.8f, 8, true, true, 1f);
            mb.Cylinder(new Vector3(0f, 0.25f, 0f), 0.9f, 0.34f, 0.26f, 8, false, true, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("shrine", true);
            go.AddComponent<MeshRenderer>().sharedMaterial = assets.matMarble;
            var orb = new GameObject("Orb");
            orb.transform.SetParent(go.transform, false);
            orb.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            var ob = new MeshBuilder();
            ob.Sphere(Vector3.zero, 0.2f, 10, 7);
            orb.AddComponent<MeshFilter>().sharedMesh = ob.ToMesh("shrine_orb", false);
            orb.AddComponent<MeshRenderer>().sharedMaterial = assets.matGlowCyan;
            var pl = new GameObject("Light");
            pl.transform.SetParent(orb.transform, false);
            var l = pl.AddComponent<Light>();
            l.type = LightType.Point; l.range = 9f; l.intensity = 4f; l.color = new Color(0.5f, 0.85f, 1f); l.shadows = LightShadows.None;
            Vfx.Play("aura_blue", pos + Vector3.up * 1.2f, Quaternion.identity, 0.6f);
            return s;
        }

        public void Interact(PlayerController player)
        {
            Game.Manager.RestAtShrine(this);
        }
    }

    /// <summary>Stone circle on a spiritual vein: better meditation (more EXP, faster recovery) and the place for breakthroughs.</summary>
    public sealed class MeditationSpot : MonoBehaviour, IInteractable
    {
        public float radius = 6f;
        public string PromptKey => "prompt.meditate";
        public bool CanInteract => Game.Mode == GameMode.Playing;
        public Vector3 InteractPoint => transform.position;
        public float InteractRadius => radius;

        public void Interact(PlayerController player)
        {
            Vector3 p = transform.position;
            player.Teleport(new Vector3(p.x, p.y + 0.05f, p.z), player.transform.eulerAngles.y);
            player.StartMeditation(true);
        }
    }

    /// <summary>A bed: sleep until the next morning, recover and let the story advance.</summary>
    public sealed class Bed : MonoBehaviour, IInteractable
    {
        public string bedId = "bed";
        public string PromptKey => "prompt.sleep";
        public bool CanInteract => Game.Mode == GameMode.Playing;
        public Vector3 InteractPoint => transform.position;
        public float InteractRadius => 2.8f;

        public void Interact(PlayerController player)
        {
            Game.Manager.Sleep(this);
        }
    }

    /// <summary>Readable lore: shows a message window.</summary>
    public sealed class Sign : MonoBehaviour, IInteractable
    {
        public string textKey;
        public string titleKey;
        public string PromptKey => "prompt.read";
        public bool CanInteract => Game.Mode == GameMode.Playing;
        public Vector3 InteractPoint => transform.position;
        public float InteractRadius => 2.8f;

        public void Interact(PlayerController player)
        {
            Game.UI?.ShowMessage(titleKey, textKey);
            EventBus.Publish(new InteractEvent { objectId = "sign:" + textKey });
        }
    }

    /// <summary>Generic scripted trigger: interact to run an effect script (flags, quests, cutscenes).</summary>
    public sealed class ScriptedInteract : MonoBehaviour, IInteractable
    {
        public string objectId;
        public string promptKey = "prompt.examine";
        public string effects = "";
        public string condition = "";
        public bool once = true;
        public float radius = 3f;
        bool used;

        public string PromptKey => promptKey;
        public bool CanInteract => Game.Mode == GameMode.Playing && !(once && used) && Cond.Eval(condition, Game.Manager);
        public Vector3 InteractPoint => transform.position;
        public float InteractRadius => radius;

        public void Interact(PlayerController player)
        {
            used = true;
            EventBus.Publish(new InteractEvent { objectId = objectId });
            if (!string.IsNullOrEmpty(effects)) Fx.Apply(effects, Game.Manager);
        }
    }
}
