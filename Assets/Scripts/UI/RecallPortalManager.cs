using UnityEngine;
using System.Collections;

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
        spawnPos.y = spawnTransform.position.y + spawnHeightOffset;

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
}
