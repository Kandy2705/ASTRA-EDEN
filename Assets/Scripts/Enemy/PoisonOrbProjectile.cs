using UnityEngine;

[DisallowMultipleComponent]
public sealed class PoisonOrbProjectile : MonoBehaviour
{
    Vector3 travelDirection;
    Vector3 spawnPosition;
    float damage;
    float speed;
    float maxTravelDistance;
    float knockbackDistance;
    float knockbackDuration;
    float verticalLift;
    Transform owner;
    Rigidbody body;
    SphereCollider hitCollider;
    bool initialized;
    bool resolved;

    void Awake()
    {
        hitCollider = GetComponent<SphereCollider>();
        if (hitCollider == null)
        {
            hitCollider = gameObject.AddComponent<SphereCollider>();
        }

        hitCollider.isTrigger = true;

        body = GetComponent<Rigidbody>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody>();
        }

        body.useGravity = false;
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        // ContinuousSpeculative có thể báo va chạm "ảo" ở phía trước quỹ đạo,
        // khiến Player mất máu trước khi nhìn thấy orb thật sự chạm.
        body.collisionDetectionMode = CollisionDetectionMode.Discrete;
    }

    public void Initialize(
        Vector3 direction,
        float hitDamage,
        float flightSpeed,
        float travelDistance,
        float collisionRadius,
        Transform projectileOwner,
        float pushDistance,
        float pushDuration,
        float pushVerticalLift)
    {
        travelDirection = direction.normalized;
        spawnPosition = transform.position;
        damage = Mathf.Max(0f, hitDamage);
        speed = Mathf.Max(0.1f, flightSpeed);
        maxTravelDistance = Mathf.Max(0.1f, travelDistance);
        owner = projectileOwner;
        knockbackDistance = Mathf.Max(0f, pushDistance);
        knockbackDuration = Mathf.Max(0.01f, pushDuration);
        verticalLift = Mathf.Max(0f, pushVerticalLift);
        hitCollider.radius = Mathf.Max(0.05f, collisionRadius);
        initialized = true;
    }

    void FixedUpdate()
    {
        if (!initialized || resolved)
        {
            return;
        }

        Vector3 nextPosition =
            body.position + travelDirection * (speed * Time.fixedDeltaTime);
        body.MovePosition(nextPosition);

        if ((nextPosition - spawnPosition).sqrMagnitude >=
            maxTravelDistance * maxTravelDistance)
        {
            resolved = true;
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!initialized || resolved || other == null)
        {
            return;
        }

        Transform otherTransform = other.transform;
        if (owner != null &&
            (otherTransform == owner ||
             otherTransform.IsChildOf(owner) ||
             owner.IsChildOf(otherTransform)))
        {
            return;
        }

        CharacterHealth health = other.GetComponentInParent<CharacterHealth>();
        if (health == null ||
            health.IsDead ||
            (!health.CompareTag("Player") &&
             !health.transform.root.CompareTag("Player")))
        {
            return;
        }

        // Prefab Player có nhiều collider ragdoll trên tay/chân. Chỉ collider
        // điều khiển thân Player mới được tính là orb chạm mục tiêu.
        CharacterController playerCollider =
            health.GetComponent<CharacterController>();
        if (playerCollider != null && other != playerCollider)
        {
            return;
        }

        resolved = true;
        health.TakeDamage(damage, triggerHitReaction: true);

        PlayerKnockbackReceiver knockback =
            health.GetComponentInParent<PlayerKnockbackReceiver>();
        if (knockback != null)
        {
            knockback.ApplyKnockback(
                travelDirection,
                knockbackDistance,
                knockbackDuration,
                verticalLift);
        }

        Destroy(gameObject);
    }
}
