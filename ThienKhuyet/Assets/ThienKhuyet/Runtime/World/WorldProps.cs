using System.Collections.Generic;
using ThienKhuyet.Audio;
using ThienKhuyet.Combat;
using ThienKhuyet.Core;
using ThienKhuyet.Data;
using ThienKhuyet.Gfx;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThienKhuyet.World
{
    /// <summary>Wooden practice dummy: soaks hits, wobbles, and (for the parry drill) periodically swings a wooden arm.</summary>
    public sealed class TrainingDummy : MonoBehaviour, IKnockbackable
    {
        public Vitals vitals;
        public bool swings;
        Transform body, arm;
        float wobble, wobbleVel;
        float swingTimer = 3f, swingT = -1f;
        Vector3 baseRot;

        public static TrainingDummy Create(Transform parent, Vector3 pos, float yaw, WorldAssets assets, bool swings)
        {
            var go = new GameObject(swings ? "ParryDummy" : "TrainingDummy");
            go.layer = GameLayers.Enemy;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var cc = go.AddComponent<CapsuleCollider>();
            cc.radius = 0.45f; cc.height = 1.9f; cc.center = new Vector3(0f, 0.95f, 0f);
            var v = go.AddComponent<Vitals>();
            v.team = Team.Enemy;
            v.stats.SetBase(StatId.MaxHp, 99999f);
            v.stats.SetBase(StatId.Poise, 100000f);
            v.SetFull();
            var d = go.AddComponent<TrainingDummy>();
            d.vitals = v;
            d.swings = swings;
            d.Build(assets);
            v.Damaged += d.OnDamaged;
            return d;
        }

        void Build(WorldAssets a)
        {
            var post = new GameObject("Post");
            post.transform.SetParent(transform, false);
            var pb = new MeshBuilder();
            pb.Cylinder(new Vector3(0f, -0.3f, 0f), 0.7f, 0.18f, 0.14f, 8, true, true, 1f);
            post.AddComponent<MeshFilter>().sharedMesh = pb.ToMesh("dummy_post", true);
            post.AddComponent<MeshRenderer>().sharedMaterial = a.matDarkWood;

            var b = new GameObject("Body");
            b.transform.SetParent(transform, false);
            b.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            body = b.transform;
            var mb = new MeshBuilder();
            mb.Cylinder(Vector3.zero, 1.25f, 0.12f, 0.1f, 8, true, true, 1f);
            mb.Ellipsoid(new Vector3(0f, 0.85f, 0f), new Vector3(0.34f, 0.42f, 0.26f), 9, 6, 1f);
            mb.Ellipsoid(new Vector3(0f, 1.45f, 0f), new Vector3(0.19f, 0.22f, 0.19f), 8, 5, 1f);
            mb.Box(new Vector3(0f, 1.0f, 0f), new Vector3(1.05f, 0.14f, 0.16f), 1f);
            b.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("dummy_body", true);
            b.AddComponent<MeshRenderer>().sharedMaterial = a.matThatch;
            if (swings)
            {
                var armGo = new GameObject("Arm");
                armGo.transform.SetParent(b.transform, false);
                armGo.transform.localPosition = new Vector3(0f, 1.0f, 0f);
                arm = armGo.transform;
                var ab = new MeshBuilder();
                ab.Limb(Vector3.zero, new Vector3(0f, 0f, 1.25f), 0.07f, 0.06f, 6, true, true);
                ab.Ellipsoid(new Vector3(0f, 0f, 1.3f), new Vector3(0.14f, 0.14f, 0.14f), 6, 4, 1f);
                armGo.AddComponent<MeshFilter>().sharedMesh = ab.ToMesh("dummy_arm", true);
                armGo.AddComponent<MeshRenderer>().sharedMaterial = a.matDarkWood;
            }
        }

        public void Knockback(Vector3 v)
        {
            wobbleVel += Mathf.Clamp(v.magnitude, 0f, 8f) * 40f;
        }

        void OnDamaged(DamageInfo info, DamageResult r)
        {
            vitals.Heal(99999f);
            if (r.dealt > 0f || r.blocked) wobbleVel += 220f;
            EventBus.Publish(new PlayerActionEvent { action = "hit_dummy" });
        }

        void Update()
        {
            float dt = Time.deltaTime;
            wobbleVel += (-wobble * 160f - wobbleVel * 9f) * dt;
            wobble += wobbleVel * dt;
            if (body != null) body.localRotation = Quaternion.Euler(wobble * 0.18f, 0f, wobble * 0.1f);

            if (!swings || Game.PlayerObject == null || Game.Mode != GameMode.Playing) return;
            float dist = Vector3.Distance(transform.position, Game.PlayerObject.transform.position);
            if (dist > 5f) { swingT = -1f; if (arm != null) arm.localRotation = Quaternion.identity; return; }
            swingTimer -= dt;
            if (swingT < 0f && swingTimer <= 0f)
            {
                swingT = 0f;
                swingTimer = 3.2f;
                Vfx.Ring(transform.position, 1.6f, new Color(1f, 0.8f, 0.2f, 0.7f), 0.6f, RingFx.Mode.Telegraph);
            }
            if (swingT >= 0f)
            {
                swingT += dt;
                float k = swingT;
                float yaw = k < 0.6f ? Mathf.Lerp(0f, -75f, k / 0.6f) : (k < 0.78f ? Mathf.Lerp(-75f, 70f, (k - 0.6f) / 0.18f) : Mathf.Lerp(70f, 0f, (k - 0.78f) / 0.5f));
                if (arm != null) arm.localRotation = Quaternion.Euler(0f, yaw, 0f);
                if (k >= 0.6f && k < 0.78f && !hitDone)
                {
                    hitDone = true;
                    var v = Game.PlayerVitals;
                    if (v != null && Vector3.Distance(transform.position, Game.PlayerObject.transform.position) < 2.6f)
                    {
                        var info = new DamageInfo { power = 3f, type = DamageType.Physical, team = Team.Enemy, source = gameObject, direction = (v.Center - transform.position).normalized, poiseDamage = 8f, knockback = 2f, attackerRealm = 0, tag = "dummy" };
                        v.TakeDamage(info);
                    }
                }
                if (k >= 1.3f) { swingT = -1f; hitDone = false; if (arm != null) arm.localRotation = Quaternion.identity; }
            }
        }

        bool hitDone;
    }

    /// <summary>Flat, additive rune array on the ground (spawn mark, meditation terrace, ruin door). Slowly rotates and breathes.</summary>
    public sealed class RuneDecal : MonoBehaviour
    {
        Material mat;
        float radius = 3f, speed = 8f, baseAlpha = 0.5f;
        Color color;
        float phase;

        public static RuneDecal Create(Transform parent, Vector3 pos, float radius, Color color, float alpha = 0.5f, float spinSpeed = 8f, int sides = 6)
        {
            var go = new GameObject("RuneDecal");
            go.transform.SetParent(parent, false);
            go.transform.position = pos + Vector3.up * 0.06f;
            var mb = new MeshBuilder();
            mb.Plane(new Vector2(2f, 2f), 2f);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("rune_quad", false);
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var d = go.AddComponent<RuneDecal>();
            d.mat = Mats.Particle(ProcTex.Rune(256, 3 + sides, sides), color, true);
            mr.sharedMaterial = d.mat;
            d.radius = radius;
            d.speed = spinSpeed;
            d.baseAlpha = alpha;
            d.color = color;
            d.phase = Random.value * 6f;
            go.transform.localScale = new Vector3(radius, 1f, radius);
            return d;
        }

        public void SetAlpha(float a)
        {
            baseAlpha = a;
        }

        void Update()
        {
            transform.Rotate(0f, speed * Time.deltaTime, 0f);
            float a = baseAlpha * (0.75f + 0.25f * Mathf.Sin(Time.time * 1.6f + phase));
            mat.SetColor("_BaseColor", new Color(color.r, color.g, color.b, a));
        }
    }

    /// <summary>Crystal shards floating and rotating around a point (the heart of the Thiên Khuyết, rift valley).</summary>
    public sealed class FloatingShards : MonoBehaviour
    {
        struct Shard
        {
            public Transform t;
            public float radius, height, speed, phase, bob;
        }

        readonly List<Shard> shards = new List<Shard>();

        public static FloatingShards Create(Transform parent, Vector3 center, int count, float radius, float heightMin, float heightMax, Material mat, int seed)
        {
            var go = new GameObject("FloatingShards");
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var f = go.AddComponent<FloatingShards>();
            var rng = new Rng(seed);
            var mb = new MeshBuilder();
            mb.Lathe(Vector3.zero, new[] { new Vector2(0.001f, -0.7f), new Vector2(0.28f, 0f), new Vector2(0.26f, 1.1f), new Vector2(0.001f, 1.9f) }, 5, 1f);
            Mesh mesh = mb.ToMesh("shard", true);
            for (int i = 0; i < count; i++)
            {
                var s = new GameObject("Shard" + i);
                s.transform.SetParent(go.transform, false);
                s.AddComponent<MeshFilter>().sharedMesh = mesh;
                s.AddComponent<MeshRenderer>().sharedMaterial = mat;
                float sc = 0.6f + rng.Value() * 2.4f;
                s.transform.localScale = new Vector3(sc, sc, sc);
                f.shards.Add(new Shard { t = s.transform, radius = radius * (0.35f + rng.Value() * 0.65f), height = Mathf.Lerp(heightMin, heightMax, rng.Value()), speed = (rng.Value() - 0.5f) * 0.2f, phase = rng.Value() * 6.28f, bob = rng.Value() * 6.28f });
            }
            return f;
        }

        void Update()
        {
            float t = Time.time;
            for (int i = 0; i < shards.Count; i++)
            {
                Shard s = shards[i];
                float a = s.phase + t * s.speed;
                s.t.localPosition = new Vector3(Mathf.Cos(a) * s.radius, s.height + Mathf.Sin(t * 0.6f + s.bob) * 0.7f, Mathf.Sin(a) * s.radius);
                s.t.localRotation = Quaternion.Euler(t * 8f * (i % 2 == 0 ? 1 : -1) + s.phase * 20f, t * 12f + s.phase * 40f, 12f * Mathf.Sin(t * 0.3f + s.bob));
            }
        }
    }

    /// <summary>Two huge stone slabs that slide open (ruin door). Opening is triggered by a flag or a cutscene command.</summary>
    public sealed class StoneDoor : MonoBehaviour
    {
        public string openFlag = "ruin_door_open";
        Transform left, right;
        float open;
        bool opening;
        float width = 4.2f;
        RuneDecal glyph;
        Material glow;

        public static StoneDoor Create(StructureKit k, Vector3 baseCenter, float totalWidth, float height, Material stone, Material glowMat)
        {
            var go = new GameObject("StoneDoor");
            go.transform.SetParent(k.root, false);
            go.transform.localPosition = baseCenter;
            var d = go.AddComponent<StoneDoor>();
            d.width = totalWidth * 0.5f;
            d.glow = glowMat;
            d.left = Slab(go.transform, "L", -totalWidth * 0.25f, totalWidth * 0.5f, height, stone, glowMat);
            d.right = Slab(go.transform, "R", totalWidth * 0.25f, totalWidth * 0.5f, height, stone, glowMat);
            return d;
        }

        static Transform Slab(Transform parent, string name, float x, float w, float h, Material stone, Material glow)
        {
            var go = new GameObject("Slab" + name);
            go.layer = GameLayers.Environment;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, 0f, 0f);
            var mb = new MeshBuilder();
            mb.Sub(0);
            mb.Box(new Vector3(0f, h * 0.5f, 0f), new Vector3(w - 0.08f, h, 0.8f), 3f);
            mb.Sub(1);
            // glowing glyph strips
            for (int i = 0; i < 5; i++)
            {
                float y = h * (0.15f + i * 0.17f);
                mb.Box(new Vector3(0f, y, 0.41f), new Vector3(w * 0.55f * (name == "L" ? 1f : 1f), 0.07f, 0.02f), 1f);
                mb.Box(new Vector3((i % 2 == 0 ? -1f : 1f) * w * 0.2f, y + 0.25f, 0.41f), new Vector3(0.07f, 0.35f, 0.02f), 1f);
            }
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("slab", true);
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { stone, glow };
            var bc = go.AddComponent<BoxCollider>();
            bc.size = new Vector3(w - 0.08f, h, 0.8f);
            bc.center = new Vector3(0f, h * 0.5f, 0f);
            return go.transform;
        }

        public void Open()
        {
            opening = true;
            AudioManager.Instance?.Sfx("door_stone", transform.position, 1f);
            Game.Camera?.Shake(0.07f, 3f);
        }

        public bool IsOpen => open >= 0.99f;

        public void SetOpen(bool v)
        {
            opening = v;
            open = v ? 1f : 0f;
            Apply();
        }

        void Update()
        {
            if (!opening && Game.Session != null && Game.Session.flags.Has(openFlag)) opening = true;
            if (opening && open < 1f)
            {
                open = Mathf.Min(1f, open + Time.deltaTime / 6f);
                Apply();
            }
        }

        void Apply()
        {
            float k = Mathx.SmootherStep01(open);
            left.localPosition = new Vector3(-width * 0.5f - k * (width - 0.1f), 0f, 0f);
            right.localPosition = new Vector3(width * 0.5f + k * (width - 0.1f), 0f, 0f);
        }
    }

    /// <summary>The Trắc Linh Thạch: a crystal sphere on a pedestal that rotates and pulses (root test device).</summary>
    public sealed class TestingStone : MonoBehaviour
    {
        Transform crystal;
        Material mat;
        float intensity = 1f, target = 1f;
        Color baseColor = new Color(0.6f, 0.85f, 1f);
        Light light;

        public static TestingStone Create(Transform parent, Vector3 pos, WorldAssets assets)
        {
            var go = new GameObject("TestingStone");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var t = go.AddComponent<TestingStone>();
            var mb = new MeshBuilder();
            mb.Cylinder(Vector3.zero, 0.35f, 1.6f, 1.45f, 8, true, true, 2f);
            mb.Cylinder(new Vector3(0f, 0.35f, 0f), 0.9f, 0.6f, 0.45f, 8, false, true, 2f);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("testing_pedestal", true);
            go.AddComponent<MeshRenderer>().sharedMaterial = assets.matMarble;
            var bc = go.AddComponent<CapsuleCollider>();
            bc.radius = 1.4f; bc.height = 1.3f; bc.center = new Vector3(0f, 0.65f, 0f);
            go.layer = GameLayers.Environment;
            var c = new GameObject("Crystal");
            c.transform.SetParent(go.transform, false);
            c.transform.localPosition = new Vector3(0f, 1.85f, 0f);
            var cb = new MeshBuilder();
            cb.Sphere(Vector3.zero, 0.42f, 14, 10);
            c.AddComponent<MeshFilter>().sharedMesh = cb.ToMesh("testing_crystal", false);
            t.mat = Mats.Glow(new Color(0.6f, 0.85f, 1f), 2.2f);
            c.AddComponent<MeshRenderer>().sharedMaterial = t.mat;
            t.crystal = c.transform;
            var l = new GameObject("Light");
            l.transform.SetParent(c.transform, false);
            t.light = l.AddComponent<Light>();
            t.light.type = LightType.Point;
            t.light.range = 12f;
            t.light.intensity = 5f;
            t.light.color = t.baseColor;
            t.light.shadows = LightShadows.None;
            return t;
        }

        /// <summary>Intensity multiplier (1 normal, 0 dull, >1 flaring) and tint, driven by the root test cutscene.</summary>
        public void SetState(float intensityTarget, Color tint)
        {
            target = intensityTarget;
            baseColor = tint;
        }

        void Update()
        {
            intensity = Mathf.MoveTowards(intensity, target, Time.deltaTime * 2.5f);
            crystal.Rotate(0f, 30f * Time.deltaTime, 0f);
            crystal.localPosition = new Vector3(0f, 1.85f + Mathf.Sin(Time.time * 1.3f) * 0.06f, 0f);
            Color c = baseColor * (0.35f + 0.65f * intensity);
            mat.SetColor("_EmissionColor", baseColor * (2.2f * intensity));
            mat.SetColor("_BaseColor", c * 0.4f);
            light.color = baseColor;
            light.intensity = 5f * intensity;
        }
    }
}
