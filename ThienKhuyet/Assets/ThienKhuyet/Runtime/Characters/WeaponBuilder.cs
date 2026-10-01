using ThienKhuyet.Data;
using ThienKhuyet.Gfx;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThienKhuyet.Characters
{
    /// <summary>
    /// Procedural weapon models attached to the hand sockets. Weapon local +Z runs along the forearm direction,
    /// the blade tip carries a TrailRenderer that combat enables during the active frames of an attack.
    /// </summary>
    public static class WeaponBuilder
    {
        static Material trailMat;
        static Texture2D trailTex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            trailMat = null;
            trailTex = null;
        }

        public static void Detach(HumanoidRig rig)
        {
            if (rig.weaponObject != null) Object.Destroy(rig.weaponObject);
            if (rig.weaponObjectL != null) Object.Destroy(rig.weaponObjectL);
            rig.weaponObject = null;
            rig.weaponObjectL = null;
            rig.weaponTip = null;
            rig.hasWeapon = false;
            rig.weaponFamily = WeaponFamily.Fist;
        }

        /// <summary>Attaches (or replaces) the weapon. 'main' is the blade/metal or wood colour, 'trim' the guard/ornament colour.</summary>
        public static void Attach(HumanoidRig rig, WeaponFamily family, Color main, Color trim)
        {
            Detach(rig);
            var root = new GameObject("Weapon_" + family);
            rig.weaponObject = root;
            rig.weaponFamily = family;
            rig.hasWeapon = true;

            var mb = new MeshBuilder();
            Color wood = new Color(0.42f, 0.28f, 0.16f);
            Color leather = new Color(0.22f, 0.15f, 0.12f);
            Vector3 tipPos = Vector3.zero;
            Transform parent = rig.socketR;

            switch (family)
            {
                case WeaponFamily.Sword:
                    Grip(mb, wood, trim, -0.13f, 0.045f);
                    Guard(mb, trim, 0.05f, 0.12f);
                    Blade(mb, 0.06f, 0.78f, 0.027f, 0.0065f, 0.8f, 0.13f, main);
                    tipPos = new Vector3(0f, 0f, 0.97f);
                    break;
                case WeaponFamily.Blade:
                    Grip(mb, leather, trim, -0.15f, 0.05f);
                    Guard(mb, trim, 0.06f, 0.09f);
                    CurvedBlade(mb, 0.07f, 0.8f, 0.034f, 0.008f, main);
                    tipPos = new Vector3(0f, 0.09f, 0.9f);
                    break;
                case WeaponFamily.Spear:
                    Shaft(mb, wood, -0.55f, 1.2f, 0.014f);
                    mb.Sub(1).PaletteUv(Palette.Uv(main));
                    mb.Push(new Vector3(0f, 0f, 1.2f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.036f, 1f, 0.0075f));
                    mb.Cylinder(Vector3.zero, 0.2f, 1f, 0.85f, 4, true, false);
                    mb.Cylinder(new Vector3(0f, 0.2f, 0f), 0.14f, 0.85f, 0f, 4, false, false);
                    mb.Pop();
                    mb.Sub(0).PaletteUv(Palette.Uv(trim));
                    mb.Sphere(new Vector3(0f, 0f, 1.19f), 0.022f, 8, 5);
                    mb.Limb(new Vector3(0f, 0f, 1.18f), new Vector3(0f, -0.03f, 1.02f), 0.026f, 0.006f, 7, false, true);   // tassel
                    tipPos = new Vector3(0f, 0f, 1.55f);
                    break;
                case WeaponFamily.Staff:
                    Shaft(mb, main, -0.55f, 1.0f, 0.017f);
                    mb.Sub(1).PaletteUv(Palette.Uv(trim));
                    mb.Push(new Vector3(0f, 0f, 1.05f), Quaternion.Euler(0f, 90f, 0f), Vector3.one);
                    mb.Torus(Vector3.zero, 0.055f, 0.009f, 16, 6);
                    mb.Pop();
                    mb.Sub(0).PaletteUv(Palette.Uv(new Color(0.75f, 0.95f, 1f)));
                    mb.Sphere(new Vector3(0f, 0f, 1.05f), 0.032f, 8, 6);
                    tipPos = new Vector3(0f, 0f, 1.12f);
                    break;
                case WeaponFamily.Fist:
                default:
                    // knuckle guards on both hands
                    mb.Sub(1).PaletteUv(Palette.Uv(main));
                    mb.Box(new Vector3(0f, 0f, 0.07f), new Vector3(0.085f, 0.04f, 0.05f));
                    for (int i = 0; i < 4; i++) mb.Sphere(new Vector3(-0.03f + i * 0.02f, 0.022f, 0.095f), 0.011f, 5, 4);
                    mb.Sub(0).PaletteUv(Palette.Uv(trim));
                    mb.Seg_(new Vector3(0f, 0f, -0.04f), new Vector3(0f, 0f, 0.0f), 0.03f);
                    tipPos = new Vector3(0f, 0f, 0.13f);
                    break;
            }

            var mf = root.AddComponent<MeshFilter>();
            mf.sharedMesh = mb.ToMesh("weapon_" + family, false);
            var mr = root.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { Palette.Standard, Palette.Metal };
            mr.shadowCastingMode = ShadowCastingMode.On;
            Palette.Flush();
            root.transform.SetParent(parent, false);

            var tip = new GameObject("Tip").transform;
            tip.SetParent(root.transform, false);
            tip.localPosition = tipPos;
            rig.weaponTip = tip;
            AddTrail(tip.gameObject, family == WeaponFamily.Fist ? new Color(1f, 0.85f, 0.6f, 0.5f) : new Color(0.8f, 0.95f, 1f, 0.6f), family == WeaponFamily.Fist ? 0.1f : 0.07f);

            if (family == WeaponFamily.Fist)
            {
                // the left hand gets a matching guard
                var left = new GameObject("Weapon_FistL");
                left.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var lmr = left.AddComponent<MeshRenderer>();
                lmr.sharedMaterials = mr.sharedMaterials;
                left.transform.SetParent(rig.socketL, false);
                rig.weaponObjectL = left;
            }
        }

        /// <summary>Short bow held in the left hand (archers). Limbs run along the socket's local Y, the string faces the archer.</summary>
        public static void AttachBow(HumanoidRig rig, Color wood)
        {
            if (rig.weaponObjectL != null) Object.Destroy(rig.weaponObjectL);
            var go = new GameObject("Bow");
            var mb = new MeshBuilder();
            mb.Sub(0).PaletteUv(Palette.Uv(wood));
            Vector3[] pts =
            {
                new Vector3(0f, -0.62f, -0.17f), new Vector3(0f, -0.36f, -0.07f), new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0.36f, -0.07f), new Vector3(0f, 0.62f, -0.17f)
            };
            for (int i = 0; i < pts.Length - 1; i++) mb.Limb(pts[i], pts[i + 1], 0.016f, 0.013f, 6, true, true);
            mb.Sub(0).PaletteUv(Palette.Uv(new Color(0.9f, 0.88f, 0.8f)));
            mb.Limb(pts[0], pts[pts.Length - 1], 0.0035f, 0.0035f, 4, false, false);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("bow", false);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { Palette.Standard, Palette.Metal };
            Palette.Flush();
            go.transform.SetParent(rig.socketL, false);
            rig.weaponObjectL = go;
        }

        // ------------------------------------------------------------------ parts
        static void Grip(MeshBuilder mb, Color grip, Color pommel, float z0, float z1)
        {
            mb.Sub(0).PaletteUv(Palette.Uv(grip));
            mb.Limb(new Vector3(0f, 0f, z0), new Vector3(0f, 0f, z1), 0.016f, 0.016f, 7, false, false);
            mb.Sub(1).PaletteUv(Palette.Uv(pommel));
            mb.Sphere(new Vector3(0f, 0f, z0 - 0.005f), 0.023f, 8, 6);
        }

        static void Guard(MeshBuilder mb, Color col, float z, float width)
        {
            mb.Sub(1).PaletteUv(Palette.Uv(col));
            mb.Box(new Vector3(0f, 0f, z), new Vector3(width, 0.016f, 0.024f));
            mb.Sphere(new Vector3(width * 0.5f, 0f, z), 0.014f, 6, 4);
            mb.Sphere(new Vector3(-width * 0.5f, 0f, z), 0.014f, 6, 4);
        }

        static void Blade(MeshBuilder mb, float z0, float length, float halfW, float halfT, float taper, float tipLen, Color col)
        {
            mb.Sub(1).PaletteUv(Palette.Uv(col));
            mb.Push(new Vector3(0f, 0f, z0), Quaternion.Euler(90f, 0f, 0f), new Vector3(halfW, 1f, halfT));
            mb.Cylinder(Vector3.zero, length, 1f, taper, 4, true, false);
            mb.Cylinder(new Vector3(0f, length, 0f), tipLen, taper, 0f, 4, false, false);
            mb.Pop();
            // bright edge line
            mb.PaletteUv(Palette.Uv(Color.Lerp(col, Color.white, 0.6f)));
            mb.Box(new Vector3(0f, 0f, z0 + length * 0.5f), new Vector3(0.004f, halfT * 2.3f, length * 0.96f));
        }

        static void CurvedBlade(MeshBuilder mb, float z0, float length, float halfW, float halfT, Color col)
        {
            mb.Sub(1).PaletteUv(Palette.Uv(col));
            int segs = 5;
            float segLen = length / segs;
            Vector3 p = new Vector3(0f, 0f, z0);
            float ang = 0f;
            for (int i = 0; i < segs; i++)
            {
                float w = Mathf.Lerp(halfW, halfW * 0.7f, (float)i / segs);
                float w2 = Mathf.Lerp(halfW, halfW * 0.7f, (float)(i + 1) / segs);
                mb.Push(p, Quaternion.Euler(90f - ang, 0f, 0f), new Vector3(w, 1f, halfT));
                mb.Cylinder(Vector3.zero, segLen * 1.05f, 1f, w2 / w, 4, i == 0, false);
                mb.Pop();
                Vector3 dir = new Vector3(0f, Mathf.Sin(ang * Mathf.Deg2Rad), Mathf.Cos(ang * Mathf.Deg2Rad));
                p += dir * segLen;
                ang += 4.5f;
            }
            mb.Push(p, Quaternion.Euler(90f - ang, 0f, 0f), new Vector3(halfW * 0.7f, 1f, halfT));
            mb.Cylinder(Vector3.zero, 0.1f, 1f, 0f, 4, false, false);
            mb.Pop();
        }

        static void Shaft(MeshBuilder mb, Color col, float z0, float z1, float r)
        {
            mb.Sub(0).PaletteUv(Palette.Uv(col));
            mb.Limb(new Vector3(0f, 0f, z0), new Vector3(0f, 0f, z1), r, r, 6, true, true);
        }

        static void Seg_(this MeshBuilder mb, Vector3 a, Vector3 b, float r)
        {
            mb.Limb(a, b, r, r, 6, true, true);
        }

        // ------------------------------------------------------------------ trail
        static void AddTrail(GameObject tip, Color color, float width)
        {
            if (trailMat == null)
            {
                trailTex = ProcTex.Streak(64);
                trailMat = Mats.Particle(trailTex, Color.white, true);
                trailMat.name = "WeaponTrail";
            }
            var tr = tip.AddComponent<TrailRenderer>();
            tr.sharedMaterial = trailMat;
            tr.time = 0.16f;
            tr.minVertexDistance = 0.04f;
            tr.widthMultiplier = width;
            tr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(color.r, color.g, color.b), 0f), new GradientColorKey(new Color(color.r, color.g, color.b), 1f) },
                new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = g;
            tr.textureMode = LineTextureMode.Stretch;
            tr.alignment = LineAlignment.View;
            tr.shadowCastingMode = ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.emitting = false;
            tr.numCapVertices = 2;
        }
    }
}
