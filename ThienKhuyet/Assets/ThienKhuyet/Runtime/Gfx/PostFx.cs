using ThienKhuyet.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ThienKhuyet.Gfx
{
    /// <summary>
    /// Runtime post-processing through a global URP Volume created from code: bloom, tonemapping, grading, vignette, grain
    /// and depth of field. Gameplay (hit pulse, low HP) and cinematics (focus pulls, white-outs, mood grading) drive it.
    /// </summary>
    public sealed class PostFx : MonoBehaviour
    {
        Volume volume;
        VolumeProfile profile;
        Bloom bloom;
        Vignette vignette;
        ColorAdjustments color;
        Tonemapping tonemap;
        DepthOfField dof;
        FilmGrain grain;
        ChromaticAberration chroma;
        WhiteBalance white;

        float hitPulse;
        float lowHp;
        float whiteOut;
        float baseSat = -6f, baseExposure = 0.1f, baseContrast = 12f, baseVignette = 0.26f;
        float cineSat, cineExposure, cineVignette, cineChroma, cineGrain;
        bool cineDof;
        float cineFocus = 8f, cineAperture = 5.6f, cineFocal = 50f;
        Color cineTint = Color.white;
        float cineTintW;
        float moodTemp, moodTint;

        public static PostFx Create(Transform parent)
        {
            var go = new GameObject("PostFx");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<PostFx>();
            fx.Build();
            return fx;
        }

        void Build()
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "RuntimeProfile";
            bloom = profile.Add<Bloom>(false);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.42f);
            bloom.scatter.Override(0.62f);
            bloom.highQualityFiltering.Override(true);
            tonemap = profile.Add<Tonemapping>(false);
            tonemap.mode.Override(TonemappingMode.ACES);
            color = profile.Add<ColorAdjustments>(false);
            color.postExposure.Override(baseExposure);
            color.contrast.Override(baseContrast);
            color.saturation.Override(baseSat);
            color.colorFilter.Override(Color.white);
            vignette = profile.Add<Vignette>(false);
            vignette.intensity.Override(baseVignette);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(Color.black);
            grain = profile.Add<FilmGrain>(false);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.08f);
            grain.response.Override(0.8f);
            chroma = profile.Add<ChromaticAberration>(false);
            chroma.intensity.Override(0f);
            white = profile.Add<WhiteBalance>(false);
            white.temperature.Override(0f);
            white.tint.Override(0f);
            dof = profile.Add<DepthOfField>(false);
            dof.mode.Override(DepthOfFieldMode.Off);
            dof.focusDistance.Override(8f);
            dof.aperture.Override(5.6f);
            dof.focalLength.Override(50f);
            dof.bladeCount.Override(5);

            volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 50f;
            volume.weight = 1f;
            volume.sharedProfile = profile;
        }

        void OnDestroy()
        {
            if (profile != null) Destroy(profile);
        }

        // ------------------------------------------------------------------ gameplay hooks
        public void HitPulse(float strength)
        {
            hitPulse = Mathf.Max(hitPulse, Mathf.Clamp01(strength));
        }

        /// <summary>0..1: how close the player is to death (desaturates and darkens the edges).</summary>
        public void SetLowHealth(float k)
        {
            lowHp = Mathf.Clamp01(k);
        }

        public void SetMood(float temperature, float tint)
        {
            moodTemp = temperature;
            moodTint = tint;
        }

        // ------------------------------------------------------------------ cinematic hooks
        public void SetCinematic(bool dofOn, float focusDistance, float aperture, float focalLength, float saturation, float exposure, float vignetteAdd, float chromaAmount, float grainAdd)
        {
            cineDof = dofOn;
            cineFocus = Mathf.Max(0.3f, focusDistance);
            cineAperture = Mathf.Clamp(aperture, 1f, 32f);
            cineFocal = Mathf.Clamp(focalLength, 20f, 300f);
            cineSat = saturation;
            cineExposure = exposure;
            cineVignette = vignetteAdd;
            cineChroma = chromaAmount;
            cineGrain = grainAdd;
        }

        public void ClearCinematic()
        {
            cineDof = false;
            cineSat = cineExposure = cineVignette = cineChroma = cineGrain = 0f;
            cineTintW = 0f;
        }

        public void SetWhiteOut(float k)
        {
            whiteOut = Mathf.Clamp01(k);
        }

        public void SetTint(Color c, float weight)
        {
            cineTint = c;
            cineTintW = Mathf.Clamp01(weight);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            hitPulse = Mathf.MoveTowards(hitPulse, 0f, dt * 2.6f);

            float vig = baseVignette + cineVignette + lowHp * 0.2f + hitPulse * 0.28f;
            vignette.intensity.value = Mathf.Clamp01(vig);
            vignette.color.value = hitPulse > 0.02f || lowHp > 0.35f ? Color.Lerp(Color.black, new Color(0.45f, 0f, 0f), Mathf.Clamp01(hitPulse * 1.2f + lowHp * 0.5f)) : Color.black;
            color.saturation.value = baseSat + cineSat - lowHp * 28f;
            color.postExposure.value = baseExposure + cineExposure + whiteOut * 6f;
            color.contrast.value = baseContrast + lowHp * 8f;
            color.colorFilter.value = Color.Lerp(Color.white, cineTint, cineTintW);
            grain.intensity.value = Mathf.Clamp01(0.07f + cineGrain);
            chroma.intensity.value = Mathf.Clamp01(cineChroma + hitPulse * 0.25f);
            white.temperature.value = moodTemp;
            white.tint.value = moodTint;
            if (cineDof)
            {
                dof.mode.value = DepthOfFieldMode.Bokeh;
                dof.focusDistance.value = Mathf.Lerp(dof.focusDistance.value, cineFocus, 1f - Mathf.Exp(-9f * dt));
                dof.aperture.value = cineAperture;
                dof.focalLength.value = cineFocal;
            }
            else dof.mode.value = DepthOfFieldMode.Off;
        }
    }
}
