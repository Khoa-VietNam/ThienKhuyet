using System;
using System.Collections;
using System.Collections.Generic;
using ThienKhuyet.Core;
using UnityEngine;

namespace ThienKhuyet.Audio
{
    /// <summary>
    /// Plays sound effects, music and ambience. Clips come from (1) recorded assets in Resources/Audio/{id} when present
    /// (drop-in replacement, also voice-over under Resources/Voice/{textKey}), otherwise (2) the procedural <see cref="Synth"/>.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        const int SfxVoices = 20;

        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, Func<float[]>> recipes = new Dictionary<string, Func<float[]>>();
        readonly Dictionary<string, bool> loopFlags = new Dictionary<string, bool>();
        AudioSource[] voices;
        AudioSource musicA, musicB, voiceSrc;
        AudioSource[] ambience = new AudioSource[3];
        string[] ambienceIds = new string[3];
        float[] ambienceTarget = new float[3];
        int nextVoice;
        bool aIsActive = true;
        string currentMusic;
        float musicFadeT = 1f, musicFadeDur = 2f;
        float musicDuck = 1f, musicDuckTarget = 1f;
        AudioLowPassFilter muffle;
        GameSettings settings;

        public static AudioManager Instance { get; private set; }
        public string CurrentMusic => currentMusic;
        public bool Ready { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }

        public static AudioManager Create(Transform parent, GameSettings settings)
        {
            var go = new GameObject("AudioManager");
            go.transform.SetParent(parent, false);
            var am = go.AddComponent<AudioManager>();
            am.settings = settings;
            am.Setup();
            Instance = am;
            return am;
        }

        void Setup()
        {
            voices = new AudioSource[SfxVoices];
            for (int i = 0; i < voices.Length; i++)
            {
                var s = NewSource("sfx" + i, false);
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Logarithmic;
                s.minDistance = 3f;
                s.maxDistance = 55f;
                voices[i] = s;
            }
            musicA = NewSource("musicA", true);
            musicB = NewSource("musicB", true);
            voiceSrc = NewSource("voice", false);
            for (int i = 0; i < ambience.Length; i++) ambience[i] = NewSource("amb" + i, true);
            RegisterRecipes();
        }

