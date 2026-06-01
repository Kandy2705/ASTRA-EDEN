using UnityEditor;
using UnityEngine;

public static class EdenTerrainAutoPainter
{
    private static readonly string[] RequiredLayerNames =
    {
        "TL_Sand_Beach",
        "TL_Dirt_Path",
        "TL_Grass_Forest",
        "TL_Rock_Cliff",
        "TL_DarkRock_Core"
    };

    private const int SandLayer = 0;
    private const int DirtLayer = 1;
    private const int GrassLayer = 2;
    private const int RockLayer = 3;
    private const int DarkRockLayer = 4;
    private const int PaintedLayerCount = 5;

    [MenuItem("ASTRA EDEN/World/Auto Paint Eden Terrain")]
    public static void AutoPaintEdenTerrain()
    {
        Terrain terrain = GetTargetTerrain();
        if (terrain == null)
        {
            Debug.LogError("Auto Paint Eden Terrain failed: select a Terrain or make sure Terrain.activeTerrain exists.");
            return;
        }

        TerrainData terrainData = terrain.terrainData;
        if (terrainData == null)
        {
            Debug.LogError("Auto Paint Eden Terrain failed: target Terrain has no TerrainData.");
            return;
        }

        Undo.RegisterCompleteObjectUndo(terrainData, "Auto Paint Eden Terrain");
        if (!EnsureRequiredTerrainLayers(terrainData))
        {
            return;
        }

        int layerCount = terrainData.alphamapLayers;
        if (layerCount < PaintedLayerCount)
        {
            Debug.LogError($"Auto Paint Eden Terrain needs at least {PaintedLayerCount} Terrain Layers. Current layer count: {layerCount}");
            return;
        }

        int width = terrainData.alphamapWidth;
        int height = terrainData.alphamapHeight;
        float[,,] alphamaps = new float[height, width, layerCount];

        for (int y = 0; y < height; y++)
        {
            float v = y / (float)(height - 1);

            for (int x = 0; x < width; x++)
            {
                float u = x / (float)(width - 1);

                float normalizedHeight = GetNormalizedHeight(terrainData, u, v);
                float normalizedSlope = terrainData.GetSteepness(u, v) / 90f;
                float noise = GetLayerNoise(u, v);

                float[] weights = CalculateLayerWeights(u, v, normalizedHeight, normalizedSlope, noise, layerCount);

                for (int layer = 0; layer < layerCount; layer++)
                {
                    alphamaps[y, x, layer] = weights[layer];
                }
            }
        }

        terrainData.SetAlphamaps(0, 0, alphamaps);
        EditorUtility.SetDirty(terrainData);
        AssetDatabase.SaveAssets();

        Debug.Log($"Auto Paint Eden Terrain complete. Alphamap: {width}x{height}, layers painted: {PaintedLayerCount}, terrain layers found: {layerCount}");
    }

