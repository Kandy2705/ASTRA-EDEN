#if UNITY_EDITOR
using DissolveExample;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Đồng bộ Enemy prefab từ instance đang có trên scene (material, gameplay wiring, cleanup demo scripts).
/// </summary>
public static class EnemyPrefabSyncFromScene
{
    const string EnemyPrefabPath = "Assets/_Project/Prefab/Enemy.prefab";

    [MenuItem("ASTRA EDEN/Enemies/Sync Enemy Prefab From Scene Instance")]
    public static void SyncFromScene()
    {
        GameObject sceneInstance = FindSceneEnemyInstance();
        if (sceneInstance == null)
        {
            EditorUtility.DisplayDialog(
                "Enemy Sync",
                "Không tìm thấy Enemy prefab instance trong scene.\n\nKéo Enemy vào scene (hoặc mở World_Eden7) rồi chạy lại.",
                "OK");
            return;
        }

        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
        if (prefabAsset == null)
        {
            Debug.LogError($"[EnemySync] Không tìm thấy prefab tại {EnemyPrefabPath}");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Sync Enemy Prefab",
                $"Sẽ copy cấu hình từ '{sceneInstance.name}' (scene) vào Enemy.prefab.\n\n" +
                "Giữ: materials, gameplay components, animator.\n" +
                "Bỏ: DissolveChilds/Rotator demo, vị trí scene, patrol point refs.\n\n" +
                "Tiếp tục?",
                "Sync",
                "Cancel"))
        {
            return;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
        try
        {
            ApplySceneConfiguration(sceneInstance, prefabRoot);
            prefabRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, EnemyPrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[EnemySync] Đã sync Enemy.prefab từ scene instance '{sceneInstance.name}'.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    public static void ApplySceneConfiguration(GameObject source, GameObject target)
    {
        if (source == null || target == null)
        {
            return;
        }

        RemoveDemoComponents(target);
        CopyRendererMaterials(source, target);
        EnsureEyeSensor(target);
        FixCharacterHealth(target);
        WireGameplayReferences(target);

        Animator sourceAnimator = source.GetComponent<Animator>();
        Animator targetAnimator = target.GetComponent<Animator>();
        if (sourceAnimator != null && targetAnimator != null)
        {
            targetAnimator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
            targetAnimator.avatar = sourceAnimator.avatar;
            targetAnimator.applyRootMotion = false;
            targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        CopySerializedComponent<EnemyAIController>(source, target);
        CopySerializedComponent<EnemySensor>(source, target);
        CopySerializedComponent<CharacterKnockback>(source, target);
        CopySerializedComponent<DissolveOnDeath>(source, target);
        CopySerializedComponent<EnemyAnimationEventRelay>(source, target);
        CopySerializedComponent<EnemyAttackHitbox>(source, target, includeChildren: true);
        CopySerializedComponent<LootDropSpawner>(source, target);
        CopySerializedComponent<EnemyHUDRange>(source, target);

        EnemyAIController ai = target.GetComponent<EnemyAIController>();
        if (ai != null)
        {
            SerializedObject aiSo = new SerializedObject(ai);
            aiSo.FindProperty("patrolPoints").ClearArray();
            aiSo.FindProperty("debugState").boolValue = false;
            aiSo.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(target);
    }

    static GameObject FindSceneEnemyInstance()
    {
        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            GameObject match = FindEnemyInHierarchy(root.transform);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    static GameObject FindEnemyInHierarchy(Transform root)
    {
        if (root.name == "Enemy" && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root.gameObject) == EnemyPrefabPath)
        {
            return root.gameObject;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            GameObject match = FindEnemyInHierarchy(root.GetChild(i));
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    static void RemoveDemoComponents(GameObject root)
    {
        DissolveChilds dissolveChilds = root.GetComponent<DissolveChilds>();
        if (dissolveChilds != null)
        {
            UnityEngine.Object.DestroyImmediate(dissolveChilds);
        }

        Rotator[] rotators = root.GetComponentsInChildren<Rotator>(true);
        for (int i = 0; i < rotators.Length; i++)
        {
            UnityEngine.Object.DestroyImmediate(rotators[i]);
        }
    }

    static void CopyRendererMaterials(GameObject source, GameObject target)
    {
        SkinnedMeshRenderer[] sourceRenderers = source.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        SkinnedMeshRenderer[] targetRenderers = target.GetComponentsInChildren<SkinnedMeshRenderer>(true);

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            SkinnedMeshRenderer targetRenderer = targetRenderers[i];
            SkinnedMeshRenderer sourceRenderer = FindRendererByName(sourceRenderers, targetRenderer.gameObject.name);
            if (sourceRenderer == null)
            {
                continue;
            }

            Material[] shared = sourceRenderer.sharedMaterials;
            Material[] copy = new Material[shared.Length];
            for (int m = 0; m < shared.Length; m++)
            {
                copy[m] = shared[m];
            }

            targetRenderer.sharedMaterials = copy;
        }
    }

    static SkinnedMeshRenderer FindRendererByName(SkinnedMeshRenderer[] renderers, string objectName)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].gameObject.name == objectName)
            {
                return renderers[i];
            }
        }

        return null;
    }

    static void EnsureEyeSensor(GameObject root)
    {
        Transform eye = root.transform.Find("EyeSensor");
        if (eye == null)
        {
            GameObject eyeObject = new GameObject("EyeSensor");
            eyeObject.transform.SetParent(root.transform, false);
            eyeObject.transform.localPosition = new Vector3(0f, 1.6f, 0.5f);
            eye = eyeObject.transform;
        }

        EnemySensor sensor = root.GetComponent<EnemySensor>();
        if (sensor != null)
        {
            SerializedObject sensorSo = new SerializedObject(sensor);
            sensorSo.FindProperty("eyeSensor").objectReferenceValue = eye;
            sensorSo.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void FixCharacterHealth(GameObject root)
    {
        CharacterHealth health = root.GetComponent<CharacterHealth>();
        if (health == null)
        {
            return;
        }

        SerializedObject healthSo = new SerializedObject(health);
        healthSo.FindProperty("initializeFromCharacterData").boolValue = false;
        healthSo.FindProperty("characterData").objectReferenceValue = null;
        healthSo.ApplyModifiedPropertiesWithoutUndo();
    }

    static void WireGameplayReferences(GameObject root)
    {
        CharacterHealth health = root.GetComponent<CharacterHealth>();
        EnemySensor sensor = root.GetComponent<EnemySensor>();
        EnemyAIController ai = root.GetComponent<EnemyAIController>();
        CharacterKnockback knockback = root.GetComponent<CharacterKnockback>();
        Animator animator = root.GetComponent<Animator>();
        EnemyAttackHitbox attackHitbox = root.GetComponentInChildren<EnemyAttackHitbox>(true);
        DissolveOnDeath dissolve = root.GetComponent<DissolveOnDeath>();
        EnemyAnimationEventRelay relay = root.GetComponent<EnemyAnimationEventRelay>();
        EnemyHUDRange hudRange = root.GetComponent<EnemyHUDRange>();

        if (ai != null)
        {
            SerializedObject aiSo = new SerializedObject(ai);
            aiSo.FindProperty("health").objectReferenceValue = health;
            aiSo.FindProperty("sensor").objectReferenceValue = sensor;
            aiSo.FindProperty("knockback").objectReferenceValue = knockback;
            aiSo.FindProperty("animator").objectReferenceValue = animator;
            aiSo.FindProperty("attackHitbox").objectReferenceValue = attackHitbox;
            aiSo.FindProperty("useHitAnimation").boolValue = true;
            aiSo.ApplyModifiedPropertiesWithoutUndo();
        }

        if (dissolve != null && health != null)
        {
            SerializedObject dissolveSo = new SerializedObject(dissolve);
            dissolveSo.FindProperty("characterHealth").objectReferenceValue = health;
            dissolveSo.ApplyModifiedPropertiesWithoutUndo();
        }

        if (relay != null && ai != null)
        {
            SerializedObject relaySo = new SerializedObject(relay);
            relaySo.FindProperty("aiOwner").objectReferenceValue = ai;
            relaySo.FindProperty("patrolOwner").objectReferenceValue = null;
            relaySo.ApplyModifiedPropertiesWithoutUndo();
        }

        if (hudRange != null && health != null)
        {
            SerializedObject hudSo = new SerializedObject(hudRange);
            hudSo.FindProperty("characterHealth").objectReferenceValue = health;
            hudSo.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void CopySerializedComponent<T>(GameObject source, GameObject target, bool includeChildren = false)
        where T : Component
    {
        T sourceComponent = includeChildren
            ? source.GetComponentInChildren<T>(true)
            : source.GetComponent<T>();
        T targetComponent = includeChildren
            ? target.GetComponentInChildren<T>(true)
            : target.GetComponent<T>();

        if (sourceComponent == null || targetComponent == null)
        {
            return;
        }

        Undo.RecordObject(targetComponent, $"Copy {typeof(T).Name}");
        EditorUtility.CopySerializedManagedFieldsOnly(sourceComponent, targetComponent);
    }
}
#endif