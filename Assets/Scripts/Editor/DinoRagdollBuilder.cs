using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class DinoRagdollBuilder
{
    private const float DefaultMass = 0.35f;
    private const float BodyMass = 1.2f;
    private const float JointSwingLimit = 25f;

    [MenuItem("ASTRA EDEN/Enemies/Build Dino Ragdoll On Selected")]
    public static void BuildSelected()
    {
        GameObject root = Selection.activeGameObject;
        if (root == null)
        {
            Debug.LogError("Select the dinosaur root object in the Hierarchy first.");
            return;
        }

        Undo.SetCurrentGroupName("Build Dino Ragdoll");
        int undoGroup = Undo.GetCurrentGroup();

        List<Transform> bones = FindRagdollBones(root.transform);
        if (bones.Count == 0)
        {
            Debug.LogError($"No useful dino bones found under {root.name}. Expand the model and check bone names.");
            return;
        }

        foreach (Transform bone in bones)
        {
            SetupBone(root.transform, bone);
        }

        SetupRuntimeController(root, bones);

        Undo.CollapseUndoOperations(undoGroup);
        EditorUtility.SetDirty(root);
        Debug.Log($"Built dino ragdoll on {root.name}. Bones: {bones.Count}. Test by killing the enemy, then tune collider sizes if needed.");
    }

    [MenuItem("ASTRA EDEN/Enemies/Remove Dino Bone Ragdoll From Selected")]
    public static void RemoveBoneRagdollFromSelected()
    {
        GameObject root = Selection.activeGameObject;
        if (root == null)
        {
            Debug.LogError("Select the dinosaur root object in the Hierarchy first.");
            return;
        }

        Undo.SetCurrentGroupName("Remove Dino Bone Ragdoll");
        int undoGroup = Undo.GetCurrentGroup();
        int removedCount = 0;

        foreach (CharacterJoint joint in root.GetComponentsInChildren<CharacterJoint>(true))
        {
            Undo.DestroyObjectImmediate(joint);
            removedCount++;
        }

        foreach (CapsuleCollider collider in root.GetComponentsInChildren<CapsuleCollider>(true))
        {
            if (collider.transform == root.transform)
            {
                continue;
            }

            Undo.DestroyObjectImmediate(collider);
            removedCount++;
        }

        foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
        {
            if (body.transform == root.transform)
            {
                continue;
            }

            Undo.DestroyObjectImmediate(body);
            removedCount++;
        }

        RagdollOnDeath ragdoll = root.GetComponent<RagdollOnDeath>();
        if (ragdoll != null)
        {
            SerializedObject serializedRagdoll = new SerializedObject(ragdoll);
            SerializedProperty useBoneRagdoll = serializedRagdoll.FindProperty("useBoneRagdoll");
            if (useBoneRagdoll != null)
            {
                useBoneRagdoll.boolValue = false;
            }

            serializedRagdoll.FindProperty("ragdollBodies").arraySize = 0;
            serializedRagdoll.FindProperty("ragdollColliders").arraySize = 0;
            serializedRagdoll.ApplyModifiedProperties();
            EditorUtility.SetDirty(ragdoll);
        }

        EnsureRootBoxCollider(root);

        Undo.CollapseUndoOperations(undoGroup);
        EditorUtility.SetDirty(root);
        Debug.Log($"Removed dino bone ragdoll components from {root.name}. Removed components: {removedCount}. Safe death physics remains on the root.");
    }

    private static List<Transform> FindRagdollBones(Transform root)
    {
        Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);
        List<Transform> bones = new List<Transform>();

        foreach (Transform child in allTransforms)
        {
            if (child == root || IsVisualOnly(child))
            {
                continue;
            }

            string name = child.name.ToLowerInvariant();
            if (IsLikelyDinoBone(name))
            {
                bones.Add(child);
            }
        }

        return bones;
    }

    private static bool IsLikelyDinoBone(string name)
    {
        return name.Contains("hip")
            || name.Contains("pelvis")
            || name.Contains("spine")
            || name.Contains("chest")
            || name.Contains("neck")
            || name.Contains("head")
            || name.Contains("jaw")
            || name.Contains("tail")
            || name.Contains("leg")
            || name.Contains("thigh")
            || name.Contains("knee")
            || name.Contains("shin")
            || name.Contains("foot")
            || name.Contains("toe")
            || name.Contains("arm")
            || name.Contains("forearm")
            || name.Contains("hand")
            || name.Contains("claw");
    }

    private static bool IsVisualOnly(Transform transform)
    {
        return transform.GetComponent<Renderer>() != null
            || transform.GetComponent<SkinnedMeshRenderer>() != null
            || transform.GetComponent<MeshFilter>() != null;
    }

    private static void SetupBone(Transform root, Transform bone)
    {
        Rigidbody body = bone.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = Undo.AddComponent<Rigidbody>(bone.gameObject);
        }

        body.mass = GetBoneMass(bone.name);
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        CapsuleCollider collider = bone.GetComponent<CapsuleCollider>();
        if (collider == null)
        {
            collider = Undo.AddComponent<CapsuleCollider>(bone.gameObject);
        }

        ConfigureCapsule(collider, bone);
        collider.enabled = false;

        Rigidbody parentBody = FindParentRigidbody(root, bone.parent);
        if (parentBody != null && bone.GetComponent<CharacterJoint>() == null)
        {
            CharacterJoint joint = Undo.AddComponent<CharacterJoint>(bone.gameObject);
            joint.connectedBody = parentBody;
            joint.enableProjection = true;
            joint.swing1Limit = CreateSoftJointLimit(JointSwingLimit);
            joint.swing2Limit = CreateSoftJointLimit(JointSwingLimit);
            joint.lowTwistLimit = CreateSoftJointLimit(-JointSwingLimit);
            joint.highTwistLimit = CreateSoftJointLimit(JointSwingLimit);
        }
    }

    private static void ConfigureCapsule(CapsuleCollider collider, Transform bone)
    {
        Vector3 childOffset = GetMainChildOffset(bone);
        int direction = GetLargestAxis(childOffset);
        float length = Mathf.Max(childOffset.magnitude, 0.12f);
        float radius = Mathf.Clamp(length * GetRadiusScale(bone.name), 0.025f, 0.22f);

        collider.direction = direction;
        collider.radius = radius;
        collider.height = Mathf.Max(length + radius * 2f, radius * 2.5f);
        collider.center = GetAxisCenter(childOffset, direction);
    }

    private static Vector3 GetMainChildOffset(Transform bone)
    {
        Transform bestChild = null;
        float bestDistance = 0f;

        for (int i = 0; i < bone.childCount; i++)
        {
            Transform child = bone.GetChild(i);
            if (IsVisualOnly(child))
            {
                continue;
            }

            float distance = child.localPosition.sqrMagnitude;
            if (distance > bestDistance)
            {
                bestDistance = distance;
                bestChild = child;
            }
        }

        if (bestChild != null)
        {
            return bestChild.localPosition;
        }

        return Vector3.forward * 0.16f;
    }

    private static int GetLargestAxis(Vector3 value)
    {
        Vector3 absolute = new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        if (absolute.x >= absolute.y && absolute.x >= absolute.z)
        {
            return 0;
        }

        if (absolute.y >= absolute.x && absolute.y >= absolute.z)
        {
            return 1;
        }

        return 2;
    }

    private static Vector3 GetAxisCenter(Vector3 childOffset, int direction)
    {
        Vector3 center = Vector3.zero;
        if (direction == 0)
        {
            center.x = childOffset.x * 0.5f;
        }
        else if (direction == 1)
        {
            center.y = childOffset.y * 0.5f;
        }
        else
        {
            center.z = childOffset.z * 0.5f;
        }

        return center;
    }

    private static float GetRadiusScale(string boneName)
    {
        string name = boneName.ToLowerInvariant();
        if (name.Contains("hip") || name.Contains("pelvis") || name.Contains("spine") || name.Contains("chest"))
        {
            return 0.45f;
        }

        if (name.Contains("head") || name.Contains("neck"))
        {
            return 0.35f;
        }

        if (name.Contains("tail"))
        {
            return 0.22f;
        }

        return 0.28f;
    }

    private static float GetBoneMass(string boneName)
    {
        string name = boneName.ToLowerInvariant();
        if (name.Contains("hip") || name.Contains("pelvis") || name.Contains("spine") || name.Contains("chest"))
        {
            return BodyMass;
        }

        if (name.Contains("head"))
        {
            return 0.55f;
        }

        if (name.Contains("tail"))
        {
            return 0.25f;
        }

        return DefaultMass;
    }

    private static Rigidbody FindParentRigidbody(Transform root, Transform start)
    {
        Transform current = start;
        while (current != null && current != root)
        {
            if (current.TryGetComponent(out Rigidbody body))
            {
                return body;
            }

            current = current.parent;
        }

        return null;
    }

    private static SoftJointLimit CreateSoftJointLimit(float limit)
    {
        SoftJointLimit softJointLimit = new SoftJointLimit();
        softJointLimit.limit = limit;
        return softJointLimit;
    }

    private static void SetupRuntimeController(GameObject root, List<Transform> bones)
    {
        CharacterHealth health = root.GetComponent<CharacterHealth>();
        if (health == null)
        {
            health = Undo.AddComponent<CharacterHealth>(root);
        }

        RagdollOnDeath ragdoll = root.GetComponent<RagdollOnDeath>();
        if (ragdoll == null)
        {
            ragdoll = Undo.AddComponent<RagdollOnDeath>(root);
        }

        SerializedObject serializedRagdoll = new SerializedObject(ragdoll);
        serializedRagdoll.FindProperty("characterHealth").objectReferenceValue = health;
        serializedRagdoll.FindProperty("animator").objectReferenceValue = root.GetComponentInChildren<Animator>();
        serializedRagdoll.FindProperty("navMeshAgent").objectReferenceValue = root.GetComponent<UnityEngine.AI.NavMeshAgent>();
        FillObjectArray(serializedRagdoll.FindProperty("ragdollBodies"), CollectComponents<Rigidbody>(bones));
        FillObjectArray(serializedRagdoll.FindProperty("ragdollColliders"), CollectComponents<Collider>(bones));
        serializedRagdoll.ApplyModifiedProperties();
    }

    private static void EnsureRootBoxCollider(GameObject root)
    {
        if (root.GetComponent<Collider>() != null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        BoxCollider boxCollider = Undo.AddComponent<BoxCollider>(root);

        if (renderers.Length == 0)
        {
            boxCollider.size = Vector3.one;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        boxCollider.center = root.transform.InverseTransformPoint(bounds.center);
        boxCollider.size = new Vector3(
            Mathf.Max(bounds.size.x / Mathf.Max(root.transform.lossyScale.x, 0.001f), 0.1f),
            Mathf.Max(bounds.size.y / Mathf.Max(root.transform.lossyScale.y, 0.001f), 0.1f),
            Mathf.Max(bounds.size.z / Mathf.Max(root.transform.lossyScale.z, 0.001f), 0.1f)
        );
    }

    private static List<T> CollectComponents<T>(List<Transform> bones) where T : Component
    {
        List<T> components = new List<T>();
        foreach (Transform bone in bones)
        {
            if (bone != null && bone.TryGetComponent(out T component))
            {
                components.Add(component);
            }
        }

        return components;
    }

    private static void FillObjectArray<T>(SerializedProperty property, List<T> values) where T : Object
    {
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
