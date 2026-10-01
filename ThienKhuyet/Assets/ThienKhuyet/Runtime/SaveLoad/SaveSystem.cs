using System;
using System.Collections.Generic;
using System.IO;
using ThienKhuyet.Combat;
using ThienKhuyet.Core;
using ThienKhuyet.Data;
using ThienKhuyet.Items;
using ThienKhuyet.Narrative;
using ThienKhuyet.Player;
using ThienKhuyet.World;
using UnityEngine;

namespace ThienKhuyet.SaveLoad
{
    /// <summary>Small display model used by the save/load screen.</summary>
    [Serializable]
    public sealed class SaveMeta
    {
        public int slot;
        public int realm;
        public int stage;
        public double playSeconds;
        public string location;
        public string when;
        public string ending;
    }

    [Serializable]
    public sealed class PlayerSaveData
    {
        public int realm;
        public int stage = 1;
        public float exp;
        public int path;
        public int vitality = 5;
        public int strength = 5;
        public int spirit = 5;
        public int agility = 5;
        public int insight = 5;
        public int unspent;
        public ItemStack[] inventory = new ItemStack[0];
        public string[] equipment = new string[0];
        public string[] learnedSkills = new string[0];
        public string[] skillSlots = new string[PlayerState.SkillSlots];
        public string[] unlocks = new string[0];
        public float hp;
        public float stamina;
        public float qi;
    }

    /// <summary>Unity-serializable snapshot of a play session. Collections are arrays because JsonUtility omits dictionaries.</summary>
    [Serializable]
    public sealed class GameSaveData
    {
        public int schemaVersion = 1;
        public string savedAt;
        public string location = "spawn";
        public string respawnPoint = "spawn_player";
        public int seed = 20260;
        public int act = 1;
        public float timeOfDay = 7.5f;
        public int day = 1;
        public double playSeconds;
        public int deaths;
        public string endingId = "";
        public Vector3 playerPosition;
        public float playerYaw;
        public bool hasPlayerPosition;
        public PlayerSaveData player = new PlayerSaveData();
        public GameFlagsData flags = new GameFlagsData();
        public QuestSave[] quests = new QuestSave[0];
        public string trackedQuest = "";
        public string[] killedSpawns = new string[0];
        public string[] enemyKillIds = new string[0];
        public int[] enemyKillCounts = new int[0];
        public string[] openedContainers = new string[0];
        public string[] discoveredPois = new string[0];
        public string[] seenCutscenes = new string[0];
        public string[] nodeRespawnKeys = new string[0];
        public double[] nodeRespawnValues = new double[0];
        public byte[] fog = new byte[0];
        public int fogResolution;
    }

    /// <summary>
    /// Versioned local save slots. Writes through a temporary file before replacing the old slot so a failed save does not
    /// corrupt the previous file. Slot zero is reserved for autosaves.
    /// </summary>
    public static class SaveSystem
    {
        public const int SlotCount = 4;
        const int CurrentSchema = 2;
        const string FolderName = "Saves";

        static string FolderPath => Path.Combine(Application.persistentDataPath, FolderName);

        static string SlotPath(int slot)
        {
            if (slot < 0 || slot >= SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            return Path.Combine(FolderPath, "slot_" + slot + ".json");
        }

        public static bool HasSave(int slot)
        {
            try { return File.Exists(SlotPath(slot)); }
            catch (ArgumentOutOfRangeException) { return false; }
        }

        public static SaveMeta ReadMeta(int slot)
        {
            GameSaveData data = Read(slot);
            if (data == null || data.player == null) return null;
            return new SaveMeta
            {
                slot = slot,
                realm = data.player.realm,
                stage = data.player.stage,
                playSeconds = data.playSeconds,
                location = string.IsNullOrEmpty(data.location) ? "spawn" : data.location,
                when = string.IsNullOrEmpty(data.savedAt) ? "" : data.savedAt,
                ending = data.endingId ?? ""
            };
        }

        public static GameSaveData Read(int slot)
        {
            string path;
            try { path = SlotPath(slot); }
            catch (ArgumentOutOfRangeException) { return null; }
            if (!File.Exists(path)) return null;
            try
            {
                string json = File.ReadAllText(path);
                GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);
                if (data == null || data.schemaVersion < 1 || data.schemaVersion > CurrentSchema || data.player == null)
                {
                    Debug.LogWarning("[SaveSystem] Unsupported or incomplete save in slot " + slot + ".");
                    return null;
                }
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SaveSystem] Could not read slot " + slot + ": " + ex.Message);
                return null;
            }
        }

