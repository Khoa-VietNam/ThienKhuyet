using System.Collections.Generic;
using ThienKhuyet.Core;
using UnityEngine;

namespace ThienKhuyet.Narrative
{
    /// <summary>Loads authored quest graphs from Resources/Story/Quests.</summary>
    public static class QuestLibrary
    {
        static readonly Dictionary<string, QuestDef> definitions = new Dictionary<string, QuestDef>();
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
            var errors = new List<string>();
            TextAsset[] scripts = Resources.LoadAll<TextAsset>("Story/Quests");
            for (int i = 0; i < scripts.Length; i++)
            {
                List<QuestDef> parsed = QuestParser.Parse(scripts[i].text, scripts[i].name, errors);
                for (int q = 0; q < parsed.Count; q++)
                {
                    QuestDef def = parsed[q];
                    if (definitions.ContainsKey(def.id)) Debug.LogWarning("[QuestLibrary] Duplicate quest id; last definition wins: " + def.id);
                    definitions[def.id] = def;
                }
            }
            for (int i = 0; i < errors.Count; i++) Debug.LogWarning("[QuestLibrary] " + errors[i]);
        }

        public static void Register(QuestDef definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.id)) return;
            definitions[definition.id] = definition;
        }

        public static IEnumerable<QuestDef> All
        {
            get { Load(); return definitions.Values; }
        }
    }
}
