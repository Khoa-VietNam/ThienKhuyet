using ThienKhuyet.Combat;
using UnityEngine;

namespace ThienKhuyet.Core
{
    public struct EnemyKilledEvent { public string enemyId; public Vector3 position; public bool boss; public bool elite; }
    public struct ItemGainedEvent { public string itemId; public int count; }
    public struct ItemUsedEvent { public string itemId; }
    public struct QuestChangedEvent { public string questId; }
    public struct RealmChangedEvent { public int realm; public int stage; public bool breakthrough; }
    public struct PlayerDiedEvent { }
    public struct PlayerRespawnedEvent { }
    public struct DamageEvent { public DamageInfo info; public DamageResult result; public Vector3 position; public GameObject victim; }
    public struct PoiDiscoveredEvent { public string poiId; }
    public struct ZoneChangedEvent { public string zoneId; }
    public struct CutsceneEvent { public string id; public bool started; }
    public struct DialogueEvent { public string npcId; public string dialogueId; public bool started; }
    public struct InteractEvent { public string objectId; }
    public struct SkillUsedEvent { public string skillId; }
    public struct PlayerActionEvent { public string action; }
    public struct ToastEvent { public string text; public int kind; }
    public struct ChoiceMadeEvent { public string key; public string value; }
    public struct FlagChangedEvent { public string key; }
    public struct MeditationEvent { public bool started; public bool atSpot; }
    public struct BreakthroughResultEvent { public bool success; public int realm; }
}
