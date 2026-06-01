using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// ASTRA EDEN terrain builder.
///
/// Goal:
/// - Build one unified Eden-7 island map, not separate chapter maps.
/// - Match the current concept sketch direction:
///   SW beach/crash site + hub plateau, central primeval forest basin,
///   NE research-lab plateau, E/SE control ridge, NW final crater/core basin.
/// - Use Gaia User Data + All In One Heightmaps as detail sources, but keep the
///   macro layout deterministic and gameplay-readable.
///
/// Put this file in: Assets/Scripts/Editor/GaiaUserDataMapBuilder.cs
/// Then run: ASTRA EDEN > World > Build Eden-7 Concept Terrain Only
/// </summary>
public static class GaiaUserDataMapBuilder
{
    private const string GaiaStampRoot = "Assets/Packages/Gaia User Data/Stamps";
    private const string AllInOneRoot = "Assets/Packages/All In One - Heightmaps/Heightmaps";
    private const string ScenePath = "Assets/Scenes/New Scene.unity";
    private const string GeneratedRoot = "Assets/_Project/Generated/GaiaUserDataMap";
    private const string TerrainDataPath = GeneratedRoot + "/Terrain/Eden7_MainIsland_Terrain.asset";
    private const string TerrainLayerFolder = GeneratedRoot + "/TerrainLayers";
    private const string MaterialFolder = GeneratedRoot + "/Materials";
    private const string TerrainPhysicsMaterialFolder = "Assets/_Project/Materials/Physics";
    private const string TerrainPhysicsMaterialPath = TerrainPhysicsMaterialFolder + "/PM_Terrain_Default.physicMaterial";
    private const string LayoutReadmePath = GeneratedRoot + "/Eden7_MapLayout_README.txt";

    private const int HeightmapResolution = 1025;
    private const int AlphamapResolution = 1024;
    private const float TerrainSize = 1200f;
    private const float TerrainHeight = 260f;
    private const float WaterHeight = 10f;
    private const float NormalizedWaterHeight = WaterHeight / TerrainHeight;

    private struct ZoneAnchor
    {
        public readonly string Name;
        public readonly float X;
        public readonly float Z;
        public readonly string Role;

        public ZoneAnchor(string name, float x, float z, string role)
        {
            Name = name;
            X = x;
            Z = z;
            Role = role;
        }
    }

    private enum StampBlendMode
    {
        Add,
        Max,
        Subtract,
        Average,
        DetailOnly
    }

    private static readonly ZoneAnchor[] ZoneAnchors =
    {
        new ZoneAnchor("MK_Intro_BeachCrashSite", 0.24f, 0.13f, "Intro beach, crash debris, first Wild Claw Raptor encounter"),
        new ZoneAnchor("MK_CH1_BeaconCampPlateau", 0.29f, 0.28f, "Hub plateau for Beacon Camp, shop, upgrade, companion, quest/map"),
        new ZoneAnchor("MK_CH1_IronHornTrikeArena", 0.40f, 0.33f, "Mini-boss clearing near first relay"),
        new ZoneAnchor("MK_CH2_PrimevalForestSouth", 0.45f, 0.43f, "Forest entrance, first pack encounters"),
        new ZoneAnchor("MK_CH2_PrimevalForestNorth", 0.52f, 0.53f, "Deep forest, nest arena, Alpha Rex approach"),
        new ZoneAnchor("MK_CH2_AlphaRexClearing", 0.57f, 0.49f, "Large natural clearing for Alpha Rex Varkos"),
        new ZoneAnchor("MK_CH3_RuinedResearchLabPlateau", 0.78f, 0.59f, "Ruined Research Lab plateau, containment and archive buildings"),
        new ZoneAnchor("MK_CH4_ControlTowerRidge", 0.79f, 0.38f, "High ridge for control tower / relay installation"),
        new ZoneAnchor("MK_CH4_TitanAnkylorArena", 0.70f, 0.31f, "Heavy boss arena before core approach"),
        new ZoneAnchor("MK_CH5_EdenCoreCaldera", 0.34f, 0.79f, "Final crater/caldera, Eden Core Facility exterior"),
        new ZoneAnchor("MK_CH5_CoreAccessBridge", 0.48f, 0.70f, "Bridge/pass leading from ridge/forest to final core"),
        new ZoneAnchor("MK_Cave_WestRidgeShortcut", 0.55f, 0.66f, "Optional shortcut/cave entrance"),
        new ZoneAnchor("MK_Cave_EastLabServiceTunnel", 0.86f, 0.48f, "Optional lab service entrance")
    };

    [MenuItem("ASTRA EDEN/World/Build Eden-7 Main Island Terrain")]
    public static void BuildGaiaUserDataMapInNewScene()
    {
        BuildEden7ConceptScene(createGreyboxLandmarks: false);
    }

    [MenuItem("ASTRA EDEN/World/Build Eden-7 Concept Terrain Only")]
    public static void BuildEden7ConceptTerrainOnly()
    {
        BuildEden7ConceptScene(createGreyboxLandmarks: false);
    }

    [MenuItem("ASTRA EDEN/World/Build Eden-7 Concept Terrain + Landmark Placeholders")]
    public static void BuildEden7ConceptTerrainWithPlaceholders()
    {
        BuildEden7ConceptScene(createGreyboxLandmarks: true);
    }

