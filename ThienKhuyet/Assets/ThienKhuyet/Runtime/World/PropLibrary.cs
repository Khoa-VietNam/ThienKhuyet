using System.Collections.Generic;
using ThienKhuyet.Core;
using ThienKhuyet.Gfx;
using UnityEngine;

namespace ThienKhuyet.World
{
    /// <summary>One instanced vegetation / rock type with two LOD meshes and per-submesh material variants.</summary>
    public sealed class Species
    {
        public string id;
        public Mesh[] lod = new Mesh[2];
        public Material[][] materials;        // [submesh][variant]
        public Vector2 scaleRange = new Vector2(0.85f, 1.25f);
        public float colliderRadius;          // 0 = no collider
        public float colliderHeight = 4f;
        public bool castShadows = true;
        public float maxDrawDistance = 260f;
        public int Variants => materials[0].Length;
        public int SubmeshCount => materials.Length;
    }

    /// <summary>Procedural tree, bush and rock meshes (no imported models).</summary>
    public sealed class PropLibrary
    {
        public const int Pine = 0, Broad = 1, Bamboo = 2, Blossom = 3, Bush = 4, RockSmall = 5, RockMid = 6, RockBig = 7, Reed = 8, Dead = 9, Count = 10;

        public readonly Species[] species = new Species[Count];

        public PropLibrary(WorldAssets a)
        {
            species[Pine] = Make("pine", a, new[] { new[] { a.matBark }, new[] { a.matPine } }, 0.85f, 1.35f, 0.38f, 5f, BuildPine);
            species[Broad] = Make("broad", a, new[] { new[] { a.matBark }, a.matLeaf }, 0.8f, 1.3f, 0.34f, 5f, BuildBroad);
            species[Bamboo] = Make("bamboo", a, new[] { new[] { a.matBamboo }, new[] { a.matBambooLeaf } }, 0.85f, 1.2f, 0.9f, 6f, BuildBamboo);
            species[Blossom] = Make("blossom", a, new[] { new[] { a.matBark }, new[] { a.matPinkLeaf } }, 0.85f, 1.2f, 0.3f, 3.5f, BuildBlossom);
            species[Bush] = Make("bush", a, new[] { a.matLeaf }, 0.7f, 1.4f, 0f, 1f, BuildBush);
            species[RockSmall] = Make("rock_s", a, new[] { new[] { a.matRock } }, 0.7f, 1.5f, 0f, 1f, (mb, lod, seed) => BuildRock(mb, lod, seed, 0.6f));
            species[RockMid] = Make("rock_m", a, new[] { new[] { a.matRock } }, 0.8f, 1.4f, 1.0f, 2f, (mb, lod, seed) => BuildRock(mb, lod, seed, 1.2f));
            species[RockBig] = Make("rock_b", a, new[] { new[] { a.matRock } }, 0.9f, 1.5f, 2.2f, 4f, (mb, lod, seed) => BuildRock(mb, lod, seed, 2.3f));
            species[Reed] = Make("reed", a, new[] { new[] { a.matReed } }, 0.8f, 1.3f, 0f, 1f, BuildReed);
            species[Dead] = Make("dead", a, new[] { new[] { a.matDeadWood } }, 0.8f, 1.2f, 0.3f, 4f, BuildDead);
            species[Bush].castShadows = false;
            species[Bush].maxDrawDistance = 130f;
            species[Reed].castShadows = false;
            species[Reed].maxDrawDistance = 110f;
            species[RockSmall].castShadows = false;
            species[RockSmall].maxDrawDistance = 150f;
        }

        delegate void Builder(MeshBuilder mb, int lod, int seed);

        static Species Make(string id, WorldAssets a, Material[][] mats, float smin, float smax, float collR, float collH, Builder build)
        {
            var s = new Species { id = id, materials = mats, scaleRange = new Vector2(smin, smax), colliderRadius = collR, colliderHeight = collH };
            for (int lod = 0; lod < 2; lod++)
            {
                var mb = new MeshBuilder();
                mb.Sub(mats.Length - 1);
                mb.Sub(0);
                build(mb, lod, id.GetHashCode() & 0xffff);
                s.lod[lod] = mb.ToMesh(id + "_lod" + lod, true);
            }
            return s;
        }

