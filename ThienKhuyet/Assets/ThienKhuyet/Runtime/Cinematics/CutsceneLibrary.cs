using System;
using System.Collections.Generic;
using ThienKhuyet.Core;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ThienKhuyet.Cinematics
{
    public enum CutsceneCommand
    {
        Shot, Subtitle, Dialogue, Animation, Expression, Move, Vfx, Sfx, Music, Ambience,
        Environment, DepthOfField, Fade, Bars, TimeScale, Flag, Teleport, Title, Shake, Choice, End
    }

    [Serializable]
    public sealed class CutsceneBeat
    {
        public int line;
        public float at;
        public float duration;
        public CutsceneCommand command;
        public string actor = "player";
        public string key = "";
        public string value = "";
        public string who = "";
        public string text = "";
        public string extra = "";
        public string[] options = new string[0];
        public Vector3 position;
        public Vector3 target;
        public Color color = Color.white;
        public float[] values = new float[0];
        public bool flag;
    }

    /// <summary>TimelineAsset-driven or script-driven cinematic. Scripts live under Resources/Story/Cutscenes.</summary>
    public sealed class CutsceneDefinition : ScriptableObject
    {
        public string id;
        public string titleKey;
        public float duration = 8f;
        public bool replayable;
        public string stage = "";
        public Vector3 playerStart;
        public float playerYaw;
        public string music = "";
        public TimelineAsset timeline;
        public List<CutsceneBeat> beats = new List<CutsceneBeat>();
    }

    /// <summary>Simple authored, data-driven cutscene syntax shared with dialogue and quest scripts.</summary>
    public static class CutsceneParser
    {
        public static CutsceneDefinition Parse(string text, string source, List<string> errors = null)
        {
            CutsceneDefinition def = ScriptableObject.CreateInstance<CutsceneDefinition>();
            def.name = source;
            def.id = source;
            bool header = false;
            float maxEnd = 0f;
            foreach (ScriptLine line in ScriptReader.Parse(text, source))
            {
                if (line.Verb == "cutscene")
                {
                    if (!header)
                    {
                        def.id = line.Arg(0, source);
                        def.titleKey = line.Get("title", "");
                        def.duration = Mathf.Max(0.1f, line.GetFloat("duration", def.duration));
                        def.replayable = line.GetBool("replayable");
                        def.stage = line.Get("stage", "");
                        def.playerStart = Vec(line.Get("start", "0,0,0"), Vector3.zero);
                        def.playerYaw = line.GetFloat("yaw", 0f);
                        def.music = line.Get("music", "");
                        header = true;
                    }
                    else errors?.Add(line + ": duplicate cutscene header");
                    continue;
                }
                if (!header) errors?.Add(line + ": command before cutscene header");
                CutsceneBeat beat;
                if (!TryBeat(line, out beat, errors)) continue;
                def.beats.Add(beat);
                maxEnd = Mathf.Max(maxEnd, beat.at + beat.duration);
            }
            if (!header) errors?.Add(source + ": missing cutscene header");
            def.duration = Mathf.Max(def.duration, maxEnd, 0.1f);
            def.beats.Sort((a, b) => a.at.CompareTo(b.at));
            return def;
        }

        static bool TryBeat(ScriptLine line, out CutsceneBeat beat, List<string> errors)
        {
            beat = new CutsceneBeat
            {
                line = line.No,
                at = Mathf.Max(0f, line.GetFloat("at", 0f)),
                duration = Mathf.Max(0f, line.GetFloat("dur", 0f)),
                actor = line.Get("actor", "player"),
                key = line.Get("id", line.Get("key", "")),
                value = line.Get("value", ""),
                who = line.Get("who", ""),
                text = line.Get("text", ""),
                extra = line.Get("extra", ""),
                position = Vec(line.Get("pos", "0,0,0"), Vector3.zero),
                target = Vec(line.Get("look", line.Get("target", "0,0,0")), Vector3.zero),
                color = ParseColor(line.Get("color", "white")),
                flag = line.GetBool("on", true)
            };
            switch (line.Verb.ToLowerInvariant())
            {
                case "shot":
                    beat.command = CutsceneCommand.Shot;
                    beat.values = new[] { line.GetFloat("fov", 55f), line.GetFloat("ease", 1.2f) };
                    beat.duration = Mathf.Max(0.05f, beat.duration);
                    break;
                case "subtitle":
                    beat.command = CutsceneCommand.Subtitle;
                    beat.duration = Mathf.Max(0.1f, beat.duration > 0f ? beat.duration : 2.8f);
                    beat.values = new[] { line.GetFloat("size", 1f) };
                    break;
                case "dialogue":
                    beat.command = CutsceneCommand.Dialogue;
                    beat.duration = Mathf.Max(0f, beat.duration);
                    break;
                case "anim":
                    beat.command = CutsceneCommand.Animation;
                    beat.values = new[] { line.GetFloat("speed", 1f) };
                    break;
                case "expr": beat.command = CutsceneCommand.Expression; break;
                case "move": beat.command = CutsceneCommand.Move; beat.values = new[] { line.GetFloat("yaw", 0f) }; break;
                case "vfx": beat.command = CutsceneCommand.Vfx; beat.values = new[] { line.GetFloat("scale", 1f) }; break;
                case "sfx": beat.command = CutsceneCommand.Sfx; beat.values = new[] { line.GetFloat("volume", 1f), line.GetFloat("pitch", 1f) }; break;
                case "music": beat.command = CutsceneCommand.Music; beat.values = new[] { line.GetFloat("fade", 2f) }; break;
                case "ambience":
                    beat.command = CutsceneCommand.Ambience;
                    beat.values = new[] { line.GetFloat("volume", 0.5f), line.GetFloat("volume2", 0f) };
                    beat.extra = line.Get("id2", "");
                    break;
                case "env":
                    beat.command = CutsceneCommand.Environment;
                    beat.values = new[] { line.GetFloat("fog", 1f), line.GetFloat("sun", 1f), line.GetFloat("ambient", 1f), line.GetFloat("weight", 0f) };
                    break;
                case "dof":
                    beat.command = CutsceneCommand.DepthOfField;
                    beat.values = new[] { line.GetFloat("focus", 10f), line.GetFloat("aperture", 8f), line.GetFloat("focal", 50f), line.GetFloat("sat", 1f), line.GetFloat("exposure", 0f), line.GetFloat("vignette", 0f), line.GetFloat("chroma", 0f), line.GetFloat("grain", 0f) };
                    break;
                case "fade":
                    beat.command = CutsceneCommand.Fade;
                    beat.values = new[] { Mathf.Clamp01(line.GetFloat("alpha", 1f)) };
                    beat.duration = Mathf.Max(0f, beat.duration);
                    break;
                case "bars": beat.command = CutsceneCommand.Bars; break;
                case "time":
                    beat.command = CutsceneCommand.TimeScale;
                    beat.values = new[] { Mathf.Clamp(line.GetFloat("scale", 1f), 0.05f, 1.5f) };
                    beat.duration = Mathf.Max(0f, beat.duration);
                    break;
                case "flag": beat.command = CutsceneCommand.Flag; break;
                case "tp": beat.command = CutsceneCommand.Teleport; break;
                case "title": beat.command = CutsceneCommand.Title; break;
                case "shake":
                    beat.command = CutsceneCommand.Shake;
                    beat.values = new[] { line.GetFloat("amp", 0.2f), line.GetFloat("dur", 0.35f) };
                    break;
                case "choice":
                    beat.command = CutsceneCommand.Choice;
                    beat.options = SplitOptions(line.Get("options", ""));
                    if (beat.options.Length < 2) errors?.Add(line + ": choice requires at least two options separated by '|'");
                    break;
                case "end": beat.command = CutsceneCommand.End; break;
                default:
                    errors?.Add(line + ": unknown cutscene command '" + line.Verb + "'");
                    return false;
            }
            return true;
        }

        static string[] SplitOptions(string options)
        {
            if (string.IsNullOrWhiteSpace(options)) return new string[0];
            string[] values = options.Split('|');
            for (int i = 0; i < values.Length; i++) values[i] = values[i].Trim();
            return values;
        }

        static Vector3 Vec(string raw, Vector3 fallback)
        {
            if (string.IsNullOrEmpty(raw)) return fallback;
            float[] v = ScriptReader.ParseFloats(raw, fallback.x, fallback.y, fallback.z);
            return new Vector3(v[0], v[1], v[2]);
        }

        public static Color ParseColor(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return Color.white;
            if (ColorUtility.TryParseHtmlString(raw, out Color parsed)) return parsed;
            switch (raw.ToLowerInvariant())
            {
                case "gold": return new Color(0.95f, 0.78f, 0.35f);
                case "jade": return new Color(0.4f, 0.9f, 0.72f);
                case "red": return new Color(1f, 0.35f, 0.28f);
                case "blue": return new Color(0.42f, 0.65f, 1f);
                case "black": return Color.black;
                default: return Color.white;
            }
        }
    }

    public static class CutsceneLibrary
    {
        static readonly Dictionary<string, CutsceneDefinition> definitions = new Dictionary<string, CutsceneDefinition>(StringComparer.Ordinal);
        static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            definitions.Clear();
            loaded = false;
        }

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            foreach (CutsceneDefinition definition in Resources.LoadAll<CutsceneDefinition>("Story/Cutscenes")) Register(definition);
            var errors = new List<string>();
            foreach (TextAsset script in Resources.LoadAll<TextAsset>("Story/Cutscenes"))
            {
                CutsceneDefinition parsed = CutsceneParser.Parse(script.text, script.name, errors);
                Register(parsed);
            }
            for (int i = 0; i < errors.Count; i++) Debug.LogWarning("[Cutscene] " + errors[i]);
        }

        public static void Register(CutsceneDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.id)) return;
            definitions[definition.id] = definition;
        }

        public static CutsceneDefinition Get(string id)
        {
            Load();
            definitions.TryGetValue(id ?? string.Empty, out CutsceneDefinition definition);
            return definition;
        }

        public static IEnumerable<CutsceneDefinition> All
        {
            get { Load(); return definitions.Values; }
        }
    }
}
