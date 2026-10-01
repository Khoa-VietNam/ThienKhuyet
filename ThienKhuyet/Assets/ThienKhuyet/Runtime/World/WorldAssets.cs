using System.Collections;
using System.Collections.Generic;
using ThienKhuyet.Gfx;
using UnityEngine;

namespace ThienKhuyet.World
{
    /// <summary>All procedurally generated textures and materials used by the world (terrain layers, buildings, vegetation, water).</summary>
    public sealed class WorldAssets
    {
        public TexSet grass, dirt, rock, sand, snow, leafFloor;
        public TexSet wall, brick, plank, plaster, roofDark, roofRed, bark, marble, cobble, cloth, metal;
        public TexSet foliageGreen, foliageDark, foliagePink, foliageGold, bambooTex;
        public Texture2D waterNormal;
        public TerrainLayer[] terrainLayers;

        public Material matWall, matBrick, matPlank, matPlaster, matRoofDark, matRoofRed, matBark, matMarble, matCobble, matCloth, matMetal, matDarkWood, matThatch;
        public Material[] matLeaf = new Material[3];
        public Material matPine, matPinkLeaf, matGoldLeaf, matBamboo, matBambooLeaf, matBush, matDeadWood, matRock, matReed;
        public Material matLake, matRiver;
        public Material matGlowCyan, matGlowGold, matGlowRed, matLantern, matFire;
        public Material matStonePath;

        public float progress;
        public bool ready;

        public IEnumerator Build(System.Action<float, string> report)
        {
            int total = 22, done = 0;
            void Step(string name)
            {
                done++;
                progress = (float)done / total;
                report?.Invoke(progress, name);
            }

            grass = ProcTex.Grass(256, new Color(0.17f, 0.36f, 0.14f), new Color(0.36f, 0.55f, 0.2f), 11); Step("grass"); yield return null;
            dirt = ProcTex.Ground(256, new Color(0.38f, 0.28f, 0.19f), new Color(0.52f, 0.41f, 0.28f), 12, 0.5f); Step("dirt"); yield return null;
            rock = ProcTex.Stone(256, new Color(0.46f, 0.45f, 0.43f), 13); Step("rock"); yield return null;
            sand = ProcTex.Ground(256, new Color(0.66f, 0.6f, 0.46f), new Color(0.78f, 0.72f, 0.56f), 14, 0.2f); Step("sand"); yield return null;
            snow = ProcTex.Snow(256, 15); Step("snow"); yield return null;
            leafFloor = ProcTex.Ground(256, new Color(0.17f, 0.2f, 0.1f), new Color(0.33f, 0.27f, 0.13f), 16, 0.3f); Step("leaves"); yield return null;

            wall = ProcTex.Stone(256, new Color(0.62f, 0.6f, 0.55f), 21, 0.3f); Step("wall"); yield return null;
            brick = ProcTex.Bricks(256, new Color(0.5f, 0.38f, 0.3f), new Color(0.3f, 0.28f, 0.26f), 22); Step("brick"); yield return null;
            plank = ProcTex.Planks(256, new Color(0.46f, 0.31f, 0.18f), 23); Step("plank"); yield return null;
            plaster = ProcTex.Plaster(256, new Color(0.88f, 0.85f, 0.78f), 24); Step("plaster"); yield return null;
            roofDark = ProcTex.RoofTiles(256, new Color(0.22f, 0.26f, 0.3f), 25); Step("roof"); yield return null;
            roofRed = ProcTex.RoofTiles(256, new Color(0.55f, 0.24f, 0.18f), 26); Step("roof2"); yield return null;
            bark = ProcTex.Bark(256, new Color(0.3f, 0.22f, 0.15f), 27); Step("bark"); yield return null;
            marble = ProcTex.Marble(256, new Color(0.88f, 0.9f, 0.88f), new Color(0.45f, 0.62f, 0.55f), 28); Step("marble"); yield return null;
            cobble = ProcTex.Cobble(256, new Color(0.56f, 0.54f, 0.5f), 29); Step("cobble"); yield return null;
            cloth = ProcTex.Cloth(128, new Color(0.7f, 0.2f, 0.18f), 30); Step("cloth"); yield return null;
            metal = ProcTex.Metal(128, new Color(0.55f, 0.57f, 0.6f), 31); Step("metal"); yield return null;
            foliageGreen = ProcTex.Foliage(256, new Color(0.1f, 0.24f, 0.09f), new Color(0.3f, 0.5f, 0.16f), 32); Step("foliage"); yield return null;
            foliageDark = ProcTex.Foliage(256, new Color(0.05f, 0.15f, 0.08f), new Color(0.14f, 0.32f, 0.16f), 33); Step("pine"); yield return null;
            foliagePink = ProcTex.Foliage(256, new Color(0.75f, 0.4f, 0.5f), new Color(0.98f, 0.78f, 0.82f), 34); Step("blossom"); yield return null;
            foliageGold = ProcTex.Foliage(128, new Color(0.62f, 0.45f, 0.1f), new Color(0.95f, 0.8f, 0.3f), 35);
            bambooTex = ProcTex.Bark(128, new Color(0.42f, 0.58f, 0.22f), 36);
            waterNormal = ProcTex.WaterNormal(128, 37); Step("water"); yield return null;

            BuildLayers();
            BuildMaterials();
            ready = true;
        }

