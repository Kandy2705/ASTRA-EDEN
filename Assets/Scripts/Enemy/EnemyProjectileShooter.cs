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

    [Header("Player Knockback")]
    [SerializeField, Min(0f)] private float knockbackDistance = 4f;
    [SerializeField, Min(0.01f)] private float knockbackDuration = 0.22f;
    [SerializeField, Min(0f)] private float verticalLift = 0.25f;

    public bool CanFire => projectilePrefab != null;

    public bool Fire(float damage)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning($"[EnemyProjectileShooter:{name}] Projectile Prefab chưa được gán.", this);
            return false;
        }

        Vector3 direction = useNegativeForward ? -transform.forward : transform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        direction.Normalize();

        Vector3 position = spawnPoint != null
            ? spawnPoint.position
            : transform.TransformPoint(localSpawnOffset);
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
