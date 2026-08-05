using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyProjectileShooter : MonoBehaviour
{
    [Header("Projectile")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Vector3 localSpawnOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField, Min(0f)] private float spawnForwardOffset = 1.1f;
    [SerializeField] private bool useNegativeForward;

    [Header("Flight")]
    [SerializeField, Min(0.1f)] private float projectileSpeed = 10f;
    [SerializeField, Min(0.1f)] private float maxTravelDistance = 18f;
    [SerializeField, Min(0.05f)] private float projectileRadius = 0.18f;

    [Header("Target Aiming")]
    [Tooltip("Độ cao ngắm vào player khi không có CharacterController (fallback).")]
    [SerializeField, Min(0f)] private float targetHeightOffset = 0.9f;

    [Header("Player Knockback")]
    [SerializeField, Min(0f)] private float knockbackDistance = 4f;
    [SerializeField, Min(0.01f)] private float knockbackDuration = 0.22f;
    [SerializeField, Min(0f)] private float verticalLift = 0.25f;

    public bool CanFire => projectilePrefab != null;

    /// <summary>Giữ nguyên API cũ — bắn theo hướng forward, không ngắm target.</summary>
    public bool Fire(float damage)
    {
        return Fire(damage, null);
    }

    public bool Fire(float damage, Transform target)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning($"[EnemyProjectileShooter:{name}] Projectile Prefab chưa được gán.", this);
            return false;
        }

        Vector3 fallbackForward =
            useNegativeForward ? -transform.forward : transform.forward;

        Vector3 position = spawnPoint != null
            ? spawnPoint.position
            : transform.TransformPoint(localSpawnOffset);

        Vector3 aimPoint;

        if (target != null)
        {
            CharacterController characterController =
                target.GetComponent<CharacterController>();
            if (characterController == null)
            {
                characterController =
                    target.GetComponentInParent<CharacterController>();
            }

            if (characterController != null)
            {
                aimPoint = characterController.bounds.center;
            }
            else
            {
                aimPoint = target.position + Vector3.up * targetHeightOffset;
            }
        }
        else
        {
            aimPoint = position + fallbackForward * maxTravelDistance;
        }

        Vector3 direction = aimPoint - position;
        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = fallbackForward;
        }

        direction.Normalize();

        // Đẩy điểm spawn ra trước theo đúng hướng bắn.
        position += direction * spawnForwardOffset;

        GameObject projectileObject = Instantiate(
            projectilePrefab,
            position,
            Quaternion.LookRotation(direction));

        PoisonOrbProjectile projectile =
            projectileObject.GetComponent<PoisonOrbProjectile>();
        if (projectile == null)
        {
            projectile = projectileObject.AddComponent<PoisonOrbProjectile>();
        }

        projectile.Initialize(
            direction,
            damage,
            projectileSpeed,
            maxTravelDistance,
            projectileRadius,
            transform,
            knockbackDistance,
            knockbackDuration,
            verticalLift);
        return true;
    }
}
