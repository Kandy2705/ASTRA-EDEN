using UnityEngine;

/// <summary>
/// Quản lý việc gọi cổng teleport khi bấm nút UI "Recall"
/// </summary>
public class RecallPortalManager : MonoBehaviour
{
    [SerializeField] private GameObject portalPrefab;
    [SerializeField] private Transform portalSpawnPoint;
    [SerializeField] private float spawnDistance = 2.5f;
    [SerializeField] private float spawnHeightOffset = 1f;
    [SerializeField] private CharacterController playerController;
    [SerializeField] private float rotateYPrefab = 90f;

    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float raycastHeight = 20f;
    [SerializeField] private float raycastDistance = 100f;
    [SerializeField] private float groundOffset = 0.05f;
    [SerializeField] private bool includeDefaultLayerGround = true;
    [SerializeField, Min(0.1f)] private float maxGroundHeightDifference = 4f;
    [SerializeField, Range(0f, 1f)] private float minGroundNormalY = 0.5f;

    private GameObject currentPortal;

    private void Awake()
    {
        if (playerController == null)
        {
            playerController = FindObjectOfType<CharacterController>();
        }
    }

    /// <summary>
    /// Gọi cổng Recall khi bấm nút UI
    /// </summary>
    public void OnRecallButtonPressed()
    {
        if (currentPortal != null)
        {
            Destroy(currentPortal);
        }

        SpawnRecallPortal();
    }

    private void SpawnRecallPortal()
    {
        if (portalPrefab == null)
        {
            Debug.LogError("Portal Prefab not assigned!");
            return;
        }

        Transform spawnTransform = portalSpawnPoint != null ? portalSpawnPoint : transform;

        Vector3 spawnPos = spawnTransform.position + spawnTransform.forward * spawnDistance;

        if (TryFindGround(spawnPos, spawnTransform.position.y, out RaycastHit hit))
        {
            spawnPos = hit.point + Vector3.up * groundOffset;
        }
        else
        {
            Debug.LogWarning(
                "[RecallPortalManager] Không tìm thấy mặt sàn gần Player để spawn portal. " +
                "Dùng vị trí mặc định.");
            spawnPos.y = spawnTransform.position.y + spawnHeightOffset;
        }

        Quaternion portalRotation = Quaternion.Euler(
            0f,
            spawnTransform.eulerAngles.y + rotateYPrefab,
            0f
        );

        currentPortal = Instantiate(portalPrefab, spawnPos, portalRotation);

        RecallPortal portalScript = currentPortal.GetComponent<RecallPortal>();
        if (portalScript != null)
        {
            portalScript.Initialize(spawnTransform.forward);
        }
    }

    private bool TryFindGround(
        Vector3 desiredPosition,
        float referenceHeight,
        out RaycastHit bestHit)
    {
        int probeMask = groundMask.value;
        if (includeDefaultLayerGround)
        {
            probeMask |= 1 << LayerMask.NameToLayer("Default");
        }

        Vector3 rayStart = desiredPosition + Vector3.up * raycastHeight;
        RaycastHit[] hits = Physics.RaycastAll(
            rayStart,
            Vector3.down,
            raycastDistance,
            probeMask,
            QueryTriggerInteraction.Ignore);

        bestHit = default;
        float bestHeightDifference = float.PositiveInfinity;

        foreach (RaycastHit candidate in hits)
        {
            if (candidate.normal.y < minGroundNormalY)
            {
                continue;
            }

            float heightDifference = Mathf.Abs(candidate.point.y - referenceHeight);
            if (heightDifference > maxGroundHeightDifference ||
                heightDifference >= bestHeightDifference)
            {
                continue;
            }

            bestHit = candidate;
            bestHeightDifference = heightDifference;
        }

        return bestHeightDifference < float.PositiveInfinity;
    }
}
