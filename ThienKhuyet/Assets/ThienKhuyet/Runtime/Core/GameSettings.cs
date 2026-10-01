using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ThienKhuyet.Core
{
    public enum QualityLevel { Low = 0, Medium = 1, High = 2 }

    /// <summary>Persistent player settings (PlayerPrefs) plus the code that applies them to the engine and the URP asset.</summary>
    public sealed class GameSettings
    {
        const string Prefix = "tk.";

        public float masterVolume = 0.8f;
        public float musicVolume = 0.6f;
        public float sfxVolume = 0.8f;
        public float mouseSensitivity = 1f;
        public float gamepadSensitivity = 1f;
        public bool invertY;
        public float fov = 62f;
        public float cameraShake = 0.6f;
        public QualityLevel quality = QualityLevel.Medium;
        public bool subtitles = true;
        public float subtitleScale = 1f;
        public string language = "vi";
        public bool fullscreen = true;
        public bool vsync = true;
        public float uiScale = 1f;

        public event Action Changed;

        public void Load()
        {
            masterVolume = PlayerPrefs.GetFloat(Prefix + "master", masterVolume);
            musicVolume = PlayerPrefs.GetFloat(Prefix + "music", musicVolume);
            sfxVolume = PlayerPrefs.GetFloat(Prefix + "sfx", sfxVolume);
            mouseSensitivity = PlayerPrefs.GetFloat(Prefix + "msens", mouseSensitivity);
            gamepadSensitivity = PlayerPrefs.GetFloat(Prefix + "gsens", gamepadSensitivity);
            invertY = PlayerPrefs.GetInt(Prefix + "invy", invertY ? 1 : 0) == 1;
            fov = PlayerPrefs.GetFloat(Prefix + "fov", fov);
            cameraShake = PlayerPrefs.GetFloat(Prefix + "shake", cameraShake);
            quality = (QualityLevel)PlayerPrefs.GetInt(Prefix + "quality", (int)quality);
            subtitles = PlayerPrefs.GetInt(Prefix + "subs", subtitles ? 1 : 0) == 1;
            subtitleScale = PlayerPrefs.GetFloat(Prefix + "subscale", subtitleScale);
            language = PlayerPrefs.GetString(Prefix + "lang", language);
            fullscreen = PlayerPrefs.GetInt(Prefix + "full", fullscreen ? 1 : 0) == 1;
            vsync = PlayerPrefs.GetInt(Prefix + "vsync", vsync ? 1 : 0) == 1;
            uiScale = PlayerPrefs.GetFloat(Prefix + "uiscale", uiScale);
            Clamp();
        }

        void Clamp()
        {
            masterVolume = Mathf.Clamp01(masterVolume);
            musicVolume = Mathf.Clamp01(musicVolume);
            sfxVolume = Mathf.Clamp01(sfxVolume);
            mouseSensitivity = Mathf.Clamp(mouseSensitivity, 0.2f, 3f);
            gamepadSensitivity = Mathf.Clamp(gamepadSensitivity, 0.2f, 3f);
            fov = Mathf.Clamp(fov, 50f, 90f);
            cameraShake = Mathf.Clamp01(cameraShake);
            subtitleScale = Mathf.Clamp(subtitleScale, 0.7f, 1.6f);
            uiScale = Mathf.Clamp(uiScale, 0.7f, 1.4f);
            if (Array.IndexOf(Loc.Languages, language) < 0) language = "vi";
        }

        public void Save()
        {
            PlayerPrefs.SetFloat(Prefix + "master", masterVolume);
            PlayerPrefs.SetFloat(Prefix + "music", musicVolume);
            PlayerPrefs.SetFloat(Prefix + "sfx", sfxVolume);
            PlayerPrefs.SetFloat(Prefix + "msens", mouseSensitivity);
            PlayerPrefs.SetFloat(Prefix + "gsens", gamepadSensitivity);
            PlayerPrefs.SetInt(Prefix + "invy", invertY ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "fov", fov);
            PlayerPrefs.SetFloat(Prefix + "shake", cameraShake);
            PlayerPrefs.SetInt(Prefix + "quality", (int)quality);
            PlayerPrefs.SetInt(Prefix + "subs", subtitles ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "subscale", subtitleScale);
            PlayerPrefs.SetString(Prefix + "lang", language);
            PlayerPrefs.SetInt(Prefix + "full", fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "vsync", vsync ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "uiscale", uiScale);
            PlayerPrefs.Save();
        }

        public void Apply()
        {
            Clamp();
            QualitySettings.vSyncCount = vsync ? 1 : 0;
            AudioListener.volume = masterVolume;
            if (Screen.fullScreen != fullscreen) Screen.fullScreen = fullscreen;
            ApplyQuality();
            Changed?.Invoke();
        }

        /// <summary>Quality presets change real costs: shadow distance, render scale, LOD bias, vegetation/viewing distance.</summary>
        public void ApplyQuality()
        {
            var urp = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            switch (quality)
            {
                case QualityLevel.Low:
                    QualitySettings.lodBias = 0.8f;
                    if (urp != null) { urp.shadowDistance = 60f; urp.renderScale = 0.85f; urp.msaaSampleCount = 1; urp.shadowCascadeCount = 2; }
                    break;
                case QualityLevel.Medium:
                    QualitySettings.lodBias = 1.2f;
                    if (urp != null) { urp.shadowDistance = 110f; urp.renderScale = 1f; urp.msaaSampleCount = 1; urp.shadowCascadeCount = 3; }
                    break;
                default:
                    QualitySettings.lodBias = 1.8f;
                    if (urp != null) { urp.shadowDistance = 160f; urp.renderScale = 1f; urp.msaaSampleCount = 2; urp.shadowCascadeCount = 4; }
                    break;
            }
        }

        /// <summary>Number of 64m chunk rings kept alive around the player, and vegetation density multiplier.</summary>
        public int ChunkRadius => quality == QualityLevel.Low ? 2 : (quality == QualityLevel.Medium ? 3 : 4);
        public float VegetationDensity => quality == QualityLevel.Low ? 0.55f : (quality == QualityLevel.Medium ? 0.85f : 1.1f);
    }
}
