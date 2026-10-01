using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThienKhuyet.Gfx
{
    public enum Surface { Opaque = 0, Cutout, Alpha, Additive }

    /// <summary>
    /// Material factory for the Universal Render Pipeline (Lit/Unlit/Particles). Everything is built from code and cached.
    /// Shader names are verified against URP 17.6; a missing shader is logged loudly instead of silently rendering pink.
    /// </summary>
    public static class Mats
    {
        const string LitName = "Universal Render Pipeline/Lit";
        const string UnlitName = "Universal Render Pipeline/Unlit";
        const string ParticleUnlitName = "Universal Render Pipeline/Particles/Unlit";
        const string TerrainLitName = "Universal Render Pipeline/Terrain/Lit";

        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static Shader lit, unlit, particleUnlit, terrainLit;

        /// <summary>Shaders that must be present in player builds (added to Always Included Shaders by the editor setup).</summary>
        public static readonly string[] RequiredShaderNames = { LitName, UnlitName, ParticleUnlitName, TerrainLitName, "Skybox/Procedural", "UI/Default" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cache.Clear();
            lit = unlit = particleUnlit = terrainLit = null;
        }

        static Shader Find(string name, ref Shader field)
        {
            if (field != null) return field;
            field = Shader.Find(name);
            if (field == null) Debug.LogError("[Mats] Shader not found: " + name + " (is the Universal Render Pipeline package installed and assigned?)");
            return field;
        }

        public static Shader LitShader => Find(LitName, ref lit);
        public static Shader UnlitShader => Find(UnlitName, ref unlit);
        public static Shader ParticleShader => Find(ParticleUnlitName, ref particleUnlit);
        public static Shader TerrainShader => Find(TerrainLitName, ref terrainLit);

        public static Material Cached(string key, System.Func<Material> make)
        {
            if (cache.TryGetValue(key, out Material m) && m != null) return m;
            m = make();
            m.name = key;
            cache[key] = m;
            return m;
        }

        public static void ClearCache()
        {
            cache.Clear();
        }

        // ------------------------------------------------------------------ configuration of surface type
        public static void SetSurface(Material m, Surface surface, bool twoSided = false)
        {
            switch (surface)
            {
                case Surface.Opaque:
                    m.SetFloat("_Surface", 0f); m.SetFloat("_Blend", 0f); m.SetFloat("_AlphaClip", 0f);
                    m.SetFloat("_SrcBlend", 1f); m.SetFloat("_DstBlend", 0f); m.SetFloat("_SrcBlendAlpha", 1f); m.SetFloat("_DstBlendAlpha", 0f);
                    m.SetFloat("_ZWrite", 1f);
                    m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.DisableKeyword("_ALPHATEST_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    m.SetOverrideTag("RenderType", "Opaque");
                    m.renderQueue = (int)RenderQueue.Geometry;
                    m.SetShaderPassEnabled("ShadowCaster", true);
                    break;
                case Surface.Cutout:
                    m.SetFloat("_Surface", 0f); m.SetFloat("_Blend", 0f); m.SetFloat("_AlphaClip", 1f);
                    m.SetFloat("_SrcBlend", 1f); m.SetFloat("_DstBlend", 0f); m.SetFloat("_SrcBlendAlpha", 1f); m.SetFloat("_DstBlendAlpha", 0f);
                    m.SetFloat("_ZWrite", 1f);
                    m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.EnableKeyword("_ALPHATEST_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    m.SetOverrideTag("RenderType", "TransparentCutout");
                    m.renderQueue = (int)RenderQueue.AlphaTest;
                    m.SetShaderPassEnabled("ShadowCaster", true);
                    break;
                case Surface.Alpha:
                    m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); m.SetFloat("_AlphaClip", 0f);
                    m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                    m.SetFloat("_ZWrite", 0f);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.DisableKeyword("_ALPHATEST_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    m.SetOverrideTag("RenderType", "Transparent");
                    m.renderQueue = (int)RenderQueue.Transparent;
                    m.SetShaderPassEnabled("ShadowCaster", false);
                    break;
                case Surface.Additive:
                    m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 2f); m.SetFloat("_AlphaClip", 0f);
                    m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.One);
                    m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
                    m.SetFloat("_ZWrite", 0f);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.DisableKeyword("_ALPHATEST_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    m.SetOverrideTag("RenderType", "Transparent");
                    m.renderQueue = (int)RenderQueue.Transparent + 10;
                    m.SetShaderPassEnabled("ShadowCaster", false);
                    break;
            }
            m.SetFloat("_Cull", twoSided ? (float)CullMode.Off : (float)CullMode.Back);
        }

        // ------------------------------------------------------------------ factories
        public static Material Lit(Color color, float smoothness = 0.3f, float metallic = 0f, TexSet tex = null, Vector2? tiling = null, float bump = 1f)
        {
            var m = new Material(LitShader);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_SpecularHighlights", 1f);
            m.SetFloat("_EnvironmentReflections", 1f);
            if (tex != null)
            {
                m.SetTexture("_BaseMap", tex.albedo);
                Vector2 t = tiling ?? Vector2.one;
                m.SetTextureScale("_BaseMap", t);
                if (tex.normal != null)
                {
                    m.SetTexture("_BumpMap", tex.normal);
                    m.SetTextureScale("_BumpMap", t);
                    m.SetFloat("_BumpScale", bump);
                    m.EnableKeyword("_NORMALMAP");
                }
            }
            m.enableInstancing = true;
            SetSurface(m, Surface.Opaque);
            return m;
        }

        public static Material LitCutout(Color color, TexSet tex, float cutoff = 0.5f, float smoothness = 0.1f)
        {
            Material m = Lit(color, smoothness, 0f, tex);
            SetSurface(m, Surface.Cutout, true);
            m.SetFloat("_Cutoff", cutoff);
            return m;
        }

        public static Material LitAlpha(Color color, float smoothness = 0.85f, TexSet tex = null, bool twoSided = false)
        {
            Material m = Lit(color, smoothness, 0f, tex);
            SetSurface(m, Surface.Alpha, twoSided);
            return m;
        }

        public static void SetEmission(Material m, Color color, float intensity = 1f, Texture map = null)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * intensity);
            if (map != null) m.SetTexture("_EmissionMap", map);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        public static Material Glow(Color color, float intensity = 2f, float smoothness = 0.4f)
        {
            Material m = Lit(color * 0.4f, smoothness);
            SetEmission(m, color, intensity);
            return m;
        }

        public static Material Unlit(Color color, Texture tex = null, Surface surface = Surface.Opaque, bool twoSided = false)
        {
            var m = new Material(UnlitShader);
            m.SetColor("_BaseColor", color);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            SetSurface(m, surface, twoSided);
            m.enableInstancing = true;
            return m;
        }

        /// <summary>Particle material: additive or alpha-blended unlit with a sprite texture; honours vertex (particle) colors.</summary>
        public static Material Particle(Texture tex, Color tint, bool additive = true)
        {
            var m = new Material(ParticleShader);
            m.SetColor("_BaseColor", tint);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            SetSurface(m, additive ? Surface.Additive : Surface.Alpha, true);
            m.SetFloat("_BlendOp", 0f);
            return m;
        }

        public static Material Terrain()
        {
            return new Material(TerrainShader);
        }

        public static void SetColor(Material m, Color c)
        {
            m.SetColor("_BaseColor", c);
        }

        /// <summary>Applies a base color to a renderer through a property block (keeps GPU instancing intact).</summary>
        public static void Tint(Renderer r, Color c, MaterialPropertyBlock block)
        {
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", c);
            r.SetPropertyBlock(block);
        }
    }
}
