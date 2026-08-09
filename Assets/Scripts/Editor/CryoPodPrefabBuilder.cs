#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Assembles a lightweight cutscene cryopod from the existing spaceship Pod/Bed
/// meshes and sci-fi terminal, then places one instance under CutScene 4/Poi.
/// </summary>
public static class CryoPodPrefabBuilder
{
    public const string PrefabPath = "Assets/_Project/Prefab/Environment/CryoPod.prefab";
    public const string ScenePath = "Assets/Scenes/CutScenes/CutScene 4.unity";

    private const string SceneInstanceName = "CryoPod - Intro CryoWake";
    private const string SpaceshipModelPath = "Assets/Prefabs/Environment/spaceship-scene/spaceship-scene.fbx";
    private const string TerminalModelPath = "Assets/Prefabs/Environment/scifi-terminal-2/source/SM_Terminal_3_embedded.fbx";
    private const string MetalMaterialPath = "Assets/Prefabs/Environment/Hub/Material/M_Metal_Panel.mat";
    private const string PodMaterialPath = "Assets/Prefabs/Environment/spaceship-scene/textures/Pods.mat";
    private const string GlassMaterialPath = "Assets/_Project/Materials/CryoPod/CryoPod_Glass.mat";

    [MenuItem("Tools/ASTRA EDEN/Cutscenes/Build CryoPod At CutScene 4 Poi")]
    public static void BuildAndPlace()
    {
        EnsureFolder("Assets/_Project/Prefab/Environment");
        EnsureFolder("Assets/_Project/Materials/CryoPod");

        GameObject spaceshipModel = AssetDatabase.LoadAssetAtPath<GameObject>(SpaceshipModelPath);
        GameObject terminalModel = AssetDatabase.LoadAssetAtPath<GameObject>(TerminalModelPath);
        Material metalMaterial = AssetDatabase.LoadAssetAtPath<Material>(MetalMaterialPath);
        Material podMaterial = AssetDatabase.LoadAssetAtPath<Material>(PodMaterialPath);
        Material glassMaterial = GetOrCreateGlassMaterial();
        if (spaceshipModel == null || terminalModel == null || metalMaterial == null || podMaterial == null || glassMaterial == null)
        {
            Debug.LogError("[CryoPod] Thiếu spaceship Pod/Bed, terminal hoặc material cần thiết; prefab chưa được tạo.");
            return;
        }

        GameObject root = new("CryoPod");
        try
        {
            CryoPodCutsceneRig rig = root.AddComponent<CryoPodCutsceneRig>();

            GameObject basePart = CloneAndFitModelPart(
                spaceshipModel,
                "Pod1",
                "Base",
                root.transform,
                new Vector3(2.15f, 1.45f, 3.35f),
                new Vector3(0f, 0.76f, 0f),
                Quaternion.identity);
            if (basePart == null)
            {
                Debug.LogError("[CryoPod] Không tìm thấy mesh group 'Pod1' trong spaceship-scene.fbx.");
                return;
            }

            AssignMaterialToRenderers(basePart, podMaterial);

            CreateBox("BackPanel", root.transform, new Vector3(0f, 0.17f, 0f), new Vector3(1.72f, 0.22f, 2.82f), metalMaterial);
            CreateBox("SideFrame_L", root.transform, new Vector3(-0.94f, 0.67f, 0f), new Vector3(0.18f, 0.34f, 3.08f), metalMaterial);
            CreateBox("SideFrame_R", root.transform, new Vector3(0.94f, 0.67f, 0f), new Vector3(0.18f, 0.34f, 3.08f), metalMaterial);
            CreateBox("TopFrame", root.transform, new Vector3(0f, 1.05f, 1.52f), new Vector3(1.95f, 0.27f, 0.24f), metalMaterial);

            Transform glassCover = CreateEmpty("GlassCover", root.transform, Vector3.zero, Quaternion.identity);
            CreateBox("GlassSide_L", glassCover, new Vector3(-0.78f, 0.94f, 0f), new Vector3(0.08f, 0.47f, 2.55f), glassMaterial);
            CreateBox("GlassSide_R", glassCover, new Vector3(0.78f, 0.94f, 0f), new Vector3(0.08f, 0.47f, 2.55f), glassMaterial);

            GameObject bed = CloneAndFitModelPart(
                spaceshipModel,
                "Bed1",
                "InteriorBed",
                root.transform,
                new Vector3(1.35f, 0.34f, 2.45f),
                new Vector3(0f, 0.43f, 0.05f),
                Quaternion.identity);
            if (bed == null)
            {
                bed = CreateBox("InteriorBed", root.transform, new Vector3(0f, 0.43f, 0.05f), new Vector3(1.35f, 0.25f, 2.45f), podMaterial);
            }

            Transform playerAnchor = CreateEmpty(
                "PlayerAnchor",
                root.transform,
                new Vector3(0f, 0.62f, 0.9f),
                Quaternion.Euler(-90f, 0f, 0f));

            Transform doorPivot = CreateEmpty(
                "DoorPivot",
                root.transform,
                new Vector3(0f, 1.18f, 1.34f),
                Quaternion.identity);
            CreateBox(
                "GlassDoor",
                doorPivot,
                new Vector3(0f, 0f, -1.31f),
                new Vector3(1.55f, 0.075f, 2.62f),
                glassMaterial);

            GameObject controlPanelObject = CloneAndFitModelPart(
                terminalModel,
                terminalModel.name,
                "ControlPanel",
                root.transform,
                new Vector3(0.48f, 0.82f, 0.38f),
                new Vector3(1.28f, 0.58f, -0.72f),
                Quaternion.Euler(0f, -18f, 0f));
            if (controlPanelObject == null)
            {
                controlPanelObject = CreateBox(
                    "ControlPanel",
                    root.transform,
                    new Vector3(1.28f, 0.58f, -0.72f),
                    new Vector3(0.42f, 0.72f, 0.32f),
                    metalMaterial);
            }

            Light interiorLight = CreateLight(
                "Light_Interior",
                root.transform,
                new Vector3(0f, 1.1f, 0f),
                new Color(0.18f, 0.82f, 1f),
                2.2f,
                3.2f);
            Light statusLight = CreateLight(
                "Light_Status",
                root.transform,
                new Vector3(1.08f, 1.02f, -0.7f),
                new Color(1f, 0.34f, 0.08f),
                2.8f,
                1.35f);

            GameObject statusLens = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            statusLens.name = "StatusLens";
            statusLens.transform.SetParent(statusLight.transform, false);
            statusLens.transform.localPosition = Vector3.zero;
            statusLens.transform.localScale = Vector3.one * 0.08f;
            RemoveCollider(statusLens);
            statusLens.GetComponent<Renderer>().sharedMaterial = podMaterial;

            Transform vfxRoot = CreateEmpty("VFX_Root", root.transform, new Vector3(0f, 0.78f, 0f), Quaternion.identity);
            CreateEmpty("SteamSpawn_L", vfxRoot, new Vector3(-0.72f, 0f, -1.12f), Quaternion.identity);
            CreateEmpty("SteamSpawn_R", vfxRoot, new Vector3(0.72f, 0f, -1.12f), Quaternion.identity);

            rig.EditorConfigure(playerAnchor, doorPivot, controlPanelObject.transform, vfxRoot, interiorLight, statusLight);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool prefabSaved);
            if (!prefabSaved || prefab == null)
            {
                Debug.LogError($"[CryoPod] Không thể lưu prefab tại {PrefabPath}.");
                return;
            }

            PlaceAtCutScenePoi(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CryoPod] Đã tạo {PrefabPath} và đặt tại CutScene 4/Poi.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static Material GetOrCreateGlassMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
        if (existing != null)
        {
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("[CryoPod] Không tìm thấy shader Universal Render Pipeline/Lit.");
            return null;
        }

        Material material = new(shader)
        {
            name = "CryoPod_Glass",
            renderQueue = (int)RenderQueue.Transparent
        };
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Smoothness", 0.92f);
        material.SetFloat("_Metallic", 0.08f);
        material.SetColor("_BaseColor", new Color(0.34f, 0.86f, 1f, 0.28f));
        material.SetColor("_Color", new Color(0.34f, 0.86f, 1f, 0.28f));
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        material.SetOverrideTag("RenderType", "Transparent");
        AssetDatabase.CreateAsset(material, GlassMaterialPath);
        return material;
    }

