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

    [Header("Damage")]
    [Tooltip("Damage thô = ATK runtime của Enemy × hệ số này, sau đó qua DEF Player.")]
    [SerializeField, Min(0f)] private float ownerAttackDamageMultiplier = 1.5f;
    [SerializeField, Min(0f)] private float flatDamageBonus;

    readonly HashSet<PlayerKnockbackReceiver> hitTargets = new();
    bool openedForCurrentAction;

    public bool IsOpen => hitboxCollider != null && hitboxCollider.enabled;

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

    /// <summary>Chuẩn bị một lần Tackle mới; mỗi lần chỉ được mở/gây hit một lượt.</summary>
    public void ArmHitbox()
    {
        openedForCurrentAction = false;
        hitTargets.Clear();
        CloseHitbox();
    }

    /// <summary>Opens the push hitbox during the active frames of the tackle animation.</summary>
    public void OpenHitbox()
    {
        if (openedForCurrentAction)
        {
            return;
        }

        openedForCurrentAction = true;

        if (hitboxCollider != null)
        {
            hitboxCollider.enabled = true;
            Physics.SyncTransforms();

            // Nếu Player đã đứng sẵn trong vùng lúc hitbox mở thì xử lý ngay,
            // không phụ thuộc OnTriggerEnter có đến trong cùng physics step hay không.
            Bounds bounds = hitboxCollider.bounds;
            Collider[] overlaps = Physics.OverlapBox(
                bounds.center,
                bounds.extents,
                Quaternion.identity,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlaps.Length; i++)
            {
                TryHit(overlaps[i]);
            }
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
        TryHit(other);
    }

    void TryHit(Collider other)
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

        CharacterHealth playerHealth = knockbackReceiver.GetComponent<CharacterHealth>() ??
                                       knockbackReceiver.GetComponentInParent<CharacterHealth>();
        float damage = ResolveDamage();
        if (playerHealth != null && damage > 0f)
        {
            playerHealth.TakeDamage(damage);
        }

        Vector3 pushDirection = knockbackReceiver.transform.position - directionSource.position;
        knockbackReceiver.ApplyKnockback(pushDirection, pushDistance, pushDuration, verticalLift);
    }

    float ResolveDamage()
    {
        CharacterHealth ownerHealth = directionSource != null
            ? directionSource.GetComponent<CharacterHealth>() ??
              directionSource.GetComponentInParent<CharacterHealth>()
            : GetComponentInParent<CharacterHealth>();
        float attack = ownerHealth != null && ownerHealth.RuntimeStats != null
            ? ownerHealth.RuntimeStats.attack
            : 0f;
        return Mathf.Max(0f, attack * ownerAttackDamageMultiplier + flatDamageBonus);
    }
}