        // ------------------------------------------------------------------ builders (sub 0 = wood / main, sub 1 = leaves)
        static void BuildPine(MeshBuilder mb, int lod, int seed)
        {
            int seg = lod == 0 ? 8 : 6;
            mb.Sub(0);
            mb.Cylinder(Vector3.zero, 8.6f, 0.34f, 0.09f, seg, false, true, 2f);
            mb.Sub(1);
            int tiers = lod == 0 ? 6 : 4;
            for (int t = 0; t < tiers; t++)
            {
                float k = (float)t / (tiers - 1);
                float baseY = 1.9f + k * 5.2f;
                float r = Mathf.Lerp(2.5f, 0.75f, k);
                int start = mb.VertexCount;
                mb.Push(new Vector3(0f, 0f, 0f), Quaternion.Euler(0f, t * 37f, 0f), Vector3.one);
                mb.Cone(new Vector3(0f, baseY, 0f), Mathf.Lerp(2.9f, 2.0f, k), r, seg + 2, true, 3f);
                mb.Pop();
                mb.Displace(start, 0.16f * (1f - k * 0.5f), 1.7f, seed + t * 7);
            }
        }

        static void BuildBroad(MeshBuilder mb, int lod, int seed)
        {
            int seg = lod == 0 ? 9 : 6;
            int rings = lod == 0 ? 6 : 4;
            mb.Sub(0);
            mb.Limb(new Vector3(0f, -0.1f, 0f), new Vector3(0.12f, 2.1f, 0.08f), 0.36f, 0.26f, seg, false, false);
            mb.Limb(new Vector3(0.12f, 2.0f, 0.08f), new Vector3(-0.08f, 3.9f, -0.1f), 0.26f, 0.15f, seg, false, false);
            mb.Limb(new Vector3(0.05f, 2.9f, 0f), new Vector3(1.25f, 4.5f, 0.55f), 0.12f, 0.07f, 6, false, false);
            mb.Limb(new Vector3(-0.05f, 3.1f, 0f), new Vector3(-1.2f, 4.4f, -0.6f), 0.12f, 0.07f, 6, false, false);
            mb.Sub(1);
            Vector3[] centers = { new Vector3(0f, 5.3f, 0f), new Vector3(1.4f, 4.9f, 0.6f), new Vector3(-1.4f, 5.0f, -0.7f), new Vector3(0.5f, 6.5f, -0.3f), new Vector3(-0.7f, 6.1f, 0.9f) };
            float[] sizes = { 1.9f, 1.5f, 1.55f, 1.4f, 1.35f };
            int count = lod == 0 ? 5 : 3;
            for (int i = 0; i < count; i++)
            {
                int start = mb.VertexCount;
                mb.Ellipsoid(centers[i], new Vector3(sizes[i], sizes[i] * 0.78f, sizes[i]), seg + 1, rings, 2.5f);
                mb.Displace(start, 0.28f, 1.1f, seed + i * 5);
            }
        }

        static void BuildBamboo(MeshBuilder mb, int lod, int seed)
        {
            var rng = new Rng(seed);
            int culms = lod == 0 ? 7 : 4;
            for (int c = 0; c < culms; c++)
            {
                Vector2 o = rng.InsideCircle() * 0.8f;
                float h = 7f + rng.Value() * 3.5f;
                float lean = (rng.Value() - 0.5f) * 0.9f, leanZ = (rng.Value() - 0.5f) * 0.9f;
                Vector3 p0 = new Vector3(o.x, 0f, o.y);
                Vector3 p1 = p0 + new Vector3(lean * 0.3f, h * 0.45f, leanZ * 0.3f);
                Vector3 p2 = p0 + new Vector3(lean, h * 0.95f, leanZ);
                mb.Sub(0);
                mb.Limb(p0, p1, 0.055f, 0.045f, 5, false, false);
                mb.Limb(p1, p2, 0.045f, 0.028f, 5, false, true);
                if (lod == 0)
                    for (int n = 1; n < 6; n++)
                    {
                        Vector3 np = Vector3.Lerp(p0, p2, n / 6f);
                        mb.Cylinder(np - new Vector3(0f, 0.03f, 0f), 0.06f, 0.06f, 0.06f, 5, false, false);
                    }
                mb.Sub(1);
                for (int l = 0; l < (lod == 0 ? 3 : 2); l++)
                {
                    int st = mb.VertexCount;
                    mb.Ellipsoid(p2 + new Vector3(Mathf.Cos(l * 2.1f) * 0.3f, -0.5f - l * 0.5f, Mathf.Sin(l * 2.1f) * 0.3f), new Vector3(0.45f, 0.18f, 0.45f), 6, 3, 2f);
                    mb.Displace(st, 0.1f, 2f, seed + c * 3 + l);
                }
            }
        }

