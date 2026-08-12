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
        ResolvePlayerReferences();
    }

    /// <summary>
    /// Gọi cổng Recall khi bấm nút UI
    /// </summary>
    public bool OnRecallButtonPressed()
    {
        ResolvePlayerReferences();
        if (currentPortal != null)
        {
            Destroy(currentPortal);
        }

        return SpawnRecallPortal();
    }

    private bool SpawnRecallPortal()
    {
        if (portalPrefab == null)
        {
            Debug.LogError("[RecallPortalManager] Portal Prefab chưa được gắn.", this);
            return false;
        }

        Transform spawnTransform = portalSpawnPoint != null
            ? portalSpawnPoint
            : playerController != null
                ? playerController.transform
                : null;
        if (spawnTransform == null)
        {
            Debug.LogWarning("[RecallPortalManager] Không tìm thấy Player/PortalSpawnPoint.", this);
            return false;
        }

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

        Debug.Log($"[RecallPortalManager] Đã spawn cổng tại {spawnPos}.", currentPortal);
        return true;
    }

    private void ResolvePlayerReferences()
    {
        if (playerController == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerController = player.GetComponent<CharacterController>();
            }
        }

        if (portalSpawnPoint == null && playerController != null)
        {
            Transform[] children = playerController.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == "PortalSpawnPoint")
                {
                    portalSpawnPoint = children[i];
                    break;
                }
            }
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
