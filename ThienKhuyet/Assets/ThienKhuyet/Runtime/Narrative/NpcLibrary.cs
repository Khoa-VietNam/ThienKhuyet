using System.Collections.Generic;
using ThienKhuyet.Data;
using UnityEngine;

namespace ThienKhuyet.Narrative
{
    public sealed class DialogueRule
    {
        public string condition;
        public string dialogueId;

        public DialogueRule(string cond, string dialogue)
        {
            condition = cond;
            dialogueId = dialogue;
        }
    }

    public sealed class NpcDef
    {
        public string id;
        public string faction = "";
        public CharacterAppearance look = new CharacterAppearance();
        public bool hasWeapon;
        public WeaponFamily weapon = WeaponFamily.Staff;
        public Color weaponColor = new Color(0.45f, 0.32f, 0.2f);
        public float wanderRadius;
        public string[] gestures = { "gesture_a", "look_around" };
        public readonly List<DialogueRule> rules = new List<DialogueRule>();
        public string idleClip = "";
        public string NameKey => "npc." + id + ".name";
        public string TitleKey => "npc." + id + ".title";

        public NpcDef Rule(string cond, string dialogue)
        {
            rules.Add(new DialogueRule(cond, dialogue));
            return this;
        }
    }

    /// <summary>Static roster of characters (appearance, behaviour and which dialogue they use under which condition).</summary>
    public static class NpcLibrary
    {
        static readonly Dictionary<string, NpcDef> defs = new Dictionary<string, NpcDef>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            defs.Clear();
        }

        public static NpcDef Get(string id)
        {
            if (defs.Count == 0) Build();
            defs.TryGetValue(id, out NpcDef d);
            return d;
        }

        public static IEnumerable<NpcDef> All
        {
            get
            {
                if (defs.Count == 0) Build();
                return defs.Values;
            }
        }

        static void Add(NpcDef d)
        {
            defs[d.id] = d;
        }

