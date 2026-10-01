using System;
using System.Collections.Generic;
using ThienKhuyet.Core;
using ThienKhuyet.Gfx;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThienKhuyet.World
{
    /// <summary>
    /// Accumulates the geometry of one structure (all parts merged into one mesh with one submesh per material) and its box colliders.
    /// Local space: origin at the structure's pivot, +Z forward. Parts are described in metres.
    /// </summary>
    public sealed class StructureKit
    {
        public readonly Transform root;
        public readonly WorldAssets A;
        readonly MeshBuilder mb = new MeshBuilder();
        readonly List<Material> mats = new List<Material>();
        readonly Dictionary<Material, int> index = new Dictionary<Material, int>();
        readonly Transform colliders;

        public StructureKit(Transform parent, string name, Vector3 worldPos, float yawDeg, WorldAssets assets)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(worldPos, Quaternion.Euler(0f, yawDeg, 0f));
            root = go.transform;
            A = assets;
            var c = new GameObject("Colliders");
            c.transform.SetParent(root, false);
            c.layer = GameLayers.Environment;
            colliders = c.transform;
        }

        public MeshBuilder M(Material m)
        {
            if (!index.TryGetValue(m, out int i))
            {
                i = mats.Count;
                mats.Add(m);
                index[m] = i;
            }
            mb.Sub(i);
            return mb;
        }

        // ------------------------------------------------------------------ colliders
        public void Box(Vector3 center, Vector3 size, float yawDeg = 0f)
        {
            var go = new GameObject("Box");
            go.layer = GameLayers.Environment;
            go.transform.SetParent(colliders, false);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            var bc = go.AddComponent<BoxCollider>();
            bc.size = size;
        }

        public void Capsule(Vector3 baseCenter, float height, float radius)
        {
            var go = new GameObject("Cap");
            go.layer = GameLayers.Environment;
            go.transform.SetParent(colliders, false);
            go.transform.localPosition = baseCenter + new Vector3(0f, height * 0.5f, 0f);
            var cc = go.AddComponent<CapsuleCollider>();
            cc.radius = radius;
            cc.height = height;
        }

        public Light AddLight(Vector3 local, Color color, float range, float intensity)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(root, false);
            go.transform.localPosition = local;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            return l;
        }

        // ------------------------------------------------------------------ primitives
        /// <summary>Axis-aligned box standing on 'baseCenter' (bottom centre), optionally with a collider.</summary>
        public void Block(Material m, Vector3 baseCenter, Vector3 size, bool collide = true, float uvTile = 2f)
        {
            M(m).Box(baseCenter + new Vector3(0f, size.y * 0.5f, 0f), size, uvTile);
            if (collide) Box(baseCenter + new Vector3(0f, size.y * 0.5f, 0f), size);
        }

        /// <summary>Wall segment from a to b (XZ), height h, thickness t.</summary>
        public void WallSeg(Material m, Vector2 a, Vector2 b, float y0, float y1, float t, bool collide = true)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.02f || y1 - y0 < 0.02f) return;
            float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            Vector2 mid = (a + b) * 0.5f;
            Vector3 center = new Vector3(mid.x, (y0 + y1) * 0.5f, mid.y);
            mb.Sub(0);
            M(m);
            mb.Push(center, Quaternion.Euler(0f, yaw, 0f), Vector3.one);
            mb.Box(Vector3.zero, new Vector3(t, y1 - y0, len), 2.5f);
            mb.Pop();
            if (collide) Box(center, new Vector3(t, y1 - y0, len), yaw);
        }

        /// <summary>Wall with a rectangular opening (door or window). tCenter in 0..1 along the wall.</summary>
        public void WallWithOpening(Material m, Vector2 a, Vector2 b, float baseY, float height, float t, float tCenter, float openW, float sill, float openH, Material lintelMat = null)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            Vector2 dir = d / len;
            float c = tCenter * len;
            float o0 = Mathf.Max(0.05f, c - openW * 0.5f), o1 = Mathf.Min(len - 0.05f, c + openW * 0.5f);
            WallSeg(m, a, a + dir * o0, baseY, baseY + height, t);
            WallSeg(m, a + dir * o1, b, baseY, baseY + height, t);
            if (sill > 0.02f) WallSeg(m, a + dir * o0, a + dir * o1, baseY, baseY + sill, t);
            if (sill + openH < height - 0.02f) WallSeg(lintelMat ?? m, a + dir * o0, a + dir * o1, baseY + sill + openH, baseY + height, t);
        }

        public void Pillar(Material m, Vector3 baseCenter, float h, float r, bool capital = true)
        {
            M(m);
            mb.Cylinder(baseCenter, 0.18f, r * 1.35f, r * 1.2f, 10, true, true, 1.5f);
            mb.Cylinder(baseCenter + new Vector3(0f, 0.18f, 0f), h - 0.36f, r, r * 0.92f, 10, false, false, 1.5f);
            if (capital) mb.Cylinder(baseCenter + new Vector3(0f, h - 0.18f, 0f), 0.18f, r * 1.25f, r * 1.4f, 10, true, true, 1.5f);
            Capsule(baseCenter, h, r);
        }

        public void Roof(Material m, Vector3 baseCenter, float w, float d, float ridge, float upturn = 0.55f, bool collide = false)
        {
            M(m).CurvedRoof(baseCenter, w, d, ridge, upturn, 14, 6, 2.2f);
            // ridge beam
            M(A.matDarkWood).Box(baseCenter + new Vector3(0f, ridge + 0.02f, 0f), new Vector3(0.16f, 0.16f, d * 0.98f), 1f);
        }

        public void GableRoof(Material m, Vector3 baseCenter, float w, float d, float h, float overhang = 0.35f)
        {
            M(m).GableRoof(baseCenter, w, d, h, overhang, 2f);
        }

        public void Steps(Material m, Vector3 baseCenter, float width, float depth, int count, float stepH)
        {
            for (int i = 0; i < count; i++)
            {
                float dz = depth / count;
                Block(m, baseCenter + new Vector3(0f, 0f, -dz * i - dz * 0.5f), new Vector3(width, stepH * (count - i), dz + 0.02f), true, 2f);
            }
        }

        public void Lantern(Vector3 pos, float scale = 1f)
        {
            M(A.matLantern).Sphere(pos, 0.16f * scale, 8, 6);
            M(A.matDarkWood).Cylinder(pos + new Vector3(0f, 0.15f * scale, 0f), 0.06f * scale, 0.07f * scale, 0.07f * scale, 6, true, true, 1f);
            M(A.matDarkWood).Cylinder(pos - new Vector3(0f, 0.2f * scale, 0f), 0.06f * scale, 0.07f * scale, 0.07f * scale, 6, true, true, 1f);
        }

        public void Banner(Vector3 baseCenter, float height, Color cloth, float width = 0.9f)
        {
            Material cloth_ = Mats.Cached("banner_" + ColorUtility.ToHtmlStringRGB(cloth), () => Mats.Lit(cloth, 0.05f, 0f, A.cloth));
            M(A.matDarkWood).Cylinder(baseCenter, height, 0.07f, 0.06f, 7, true, true, 1f);
            M(A.matDarkWood).Box(baseCenter + new Vector3(0f, height - 0.15f, 0.3f * width), new Vector3(0.05f, 0.05f, width + 0.2f), 1f);
            M(cloth_).Box(baseCenter + new Vector3(0f, height - 0.15f - width * 1.1f * 0.5f, 0.3f * width), new Vector3(0.025f, width * 1.1f, width), 1f);
            Capsule(baseCenter, height, 0.1f);
        }

        public void Fence(Vector2 a, Vector2 b, float height = 1.1f)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.1f) return;
            Vector2 dir = d / len;
            int posts = Mathf.Max(2, Mathf.CeilToInt(len / 2f) + 1);
            for (int i = 0; i < posts; i++)
            {
                Vector2 p = a + dir * (len * i / (posts - 1));
                M(A.matDarkWood).Cylinder(new Vector3(p.x, -0.05f, p.y), height + 0.05f, 0.07f, 0.06f, 6, true, true, 1f);
            }
            WallSeg(A.matDarkWood, a, b, height * 0.45f, height * 0.45f + 0.1f, 0.06f, false);
            WallSeg(A.matDarkWood, a, b, height * 0.8f, height * 0.8f + 0.1f, 0.06f, false);
            Vector2 mid = (a + b) * 0.5f;
            Box(new Vector3(mid.x, height * 0.5f, mid.y), new Vector3(0.12f, height, len), Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg);
        }

        public GameObject Finish(bool castShadows = true)
        {
            var go = new GameObject("Mesh");
            go.transform.SetParent(root, false);
            go.layer = GameLayers.Environment;
            var mesh = mb.ToMesh(root.name + "_mesh", true);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats.ToArray();
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return root.gameObject;
        }
    }

    /// <summary>Composite structure builders (houses, halls, tents, palisades, bridges ...).</summary>
    public static class Structures
    {
        /// <summary>Villager house with door, windows, wooden frame, curved roof and simple furniture. Door faces +Z.</summary>
        public static void House(StructureKit k, Vector3 baseCenter, float w, float d, float wallH, Material wallMat, Material roofMat, bool bedInside = true, bool chimney = false)
        {
            float t = 0.28f;
            float hx = w * 0.5f, hz = d * 0.5f;
            Vector2 fl = new Vector2(baseCenter.x - hx, baseCenter.z + hz), fr = new Vector2(baseCenter.x + hx, baseCenter.z + hz);
            Vector2 bl = new Vector2(baseCenter.x - hx, baseCenter.z - hz), br = new Vector2(baseCenter.x + hx, baseCenter.z - hz);
            float y0 = baseCenter.y;
            // foundation and floor
            k.Block(k.A.matCobble, new Vector3(baseCenter.x, y0 - 0.6f, baseCenter.z), new Vector3(w + 0.7f, 0.9f, d + 0.7f), true, 2f);
            k.M(k.A.matPlank).Box(new Vector3(baseCenter.x, y0 + 0.31f, baseCenter.z), new Vector3(w - 0.1f, 0.04f, d - 0.1f), 1.5f);
            // walls sit on the foundation
            float wy = y0 + 0.3f;
            k.WallWithOpening(wallMat, fl, fr, wy, wallH, t, 0.5f, 1.35f, 0f, 2.15f, k.A.matDarkWood);
            k.WallSeg(wallMat, br, bl, wy, wy + wallH, t);
            k.WallWithOpening(wallMat, bl, fl, wy, wallH, t, 0.5f, 1.0f, 1.0f, 0.95f, k.A.matDarkWood);
            k.WallWithOpening(wallMat, fr, br, wy, wallH, t, 0.5f, 1.0f, 1.0f, 0.95f, k.A.matDarkWood);
            // timber frame
            Vector2[] corners = { fl, fr, bl, br };
            for (int i = 0; i < 4; i++)
                k.M(k.A.matDarkWood).Box(new Vector3(corners[i].x, y0 + 0.3f + wallH * 0.5f, corners[i].y), new Vector3(0.32f, wallH, 0.32f), 1f);
            k.M(k.A.matDarkWood).Box(new Vector3(baseCenter.x, y0 + 0.3f + wallH, baseCenter.z + hz), new Vector3(w + 0.3f, 0.22f, 0.34f), 1f);
            k.M(k.A.matDarkWood).Box(new Vector3(baseCenter.x, y0 + 0.3f + wallH, baseCenter.z - hz), new Vector3(w + 0.3f, 0.22f, 0.34f), 1f);
            // door frame
            k.M(k.A.matDarkWood).Box(new Vector3(baseCenter.x - 0.74f, y0 + 0.3f + 1.1f, baseCenter.z + hz), new Vector3(0.14f, 2.2f, 0.34f), 1f);
            k.M(k.A.matDarkWood).Box(new Vector3(baseCenter.x + 0.74f, y0 + 0.3f + 1.1f, baseCenter.z + hz), new Vector3(0.14f, 2.2f, 0.34f), 1f);
            // roof
            k.Roof(roofMat, new Vector3(baseCenter.x, y0 + 0.3f + wallH + 0.1f, baseCenter.z), w + 1.7f, d + 1.7f, 1.5f + d * 0.08f, 0.5f);
            k.Lantern(new Vector3(baseCenter.x + 1.0f, y0 + 0.3f + 2.4f, baseCenter.z + hz + 0.4f), 0.8f);
            if (chimney) k.Block(k.A.matCobble, new Vector3(baseCenter.x + hx - 0.8f, y0 + 0.3f + wallH, baseCenter.z - hz + 0.8f), new Vector3(0.7f, 2.2f, 0.7f), false, 1.5f);
            // furniture
            if (bedInside)
            {
                k.Block(k.A.matDarkWood, new Vector3(baseCenter.x - hx + 1.1f, y0 + 0.33f, baseCenter.z - hz + 1.5f), new Vector3(1.0f, 0.4f, 2.0f), false, 1f);
                k.M(k.A.matPlaster).Box(new Vector3(baseCenter.x - hx + 1.1f, y0 + 0.78f, baseCenter.z - hz + 0.8f), new Vector3(0.55f, 0.14f, 0.4f), 1f);
                k.Block(k.A.matDarkWood, new Vector3(baseCenter.x + hx - 1.2f, y0 + 0.33f, baseCenter.z - 0.2f), new Vector3(1.0f, 0.75f, 0.7f), false, 1f);
            }
        }

        public static void Tent(StructureKit k, Vector3 baseCenter, float w, float d, Color cloth)
        {
            Material m = Mats.Cached("tent_" + ColorUtility.ToHtmlStringRGB(cloth), () => Mats.Lit(cloth, 0.05f, 0f, k.A.cloth));
            float hx = w * 0.5f, hz = d * 0.5f, h = 2.3f;
            k.M(m).GableRoof(baseCenter, w - 0.6f, d, h, 0.3f, 2f);
            k.M(k.A.matDarkWood).Cylinder(baseCenter + new Vector3(0f, 0f, hz), h + 0.2f, 0.06f, 0.05f, 6, true, true, 1f);
            k.M(k.A.matDarkWood).Cylinder(baseCenter + new Vector3(0f, 0f, -hz), h + 0.2f, 0.06f, 0.05f, 6, true, true, 1f);
            k.Box(baseCenter + new Vector3(0f, 0.8f, 0f), new Vector3(w * 0.7f, 1.6f, d * 0.9f));
        }

        public static void Well(StructureKit k, Vector3 baseCenter)
        {
            k.M(k.A.matCobble).Cylinder(baseCenter, 0.9f, 1.15f, 1.0f, 12, true, true, 1.5f);
            k.M(k.A.matLake).Disc(baseCenter + new Vector3(0f, 0.55f, 0f), 0.82f, 12, true, 1f);
            k.Capsule(baseCenter, 1.0f, 1.1f);
            k.M(k.A.matDarkWood).Box(baseCenter + new Vector3(-1.0f, 1.5f, 0f), new Vector3(0.18f, 3f, 0.18f), 1f);
            k.M(k.A.matDarkWood).Box(baseCenter + new Vector3(1.0f, 1.5f, 0f), new Vector3(0.18f, 3f, 0.18f), 1f);
            k.M(k.A.matDarkWood).Box(baseCenter + new Vector3(0f, 2.9f, 0f), new Vector3(2.5f, 0.2f, 0.25f), 1f);
            k.GableRoof(k.A.matRoofDark, baseCenter + new Vector3(0f, 2.95f, 0f), 2.2f, 1.8f, 0.8f, 0.3f);
        }

        public static void Stall(StructureKit k, Vector3 baseCenter, Color awning, float yawDeg = 0f)
        {
            Material m = Mats.Cached("awning_" + ColorUtility.ToHtmlStringRGB(awning), () => Mats.Lit(awning, 0.05f, 0f, k.A.cloth));
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * 1.2f, z = (i < 2 ? -1f : 1f) * 0.8f;
                k.M(k.A.matDarkWood).Cylinder(baseCenter + new Vector3(x, 0f, z), 2.3f, 0.06f, 0.05f, 6, true, true, 1f);
            }
            k.M(m).GableRoof(baseCenter + new Vector3(0f, 2.3f, 0f), 2.6f, 2.0f, 0.55f, 0.15f, 2f);
            k.Block(k.A.matDarkWood, baseCenter + new Vector3(0f, 0f, 0.6f), new Vector3(2.2f, 0.9f, 0.7f), true, 1.5f);
            k.M(k.A.matBush).Ellipsoid(baseCenter + new Vector3(-0.5f, 1.0f, 0.6f), new Vector3(0.28f, 0.14f, 0.2f), 6, 4, 1f);
            k.M(k.A.matPinkLeaf).Ellipsoid(baseCenter + new Vector3(0.4f, 1.0f, 0.6f), new Vector3(0.26f, 0.14f, 0.2f), 6, 4, 1f);
        }

        public static void Palisade(StructureKit k, Vector2 center, float radius, float gapAngleDeg, float gapWidthDeg, Func<float, float, float> ground)
        {
            int count = Mathf.RoundToInt(radius * 2f * Mathf.PI / 0.9f);
            for (int i = 0; i < count; i++)
            {
                float a = (float)i / count * 360f;
                if (Mathf.Abs(Mathf.DeltaAngle(a, gapAngleDeg)) < gapWidthDeg * 0.5f) continue;
                float rad = a * Mathf.Deg2Rad;
                float x = center.x + Mathf.Sin(rad) * radius, z = center.y + Mathf.Cos(rad) * radius;
                float h = 2.7f + Mathx.Hash01(i, 3, 5) * 0.8f;
                float gy = ground(x, z);
                k.M(k.A.matDarkWood).Cylinder(new Vector3(x, gy - 0.4f, z), h + 0.4f, 0.17f, 0.12f, 6, true, false, 1f);
                k.M(k.A.matDarkWood).Cone(new Vector3(x, gy + h, z), 0.35f, 0.14f, 6, true, 1f);
            }
            // collision as segments between gate posts
            int segs = 24;
            for (int i = 0; i < segs; i++)
            {
                float a0 = (float)i / segs * 360f, a1 = (float)(i + 1) / segs * 360f, am = (a0 + a1) * 0.5f;
                if (Mathf.Abs(Mathf.DeltaAngle(am, gapAngleDeg)) < gapWidthDeg * 0.5f + 360f / segs * 0.5f) continue;
                Vector2 p0 = center + new Vector2(Mathf.Sin(a0 * Mathf.Deg2Rad), Mathf.Cos(a0 * Mathf.Deg2Rad)) * radius;
                Vector2 p1 = center + new Vector2(Mathf.Sin(a1 * Mathf.Deg2Rad), Mathf.Cos(a1 * Mathf.Deg2Rad)) * radius;
                Vector2 mid = (p0 + p1) * 0.5f;
                float gy = ground(mid.x, mid.y);
                k.Box(new Vector3(mid.x, gy + 1.8f, mid.y), new Vector3(0.5f, 3.6f, (p1 - p0).magnitude), Mathf.Atan2(p1.x - p0.x, p1.y - p0.y) * Mathf.Rad2Deg);
            }
        }

        public static void Tower(StructureKit k, Vector3 baseCenter, float height)
        {
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * 1.1f, z = (i < 2 ? -1f : 1f) * 1.1f;
                k.M(k.A.matDarkWood).Cylinder(baseCenter + new Vector3(x, -0.4f, z), height + 0.4f, 0.16f, 0.12f, 6, true, true, 1f);
            }
            k.Block(k.A.matPlank, baseCenter + new Vector3(0f, height - 0.1f, 0f), new Vector3(3.0f, 0.2f, 3.0f), true, 1.5f);
            k.WallSeg(k.A.matPlank, new Vector2(baseCenter.x - 1.4f, baseCenter.z + 1.4f), new Vector2(baseCenter.x + 1.4f, baseCenter.z + 1.4f), baseCenter.y + height, baseCenter.y + height + 0.9f, 0.1f, false);
            k.WallSeg(k.A.matPlank, new Vector2(baseCenter.x - 1.4f, baseCenter.z - 1.4f), new Vector2(baseCenter.x + 1.4f, baseCenter.z - 1.4f), baseCenter.y + height, baseCenter.y + height + 0.9f, 0.1f, false);
            k.WallSeg(k.A.matPlank, new Vector2(baseCenter.x - 1.4f, baseCenter.z - 1.4f), new Vector2(baseCenter.x - 1.4f, baseCenter.z + 1.4f), baseCenter.y + height, baseCenter.y + height + 0.9f, 0.1f, false);
            k.WallSeg(k.A.matPlank, new Vector2(baseCenter.x + 1.4f, baseCenter.z - 1.4f), new Vector2(baseCenter.x + 1.4f, baseCenter.z + 1.4f), baseCenter.y + height, baseCenter.y + height + 0.9f, 0.1f, false);
            k.GableRoof(k.A.matRoofRed, baseCenter + new Vector3(0f, height + 1.9f, 0f), 3.2f, 3.2f, 1.2f, 0.3f);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * 1.3f, z = (i < 2 ? -1f : 1f) * 1.3f;
                k.M(k.A.matDarkWood).Cylinder(baseCenter + new Vector3(x, height, z), 1.95f, 0.07f, 0.07f, 5, true, true, 1f);
            }
            k.Box(baseCenter + new Vector3(0f, height * 0.5f, 0f), new Vector3(2.6f, height, 2.6f));
        }

        /// <summary>Arched wooden bridge along local +Z (centre at baseCenter, deck at baseCenter.y).</summary>
        public static void Bridge(StructureKit k, Vector3 baseCenter, float length, float width)
        {
            int segs = 10;
            for (int i = 0; i < segs; i++)
            {
                float t0 = (float)i / segs, t1 = (float)(i + 1) / segs;
                float z0 = Mathf.Lerp(-length * 0.5f, length * 0.5f, t0), z1 = Mathf.Lerp(-length * 0.5f, length * 0.5f, t1);
                float y0 = baseCenter.y + Mathf.Sin(t0 * Mathf.PI) * 0.7f, y1 = baseCenter.y + Mathf.Sin(t1 * Mathf.PI) * 0.7f;
                Vector3 c = new Vector3(baseCenter.x, (y0 + y1) * 0.5f, baseCenter.z + (z0 + z1) * 0.5f);
                float len = Vector3.Distance(new Vector3(0, y0, z0), new Vector3(0, y1, z1));
                float pitch = -Mathf.Atan2(y1 - y0, z1 - z0) * Mathf.Rad2Deg;
                k.M(k.A.matPlank).Push(c, Quaternion.Euler(pitch, 0f, 0f), Vector3.one);
                k.M(k.A.matPlank).Box(Vector3.zero, new Vector3(width, 0.22f, len + 0.05f), 1.5f);
                k.M(k.A.matPlank).Pop();
                k.Box(c, new Vector3(width, 0.3f, len + 0.05f), 0f);
                // rails
                foreach (float s in new[] { -1f, 1f })
                {
                    k.M(k.A.matDarkWood).Push(c + new Vector3(s * (width * 0.5f - 0.05f), 0.55f, 0f), Quaternion.Euler(pitch, 0f, 0f), Vector3.one);
                    k.M(k.A.matDarkWood).Box(Vector3.zero, new Vector3(0.09f, 0.09f, len + 0.05f), 1f);
                    k.M(k.A.matDarkWood).Pop();
                    k.M(k.A.matDarkWood).Box(c + new Vector3(s * (width * 0.5f - 0.05f), 0.28f, 0f), new Vector3(0.09f, 0.6f, 0.09f), 1f);
                    k.Box(c + new Vector3(s * (width * 0.5f - 0.05f), 0.6f, 0f), new Vector3(0.12f, 0.8f, len + 0.05f), 0f);
                }
            }
            // abutments
            foreach (float s in new[] { -1f, 1f })
                k.Block(k.A.matCobble, new Vector3(baseCenter.x, baseCenter.y - 1.4f, baseCenter.z + s * (length * 0.5f - 0.3f)), new Vector3(width + 0.8f, 1.5f, 1.2f), false, 2f);
        }

        public static void Gate(StructureKit k, Vector3 baseCenter, float width, float height, Material roof)
        {
            foreach (float s in new[] { -1f, 1f })
                k.Pillar(k.A.matDarkWood, baseCenter + new Vector3(s * width * 0.5f, 0f, 0f), height, 0.26f, true);
            k.M(k.A.matDarkWood).Box(baseCenter + new Vector3(0f, height + 0.1f, 0f), new Vector3(width + 1.2f, 0.34f, 0.5f), 1f);
            k.M(k.A.matDarkWood).Box(baseCenter + new Vector3(0f, height - 0.8f, 0f), new Vector3(width, 0.22f, 0.36f), 1f);
            k.Roof(roof, baseCenter + new Vector3(0f, height + 0.3f, 0f), width + 2.4f, 2.2f, 0.9f, 0.5f);
        }

        public static void Pagoda(StructureKit k, Vector3 baseCenter, float baseSize, int tiers, Material wall, Material roof)
        {
            float y = baseCenter.y;
            k.Block(k.A.matCobble, new Vector3(baseCenter.x, y - 0.4f, baseCenter.z), new Vector3(baseSize + 1.6f, 1.2f, baseSize + 1.6f), true, 2f);
            for (int t = 0; t < tiers; t++)
            {
                float s = baseSize * Mathf.Lerp(1f, 0.6f, (float)t / Mathf.Max(1, tiers - 1));
                float h = 3.3f;
                k.Block(wall, new Vector3(baseCenter.x, y + 0.8f, baseCenter.z), new Vector3(s, h, s), true, 2.5f);
                for (int c = 0; c < 4; c++)
                {
                    float x = (c % 2 == 0 ? -1f : 1f) * s * 0.5f, z = (c < 2 ? -1f : 1f) * s * 0.5f;
                    k.M(k.A.matDarkWood).Box(new Vector3(baseCenter.x + x, y + 0.8f + h * 0.5f, baseCenter.z + z), new Vector3(0.3f, h, 0.3f), 1f);
                }
                k.Roof(roof, new Vector3(baseCenter.x, y + 0.8f + h, baseCenter.z), s + 2.8f, s + 2.8f, 1.0f, 0.75f);
                y += h + 0.9f;
            }
            k.M(k.A.matMetal).Cylinder(new Vector3(baseCenter.x, y + 0.9f, baseCenter.z), 2.4f, 0.1f, 0.03f, 6, true, true, 1f);
            k.M(k.A.matGlowGold).Sphere(new Vector3(baseCenter.x, y + 1.6f, baseCenter.z), 0.22f, 8, 6);
        }
    }
}