    private static void PlaceAtCutScenePoi(GameObject prefab)
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForPlacement = !scene.IsValid() || !scene.isLoaded;
        if (openedForPlacement)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        try
        {
            GameObject poi = FindInScene(scene, "Poi");
            if (poi == null)
            {
                Debug.LogError("[CryoPod] Không tìm thấy object 'Poi' trong CutScene 4.");
                return;
            }

            Transform existing = FindChildRecursive(poi.transform, SceneInstanceName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
            {
                Debug.LogError("[CryoPod] Không thể instantiate prefab vào CutScene 4.");
                return;
            }

            instance.name = SceneInstanceName;
            instance.transform.SetParent(poi.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            EditorUtility.SetDirty(instance);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (openedForPlacement && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static GameObject CloneAndFitModelPart(
        GameObject modelAsset,
        string sourceName,
        string objectName,
        Transform parent,
        Vector3 targetSize,
        Vector3 targetCenter,
        Quaternion rotation)
    {
        if (modelAsset == null)
        {
            return null;
        }

        Transform source = modelAsset.name == sourceName
            ? modelAsset.transform
            : FindChildRecursive(modelAsset.transform, sourceName);
        if (source == null)
        {
            return null;
        }

        GameObject clone = Object.Instantiate(source.gameObject);
        clone.name = objectName;
        clone.transform.SetParent(parent, false);
        clone.transform.localPosition = Vector3.zero;
        clone.transform.localRotation = rotation;
        clone.transform.localScale = Vector3.one;
        RemoveAllColliders(clone);

        if (!TryGetRendererBounds(clone, out Bounds bounds))
        {
            Object.DestroyImmediate(clone);
            return null;
        }

        Vector3 size = bounds.size;
        Vector3 scale = new(
            size.x > 0.001f ? targetSize.x / size.x : 1f,
            size.y > 0.001f ? targetSize.y / size.y : 1f,
            size.z > 0.001f ? targetSize.z / size.z : 1f);
        clone.transform.localScale = scale;

        if (TryGetRendererBounds(clone, out Bounds fittedBounds))
        {
            clone.transform.position += targetCenter - fittedBounds.center;
        }

        return clone;
    }

    private static GameObject CreateBox(string objectName, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = objectName;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPosition;
        box.transform.localRotation = Quaternion.identity;
        box.transform.localScale = localScale;
        RemoveCollider(box);
        Renderer renderer = box.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }

        return box;
    }

    private static Transform CreateEmpty(string objectName, Transform parent, Vector3 localPosition, Quaternion localRotation)
    {
        GameObject gameObject = new(objectName);
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = localPosition;
        gameObject.transform.localRotation = localRotation;
        gameObject.transform.localScale = Vector3.one;
        return gameObject.transform;
    }

    private static Light CreateLight(
        string objectName,
        Transform parent,
        Vector3 localPosition,
        Color color,
        float intensity,
        float range)
    {
        GameObject lightObject = new(objectName, typeof(Light));
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localPosition = localPosition;
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        return light;
    }

    private static void AssignMaterialToRenderers(GameObject target, Material material)
    {
        if (target == null || material == null)
        {
            return;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] materials = renderers[i].sharedMaterials;
            for (int j = 0; j < materials.Length; j++)
            {
                materials[j] = material;
            }

            renderers[i].sharedMaterials = materials;
        }
    }

    private static bool TryGetRendererBounds(GameObject target, out Bounds bounds)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        bounds = default;
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        return hasBounds;
    }

    private static void RemoveAllColliders(GameObject target)
    {
        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Object.DestroyImmediate(colliders[i]);
        }
    }

    private static void RemoveCollider(GameObject target)
    {
        Collider collider = target.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }
    }

    private static GameObject FindInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == objectName)
            {
                return root;
            }

            Transform nested = FindChildRecursive(root.transform, objectName);
            if (nested != null)
            {
                return nested.gameObject;
            }
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform parent, string objectName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == objectName)
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, objectName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
