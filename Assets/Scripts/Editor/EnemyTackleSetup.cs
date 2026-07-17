#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class EnemyTackleSetup
{
    const string EnemyPrefabPath = "Assets/_Project/Prefab/Enemy.prefab";
    const string MiniBossPrefabPath = "Assets/_Project/Prefab/Enemy_MiniBoss_Velociraptor.prefab";
    const string WildClawPrefabPath = "Assets/_Project/Prefab/Enemy_WildClawRaptor.prefab";
    const string FangPrefabPath = "Assets/_Project/Prefab/Enemy_FangRaptor.prefab";
    const string PlayerPrefabPath = "Assets/Prefabs/Vroids/Seeker Prototype/Seeker Prototype Nu.prefab";

    [MenuItem("ASTRA EDEN/Enemies/Setup Tackle Push (Enemy + Player Prefabs)")]
    public static void SetupAllPrefabs()
    {
        SetupEnemyPrefab(EnemyPrefabPath);
        SetupEnemyPrefab(MiniBossPrefabPath);
        SetupEnemyPrefab(WildClawPrefabPath);
        SetupEnemyPrefab(FangPrefabPath);
        SetupPlayerPrefab();
        AssetDatabase.SaveAssets();
        Debug.Log("[TackleSetup] Tackle push hitbox + PlayerKnockbackReceiver wired on all raptor enemy prefabs + Player.");
    }

    [MenuItem("ASTRA EDEN/Enemies/Setup Tackle Push On Selected Enemy")]
    public static void SetupSelectedEnemy()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("[TackleSetup] Chọn enemy root trong Hierarchy hoặc Prefab mode.");
            return;
        }

        Transform enemyRoot = PrefabUtility.GetNearestPrefabInstanceRoot(selected) != null
            ? PrefabUtility.GetNearestPrefabInstanceRoot(selected).transform
            : selected.transform;

        EnsureTacklePushHitbox(enemyRoot.gameObject);
        WireAnimationRelay(enemyRoot.gameObject);
        EditorUtility.SetDirty(enemyRoot.gameObject);
        Debug.Log($"[TackleSetup] Tackle push wired on '{enemyRoot.name}'.");
    }

    static void SetupEnemyPrefab(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            EnsureTacklePushHitbox(root);
            WireAnimationRelay(root);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void SetupPlayerPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            if (root.GetComponent<PlayerKnockbackReceiver>() == null)
            {
                root.AddComponent<PlayerKnockbackReceiver>();
            }

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static void EnsureTacklePushHitboxPublic(GameObject enemyRoot) => EnsureTacklePushHitbox(enemyRoot);

    public static void WireAnimationRelayPublic(GameObject enemyRoot) => WireAnimationRelay(enemyRoot);

    static void EnsureTacklePushHitbox(GameObject enemyRoot)
    {
        Transform existing = enemyRoot.transform.Find("TacklePushHitbox");
        GameObject hitboxObject;

        if (existing != null)
        {
            hitboxObject = existing.gameObject;
        }
        else
        {
            hitboxObject = new GameObject("TacklePushHitbox");
            hitboxObject.transform.SetParent(enemyRoot.transform, false);
            hitboxObject.transform.localPosition = new Vector3(0f, 0.55f, 0.65f);
            hitboxObject.transform.localRotation = Quaternion.identity;
        }

        BoxCollider box = hitboxObject.GetComponent<BoxCollider>();
        if (box == null)
        {
            box = hitboxObject.AddComponent<BoxCollider>();
        }

        box.isTrigger = true;
        box.enabled = false;
        box.size = new Vector3(0.9f, 0.7f, 0.8f);
        box.center = new Vector3(0f, 0.1f, 0.15f);

        Rigidbody body = hitboxObject.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = hitboxObject.AddComponent<Rigidbody>();
        }

        body.isKinematic = true;
        body.useGravity = false;

        EnemyPushHitbox pushHitbox = hitboxObject.GetComponent<EnemyPushHitbox>();
        if (pushHitbox == null)
        {
            pushHitbox = hitboxObject.AddComponent<EnemyPushHitbox>();
        }

        SerializedObject pushSo = new SerializedObject(pushHitbox);
        pushSo.FindProperty("hitboxCollider").objectReferenceValue = box;
        pushSo.FindProperty("directionSource").objectReferenceValue = enemyRoot.transform;
        pushSo.FindProperty("pushDistance").floatValue = 4.2f;
        pushSo.FindProperty("pushDuration").floatValue = 0.18f;
        pushSo.FindProperty("verticalLift").floatValue = 0.15f;
        pushSo.ApplyModifiedPropertiesWithoutUndo();
    }

    static void WireAnimationRelay(GameObject enemyRoot)
    {
        EnemyAnimationEventRelay relay = enemyRoot.GetComponent<EnemyAnimationEventRelay>();
        EnemyPushHitbox pushHitbox = enemyRoot.GetComponentInChildren<EnemyPushHitbox>(true);
        if (relay == null || pushHitbox == null)
        {
            return;
        }

        SerializedObject relaySo = new SerializedObject(relay);
        relaySo.FindProperty("tacklePushHitbox").objectReferenceValue = pushHitbox;
        relaySo.ApplyModifiedPropertiesWithoutUndo();

        EnemyAIController ai = enemyRoot.GetComponent<EnemyAIController>();
        if (ai != null)
        {
            SerializedObject aiSo = new SerializedObject(ai);
            aiSo.FindProperty("useTackle").boolValue = true;
            aiSo.FindProperty("attacksBeforeTackle").intValue = 2;
            aiSo.FindProperty("tackleRange").floatValue = 3.2f;
            aiSo.FindProperty("tackleCooldown").floatValue = 6f;
            aiSo.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif