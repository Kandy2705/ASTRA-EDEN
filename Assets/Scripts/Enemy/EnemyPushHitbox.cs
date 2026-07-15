using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyPushHitbox : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Collider hitboxCollider;
    [SerializeField] private Transform directionSource;

    [Header("Push Settings")]
    [SerializeField] private float pushDistance = 4.2f;
    [SerializeField] private float pushDuration = 0.18f;
    [SerializeField] private float verticalLift = 0.15f;

    readonly HashSet<PlayerKnockbackReceiver> hitTargets = new();

    void Awake()
    {
        if (hitboxCollider == null)
        {
            hitboxCollider = GetComponent<Collider>();
        }

        if (directionSource == null)
        {
            directionSource = transform.root;
        }

        EnsureTriggerPhysics();

        if (hitboxCollider != null)
        {
            hitboxCollider.isTrigger = true;
            hitboxCollider.enabled = false;
        }
    }

    void EnsureTriggerPhysics()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody>();
        }

        body.isKinematic = true;
        body.useGravity = false;
    }

    /// <summary>Opens the push hitbox during the active frames of the tackle animation.</summary>
    public void OpenHitbox()
    {
        hitTargets.Clear();

        if (hitboxCollider != null)
        {
            hitboxCollider.enabled = true;
        }
    }

    /// <summary>Closes the push hitbox after the active frames end.</summary>
    public void CloseHitbox()
    {
        if (hitboxCollider != null)
        {
            hitboxCollider.enabled = false;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        PlayerKnockbackReceiver knockbackReceiver = other.GetComponentInParent<PlayerKnockbackReceiver>();
        if (knockbackReceiver == null)
        {
            return;
        }

        if (!hitTargets.Add(knockbackReceiver))
        {
            return;
        }

        Vector3 pushDirection = knockbackReceiver.transform.position - directionSource.position;
        knockbackReceiver.ApplyKnockback(pushDirection, pushDistance, pushDuration, verticalLift);
    }
}