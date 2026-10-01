using ThienKhuyet.Core;
using ThienKhuyet.Gfx;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThienKhuyet.World
{
    /// <summary>
    /// Day/night cycle: sun and moon lights, procedural skybox, gradient ambient, exponential fog and a star dome.
    /// Cinematics can freeze time and override the mood through <see cref="Override"/>.
    /// </summary>
    public sealed class EnvironmentController : MonoBehaviour
    {
        public const float DayLengthSeconds = 1320f;   // 22 real minutes per in-game day

        Light sun, moon;
        Material sky;
        Transform starDome;
        Material starMat;
        Camera cam;

        public float TimeOfDay = 7.5f;
        public bool frozen;
        public float fogBase = 0.0011f;

        // cinematic overrides
        bool overrideOn;
        float overrideFogMul = 1f, overrideSunMul = 1f, overrideAmbientMul = 1f;
        Color overrideTint = Color.white;
        float overrideTintW;

        public bool IsNight => TimeOfDay < 5.3f || TimeOfDay > 19.2f;
        public float SunElevation01 { get; private set; }
        public Light Sun => sun;

        public static EnvironmentController Create(Transform parent, Camera camera)
        {
            var go = new GameObject("Environment");
            go.transform.SetParent(parent, false);
            var env = go.AddComponent<EnvironmentController>();
            env.cam = camera;
            env.Build();
            return env;
        }

        void Build()
        {
            // reuse the scene's directional light if there is one, otherwise create it
            Light existing = null;
            foreach (Light l in FindObjectsByType<Light>())
                if (l.type == LightType.Directional) { existing = l; break; }
            if (existing != null) sun = existing;
            else sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.95f;
            sun.transform.SetParent(transform, true);
            sun.color = new Color(1f, 0.96f, 0.88f);
            RenderSettings.sun = sun;

            moon = new GameObject("Moon").AddComponent<Light>();
            moon.transform.SetParent(transform, false);
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.65f, 1f);
            moon.shadows = LightShadows.None;
            moon.intensity = 0f;

            Shader sh = Shader.Find("Skybox/Procedural");
            if (sh != null)
            {
                sky = new Material(sh);
                RenderSettings.skybox = sky;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.8f;

            // star dome
            var dome = new GameObject("StarDome");
            dome.transform.SetParent(transform, false);
            starDome = dome.transform;
            var mb = new MeshBuilder();
            mb.Sphere(Vector3.zero, 900f, 24, 16, 100f);
            dome.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("stars", false);
            starMat = Mats.Unlit(new Color(1f, 1f, 1f, 0f), ProcTex.Stars(1024, 512, 3), Surface.Additive, true);
            starMat.SetFloat("_ZWrite", 0f);
            var mr = dome.AddComponent<MeshRenderer>();
            mr.sharedMaterial = starMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            Apply();
        }

        /// <summary>Cinematic mood: fog/sun/ambient multipliers and a colour tint of the light.</summary>
        public void Override(float fogMul, float sunMul, float ambientMul, Color tint, float tintWeight)
        {
            overrideOn = true;
            overrideFogMul = fogMul;
            overrideSunMul = sunMul;
            overrideAmbientMul = ambientMul;
            overrideTint = tint;
            overrideTintW = tintWeight;
        }

        public void ClearOverride()
        {
            overrideOn = false;
            overrideFogMul = overrideSunMul = overrideAmbientMul = 1f;
            overrideTintW = 0f;
        }

        void Update()
        {
            if (!frozen && Game.Mode != GameMode.MainMenu && Game.Mode != GameMode.Loading)
            {
                TimeOfDay += Time.deltaTime * 24f / DayLengthSeconds;
                if (TimeOfDay >= 24f)
                {
                    TimeOfDay -= 24f;
                    if (Game.Session != null) Game.Session.day++;
                    Game.Manager?.OnNewDay();
                }
                if (Game.Session != null) Game.Session.timeOfDay = TimeOfDay;
            }
            Apply();
            if (cam != null && starDome != null) starDome.position = cam.transform.position;
        }

        public void SetTime(float t)
        {
            TimeOfDay = Mathf.Repeat(t, 24f);
            Apply();
        }

        static Color Lerp3(Color night, Color dawn, Color day, float e)
        {
            // e: -1 (deep night) .. 0 (horizon) .. 1 (noon)
            if (e < 0f) return Color.Lerp(dawn, night, Mathf.Clamp01(-e * 3.5f));
            return Color.Lerp(dawn, day, Mathf.Clamp01(e * 2.2f));
        }

        void Apply()
        {
            float t = TimeOfDay;
            float ang = (t - 6f) / 24f * 360f;               // 0 at 06:00 (east horizon) .. 90 noon .. 180 at 18:00
            float elev = Mathf.Sin(ang * Mathf.Deg2Rad);      // -1..1
            SunElevation01 = Mathf.Clamp01(elev);
            sun.transform.rotation = Quaternion.Euler(ang, -38f, 0f);
            moon.transform.rotation = Quaternion.Euler(ang + 180f, -38f, 0f);

            float dayK = Mathf.Clamp01(elev * 2.6f + 0.08f);
            Color warm = new Color(1f, 0.62f, 0.38f), white = new Color(1f, 0.96f, 0.88f);
            sun.color = Color.Lerp(warm, white, Mathf.Clamp01(elev * 2.2f));
            sun.intensity = Mathf.Lerp(0f, 2.1f, dayK) * overrideSunMul;
            sun.enabled = elev > -0.12f;
            moon.intensity = Mathf.Clamp01(-elev * 2f) * 0.22f * overrideSunMul;
            moon.enabled = elev < 0.05f;

            Color skyDay = new Color(0.46f, 0.64f, 0.88f), skyDawn = new Color(0.95f, 0.55f, 0.4f), skyNight = new Color(0.06f, 0.08f, 0.18f);
            Color tint = Lerp3(skyNight, skyDawn, skyDay, elev);
            Color ambSky = Lerp3(new Color(0.05f, 0.07f, 0.16f), new Color(0.5f, 0.36f, 0.38f), new Color(0.45f, 0.55f, 0.7f), elev);
            Color ambEq = Lerp3(new Color(0.04f, 0.05f, 0.1f), new Color(0.38f, 0.28f, 0.26f), new Color(0.36f, 0.4f, 0.42f), elev);
            Color ambGr = Lerp3(new Color(0.02f, 0.025f, 0.04f), new Color(0.16f, 0.12f, 0.1f), new Color(0.2f, 0.18f, 0.14f), elev);
            RenderSettings.ambientSkyColor = ambSky * overrideAmbientMul;
            RenderSettings.ambientEquatorColor = ambEq * overrideAmbientMul;
            RenderSettings.ambientGroundColor = ambGr * overrideAmbientMul;

            Color fog = Lerp3(new Color(0.03f, 0.05f, 0.1f), new Color(0.72f, 0.5f, 0.45f), new Color(0.62f, 0.72f, 0.82f), elev);
            if (overrideTintW > 0f)
            {
                fog = Color.Lerp(fog, overrideTint, overrideTintW);
                sun.color = Color.Lerp(sun.color, overrideTint, overrideTintW * 0.6f);
            }
            RenderSettings.fogColor = fog;
            float dawnFog = 1f + 1.2f * Mathf.Exp(-Mathf.Pow((t - 6.3f) / 1.1f, 2f)) + 0.8f * Mathf.Exp(-Mathf.Pow((t - 18.5f) / 1.1f, 2f));
            RenderSettings.fogDensity = fogBase * dawnFog * overrideFogMul;

            if (sky != null)
            {
                sky.SetFloat("_SunSize", 0.045f);
                sky.SetFloat("_SunSizeConvergence", 6f);
                sky.SetFloat("_AtmosphereThickness", Mathf.Lerp(0.55f, 1.1f, Mathf.Clamp01(elev + 0.3f)));
                sky.SetColor("_SkyTint", tint);
                sky.SetColor("_GroundColor", Color.Lerp(new Color(0.08f, 0.1f, 0.1f), new Color(0.35f, 0.38f, 0.34f), dayK));
                sky.SetFloat("_Exposure", Mathf.Lerp(0.12f, 1.25f, dayK) * (overrideOn ? overrideSunMul * 0.6f + 0.4f : 1f));
            }
            float stars = Mathf.Clamp01(-elev * 3f + 0.2f);
            if (starMat != null) starMat.SetColor("_BaseColor", new Color(1f, 1f, 1f, stars * 0.9f));
            RenderSettings.reflectionIntensity = Mathf.Lerp(0.35f, 0.9f, dayK);
        }
    }
}