        public static bool Write(int slot, GameSession session, Vector3 playerPosition, float playerYaw, Vitals vitals, string location, WorldManager world)
        {
            if (session == null || session.player == null) return false;
            string path;
            try { path = SlotPath(slot); }
            catch (ArgumentOutOfRangeException) { return false; }

            try
            {
                GameSaveData data = Capture(session, playerPosition, playerYaw, vitals, location, world);
                string json = JsonUtility.ToJson(data, true);
                Directory.CreateDirectory(FolderPath);
                string temporary = path + ".tmp";
                File.WriteAllText(temporary, json);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[SaveSystem] Could not write slot " + slot + ": " + ex.Message);
                return false;
            }
        }

        static GameSaveData Capture(GameSession session, Vector3 position, float yaw, Vitals vitals, string location, WorldManager world)
        {
            PlayerState player = session.player;
            var p = new PlayerSaveData
            {
                realm = player.cultivation.Realm,
                stage = player.cultivation.Stage,
                exp = player.cultivation.Exp,
                path = (int)player.path,
                vitality = player.attributes.vitality,
                strength = player.attributes.strength,
                spirit = player.attributes.spirit,
                agility = player.attributes.agility,
                insight = player.attributes.insight,
                unspent = player.attributes.unspent,
                inventory = player.inventory.ToArray(),
                equipment = player.equipment.ToArray(),
                learnedSkills = player.learnedSkills.ToArray(),
                skillSlots = (string[])player.skillSlots.Clone(),
                unlocks = new List<string>(player.unlocks).ToArray(),
                hp = vitals != null ? vitals.hp : 0f,
                stamina = vitals != null ? vitals.stamina : 0f,
                qi = vitals != null ? vitals.qi : 0f
            };

            var data = new GameSaveData
            {
                schemaVersion = CurrentSchema,
                savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                location = string.IsNullOrEmpty(location) ? "spawn" : location,
                respawnPoint = session.respawnPoint,
                seed = session.seed,
                act = session.act,
                timeOfDay = session.timeOfDay,
                day = session.day,
                playSeconds = session.playSeconds,
                deaths = session.deaths,
                endingId = session.endingId,
                playerPosition = position,
                playerYaw = yaw,
                hasPlayerPosition = true,
                player = p,
                flags = session.flags.ToData(),
                quests = session.quests != null ? session.quests.ToSave().ToArray() : new QuestSave[0],
                trackedQuest = session.quests != null ? session.quests.TrackedId : "",
                killedSpawns = new List<string>(session.killedSpawns).ToArray(),
                openedContainers = new List<string>(session.openedContainers).ToArray(),
                discoveredPois = new List<string>(session.discoveredPois).ToArray(),
                seenCutscenes = new List<string>(session.seenCutscenes).ToArray(),
                fogResolution = world != null ? WorldManager.FogRes : 0,
                fog = world != null ? (byte[])world.Fog.Clone() : new byte[0]
            };

            var killKeys = new List<string>(session.enemyKillCounts.Keys);
            killKeys.Sort(StringComparer.Ordinal);
            data.enemyKillIds = killKeys.ToArray();
            data.enemyKillCounts = new int[killKeys.Count];
            for (int i = 0; i < killKeys.Count; i++) data.enemyKillCounts[i] = Mathf.Max(0, session.enemyKillCounts[killKeys[i]]);

            var keys = new List<string>(session.nodeRespawn.Keys);
            keys.Sort(StringComparer.Ordinal);
            data.nodeRespawnKeys = keys.ToArray();
            data.nodeRespawnValues = new double[keys.Count];
            for (int i = 0; i < keys.Count; i++) data.nodeRespawnValues[i] = session.nodeRespawn[keys[i]];
            return data;
        }

