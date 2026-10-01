using System.Collections.Generic;
using ThienKhuyet.Gfx;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThienKhuyet.World
{
    /// <summary>Lake plane and river ribbon with animated flowing normal maps.</summary>
    public sealed class WaterSystem : MonoBehaviour
    {
        Material lake, river;
        Vector2 lakeOffset, riverOffset;

        public static WaterSystem Build(WorldGen gen, WorldAssets assets, Transform parent)
        {
            var go = new GameObject("Water");
            go.transform.SetParent(parent, false);
            var ws = go.AddComponent<WaterSystem>();
            ws.lake = assets.matLake;
            ws.river = assets.matRiver;

            // lake: a plane over the lake's bounding box (terrain hides everything outside the basin)
            Vector2 c = WorldLayout.LakeCenter, r = WorldLayout.LakeRadius * 1.3f;
            var mb = new MeshBuilder();
            mb.HeightGrid(new Vector3(c.x - r.x, WorldLayout.LakeLevel - 0.05f, c.y - r.y), new Vector2(r.x * 2f, r.y * 2f), 24, 24, (x, z) => 0f, 1f);
            var lakeGo = new GameObject("Lake");
            lakeGo.transform.SetParent(go.transform, false);
            lakeGo.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("lake", true);
            var lr = lakeGo.AddComponent<MeshRenderer>();
            lr.sharedMaterial = assets.matLake;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lakeGo.layer = 4; // Water layer

            // river ribbon along the same polyline the terrain was carved with
            var pts = new List<Vector3>();
            Vector3[] nodes = WorldLayout.River;
            for (int i = 0; i < nodes.Length - 1; i++)
            {
                for (int s = 0; s < 8; s++)
                {
                    float t = s / 8f;
                    pts.Add(new Vector3(Mathf.Lerp(nodes[i].x, nodes[i + 1].x, t), Mathf.Lerp(nodes[i].z, nodes[i + 1].z, t) - 0.05f, Mathf.Lerp(nodes[i].y, nodes[i + 1].y, t)));
                }
            }
            pts.Add(new Vector3(nodes[nodes.Length - 1].x, nodes[nodes.Length - 1].z - 0.05f, nodes[nodes.Length - 1].y));
            var rb = new MeshBuilder();
            rb.Ribbon(pts, (WorldLayout.RiverHalfWidth + 1.1f) * 2f, 1f);
            var riverGo = new GameObject("River");
            riverGo.transform.SetParent(go.transform, false);
            riverGo.AddComponent<MeshFilter>().sharedMesh = rb.ToMesh("river", true);
            var rr = riverGo.AddComponent<MeshRenderer>();
            rr.sharedMaterial = assets.matRiver;
            rr.shadowCastingMode = ShadowCastingMode.Off;
            riverGo.layer = 4;
            return ws;
        }

        void Update()
        {
            float t = Time.time;
            lakeOffset = new Vector2(t * 0.011f, t * 0.007f);
            riverOffset = new Vector2(0f, -t * 0.32f);
            if (lake != null) lake.SetTextureOffset("_BumpMap", lakeOffset);
            if (river != null) river.SetTextureOffset("_BumpMap", riverOffset);
        }
    }
}