        static void BuildBlossom(MeshBuilder mb, int lod, int seed)
        {
            int seg = lod == 0 ? 9 : 6;
            mb.Sub(0);
            mb.Limb(new Vector3(0f, -0.1f, 0f), new Vector3(0.35f, 1.5f, 0.1f), 0.24f, 0.17f, seg, false, false);
            mb.Limb(new Vector3(0.35f, 1.4f, 0.1f), new Vector3(-0.15f, 2.7f, -0.2f), 0.17f, 0.1f, seg, false, false);
            mb.Limb(new Vector3(0.1f, 1.9f, 0f), new Vector3(1.2f, 2.9f, 0.4f), 0.09f, 0.05f, 6, false, false);
            mb.Limb(new Vector3(0f, 2.2f, 0f), new Vector3(-1.1f, 3.0f, -0.4f), 0.09f, 0.05f, 6, false, false);
            mb.Sub(1);
            Vector3[] c = { new Vector3(0f, 3.4f, 0f), new Vector3(1.3f, 3.1f, 0.4f), new Vector3(-1.2f, 3.2f, -0.4f), new Vector3(0.2f, 4.0f, -0.5f), new Vector3(-0.5f, 3.9f, 0.7f) };
            int count = lod == 0 ? 5 : 3;
            for (int i = 0; i < count; i++)
            {
                int st = mb.VertexCount;
                mb.Ellipsoid(c[i], new Vector3(1.35f, 0.95f, 1.3f), seg + 1, lod == 0 ? 6 : 4, 2.2f);
                mb.Displace(st, 0.24f, 1.3f, seed + i * 11);
            }
        }

        static void BuildBush(MeshBuilder mb, int lod, int seed)
        {
            mb.Sub(0);
            int count = lod == 0 ? 3 : 2;
            for (int i = 0; i < count; i++)
            {
                int st = mb.VertexCount;
                float a = i * 2.3f;
                mb.Ellipsoid(new Vector3(Mathf.Cos(a) * 0.45f, 0.5f + i * 0.08f, Mathf.Sin(a) * 0.45f), new Vector3(0.75f, 0.6f, 0.75f), lod == 0 ? 8 : 6, lod == 0 ? 5 : 4, 1.5f);
                mb.Displace(st, 0.16f, 2f, seed + i * 3);
            }
        }

        static void BuildRock(MeshBuilder mb, int lod, int seed, float radius)
        {
            mb.Sub(0);
            int seg = lod == 0 ? 9 : 6, rings = lod == 0 ? 6 : 4;
            int st = mb.VertexCount;
            mb.Ellipsoid(new Vector3(0f, radius * 0.38f, 0f), new Vector3(radius, radius * 0.72f, radius * 0.88f), seg, rings, 2.5f);
            mb.Displace(st, radius * 0.22f, 0.9f / Mathf.Max(0.4f, radius), seed);
            mb.Remap(st, v => new Vector3(v.x, Mathf.Max(v.y, 0.02f), v.z));
            if (radius > 1.1f)
            {
                int s2 = mb.VertexCount;
                mb.Ellipsoid(new Vector3(radius * 0.7f, radius * 0.25f, radius * 0.35f), new Vector3(radius * 0.5f, radius * 0.38f, radius * 0.45f), seg, rings, 2.5f);
                mb.Displace(s2, radius * 0.12f, 1.2f / radius, seed + 5);
                mb.Remap(s2, v => new Vector3(v.x, Mathf.Max(v.y, 0.02f), v.z));
            }
        }

        static void BuildReed(MeshBuilder mb, int lod, int seed)
        {
            var rng = new Rng(seed);
            mb.Sub(0);
            int n = lod == 0 ? 9 : 5;
            for (int i = 0; i < n; i++)
            {
                Vector2 o = rng.InsideCircle() * 0.45f;
                float h = 1.1f + rng.Value() * 1.1f;
                Vector3 b = new Vector3(o.x, -0.05f, o.y);
                Vector3 t = b + new Vector3((rng.Value() - 0.5f) * 0.5f, h, (rng.Value() - 0.5f) * 0.5f);
                mb.Limb(b, t, 0.022f, 0.004f, 4, false, false);
            }
        }

        static void BuildDead(MeshBuilder mb, int lod, int seed)
        {
            mb.Sub(0);
            mb.Limb(new Vector3(0f, -0.1f, 0f), new Vector3(0.1f, 2.5f, 0.05f), 0.3f, 0.14f, 7, false, false);
            mb.Limb(new Vector3(0.1f, 2.5f, 0.05f), new Vector3(-0.2f, 4.2f, 0.1f), 0.14f, 0.05f, 6, false, true);
            mb.Limb(new Vector3(0.05f, 1.9f, 0f), new Vector3(1.4f, 3.2f, 0.3f), 0.09f, 0.03f, 5, false, true);
            mb.Limb(new Vector3(0f, 2.4f, 0f), new Vector3(-1.2f, 3.6f, -0.5f), 0.08f, 0.03f, 5, false, true);
            if (lod == 0) mb.Limb(new Vector3(0.1f, 3.2f, 0f), new Vector3(0.9f, 4.3f, -0.6f), 0.06f, 0.02f, 5, false, true);
        }
    }
}
