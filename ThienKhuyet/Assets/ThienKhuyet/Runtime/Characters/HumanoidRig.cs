using System.Collections.Generic;
using ThienKhuyet.Data;
using UnityEngine;

namespace ThienKhuyet.Characters
{
    /// <summary>A bone that lags behind its parent's motion (hair, skirts, cloaks): cheap secondary animation.</summary>
    public sealed class SpringBone
    {
        public Transform t;
        public Quaternion rest;
        public float stiffness = 70f;
        public float damping = 9f;
        public float influence = 1f;
        public float maxAngle = 40f;
        public Vector2 angle;     // pitch (x), roll (z) in degrees
        public Vector2 velocity;
    }

    /// <summary>References to everything animators, weapons and cutscenes need on a procedural humanoid.</summary>
    public sealed class HumanoidRig : MonoBehaviour
    {
        public Transform model;
        public Transform[] joints = new Transform[17];
        public Transform socketR, socketL;
        public Transform weaponTip;           // set by WeaponBuilder (blade end / fist), used for trails
        public Transform headBone;
        public Transform eyeL, eyeR, browL, browR, mouth;
        public SkinnedMeshRenderer body;
        public readonly List<SpringBone> springs = new List<SpringBone>();
        public readonly List<Renderer> extraRenderers = new List<Renderer>();
        public CharacterAppearance appearance;
        public GameObject weaponObject;
        public GameObject weaponObjectL;   // off-hand piece (fist guards)
        public WeaponFamily weaponFamily = WeaponFamily.Fist;
        public bool hasWeapon;

        public Vector3 HeadWorldPosition => headBone != null ? headBone.position + Vector3.up * 0.12f * model.lossyScale.y : transform.position + Vector3.up * 1.7f;
        public float Height => HumanoidDims.StandingHeight * (appearance != null ? appearance.height : 1f);
    }
}
