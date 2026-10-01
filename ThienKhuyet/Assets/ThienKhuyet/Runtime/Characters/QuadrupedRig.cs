using System.Collections.Generic;
using ThienKhuyet.Data;
using UnityEngine;

namespace ThienKhuyet.Characters
{
    /// <summary>Bind-pose skeleton of the procedural four-legged beast (wolf / boar), metres, facing +Z.</summary>
    public static class QuadrupedDims
    {
        public static readonly int[] Parent = { -1, 0, 1, 2, 3, 4, 0, 6, 7, 2, 9, 10, 2, 12, 13, 0, 15, 16, 0, 18, 19 };

        public static readonly Vector3[] LocalOffset =
        {
            new Vector3(0f, 0.62f, -0.30f),   // Bd
            new Vector3(0f, 0.02f, 0.28f),    // Sp
            new Vector3(0f, 0.04f, 0.28f),    // Ch
            new Vector3(0f, 0.07f, 0.16f),    // Nk
            new Vector3(0f, 0.03f, 0.10f),    // Hd
            new Vector3(0f, -0.035f, 0.065f), // Jw
            new Vector3(0f, 0.05f, -0.14f),   // T1 (rel Bd)
            new Vector3(0f, 0f, -0.18f),      // T2
            new Vector3(0f, -0.05f, -0.16f),  // T3
            new Vector3(0.09f, -0.06f, 0f),   // FuR (rel Ch)
            new Vector3(0f, -0.28f, 0f),      // FlR
            new Vector3(0f, -0.30f, 0f),      // FpR
            new Vector3(-0.09f, -0.06f, 0f),  // FuL
            new Vector3(0f, -0.28f, 0f),      // FlL
            new Vector3(0f, -0.30f, 0f),      // FpL
            new Vector3(0.09f, -0.04f, 0f),   // HuR (rel Bd)
            new Vector3(0f, -0.28f, -0.03f),  // HlR
            new Vector3(0f, -0.26f, 0.03f),   // HpR
            new Vector3(-0.09f, -0.04f, 0f),  // HuL
            new Vector3(0f, -0.28f, -0.03f),  // HlL
            new Vector3(0f, -0.26f, 0.03f)    // HpL
        };

        static Vector3[] bind;

        public static Vector3[] BindPositions
        {
            get
            {
                if (bind != null) return bind;
                bind = new Vector3[LocalOffset.Length];
                for (int i = 0; i < bind.Length; i++) bind[i] = (Parent[i] < 0 ? Vector3.zero : bind[Parent[i]]) + LocalOffset[i];
                return bind;
            }
        }
    }

    public sealed class QuadrupedRig : MonoBehaviour
    {
        public Transform model;
        public Transform[] joints = new Transform[21];
        public SkinnedMeshRenderer body;
        public readonly List<Renderer> extraRenderers = new List<Renderer>();
        public CharacterAppearance appearance;
        public Transform headBone;

        public Vector3 HeadWorldPosition => headBone != null ? headBone.position : transform.position + Vector3.up;
    }
}
