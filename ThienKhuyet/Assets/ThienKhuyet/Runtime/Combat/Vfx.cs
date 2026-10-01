using System;
using System.Collections.Generic;
using ThienKhuyet.Core;
using ThienKhuyet.Gfx;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThienKhuyet.Combat
{
    /// <summary>Pooled particle effect root: restarts every particle system when activated and returns itself to the pool when done.</summary>
    public sealed class VfxItem : MonoBehaviour
    {
        public ParticleSystem[] systems = new ParticleSystem[0];
        public TrailRenderer[] trails = new TrailRenderer[0];
        public float lifetime = 1f;
        public bool looping;
        public string poolId;

        void OnEnable()
        {
            for (int i = 0; i < trails.Length; i++) if (trails[i] != null) trails[i].Clear();
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] == null) continue;
                systems[i].Clear(true);
                systems[i].Play(true);
            }
        }

        public void Release()
        {
            for (int i = 0; i < systems.Length; i++) if (systems[i] != null) systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var ar = GetComponent<AutoRelease>();
            if (ar != null) ar.enabled = false;
            GameObjectPool pool = Pools.Get("vfx:" + poolId, null);
            if (pool != null) pool.Release(gameObject);
            else Destroy(gameObject);
        }
    }

    /// <summary>Expanding ring / telegraph disc on the ground (shockwaves, AoE warnings, breakthrough rings).</summary>
    public sealed class RingFx : MonoBehaviour
    {
        public enum Mode { Expand, Telegraph, Pulse }

        Transform inner;
        Material mat, innerMat;
        Mode mode;
        float radius, duration, t;
        Color color;
        MeshRenderer outerR, innerR;
        bool unscaled;

        public static RingFx Create(Transform parent)
        {
            var go = new GameObject("RingFx");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<RingFx>();
            fx.Build();
            return fx;
        }

        void Build()
        {
            var mb = new MeshBuilder();
            mb.Plane(new Vector2(2f, 2f), 2f);
            Mesh mesh = mb.ToMesh("ring_quad", false);
            Texture2D ringTex = Vfx.RingTexture;
            mat = Mats.Particle(ringTex, Color.white, true);
            innerMat = Mats.Particle(Vfx.SoftTexture, Color.white, false);
            var o = new GameObject("Ring");
            o.transform.SetParent(transform, false);
            o.AddComponent<MeshFilter>().sharedMesh = mesh;
            outerR = o.AddComponent<MeshRenderer>();
            outerR.sharedMaterial = mat;
            outerR.shadowCastingMode = ShadowCastingMode.Off;
            outerR.receiveShadows = false;
            var i = new GameObject("Fill");
            i.transform.SetParent(transform, false);
            i.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            i.AddComponent<MeshFilter>().sharedMesh = mesh;
            innerR = i.AddComponent<MeshRenderer>();
            innerR.sharedMaterial = innerMat;
            innerR.shadowCastingMode = ShadowCastingMode.Off;
            innerR.receiveShadows = false;
            inner = i.transform;
        }

        public void Setup(Mode m, float radius, Color c, float duration, bool unscaledTime = false)
        {
            mode = m;
            this.radius = radius;
            color = c;
            this.duration = Mathf.Max(0.05f, duration);
            unscaled = unscaledTime;
            t = 0f;
            innerR.enabled = m == Mode.Telegraph;
            Apply(0f);
        }

        void Update()
        {
            t += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            Apply(k);
            if (t >= duration)
            {
                var ar = GetComponent<AutoRelease>();
                GameObjectPool pool = Pools.Get("ring", null);
                if (pool != null) pool.Release(gameObject);
                else Destroy(gameObject);
            }
        }

        void Apply(float k)
        {
            switch (mode)
            {
                case Mode.Expand:
                    {
                        float s = Mathf.Lerp(0.15f, 1f, Mathx.EaseOutCubic(k)) * radius;
                        outerR.transform.localScale = new Vector3(s, 1f, s);
                        Color c = color; c.a = color.a * (1f - k) * (1f - k);
                        mat.SetColor("_BaseColor", c);
                        break;
                    }
                case Mode.Telegraph:
                    {
                        outerR.transform.localScale = new Vector3(radius, 1f, radius);
                        float pulse = 0.65f + 0.35f * Mathf.Sin(t * 18f);
                        Color c = color; c.a = color.a * pulse;
                        mat.SetColor("_BaseColor", c);
                        float s = Mathf.Lerp(0.05f, 1f, k) * radius * 0.96f;
                        inner.localScale = new Vector3(s, 1f, s);
                        Color f = color; f.a = color.a * 0.35f;
                        innerMat.SetColor("_BaseColor", f);
                        break;
                    }
                default:
                    {
                        float s = radius * (1f + 0.1f * Mathf.Sin(t * 6f));
                        outerR.transform.localScale = new Vector3(s, 1f, s);
                        Color c = color; c.a = color.a * (1f - k);
                        mat.SetColor("_BaseColor", c);
                        break;
                    }
            }
        }
    }

    /// <summary>
    /// Code-built particle effects (no prefabs). Each id builds one pooled root with child particle systems;
    /// <see cref="Play"/> positions and activates it. Effects are deliberately light so they never hide the combat.
    /// </summary>
    public static class Vfx
    {
        static readonly Dictionary<string, Func<GameObject>> makers = new Dictionary<string, Func<GameObject>>();
        static Texture2D soft, ring, crescent, streak, star, smoke, rune;
        static Mesh crescentMesh, flatDiscMesh, bladeMesh, quadMesh;
        static Transform root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            makers.Clear();
            soft = ring = crescent = streak = star = smoke = rune = null;
            crescentMesh = flatDiscMesh = bladeMesh = quadMesh = null;
            root = null;
        }

        public static Texture2D SoftTexture => soft != null ? soft : (soft = ProcTex.SoftCircle(64, 0.1f));
        public static Texture2D RingTexture => ring != null ? ring : (ring = ProcTex.Ring(128, 0.88f, 0.07f));
        public static Texture2D CrescentTexture => crescent != null ? crescent : (crescent = ProcTex.Crescent(128));
        public static Texture2D StreakTexture => streak != null ? streak : (streak = ProcTex.Streak(64));
        public static Texture2D StarTexture => star != null ? star : (star = ProcTex.Star(64));
        public static Texture2D SmokeTexture => smoke != null ? smoke : (smoke = ProcTex.Smoke(64, 3));
        public static Texture2D RuneTexture => rune != null ? rune : (rune = ProcTex.Rune(256, 7, 6));

        static Transform Root
        {
            get
            {
                if (root == null)
                {
                    var go = new GameObject("~Vfx");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    root = go.transform;
                }
                return root;
            }
        }

        // ------------------------------------------------------------------ public API
        public static GameObject Play(string id, Vector3 pos, Quaternion rot, float scale = 1f)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureMakers();
            if (!makers.TryGetValue(id, out Func<GameObject> make))
            {
                if (!makers.TryGetValue("hit_spark", out make)) return null;
                id = "hit_spark";
            }
            string poolId = "vfx:" + id;
            GameObjectPool pool = Pools.Get(poolId, () =>
            {
                GameObject g = make();
                g.name = id;
                var item = g.GetComponent<VfxItem>();
                item.poolId = id;
                g.AddComponent<AutoRelease>().enabled = false;
                return g;
            });
            GameObject go = pool.Get(pos, rot);
            go.transform.localScale = Vector3.one * scale;
            var vi = go.GetComponent<VfxItem>();
            var ar = go.GetComponent<AutoRelease>();
            if (!vi.looping) { ar.Arm(pool, vi.lifetime + 0.3f); }
            else ar.enabled = false;
            return go;
        }

        public static GameObject Play(string id, Vector3 pos)
        {
            return Play(id, pos, Quaternion.identity, 1f);
        }

        /// <summary>Looping effects (auras, projectile glows) must be stopped explicitly.</summary>
        public static void Stop(GameObject fx)
        {
            if (fx == null) return;
            var vi = fx.GetComponent<VfxItem>();
            if (vi != null) vi.Release();
        }

        public static void Ring(Vector3 pos, float radius, Color color, float duration, RingFx.Mode mode = RingFx.Mode.Expand, bool unscaled = false)
        {
            GameObjectPool pool = Pools.Get("ring", () => RingFx.Create(Root).gameObject);
            GameObject go = pool.Get(pos + Vector3.up * 0.06f, Quaternion.identity);
            go.GetComponent<RingFx>().Setup(mode, radius, color, duration, unscaled);
        }

        public static void Shutdown()
        {
            makers.Clear();
            Pools.Clear();
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
            root = null;
        }

        // ------------------------------------------------------------------ meshes
        static Mesh CrescentMesh()
        {
            if (crescentMesh != null) return crescentMesh;
            var mb = new MeshBuilder();
            const int n = 16;
            var pts = new List<Vector3>();
            float R = 1f;
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.Lerp(-1.25f, 1.25f, (float)i / n);
                pts.Add(new Vector3(Mathf.Sin(a) * R, Mathf.Cos(a) * R - R * 0.45f, 0f));
            }
            for (int i = 0; i < n; i++)
            {
                float a0 = Mathf.Lerp(-1.25f, 1.25f, (float)i / n), a1 = Mathf.Lerp(-1.25f, 1.25f, (float)(i + 1) / n);
                float w0 = 0.34f * Mathf.Cos(a0 * 1.25f), w1 = 0.34f * Mathf.Cos(a1 * 1.25f);
                Vector3 o0 = new Vector3(Mathf.Sin(a0), Mathf.Cos(a0), 0f), o1 = new Vector3(Mathf.Sin(a1), Mathf.Cos(a1), 0f);
                Vector3 p0 = pts[i], p1 = pts[i + 1];
                mb.Quad(p0 - o0 * (w0 * 0.15f), p0 + o0 * w0, p1 + o1 * w1, p1 - o1 * (w1 * 0.15f), Vector3.back,
                    new Vector2((float)i / n, 0f), new Vector2((float)i / n, 1f), new Vector2((float)(i + 1) / n, 1f), new Vector2((float)(i + 1) / n, 0f));
            }
            crescentMesh = mb.ToMesh("vfx_crescent", false);
            return crescentMesh;
        }

        static Mesh QuadMesh()
        {
            if (quadMesh != null) return quadMesh;
            var mb = new MeshBuilder();
            mb.Quad(new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), Vector3.back,
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));
            quadMesh = mb.ToMesh("vfx_quad", false);
            return quadMesh;
        }

        static Mesh FlatDiscMesh()
        {
            if (flatDiscMesh != null) return flatDiscMesh;
            var mb = new MeshBuilder();
            mb.Plane(new Vector2(2f, 2f), 2f);
            flatDiscMesh = mb.ToMesh("vfx_flat", false);
            return flatDiscMesh;
        }

        static Mesh BladeMesh()
        {
            if (bladeMesh != null) return bladeMesh;
            var mb = new MeshBuilder();
            mb.Push(Vector3.zero, Quaternion.Euler(-90f, 0f, 0f), new Vector3(0.05f, 1f, 0.012f));
            mb.Cylinder(Vector3.zero, 0.8f, 1f, 0.9f, 4, true, false);
            mb.Cylinder(new Vector3(0f, 0.8f, 0f), 0.2f, 0.9f, 0f, 4, false, false);
            mb.Pop();
            bladeMesh = mb.ToMesh("vfx_blade", false);
            return bladeMesh;
        }

        // ------------------------------------------------------------------ particle helpers
        struct P
        {
            public float lifetime, speed, size, gravity, rate, drag, startRot;
            public Color color;
            public int burst;
            public ParticleSystemShapeType shape;
            public float shapeAngle, shapeRadius;
            public bool additive, shrink, grow, loop, local, stretch, fadeIn;
            public Texture2D tex;
            public float duration;
            public Mesh mesh;
            public float stretchLen;
            public bool alignLocal;
            public float sizeVar;
        }

        static P Defaults()
        {
            return new P
            {
                lifetime = 0.4f, speed = 4f, size = 0.2f, gravity = 0f, burst = 10, color = Color.white, shape = ParticleSystemShapeType.Sphere,
                shapeAngle = 25f, shapeRadius = 0.1f, additive = true, shrink = true, duration = 0.5f, tex = null, sizeVar = 0.4f
            };
        }

        static ParticleSystem Make(Transform parent, string name, P p)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = p.loop;
            main.duration = Mathf.Max(0.1f, p.duration);
            main.startLifetime = new ParticleSystem.MinMaxCurve(p.lifetime * 0.75f, p.lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(p.speed * 0.6f, p.speed);
            main.startSize = new ParticleSystem.MinMaxCurve(p.size * (1f - p.sizeVar), p.size);
            main.startColor = p.color;
            main.gravityModifier = p.gravity;
            main.simulationSpace = p.local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            main.startRotation = p.startRot * Mathf.Deg2Rad;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = p.loop ? p.rate : 0f;
            if (p.burst > 0) em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)p.burst) });

            var sh = ps.shape;
            sh.enabled = p.shape != ParticleSystemShapeType.Sprite;
            sh.shapeType = p.shape;
            sh.angle = p.shapeAngle;
            sh.radius = p.shapeRadius;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            float a0 = p.fadeIn ? 0f : 1f;
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(a0, 0f), new GradientAlphaKey(1f, p.fadeIn ? 0.2f : 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            if (p.shrink || p.grow)
            {
                var sz = ps.sizeOverLifetime;
                sz.enabled = true;
                sz.size = new ParticleSystem.MinMaxCurve(1f, p.grow ? AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.25f) : AnimationCurve.Linear(0f, 1f, 1f, 0.05f));
            }
            if (p.drag > 0f)
            {
                var lim = ps.limitVelocityOverLifetime;
                lim.enabled = true;
                lim.dampen = Mathf.Clamp01(p.drag);
                lim.limit = 0.1f;
            }

            var r = go.GetComponent<ParticleSystemRenderer>();
            Texture2D tex = p.tex != null ? p.tex : SoftTexture;
            r.sharedMaterial = Mats.Particle(tex, Color.white, p.additive);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (p.mesh != null)
            {
                r.renderMode = ParticleSystemRenderMode.Mesh;
                r.mesh = p.mesh;
                r.alignment = p.alignLocal ? ParticleSystemRenderSpace.Local : ParticleSystemRenderSpace.View;
            }
            else if (p.stretch)
            {
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.lengthScale = p.stretchLen > 0f ? p.stretchLen : 2.5f;
                r.velocityScale = 0.12f;
            }
            else r.renderMode = ParticleSystemRenderMode.Billboard;
            return ps;
        }

        static GameObject Root_(string name, float lifetime, bool looping, params ParticleSystem[] systems)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            foreach (var s in systems) s.transform.SetParent(go.transform, false);
            var vi = go.AddComponent<VfxItem>();
            vi.systems = systems;
            vi.lifetime = lifetime;
            vi.looping = looping;
            return go;
        }

        static ParticleSystem Sparks(string name, Color c, int count, float speed, float size, float life, float gravity = 2f, float spread = 180f, bool stretch = true)
        {
            P p = Defaults();
            p.color = c; p.burst = count; p.speed = speed; p.size = size; p.lifetime = life; p.gravity = gravity;
            p.shape = ParticleSystemShapeType.Cone; p.shapeAngle = spread * 0.5f; p.shapeRadius = 0.05f; p.tex = StreakTexture; p.stretch = stretch;
            p.stretchLen = 3f; p.drag = 0.2f;
            return Make(null, name, p);
        }

        static ParticleSystem Flash(string name, Color c, float size, float life)
        {
            P p = Defaults();
            p.color = c; p.burst = 1; p.speed = 0f; p.size = size; p.lifetime = life; p.shape = ParticleSystemShapeType.Sphere; p.shapeRadius = 0.001f; p.tex = StarTexture;
            p.shrink = false; p.grow = true; p.sizeVar = 0f;
            return Make(null, name, p);
        }

        static ParticleSystem Puff(string name, Color c, int count, float speed, float size, float life, float gravity = -0.1f, bool additive = false)
        {
            P p = Defaults();
            p.color = c; p.burst = count; p.speed = speed; p.size = size; p.lifetime = life; p.gravity = gravity; p.additive = additive;
            p.tex = SmokeTexture; p.shrink = false; p.grow = true; p.shape = ParticleSystemShapeType.Hemisphere; p.shapeRadius = 0.2f; p.drag = 0.4f;
            return Make(null, name, p);
        }

        static ParticleSystem Motes(string name, Color c, float rate, float size, float life, float radius, float rise, bool loop = true, float duration = 2f, int burst = 0)
        {
            P p = Defaults();
            p.color = c; p.burst = burst; p.rate = rate; p.speed = 0.05f; p.size = size; p.lifetime = life; p.loop = loop; p.duration = duration;
            p.shape = ParticleSystemShapeType.Circle; p.shapeRadius = radius; p.tex = SoftTexture; p.fadeIn = true; p.gravity = -rise; p.local = false;
            return Make(null, name, p);
        }

        static ParticleSystem MeshBurst(string name, Mesh mesh, Texture2D tex, Color c, float size, float life, bool grow, bool alignLocal = true, float startRot = 0f)
        {
            P p = Defaults();
            p.color = c; p.burst = 1; p.speed = 0f; p.size = size; p.lifetime = life; p.shape = ParticleSystemShapeType.Sphere; p.shapeRadius = 0.001f;
            p.mesh = mesh; p.tex = tex; p.shrink = false; p.grow = grow; p.sizeVar = 0f; p.local = true; p.alignLocal = alignLocal; p.startRot = startRot;
            return Make(null, name, p);
        }

        // ------------------------------------------------------------------ effect catalogue
        static void EnsureMakers()
        {
            if (makers.Count > 0) return;
            Color warm = new Color(1f, 0.82f, 0.5f), white = new Color(1f, 1f, 1f), cyan = new Color(0.5f, 0.9f, 1f), gold = new Color(1f, 0.85f, 0.4f);

            makers["hit_spark"] = () => Root_("hit_spark", 0.5f, false, Sparks("s", warm, 10, 6f, 0.07f, 0.25f), Flash("f", warm, 0.5f, 0.12f));
            makers["impact"] = () => Root_("impact", 0.6f, false, Sparks("s", new Color(1f, 0.9f, 0.7f), 8, 5f, 0.08f, 0.3f), Flash("f", white, 0.8f, 0.15f), Puff("p", new Color(0.8f, 0.75f, 0.65f, 0.4f), 4, 1.2f, 0.5f, 0.5f));
            makers["block_spark"] = () => Root_("block_spark", 0.5f, false, Sparks("s", new Color(1f, 0.95f, 0.6f), 16, 8f, 0.09f, 0.3f, 1f, 140f), Flash("f", new Color(1f, 1f, 0.8f), 0.9f, 0.14f));
            makers["parry_flash"] = () => Root_("parry_flash", 0.7f, false, Sparks("s", new Color(0.6f, 0.95f, 1f), 24, 10f, 0.1f, 0.4f, 0f, 360f), Flash("f", new Color(0.7f, 1f, 1f), 2.0f, 0.22f), MeshBurst("r", FlatDiscMesh(), RingTexture, new Color(0.6f, 0.95f, 1f, 0.9f), 1.2f, 0.35f, true));
            makers["slash"] = () => Root_("slash", 0.4f, false, MeshBurst("c", CrescentMesh(), CrescentTexture, new Color(0.85f, 0.95f, 1f, 0.85f), 1.35f, 0.17f, true), Sparks("s", warm, 4, 4f, 0.05f, 0.2f));
            makers["slash_big"] = () => Root_("slash_big", 0.5f, false, MeshBurst("c", CrescentMesh(), CrescentTexture, new Color(1f, 0.95f, 0.8f, 0.95f), 2.0f, 0.24f, true), Sparks("s", warm, 10, 6f, 0.06f, 0.3f));
            makers["thrust"] = () => Root_("thrust", 0.4f, false, MeshBurst("l", QuadMesh(), StreakTexture, new Color(0.9f, 0.97f, 1f, 0.85f), 2.2f, 0.16f, false, true), Sparks("s", warm, 5, 4f, 0.05f, 0.2f));
            makers["shockwave"] = () => Root_("shockwave", 0.9f, false, MeshBurst("r", FlatDiscMesh(), RingTexture, new Color(1f, 0.9f, 0.7f, 0.9f), 3.2f, 0.5f, true), Puff("d", new Color(0.7f, 0.65f, 0.55f, 0.5f), 10, 3f, 0.8f, 0.7f), Flash("f", warm, 1.4f, 0.14f));
            makers["dust"] = () => Root_("dust", 0.8f, false, Puff("d", new Color(0.72f, 0.66f, 0.56f, 0.45f), 6, 1.6f, 0.5f, 0.55f, -0.05f));
            makers["dash_trail"] = () => Root_("dash_trail", 0.6f, false, Sparks("s", new Color(0.8f, 0.95f, 1f, 0.8f), 12, 3f, 0.14f, 0.35f, 0f, 60f), Puff("p", new Color(0.8f, 0.9f, 1f, 0.35f), 5, 0.8f, 0.6f, 0.4f, 0f, true));
            makers["heal_glow"] = () => Root_("heal_glow", 1.6f, false, Motes("m", new Color(0.55f, 1f, 0.6f), 28f, 0.14f, 1.1f, 0.6f, 1.2f, false, 1f), Flash("f", new Color(0.6f, 1f, 0.7f, 0.6f), 1.6f, 0.4f));
            makers["aura_gold"] = () => Root_("aura_gold", 1.4f, false, Motes("m", new Color(1f, 0.82f, 0.4f), 36f, 0.12f, 1.0f, 0.6f, 1.4f, false, 1.1f), Flash("f", new Color(1f, 0.9f, 0.5f, 0.6f), 1.8f, 0.4f));
            makers["aura_blue"] = () => Root_("aura_blue", 1.6f, false, Motes("m", new Color(0.5f, 0.8f, 1f), 36f, 0.12f, 1.1f, 0.6f, 1.2f, false, 1.2f), Flash("f", new Color(0.55f, 0.8f, 1f, 0.6f), 1.8f, 0.4f));
            makers["meditate_aura"] = () => Root_("meditate_aura", 1f, true, Motes("m", new Color(0.55f, 0.85f, 1f), 14f, 0.1f, 2.4f, 0.9f, 0.5f, true, 4f));
            makers["qi_gather"] = () => Root_("qi_gather", 1f, true, Motes("m", new Color(0.75f, 0.95f, 1f), 40f, 0.09f, 1.6f, 3.2f, -0.5f, true, 4f));

            makers["qi_bolt"] = () => Root_("qi_bolt", 1f, true, ProjectileGlow("g", new Color(0.5f, 0.9f, 1f), 0.5f), ProjectileTrail("t", new Color(0.5f, 0.85f, 1f)));
            makers["sword_wave"] = () => Root_("sword_wave", 1f, true, ProjectileCrescent("c", new Color(0.7f, 0.95f, 1f), 2.2f), ProjectileTrail("t", new Color(0.7f, 0.95f, 1f)));
            makers["fireball"] = () => Root_("fireball", 1f, true, ProjectileGlow("g", new Color(1f, 0.55f, 0.2f), 0.9f), ProjectileTrail("t", new Color(1f, 0.5f, 0.15f), 0.35f), ProjectileSmoke("s"));
            makers["shadow_bolt"] = () => Root_("shadow_bolt", 1f, true, ProjectileGlow("g", new Color(0.85f, 0.15f, 0.35f), 0.6f), ProjectileTrail("t", new Color(0.6f, 0.1f, 0.3f)));
            makers["arrow"] = () => Root_("arrow", 1f, true, ProjectileTrail("t", new Color(1f, 0.95f, 0.8f), 0.12f), ProjectileGlow("g", new Color(1f, 0.95f, 0.8f, 0.5f), 0.25f));
            makers["explosion_fire"] = () => Root_("explosion_fire", 1.1f, false, Flash("f", new Color(1f, 0.7f, 0.3f), 3.4f, 0.25f), Sparks("s", new Color(1f, 0.6f, 0.2f), 22, 8f, 0.12f, 0.5f, 3f, 360f), Puff("p", new Color(0.35f, 0.3f, 0.28f, 0.5f), 8, 3f, 1.2f, 0.9f, -0.2f), MeshBurst("r", FlatDiscMesh(), RingTexture, new Color(1f, 0.7f, 0.3f, 0.9f), 2.4f, 0.4f, true));
            makers["shadow_burst"] = () => Root_("shadow_burst", 0.8f, false, Flash("f", new Color(0.9f, 0.2f, 0.4f), 2.2f, 0.2f), Sparks("s", new Color(0.85f, 0.2f, 0.4f), 14, 6f, 0.1f, 0.4f, 1f, 360f));
            makers["qi_burst"] = () => Root_("qi_burst", 0.8f, false, Flash("f", new Color(0.6f, 0.95f, 1f), 2.2f, 0.2f), Sparks("s", new Color(0.6f, 0.95f, 1f), 16, 7f, 0.1f, 0.4f, 1f, 360f), MeshBurst("r", FlatDiscMesh(), RingTexture, new Color(0.6f, 0.95f, 1f, 0.9f), 1.8f, 0.35f, true));
            makers["frost_nova"] = () => Root_("frost_nova", 1.2f, false, MeshBurst("r", FlatDiscMesh(), RingTexture, new Color(0.65f, 0.9f, 1f, 0.95f), 6.5f, 0.55f, true), Sparks("s", new Color(0.8f, 0.95f, 1f), 40, 9f, 0.1f, 0.6f, 0.5f, 90f), Puff("p", new Color(0.8f, 0.95f, 1f, 0.5f), 12, 4f, 1.4f, 0.9f, -0.1f, true), Flash("f", new Color(0.75f, 0.95f, 1f), 3f, 0.3f));
            makers["lightning"] = () => Root_("lightning", 0.9f, false, LightningBolt("b"), Flash("f", new Color(0.85f, 0.8f, 1f), 4f, 0.22f), Sparks("s", new Color(0.9f, 0.85f, 1f), 24, 9f, 0.1f, 0.4f, 2f, 360f), MeshBurst("r", FlatDiscMesh(), RingTexture, new Color(0.85f, 0.8f, 1f, 0.9f), 3.2f, 0.4f, true));
            makers["sword_rain"] = () => Root_("sword_rain", 1.4f, false, SwordRain("r"));
            makers["breakthrough_ring"] = () => Root_("breakthrough_ring", 2.2f, false, MeshBurst("r", FlatDiscMesh(), RuneTexture, new Color(0.6f, 0.9f, 1f, 0.9f), 3.4f, 1.8f, true), Motes("m", new Color(0.8f, 0.95f, 1f), 0f, 0.1f, 1.8f, 1.2f, 0.6f, false, 1f, 40));
            makers["loot_sparkle"] = () => Root_("loot_sparkle", 1f, true, Motes("m", new Color(1f, 0.92f, 0.6f), 6f, 0.07f, 1.2f, 0.25f, 0.3f, true, 2f));
            makers["campfire"] = () => Root_("campfire", 1f, true, FlameLoop("f"), Motes("m", new Color(1f, 0.6f, 0.2f), 5f, 0.05f, 1.5f, 0.18f, 1.0f, true, 2f));
            makers["torch"] = () => Root_("torch", 1f, true, FlameLoop("f", 0.6f));
            makers["mist"] = () => Root_("mist", 1f, true, MistLoop("m"));
        }

        static ParticleSystem ProjectileGlow(string name, Color c, float size)
        {
            P p = Defaults();
            p.color = c; p.loop = true; p.rate = 20f; p.burst = 0; p.speed = 0f; p.size = size; p.lifetime = 0.25f; p.local = true;
            p.shape = ParticleSystemShapeType.Sphere; p.shapeRadius = 0.01f; p.shrink = false; p.sizeVar = 0f; p.tex = SoftTexture; p.duration = 1f;
            return Make(null, name, p);
        }

        static ParticleSystem ProjectileTrail(string name, Color c, float size = 0.22f)
        {
            P p = Defaults();
            p.color = c; p.loop = true; p.rate = 55f; p.burst = 0; p.speed = 0.2f; p.size = size; p.lifetime = 0.4f; p.local = false;
            p.shape = ParticleSystemShapeType.Sphere; p.shapeRadius = 0.05f; p.tex = SoftTexture; p.duration = 1f;
            return Make(null, name, p);
        }

        static ParticleSystem ProjectileSmoke(string name)
        {
            P p = Defaults();
            p.color = new Color(0.3f, 0.25f, 0.22f, 0.4f); p.loop = true; p.rate = 22f; p.burst = 0; p.speed = 0.3f; p.size = 0.45f; p.lifetime = 0.7f; p.local = false;
            p.additive = false; p.tex = SmokeTexture; p.shrink = false; p.grow = true; p.shape = ParticleSystemShapeType.Sphere; p.shapeRadius = 0.08f; p.duration = 1f;
            return Make(null, name, p);
        }

        static ParticleSystem ProjectileCrescent(string name, Color c, float size)
        {
            P p = Defaults();
            p.color = c; p.loop = true; p.rate = 30f; p.burst = 0; p.speed = 0f; p.size = size; p.lifetime = 0.12f; p.local = true;
            p.mesh = CrescentMesh(); p.tex = CrescentTexture; p.shape = ParticleSystemShapeType.Sphere; p.shapeRadius = 0.001f; p.shrink = false; p.sizeVar = 0f; p.alignLocal = true; p.duration = 1f;
            return Make(null, name, p);
        }

        static ParticleSystem LightningBolt(string name)
        {
            P p = Defaults();
            p.color = new Color(0.9f, 0.88f, 1f); p.burst = 6; p.speed = 0f; p.size = 0.35f; p.lifetime = 0.2f; p.local = true;
            p.shape = ParticleSystemShapeType.Box; p.shapeRadius = 0.2f; p.tex = StreakTexture; p.stretch = true; p.stretchLen = 40f; p.shrink = false; p.duration = 0.3f;
            ParticleSystem ps = Make(null, name, p);
            var sh = ps.shape;
            sh.scale = new Vector3(0.6f, 0.01f, 0.6f);
            sh.position = new Vector3(0f, 6f, 0f);
            sh.rotation = new Vector3(0f, 0f, 0f);
            var main = ps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(40f, 55f);
            main.startRotation3D = false;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.y = new ParticleSystem.MinMaxCurve(-70f, -90f);
            return ps;
        }

        static ParticleSystem SwordRain(string name)
        {
            P p = Defaults();
            p.color = new Color(0.8f, 0.95f, 1f); p.burst = 14; p.speed = 0f; p.size = 1.1f; p.lifetime = 0.55f; p.local = true;
            p.mesh = BladeMesh(); p.tex = StreakTexture; p.shape = ParticleSystemShapeType.Circle; p.shapeRadius = 4f; p.shrink = false; p.alignLocal = false; p.duration = 1f;
            ParticleSystem ps = Make(null, name, p);
            var sh = ps.shape;
            sh.position = new Vector3(0f, 9f, 0f);
            sh.rotation = new Vector3(90f, 0f, 0f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.y = new ParticleSystem.MinMaxCurve(-18f, -24f);
            var em = ps.emission;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 5), new ParticleSystem.Burst(0.12f, 5), new ParticleSystem.Burst(0.25f, 4) });
            return ps;
        }

        static ParticleSystem FlameLoop(string name, float scale = 1f)
        {
            P p = Defaults();
            p.color = new Color(1f, 0.55f, 0.18f); p.loop = true; p.rate = 26f; p.burst = 0; p.speed = 0.6f; p.size = 0.55f * scale; p.lifetime = 0.7f; p.gravity = -0.35f;
            p.shape = ParticleSystemShapeType.Circle; p.shapeRadius = 0.12f * scale; p.tex = SoftTexture; p.local = true; p.duration = 1f;
            return Make(null, name, p);
        }

        static ParticleSystem MistLoop(string name)
        {
            P p = Defaults();
            p.color = new Color(0.85f, 0.9f, 0.95f, 0.18f); p.loop = true; p.rate = 3f; p.burst = 0; p.speed = 0.15f; p.size = 6f; p.lifetime = 9f;
            p.additive = false; p.tex = SmokeTexture; p.shrink = false; p.grow = true; p.shape = ParticleSystemShapeType.Box; p.shapeRadius = 8f; p.local = false; p.duration = 10f; p.fadeIn = true;
            return Make(null, name, p);
        }
    }
}
