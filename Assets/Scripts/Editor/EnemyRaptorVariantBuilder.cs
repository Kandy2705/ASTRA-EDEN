#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Build gameplay enemy prefabs from PBR Velociraptor color variants (textures/materials).
/// Menu: ASTRA EDEN → Enemies → Build Raptor Texture Variants (WildClaw + Fang)
/// </summary>
public static class EnemyRaptorVariantBuilder
{
    const string EnemyTemplatePath = "Assets/_Project/Prefab/Enemy.prefab";
    const string GreenSourcePath = "Assets/Packages/PBRVelociraptor/Prefabs/PBR/15K/PBR_Velociraptor_Green.prefab";
    const string SandMaleSourcePath = "Assets/Packages/PBRVelociraptor/Prefabs/PBR/15K/PBR_Velociraptor_Sand_Male.prefab";

    const string WildClawPrefabPath = "Assets/_Project/Prefab/Enemy_WildClawRaptor.prefab";
    const string FangPrefabPath = "Assets/_Project/Prefab/Enemy_FangRaptor.prefab";

    const string WildClawDataPath = "Assets/_Project/ScriptableObjects/Enemies/Units/SO_Enemy_EnemyWildClawRaptor.asset";
    const string FangDataPath = "Assets/_Project/ScriptableObjects/Enemies/Units/SO_Enemy_EnemyFangRaptor.asset";

    // V12PBR body materials (Albedo patterns A/E).
    const string BodyAMatPath = "Assets/Packages/PBRVelociraptor/Materials/V12PBR/RaptorBodyA.mat";
    const string BodyEMatPath = "Assets/Packages/PBRVelociraptor/Materials/V12PBR/RaptorBodyE.mat";
    const string EyesMatPath = "Assets/Packages/PBRVelociraptor/Materials/V12PBR/RaptorEyesClawsTeethSpikes.mat";
    const string CorneaMatPath = "Assets/Packages/PBRVelociraptor/Materials/V12PBR/RaptorCornea.mat";

    [MenuItem("ASTRA EDEN/Enemies/Build Raptor Texture Variants (WildClaw + Fang)")]
    public static void BuildWildClawAndFang()
    {
        BuildVariant(
            sourcePath: GreenSourcePath,
            outputPath: WildClawPrefabPath,
            prefabName: "Enemy_WildClawRaptor",
            localScale: 0.5f,
            bodyMaterialPath: BodyAMatPath,
            enemyDataPath: WildClawDataPath);

        BuildVariant(
            sourcePath: SandMaleSourcePath,
            outputPath: FangPrefabPath,
            prefabName: "Enemy_FangRaptor",
            localScale: 0.55f,
            bodyMaterialPath: BodyEMatPath,
            enemyDataPath: FangDataPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[EnemyRaptorVariantBuilder] Built Enemy_WildClawRaptor (green) + Enemy_FangRaptor (sand).");
    }

    static void BuildVariant(
        string sourcePath,
        string outputPath,
        string prefabName,
        float localScale,
        string bodyMaterialPath,
        string enemyDataPath)
    {
        GameObject sourceRoot = PrefabUtility.LoadPrefabContents(sourcePath);
        GameObject templateRoot = PrefabUtility.LoadPrefabContents(EnemyTemplatePath);
        try
        {
            sourceRoot.name = prefabName;
            sourceRoot.transform.localScale = Vector3.one * localScale;
            sourceRoot.transform.localRotation = Quaternion.identity;

            ApplyRaptorMaterials(sourceRoot, bodyMaterialPath);
            AddEnemyGameplayComponents(sourceRoot, templateRoot);

            string folder = Path.GetDirectoryName(outputPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
            {
                // Expect Assets/_Project/Prefab already exists.
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }

            PrefabUtility.SaveAsPrefabAsset(sourceRoot, outputPath);

            EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(enemyDataPath);
            GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(outputPath);
            if (data != null && savedPrefab != null)
            {
                SerializedObject dataSo = new SerializedObject(data);
                dataSo.FindProperty("enemyPrefab").objectReferenceValue = savedPrefab;
                dataSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
            }

            Debug.Log($"[EnemyRaptorVariantBuilder] {prefabName} → {outputPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(sourceRoot);
            PrefabUtility.UnloadPrefabContents(templateRoot);
        }
    }

    static void ApplyRaptorMaterials(GameObject root, string bodyMaterialPath)
    {
        Material body = AssetDatabase.LoadAssetAtPath<Material>(bodyMaterialPath);
        Material eyes = AssetDatabase.LoadAssetAtPath<Material>(EyesMatPath);
        Material cornea = AssetDatabase.LoadAssetAtPath<Material>(CorneaMatPath);

        foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr == null)
            {
                continue;
            }

            string n = smr.gameObject.name;
            if (n == "Retopo" && body != null)
            {
                // Retopo typically uses body + eyes submeshes.
                Material[] mats = smr.sharedMaterials;
                if (mats == null || mats.Length == 0)
                {
                    smr.sharedMaterials = eyes != null
                        ? new[] { body, eyes }
                        : new[] { body };
                }
                else
                {
                    mats[0] = body;
                    if (mats.Length > 1 && eyes != null)
                    {
                        mats[1] = eyes;
                    }

                    smr.sharedMaterials = mats;
                }
            }
            else if (n == "Spikes" && eyes != null)
            {
                smr.sharedMaterial = eyes;
            }
            else if (n == "Cornea" && cornea != null)
            {
                smr.sharedMaterial = cornea;
            }
        }
    }

    static void AddEnemyGameplayComponents(GameObject root, GameObject template)
    {
        // Remove default capsule if present; match Enemy body blocking.
        CapsuleCollider capsule = root.GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            Object.DestroyImmediate(capsule);
        }

        BoxCollider templateBox = template != null ? template.GetComponent<BoxCollider>() : null;
        BoxCollider bodyCollider = root.GetComponent<BoxCollider>();
        if (bodyCollider == null)
        {
            bodyCollider = root.AddComponent<BoxCollider>();
        }

        if (templateBox != null)
        {
            bodyCollider.center = templateBox.center;
            bodyCollider.size = templateBox.size;
        }
        else
        {
            bodyCollider.center = new Vector3(0.052734375f, 0.8257828f, 0.37524414f);
            bodyCollider.size = new Vector3(1.546875f, 1.9324493f, 3.6594238f);
        }

        bodyCollider.isTrigger = false;
        bodyCollider.enabled = true;

        Rigidbody templateBody = template != null ? template.GetComponent<Rigidbody>() : null;
        Rigidbody body = root.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = root.AddComponent<Rigidbody>();
        }