        void BuildLayers()
        {
            terrainLayers = new[]
            {
                Layer(grass, 7f, 0.05f), Layer(dirt, 6f, 0.04f), Layer(rock, 9f, 0.1f), Layer(sand, 6f, 0.08f), Layer(snow, 8f, 0.25f), Layer(leafFloor, 6f, 0.04f)
            };
        }

        static TerrainLayer Layer(TexSet t, float tile, float smooth)
        {
            var l = new TerrainLayer
            {
                diffuseTexture = t.albedo,
                normalMapTexture = t.normal,
                tileSize = new Vector2(tile, tile),
                smoothness = smooth,
                metallic = 0f,
                normalScale = 1f
            };
            return l;
        }

        void BuildMaterials()
        {
            matWall = Mats.Lit(Color.white, 0.12f, 0f, wall, new Vector2(1f, 1f));
            matBrick = Mats.Lit(Color.white, 0.1f, 0f, brick);
            matPlank = Mats.Lit(Color.white, 0.12f, 0f, plank);
            matDarkWood = Mats.Lit(new Color(0.55f, 0.45f, 0.4f), 0.15f, 0f, plank);
            matThatch = Mats.Lit(new Color(0.85f, 0.75f, 0.45f), 0.05f, 0f, plaster);
            matPlaster = Mats.Lit(Color.white, 0.08f, 0f, plaster);
            matRoofDark = Mats.Lit(Color.white, 0.25f, 0f, roofDark);
            matRoofRed = Mats.Lit(Color.white, 0.22f, 0f, roofRed);
            matMarble = Mats.Lit(Color.white, 0.5f, 0f, marble);
            matCobble = Mats.Lit(Color.white, 0.1f, 0f, cobble);
            matStonePath = matCobble;
            matCloth = Mats.Lit(Color.white, 0.05f, 0f, cloth);
            matMetal = Mats.Lit(Color.white, 0.7f, 0.8f, metal);
            matBark = Mats.Lit(Color.white, 0.05f, 0f, bark);
            matDeadWood = Mats.Lit(new Color(0.65f, 0.6f, 0.55f), 0.04f, 0f, bark);
            matRock = Mats.Lit(Color.white, 0.08f, 0f, rock);
            matLeaf[0] = Mats.Lit(new Color(0.92f, 1f, 0.85f), 0.1f, 0f, foliageGreen, new Vector2(1f, 1f));
            matLeaf[1] = Mats.Lit(new Color(1f, 1f, 0.78f), 0.1f, 0f, foliageGreen, new Vector2(1f, 1f));
            matLeaf[2] = Mats.Lit(new Color(0.78f, 0.95f, 0.85f), 0.1f, 0f, foliageGreen, new Vector2(1f, 1f));
            matPine = Mats.Lit(Color.white, 0.08f, 0f, foliageDark);
            matPinkLeaf = Mats.Lit(Color.white, 0.1f, 0f, foliagePink);
            matGoldLeaf = Mats.Lit(Color.white, 0.1f, 0f, foliageGold);
            matBamboo = Mats.Lit(new Color(0.95f, 1f, 0.8f), 0.3f, 0f, bambooTex);
            matBambooLeaf = Mats.Lit(new Color(0.78f, 1f, 0.6f), 0.1f, 0f, foliageGreen);
            matBush = Mats.Lit(new Color(0.8f, 1f, 0.75f), 0.08f, 0f, foliageGreen);
            matReed = Mats.Lit(new Color(0.75f, 0.85f, 0.45f), 0.1f, 0f, foliageGreen);

            matLake = Mats.LitAlpha(new Color(0.07f, 0.3f, 0.38f, 0.82f), 0.95f, new TexSet { albedo = Texture2D.whiteTexture, normal = waterNormal }, true);
            matLake.SetTextureScale("_BumpMap", new Vector2(0.2f, 0.2f));
            matLake.SetFloat("_BumpScale", 0.6f);
            matRiver = Mats.LitAlpha(new Color(0.09f, 0.34f, 0.4f, 0.8f), 0.95f, new TexSet { albedo = Texture2D.whiteTexture, normal = waterNormal }, true);
            matRiver.SetTextureScale("_BumpMap", new Vector2(0.17f, 0.17f));
            matRiver.SetFloat("_BumpScale", 0.8f);

            matGlowCyan = Mats.Glow(new Color(0.45f, 0.85f, 1f), 2.5f);
            matGlowGold = Mats.Glow(new Color(1f, 0.8f, 0.4f), 2f);
            matGlowRed = Mats.Glow(new Color(1f, 0.25f, 0.2f), 2.5f);
            matLantern = Mats.Glow(new Color(1f, 0.7f, 0.35f), 2.2f);
            matFire = Mats.Glow(new Color(1f, 0.5f, 0.15f), 3f);
        }
    }
}
