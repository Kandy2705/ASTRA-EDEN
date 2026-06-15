using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class EnemyTemplateApplier
{
    [MenuItem("ASTRA EDEN/Enemies/Apply Enemy Template (Compy) On Selected")]
    public static void ApplyOnSelected()
    {
        GameObject enemy = Selection.activeGameObject;
        if (enemy == null)
        {
            Debug.LogError("[EnemyTemplate] Select an enemy GameObject (in Hierarchy or open the prefab) first.");
            return;
        }

        if (PrefabUtility.IsPartOfPrefabAsset(enemy))
        {
            Debug.LogWarning(
                "[EnemyTemplate] You picked a prefab asset directly. " +
                "Open the prefab (double-click) then select the root, OR drop the prefab into a scene and run the tool on the scene instance. " +
                "This avoids unintended writes to imported model files.");
            return;
        }

        Undo.SetCurrentGroupName("Apply Enemy Template");
        int undoGroup = Undo.GetCurrentGroup();

        EnsureAnimator(enemy);
        EnsureRigidbody(enemy);
        EnsureBoxCollider(enemy);
        EnsureNavMeshAgent(enemy);

        CharacterHealth health = EnsureCharacterHealth(enemy);
        EnemyAttackHitbox attackHitbox = EnsureAttackHitboxChild(enemy);
        EnemyPatrol patrol = EnsureEnemyPatrol(enemy, health, attackHitbox);
        EnsureRagdollOnDeath(enemy, health);
        EnsureAnimationEventRelay(enemy, patrol);

        EnemyHUDBuilder.SetupSelected();

        Undo.CollapseUndoOperations(undoGroup);
        EditorUtility.SetDirty(enemy);

        Debug.Log(
            $"[EnemyTemplate] Applied enemy template on '{enemy.name}'. " +
            "Animator controller is NOT auto-assigned because compy's controller references compy bones. " +
            "Assign your own AnimatorController with params: Blend (float), Horizontal (float), Vertical (float), Attack (trigger).");
    }

    private static Animator EnsureAnimator(GameObject enemy)
    {
        Animator animator = enemy.GetComponent<Animator>();
        if (animator == null)
        {
            animator = enemy.GetComponentInChildren<Animator>(true);
        }
        if (animator == null)
        {
            animator = Undo.AddComponent<Animator>(enemy);
        }
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        return animator;
    }

    private static Rigidbody EnsureRigidbody(GameObject enemy)
    {
        Rigidbody rb = enemy.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = Undo.AddComponent<Rigidbody>(enemy);
        }
        Undo.RecordObject(rb, "Configure Rigidbody");
        rb.useGravity = false;
        rb.isKinematic = true;
        return rb;
    }

    private static BoxCollider EnsureBoxCollider(GameObject enemy)
    {
        BoxCollider box = enemy.GetComponent<BoxCollider>();
        if (box == null)
        {
            box = Undo.AddComponent<BoxCollider>(enemy);
        }

        Renderer[] renderers = enemy.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            Undo.RecordObject(box, "Fit BoxCollider To Mesh");
            box.center = enemy.transform.InverseTransformPoint(bounds.center);
            Vector3 scale = enemy.transform.lossyScale;
            box.size = new Vector3(
                Mathf.Max(bounds.size.x / Mathf.Max(scale.x, 0.001f), 0.1f),
                Mathf.Max(bounds.size.y / Mathf.Max(scale.y, 0.001f), 0.1f),
                Mathf.Max(bounds.size.z / Mathf.Max(scale.z, 0.001f), 0.1f));
        }
        return box;
    }

    private static NavMeshAgent EnsureNavMeshAgent(GameObject enemy)
    {
        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = Undo.AddComponent<NavMeshAgent>(enemy);
        }
        Undo.RecordObject(agent, "Configure NavMeshAgent");
        agent.radius = 0.27f;
        agent.height = 0.52f;
        agent.speed = 3.5f;
        agent.acceleration = 8f;
        agent.angularSpeed = 120f;
        agent.stoppingDistance = 0.1f;
        agent.autoBraking = true;
        agent.autoRepath = true;
        return agent;
    }

    private static CharacterHealth EnsureCharacterHealth(GameObject enemy)
    {
        CharacterHealth health = enemy.GetComponent<CharacterHealth>();
        if (health == null)
        {
            health = Undo.AddComponent<CharacterHealth>(enemy);
        }

        var so = new SerializedObject(health);
        var fallback = so.FindProperty("fallbackBaseStats");
        if (fallback != null)
        {
            SetIfExists(fallback, "maxHP", 100f);
            SetIfExists(fallback, "attack", 20f);
            SetIfExists(fallback, "defense", 10f);
            SetIfExists(fallback, "critRate", 0.05f);
            SetIfExists(fallback, "critDamage", 0.5f);
            SetIfExists(fallback, "moveSpeed", 5f);
            SetIfExists(fallback, "attackSpeed", 1f);
            SetIfExists(fallback, "staminaMax", 100f);
            SetIfExists(fallback, "staminaRegen", 12f);
            SetIfExists(fallback, "energyMax", 100f);
            SetIfExists(fallback, "energyRegen", 5f);
            SetIfExists(fallback, "companionSynergy", 1f);
        }
        so.FindProperty("initializeFromCharacterData").boolValue = true;
        so.FindProperty("destroyOnDeath").boolValue = false;
        so.ApplyModifiedProperties();
        return health;
    }

    private static void SetIfExists(SerializedProperty parent, string name, float value)
    {
        var prop = parent.FindPropertyRelative(name);
        if (prop != null) prop.floatValue = value;
    }

    private static EnemyAttackHitbox EnsureAttackHitboxChild(GameObject enemy)
    {
        Transform child = enemy.transform.Find("AttackHitbox");
        GameObject hitboxObj;
        if (child == null)
        {
            hitboxObj = new GameObject("AttackHitbox");
            Undo.RegisterCreatedObjectUndo(hitboxObj, "Create AttackHitbox");
            hitboxObj.transform.SetParent(enemy.transform, false);
            hitboxObj.transform.localPosition = new Vector3(0f, 0.4f, 0.5f);
        }
        else
        {
            hitboxObj = child.gameObject;
        }

        BoxCollider trigger = hitboxObj.GetComponent<BoxCollider>();
        if (trigger == null)
        {
            trigger = Undo.AddComponent<BoxCollider>(hitboxObj);
        }
        Undo.RecordObject(trigger, "Configure AttackHitbox");
        trigger.isTrigger = true;
        trigger.size = new Vector3(0.5f, 0.5f, 0.6f);
        trigger.enabled = false;

        EnemyAttackHitbox hitbox = hitboxObj.GetComponent<EnemyAttackHitbox>();
        if (hitbox == null)
        {
            hitbox = Undo.AddComponent<EnemyAttackHitbox>(hitboxObj);
        }
        return hitbox;
    }

    private static EnemyPatrol EnsureEnemyPatrol(GameObject enemy, CharacterHealth health, EnemyAttackHitbox attackHitbox)
    {
        EnemyPatrol patrol = enemy.GetComponent<EnemyPatrol>();
        if (patrol == null)
        {
            patrol = Undo.AddComponent<EnemyPatrol>(enemy);
        }

        Animator animator = enemy.GetComponent<Animator>();
        var so = new SerializedObject(patrol);
        TrySet(so, "animator", animator);
        TrySet(so, "enemyHealth", health);
        TrySet(so, "attackHitbox", attackHitbox);
        TrySetFloat(so, "detectRange", 5f);
        TrySetFloat(so, "loseRange", 7f);
        TrySetFloat(so, "attackRange", 1.25f);
        TrySetFloat(so, "patrolSpeed", 3f);
        TrySetFloat(so, "chaseSpeed", 6f);
        TrySetFloat(so, "animatorDampTime", 0.1f);
        TrySetFloat(so, "movingThreshold", 0.4f);
        TrySetFloat(so, "attackLockDuration", 0.9f);
        TrySetFloat(so, "attackCooldown", 1.2f);
        TrySetFloat(so, "attackDamage", 10f);
        TrySetLayerMask(so, "playerLayer", "Player");
        so.ApplyModifiedProperties();
        return patrol;
    }

    private static RagdollOnDeath EnsureRagdollOnDeath(GameObject enemy, CharacterHealth health)
    {
        RagdollOnDeath ragdoll = enemy.GetComponent<RagdollOnDeath>();
        if (ragdoll == null)
        {
            ragdoll = Undo.AddComponent<RagdollOnDeath>(enemy);
        }

        var so = new SerializedObject(ragdoll);
        TrySet(so, "characterHealth", health);
        TrySet(so, "animator", enemy.GetComponent<Animator>());
        TrySet(so, "navMeshAgent", enemy.GetComponent<NavMeshAgent>());
        TrySetBool(so, "useBoneRagdoll", false);
        TrySetBool(so, "disableAnimator", true);
        TrySetBool(so, "disableNavMeshAgent", true);
        TrySetBool(so, "disableOtherBehaviours", true);
        TrySetBool(so, "disableRootCollider", true);
        TrySetFloat(so, "deathImpulse", 1.5f);
        TrySetFloat(so, "deathTorque", 2.5f);
        so.ApplyModifiedProperties();
        return ragdoll;
    }

    private static EnemyAnimationEventRelay EnsureAnimationEventRelay(GameObject enemy, EnemyPatrol patrol)
    {
        Animator animator = enemy.GetComponent<Animator>();
        GameObject relayHost = animator != null ? animator.gameObject : enemy;
        EnemyAnimationEventRelay relay = relayHost.GetComponent<EnemyAnimationEventRelay>();
        if (relay == null)
        {
            relay = Undo.AddComponent<EnemyAnimationEventRelay>(relayHost);
        }
        var so = new SerializedObject(relay);
        TrySet(so, "owner", patrol);
        so.ApplyModifiedProperties();
        return relay;
    }

    private static void TrySet(SerializedObject so, string name, Object value)
    {
        var prop = so.FindProperty(name);
        if (prop != null) prop.objectReferenceValue = value;
    }

    private static void TrySetFloat(SerializedObject so, string name, float value)
    {
        var prop = so.FindProperty(name);
        if (prop != null) prop.floatValue = value;
    }

    private static void TrySetBool(SerializedObject so, string name, bool value)
    {
        var prop = so.FindProperty(name);
        if (prop != null) prop.boolValue = value;
    }

    private static void TrySetLayerMask(SerializedObject so, string name, string layerName)
    {
        var prop = so.FindProperty(name);
        if (prop == null) return;
        int layer = LayerMask.NameToLayer(layerName);
        prop.intValue = layer >= 0 ? (1 << layer) : 0;
    }
}
