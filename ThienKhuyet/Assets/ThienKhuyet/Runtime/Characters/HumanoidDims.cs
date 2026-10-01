using UnityEngine;

namespace ThienKhuyet.Characters
{
    /// <summary>
    /// Bind-pose skeleton of the procedural humanoid (metres, model space, feet at y=0, facing +Z).
    /// The same numbers are mirrored in Tools/posepreview/preview.py so animation can be reviewed offline.
    /// </summary>
    public static class HumanoidDims
    {
        public static readonly int[] Parent = { -1, 0, 1, 2, 3, 2, 5, 6, 2, 8, 9, 0, 11, 12, 0, 14, 15 };

        public static readonly Vector3[] LocalOffset =
        {
            new Vector3(0f, 0.93f, 0f),        // Hp (relative to the model root)
            new Vector3(0f, 0.07f, 0f),        // Sp
            new Vector3(0f, 0.17f, 0f),        // Ch
            new Vector3(0f, 0.27f, 0f),        // Nk
            new Vector3(0f, 0.06f, 0f),        // Hd
            new Vector3(0.19f, 0.24f, 0f),     // ArR (relative to Ch)
            new Vector3(0f, -0.29f, 0f),       // FoR
            new Vector3(0f, -0.26f, 0f),       // HaR
            new Vector3(-0.19f, 0.24f, 0f),    // ArL
            new Vector3(0f, -0.29f, 0f),       // FoL
            new Vector3(0f, -0.26f, 0f),       // HaL
            new Vector3(0.09f, -0.03f, 0f),    // ThR (relative to Hp)
            new Vector3(0f, -0.45f, 0f),       // KnR
            new Vector3(0f, -0.41f, 0f),       // AnR
            new Vector3(-0.09f, -0.03f, 0f),   // ThL
            new Vector3(0f, -0.45f, 0f),       // KnL
            new Vector3(0f, -0.41f, 0f)        // AnL
        };

        static Vector3[] bind;

        /// <summary>Joint positions in model space for the zero pose.</summary>
        public static Vector3[] BindPositions
        {
            get
            {
                if (bind != null) return bind;
                bind = new Vector3[LocalOffset.Length];
                for (int i = 0; i < bind.Length; i++)
                    bind[i] = (Parent[i] < 0 ? Vector3.zero : bind[Parent[i]]) + LocalOffset[i];
                return bind;
            }
        }

        public const float HeadCenterY = 1.63f;
        public const float StandingHeight = 1.75f;
    }
}
