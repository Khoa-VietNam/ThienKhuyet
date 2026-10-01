using System.Collections.Generic;
using ThienKhuyet.Data;
using ThienKhuyet.Gfx;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThienKhuyet.Characters
{
    public enum BeastStyle { Wolf = 0, Boar = 1 }

    /// <summary>One skinned mesh beast (wolf, boar) from palette colours; appearance.primary = fur, secondary = belly/muzzle, skin = nose/paws.</summary>
    public static class QuadrupedBuilder
    {
        static readonly string[] Names =
        {
            "Body", "Spine", "Chest", "Neck", "Head", "Jaw", "Tail1", "Tail2", "Tail3",
            "FrontUpperR", "FrontLowerR", "FrontPawR", "FrontUpperL", "FrontLowerL", "FrontPawL",
            "HindUpperR", "HindLowerR", "HindPawR", "HindUpperL", "HindLowerL", "HindPawL"
        };

        public static QuadrupedRig Build(CharacterAppearance a, BeastStyle style, Transform parent, string name = "Model", Color? glow = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rig = go.AddComponent<QuadrupedRig>();
            rig.model = go.transform;
            rig.appearance = a;

            var bones = new List<Transform>();
            var bind = QuadrupedDims.BindPositions;
            for (int i = 0; i < Names.Length; i++)
            {
                var b = new GameObject(Names[i]).transform;
                b.SetParent(QuadrupedDims.Parent[i] < 0 ? go.transform : bones[QuadrupedDims.Parent[i]], false);
                b.localPosition = QuadrupedDims.LocalOffset[i];
                bones.Add(b);
                rig.joints[i] = b;
            }
            rig.headBone = bones[Rigs.QHead];

            var mb = new MeshBuilder(true);
            bool boar = style == BeastStyle.Boar;
            Color fur = a.primary, belly = a.secondary, skin = a.skin;
            float w = boar ? 1.25f : 1f;   // body width factor

            void Use(int bone, Color c, bool metal = false)
            {
                mb.Bone(bone).Sub(metal ? 1 : 0).PaletteUv(Palette.Uv(c));
            }

            // torso
            Use(Rigs.QBody, fur);
            mb.Ellipsoid(bind[Rigs.QBody] + new Vector3(0f, 0.0f, 0.02f), new Vector3(0.155f * w, 0.17f, 0.24f), 12, 8);
            Use(Rigs.QSpine, fur);
            mb.Ellipsoid(bind[Rigs.QSpine] + new Vector3(0f, 0.0f, 0.0f), new Vector3(0.165f * w, 0.18f, 0.22f), 12, 8);
            Use(Rigs.QChest, fur);
            mb.Ellipsoid(bind[Rigs.QChest] + new Vector3(0f, -0.01f, 0.0f), new Vector3(0.18f * w, 0.2f, 0.24f), 12, 8);
            Use(Rigs.QChest, belly);
            mb.Ellipsoid(bind[Rigs.QChest] + new Vector3(0f, -0.1f, 0.02f), new Vector3(0.11f * w, 0.1f, 0.22f), 10, 6);
            // mane / ruff
            Use(Rigs.QNeck, Color.Lerp(fur, belly, 0.4f));
            mb.Ellipsoid(bind[Rigs.QNeck] + new Vector3(0f, -0.01f, -0.04f), new Vector3(0.13f * w, 0.14f, 0.15f), 10, 7);
            // head
            Use(Rigs.QHead, fur);
            mb.Ellipsoid(bind[Rigs.QHead] + new Vector3(0f, 0.0f, 0.04f), new Vector3(0.095f * w, 0.09f, 0.115f), 10, 7);
            if (boar)
            {
                Use(Rigs.QHead, fur);
                mb.Limb(bind[Rigs.QHead] + new Vector3(0f, -0.02f, 0.08f), bind[Rigs.QHead] + new Vector3(0f, -0.04f, 0.26f), 0.075f, 0.058f, 8, false, true);
                Use(Rigs.QHead, skin);
                mb.Ellipsoid(bind[Rigs.QHead] + new Vector3(0f, -0.04f, 0.27f), new Vector3(0.062f, 0.05f, 0.025f), 8, 5);
                for (int s = -1; s <= 1; s += 2)
                {
                    Use(Rigs.QHead, new Color(0.93f, 0.9f, 0.8f));
                    mb.Limb(bind[Rigs.QHead] + new Vector3(0.05f * s, -0.07f, 0.2f), bind[Rigs.QHead] + new Vector3(0.075f * s, 0.02f, 0.27f), 0.014f, 0.004f, 5, false, true);
                    Use(Rigs.QHead, fur);
                    mb.Ellipsoid(bind[Rigs.QHead] + new Vector3(0.07f * s, 0.085f, -0.02f), new Vector3(0.03f, 0.04f, 0.015f), 6, 4);
                }
            }
            else
            {
                Use(Rigs.QHead, belly);
                mb.Limb(bind[Rigs.QHead] + new Vector3(0f, -0.02f, 0.07f), bind[Rigs.QHead] + new Vector3(0f, -0.035f, 0.2f), 0.058f, 0.036f, 8, false, true);
                Use(Rigs.QHead, skin);
                mb.Ellipsoid(bind[Rigs.QHead] + new Vector3(0f, -0.022f, 0.215f), new Vector3(0.03f, 0.024f, 0.022f), 8, 5);
                for (int s = -1; s <= 1; s += 2)
                {
                    Use(Rigs.QHead, fur);
                    mb.Limb(bind[Rigs.QHead] + new Vector3(0.055f * s, 0.07f, -0.02f), bind[Rigs.QHead] + new Vector3(0.07f * s, 0.17f, -0.045f), 0.036f, 0.004f, 5, false, true);
                }
            }
            // jaw
            Use(Rigs.QJaw, belly);
            mb.Limb(bind[Rigs.QJaw] + new Vector3(0f, 0f, 0.0f), bind[Rigs.QJaw] + new Vector3(0f, -0.005f, boar ? 0.2f : 0.15f), 0.04f, 0.026f, 6, false, true);
            Use(Rigs.QJaw, new Color(0.9f, 0.88f, 0.82f), false);
            for (int s = -1; s <= 1; s += 2)
                mb.Limb(bind[Rigs.QJaw] + new Vector3(0.02f * s, 0.015f, 0.1f), bind[Rigs.QJaw] + new Vector3(0.02f * s, 0.045f, 0.11f), 0.006f, 0.001f, 4, false, false);
            // eyes (dark; glowing ones are separate renderers)
            Use(Rigs.QHead, new Color(0.04f, 0.03f, 0.03f));
            for (int s = -1; s <= 1; s += 2) mb.Ellipsoid(bind[Rigs.QHead] + new Vector3(0.05f * s * w, 0.025f, 0.1f), new Vector3(0.012f, 0.013f, 0.01f), 6, 4);

            // tail
            Use(Rigs.QTail1, fur);
            mb.Limb(bind[Rigs.QTail1], bind[Rigs.QTail2], boar ? 0.02f : 0.05f, boar ? 0.015f : 0.045f, 6, true, false);
            Use(Rigs.QTail2, fur);
            mb.Limb(bind[Rigs.QTail2], bind[Rigs.QTail3], boar ? 0.015f : 0.045f, boar ? 0.012f : 0.04f, 6, true, false);
            Use(Rigs.QTail3, boar ? fur : belly);
            mb.Limb(bind[Rigs.QTail3], bind[Rigs.QTail3] + new Vector3(0f, -0.07f, -0.12f), boar ? 0.012f : 0.04f, 0.004f, 6, true, true);

            // legs
            for (int s = 0; s < 2; s++)
            {
                int fu = s == 0 ? Rigs.QFlR : Rigs.QFlL, fl = fu + 1, fp = fu + 2;
                int hu = s == 0 ? Rigs.QHlR : Rigs.QHlL, hl = hu + 1, hp = hu + 2;
                float legR = boar ? 1.1f : 1f;
                Use(fu, fur);
                mb.Limb(bind[fu], bind[fl], 0.062f * legR, 0.04f * legR, 7, true, false);
                Use(fl, fur);
                mb.Limb(bind[fl], bind[fp] + new Vector3(0f, 0.02f, 0f), 0.04f * legR, 0.028f * legR, 7, true, false);
                Use(fp, skin);
                mb.Ellipsoid(bind[fp] + new Vector3(0f, 0.015f, 0.025f), new Vector3(0.036f, 0.03f, 0.056f), 8, 5);
                Use(hu, fur);
                mb.Limb(bind[hu], bind[hl], 0.085f * legR, 0.044f * legR, 7, true, false);
                Use(hl, fur);
                mb.Limb(bind[hl], bind[hp] + new Vector3(0f, 0.02f, 0f), 0.042f * legR, 0.028f * legR, 7, true, false);
                Use(hp, skin);
                mb.Ellipsoid(bind[hp] + new Vector3(0f, 0.015f, 0.025f), new Vector3(0.036f, 0.03f, 0.056f), 8, 5);
            }

            Mesh mesh = mb.ToMesh(name + "_mesh", false);
            var bp = new Matrix4x4[bones.Count];
            for (int i = 0; i < bp.Length; i++) bp[i] = Matrix4x4.Translate(-bind[i]);
            mesh.bindposes = bp;
            Palette.Flush();
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones.ToArray();
            smr.rootBone = bones[Rigs.QBody];
            smr.sharedMaterials = new[] { Palette.Standard, Palette.Metal };
            smr.shadowCastingMode = ShadowCastingMode.On;
            smr.localBounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(1.4f, 1.3f, 2.0f));
            rig.body = smr;

            if (glow.HasValue && glow.Value.maxColorComponent > 0.05f)
            {
                var eyes = new GameObject("GlowEyes");
                eyes.transform.SetParent(bones[Rigs.QHead], false);
                var gb = new MeshBuilder();
                for (int s = -1; s <= 1; s += 2) gb.Ellipsoid(bind[Rigs.QHead] - bind[Rigs.QHead] + new Vector3(0.05f * s * w, 0.025f, 0.105f), new Vector3(0.016f, 0.012f, 0.008f), 6, 4);
                eyes.AddComponent<MeshFilter>().sharedMesh = gb.ToMesh("glow_eyes", false);
                var mr = eyes.AddComponent<MeshRenderer>();
                Color g = glow.Value;
                mr.sharedMaterial = Mats.Cached("glow_" + ColorUtility.ToHtmlStringRGB(g), () => Mats.Glow(g, 4f));
                rig.extraRenderers.Add(mr);
            }
            return rig;
        }
    }
}
