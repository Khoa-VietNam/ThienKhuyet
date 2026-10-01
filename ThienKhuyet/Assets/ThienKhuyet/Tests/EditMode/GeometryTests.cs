using NUnit.Framework;
using ThienKhuyet.Gfx;
using UnityEngine;

namespace ThienKhuyet.Tests
{
    /// <summary>Winding checks for every procedural primitive (no renderer needed): a bad winding would make faces invisible.</summary>
    public class GeometryTests
    {
        static void AssertOutward(MeshBuilder mb, string what)
        {
            int bad = mb.WindingErrors(out int count);
            Assert.Greater(count, 0, what + " produced no triangles");
            Assert.AreEqual(0, bad, what + ": " + bad + " of " + count + " triangles are inside-out");
        }

        [Test] public void Box() { var m = new MeshBuilder(); m.Box(Vector3.zero, new Vector3(2, 3, 4)); AssertOutward(m, "box"); }
        [Test] public void Plane() { var m = new MeshBuilder(); m.Plane(new Vector2(3, 2)); AssertOutward(m, "plane"); }
        [Test] public void Cylinder() { var m = new MeshBuilder(); m.Cylinder(Vector3.zero, 2f, 1f, 0.6f, 12); AssertOutward(m, "cylinder"); }
        [Test] public void Cone() { var m = new MeshBuilder(); m.Cone(Vector3.zero, 2f, 1f, 12); AssertOutward(m, "cone"); }
        [Test] public void Sphere() { var m = new MeshBuilder(); m.Sphere(Vector3.zero, 1f, 12, 8); AssertOutward(m, "sphere"); }
        [Test] public void Ellipsoid() { var m = new MeshBuilder(); m.Ellipsoid(Vector3.zero, new Vector3(1, 2, 0.5f), 12, 8); AssertOutward(m, "ellipsoid"); }
        [Test] public void Torus() { var m = new MeshBuilder(); m.Torus(Vector3.zero, 2f, 0.3f, 16, 8); AssertOutward(m, "torus"); }
        [Test] public void DiscUp() { var m = new MeshBuilder(); m.Disc(Vector3.zero, 1f, 16, true); AssertOutward(m, "disc up"); }
        [Test] public void DiscDown() { var m = new MeshBuilder(); m.Disc(Vector3.zero, 1f, 16, false); AssertOutward(m, "disc down"); }

        [Test]
        public void Lathe()
        {
            var m = new MeshBuilder();
            m.Lathe(Vector3.zero, new[] { new Vector2(0.4f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0.3f, 1.2f), new Vector2(0.2f, 1.5f) }, 12);
            AssertOutward(m, "lathe");
        }

        [Test] public void GableRoof() { var m = new MeshBuilder(); m.GableRoof(Vector3.zero, 4f, 5f, 1.8f, 0.3f); AssertOutward(m, "gable roof"); }
        [Test] public void CurvedRoof() { var m = new MeshBuilder(); m.CurvedRoof(Vector3.zero, 6f, 6f, 1.6f, 0.6f); AssertOutward(m, "curved roof"); }

        [Test]
        public void HeightGrid()
        {
            var m = new MeshBuilder();
            m.HeightGrid(Vector3.zero, new Vector2(10, 10), 8, 8, (x, z) => Mathf.Sin(x * 0.3f) * 0.5f + z * 0.05f);
            AssertOutward(m, "height grid");
        }

        [Test]
        public void Ribbon()
        {
            var m = new MeshBuilder();
            m.Ribbon(new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 3), new Vector3(3, 0, 6), new Vector3(6, 0, 7) }, 2f);
            AssertOutward(m, "ribbon");
        }
    }
}