        body.mass = templateBody != null ? templateBody.mass : 100f;
        body.useGravity = false;
        body.isKinematic = true;

        NavMeshAgent templateAgent = template != null ? template.GetComponent<NavMeshAgent>() : null;
        NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = root.AddComponent<NavMeshAgent>();
        }

        if (templateAgent != null)
        {
            agent.radius = templateAgent.radius;
            agent.height = templateAgent.height;
            agent.speed = templateAgent.speed;
            agent.angularSpeed = templateAgent.angularSpeed;
            agent.stoppingDistance = templateAgent.stoppingDistance;
            agent.baseOffset = templateAgent.baseOffset;
            agent.acceleration = templateAgent.acceleration;
        }
        else
        {
            agent.radius = 0.27f;
            agent.height = 0.52f;
            agent.speed = 3.5f;
            agent.angularSpeed = 120f;
            agent.stoppingDistance = 0.1f;
        }

        CharacterHealth health = EnsureComponent<CharacterHealth>(root);
        EnemySensor sensor = EnsureComponent<EnemySensor>(root);
        EnemyAIController ai = EnsureComponent<EnemyAIController>(root);
        CharacterKnockback knockback = EnsureComponent<CharacterKnockback>(root);
        EnsureComponent<LootDropSpawner>(root);
        DissolveOnDeath dissolve = EnsureComponent<DissolveOnDeath>(root);
        EnemyAnimationEventRelay relay = EnsureComponent<EnemyAnimationEventRelay>(root);

        Transform templateHitbox = template != null ? template.transform.Find("AttackHitbox") : null;
        if (templateHitbox != null && root.transform.Find("AttackHitbox") == null)
        {
            GameObject hitbox = Object.Instantiate(templateHitbox.gameObject, root.transform);
            hitbox.name = "AttackHitbox";
        }

        Transform eye = root.transform.Find("EyeSensor");
        if (eye == null)
        {
            var eyeObject = new GameObject("EyeSensor");
            eyeObject.transform.SetParent(root.transform, false);
            eyeObject.transform.localPosition = new Vector3(0f, 1.6f, 0.5f);
            eye = eyeObject.transform;
        }

        Animator animator = root.GetComponent<Animator>();
        Animator templateAnimator = template != null ? template.GetComponent<Animator>() : null;
        if (animator != null && templateAnimator != null)
        {
            animator.runtimeAnimatorController = templateAnimator.runtimeAnimatorController;
            animator.applyRootMotion = false;
        }

        EnemyAttackHitbox attackHitbox = root.GetComponentInChildren<EnemyAttackHitbox>(true);

        SerializedObject aiSo = new SerializedObject(ai);
        aiSo.FindProperty("sensor").objectReferenceValue = sensor;
        aiSo.FindProperty("health").objectReferenceValue = health;
        aiSo.FindProperty("knockback").objectReferenceValue = knockback;
        aiSo.FindProperty("animator").objectReferenceValue = animator;
        if (attackHitbox != null)
        {
            aiSo.FindProperty("attackHitbox").objectReferenceValue = attackHitbox;
        }

        aiSo.FindProperty("flipForward180").boolValue = true;
        aiSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject sensorSo = new SerializedObject(sensor);
        sensorSo.FindProperty("eyeSensor").objectReferenceValue = eye;
        sensorSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject relaySo = new SerializedObject(relay);
        relaySo.FindProperty("aiOwner").objectReferenceValue = ai;
        relaySo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject dissolveSo = new SerializedObject(dissolve);
        dissolveSo.FindProperty("characterHealth").objectReferenceValue = health;
        dissolveSo.ApplyModifiedPropertiesWithoutUndo();

        EnemyTackleSetup.EnsureTacklePushHitboxPublic(root);
        EnemyTackleSetup.WireAnimationRelayPublic(root);

        // World-space HP bar trên đầu — copy pattern prefab Enemy.
        EnemyHUDBuilder.EnsureHudOnRoot(root, canvasLocalY: 1.7f, canvasScale: 0.01f, showDistance: 6f);
    }

    static T EnsureComponent<T>(GameObject go) where T : Component
    {
        T comp = go.GetComponent<T>();
        if (comp == null)
        {
            comp = go.AddComponent<T>();
        }

        return comp;
    }
}
#endif