        /// <summary>Restores gameplay state into an already-created session; world objects read these collections on build.</summary>
        public static void Apply(GameSaveData data, GameSession session)
        {
            if (data == null || session == null || session.player == null) return;
            PlayerState player = session.player;
            PlayerSaveData p = data.player ?? new PlayerSaveData();

            player.Suspend(true);
            player.cultivation.Set(p.realm, p.stage, p.exp);
            player.path = (CultivationPath)Mathf.Clamp(p.path, (int)CultivationPath.None, (int)CultivationPath.Hybrid);
            player.attributes.vitality = Mathf.Max(1, p.vitality);
            player.attributes.strength = Mathf.Max(1, p.strength);
            player.attributes.spirit = Mathf.Max(1, p.spirit);
            player.attributes.agility = Mathf.Max(1, p.agility);
            player.attributes.insight = Mathf.Max(1, p.insight);
            player.attributes.unspent = Mathf.Max(0, p.unspent);
            player.inventory.FromArray(p.inventory);
            player.equipment.FromArray(p.equipment);
            player.learnedSkills.Clear();
            if (p.learnedSkills != null)
                for (int i = 0; i < p.learnedSkills.Length; i++)
                    if (ContentDB.Skill(p.learnedSkills[i]) != null && !player.learnedSkills.Contains(p.learnedSkills[i])) player.learnedSkills.Add(p.learnedSkills[i]);
            for (int i = 0; i < player.skillSlots.Length; i++)
                player.skillSlots[i] = p.skillSlots != null && i < p.skillSlots.Length && player.KnowsSkill(p.skillSlots[i]) ? p.skillSlots[i] : string.Empty;
            player.unlocks.Clear();
            if (p.unlocks != null)
                for (int i = 0; i < p.unlocks.Length; i++) if (!string.IsNullOrEmpty(p.unlocks[i])) player.unlocks.Add(p.unlocks[i]);
            player.Suspend(false);

            session.flags.FromData(data.flags);
            session.seed = data.seed;
            session.act = Mathf.Clamp(data.act, 1, 4);
            session.timeOfDay = Mathf.Repeat(data.timeOfDay, 24f);
            session.day = Mathf.Max(1, data.day);
            session.playSeconds = Math.Max(0.0, data.playSeconds);
            session.deaths = Mathf.Max(0, data.deaths);
            session.respawnPoint = string.IsNullOrEmpty(data.respawnPoint) ? "spawn_player" : data.respawnPoint;
            session.endingId = data.endingId ?? "";
            session.killedSpawns.Clear();
            session.enemyKillCounts.Clear();
            session.openedContainers.Clear();
            session.discoveredPois.Clear();
            session.seenCutscenes.Clear();
            session.nodeRespawn.Clear();
            Copy(data.killedSpawns, session.killedSpawns);
            if (data.enemyKillIds != null && data.enemyKillCounts != null)
                for (int i = 0; i < data.enemyKillIds.Length && i < data.enemyKillCounts.Length; i++)
                    if (!string.IsNullOrEmpty(data.enemyKillIds[i])) session.enemyKillCounts[data.enemyKillIds[i]] = Mathf.Max(0, data.enemyKillCounts[i]);
            Copy(data.openedContainers, session.openedContainers);
            Copy(data.discoveredPois, session.discoveredPois);
            Copy(data.seenCutscenes, session.seenCutscenes);
            if (data.nodeRespawnKeys != null && data.nodeRespawnValues != null)
                for (int i = 0; i < data.nodeRespawnKeys.Length && i < data.nodeRespawnValues.Length; i++)
                    if (!string.IsNullOrEmpty(data.nodeRespawnKeys[i])) session.nodeRespawn[data.nodeRespawnKeys[i]] = data.nodeRespawnValues[i];
            if (session.quests != null) session.quests.FromSave(data.quests != null ? new List<QuestSave>(data.quests) : new List<QuestSave>(), data.trackedQuest);
        }

        static void Copy(string[] source, HashSet<string> destination)
        {
            if (source == null) return;
            for (int i = 0; i < source.Length; i++) if (!string.IsNullOrEmpty(source[i])) destination.Add(source[i]);
        }

        public static void Delete(int slot)
        {
            try
            {
                string path = SlotPath(slot);
                if (File.Exists(path)) File.Delete(path);
                string temporary = path + ".tmp";
                if (File.Exists(temporary)) File.Delete(temporary);
            }
            catch (Exception ex) { Debug.LogWarning("[SaveSystem] Could not delete slot " + slot + ": " + ex.Message); }
        }
    }
}