        AudioSource NewSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            return s;
        }

        // ------------------------------------------------------------------ recipes
        void R(string id, Func<float[]> f, bool loop = false)
        {
            recipes[id] = f;
            loopFlags[id] = loop;
        }

        static float[] Mix(params float[][] parts)
        {
            int n = 0;
            for (int i = 0; i < parts.Length; i++) n = Math.Max(n, parts[i].Length);
            var d = new float[n];
            for (int p = 0; p < parts.Length; p++)
                for (int i = 0; i < parts[p].Length; i++) d[i] += parts[p][i];
            return d;
        }

        void RegisterRecipes()
        {
            R("swing", () => Synth.Whoosh(0.25f, 300f, 2400f, 1, 0.8f));
            R("heavy_swing", () => Synth.Whoosh(0.45f, 160f, 1500f, 2, 1f));
            R("thrust", () => Synth.Whoosh(0.2f, 700f, 3200f, 3, 0.8f));
            R("hit", () => Synth.Thump(0.18f, 140f, 48f, 0.9f, 4));
            R("hit_heavy", () => Synth.Thump(0.28f, 100f, 34f, 1.2f, 5));
            R("punch", () => Synth.Thump(0.15f, 170f, 62f, 0.6f, 6));
            R("heavy_punch", () => Synth.Thump(0.24f, 110f, 40f, 0.9f, 7));
            R("kick", () => Synth.Thump(0.2f, 150f, 52f, 0.7f, 8));
            R("staff", () => Mix(Synth.Thump(0.16f, 210f, 90f, 0.9f, 9), Synth.Clang(0.2f, 420f, 9, 0.25f)));
            R("block", () => Synth.Clang(0.32f, 880f, 10));
            R("parry", () => Mix(Synth.Clang(0.7f, 1500f, 11, 0.8f), Synth.Chime(new[] { 1760f, 2350f }, 0.05f, 0.35f, 0.25f)));
            R("cast", () => Synth.Sweep(0.5f, 280f, 950f, 0.3f, 12, 0.6f, true));
            R("sword_wave", () => Synth.Sweep(0.38f, 500f, 2000f, 0.25f, 13, 0.6f));
            R("fire_cast", () => Mix(Synth.NoiseBurst(0.45f, 1800f, 0.2f, 14, 0.7f), Synth.Sweep(0.4f, 200f, 520f, 0.4f, 15, 0.4f)));
            R("thunder", () => Mix(Synth.Rumble(1.4f, 150f, 16, 0.9f), Synth.NoiseBurst(0.35f, 4500f, 0.06f, 17, 1f)));
            R("explosion", () => Mix(Synth.Thump(0.5f, 90f, 28f, 1f, 18), Synth.NoiseBurst(0.5f, 1500f, 0.18f, 19, 0.8f)));
            R("jump", () => Synth.Sweep(0.18f, 210f, 440f, 0.15f, 20, 0.35f));
            R("land", () => Synth.Thump(0.2f, 95f, 46f, 0.5f, 21, 0.7f));
            R("dodge", () => Synth.Whoosh(0.3f, 180f, 1500f, 22, 0.6f));
            R("step_grass", () => Synth.NoiseBurst(0.1f, 900f, 0.035f, 23, 0.35f));
            R("step_stone", () => Synth.NoiseBurst(0.08f, 2800f, 0.022f, 24, 0.4f));
            R("step_dirt", () => Synth.NoiseBurst(0.1f, 1400f, 0.03f, 25, 0.38f));
            R("step_wood", () => Mix(Synth.NoiseBurst(0.09f, 1900f, 0.025f, 26, 0.3f), Synth.Thump(0.09f, 180f, 110f, 0.2f, 27, 0.35f)));
            R("step_water", () => Synth.NoiseBurst(0.2f, 2600f, 0.08f, 28, 0.45f));
            R("pickup", () => Synth.Chime(new[] { 880f, 1320f }, 0.07f, 0.25f));
            R("pickup_rare", () => Synth.Chime(new[] { 660f, 990f, 1320f, 1760f }, 0.08f, 0.4f));
            R("equip", () => Synth.Clang(0.18f, 700f, 29, 0.5f));
            R("ui_click", () => Synth.Chime(new[] { 1300f }, 0.01f, 0.05f, 0.3f));
            R("ui_hover", () => Synth.Chime(new[] { 1800f }, 0.01f, 0.03f, 0.12f));
            R("ui_open", () => Synth.Chime(new[] { 520f, 780f }, 0.05f, 0.15f, 0.35f));
            R("ui_close", () => Synth.Chime(new[] { 780f, 520f }, 0.05f, 0.15f, 0.35f));
            R("ui_error", () => Synth.Sweep(0.22f, 300f, 160f, 0.1f, 30, 0.4f));
            R("quest_update", () => Synth.Chime(new[] { 660f, 880f, 1100f }, 0.09f, 0.4f));
            R("quest_done", () => Synth.Chime(new[] { 523f, 659f, 784f, 1046f }, 0.1f, 0.6f));
            R("heal", () => Synth.Chime(new[] { 523f, 659f, 784f }, 0.06f, 0.5f, 0.35f));
            R("levelup", () => Synth.Chime(new[] { 392f, 523f, 659f, 784f, 1046f }, 0.1f, 0.9f));
            R("breakthrough", () => Mix(Synth.Drone(4f, new[] { 110f, 165f, 220f, 330f, 440f }, 0.5f), Synth.Chime(new[] { 392f, 523f, 659f, 784f, 1046f, 1318f }, 0.28f, 1.2f, 0.4f)));
            R("die_enemy", () => Synth.Sweep(0.45f, 240f, 70f, 0.35f, 31, 0.6f));
            R("die_player", () => Mix(Synth.Sweep(1.2f, 260f, 45f, 0.2f, 32, 0.6f), Synth.Thump(0.4f, 80f, 30f, 0.4f, 33, 0.8f)));
            R("hurt_player", () => Mix(Synth.Thump(0.16f, 220f, 90f, 0.7f, 34, 0.8f), Synth.Sweep(0.18f, 420f, 220f, 0.3f, 35, 0.3f)));
            R("enemy_hurt", () => Synth.Sweep(0.16f, 420f, 200f, 0.5f, 36, 0.45f));
            R("growl", () => Synth.Growl(0.7f, 85f, 37));
            R("roar", () => Synth.Growl(1.2f, 55f, 38, 0.9f));
            R("howl", () => Synth.Howl(1.8f, 39));
            R("shoot", () => Mix(Synth.Whoosh(0.16f, 1200f, 4500f, 40, 0.6f), Synth.Sweep(0.12f, 900f, 300f, 0.2f, 41, 0.3f)));
            R("heartbeat", () => Synth.Heartbeat(1.1f));
            R("glass_shatter", () => Synth.Glass(1.5f, 42));
            R("light_surge", () => Synth.Drone(5f, new[] { 82.4f, 123.5f, 164.8f, 247f, 329.6f }, 0.7f, 0.6f));
            R("static_distort", () => Mix(Synth.NoiseBurst(1.2f, 3500f, 0.9f, 43, 0.5f), Synth.Sweep(1.2f, 900f, 60f, 0.2f, 44, 0.5f)));
            R("door_stone", () => Synth.Rumble(3f, 90f, 45, 1f));
            R("glyph_hum", () => Synth.Drone(3.5f, new[] { 440f, 660f, 880f, 1320f }, 0.3f, 1.2f));
            R("whoosh_big", () => Synth.Whoosh(1.4f, 100f, 1600f, 46, 0.9f));
            R("chime_soft", () => Synth.Chime(new[] { 784f, 1046f }, 0.3f, 1.4f, 0.3f));
            R("wood_creak", () => Synth.Sweep(0.4f, 140f, 220f, 0.5f, 47, 0.3f));
            R("zoom_in", () => Synth.Sweep(1.8f, 120f, 520f, 0.2f, 48, 0.5f, true));

            R("amb_wind", () => Synth.Wind(8f, 51), true);
            R("amb_birds", () => Synth.Birds(8f, 52), true);
            R("amb_water", () => Synth.Water(7f, 53), true);
            R("amb_crickets", () => Synth.Crickets(6f, 54), true);
            R("amb_fire", () => Synth.Fire(5f, 55), true);
            R("amb_cave", () => Synth.Rumble(8f, 120f, 56, 0.6f), true);
            R("amb_city", () => Mix(Synth.Rumble(8f, 260f, 57, 0.35f), Synth.Drone(8f, new[] { 55f, 82.4f }, 0.12f, 0.2f)), true);
            R("amb_hum", () => Synth.Drone(8f, new[] { 98f, 147f, 196f }, 0.3f, 0.5f), true);

            string[] moods = { "calm", "village", "mystery", "combat", "boss", "title", "sorrow" };
            for (int i = 0; i < moods.Length; i++)
            {
                string m = moods[i];
                int seed = 1000 + i * 37;
                R("music_" + m, () => Synth.Music(m, seed), true);
            }
        }

        // ------------------------------------------------------------------ clip access
        AudioClip Build(string id)
        {
            if (clips.TryGetValue(id, out AudioClip c) && c != null) return c;
            AudioClip rec = Resources.Load<AudioClip>("Audio/" + id);
            if (rec != null) { clips[id] = rec; return rec; }
            if (!recipes.TryGetValue(id, out Func<float[]> make)) return null;
            float[] data = make();
            Synth.Normalize(data, id.StartsWith("music_") ? 0.6f : (id.StartsWith("amb_") ? 0.5f : 0.9f));
            c = AudioClip.Create(id, data.Length, 1, Synth.Rate, false);
            c.SetData(data, 0);
            clips[id] = c;
            return c;
        }

        /// <summary>Synthesizes every clip over several frames (call from the loading screen).</summary>
        public IEnumerator Preload(Action<float> progress)
        {
            var ids = new List<string>(recipes.Keys);
            for (int i = 0; i < ids.Count; i++)
            {
                Build(ids[i]);
                progress?.Invoke((i + 1f) / ids.Count);
                if (i % 2 == 1) yield return null;
            }
            Ready = true;
        }

        public bool Has(string id)
        {
            return recipes.ContainsKey(id) || Resources.Load<AudioClip>("Audio/" + id) != null;
        }

        // ------------------------------------------------------------------ playback
        public void Sfx(string id, Vector3 pos, float volume = 1f, float pitch = 1f, float pitchVar = 0.06f)
        {
            if (string.IsNullOrEmpty(id)) return;
            AudioClip c = Build(id);
            if (c == null) return;
            AudioSource s = NextVoice();
            s.transform.position = pos;
            s.spatialBlend = 1f;
            s.clip = c;
            s.volume = Mathf.Clamp01(volume) * (settings != null ? settings.sfxVolume : 1f);
            s.pitch = pitch * (1f + UnityEngine.Random.Range(-pitchVar, pitchVar));
            s.Play();
        }

        public void Sfx2D(string id, float volume = 1f, float pitch = 1f)
        {
            if (string.IsNullOrEmpty(id)) return;
            AudioClip c = Build(id);
            if (c == null) return;
            AudioSource s = NextVoice();
            s.spatialBlend = 0f;
            s.clip = c;
            s.volume = Mathf.Clamp01(volume) * (settings != null ? settings.sfxVolume : 1f);
            s.pitch = pitch;
            s.Play();
        }

        AudioSource NextVoice()
        {
            for (int i = 0; i < voices.Length; i++)
            {
                int k = (nextVoice + i) % voices.Length;
                if (!voices[k].isPlaying) { nextVoice = (k + 1) % voices.Length; return voices[k]; }
            }
            AudioSource s = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            return s;
        }

        public void Music(string mood, float fade = 2.5f)
        {
            string id = string.IsNullOrEmpty(mood) || mood == "none" ? null : (mood.StartsWith("music_") ? mood : "music_" + mood);
            if (id == currentMusic) return;
            currentMusic = id;
            AudioSource from = aIsActive ? musicA : musicB;
            AudioSource to = aIsActive ? musicB : musicA;
            aIsActive = !aIsActive;
            musicFadeT = 0f;
            musicFadeDur = Mathf.Max(0.05f, fade);
            if (id != null)
            {
                AudioClip c = Build(id);
                if (c != null)
                {
                    to.clip = c;
                    to.volume = 0f;
                    to.loop = true;
                    to.Play();
                }
            }
            musicFrom = from;
            musicTo = id != null ? to : null;
        }

        AudioSource musicFrom, musicTo;

        public void DuckMusic(float level)
        {
            musicDuckTarget = Mathf.Clamp01(level);
        }

        public void SetAmbience(string id0, float v0, string id1 = null, float v1 = 0f, string id2 = null, float v2 = 0f)
        {
            SetAmb(0, id0, v0);
            SetAmb(1, id1, v1);
            SetAmb(2, id2, v2);
        }

        void SetAmb(int slot, string id, float vol)
        {
            ambienceTarget[slot] = string.IsNullOrEmpty(id) ? 0f : vol;
            if (string.IsNullOrEmpty(id)) return;
            if (ambienceIds[slot] != id)
            {
                AudioClip c = Build(id);
                if (c == null) return;
                ambienceIds[slot] = id;
                ambience[slot].clip = c;
                ambience[slot].volume = 0f;
                ambience[slot].Play();
            }
        }

        /// <summary>Plays recorded or placeholder voice for a localized line, if any. Returns its length (0 when none).</summary>
        public float PlayVoice(string textKey)
        {
            var c = Resources.Load<AudioClip>("Voice/" + textKey);
            if (c == null) return 0f;
            voiceSrc.clip = c;
            voiceSrc.volume = settings != null ? Mathf.Clamp01(settings.sfxVolume + 0.2f) : 1f;
            voiceSrc.Play();
            return c.length;
        }

        public void StopVoice()
        {
            voiceSrc.Stop();
        }

        /// <summary>Muffles all audio (underwater / cutscene distortion). cutoff in Hz, 22000 = off.</summary>
        public void Muffle(float cutoff)
        {
            if (muffle == null)
            {
                var listener = FindAnyObjectByType<AudioListener>();
                if (listener == null) return;
                muffle = listener.GetComponent<AudioLowPassFilter>();
                if (muffle == null) muffle = listener.gameObject.AddComponent<AudioLowPassFilter>();
            }
            muffle.cutoffFrequency = Mathf.Clamp(cutoff, 200f, 22000f);
            muffle.enabled = cutoff < 21000f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float master = settings != null ? settings.musicVolume : 0.6f;
            musicDuck = Mathf.MoveTowards(musicDuck, musicDuckTarget, dt * 1.5f);
            if (musicFadeT < 1f)
            {
                musicFadeT = Mathf.Min(1f, musicFadeT + dt / musicFadeDur);
                if (musicFrom != null) musicFrom.volume = (1f - musicFadeT) * master * musicDuck;
                if (musicTo != null) musicTo.volume = musicFadeT * master * musicDuck;
                if (musicFadeT >= 1f && musicFrom != null) musicFrom.Stop();
            }
            else if (musicTo != null) musicTo.volume = master * musicDuck;

            float amb = settings != null ? settings.sfxVolume : 0.8f;
            for (int i = 0; i < ambience.Length; i++)
            {
                if (ambience[i].clip == null) continue;
                ambience[i].volume = Mathf.MoveTowards(ambience[i].volume, ambienceTarget[i] * amb * 0.8f, dt * 0.4f);
                if (ambienceTarget[i] <= 0.001f && ambience[i].volume <= 0.001f && ambience[i].isPlaying) { ambience[i].Stop(); ambienceIds[i] = null; }
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