    private static void BuildEden7ConceptScene(bool createGreyboxLandmarks)
    {
        EnsureProjectFolders();

        List<string> gaiaStampPaths = FindTextures(GaiaStampRoot, ".exr");
        List<string> allInOneHeightmaps = FindTextures(AllInOneRoot, ".png");

        if (gaiaStampPaths.Count == 0 && allInOneHeightmaps.Count == 0)
        {
            Debug.LogError("No terrain heightmap data found in Gaia User Data or All In One - Heightmaps.");
            return;
        }

        Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
        float[,] heights = CreateConceptIslandBase();

        // Use imported packages for secondary relief only. Macro layout is hand-sculpted below.
        ApplySourceHeightmapsAsDetail(heights, allInOneHeightmaps, textureCache);
        ApplyGaiaStampsAsDetail(heights, gaiaStampPaths, textureCache);

        SculptConceptMacroLayout(heights);
        SculptPlayableRoutes(heights);
        FlattenGameplayPads(heights);
        AddLocalizedTerrainDetail(heights);
        SmoothHeightmap(heights, 2, 0.42f);
        ReinforceConceptLandmarks(heights);
        ShapeFinalCoastline(heights);
        NormalizePlayableHeightRange(heights);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        TerrainData terrainData = CreateOrReplaceTerrainData(heights);
        Terrain terrain = Terrain.CreateTerrainGameObject(terrainData).GetComponent<Terrain>();
        terrain.name = "Eden-7 Main Island Terrain";
        terrain.transform.position = new Vector3(-TerrainSize * 0.5f, 0f, -TerrainSize * 0.5f);
        ConfigureTerrainComponent(terrain);
        AssignTerrainPhysicsMaterial(terrain);

        TerrainLayer[] terrainLayers = CreateTerrainLayers();
        terrainData.terrainLayers = terrainLayers;
        PaintTerrain(terrainData, terrainLayers.Length);

        CreateWater();
        CreateLighting();
        CreateCamera(terrain);
        CreateZoneMarkers(terrain);
        if (createGreyboxLandmarks)
        {
            CreateGreyboxLandmarks(terrain);
        }

        WriteLayoutReadme();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Built Eden-7 concept island in {ScenePath}. " +
            $"Gaia stamps: {gaiaStampPaths.Count}, All In One heightmaps: {allInOneHeightmaps.Count}, " +
            $"Landmark placeholders: {createGreyboxLandmarks}");
    }

    private static void EnsureProjectFolders()
    {
        EnsureFolder(GeneratedRoot);
        EnsureFolder(Path.GetDirectoryName(TerrainDataPath));
        EnsureFolder(TerrainLayerFolder);
        EnsureFolder(MaterialFolder);
        EnsureFolder(TerrainPhysicsMaterialFolder);
    }

    private static List<string> FindTextures(string root, string extension)
    {
        if (!AssetDatabase.IsValidFolder(root))
        {
            return new List<string>();
        }

        return AssetDatabase.FindAssets("t:Texture2D", new[] { root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(extension))
            .OrderBy(path => path)
            .ToList();
    }

    private static float[,] CreateConceptIslandBase()
    {
        float[,] heights = new float[HeightmapResolution, HeightmapResolution];

        for (int z = 0; z < HeightmapResolution; z++)
        {
            float v = z / (float)(HeightmapResolution - 1);

            for (int x = 0; x < HeightmapResolution; x++)
            {
                float u = x / (float)(HeightmapResolution - 1);
                float island = GetIslandMask(u, v);

                float broad = Mathf.PerlinNoise(u * 3.8f + 10.1f, v * 3.8f + 19.3f);
                float middle = Mathf.PerlinNoise(u * 9.0f + 17.4f, v * 9.0f + 3.8f);
                float fine = Mathf.PerlinNoise(u * 22.0f + 4.4f, v * 22.0f + 8.8f);

                float baseHeight = 0.010f;
                float landHeight = 0.058f + broad * 0.030f + middle * 0.012f + fine * 0.004f;
                heights[z, x] = Mathf.Clamp01(baseHeight + island * landHeight);
            }
        }

        return heights;
    }

    private static float GetIslandMask(float u, float v)
    {
        // Main oval body plus deliberate lobes to match the sketch:
        // SW beach/hub shelf, central body, NE lab shelf, E/SE ridge, NW crater mass.
        float dx = (u - 0.50f) / 0.56f;
        float dz = (v - 0.50f) / 0.50f;
        float distance = Mathf.Sqrt(dx * dx + dz * dz);

        float baseMask = Mathf.SmoothStep(1.02f, 0.58f, distance);
        float northWestCalderaMass = Gaussian(u, v, 0.34f, 0.79f, 0.34f, 0.22f) * 0.42f;
        float southWestBeachMass = Gaussian(u, v, 0.25f, 0.20f, 0.28f, 0.20f) * 0.34f;
        float northEastLabMass = Gaussian(u, v, 0.78f, 0.61f, 0.27f, 0.23f) * 0.31f;
        float eastControlMass = Gaussian(u, v, 0.80f, 0.38f, 0.22f, 0.23f) * 0.28f;
        float southEastCape = Gaussian(u, v, 0.69f, 0.18f, 0.22f, 0.15f) * 0.16f;

        float southBayCut = Gaussian(u, v, 0.45f, 0.055f, 0.31f, 0.095f) * 0.50f;
        float westCoveCut = Gaussian(u, v, 0.06f, 0.45f, 0.21f, 0.24f) * 0.26f;
        float northEastBite = Gaussian(u, v, 0.98f, 0.74f, 0.12f, 0.16f) * 0.20f;

        float coastlineNoise =
            (Mathf.PerlinNoise(u * 8.0f + 2.3f, v * 8.0f + 9.1f) - 0.5f) * 0.08f +
            (Mathf.PerlinNoise(u * 17.0f + 12.6f, v * 17.0f + 4.2f) - 0.5f) * 0.035f;

        float mask = baseMask + northWestCalderaMass + southWestBeachMass + northEastLabMass + eastControlMass + southEastCape;
        mask -= southBayCut + westCoveCut + northEastBite;
        mask += coastlineNoise;

        return Mathf.Clamp01(mask);
    }

    private static void ApplySourceHeightmapsAsDetail(float[,] heights, List<string> paths, Dictionary<string, Texture2D> cache)
    {
        ApplyStamp(heights, LoadTexture(FindHeightmap(paths, "IslandHeightmapsV3"), cache, 1024), 0.50f, 0.50f, 1.02f, 0f, 0.030f, StampBlendMode.DetailOnly);
        ApplyStamp(heights, LoadTexture(FindHeightmap(paths, "BeachHeightmaps"), cache, 1024), 0.25f, 0.15f, 0.36f, -15f, 0.018f, StampBlendMode.DetailOnly);
        ApplyStamp(heights, LoadTexture(FindHeightmap(paths, "ValleyLandscape"), cache, 1024), 0.48f, 0.48f, 0.60f, 24f, 0.024f, StampBlendMode.DetailOnly);
        ApplyStamp(heights, LoadTexture(FindHeightmap(paths, "CanyonsV4"), cache, 1024), 0.54f, 0.60f, 0.45f, 34f, 0.020f, StampBlendMode.Subtract);
        ApplyStamp(heights, LoadTexture(FindHeightmap(paths, "TerracedWorld"), cache, 1024), 0.76f, 0.58f, 0.34f, -10f, 0.018f, StampBlendMode.DetailOnly);
        ApplyStamp(heights, LoadTexture(FindHeightmap(paths, "VolcanoMountains"), cache, 1024), 0.34f, 0.79f, 0.42f, 0f, 0.025f, StampBlendMode.DetailOnly);
    }

    private static void ApplyGaiaStampsAsDetail(float[,] heights, List<string> paths, Dictionary<string, Texture2D> cache)
    {
        ApplyStamp(heights, LoadTexture(FindGaiaStamp(paths, "2K Islands", "Islands 08"), cache, 512), 0.50f, 0.50f, 0.95f, 0f, 0.026f, StampBlendMode.DetailOnly);
        ApplyStamp(heights, LoadTexture(FindGaiaStamp(paths, "2K Meadows", "Meadows 07"), cache, 512), 0.43f, 0.43f, 0.42f, 12f, 0.018f, StampBlendMode.DetailOnly);
        ApplyStamp(heights, LoadTexture(FindGaiaStamp(paths, "2K Seaside Cliffs", "Seaside Cliffs 05"), cache, 512), 0.15f, 0.46f, 0.32f, 78f, 0.026f, StampBlendMode.DetailOnly);
        ApplyStamp(heights, LoadTexture(FindGaiaStamp(paths, "2K Highlands", "Highlands 06"), cache, 512), 0.62f, 0.66f, 0.46f, -18f, 0.024f, StampBlendMode.DetailOnly);
        ApplyStamp(heights, LoadTexture(FindGaiaStamp(paths, "2K Rocky Plateaus", "Rocky Plateaus 04"), cache, 512), 0.78f, 0.56f, 0.31f, 18f, 0.024f, StampBlendMode.DetailOnly);
        ApplyStamp(heights, LoadTexture(FindGaiaStamp(paths, "2K Canyons", "Canyon 08"), cache, 512), 0.53f, 0.57f, 0.36f, -42f, 0.022f, StampBlendMode.Subtract);
        ApplyStamp(heights, LoadTexture(FindGaiaStamp(paths, "2K Craters", "Craters 06"), cache, 512), 0.34f, 0.79f, 0.30f, 8f, 0.030f, StampBlendMode.DetailOnly);
        ApplyStamp(heights, LoadTexture(FindGaiaStamp(paths, "2K Rugged Rocks", "Rugged Rocks 04"), cache, 512), 0.86f, 0.42f, 0.25f, -8f, 0.022f, StampBlendMode.DetailOnly);
    }

    private static void SculptConceptMacroLayout(float[,] heights)
    {
        // Intro / Chapter 1: broad beach + hub shelf.
        LowerBeachCove(heights, 0.24f, 0.13f, 0.35f, 0.16f, NormalizedWaterHeight + 0.010f);
        AddMesaPlateau(heights, 0.29f, 0.28f, 0.155f, 0.105f, 0.158f, 0.045f, 0.96f);
        AddMesaPlateau(heights, 0.40f, 0.33f, 0.105f, 0.078f, 0.138f, 0.030f, 0.78f);

        // Chapter 2: central forest valley with multiple clearings.
        LowerBasin(heights, 0.49f, 0.47f, 0.24f, 0.18f, 0.125f, 0.55f);
        AddSoftClearing(heights, 0.45f, 0.43f, 0.105f, 0.078f, 0.132f, 0.55f);
        AddSoftClearing(heights, 0.52f, 0.53f, 0.095f, 0.075f, 0.145f, 0.55f);
        AddSoftClearing(heights, 0.57f, 0.49f, 0.125f, 0.090f, 0.150f, 0.65f);

        // Chapter 3: secluded research-lab plateau.
        AddMesaPlateau(heights, 0.78f, 0.59f, 0.155f, 0.120f, 0.305f, 0.055f, 0.96f);
        AddMesaPlateau(heights, 0.86f, 0.68f, 0.080f, 0.065f, 0.285f, 0.040f, 0.72f);

        // Chapter 4: tall control ridge / tower approach.
        RaiseRidge(heights, new Vector2(0.61f, 0.31f), new Vector2(0.86f, 0.50f), 0.048f, 0.145f);
        RaiseRidge(heights, new Vector2(0.67f, 0.25f), new Vector2(0.84f, 0.42f), 0.035f, 0.105f);
        AddMesaPlateau(heights, 0.79f, 0.38f, 0.110f, 0.085f, 0.265f, 0.045f, 0.88f);
        AddMesaPlateau(heights, 0.70f, 0.31f, 0.115f, 0.082f, 0.205f, 0.040f, 0.75f);

        // Chapter 5: large dramatic caldera / final core basin.
        RaiseRidge(heights, new Vector2(0.18f, 0.55f), new Vector2(0.39f, 0.93f), 0.055f, 0.155f);
        SculptCaldera(heights, 0.34f, 0.79f, 0.235f, 0.170f, 0.235f, 0.210f);
        AddMesaPlateau(heights, 0.34f, 0.79f, 0.050f, 0.040f, 0.190f, 0.015f, 0.88f);
        AddMesaPlateau(heights, 0.48f, 0.70f, 0.065f, 0.040f, 0.225f, 0.024f, 0.62f);
    }

    private static void SculptPlayableRoutes(float[,] heights)
    {
        // Main progression route: beach -> hub -> forest -> lab -> ridge -> core.
        CarvePathValley(heights, new Vector2(0.24f, 0.13f), new Vector2(0.29f, 0.28f), 0.050f, 0.034f);
        CarvePathValley(heights, new Vector2(0.29f, 0.28f), new Vector2(0.45f, 0.43f), 0.045f, 0.030f);
        CarvePathValley(heights, new Vector2(0.45f, 0.43f), new Vector2(0.57f, 0.49f), 0.040f, 0.026f);
        CarvePathValley(heights, new Vector2(0.57f, 0.49f), new Vector2(0.78f, 0.59f), 0.036f, 0.030f);
        CarvePathValley(heights, new Vector2(0.78f, 0.59f), new Vector2(0.79f, 0.38f), 0.032f, 0.028f);
        CarvePathValley(heights, new Vector2(0.79f, 0.38f), new Vector2(0.48f, 0.70f), 0.030f, 0.026f);
        CarvePathValley(heights, new Vector2(0.48f, 0.70f), new Vector2(0.34f, 0.79f), 0.032f, 0.028f);

        // Secondary routes/shortcuts.
        CarvePathValley(heights, new Vector2(0.31f, 0.28f), new Vector2(0.52f, 0.53f), 0.030f, 0.020f);
        CarvePathValley(heights, new Vector2(0.52f, 0.53f), new Vector2(0.34f, 0.79f), 0.026f, 0.020f);
        CarvePathValley(heights, new Vector2(0.70f, 0.31f), new Vector2(0.24f, 0.13f), 0.020f, 0.014f);

        SculptCaveEntrance(heights, 0.55f, 0.66f, 0.055f, 0.036f);
        SculptCaveEntrance(heights, 0.86f, 0.48f, 0.050f, 0.030f);
    }

    private static void FlattenGameplayPads(float[,] heights)
    {
        FlattenPlateau(heights, 0.29f, 0.28f, 0.080f, 0.060f, 0.158f, 0.88f); // Beacon Camp usable pad
        FlattenPlateau(heights, 0.40f, 0.33f, 0.065f, 0.048f, 0.138f, 0.70f); // Mini-boss pad
        FlattenPlateau(heights, 0.57f, 0.49f, 0.085f, 0.065f, 0.150f, 0.70f); // Alpha Rex clearing
        FlattenPlateau(heights, 0.78f, 0.59f, 0.095f, 0.070f, 0.305f, 0.86f); // Lab building pad
        FlattenPlateau(heights, 0.79f, 0.38f, 0.070f, 0.052f, 0.265f, 0.88f); // Control tower pad
        FlattenPlateau(heights, 0.70f, 0.31f, 0.080f, 0.058f, 0.205f, 0.72f); // Titan arena
        FlattenPlateau(heights, 0.34f, 0.79f, 0.115f, 0.083f, 0.190f, 0.62f); // Final arena floor
    }

    private static void AddLocalizedTerrainDetail(float[,] heights)
    {
        AddRavine(heights, new Vector2(0.63f, 0.25f), new Vector2(0.67f, 0.12f), 0.030f, 0.055f);
        AddRavine(heights, new Vector2(0.60f, 0.63f), new Vector2(0.50f, 0.76f), 0.026f, 0.045f);
        AddRavine(heights, new Vector2(0.83f, 0.50f), new Vector2(0.96f, 0.45f), 0.022f, 0.040f);

        RaiseBrokenCoastalCliffs(heights);
        AddNoiseRelief(heights, 0.009f, 11.0f, 0.55f);
        AddNoiseRelief(heights, 0.004f, 34.0f, 0.25f);
    }

    private static void ReinforceConceptLandmarks(float[,] heights)
    {
        // Reapply after smoothing so important pads/basins stay readable from top-down.
        LowerBeachCove(heights, 0.24f, 0.13f, 0.35f, 0.16f, NormalizedWaterHeight + 0.010f);
        AddMesaPlateau(heights, 0.29f, 0.28f, 0.155f, 0.105f, 0.158f, 0.045f, 0.90f);
        AddMesaPlateau(heights, 0.78f, 0.59f, 0.155f, 0.120f, 0.305f, 0.055f, 0.90f);
        AddMesaPlateau(heights, 0.79f, 0.38f, 0.110f, 0.085f, 0.265f, 0.045f, 0.82f);
        SculptCaldera(heights, 0.34f, 0.79f, 0.235f, 0.170f, 0.235f, 0.210f);
        FlattenGameplayPads(heights);
    }

    private static void ApplyStamp(float[,] heights, Texture2D stamp, float centerX, float centerZ, float size, float rotationDegrees, float strength, StampBlendMode blendMode)
    {
        if (stamp == null)
        {
            return;
        }

        float radians = rotationDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        float halfSize = size * 0.5f;

        for (int z = 0; z < HeightmapResolution; z++)
        {
            float v = z / (float)(HeightmapResolution - 1);

            for (int x = 0; x < HeightmapResolution; x++)
            {
                float u = x / (float)(HeightmapResolution - 1);
                float localX = (u - centerX) / halfSize;
                float localZ = (v - centerZ) / halfSize;
                float rotatedX = localX * cos - localZ * sin;
                float rotatedZ = localX * sin + localZ * cos;

                if (Mathf.Abs(rotatedX) > 1f || Mathf.Abs(rotatedZ) > 1f)
                {
                    continue;
                }

                float stampU = rotatedX * 0.5f + 0.5f;
                float stampV = rotatedZ * 0.5f + 0.5f;
                float edgeFade = Mathf.SmoothStep(1f, 0f, Mathf.Max(Mathf.Abs(rotatedX), Mathf.Abs(rotatedZ)));
                float stampValue = stamp.GetPixelBilinear(stampU, stampV).r;
                stampValue = Mathf.SmoothStep(0.05f, 0.98f, stampValue) * edgeFade * GetIslandMask(u, v);

                switch (blendMode)
                {
                    case StampBlendMode.Max:
                        heights[z, x] = Mathf.Max(heights[z, x], stampValue * strength);
                        break;
                    case StampBlendMode.Subtract:
                        heights[z, x] -= stampValue * strength;
                        break;
                    case StampBlendMode.Average:
                        heights[z, x] = Mathf.Lerp(heights[z, x], stampValue * strength + heights[z, x] * 0.65f, edgeFade * 0.35f);
                        break;
                    case StampBlendMode.DetailOnly:
                        heights[z, x] += (stampValue - 0.5f * edgeFade * GetIslandMask(u, v)) * strength;
                        break;
                    default:
                        heights[z, x] += stampValue * strength;
                        break;
                }
            }
        }
    }

    private static void LowerBeachCove(float[,] heights, float centerX, float centerZ, float radiusX, float radiusZ, float targetHeight)
    {
        ForEachHeight((x, z, u, v) =>
        {
            float distance = EllipseDistance(u, v, centerX, centerZ, radiusX, radiusZ);
            float weight = Mathf.SmoothStep(1f, 0f, distance);
            if (weight > 0f)
            {
                float noise = (Mathf.PerlinNoise(u * 28f, v * 28f) - 0.5f) * 0.004f;
                heights[z, x] = Mathf.Lerp(heights[z, x], targetHeight + noise, weight * 0.88f);
            }
        });
    }

    private static void LowerBasin(float[,] heights, float centerX, float centerZ, float radiusX, float radiusZ, float targetHeight, float strength)
    {
        ForEachHeight((x, z, u, v) =>
        {
            float distance = EllipseDistance(u, v, centerX, centerZ, radiusX, radiusZ);
            float weight = Mathf.SmoothStep(1f, 0f, distance);
            if (weight > 0f)
            {
                float localTarget = targetHeight + (Mathf.PerlinNoise(u * 12f + 8f, v * 12f + 2f) - 0.5f) * 0.012f;
                heights[z, x] = Mathf.Lerp(heights[z, x], localTarget, weight * strength);
            }
        });
    }

    private static void AddSoftClearing(float[,] heights, float centerX, float centerZ, float radiusX, float radiusZ, float targetHeight, float strength)
    {
        ForEachHeight((x, z, u, v) =>
        {
            float distance = EllipseDistance(u, v, centerX, centerZ, radiusX, radiusZ);
            float weight = Mathf.SmoothStep(1f, 0f, distance);
            if (weight > 0f)
            {
                heights[z, x] = Mathf.Lerp(heights[z, x], targetHeight, weight * strength);
            }
        });
    }

    private static void FlattenPlateau(float[,] heights, float centerX, float centerZ, float radiusX, float radiusZ, float targetHeight, float strength)
    {
        ForEachHeight((x, z, u, v) =>
        {
            float distance = EllipseDistance(u, v, centerX, centerZ, radiusX, radiusZ);
            float weight = Mathf.SmoothStep(1f, 0f, distance);
            if (weight <= 0f)
            {
                return;
            }

            float microUndulation = (Mathf.PerlinNoise(u * 20f + 7f, v * 20f + 11f) - 0.5f) * 0.005f;
            float localTarget = targetHeight + microUndulation;
            heights[z, x] = Mathf.Lerp(heights[z, x], localTarget, weight * strength);
        });
    }

    private static void AddMesaPlateau(float[,] heights, float centerX, float centerZ, float radiusX, float radiusZ, float plateauHeight, float wallWidth, float strength)
    {
        ForEachHeight((x, z, u, v) =>
        {
            float distance = EllipseDistance(u, v, centerX, centerZ, radiusX, radiusZ);
            float outerDistance = EllipseDistance(u, v, centerX, centerZ, radiusX + wallWidth, radiusZ + wallWidth);

            if (outerDistance > 1f)
            {
                return;
            }

            float mesaNoise = (Mathf.PerlinNoise(u * 24f + 3.1f, v * 24f + 9.7f) - 0.5f) * 0.010f;
            float topTarget = plateauHeight + mesaNoise;

            if (distance <= 0.72f)
            {
                float topWeight = Mathf.SmoothStep(1f, 0f, distance / 0.72f);
                heights[z, x] = Mathf.Lerp(heights[z, x], topTarget, Mathf.Lerp(0.72f, 1f, topWeight) * strength);
                return;
            }

            float wallT = Mathf.InverseLerp(1f, 0.72f, distance);
            float cliffProfile = Mathf.SmoothStep(0f, 1f, wallT);
            float footNoise = Mathf.PerlinNoise(u * 18f + 12.5f, v * 18f + 6.5f) * 0.018f;
            float wallTarget = Mathf.Lerp(heights[z, x] + footNoise, plateauHeight, cliffProfile);
            float wallWeight = Mathf.SmoothStep(1f, 0f, outerDistance);

            heights[z, x] = Mathf.Max(heights[z, x], Mathf.Lerp(heights[z, x], wallTarget, wallWeight * strength));
        });
    }

    private static void CarvePathValley(float[,] heights, Vector2 start, Vector2 end, float width, float depth)
    {
        Vector2 segment = end - start;
        float segmentLengthSqr = Mathf.Max(segment.sqrMagnitude, 0.0001f);

        ForEachHeight((x, z, u, v) =>
        {
            Vector2 point = new Vector2(u, v);
            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / segmentLengthSqr);
            Vector2 closest = start + segment * t;
            float distance = Vector2.Distance(point, closest) / width;
            float weight = Mathf.SmoothStep(1f, 0f, distance);

            if (weight > 0f)
            {
                heights[z, x] -= depth * weight;
                heights[z, x] = Mathf.Lerp(heights[z, x], Mathf.Max(heights[z, x], NormalizedWaterHeight + 0.030f), weight * 0.10f);
            }
        });
    }

    private static void AddRavine(float[,] heights, Vector2 start, Vector2 end, float width, float depth)
    {
        Vector2 segment = end - start;
        float segmentLengthSqr = Mathf.Max(segment.sqrMagnitude, 0.0001f);

        ForEachHeight((x, z, u, v) =>
        {
            Vector2 point = new Vector2(u, v);
            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / segmentLengthSqr);
            Vector2 closest = start + segment * t;
            float distance = Vector2.Distance(point, closest) / width;
            float weight = Mathf.SmoothStep(1f, 0f, distance);

            if (weight > 0f)
            {
                heights[z, x] -= depth * weight * Mathf.Lerp(0.7f, 1.1f, t);
            }
        });
    }

    private static void RaiseRidge(float[,] heights, Vector2 start, Vector2 end, float width, float height)
    {
        Vector2 segment = end - start;
        float segmentLengthSqr = Mathf.Max(segment.sqrMagnitude, 0.0001f);

        ForEachHeight((x, z, u, v) =>
        {
            Vector2 point = new Vector2(u, v);
            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / segmentLengthSqr);
            Vector2 closest = start + segment * t;
            float distance = Vector2.Distance(point, closest) / width;
            float core = Mathf.SmoothStep(1f, 0f, distance);
            float shoulder = Mathf.SmoothStep(1f, 0f, distance / 2.2f) * 0.35f;
            float weight = core + shoulder;

            if (weight > 0f)
            {
                heights[z, x] += height * weight * Mathf.Lerp(0.75f, 1.15f, t);
            }
        });
    }

    private static void SculptCaldera(float[,] heights, float centerX, float centerZ, float radius, float bowlDepth, float rimHeight, float floorHeight)
    {
        ForEachHeight((x, z, u, v) =>
        {
            float distance = Vector2.Distance(new Vector2(u, v), new Vector2(centerX, centerZ)) / radius;

            if (distance < 0.43f)
            {
                float floorWeight = Mathf.SmoothStep(1f, 0f, distance / 0.43f);
                float brokenFloor = (Mathf.PerlinNoise(u * 34f + 1.4f, v * 34f + 2.8f) - 0.5f) * 0.012f;
                heights[z, x] = Mathf.Lerp(heights[z, x], floorHeight - bowlDepth * 0.26f + brokenFloor, 0.90f - floorWeight * 0.12f);
            }
            else if (distance < 0.80f)
            {
                float bowlT = Mathf.InverseLerp(0.43f, 0.80f, distance);
                float wallProfile = Mathf.SmoothStep(0f, 1f, bowlT);
                float target = Mathf.Lerp(floorHeight, floorHeight + rimHeight * 0.80f, wallProfile);
                heights[z, x] = Mathf.Lerp(heights[z, x], target, 0.72f);
            }
            else if (distance < 1.12f)
            {
                float rimT = Mathf.InverseLerp(0.80f, 1.12f, distance);
                float rim = Mathf.Sin(rimT * Mathf.PI);
                heights[z, x] += rim * rimHeight;
            }
        });
    }

    private static void SculptCaveEntrance(float[,] heights, float centerX, float centerZ, float radius, float depth)
    {
        ForEachHeight((x, z, u, v) =>
        {
            float distance = EllipseDistance(u, v, centerX, centerZ, radius, radius * 0.55f);
            float weight = Mathf.SmoothStep(1f, 0f, distance);

            if (weight > 0f)
            {
                heights[z, x] -= depth * weight;
            }
        });
    }

    private static void RaiseBrokenCoastalCliffs(float[,] heights)
    {
        ForEachHeight((x, z, u, v) =>
        {
            float island = GetIslandMask(u, v);
            float coastBand = Mathf.SmoothStep(0.18f, 0.55f, island) * (1f - Mathf.SmoothStep(0.56f, 0.82f, island));

            // Keep SW beach readable and low.
            float beachProtection = Gaussian(u, v, 0.24f, 0.13f, 0.37f, 0.18f);
            float cliffWeight = coastBand * (1f - beachProtection);

            if (cliffWeight > 0f)
            {
                float jagged = Mathf.PerlinNoise(u * 42f + 1f, v * 42f + 4f);
                heights[z, x] += cliffWeight * Mathf.Lerp(0.020f, 0.065f, jagged);
            }
        });
    }

    private static void AddNoiseRelief(float[,] heights, float strength, float frequency, float maskPower)
    {
        ForEachHeight((x, z, u, v) =>
        {
            float island = Mathf.Pow(GetIslandMask(u, v), maskPower);
            float noise = Mathf.PerlinNoise(u * frequency + 13.7f, v * frequency + 22.2f) - 0.5f;
            heights[z, x] += noise * strength * island;
        });
    }

    private static void SmoothHeightmap(float[,] heights, int iterations, float strength)
    {
        float[,] temp = new float[HeightmapResolution, HeightmapResolution];

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            for (int z = 1; z < HeightmapResolution - 1; z++)
            {
                for (int x = 1; x < HeightmapResolution - 1; x++)
                {
                    float center = heights[z, x] * 4f;
                    float cardinal = heights[z - 1, x] + heights[z + 1, x] + heights[z, x - 1] + heights[z, x + 1];
                    float diagonal = heights[z - 1, x - 1] + heights[z - 1, x + 1] + heights[z + 1, x - 1] + heights[z + 1, x + 1];
                    temp[z, x] = (center + cardinal * 2f + diagonal) / 16f;
                }
            }

            for (int z = 1; z < HeightmapResolution - 1; z++)
            {
                for (int x = 1; x < HeightmapResolution - 1; x++)
                {
                    heights[z, x] = Mathf.Lerp(heights[z, x], temp[z, x], strength);
                }
            }
        }
    }

    private static void ShapeFinalCoastline(float[,] heights)
    {
        ForEachHeight((x, z, u, v) =>
        {
            float islandMask = GetIslandMask(u, v);
            float shoreFade = Mathf.SmoothStep(0.18f, 0.66f, islandMask);
            float oceanFloor = 0.006f + Mathf.PerlinNoise(u * 15f, v * 15f) * 0.004f;
            float shoreTarget = Mathf.Lerp(oceanFloor, heights[z, x], shoreFade);
            heights[z, x] = Mathf.Clamp(shoreTarget, 0.004f, 0.92f);
        });
    }

    private static void NormalizePlayableHeightRange(float[,] heights)
    {
        ForEachHeight((x, z, u, v) =>
        {
            float island = GetIslandMask(u, v);
            float minLand = Mathf.Lerp(0.004f, NormalizedWaterHeight + 0.010f, Mathf.SmoothStep(0.25f, 0.60f, island));
            heights[z, x] = Mathf.Clamp(heights[z, x], minLand, 0.86f);
        });
    }

    private static TerrainData CreateOrReplaceTerrainData(float[,] heights)
    {
        TerrainData existing = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
        if (existing != null)
        {
            AssetDatabase.DeleteAsset(TerrainDataPath);
        }

        TerrainData terrainData = new TerrainData
        {
            heightmapResolution = HeightmapResolution,
            alphamapResolution = AlphamapResolution,
            baseMapResolution = 2048,
            size = new Vector3(TerrainSize, TerrainHeight, TerrainSize)
        };

        terrainData.SetHeights(0, 0, heights);
        AssetDatabase.CreateAsset(terrainData, TerrainDataPath);
        return terrainData;
    }

    private static void ConfigureTerrainComponent(Terrain terrain)
    {
        terrain.drawInstanced = true;
        terrain.heightmapPixelError = 3f;
        terrain.basemapDistance = 2200f;
        terrain.shadowCastingMode = ShadowCastingMode.On;
        terrain.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
        terrain.treeDistance = 1500f;
        terrain.treeBillboardDistance = 55f;
        terrain.treeCrossFadeLength = 8f;
        terrain.detailObjectDistance = 0f;
        terrain.detailObjectDensity = 0f;
        terrain.allowAutoConnect = true;
    }

    private static TerrainLayer[] CreateTerrainLayers()
    {
        TerrainLayer sand = CreateTerrainLayer("01_Sand_Beach", "Assets/Packages/package TextureHaven/terrain_4k/terrain_4k_textures/aerial_sand/aerial_sand_diff_4k.png", 42f, new Color(0.86f, 0.75f, 0.56f));
        TerrainLayer forest = CreateTerrainLayer("02_Primeval_Forest_Ground", "Assets/Packages/package TextureHaven/terrain_4k/terrain_4k_textures/grass_path_3/grass_path_3_diff_4k.png", 38f, new Color(0.31f, 0.42f, 0.20f));
        TerrainLayer dirt = CreateTerrainLayer("03_Dirt_Ridge", "Assets/Packages/package TextureHaven/terrain_4k/terrain_4k_textures/brown_mud_rocks_01/brown_mud_rocks_01_diff_4k.png", 34f, new Color(0.42f, 0.32f, 0.22f));
        TerrainLayer rock = CreateTerrainLayer("04_Rock_Cliff", "Assets/Packages/package TextureHaven/terrain_4k/terrain_4k_textures/rocks_ground_05/rocks_ground_05_diff_4k.png", 26f, new Color(0.43f, 0.42f, 0.38f));
        TerrainLayer core = CreateTerrainLayer("05_Core_Basin_Dust", "Assets/Packages/package TextureHaven/terrain_4k/terrain_4k_textures/brown_mud_leaves_01/brown_mud_leaves_01_diff_4k.png", 32f, new Color(0.40f, 0.36f, 0.32f));

        return new[] { sand, forest, dirt, rock, core };
    }

    private static TerrainLayer CreateTerrainLayer(string name, string diffusePath, float tileSize, Color fallbackTint)
    {
        string path = $"{TerrainLayerFolder}/{name}.terrainlayer";
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);

        if (layer == null)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, path);
        }

        Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);
        if (diffuse == null)
        {
            diffuse = CreateFallbackTexture($"T_{name}_Fallback", fallbackTint);
        }

        layer.diffuseTexture = diffuse;
        layer.tileSize = Vector2.one * tileSize;
        layer.smoothness = 0.08f;
        layer.metallic = 0f;
        EditorUtility.SetDirty(layer);

        return layer;
    }

    private static Texture2D CreateFallbackTexture(string name, Color color)
    {
        string texturePath = $"{MaterialFolder}/{name}.asset";
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (existing != null)
        {
            return existing;
        }

        Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false, true)
        {
            name = name
        };

        Color[] pixels = Enumerable.Repeat(color, 16).ToArray();
        texture.SetPixels(pixels);
        texture.Apply();
        AssetDatabase.CreateAsset(texture, texturePath);
        return texture;
    }

    private static void PaintTerrain(TerrainData terrainData, int layerCount)
    {
        float[,,] alpha = new float[AlphamapResolution, AlphamapResolution, layerCount];

        for (int z = 0; z < AlphamapResolution; z++)
        {
            float v = z / (float)(AlphamapResolution - 1);

            for (int x = 0; x < AlphamapResolution; x++)
            {
                float u = x / (float)(AlphamapResolution - 1);
                float height = terrainData.GetInterpolatedHeight(u, v);
                float height01 = height / TerrainHeight;
                float steepness = terrainData.GetSteepness(u, v);

                float finalCore = Gaussian(u, v, 0.34f, 0.79f, 0.25f, 0.18f);
                float beach = Mathf.Clamp01(1f - Mathf.InverseLerp(WaterHeight + 5f, WaterHeight + 27f, height));
                float cliff = Mathf.Clamp01(Mathf.InverseLerp(34f, 58f, steepness));
                float ridgeDirt = Mathf.Clamp01(Mathf.InverseLerp(0.18f, 0.42f, height01)) * (1f - beach);
                float coreDust = Mathf.Clamp01(finalCore * Mathf.Clamp01(1f - Mathf.InverseLerp(0.22f, 0.42f, height01))) * (1f - beach);
                float forest = Mathf.Clamp01(1f - beach - cliff * 0.85f - ridgeDirt * 0.35f - coreDust * 0.90f);

                float total = beach + forest + ridgeDirt + cliff + coreDust;
                if (total <= 0.0001f)
                {
                    forest = 1f;
                    total = 1f;
                }

                alpha[z, x, 0] = beach / total;
                alpha[z, x, 1] = forest / total;
                alpha[z, x, 2] = ridgeDirt / total;
                alpha[z, x, 3] = cliff / total;
                alpha[z, x, 4] = coreDust / total;
            }
        }

        terrainData.SetAlphamaps(0, 0, alpha);
    }

    private static void AssignTerrainPhysicsMaterial(Terrain terrain)
    {
        TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
        if (collider == null)
        {
            collider = terrain.gameObject.AddComponent<TerrainCollider>();
            collider.terrainData = terrain.terrainData;
        }

        collider.sharedMaterial = CreateTerrainPhysicsMaterial();
    }

    private static PhysicsMaterial CreateTerrainPhysicsMaterial()
    {
        PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(TerrainPhysicsMaterialPath);

        if (material == null)
        {
            material = new PhysicsMaterial("PM_Terrain_Default");
            AssetDatabase.CreateAsset(material, TerrainPhysicsMaterialPath);
        }

        material.dynamicFriction = 0.6f;
        material.staticFriction = 0.7f;
        material.bounciness = 0f;
        material.frictionCombine = PhysicsMaterialCombine.Average;
        material.bounceCombine = PhysicsMaterialCombine.Minimum;
        EditorUtility.SetDirty(material);

        return material;
    }

    private static void CreateWater()
    {
        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Plane);
        water.name = "Ocean Water Plane";
        water.transform.position = new Vector3(0f, WaterHeight, 0f);
        water.transform.localScale = new Vector3(150f, 1f, 150f);

        Material material = CreateWaterMaterial();
        water.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static Material CreateWaterMaterial()
    {
        string materialPath = $"{MaterialFolder}/M_Eden7_OceanWater.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            material = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(material, materialPath);
        }

        material.color = new Color(0.06f, 0.33f, 0.48f, 0.52f);
        SetFloatIfExists(material, "_Surface", 1f);
        SetFloatIfExists(material, "_Blend", 0f);
        SetFloatIfExists(material, "_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);

        return material;
    }

    private static void CreateLighting()
    {
        GameObject sun = new GameObject("Directional Light");
        Light sunLight = sun.AddComponent<Light>();
        sunLight.type = LightType.Directional;
        sunLight.intensity = 1.18f;
        sunLight.color = new Color(1f, 0.95f, 0.86f);
        sunLight.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(52f, -38f, 0f);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.42f, 0.48f, 0.58f);
        RenderSettings.ambientEquatorColor = new Color(0.29f, 0.33f, 0.31f);
        RenderSettings.ambientGroundColor = new Color(0.11f, 0.10f, 0.08f);
    }

    private static void CreateCamera(Terrain terrain)
    {
        GameObject cameraObject = new GameObject("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.fieldOfView = 46f;
        camera.farClipPlane = 2600f;
        cameraObject.AddComponent<AudioListener>();

        Vector3 target = new Vector3(90f, terrain.SampleHeight(new Vector3(90f, 0f, 110f)) + 40f, 110f);
        cameraObject.transform.position = new Vector3(-180f, 390f, -620f);
        cameraObject.transform.LookAt(target);
    }

    private static void CreateZoneMarkers(Terrain terrain)
    {
        GameObject root = new GameObject("Eden-7 Terrain Zone Markers");

        foreach (ZoneAnchor anchor in ZoneAnchors)
        {
            CreateMarker(root.transform, terrain, anchor);
        }
    }

    private static void CreateMarker(Transform root, Terrain terrain, ZoneAnchor anchor)
    {
        Vector3 position = NormalizedToWorld(anchor.X, anchor.Z);
        position.y = terrain.SampleHeight(position) + 2f;

        GameObject marker = new GameObject(anchor.Name);
        marker.transform.SetParent(root);
        marker.transform.position = position;
    }

    private static void CreateGreyboxLandmarks(Terrain terrain)
    {
        GameObject root = new GameObject("Eden-7 Greybox Landmark Placeholders");

        Material campMat = CreateColorMaterial("M_Greybox_Camp_Cyan", new Color(0.2f, 0.7f, 0.9f, 0.7f));
        Material labMat = CreateColorMaterial("M_Greybox_Lab_Green", new Color(0.35f, 0.85f, 0.55f, 0.75f));
        Material towerMat = CreateColorMaterial("M_Greybox_Tower_Red", new Color(0.9f, 0.25f, 0.22f, 0.85f));
        Material coreMat = CreateColorMaterial("M_Greybox_Core_Purple", new Color(0.60f, 0.25f, 1f, 0.85f));

        CreatePad(root.transform, terrain, "GB_BeaconCamp_OperationsPad", 0.29f, 0.28f, new Vector3(78f, 6f, 58f), campMat);
        CreatePad(root.transform, terrain, "GB_RuinedResearchLab_Footprint", 0.78f, 0.59f, new Vector3(92f, 7f, 62f), labMat);
        CreateTower(root.transform, terrain, "GB_ControlTower_Relay", 0.79f, 0.38f, 18f, 86f, towerMat);
        CreatePad(root.transform, terrain, "GB_EdenCore_FinalArena", 0.34f, 0.79f, new Vector3(130f, 5f, 98f), coreMat);
        CreateTower(root.transform, terrain, "GB_EdenCore_CoreSpire", 0.34f, 0.79f, 16f, 58f, coreMat);
    }

    private static void CreatePad(Transform root, Terrain terrain, string name, float normalizedX, float normalizedZ, Vector3 scale, Material material)
    {
        Vector3 position = NormalizedToWorld(normalizedX, normalizedZ);
        position.y = terrain.SampleHeight(position) + scale.y * 0.5f + 0.5f;

        GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = name;
        pad.transform.SetParent(root);
        pad.transform.position = position;
        pad.transform.localScale = scale;
        pad.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static void CreateTower(Transform root, Terrain terrain, string name, float normalizedX, float normalizedZ, float radius, float height, Material material)
    {
        Vector3 position = NormalizedToWorld(normalizedX, normalizedZ);
        position.y = terrain.SampleHeight(position) + height * 0.5f + 1f;

        GameObject tower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tower.name = name;
        tower.transform.SetParent(root);
        tower.transform.position = position;
        tower.transform.localScale = new Vector3(radius, height * 0.5f, radius);
        tower.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static Material CreateColorMaterial(string name, Color color)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader != null ? shader : Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void WriteLayoutReadme()
    {
        string text =
            "ASTRA EDEN - Eden-7 Terrain Layout\n" +
            "Generated by GaiaUserDataMapBuilder_Reworked.\n\n" +
            "Macro layout:\n" +
            "- SW coast: Intro Beach Crash Site + broad sandy beach.\n" +
            "- SW low plateau: Beacon Camp / hub.\n" +
            "- Center: Primeval Forest basin, rolling hills, nest clearings, Alpha Rex clearing.\n" +
            "- NE plateau: Ruined Research Lab.\n" +
            "- E/SE ridge: Control Tower / relay approach + Titan Ankylor arena.\n" +
            "- NW caldera: Eden Core Facility / final crater arena.\n\n" +
            "Zone markers:\n";

        foreach (ZoneAnchor anchor in ZoneAnchors)
        {
            Vector3 world = NormalizedToWorld(anchor.X, anchor.Z);
            text += $"- {anchor.Name}: normalized=({anchor.X:0.000}, {anchor.Z:0.000}), world=({world.x:0.0}, {world.z:0.0}) :: {anchor.Role}\n";
        }

        File.WriteAllText(LayoutReadmePath, text);
        AssetDatabase.ImportAsset(LayoutReadmePath);
    }

    private static Vector3 NormalizedToWorld(float normalizedX, float normalizedZ)
    {
        return new Vector3((normalizedX - 0.5f) * TerrainSize, 0f, (normalizedZ - 0.5f) * TerrainSize);
    }

    private static string FindHeightmap(List<string> paths, string folderName)
    {
        return paths.FirstOrDefault(path => path.Contains($"/{folderName}/")) ?? paths.FirstOrDefault();
    }

    private static string FindGaiaStamp(List<string> paths, string folderName, string fileNameWithoutExtension)
    {
        string match = paths.FirstOrDefault(path =>
            path.Contains($"/{folderName}/") &&
            Path.GetFileNameWithoutExtension(path) == fileNameWithoutExtension);

        if (!string.IsNullOrEmpty(match))
        {
            return match;
        }

        return paths.FirstOrDefault(path => path.Contains($"/{folderName}/")) ?? paths.FirstOrDefault();
    }

    private static Texture2D LoadTexture(string path, Dictionary<string, Texture2D> cache, int size)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        string cacheKey = $"{path}:{size}";
        if (cache.TryGetValue(cacheKey, out Texture2D cached))
        {
            return cached;
        }

        Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (source == null)
        {
            Debug.LogWarning($"Missing terrain source texture: {path}");
            return null;
        }

        Texture2D readable = CreateReadableCopy(source, size);
        cache.Add(cacheKey, readable);

        return readable;
    }

    private static Texture2D CreateReadableCopy(Texture2D source, int size)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture renderTexture = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);

        Graphics.Blit(source, renderTexture);
        RenderTexture.active = renderTexture;

        Texture2D readable = new Texture2D(size, size, TextureFormat.RGBAFloat, false, true);
        readable.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        readable.Apply(false, false);

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTexture);

        return readable;
    }

    private static float Gaussian(float u, float v, float centerX, float centerZ, float radiusX, float radiusZ)
    {
        float dx = (u - centerX) / radiusX;
        float dz = (v - centerZ) / radiusZ;
        return Mathf.Exp(-(dx * dx + dz * dz));
    }

    private static float EllipseDistance(float u, float v, float centerX, float centerZ, float radiusX, float radiusZ)
    {
        float dx = (u - centerX) / radiusX;
        float dz = (v - centerZ) / radiusZ;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static void ForEachHeight(System.Action<int, int, float, float> action)
    {
        for (int z = 0; z < HeightmapResolution; z++)
        {
            float v = z / (float)(HeightmapResolution - 1);

            for (int x = 0; x < HeightmapResolution; x++)
            {
                float u = x / (float)(HeightmapResolution - 1);
                action(x, z, u, v);
            }
        }
    }

    private static void SetFloatIfExists(Material material, string property, float value)
    {
        if (material.HasProperty(property))
        {
            material.SetFloat(property, value);
        }
    }

    private static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string[] parts = folderPath.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
