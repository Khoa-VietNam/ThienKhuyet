using UnityEngine;

namespace ThienKhuyet.World
{
    public enum PoiKind
    {
        Clearing, Village, Hut, Terrace, Camp, Cave, Ruin, Lodge, Scholar, Valley, Crater, Road, Lake, Lair, Tomb, Spring, Hidden, Ground
    }

    public enum Biome { Meadow = 0, Forest, Pine, Bamboo, Blossom, Alpine, Snow, Wetland, Rocky }

    public sealed class PoiDef
    {
        public string id;
        public PoiKind kind;
        public Vector2 pos;
        public float radius = 20f;          // discovery / content radius
        public float flatten;               // plateau radius (0 = none)
        public float heightOffset;          // plateau height relative to the natural terrain at its centre
        public string nameKey;
        public bool hidden;                 // not shown on the map until discovered
        public string zone;
        public float yaw;

        public PoiDef(string id, PoiKind kind, float x, float z, float radius, float flatten, float offset, string zone, bool hidden = false, float yaw = 0f)
        {
            this.id = id;
            this.kind = kind;
            pos = new Vector2(x, z);
            this.radius = radius;
            this.flatten = flatten;
            heightOffset = offset;
            nameKey = "poi." + id;
            this.zone = zone;
            this.hidden = hidden;
            this.yaw = yaw;
        }
    }

    /// <summary>Hand-authored layout of the open world (Thiên Khuyết Giới – Tây Cốc). Coordinates in metres from the SW corner.</summary>
    public static class WorldLayout
    {
        public const float Size = 1024f;
        public const int HeightRes = 1025;
        public const float MaxHeight = 280f;
        public const float LakeLevel = 26f;
        public static readonly Vector2 LakeCenter = new Vector2(690f, 292f);
        public static readonly Vector2 LakeRadius = new Vector2(120f, 82f);
        public const float ChunkSize = 64f;
        public const int ChunkCount = 16;

        public static readonly PoiDef[] Pois =
        {
            new PoiDef("spawn", PoiKind.Clearing, 250f, 200f, 26f, 22f, 0f, "mi_lam"),
            new PoiDef("wolf_den", PoiKind.Lair, 245f, 335f, 30f, 20f, 0f, "mi_lam"),
            new PoiDef("forest_road", PoiKind.Road, 335f, 305f, 18f, 0f, 0f, "mi_lam"),
            new PoiDef("riverside_hut", PoiKind.Hut, 470f, 368f, 16f, 14f, 0f, "thanh_ha"),
            new PoiDef("thanh_ha", PoiKind.Village, 438f, 410f, 78f, 62f, 0f, "thanh_ha"),
            new PoiDef("training_ground", PoiKind.Ground, 392f, 448f, 20f, 16f, 0f, "thanh_ha"),
            new PoiDef("vein_terrace", PoiKind.Terrace, 548f, 528f, 26f, 22f, 7f, "linh_mach"),
            new PoiDef("kinh_ho", PoiKind.Lake, 640f, 330f, 40f, 0f, 0f, "kinh_ho"),
            new PoiDef("bandit_camp", PoiKind.Camp, 848f, 440f, 52f, 36f, 0f, "hac_phong"),
            new PoiDef("cold_cave", PoiKind.Cave, 505f, 742f, 30f, 20f, 0f, "thien_tru"),
            new PoiDef("jade_lodge", PoiKind.Lodge, 262f, 702f, 52f, 44f, 0f, "ngoc_hu"),
            new PoiDef("scholar_camp", PoiKind.Scholar, 884f, 664f, 40f, 30f, 0f, "van_tuong"),
            new PoiDef("ruin", PoiKind.Ruin, 780f, 898f, 66f, 58f, 0f, "vong_nguyet"),
            new PoiDef("alpha_lair", PoiKind.Lair, 112f, 470f, 34f, 24f, 0f, "mi_lam", false, 90f),
            new PoiDef("memory_road", PoiKind.Hidden, 112f, 640f, 28f, 18f, 0f, "ran_nut", true),
            new PoiDef("spirit_spring", PoiKind.Spring, 190f, 538f, 22f, 12f, 0f, "mi_lam", true),
            new PoiDef("rift_valley", PoiKind.Valley, 176f, 900f, 62f, 0f, 0f, "ran_nut"),
            new PoiDef("sword_tomb", PoiKind.Tomb, 952f, 872f, 34f, 22f, 0f, "thien_tru", true),
            new PoiDef("heart", PoiKind.Crater, 520f, 958f, 96f, 80f, -16f, "thien_khuyet")
        };

        /// <summary>River from the northern spring to the lake: x,z and the water surface height at each node.</summary>
        public static readonly Vector3[] River =
        {
            new Vector3(612f, 840f, 78f), new Vector3(596f, 756f, 70f), new Vector3(584f, 690f, 62f), new Vector3(560f, 622f, 54f),
            new Vector3(522f, 566f, 47f), new Vector3(492f, 520f, 42f), new Vector3(484f, 470f, 38f), new Vector3(500f, 424f, 34.5f),
            new Vector3(526f, 386f, 31.5f), new Vector3(562f, 352f, 29f), new Vector3(592f, 330f, LakeLevel)
        };

        public const float RiverHalfWidth = 5.5f;
        public const float RiverInfluence = 22f;

        /// <summary>Roads as POI id chains.</summary>
        public static readonly string[][] Roads =
        {
            new[] { "spawn", "forest_road", "riverside_hut", "thanh_ha" },
            new[] { "thanh_ha", "training_ground" },
            new[] { "thanh_ha", "vein_terrace" },
            new[] { "thanh_ha", "kinh_ho", "bandit_camp" },
            new[] { "vein_terrace", "scholar_camp" },
            new[] { "vein_terrace", "cold_cave" },
            new[] { "thanh_ha", "jade_lodge" },
            new[] { "cold_cave", "ruin" },
            new[] { "cold_cave", "heart" },
            new[] { "spawn", "wolf_den" }
        };

        public static PoiDef Poi(string id)
        {
            for (int i = 0; i < Pois.Length; i++) if (Pois[i].id == id) return Pois[i];
            return null;
        }

        public static int PoiIndex(string id)
        {
            for (int i = 0; i < Pois.Length; i++) if (Pois[i].id == id) return i;
            return -1;
        }
    }
}
