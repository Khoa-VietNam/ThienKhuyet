using System.Collections.Generic;
using ThienKhuyet.Core;
using ThienKhuyet.Data;
using ThienKhuyet.Gfx;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThienKhuyet.Characters
{
    /// <summary>
    /// Builds a procedural humanoid as ONE skinned mesh (rigid bone binding) with a palette material: ~1 draw call per character.
    /// Outfits, hair and accessories are generated from <see cref="CharacterAppearance"/>; no imported assets are used.
    /// </summary>
    public static class HumanoidBuilder
    {
        static readonly string[] JointNames =
        {
            "Hips", "Spine", "Chest", "Neck", "Head", "UpperArmR", "ForeArmR", "HandR", "UpperArmL", "ForeArmL", "HandL",
            "ThighR", "ShinR", "FootR", "ThighL", "ShinL", "FootL"
        };

        sealed class Ctx
        {
            public readonly MeshBuilder mb = new MeshBuilder(true);
            public readonly List<Transform> bones = new List<Transform>();
            public readonly List<Vector3> bind = new List<Vector3>();
            public readonly List<string> names = new List<string>();
            public Transform root;
            public HumanoidRig rig;
            public CharacterAppearance a;
            public float k;   // bulk factor

            public int AddBone(string name, int parent, Vector3 modelPos)
            {
                var go = new GameObject(name);
                Transform p = parent >= 0 ? bones[parent] : root;
                go.transform.SetParent(p, false);
                go.transform.localPosition = modelPos - (parent >= 0 ? bind[parent] : Vector3.zero);
                bones.Add(go.transform);
                bind.Add(modelPos);
                names.Add(name);
                return bones.Count - 1;
            }

            void Use(int bone, Color c, bool metal)
            {
                mb.Bone(bone).Sub(metal ? 1 : 0).PaletteUv(Palette.Uv(c));
            }

            public void Ell(int bone, Vector3 c, Vector3 r, Color col, bool metal = false, int seg = 10, int rings = 7)
            {
                Use(bone, col, metal);
                mb.Ellipsoid(c, r, seg, rings);
            }

            public void Seg(int bone, Vector3 a, Vector3 b, float r0, float r1, Color col, bool metal = false, int seg = 8, bool rs = true, bool re = true)
            {
                Use(bone, col, metal);
                mb.Limb(a, b, r0, r1, seg, rs, re);
            }

            public void Bx(int bone, Vector3 c, Vector3 size, Color col, bool metal = false)
            {
                Use(bone, col, metal);
                mb.Box(c, size);
            }

            public void Cyl(int bone, Vector3 baseC, float h, float r0, float r1, Color col, bool metal = false, int seg = 10, bool capB = true, bool capT = true)
            {
                Use(bone, col, metal);
                mb.Cylinder(baseC, h, r0, r1, seg, capB, capT);
            }

            public void Cone(int bone, Vector3 baseC, float h, float r, Color col, int seg = 10, bool metal = false)
            {
                Use(bone, col, metal);
                mb.Cone(baseC, h, r, seg, true);
            }

            public void Lathe(int bone, Vector3 baseC, Vector2[] profile, Color col, float zScale = 1f, bool metal = false, int seg = 14)
            {
                Use(bone, col, metal);
                mb.Push(Vector3.zero, Quaternion.identity, new Vector3(1f, 1f, zScale));
                mb.Lathe(baseC, profile, seg);
                mb.Pop();
            }

            public void Torus(int bone, Vector3 c, float major, float minor, Color col, Vector3 euler, bool metal = false, int seg = 16)
            {
                Use(bone, col, metal);
                mb.Push(c, Quaternion.Euler(euler), Vector3.one);
                mb.Torus(Vector3.zero, major, minor, seg, 6);
                mb.Pop();
            }

            /// <summary>Box rotated around its centre (used for collars, ribbons, straps).</summary>
            public void BxRot(int bone, Vector3 c, Vector3 size, Vector3 euler, Color col, bool metal = false)
            {
                Use(bone, col, metal);
                mb.Push(c, Quaternion.Euler(euler), Vector3.one);
                mb.Box(Vector3.zero, size);
                mb.Pop();
            }
        }

        static Color Dk(Color c, float f)
        {
            return new Color(c.r * f, c.g * f, c.b * f, 1f);
        }

        static Color Lt(Color c, float f)
        {
            return Color.Lerp(c, Color.white, f);
        }

        // bone indices of the 17 core joints
        const int Hp = 0, Sp = 1, Ch = 2, Nk = 3, Hd = 4, ArR = 5, FoR = 6, HaR = 7, ArL = 8, FoL = 9, HaL = 10, ThR = 11, KnR = 12, AnR = 13, ThL = 14, KnL = 15, AnL = 16;

        public static HumanoidRig Build(CharacterAppearance a, Transform parent, string name = "Model")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * a.height;
            var rig = go.AddComponent<HumanoidRig>();
            rig.model = go.transform;
            rig.appearance = a;
            var c = new Ctx { root = go.transform, rig = rig, a = a, k = Mathf.Max(0.6f, a.bulk) };

            Vector3[] bp = HumanoidDims.BindPositions;
            for (int i = 0; i < JointNames.Length; i++) c.AddBone(JointNames[i], HumanoidDims.Parent[i], bp[i]);
            for (int i = 0; i < 17; i++) rig.joints[i] = c.bones[i];
            rig.headBone = c.bones[Hd];

            BuildFaceBones(c);
            BuildBody(c);
            BuildHairAndAccessories(c);
            FinishMesh(c, go);

            // weapon sockets: weapon local +Z maps to the hand's -Y (continuation of the forearm)
            rig.socketR = MakeSocket(rig.joints[HaR], "SocketR");
            rig.socketL = MakeSocket(rig.joints[HaL], "SocketL");
            return rig;
        }

        static Transform MakeSocket(Transform hand, string name)
        {
            var s = new GameObject(name).transform;
            s.SetParent(hand, false);
            s.localPosition = new Vector3(0f, -0.045f, 0.012f);
            s.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return s;
        }

        static void FinishMesh(Ctx c, GameObject go)
        {
            Mesh mesh = c.mb.ToMesh(go.name + "_mesh", false);
            var bind = new Matrix4x4[c.bones.Count];
            for (int i = 0; i < bind.Length; i++) bind[i] = Matrix4x4.Translate(-c.bind[i]);
            mesh.bindposes = bind;
            Palette.Flush();

            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = c.bones.ToArray();
            smr.rootBone = c.bones[Hp];
            smr.sharedMaterials = new[] { Palette.Standard, Palette.Metal };
            smr.shadowCastingMode = ShadowCastingMode.On;
            smr.receiveShadows = true;
            smr.updateWhenOffscreen = false;
            smr.localBounds = new Bounds(new Vector3(0f, 0.9f, 0f), new Vector3(1.6f, 2.2f, 1.6f));
            c.rig.body = smr;
        }

        // ------------------------------------------------------------------ face
        static void BuildFaceBones(Ctx c)
        {
            Color skin = c.a.skin;
            Color eye = new Color(0.05f, 0.04f, 0.05f);
            Color brow = Dk(c.a.hair.maxColorComponent < 0.15f ? c.a.hair : c.a.hair, 0.9f);
            Color mouth = Color.Lerp(skin, new Color(0.65f, 0.25f, 0.25f), 0.55f);

            int eyeL = c.AddBone("EyeL", Hd, new Vector3(-0.034f, 1.64f, 0.088f));
            int eyeR = c.AddBone("EyeR", Hd, new Vector3(0.034f, 1.64f, 0.088f));
            int browL = c.AddBone("BrowL", Hd, new Vector3(-0.036f, 1.672f, 0.082f));
            int browR = c.AddBone("BrowR", Hd, new Vector3(0.036f, 1.672f, 0.082f));
            int mouthB = c.AddBone("Mouth", Hd, new Vector3(0f, 1.585f, 0.088f));

            c.rig.eyeL = c.bones[eyeL];
            c.rig.eyeR = c.bones[eyeR];
            c.rig.browL = c.bones[browL];
            c.rig.browR = c.bones[browR];
            c.rig.mouth = c.bones[mouthB];

            if (c.a.outfit == OutfitStyle.Stone) return; // golems have no face

            c.Ell(eyeL, c.bind[eyeL], new Vector3(0.012f, 0.015f, 0.008f), eye, false, 8, 5);
            c.Ell(eyeR, c.bind[eyeR], new Vector3(0.012f, 0.015f, 0.008f), eye, false, 8, 5);
            c.Bx(browL, c.bind[browL], new Vector3(0.034f, 0.0065f, 0.01f), brow);
            c.Bx(browR, c.bind[browR], new Vector3(0.034f, 0.0065f, 0.01f), brow);
            c.Bx(mouthB, c.bind[mouthB], new Vector3(0.034f, 0.0065f, 0.008f), mouth);
        }

        // ------------------------------------------------------------------ body
        static void BuildBody(Ctx c)
        {
            CharacterAppearance a = c.a;
            Vector3[] bp = HumanoidDims.BindPositions;
            float k = c.k;
            Color skin = a.skin;

            // ---- head, neck, hands (always visible)
            Vector3 headC = new Vector3(0f, HumanoidDims.HeadCenterY, 0.005f);
            if (a.outfit == OutfitStyle.Stone)
            {
                c.Ell(Hd, headC, new Vector3(0.10f, 0.115f, 0.10f), skin, false, 8, 6);
                c.mb.Displace(0, 0.012f, 40f, 11);
                c.Cyl(Nk, new Vector3(0, 1.40f, 0), 0.12f, 0.07f * k, 0.065f * k, skin, false, 8);
            }
            else
            {
                c.Ell(Hd, headC, new Vector3(0.085f, 0.11f, 0.095f), skin);
                c.Ell(Hd, new Vector3(0f, 1.572f, 0.03f), new Vector3(0.062f, 0.06f, 0.068f), skin, false, 8, 5);   // jaw
                c.Ell(Hd, new Vector3(-0.087f, 1.63f, 0.0f), new Vector3(0.012f, 0.026f, 0.018f), skin, false, 6, 4); // ears
                c.Ell(Hd, new Vector3(0.087f, 1.63f, 0.0f), new Vector3(0.012f, 0.026f, 0.018f), skin, false, 6, 4);
                c.Use_Nose(Hd, new Vector3(0f, 1.618f, 0.088f), skin);
                c.Cyl(Nk, new Vector3(0, 1.405f, 0), 0.115f, 0.047f * k, 0.042f * k, skin, false, 8);
            }
            for (int s = 0; s < 2; s++)
            {
                int hand = s == 0 ? HaR : HaL;
                Vector3 w = bp[hand];
                Vector3 hc = w + new Vector3(0f, -0.05f, 0.01f);
                bool gauntlet = a.outfit == OutfitStyle.Armor || a.outfit == OutfitStyle.Stone;
                c.Ell(hand, hc, new Vector3(0.036f * k, 0.058f, 0.026f * k), gauntlet ? a.secondary : skin, gauntlet && a.outfit == OutfitStyle.Armor, 8, 6);
                c.Ell(hand, w + new Vector3(s == 0 ? -0.03f : 0.03f, -0.04f, 0.035f), new Vector3(0.012f, 0.03f, 0.012f), skin, false, 6, 4); // thumb
            }

            switch (a.outfit)
            {
                case OutfitStyle.Robe: BuildRobe(c, false); break;
                case OutfitStyle.Wanderer: BuildRobe(c, true); break;
                case OutfitStyle.Tunic: BuildTunic(c); break;
                case OutfitStyle.Leather: BuildLeather(c); break;
                case OutfitStyle.Armor: BuildArmor(c); break;
                case OutfitStyle.Modern: BuildModern(c); break;
                case OutfitStyle.Rags: BuildRags(c); break;
                case OutfitStyle.BlackRobe: BuildBlackRobe(c); break;
                case OutfitStyle.Stone: BuildStone(c); break;
            }
        }

        static void Use_Nose(this Ctx c, int bone, Vector3 root, Color skin)
        {
            c.mb.Bone(bone).Sub(0).PaletteUv(Palette.Uv(skin));
            c.mb.Push(root, Quaternion.Euler(90f, 0f, 0f), Vector3.one);
            c.mb.Cone(Vector3.zero, 0.028f, 0.014f, 6, false);
            c.mb.Pop();
        }

        // limb helpers --------------------------------------------------------
        static void Legs(Ctx c, Color thigh, Color shin, Color boot, bool tallBoot, float footLen = 0.24f)
        {
            Vector3[] bp = HumanoidDims.BindPositions;
            float k = c.k;
            for (int s = 0; s < 2; s++)
            {
                int th = s == 0 ? ThR : ThL, kn = s == 0 ? KnR : KnL, an = s == 0 ? AnR : AnL;
                c.Seg(th, bp[th], bp[kn] + new Vector3(0, 0.02f, 0), 0.088f * k, 0.064f * k, thigh, false, 9);
                c.Seg(kn, bp[kn], bp[an] + new Vector3(0, 0.03f, 0), 0.062f * k, 0.044f * k, shin, false, 8, true, false);
                if (tallBoot) c.Seg(kn, bp[kn] + new Vector3(0, -0.12f, 0), bp[an] + new Vector3(0, 0.02f, 0), 0.058f * k, 0.05f * k, boot, false, 8, false, false);
                // foot: boot body + toe
                float x = bp[an].x;
                c.Bx(an, new Vector3(x, 0.036f, 0.045f), new Vector3(0.088f * k, 0.07f, footLen * 0.8f), boot);
                c.Ell(an, new Vector3(x, 0.034f, 0.14f), new Vector3(0.044f * k, 0.034f, 0.06f), boot, false, 8, 5);
            }
        }

        static void ArmSegments(Ctx c, Color upper, Color lower, float upperR, float lowerR0, float lowerR1, bool shoulderBall = true)
        {
            Vector3[] bp = HumanoidDims.BindPositions;
            float k = c.k;
            for (int s = 0; s < 2; s++)
            {
                int ar = s == 0 ? ArR : ArL, fo = s == 0 ? FoR : FoL, ha = s == 0 ? HaR : HaL;
                c.Seg(ar, bp[ar], bp[fo], upperR * k, (upperR * 0.84f) * k, upper, false, 8, shoulderBall, false);
                c.Seg(fo, bp[fo], bp[ha], lowerR0 * k, lowerR1 * k, lower, false, 8, true, false);
            }
        }

        static void Torso(Ctx c, Color col, float flareBottomY = 0f, bool sleeves = false)
        {
            float k = c.k;
            // waist (Sp) and chest (Ch) shells overlap at the Spine/Chest joint to hide the crease
            var low = new[] { new Vector2(0.118f * k, 0.95f), new Vector2(0.122f * k, 1.02f), new Vector2(0.134f * k, 1.12f), new Vector2(0.145f * k, 1.20f) };
            var up = new[] { new Vector2(0.132f * k, 1.12f), new Vector2(0.152f * k, 1.20f), new Vector2(0.168f * k, 1.30f), new Vector2(0.172f * k, 1.37f), new Vector2(0.135f * k, 1.425f), new Vector2(0.06f * k, 1.445f) };
            c.Lathe(Sp, Vector3.zero, low, col, 0.66f, false, 14);
            c.Lathe(Ch, Vector3.zero, up, col, 0.64f, false, 14);
            // pelvis
            c.Ell(Hp, new Vector3(0f, 0.935f, 0f), new Vector3(0.148f * k, 0.1f, 0.098f * k), col, false, 12, 7);
        }

        static void Belt(Ctx c, Color col, float y = 1.0f)
        {
            float k = c.k;
            c.Cyl(Sp, new Vector3(0, y - 0.025f, 0), 0.05f, 0.128f * k, 0.128f * k, col, false, 14);
            c.mb.Sub(0);
        }

        // outfits -------------------------------------------------------------
        static void BuildRobe(Ctx c, bool cloak)
        {
            CharacterAppearance a = c.a;
            float k = c.k;
            Vector3[] bp = HumanoidDims.BindPositions;
            Color pri = a.primary, sec = a.secondary, acc = a.accent, pants = a.pants;
            Legs(c, pants, pants, Dk(sec, 0.35f), true);
            Torso(c, pri);
            // crossed collar and sash
            c.Seg(Ch, new Vector3(-0.075f * k, 1.42f, 0.083f), new Vector3(0.03f * k, 1.17f, 0.099f), 0.013f, 0.013f, sec, false, 5);
            c.Seg(Ch, new Vector3(0.075f * k, 1.42f, 0.083f), new Vector3(-0.03f * k, 1.17f, 0.099f), 0.013f, 0.013f, Lt(sec, 0.1f), false, 5);
            Belt(c, acc, 1.01f);
            c.Bx(Sp, new Vector3(0.02f, 1.0f, 0.1f), new Vector3(0.05f, 0.045f, 0.02f), Dk(acc, 0.8f));
            int tailL = c.AddBone("SashL", Hp, new Vector3(0.03f, 0.98f, 0.095f));
            int tailR = c.AddBone("SashR", Hp, new Vector3(0.0f, 0.98f, 0.095f));
            c.Bx(tailL, new Vector3(0.035f, 0.88f, 0.1f), new Vector3(0.028f, 0.22f, 0.008f), acc);
            c.Bx(tailR, new Vector3(-0.0f, 0.86f, 0.1f), new Vector3(0.028f, 0.26f, 0.008f), Dk(acc, 0.85f));
            AddSpring(c, tailL, 80f, 7f, 1f, 50f);
            AddSpring(c, tailR, 80f, 7f, 1f, 50f);

            // sleeves
            ArmSegments(c, pri, pri, 0.062f, 0.066f, 0.115f);
            for (int s = 0; s < 2; s++)
            {
                int fo = s == 0 ? FoR : FoL, ha = s == 0 ? HaR : HaL;
                Vector3 w = bp[ha];
                c.Seg(fo, w + new Vector3(0, 0.07f, 0), w + new Vector3(0, 0.03f, 0), 0.118f * k, 0.118f * k, sec, false, 10, false, false); // cuff
            }

            // skirt panels (spring bones)
            int sf = c.AddBone("SkirtF", Hp, new Vector3(0f, 0.93f, 0.09f));
            int sb = c.AddBone("SkirtB", Hp, new Vector3(0f, 0.93f, -0.09f));
            int sl = c.AddBone("SkirtL", Hp, new Vector3(-0.13f, 0.93f, 0f));
            int sr = c.AddBone("SkirtR", Hp, new Vector3(0.13f, 0.93f, 0f));
            float hem = cloak ? 0.42f : 0.5f;
            c.Bx(sf, new Vector3(0f, 0.93f - 0.26f, 0.105f * k), new Vector3(0.27f * k, 0.52f, 0.022f), pri);
            c.Bx(sf, new Vector3(0f, hem - 0.01f - 0.03f, 0.11f * k), new Vector3(0.28f * k, 0.035f, 0.024f), sec);
            c.Bx(sb, new Vector3(0f, 0.93f - 0.28f, -0.108f * k), new Vector3(0.29f * k, 0.56f, 0.022f), Dk(pri, 0.92f));
            c.Bx(sb, new Vector3(0f, hem - 0.07f, -0.112f * k), new Vector3(0.3f * k, 0.035f, 0.024f), sec);
            c.Bx(sl, new Vector3(-0.145f * k, 0.93f - 0.2f, 0f), new Vector3(0.022f, 0.4f, 0.17f * k), Dk(pri, 0.95f));
            c.Bx(sr, new Vector3(0.145f * k, 0.93f - 0.2f, 0f), new Vector3(0.022f, 0.4f, 0.17f * k), Dk(pri, 0.95f));
            AddSpring(c, sf, 60f, 8f, 1f, 35f);
            AddSpring(c, sb, 60f, 8f, 1f, 35f);
            AddSpring(c, sl, 60f, 8f, 0.6f, 20f);
            AddSpring(c, sr, 60f, 8f, 0.6f, 20f);

            if (cloak)
            {
                // travelling cloak: shoulder cape + hood fold
                int cp = c.AddBone("Cape", Ch, new Vector3(0f, 1.4f, -0.1f));
                c.Bx(cp, new Vector3(0f, 1.05f, -0.14f * k), new Vector3(0.36f * k, 0.7f, 0.02f), Dk(sec, 0.8f));
                c.Seg(Ch, new Vector3(-0.17f * k, 1.4f, 0f), new Vector3(0.17f * k, 1.4f, 0f), 0.05f * k, 0.05f * k, Dk(sec, 0.8f), false, 8);
                AddSpring(c, cp, 55f, 8f, 1f, 40f);
            }
        }

        static void BuildTunic(Ctx c)
        {
            CharacterAppearance a = c.a;
            float k = c.k;
            Vector3[] bp = HumanoidDims.BindPositions;
            Color pri = a.primary, sec = a.secondary, acc = a.accent;
            Legs(c, a.pants, a.pants, Dk(sec, 0.55f), false);
            for (int s = 0; s < 2; s++)
            {
                int kn = s == 0 ? KnR : KnL, an = s == 0 ? AnR : AnL;
                c.Seg(kn, bp[kn] + new Vector3(0, -0.12f, 0), bp[an] + new Vector3(0, 0.05f, 0), 0.056f * k, 0.05f * k, sec, false, 8, false, false); // leg wraps
            }
            Torso(c, pri);
            // tunic skirt to mid-thigh
            var skirt = new[] { new Vector2(0.15f * k, 0.98f), new Vector2(0.17f * k, 0.86f), new Vector2(0.19f * k, 0.72f) };
            c.Lathe(Hp, Vector3.zero, skirt, pri, 0.75f, false, 14);
            Belt(c, acc, 1.0f);
            // short sleeves to the elbow, bare forearms
            ArmSegments(c, pri, a.skin, 0.06f, 0.044f, 0.036f);
            for (int s = 0; s < 2; s++)
            {
                int fo = s == 0 ? FoR : FoL;
                c.Seg(fo, bp[fo] + new Vector3(0, 0.03f, 0), bp[fo] + new Vector3(0, -0.05f, 0), 0.058f * k, 0.056f * k, pri, false, 8, false, false);
            }
        }

        static void BuildLeather(Ctx c)
        {
            CharacterAppearance a = c.a;
            float k = c.k;
            Vector3[] bp = HumanoidDims.BindPositions;
            Color pri = a.primary, sec = a.secondary, acc = a.accent;
            Legs(c, a.pants, a.pants, Dk(sec, 0.7f), true);
            for (int s = 0; s < 2; s++)
            {
                int kn = s == 0 ? KnR : KnL;
                c.Seg(kn, bp[kn] + new Vector3(0, 0.03f, 0), bp[kn] + new Vector3(0, -0.2f, 0), 0.068f * k, 0.058f * k, sec, false, 8, false, false); // knee guard
            }
            Torso(c, pri);
            // leather vest slightly bigger over the shirt
            var vest = new[] { new Vector2(0.126f * k, 1.0f), new Vector2(0.14f * k, 1.12f), new Vector2(0.16f * k, 1.22f), new Vector2(0.175f * k, 1.3f), new Vector2(0.176f * k, 1.37f), new Vector2(0.14f * k, 1.41f) };
            c.Lathe(Ch, Vector3.zero, vest, sec, 0.65f, false, 14);
            Belt(c, Dk(acc, 0.8f), 1.0f);
            c.Bx(Sp, new Vector3(0.12f * k, 0.95f, 0.05f), new Vector3(0.07f, 0.09f, 0.05f), Dk(sec, 0.75f));   // pouch
            c.Bx(Sp, new Vector3(-0.12f * k, 0.95f, 0.04f), new Vector3(0.06f, 0.08f, 0.05f), Dk(sec, 0.85f));
            ArmSegments(c, pri, pri, 0.052f, 0.043f, 0.037f);
            for (int s = 0; s < 2; s++)
            {
                int fo = s == 0 ? FoR : FoL, ha = s == 0 ? HaR : HaL;
                c.Seg(fo, bp[fo] + new Vector3(0, -0.08f, 0), bp[ha] + new Vector3(0, 0.03f, 0), 0.047f * k, 0.042f * k, sec, false, 8, false, false); // bracer
            }
        }

        static void BuildArmor(Ctx c)
        {
            CharacterAppearance a = c.a;
            float k = c.k;
            Vector3[] bp = HumanoidDims.BindPositions;
            Color pri = a.primary, sec = a.secondary, acc = a.accent;
            Color steel = new Color(0.62f, 0.66f, 0.7f);
            Legs(c, a.pants, a.pants, Dk(steel, 0.6f), true);
            for (int s = 0; s < 2; s++)
            {
                int kn = s == 0 ? KnR : KnL, th = s == 0 ? ThR : ThL;
                c.Seg(kn, bp[kn] + new Vector3(0, 0.02f, 0), bp[kn] + new Vector3(0, -0.28f, 0), 0.066f * k, 0.056f * k, steel, true, 8, false, false);
                c.Seg(th, bp[th] + new Vector3(0, -0.04f, 0), bp[th] + new Vector3(0, -0.3f, 0), 0.094f * k, 0.082f * k, Dk(pri, 0.9f), false, 8, false, false);
            }
            Torso(c, pri);
            var plate = new[] { new Vector2(0.13f * k, 1.04f), new Vector2(0.15f * k, 1.14f), new Vector2(0.172f * k, 1.24f), new Vector2(0.18f * k, 1.34f), new Vector2(0.15f * k, 1.41f) };
            c.Lathe(Ch, Vector3.zero, plate, steel, 0.68f, true, 14);
            Belt(c, acc, 1.0f);
            ArmSegments(c, pri, pri, 0.056f, 0.046f, 0.04f);
            for (int s = 0; s < 2; s++)
            {
                int ar = s == 0 ? ArR : ArL, fo = s == 0 ? FoR : FoL, ha = s == 0 ? HaR : HaL;
                c.Ell(ar, bp[ar] + new Vector3(s == 0 ? 0.02f : -0.02f, 0.02f, 0), new Vector3(0.088f, 0.06f, 0.084f), steel, true, 10, 6);
                c.Seg(fo, bp[fo] + new Vector3(0, -0.04f, 0), bp[ha] + new Vector3(0, 0.02f, 0), 0.05f * k, 0.044f * k, steel, true, 8, false, false);
            }
        }

        static void BuildModern(Ctx c)
        {
            CharacterAppearance a = c.a;
            float k = c.k;
            Vector3[] bp = HumanoidDims.BindPositions;
            Color pri = a.primary, sec = a.secondary, acc = a.accent;
            Legs(c, a.pants, a.pants, sec, false);
            for (int s = 0; s < 2; s++)
            {
                int an = s == 0 ? AnR : AnL;
                float x = bp[an].x;
                c.Bx(an, new Vector3(x, 0.012f, 0.05f), new Vector3(0.094f * k, 0.026f, 0.27f), acc); // sneaker sole
            }
            Torso(c, pri);
            // hoodie: longer body to the hips, hood bunched at the neck, cuffs
            var skirt = new[] { new Vector2(0.15f * k, 0.98f), new Vector2(0.158f * k, 0.84f), new Vector2(0.155f * k, 0.78f) };
            c.Lathe(Hp, Vector3.zero, skirt, pri, 0.72f, false, 14);
            c.Torus(Ch, new Vector3(0f, 1.4f, -0.02f), 0.075f, 0.032f, Dk(pri, 0.85f), new Vector3(8f, 0f, 0f));
            c.Bx(Ch, new Vector3(0f, 1.1f, 0.115f * k), new Vector3(0.12f, 0.06f, 0.01f), Dk(pri, 0.8f)); // pocket
            ArmSegments(c, pri, pri, 0.058f, 0.05f, 0.043f);
            for (int s = 0; s < 2; s++)
            {
                int fo = s == 0 ? FoR : FoL, ha = s == 0 ? HaR : HaL;
                c.Seg(fo, bp[ha] + new Vector3(0, 0.05f, 0), bp[ha] + new Vector3(0, 0.0f, 0), 0.044f * k, 0.044f * k, Dk(pri, 0.8f), false, 8, false, false);
            }
        }

        static void BuildRags(Ctx c)
        {
            CharacterAppearance a = c.a;
            float k = c.k;
            Vector3[] bp = HumanoidDims.BindPositions;
            Color pri = a.primary, sec = a.secondary, acc = a.accent;
            Legs(c, a.pants.maxColorComponent > 0.01f ? a.pants : Dk(pri, 0.6f), Dk(pri, 0.7f), Dk(sec, 0.8f), false);
            for (int s = 0; s < 2; s++)
            {
                int kn = s == 0 ? KnR : KnL, an = s == 0 ? AnR : AnL;
                c.Seg(kn, bp[kn] + new Vector3(0, -0.14f, 0), bp[an] + new Vector3(0, 0.05f, 0), 0.052f * k, 0.047f * k, Dk(acc, 0.6f), false, 8, false, false); // wraps
            }
            Torso(c, pri);
            var vest = new[] { new Vector2(0.128f * k, 1.0f), new Vector2(0.15f * k, 1.18f), new Vector2(0.172f * k, 1.31f), new Vector2(0.166f * k, 1.39f) };
            c.Lathe(Ch, Vector3.zero, vest, sec, 0.66f, false, 12);
            Belt(c, Dk(acc, 0.7f), 0.99f);
            c.Seg(Hp, new Vector3(0.1f, 0.96f, 0.1f), new Vector3(0.13f, 0.86f, 0.1f), 0.012f, 0.012f, Dk(sec, 0.5f), false, 5);   // knife
            ArmSegments(c, a.skin, a.skin, 0.05f, 0.042f, 0.036f);
            for (int s = 0; s < 2; s++)
            {
                int ha = s == 0 ? HaR : HaL, fo = s == 0 ? FoR : FoL;
                c.Seg(fo, bp[ha] + new Vector3(0, 0.1f, 0), bp[ha] + new Vector3(0, 0.02f, 0), 0.045f * k, 0.041f * k, Dk(acc, 0.7f), false, 8, false, false);
            }
        }

        static void BuildBlackRobe(Ctx c)
        {
            CharacterAppearance a = c.a;
            float k = c.k;
            Vector3[] bp = HumanoidDims.BindPositions;
            Color pri = a.primary, sec = a.secondary, acc = a.accent;
            Legs(c, Dk(pri, 0.7f), Dk(pri, 0.7f), Dk(pri, 0.5f), false);
            Torso(c, pri);
            // one piece long robe (cone around the legs)
            var robe = new[] { new Vector2(0.15f * k, 0.99f), new Vector2(0.2f * k, 0.8f), new Vector2(0.27f * k, 0.52f), new Vector2(0.34f * k, 0.2f), new Vector2(0.355f * k, 0.1f) };
            c.Lathe(Hp, Vector3.zero, robe, pri, 0.85f, false, 16);
            c.Torus(Hp, new Vector3(0f, 0.11f, 0f), 0.345f * k, 0.014f, sec, Vector3.zero, false, 20);
            Belt(c, sec, 1.0f);
            int t1 = c.AddBone("TalA", Hp, new Vector3(0.1f, 0.97f, 0.12f));
            int t2 = c.AddBone("TalB", Hp, new Vector3(-0.08f, 0.97f, 0.13f));
            c.Bx(t1, new Vector3(0.1f, 0.84f, 0.125f), new Vector3(0.035f, 0.14f, 0.004f), new Color(0.92f, 0.82f, 0.45f));
            c.Bx(t2, new Vector3(-0.08f, 0.82f, 0.135f), new Vector3(0.035f, 0.17f, 0.004f), new Color(0.92f, 0.82f, 0.45f));
            AddSpring(c, t1, 70f, 7f, 1f, 45f);
            AddSpring(c, t2, 70f, 7f, 1f, 45f);
            ArmSegments(c, pri, pri, 0.064f, 0.07f, 0.12f);
            for (int s = 0; s < 2; s++)
            {
                int fo = s == 0 ? FoR : FoL, ha = s == 0 ? HaR : HaL;
                c.Seg(fo, bp[ha] + new Vector3(0, 0.07f, 0), bp[ha] + new Vector3(0, 0.03f, 0), 0.122f * k, 0.122f * k, sec, false, 10, false, false);
            }
            // hood
            c.Ell(Hd, new Vector3(0f, 1.655f, -0.03f), new Vector3(0.12f, 0.125f, 0.13f), Dk(pri, 0.95f), false, 12, 8);
            c.Torus(Ch, new Vector3(0f, 1.41f, 0f), 0.1f, 0.04f, Dk(pri, 0.85f), new Vector3(0f, 0f, 0f));
            if ((a.accessories & Accessory.Talismans) != 0 && (a.accessories & Accessory.Mask) != 0)
            {
                // glowing sigil on the chest
                var glow = new GameObject("Sigil");
                glow.transform.SetParent(c.bones[Ch], false);
                var mb = new MeshBuilder();
                mb.Disc(new Vector3(0f, 1.27f - bp[Ch].y, 0.108f * k), 0.035f, 12, true);
                var mf = glow.AddComponent<MeshFilter>();
                mf.sharedMesh = mb.ToMesh("sigil", false);
                var mr = glow.AddComponent<MeshRenderer>();
                mr.sharedMaterial = Mats.Cached("sigil_red", () => Mats.Glow(new Color(1f, 0.15f, 0.25f), 3f));
                mr.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                c.rig.extraRenderers.Add(mr);
            }
        }

        static void BuildStone(Ctx c)
        {
            CharacterAppearance a = c.a;
            float k = c.k;
            Vector3[] bp = HumanoidDims.BindPositions;
            Color pri = a.primary, sec = a.secondary, acc = a.accent;
            int start;
            for (int s = 0; s < 2; s++)
            {
                int th = s == 0 ? ThR : ThL, kn = s == 0 ? KnR : KnL, an = s == 0 ? AnR : AnL;
                start = c.mb.VertexCount;
                c.Seg(th, bp[th], bp[kn], 0.11f * k, 0.085f * k, pri, false, 8);
                c.Seg(kn, bp[kn], bp[an] + new Vector3(0, 0.03f, 0), 0.09f * k, 0.07f * k, sec, false, 8, true, false);
                c.Bx(an, new Vector3(bp[an].x, 0.045f, 0.05f), new Vector3(0.14f * k, 0.09f, 0.26f), pri);
                c.mb.Displace(start, 0.014f, 30f, 5 + s);
            }
            start = c.mb.VertexCount;
            c.Lathe(Sp, Vector3.zero, new[] { new Vector2(0.14f * k, 0.95f), new Vector2(0.15f * k, 1.1f), new Vector2(0.17f * k, 1.2f) }, sec, 0.8f, false, 12);
            c.Lathe(Ch, Vector3.zero, new[] { new Vector2(0.16f * k, 1.12f), new Vector2(0.21f * k, 1.22f), new Vector2(0.235f * k, 1.34f), new Vector2(0.2f * k, 1.43f), new Vector2(0.09f * k, 1.46f) }, pri, 0.78f, false, 14);
            c.Ell(Hp, new Vector3(0f, 0.935f, 0f), new Vector3(0.16f * k, 0.1f, 0.12f * k), sec, false, 10, 6);
            c.mb.Displace(start, 0.02f, 24f, 9);
            for (int s = 0; s < 2; s++)
            {
                int ar = s == 0 ? ArR : ArL, fo = s == 0 ? FoR : FoL, ha = s == 0 ? HaR : HaL;
                start = c.mb.VertexCount;
                c.Ell(ar, bp[ar] + new Vector3(s == 0 ? 0.03f : -0.03f, 0.0f, 0f), new Vector3(0.11f, 0.095f, 0.1f), pri, false, 9, 6);
                c.Seg(ar, bp[ar], bp[fo], 0.08f * k, 0.07f * k, sec, false, 8, false, false);
                c.Seg(fo, bp[fo], bp[ha], 0.075f * k, 0.082f * k, pri, false, 8, true, false);
                c.Ell(ha, bp[ha] + new Vector3(0, -0.07f, 0.0f), new Vector3(0.075f * k, 0.085f, 0.07f * k), sec, false, 8, 6);
                c.mb.Displace(start, 0.016f, 30f, 21 + s);
            }
            // glowing runes
            var glow = new GameObject("Runes");
            glow.transform.SetParent(c.bones[Ch], false);
            var gb = new MeshBuilder();
            gb.Box(new Vector3(0f, 1.26f - bp[Ch].y, 0.185f * k), new Vector3(0.012f, 0.16f, 0.006f));
            gb.Box(new Vector3(0f, 1.3f - bp[Ch].y, 0.186f * k), new Vector3(0.12f, 0.012f, 0.006f));
            gb.Box(new Vector3(-0.07f * k, 1.2f - bp[Ch].y, 0.18f * k), new Vector3(0.012f, 0.1f, 0.006f));
            gb.Box(new Vector3(0.07f * k, 1.2f - bp[Ch].y, 0.18f * k), new Vector3(0.012f, 0.1f, 0.006f));
            glow.AddComponent<MeshFilter>().sharedMesh = gb.ToMesh("runes", false);
            var mr = glow.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Cached("rune_glow_cyan", () => Mats.Glow(new Color(0.4f, 0.85f, 1f), 3f));
            c.rig.extraRenderers.Add(mr);
            // glowing eyes
            var eyes = new GameObject("Eyes");
            eyes.transform.SetParent(c.bones[Hd], false);
            var eb = new MeshBuilder();
            eb.Box(new Vector3(-0.04f, 1.64f - bp[Hd].y, 0.098f), new Vector3(0.03f, 0.012f, 0.01f));
            eb.Box(new Vector3(0.04f, 1.64f - bp[Hd].y, 0.098f), new Vector3(0.03f, 0.012f, 0.01f));
            eyes.AddComponent<MeshFilter>().sharedMesh = eb.ToMesh("eyes", false);
            var er = eyes.AddComponent<MeshRenderer>();
            er.sharedMaterial = Mats.Cached("rune_glow_cyan", () => Mats.Glow(new Color(0.4f, 0.85f, 1f), 3f));
            c.rig.extraRenderers.Add(er);
        }

        // ------------------------------------------------------------------ hair & accessories
        static void AddSpring(Ctx c, int bone, float stiffness, float damping, float influence, float maxAngle)
        {
            c.rig.springs.Add(new SpringBone { t = c.bones[bone], rest = Quaternion.identity, stiffness = stiffness, damping = damping, influence = influence, maxAngle = maxAngle });
        }

        static void BuildHairAndAccessories(Ctx c)
        {
            CharacterAppearance a = c.a;
            float k = c.k;
            Color hair = a.hair, acc = a.accent;
            Vector3[] bp = HumanoidDims.BindPositions;
            Accessory f = a.accessories;

            // hair cap
            bool cap = a.hairStyle != HairStyle.None && a.hairStyle != HairStyle.Bald && (f & Accessory.Hat) == 0;
            if (cap)
            {
                float r = a.hairStyle == HairStyle.Short ? 0.092f : 0.097f;
                c.Ell(Hd, new Vector3(0f, 1.688f, -0.014f), new Vector3(r, 0.078f, 0.101f), hair, false, 12, 7);
                if (a.hairStyle != HairStyle.Short)
                    c.Ell(Hd, new Vector3(0f, 1.62f, -0.045f), new Vector3(0.092f, 0.1f, 0.075f), hair, false, 10, 6); // back of the head
            }
            switch (a.hairStyle)
            {
                case HairStyle.Topknot:
                    c.Ell(Hd, new Vector3(0f, 1.775f, -0.015f), new Vector3(0.04f, 0.05f, 0.04f), hair, false, 8, 6);
                    c.Cyl(Hd, new Vector3(0f, 1.745f, -0.015f), 0.02f, 0.03f, 0.022f, acc, false, 8);
                    c.Seg(Hd, new Vector3(0.03f, 1.77f, -0.02f), new Vector3(0.062f, 1.745f, -0.02f), 0.007f, 0.004f, acc, false, 4);
                    break;
                case HairStyle.Bun:
                    c.Ell(Hd, new Vector3(0f, 1.74f, -0.09f), new Vector3(0.05f, 0.05f, 0.05f), hair, false, 8, 6);
                    break;
                case HairStyle.Messy:
                    for (int i = 0; i < 7; i++)
                    {
                        float ang = i / 7f * Mathf.PI * 2f;
                        Vector3 baseP = new Vector3(Mathf.Cos(ang) * 0.06f, 1.74f, -0.01f + Mathf.Sin(ang) * 0.06f);
                        c.Seg(Hd, baseP, baseP + new Vector3(Mathf.Cos(ang) * 0.04f, 0.05f, Mathf.Sin(ang) * 0.04f), 0.02f, 0.004f, hair, false, 5);
                    }
                    break;
                case HairStyle.Ponytail:
                    {
                        int p1 = c.AddBone("Pony1", Hd, new Vector3(0f, 1.69f, -0.09f));
                        int p2 = c.AddBone("Pony2", p1, new Vector3(0f, 1.58f, -0.14f));
                        int p3 = c.AddBone("Pony3", p2, new Vector3(0f, 1.46f, -0.16f));
                        c.Seg(p1, c.bind[p1], c.bind[p2], 0.035f, 0.03f, hair, false, 6);
                        c.Seg(p2, c.bind[p2], c.bind[p3], 0.03f, 0.022f, hair, false, 6);
                        c.Seg(p3, c.bind[p3], c.bind[p3] + new Vector3(0f, -0.1f, -0.01f), 0.022f, 0.005f, hair, false, 6);
                        AddSpring(c, p1, 90f, 8f, 0.7f, 30f);
                        AddSpring(c, p2, 80f, 7f, 1f, 40f);
                        AddSpring(c, p3, 70f, 6f, 1.2f, 50f);
                        break;
                    }
                case HairStyle.Long:
                    {
                        int h1 = c.AddBone("HairA", Hd, new Vector3(0f, 1.68f, -0.085f));
                        int h2 = c.AddBone("HairB", h1, new Vector3(0f, 1.46f, -0.11f));
                        int h3 = c.AddBone("HairC", h2, new Vector3(0f, 1.22f, -0.12f));
                        c.Ell(h1, (c.bind[h1] + c.bind[h2]) * 0.5f, new Vector3(0.1f, 0.13f, 0.034f), hair, false, 10, 6);
                        c.Ell(h2, (c.bind[h2] + c.bind[h3]) * 0.5f, new Vector3(0.1f, 0.13f, 0.034f), hair, false, 10, 6);
                        c.Ell(h3, c.bind[h3] + new Vector3(0, -0.06f, 0f), new Vector3(0.085f, 0.12f, 0.03f), hair, false, 10, 6);
                        AddSpring(c, h1, 90f, 9f, 0.6f, 25f);
                        AddSpring(c, h2, 80f, 8f, 0.9f, 35f);
                        AddSpring(c, h3, 70f, 7f, 1.2f, 45f);
                        break;
                    }
            }

            if ((f & Accessory.Hat) != 0)
            {
                c.Use_Cone(Hd, new Vector3(0f, 1.705f, 0f), 0.13f, 0.27f, Dk(acc, 0.95f));
                c.Torus(Hd, new Vector3(0f, 1.71f, 0f), 0.265f, 0.008f, Dk(acc, 0.7f), Vector3.zero);
            }
            if ((f & Accessory.Headband) != 0)
            {
                c.Torus(Hd, new Vector3(0f, 1.675f, 0f), 0.091f, 0.0085f, acc, new Vector3(-6f, 0f, 0f), false, 14);
                c.BxRot(Hd, new Vector3(0.02f, 1.63f, -0.108f), new Vector3(0.02f, 0.12f, 0.004f), new Vector3(0f, 0f, 8f), acc);
                c.BxRot(Hd, new Vector3(-0.02f, 1.62f, -0.108f), new Vector3(0.02f, 0.14f, 0.004f), new Vector3(0f, 0f, -6f), Dk(acc, 0.85f));
            }
            if ((f & Accessory.Mask) != 0)
            {
                Color m = a.outfit == OutfitStyle.BlackRobe ? Dk(a.primary, 0.6f) : Dk(a.secondary, 0.7f);
                c.Ell(Hd, new Vector3(0f, 1.59f, 0.04f), new Vector3(0.078f, 0.05f, 0.062f), m, false, 10, 6);
            }
            if ((f & Accessory.Glasses) != 0)
            {
                Color g = new Color(0.1f, 0.1f, 0.12f);
                c.Torus(Hd, new Vector3(-0.034f, 1.642f, 0.098f), 0.023f, 0.0035f, g, new Vector3(90f, 0f, 0f), false, 12);
                c.Torus(Hd, new Vector3(0.034f, 1.642f, 0.098f), 0.023f, 0.0035f, g, new Vector3(90f, 0f, 0f), false, 12);
                c.Bx(Hd, new Vector3(0f, 1.645f, 0.1f), new Vector3(0.022f, 0.004f, 0.004f), g);
            }
            if ((f & Accessory.Beard) != 0)
            {
                c.Ell(Hd, new Vector3(0f, 1.52f, 0.055f), new Vector3(0.058f, 0.11f, 0.04f), a.hair.maxColorComponent < 0.3f ? new Color(0.88f, 0.88f, 0.86f) : hair, false, 10, 6);
            }
            if ((f & Accessory.ShoulderPads) != 0 && a.outfit != OutfitStyle.Armor)
            {
                for (int s = 0; s < 2; s++)
                {
                    int ar = s == 0 ? ArR : ArL;
                    c.Ell(ar, bp[ar] + new Vector3(s == 0 ? 0.025f : -0.025f, 0.02f, 0f), new Vector3(0.085f, 0.055f, 0.08f), Dk(a.secondary, 0.8f), true, 10, 6);
                }
            }
            if ((f & Accessory.Backpack) != 0)
            {
                Color pack = Dk(a.secondary, 0.9f);
                c.Bx(Ch, new Vector3(0f, 1.24f, -0.17f * k), new Vector3(0.27f * k, 0.36f, 0.14f), pack);
                c.Cyl(Ch, new Vector3(0f, 1.425f, -0.17f * k), 0.0f + 0.27f * k, 0.05f, 0.05f, acc, false, 8);
                c.mb.Sub(0);
                for (int s = 0; s < 2; s++)
                    c.Seg(Ch, new Vector3((s == 0 ? 1f : -1f) * 0.085f * k, 1.38f, -0.1f * k), new Vector3((s == 0 ? 1f : -1f) * 0.095f * k, 1.12f, 0.09f * k), 0.012f, 0.012f, Dk(pack, 0.7f), false, 4);
            }
            if ((f & Accessory.Cape) != 0 && a.outfit != OutfitStyle.Wanderer)
            {
                int cp = c.AddBone("CapeB", Ch, new Vector3(0f, 1.41f, -0.11f));
                c.Bx(cp, new Vector3(0f, 1.0f, -0.145f * k), new Vector3(0.38f * k, 0.8f, 0.018f), a.secondary);
                AddSpring(c, cp, 55f, 8f, 1f, 40f);
            }
            if ((f & Accessory.Talismans) != 0 && a.outfit != OutfitStyle.BlackRobe)
            {
                int t1 = c.AddBone("TalC", Hp, new Vector3(0.12f, 0.96f, 0.1f));
                c.Bx(t1, new Vector3(0.12f, 0.84f, 0.105f), new Vector3(0.03f, 0.13f, 0.004f), new Color(0.92f, 0.82f, 0.45f));
                AddSpring(c, t1, 70f, 7f, 1f, 45f);
            }
            if ((f & Accessory.Horns) != 0)
            {
                for (int s = 0; s < 2; s++)
                {
                    float x = s == 0 ? 0.06f : -0.06f;
                    c.Seg(Hd, new Vector3(x, 1.71f, 0.0f), new Vector3(x * 1.7f, 1.84f, -0.03f), 0.022f, 0.004f, Dk(a.secondary, 0.7f), false, 6);
                }
            }
        }

        static void Use_Cone(this Ctx c, int bone, Vector3 baseC, float h, float r, Color col)
        {
            c.Cone(bone, baseC, h, r, col, 14);
        }
    }
}
