using System;
using System.Collections.Generic;
using ThienKhuyet.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThienKhuyet.Gfx
{
    /// <summary>
    /// Code-driven geometry (no imported assets). Shapes are appended with the current transform; UVs are in world units
    /// divided by a tile size so texture density stays consistent. One index list per submesh (= one material each).
    /// </summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> verts = new List<Vector3>(256);
        readonly List<Vector3> normals = new List<Vector3>(256);
        readonly List<Vector2> uvs = new List<Vector2>(256);
        readonly List<Color32> colors = new List<Color32>(256);
        readonly List<List<int>> subs = new List<List<int>> { new List<int>(512) };
        readonly Stack<Matrix4x4> stack = new Stack<Matrix4x4>();
        readonly List<BoneWeight> weights = new List<BoneWeight>();
        readonly bool skinned;
        Matrix4x4 xf = Matrix4x4.identity;
        Matrix4x4 nxf = Matrix4x4.identity;
        int sub;
        int bone;
        Vector2? fixedUv;
        Color32 tint = new Color32(255, 255, 255, 255);

        public MeshBuilder(bool skinned = false)
        {
            this.skinned = skinned;
        }

        public int VertexCount => verts.Count;
        public int SubmeshCount => subs.Count;

        public MeshBuilder Sub(int index)
        {
            while (subs.Count <= index) subs.Add(new List<int>(256));
            sub = index;
            return this;
        }

        /// <summary>Rigidly binds following vertices to this bone (skinned meshes only).</summary>
        public MeshBuilder Bone(int index)
        {
            bone = index;
            return this;
        }

        /// <summary>When set, every following vertex uses this UV (palette atlas lookup) instead of generated tiling UVs.</summary>
        public MeshBuilder PaletteUv(Vector2? uv)
        {
            fixedUv = uv;
            return this;
        }

        /// <summary>Vertex color applied to following geometry (read by materials that use vertex color, ignored otherwise).</summary>
        public MeshBuilder Tint(Color c)
        {
            tint = c;
            return this;
        }

        // ------------------------------------------------------------------ transform stack
        public void Push(Vector3 pos, Quaternion rot, Vector3 scale)
        {
            stack.Push(xf);
            xf = xf * Matrix4x4.TRS(pos, rot, scale);
            nxf = xf.inverse.transpose;
        }

        public void Push(Vector3 pos, Vector3 eulerDeg)
        {
            Push(pos, Quaternion.Euler(eulerDeg), Vector3.one);
        }

        public void Push(Vector3 pos)
        {
            Push(pos, Quaternion.identity, Vector3.one);
        }

        public void Pop()
        {
            xf = stack.Pop();
            nxf = xf.inverse.transpose;
        }

        // ------------------------------------------------------------------ primitives
        int V(Vector3 p, Vector3 n, Vector2 uv)
        {
            verts.Add(xf.MultiplyPoint3x4(p));
            Vector3 nn = nxf.MultiplyVector(n);
            float m = nn.sqrMagnitude;
            normals.Add(m > 1e-10f ? nn / Mathf.Sqrt(m) : Vector3.up);
            uvs.Add(fixedUv ?? uv);
            colors.Add(tint);
            if (skinned) weights.Add(new BoneWeight { boneIndex0 = bone, weight0 = 1f });
            return verts.Count - 1;
        }

        void Tri(int a, int b, int c)
        {
            List<int> t = subs[sub];
            t.Add(a); t.Add(b); t.Add(c);
        }

        /// <summary>Quad with corners p0 (bottom-left), p1 (top-left), p2 (top-right), p3 (bottom-right) seen from the front.</summary>
        public void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 n, Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3)
        {
            int a = V(p0, n, uv0), b = V(p1, n, uv1), c = V(p2, n, uv2), d = V(p3, n, uv3);
            Tri(a, b, c);
            Tri(a, c, d);
        }

        /// <summary>Flat quad facing +Y at the origin of the current transform (size x by z).</summary>
        public void Plane(Vector2 size, float uvTile = 1f)
        {
            float hx = size.x * 0.5f, hz = size.y * 0.5f;
            Quad(new Vector3(-hx, 0, -hz), new Vector3(-hx, 0, hz), new Vector3(hx, 0, hz), new Vector3(hx, 0, -hz), Vector3.up,
                Vector2.zero, new Vector2(0, size.y / uvTile), new Vector2(size.x / uvTile, size.y / uvTile), new Vector2(size.x / uvTile, 0));
        }

        public void Box(Vector3 center, Vector3 size, float uvTile = 1f)
        {
            Vector3 h = size * 0.5f;
            Face(center + new Vector3(h.x, 0, 0), Vector3.right, Vector3.up, size.z, size.y, uvTile);
            Face(center - new Vector3(h.x, 0, 0), Vector3.left, Vector3.up, size.z, size.y, uvTile);
            Face(center + new Vector3(0, h.y, 0), Vector3.up, Vector3.forward, size.x, size.z, uvTile);
            Face(center - new Vector3(0, h.y, 0), Vector3.down, Vector3.forward, size.x, size.z, uvTile);
            Face(center + new Vector3(0, 0, h.z), Vector3.forward, Vector3.up, size.x, size.y, uvTile);
            Face(center - new Vector3(0, 0, h.z), Vector3.back, Vector3.up, size.x, size.y, uvTile);
        }

        void Face(Vector3 c, Vector3 n, Vector3 u, float w, float h, float tile)
        {
            Vector3 r = Vector3.Cross(n, u);
            Vector3 rw = r * (w * 0.5f), uh = u * (h * 0.5f);
            Quad(c - rw - uh, c - rw + uh, c + rw + uh, c + rw - uh, n,
                Vector2.zero, new Vector2(0, h / tile), new Vector2(w / tile, h / tile), new Vector2(w / tile, 0));
        }

        /// <summary>Box whose bottom face sits at y = baseY (convenience for walls and props standing on the ground).</summary>
        public void BoxOnGround(Vector3 baseCenter, Vector3 size, float uvTile = 1f)
        {
            Box(baseCenter + new Vector3(0, size.y * 0.5f, 0), size, uvTile);
        }

        public void Cylinder(Vector3 baseCenter, float height, float rBottom, float rTop, int seg = 12, bool capBottom = true, bool capTop = true, float uvTile = 1f)
        {
            float slope = height > 1e-5f ? (rBottom - rTop) / height : 0f;
            int first = verts.Count;
            for (int ring = 0; ring < 2; ring++)
            {
                float r = ring == 0 ? rBottom : rTop;
                float y = ring == 0 ? 0f : height;
                for (int i = 0; i <= seg; i++)
                {
                    float a = (float)i / seg * Mathf.PI * 2f;
                    float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                    Vector3 n = new Vector3(cs, slope, sn);
                    float circ = Mathf.Max(rBottom, rTop) * Mathf.PI * 2f;
                    V(baseCenter + new Vector3(cs * r, y, sn * r), n, new Vector2((float)i / seg * circ / uvTile, y / uvTile));
                }
            }
            int row = seg + 1;
            for (int i = 0; i < seg; i++)
            {
                int b0 = first + i, b1 = first + i + 1, t0 = first + row + i, t1 = first + row + i + 1;
                Tri(b0, t0, t1);
                Tri(b0, t1, b1);
            }
            if (capTop && rTop > 1e-4f) Cap(baseCenter + new Vector3(0, height, 0), rTop, seg, true, uvTile);
            if (capBottom && rBottom > 1e-4f) Cap(baseCenter, rBottom, seg, false, uvTile);
        }

        void Cap(Vector3 center, float r, int seg, bool top, float uvTile)
        {
            Vector3 n = top ? Vector3.up : Vector3.down;
            int c = V(center, n, new Vector2(0.5f * r / uvTile, 0.5f * r / uvTile));
            int first = verts.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = (float)i / seg * Mathf.PI * 2f;
                float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                V(center + new Vector3(cs * r, 0, sn * r), n, new Vector2((0.5f + cs * 0.5f) * r / uvTile, (0.5f + sn * 0.5f) * r / uvTile));
            }
            for (int i = 0; i < seg; i++)
            {
                if (top) Tri(c, first + i + 1, first + i);
                else Tri(c, first + i, first + i + 1);
            }
        }

        public void Cone(Vector3 baseCenter, float height, float radius, int seg = 12, bool cap = true, float uvTile = 1f)
        {
            Cylinder(baseCenter, height, radius, 0f, seg, cap, false, uvTile);
        }

        public void Sphere(Vector3 center, float radius, int seg = 12, int rings = 8, float uvTile = 1f)
        {
            Ellipsoid(center, new Vector3(radius, radius, radius), seg, rings, uvTile);
        }

        public void Ellipsoid(Vector3 center, Vector3 radii, int seg = 12, int rings = 8, float uvTile = 1f)
        {
            int first = verts.Count;
            for (int j = 0; j <= rings; j++)
            {
                float th = (float)j / rings * Mathf.PI;
                float y = Mathf.Cos(th), s = Mathf.Sin(th);
                for (int i = 0; i <= seg; i++)
                {
                    float ph = (float)i / seg * Mathf.PI * 2f;
                    Vector3 d = new Vector3(s * Mathf.Cos(ph), y, s * Mathf.Sin(ph));
                    Vector3 p = new Vector3(d.x * radii.x, d.y * radii.y, d.z * radii.z);
                    Vector3 n = new Vector3(d.x / radii.x, d.y / radii.y, d.z / radii.z);
                    float circ = Mathf.Max(radii.x, radii.z) * Mathf.PI * 2f;
                    V(center + p, n, new Vector2((float)i / seg * circ / uvTile, (1f - (float)j / rings) * radii.y * Mathf.PI / uvTile));
                }
            }
            int row = seg + 1;
            for (int j = 0; j < rings; j++)
            {
                for (int i = 0; i < seg; i++)
                {
                    int up0 = first + j * row + i, up1 = up0 + 1;
                    int lo0 = up0 + row, lo1 = lo0 + 1;
                    Tri(lo0, up0, up1);
                    Tri(lo0, up1, lo1);
                }
            }
        }

        /// <summary>Tapered limb between two points with rounded ends.</summary>
        public void Limb(Vector3 from, Vector3 to, float r0, float r1, int seg = 8, bool roundStart = true, bool roundEnd = true)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            Quaternion q = Quaternion.FromToRotation(Vector3.up, d / len);
            Push(from, q, Vector3.one);
            Cylinder(Vector3.zero, len, r0, r1, seg, false, false);
            if (roundStart) Sphere(Vector3.zero, r0, seg, Mathf.Max(3, seg / 2));
            if (roundEnd) Sphere(new Vector3(0, len, 0), r1, seg, Mathf.Max(3, seg / 2));
            Pop();
        }

        public void Disc(Vector3 center, float radius, int seg = 24, bool up = true, float uvTile = 1f)
        {
            Cap(center, radius, seg, up, uvTile);
        }

        public void Torus(Vector3 center, float majorR, float minorR, int seg = 24, int tube = 8)
        {
            int first = verts.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = (float)i / seg * Mathf.PI * 2f;
                Vector3 c = new Vector3(Mathf.Cos(a) * majorR, 0, Mathf.Sin(a) * majorR);
                Vector3 radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                for (int j = 0; j <= tube; j++)
                {
                    float b = (float)j / tube * Mathf.PI * 2f;
                    Vector3 n = radial * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    V(center + c + n * minorR, n, new Vector2((float)i / seg * 4f, (float)j / tube));
                }
            }
            int row = tube + 1;
            for (int i = 0; i < seg; i++)
            {
                for (int j = 0; j < tube; j++)
                {
                    int a0 = first + i * row + j, a1 = a0 + 1, b0 = a0 + row, b1 = b0 + 1;
                    Tri(a0, b1, b0);
                    Tri(a0, a1, b1);
                }
            }
        }

        /// <summary>Surface of revolution around +Y: profile points are (radius, height).</summary>
        public void Lathe(Vector3 baseCenter, IList<Vector2> profile, int seg = 16, float uvTile = 1f)
        {
            int n = profile.Count;
            int first = verts.Count;
            float vAcc = 0f;
            for (int k = 0; k < n; k++)
            {
                Vector2 p = profile[k];
                Vector2 t = k < n - 1 ? profile[k + 1] - p : p - profile[k - 1];
                if (k > 0 && k < n - 1) t = profile[k + 1] - profile[k - 1];
                Vector2 nrm = new Vector2(t.y, -t.x);
                if (nrm.sqrMagnitude < 1e-8f) nrm = Vector2.right;
                nrm.Normalize();
                if (k > 0) vAcc += (p - profile[k - 1]).magnitude;
                for (int i = 0; i <= seg; i++)
                {
                    float a = (float)i / seg * Mathf.PI * 2f;
                    float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                    V(baseCenter + new Vector3(cs * p.x, p.y, sn * p.x), new Vector3(cs * nrm.x, nrm.y, sn * nrm.x),
                        new Vector2((float)i / seg * Mathf.Max(0.01f, p.x) * Mathf.PI * 2f / uvTile, vAcc / uvTile));
                }
            }
            int row = seg + 1;
            for (int k = 0; k < n - 1; k++)
            {
                for (int i = 0; i < seg; i++)
                {
                    int b0 = first + k * row + i, b1 = b0 + 1, t0 = b0 + row, t1 = t0 + 1;
                    Tri(b0, t0, t1);
                    Tri(b0, t1, b1);
                }
            }
        }

        /// <summary>Gable roof prism (ridge along Z) with optional overhang. Base rectangle is centered on 'baseCenter'.</summary>
        public void GableRoof(Vector3 baseCenter, float width, float depth, float height, float overhang = 0.3f, float uvTile = 1f)
        {
            float hw = width * 0.5f + overhang, hd = depth * 0.5f + overhang;
            Vector3 r0 = baseCenter + new Vector3(0, height, -hd), r1 = baseCenter + new Vector3(0, height, hd);
            Vector3 lf = baseCenter + new Vector3(-hw, 0, hd), lb = baseCenter + new Vector3(-hw, 0, -hd);
            Vector3 rf = baseCenter + new Vector3(hw, 0, hd), rb = baseCenter + new Vector3(hw, 0, -hd);
            Vector3 nl = new Vector3(-height, hw, 0f).normalized;   // outward normal of the left slope
            Vector3 nr = new Vector3(height, hw, 0f).normalized;    // outward normal of the right slope
            float slopeLen = Mathf.Sqrt(hw * hw + height * height);
            float du = (depth + overhang * 2f) / uvTile, dv = slopeLen / uvTile;
            // top surfaces (front face = outside)
            Quad(lb, lf, r1, r0, nl, new Vector2(0, 0), new Vector2(du, 0), new Vector2(du, dv), new Vector2(0, dv));
            Quad(rf, rb, r0, r1, nr, new Vector2(0, 0), new Vector2(du, 0), new Vector2(du, dv), new Vector2(0, dv));
            // undersides so the roof is visible from inside / below
            Quad(lb, r0, r1, lf, -nl, new Vector2(0, 0), new Vector2(0, dv), new Vector2(du, dv), new Vector2(du, 0));
            Quad(rb, rf, r1, r0, -nr, new Vector2(0, 0), new Vector2(du, 0), new Vector2(du, dv), new Vector2(0, dv));
            // gable end triangles (clockwise as seen from outside)
            float k = Mathf.Clamp01(1f - overhang / Mathf.Max(0.01f, hw));
            Vector3 fa = baseCenter + new Vector3(-hw + overhang, 0, depth * 0.5f), fb = baseCenter + new Vector3(hw - overhang, 0, depth * 0.5f);
            Vector3 ft = baseCenter + new Vector3(0, height * k, depth * 0.5f);
            int a = V(fb, Vector3.forward, Vector2.zero), b = V(ft, Vector3.forward, new Vector2(width * 0.5f / uvTile, height / uvTile)), c = V(fa, Vector3.forward, new Vector2(width / uvTile, 0));
            Tri(a, b, c);
            Vector3 ba = baseCenter + new Vector3(-hw + overhang, 0, -depth * 0.5f), bb = baseCenter + new Vector3(hw - overhang, 0, -depth * 0.5f);
            Vector3 bt = baseCenter + new Vector3(0, height * k, -depth * 0.5f);
            int d = V(ba, Vector3.back, Vector2.zero), e = V(bt, Vector3.back, new Vector2(width * 0.5f / uvTile, height / uvTile)), f = V(bb, Vector3.back, new Vector2(width / uvTile, 0));
            Tri(d, e, f);
        }

        /// <summary>Chinese-style roof: concave slopes with upturned eaves, as a smooth grid surface (double sided).</summary>
        public void CurvedRoof(Vector3 baseCenter, float width, float depth, float ridgeHeight, float upturn = 0.5f, int nx = 14, int nz = 6, float uvTile = 2f)
        {
            float hw = width * 0.5f, hd = depth * 0.5f;
            int first = verts.Count;
            for (int k = 0; k <= 1; k++)
            {
                for (int j = 0; j <= nz; j++)
                {
                    float v = (float)j / nz * 2f - 1f;
                    for (int i = 0; i <= nx; i++)
                    {
                        float u = (float)i / nx * 2f - 1f;
                        float au = Mathf.Abs(u), av = Mathf.Abs(v);
                        float y = ridgeHeight * Mathf.Pow(1f - au, 1.7f) + upturn * Mathf.Pow(au, 5f) + upturn * 0.8f * Mathf.Pow(au, 3f) * Mathf.Pow(av, 4f);
                        float zFlare = 1f + 0.06f * Mathf.Pow(au, 3f);
                        Vector3 p = new Vector3(u * hw, y - (k == 1 ? 0.12f : 0f), v * hd * zFlare);
                        float du = (ridgeHeight * 1.7f * -Mathf.Pow(1f - au, 0.7f) * Mathf.Sign(u) + upturn * 5f * Mathf.Pow(au, 4f) * Mathf.Sign(u)) / hw;
                        Vector3 n = new Vector3(-du, 1f, 0f);
                        if (k == 1) n = -n;
                        V(p, n, new Vector2(u * hw / uvTile + 0.5f, v * hd / uvTile));
                    }
                }
            }
            int row = nx + 1;
            int layer = (nz + 1) * row;
            for (int k = 0; k <= 1; k++)
            {
                for (int j = 0; j < nz; j++)
                {
                    for (int i = 0; i < nx; i++)
                    {
                        int a = first + k * layer + j * row + i, b = a + 1, c = a + row, d = c + 1;
                        if (k == 0) { Tri(a, c, d); Tri(a, d, b); }
                        else { Tri(a, d, c); Tri(a, b, d); }
                    }
                }
            }
        }

        /// <summary>Grid surface from a height function over [0,size]; used for ground patches, banks, paths.</summary>
        public void HeightGrid(Vector3 origin, Vector2 size, int nx, int nz, Func<float, float, float> height, float uvTile = 2f)
        {
            int first = verts.Count;
            float eps = Mathf.Max(size.x, size.y) / Mathf.Max(nx, nz) * 0.5f;
            for (int j = 0; j <= nz; j++)
            {
                for (int i = 0; i <= nx; i++)
                {
                    float x = (float)i / nx * size.x, z = (float)j / nz * size.y;
                    float h = height(x, z);
                    float hx = height(x + eps, z) - height(x - eps, z);
                    float hz = height(x, z + eps) - height(x, z - eps);
                    Vector3 n = new Vector3(-hx / (2f * eps), 1f, -hz / (2f * eps));
                    V(origin + new Vector3(x, h, z), n, new Vector2(x / uvTile, z / uvTile));
                }
            }
            int row = nx + 1;
            for (int j = 0; j < nz; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    int a = first + j * row + i, b = a + 1, c = a + row, d = c + 1;
                    Tri(a, c, d);
                    Tri(a, d, b);
                }
            }
        }

        /// <summary>Ribbon along a polyline (roads, rivers): width per point, y follows points.</summary>
        public void Ribbon(IList<Vector3> pts, float width, float uvTile = 4f, float uvV0 = 0f)
        {
            if (pts.Count < 2) return;
            int first = verts.Count;
            float acc = uvV0;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 dir = i < pts.Count - 1 ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1];
                if (i > 0 && i < pts.Count - 1) dir = pts[i + 1] - pts[i - 1];
                dir.y = 0f;
                dir = dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector3.forward;
                Vector3 side = new Vector3(dir.z, 0, -dir.x) * (width * 0.5f);
                if (i > 0) acc += Vector3.Distance(pts[i], pts[i - 1]);
                V(pts[i] - side, Vector3.up, new Vector2(0, acc / uvTile));
                V(pts[i] + side, Vector3.up, new Vector2(width / uvTile, acc / uvTile));
            }
            for (int i = 0; i < pts.Count - 1; i++)
            {
                int a = first + i * 2, b = a + 1, c = a + 2, d = a + 3;
                Tri(a, c, d);
                Tri(a, d, b);
            }
        }

        // ------------------------------------------------------------------ deformation
        /// <summary>Displaces vertices added since 'fromVertex' along their normals with 3D-ish noise (organic shapes).</summary>
        public void Displace(int fromVertex, float amplitude, float frequency, int seed)
        {
            for (int i = fromVertex; i < verts.Count; i++)
            {
                Vector3 p = verts[i];
                float n = Noise.Perlin(p.x * frequency + p.y * 0.37f * frequency, p.z * frequency + p.y * frequency * 0.71f, seed);
                verts[i] = p + normals[i] * (n * amplitude);
            }
        }

        public void Jitter(int fromVertex, float amplitude, int seed)
        {
            for (int i = fromVertex; i < verts.Count; i++)
            {
                Vector3 p = verts[i];
                float a = Mathx.Hash01(Mathf.RoundToInt(p.x * 50f), Mathf.RoundToInt(p.z * 50f), seed);
                float b = Mathx.Hash01(Mathf.RoundToInt(p.y * 50f), Mathf.RoundToInt(p.x * 50f), seed + 7);
                float c = Mathx.Hash01(Mathf.RoundToInt(p.z * 50f), Mathf.RoundToInt(p.y * 50f), seed + 13);
                verts[i] = p + new Vector3(a - 0.5f, b - 0.5f, c - 0.5f) * amplitude;
            }
        }

        /// <summary>Scales vertices added since 'fromVertex' by a function of their height (tapering, flaring).</summary>
        public void Remap(int fromVertex, Func<Vector3, Vector3> f)
        {
            for (int i = fromVertex; i < verts.Count; i++) verts[i] = f(verts[i]);
        }

        // ------------------------------------------------------------------ output
        public Mesh ToMesh(string name, bool tangents = true)
        {
            var m = new Mesh { name = name };
            if (verts.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetNormals(normals);
            m.SetUVs(0, uvs);
            m.SetColors(colors);
            if (skinned) m.boneWeights = weights.ToArray();
            m.subMeshCount = subs.Count;
            for (int i = 0; i < subs.Count; i++) m.SetTriangles(subs[i], i, false);
            m.RecalculateBounds();
            if (tangents) m.RecalculateTangents();
            return m;
        }

        /// <summary>
        /// Counts triangles whose winding disagrees with their vertex normals (front face = Cross(b-a, c-a) in Unity).
        /// Used by unit tests to catch inside-out primitives without a renderer.
        /// </summary>
        public int WindingErrors(out int triangleCount)
        {
            int bad = 0;
            triangleCount = 0;
            for (int s = 0; s < subs.Count; s++)
            {
                List<int> t = subs[s];
                for (int i = 0; i + 2 < t.Count; i += 3)
                {
                    Vector3 a = verts[t[i]], b = verts[t[i + 1]], c = verts[t[i + 2]];
                    Vector3 geo = Vector3.Cross(b - a, c - a);
                    if (geo.sqrMagnitude < 1e-12f) continue; // degenerate (poles, collapsed rings)
                    triangleCount++;
                    Vector3 avg = normals[t[i]] + normals[t[i + 1]] + normals[t[i + 2]];
                    if (Vector3.Dot(geo, avg) < 0f) bad++;
                }
            }
            return bad;
        }

        public void Clear()
        {
            verts.Clear();
            normals.Clear();
            uvs.Clear();
            colors.Clear();
            weights.Clear();
            for (int i = 0; i < subs.Count; i++) subs[i].Clear();
            sub = 0;
            xf = Matrix4x4.identity;
            nxf = Matrix4x4.identity;
            stack.Clear();
        }
    }
}