        static void Build()
        {
            Add(new NpcDef
            {
                id = "elder_vu", faction = "village",
                look = new CharacterAppearance { outfit = OutfitStyle.Robe, hairStyle = HairStyle.Topknot, hair = new Color(0.86f, 0.86f, 0.86f), skin = new Color(0.78f, 0.62f, 0.5f), primary = new Color(0.78f, 0.82f, 0.76f), secondary = new Color(0.2f, 0.38f, 0.36f), accent = new Color(0.8f, 0.65f, 0.3f), pants = new Color(0.3f, 0.34f, 0.34f), accessories = Accessory.Beard, height = 0.97f },
                hasWeapon = true, weapon = WeaponFamily.Staff, weaponColor = new Color(0.4f, 0.3f, 0.2f)
            }.Rule("flag:root_test_done&!flag:elder_after_test", "elder_after_test")
             .Rule("quest:m1_thanha=active|quest:m1_thanha=done&!flag:root_test_done", "elder_root_test")
             .Rule("flag:path_chosen&!flag:elder_ruin_hint", "elder_ruin_hint")
             .Rule("flag:root_test_done", "elder_idle")
             .Rule("", "elder_first"));

            Add(new NpcDef
            {
                id = "hunter_luc", faction = "village",
                look = new CharacterAppearance { outfit = OutfitStyle.Leather, hairStyle = HairStyle.Messy, hair = new Color(0.1f, 0.08f, 0.07f), skin = new Color(0.72f, 0.55f, 0.42f), primary = new Color(0.42f, 0.38f, 0.28f), secondary = new Color(0.3f, 0.21f, 0.14f), accent = new Color(0.55f, 0.2f, 0.15f), pants = new Color(0.25f, 0.22f, 0.18f), accessories = Accessory.Headband, height = 1.05f, bulk = 1.08f },
                hasWeapon = true, weapon = WeaponFamily.Spear, weaponColor = new Color(0.75f, 0.78f, 0.82f), gestures = new[] { "gesture_b", "point" }
            }.Rule("quest:m1_train=active", "hunter_training")
             .Rule("quest:m1_wolves=active", "hunter_wolves")
             .Rule("flag:trained&!flag:hunter_body_tempering", "hunter_tempering")
             .Rule("flag:hunter_body_tempering", "hunter_idle")
             .Rule("", "hunter_first"));

            Add(new NpcDef
            {
                id = "healer_lan", faction = "village",
                look = new CharacterAppearance { outfit = OutfitStyle.Robe, hairStyle = HairStyle.Long, hair = new Color(0.06f, 0.05f, 0.06f), skin = new Color(0.88f, 0.72f, 0.62f), primary = new Color(0.58f, 0.78f, 0.58f), secondary = new Color(0.95f, 0.95f, 0.88f), accent = new Color(0.9f, 0.78f, 0.45f), pants = new Color(0.85f, 0.85f, 0.8f), height = 0.93f, bulk = 0.92f }
            }.Rule("quest:m1_paste=active", "healer_paste")
             .Rule("flag:healer_met", "healer_idle")
             .Rule("", "healer_first"));

            Add(new NpcDef
            {
                id = "merchant_trinh", faction = "village",
                look = new CharacterAppearance { outfit = OutfitStyle.Tunic, hairStyle = HairStyle.Short, hair = new Color(0.12f, 0.1f, 0.09f), skin = new Color(0.8f, 0.62f, 0.48f), primary = new Color(0.58f, 0.36f, 0.22f), secondary = new Color(0.25f, 0.3f, 0.42f), accent = new Color(0.78f, 0.6f, 0.25f), pants = new Color(0.3f, 0.26f, 0.22f), accessories = Accessory.Hat | Accessory.Backpack, height = 0.96f, bulk = 1.12f },
                gestures = new[] { "gesture_b", "gesture_a" }
            }.Rule("", "merchant_shop"));

            Add(new NpcDef
            {
                id = "villager_tam", faction = "village",
                look = new CharacterAppearance { outfit = OutfitStyle.Tunic, hairStyle = HairStyle.Short, hair = new Color(0.3f, 0.28f, 0.27f), skin = new Color(0.74f, 0.56f, 0.42f), primary = new Color(0.5f, 0.45f, 0.32f), secondary = new Color(0.34f, 0.3f, 0.22f), accent = new Color(0.6f, 0.5f, 0.3f), pants = new Color(0.36f, 0.32f, 0.26f), accessories = Accessory.Hat, height = 0.98f, bulk = 1.05f },
                wanderRadius = 5f, gestures = new[] { "gather", "look_around" }
            }.Rule("flag:wolves_warned", "tam_idle").Rule("", "tam_first"));

            Add(new NpcDef
            {
                id = "child_bao", faction = "village",
                look = new CharacterAppearance { outfit = OutfitStyle.Tunic, hairStyle = HairStyle.Messy, hair = new Color(0.08f, 0.07f, 0.07f), skin = new Color(0.86f, 0.68f, 0.55f), primary = new Color(0.8f, 0.55f, 0.3f), secondary = new Color(0.3f, 0.5f, 0.55f), accent = new Color(0.9f, 0.75f, 0.3f), pants = new Color(0.3f, 0.3f, 0.35f), height = 0.62f, bulk = 0.85f },
                wanderRadius = 8f, gestures = new[] { "startled", "gesture_a", "look_around" }
            }.Rule("", "child_chat"));

            Add(new NpcDef
            {
                id = "villager_hoa", faction = "village",
                look = new CharacterAppearance { outfit = OutfitStyle.Tunic, hairStyle = HairStyle.Bun, hair = new Color(0.08f, 0.06f, 0.06f), skin = new Color(0.86f, 0.7f, 0.58f), primary = new Color(0.72f, 0.46f, 0.5f), secondary = new Color(0.4f, 0.28f, 0.3f), accent = new Color(0.85f, 0.7f, 0.4f), pants = new Color(0.45f, 0.38f, 0.38f), height = 0.92f, bulk = 0.94f },
                wanderRadius = 4f
            }.Rule("", "hoa_chat"));

            // story characters used in cutscenes and later acts
            Add(new NpcDef
            {
                id = "stranger", faction = "",
                look = new CharacterAppearance { outfit = OutfitStyle.Wanderer, hairStyle = HairStyle.Ponytail, hair = new Color(0.07f, 0.06f, 0.08f), skin = new Color(0.8f, 0.64f, 0.55f), primary = new Color(0.22f, 0.26f, 0.3f), secondary = new Color(0.35f, 0.15f, 0.18f), accent = new Color(0.7f, 0.65f, 0.5f), pants = new Color(0.16f, 0.17f, 0.2f), accessories = Accessory.Mask | Accessory.Cape, height = 1.02f, bulk = 0.95f }
            });

            Add(new NpcDef
            {
                id = "self_echo", faction = "",
                look = new CharacterAppearance { outfit = OutfitStyle.Modern, hairStyle = HairStyle.Short, hair = new Color(0.07f, 0.06f, 0.06f), primary = new Color(0.25f, 0.28f, 0.34f), secondary = new Color(0.9f, 0.9f, 0.9f), accent = new Color(0.85f, 0.85f, 0.9f), pants = new Color(0.18f, 0.24f, 0.4f), accessories = Accessory.Backpack, height = 1f }
            });

            Add(new NpcDef
            {
                id = "ling_qingyun", faction = "jade_lodge",
                look = new CharacterAppearance { outfit = OutfitStyle.Robe, hairStyle = HairStyle.Ponytail, hair = new Color(0.07f, 0.07f, 0.1f), skin = new Color(0.9f, 0.76f, 0.66f), primary = new Color(0.9f, 0.93f, 0.97f), secondary = new Color(0.25f, 0.45f, 0.72f), accent = new Color(0.85f, 0.85f, 0.7f), pants = new Color(0.82f, 0.86f, 0.92f), height = 1.02f, bulk = 0.95f },
                hasWeapon = true, weapon = WeaponFamily.Sword, weaponColor = new Color(0.8f, 0.9f, 1f)
            });
        }
    }
}
