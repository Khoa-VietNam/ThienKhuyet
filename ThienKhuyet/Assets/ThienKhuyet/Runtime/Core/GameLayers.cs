using UnityEngine;

namespace ThienKhuyet.Core
{
    /// <summary>Physics layers. Names are registered in ProjectSettings/TagManager.asset (indices must match).</summary>
    public static class GameLayers
    {
        public const int Default = 0;
        public const int Player = 6;
        public const int Enemy = 7;
        public const int Npc = 8;
        public const int Projectile = 9;
        public const int Interactable = 10;
        public const int Pickup = 11;
        public const int Environment = 12;
        public const int Terrain = 13;
        public const int Hitbox = 14;
        public const int Cinematic = 15;

        public static readonly int GroundMask = (1 << Default) | (1 << Environment) | (1 << Terrain);
        public static readonly int ObstacleMask = (1 << Environment) | (1 << Terrain);
        public static readonly int CharacterMask = (1 << Player) | (1 << Enemy) | (1 << Npc);
        public static readonly int DamageableMask = (1 << Player) | (1 << Enemy);
        public static readonly int InteractMask = (1 << Interactable) | (1 << Npc) | (1 << Pickup);

        public static void SetRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++) SetRecursive(t.GetChild(i).gameObject, layer);
        }

        /// <summary>Configures the layer collision matrix at runtime so we do not depend on hand-edited physics settings.</summary>
        public static void ConfigurePhysics()
        {
            Physics.IgnoreLayerCollision(Projectile, Projectile, true);
            Physics.IgnoreLayerCollision(Projectile, Pickup, true);
            Physics.IgnoreLayerCollision(Projectile, Interactable, true);
            Physics.IgnoreLayerCollision(Pickup, Player, true);
            Physics.IgnoreLayerCollision(Pickup, Enemy, true);
            Physics.IgnoreLayerCollision(Pickup, Npc, true);
            Physics.IgnoreLayerCollision(Pickup, Pickup, true);
            Physics.IgnoreLayerCollision(Hitbox, Hitbox, true);
            Physics.IgnoreLayerCollision(Cinematic, Default, true);
            Physics.IgnoreLayerCollision(Cinematic, Player, true);
            Physics.IgnoreLayerCollision(Cinematic, Enemy, true);
        }
    }

    public static class GameTags
    {
        public const string Player = "Player";
        public const string MainCamera = "MainCamera";
    }
}