    private static Terrain GetTargetTerrain()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected != null)
        {
            Terrain selectedTerrain = selected.GetComponent<Terrain>();
            if (selectedTerrain != null)
            {
                return selectedTerrain;
            }

            selectedTerrain = selected.GetComponentInParent<Terrain>();
            if (selectedTerrain != null)
            {
                return selectedTerrain;
            }
        }

        return Terrain.activeTerrain;
    }

    private static bool EnsureRequiredTerrainLayers(TerrainData terrainData)
    {
        TerrainLayer[] terrainLayers = terrainData.terrainLayers;
        if (HasRequiredTerrainLayers(terrainLayers))
        {
            return true;
        }

        TerrainLayer[] requiredLayers = new TerrainLayer[PaintedLayerCount];
        for (int i = 0; i < RequiredLayerNames.Length; i++)
        {
            requiredLayers[i] = FindTerrainLayer(RequiredLayerNames[i]);
            if (requiredLayers[i] == null)
            {
                Debug.LogError($"Auto Paint Eden Terrain failed: could not find Terrain Layer asset named '{RequiredLayerNames[i]}'.");
                return false;
            }
        }

        TerrainLayer[] mergedLayers = new TerrainLayer[Mathf.Max(terrainLayers.Length, PaintedLayerCount)];
        for (int i = 0; i < terrainLayers.Length; i++)
        {
            mergedLayers[i] = terrainLayers[i];
        }

        for (int i = 0; i < PaintedLayerCount; i++)
        {
            mergedLayers[i] = requiredLayers[i];
        }

        terrainData.terrainLayers = mergedLayers;
        EditorUtility.SetDirty(terrainData);

        Debug.Log("Auto Paint Eden Terrain assigned required Terrain Layers: " + string.Join(", ", RequiredLayerNames));
        return true;
    }

    private static bool HasRequiredTerrainLayers(TerrainLayer[] terrainLayers)
    {
        if (terrainLayers == null || terrainLayers.Length < PaintedLayerCount)
        {
            return false;
        }

        for (int i = 0; i < PaintedLayerCount; i++)
        {
            if (terrainLayers[i] == null || terrainLayers[i].name != RequiredLayerNames[i])
            {
                return false;
            }
        }

        return true;
    }

    private static TerrainLayer FindTerrainLayer(string layerName)
    {
        string[] guids = AssetDatabase.FindAssets(layerName + " t:TerrainLayer");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            TerrainLayer terrainLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (terrainLayer != null && terrainLayer.name == layerName)
            {
                return terrainLayer;
            }
        }

        return null;
    }

    private static float GetNormalizedHeight(TerrainData terrainData, float u, float v)
    {
        float worldHeight = terrainData.GetInterpolatedHeight(u, v);
        return Mathf.Clamp01(worldHeight / Mathf.Max(1f, terrainData.size.y));
    }

    private static float[] CalculateLayerWeights(float u, float v, float normalizedHeight, float normalizedSlope, float noise, int layerCount)
    {
        float lowBeach = 1f - Smooth01(0.045f, 0.115f, normalizedHeight + noise * 0.018f);
        float wetEdge = 1f - Smooth01(0.025f, 0.075f, normalizedHeight);
        float steepRock = Smooth01(0.24f, 0.56f, normalizedSlope) * Smooth01(0.075f, 0.22f, normalizedHeight);
        float highRock = Smooth01(0.34f, 0.58f, normalizedHeight) * (0.30f + normalizedSlope * 0.90f);

        float midHeight = Bell(normalizedHeight, 0.12f, 0.38f);
        float flatness = 1f - Smooth01(0.08f, 0.28f, normalizedSlope);
        float slopeGrassFade = 1f - Smooth01(0.18f, 0.34f, normalizedSlope);
        float grass = midHeight * flatness * slopeGrassFade * (0.85f + noise * 0.30f);

        float transitionBand = Smooth01(0.075f, 0.18f, normalizedHeight) * (1f - Smooth01(0.46f, 0.62f, normalizedHeight));
        float moderateSlope = Bell(normalizedSlope, 0.10f, 0.48f);
        float naturalPathNoise = Smooth01(0.42f, 0.72f, Mathf.PerlinNoise(u * 8.0f + 41.7f, v * 8.0f + 13.2f));
        float dirt = transitionBand * (0.35f + moderateSlope * 0.55f + naturalPathNoise * 0.35f);
        dirt += Smooth01(0.16f, 0.32f, normalizedSlope) * (1f - Smooth01(0.52f, 0.72f, normalizedSlope)) * 0.28f;

        float finalCoreMask = Mathf.Max(
            EllipseMask(u, v, 0.34f, 0.79f, 0.19f, 0.15f),
            EllipseMask(u, v, 0.68f, 0.82f, 0.15f, 0.12f)
        );
        float darkRock = Smooth01(0.48f, 0.70f, normalizedHeight) * 0.75f;
        darkRock += finalCoreMask * (0.65f + normalizedSlope * 0.35f);
        darkRock += Smooth01(0.62f, 0.82f, normalizedSlope) * Smooth01(0.28f, 0.46f, normalizedHeight) * 0.35f;

        float sand = Mathf.Max(lowBeach, wetEdge * 0.75f);
        float rock = Mathf.Max(steepRock, highRock * 0.65f);

        // Cross-fade priorities keep cliffs/core readable without hard cuts.
        grass *= 1f - Mathf.Clamp01(sand * 0.85f + rock * 0.35f + darkRock * 0.65f);
        dirt *= 1f - Mathf.Clamp01(sand * 0.55f + darkRock * 0.45f);
        sand *= 1f - Mathf.Clamp01(rock * 0.35f + darkRock * 0.65f);
        rock *= 1f - Mathf.Clamp01(darkRock * 0.45f);

        float[] weights = new float[layerCount];
        weights[SandLayer] = Mathf.Max(0.001f, sand);
        weights[DirtLayer] = Mathf.Max(0.001f, dirt);
        weights[GrassLayer] = Mathf.Max(0.001f, grass);
        weights[RockLayer] = Mathf.Max(0.001f, rock);
        weights[DarkRockLayer] = Mathf.Max(0.001f, darkRock);

        Normalize(weights);
        return weights;
    }

    private static float GetLayerNoise(float u, float v)
    {
        float large = Mathf.PerlinNoise(u * 5.5f + 9.1f, v * 5.5f + 2.3f);
        float medium = Mathf.PerlinNoise(u * 17.0f + 31.4f, v * 17.0f + 8.6f);
        return (large * 0.65f + medium * 0.35f) - 0.5f;
    }

    private static float Smooth01(float min, float max, float value)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(min, max, value));
    }

    private static float Bell(float value, float min, float max)
    {
        float up = Smooth01(min, Mathf.Lerp(min, max, 0.45f), value);
        float down = 1f - Smooth01(Mathf.Lerp(min, max, 0.65f), max, value);
        return Mathf.Clamp01(up * down);
    }

    private static float EllipseMask(float u, float v, float centerU, float centerV, float radiusU, float radiusV)
    {
        float du = (u - centerU) / radiusU;
        float dv = (v - centerV) / radiusV;
        float distance = Mathf.Sqrt(du * du + dv * dv);
        return 1f - Smooth01(0.35f, 1f, distance);
    }

    private static void Normalize(float[] weights)
    {
        float total = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            total += weights[i];
        }

        if (total <= 0.0001f)
        {
            weights[GrassLayer] = 1f;
            return;
        }

        for (int i = 0; i < weights.Length; i++)
        {
            weights[i] /= total;
        }
    }
}
