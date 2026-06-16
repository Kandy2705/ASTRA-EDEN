using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class RagdollOnDeath : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterHealth characterHealth;
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent navMeshAgent;

    [Header("Ragdoll Parts")]
    [SerializeField] private bool useBoneRagdoll;
    [SerializeField] private Rigidbody[] ragdollBodies;
    [SerializeField] private Collider[] ragdollColliders;

    [Header("Death Behaviour")]
    [SerializeField] private bool disableAnimator = true;
    [SerializeField] private bool disableNavMeshAgent = true;
    [SerializeField] private bool disableOtherBehaviours = true;
    [SerializeField] private bool disableRootCollider = true;
    [SerializeField] private float deathImpulse = 1.5f;
    [SerializeField] private float deathTorque = 2.5f;

    [Tooltip("Bật khi có script khác (vd EnemyPatrol) điều phối việc chạy anim Die rồi mới ragdoll. Khi true, RagdollOnDeath KHÔNG tự bật ragdoll lúc Died.")]
    [SerializeField] private bool controlledExternally;

    [Tooltip("Áp impulse + torque vào root Rigidbody khi enable ragdoll (chỉ áp dụng khi useBoneRagdoll = false). Tắt để enemy giữ pose anim Die, không bị hất khi chuyển giao sang dissolve.")]
    [SerializeField] private bool applyRootDeathImpulse = false;

    private bool isRagdollActive;
    private bool deathPhysicsStarted;
    private bool hasWarnedMissingParts;
    private Collider rootCollider;
    private Rigidbody rootRigidbody;

    private void Awake()
    {
        CacheReferences();
        SetRagdollActive(false);
    }

    private void OnEnable()
    {
        CacheReferences();

        if (characterHealth != null)
        {
            characterHealth.Died -= HandleDied;
            characterHealth.Died += HandleDied;
        }
    }

    private void OnDisable()
    {
        if (characterHealth != null)
        {
            characterHealth.Died -= HandleDied;
        }
    }

    public void EnableRagdoll()
    {
        if (isRagdollActive)
        {
            return;
        }

        isRagdollActive = true;
        CacheReferences();

        if (useBoneRagdoll && (ragdollBodies == null || ragdollBodies.Length == 0))
        {
            WarnMissingParts();
        }

        if (disableAnimator && animator != null)
        {
            animator.enabled = false;
        }

        if (disableNavMeshAgent && navMeshAgent != null)
        {
            navMeshAgent.enabled = false;
        }

        if (useBoneRagdoll && disableRootCollider && rootCollider != null)
        {
            rootCollider.enabled = false;
        }

        if (useBoneRagdoll && rootRigidbody != null)
        {
            rootRigidbody.isKinematic = true;
        }

        if (disableOtherBehaviours)
        {
            DisableSiblingBehaviours();
        }

        if (useBoneRagdoll)
        {
            SetRagdollActive(true);
            ApplyDeathImpulse();
        }
        else if (!deathPhysicsStarted)
        {
            EnableRootDeathPhysics();
        }
    }

    /// <summary>
    /// Bật physics root sớm (gravity + collider) để enemy đứng yên trên đất trong khi anim Die chạy.
    /// Gọi trước khi trigger anim Die. Không áp impulse, không disable animator/agent.
    /// Chỉ có tác dụng khi useBoneRagdoll = false.
    /// </summary>
    public void BeginDeathPhysics()
    {
        if (deathPhysicsStarted || isRagdollActive) return;
        deathPhysicsStarted = true;
        CacheReferences();

        if (useBoneRagdoll) return;

        if (rootCollider != null)
        {
            rootCollider.enabled = true;
        }

        if (rootRigidbody == null)
        {
            rootRigidbody = gameObject.AddComponent<Rigidbody>();
        }

        rootRigidbody.isKinematic = false;
        rootRigidbody.useGravity = true;
        rootRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        rootRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        rootRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rootRigidbody.linearVelocity = Vector3.zero;
        rootRigidbody.angularVelocity = Vector3.zero;
    }

    private void CacheReferences()
    {
        if (characterHealth == null)
        {
            characterHealth = GetComponent<CharacterHealth>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (navMeshAgent == null)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
        }

        if (rootCollider == null)
        {
            rootCollider = GetComponent<Collider>();
        }

        if (rootCollider == null)
        {
            rootCollider = CreateSafeRootCollider();
        }

        if (rootRigidbody == null)
        {
            rootRigidbody = GetComponent<Rigidbody>();
        }

        if (ragdollBodies == null || ragdollBodies.Length == 0)
        {
            ragdollBodies = FindRagdollBodies();
        }

        if (ragdollColliders == null || ragdollColliders.Length == 0)
        {
            ragdollColliders = FindRagdollColliders();
        }
    }

    private void SetRagdollActive(bool active)
    {
        foreach (Rigidbody body in ragdollBodies)
        {
            if (body == null || body.transform == transform)
            {
                continue;
            }

            body.isKinematic = !active;
            body.useGravity = active;
        }

        foreach (Collider ragdollCollider in ragdollColliders)
        {
            if (ragdollCollider == null || ragdollCollider.transform == transform)
            {
                continue;
            }

            ragdollCollider.enabled = active;
        }
    }

    private void ApplyDeathImpulse()
    {
        if (deathImpulse <= 0f)
        {
            return;
        }

        Vector3 impulse = (-transform.forward + Vector3.up * 0.35f).normalized * deathImpulse;
        foreach (Rigidbody body in ragdollBodies)
        {
            if (body == null || body.transform == transform)
            {
                continue;
            }

            body.AddForce(impulse, ForceMode.Impulse);
        }
    }

    private void EnableRootDeathPhysics()
    {
        if (rootCollider != null)
        {
            rootCollider.enabled = true;
        }

        if (rootRigidbody == null)
        {
            rootRigidbody = gameObject.AddComponent<Rigidbody>();
        }

        rootRigidbody.isKinematic = false;
        rootRigidbody.useGravity = true;
        rootRigidbody.constraints = applyRootDeathImpulse ? RigidbodyConstraints.None : RigidbodyConstraints.FreezeRotation;
        rootRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        rootRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        if (!applyRootDeathImpulse) return;

        Vector3 impulse = (-transform.forward + Vector3.up * 0.25f).normalized * deathImpulse;
        rootRigidbody.AddForce(impulse, ForceMode.Impulse);
        rootRigidbody.AddTorque(transform.right * deathTorque, ForceMode.Impulse);
    }

    private void DisableSiblingBehaviours()
    {
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null || behaviour == this || behaviour is CharacterHealth)
            {
                continue;
            }

            behaviour.enabled = false;
        }
    }

    private void HandleDied(CharacterHealth deadHealth)
    {
        if (controlledExternally) return;
        EnableRagdoll();
    }

    public void SetControlledExternally(bool value)
    {
        controlledExternally = value;
    }

    private void WarnMissingParts()
    {
        if (hasWarnedMissingParts)
        {
            return;
        }

        hasWarnedMissingParts = true;
        Debug.LogWarning($"{name} has RagdollOnDeath, but no ragdoll Rigidbody parts were found. Select the dinosaur root and run ASTRA EDEN > Enemies > Build Dino Ragdoll On Selected.");
    }

    private Rigidbody[] FindRagdollBodies()
    {
        Rigidbody[] bodies = GetComponentsInChildren<Rigidbody>(true);
        List<Rigidbody> result = new List<Rigidbody>();

        foreach (Rigidbody body in bodies)
        {
            if (body != null && body.transform != transform)
            {
                result.Add(body);
            }
        }

        return result.ToArray();
    }

    private Collider[] FindRagdollColliders()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        List<Collider> result = new List<Collider>();

        foreach (Collider ragdollCollider in colliders)
        {
            if (ragdollCollider == null || ragdollCollider.transform == transform)
            {
                continue;
            }

            if (ragdollCollider.GetComponent<Rigidbody>() != null)
            {
                result.Add(ragdollCollider);
            }
        }

        return result.ToArray();
    }

    private Collider CreateSafeRootCollider()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return gameObject.AddComponent<BoxCollider>();
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
        boxCollider.center = transform.InverseTransformPoint(bounds.center);
        boxCollider.size = new Vector3(
            Mathf.Max(bounds.size.x / Mathf.Max(transform.lossyScale.x, 0.001f), 0.1f),
            Mathf.Max(bounds.size.y / Mathf.Max(transform.lossyScale.y, 0.001f), 0.1f),
            Mathf.Max(bounds.size.z / Mathf.Max(transform.lossyScale.z, 0.001f), 0.1f)
        );

        return boxCollider;
    }
}
