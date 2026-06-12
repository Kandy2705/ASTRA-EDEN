using UnityEngine;
using System.Collections;

public class PlayerVFXController : MonoBehaviour
{
    [SerializeField] private Transform playerRoot;

    [SerializeField] private GameObject slashFireVFXPrefab;
    [SerializeField] private GameObject multipleSlashesVFXPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private float heightOffset = 4f;
    [SerializeField] private bool destroyAfterFinish = true;
    [SerializeField] private float fallbackDestroyTime = 3f;

    [Header("VFX Direction Fix")]
    [SerializeField] private float slashFireYawOffset = 0f;
    [SerializeField] private float multipleSlashesYawOffset = 0f;

    [Header("Scale Animation")]
    [SerializeField] private float startScale = 0.1f;
    [SerializeField] private float targetScale = 1.5f;

    [Header("VFX Speed")]
    [SerializeField] private float simulationSpeed = 0.5f;

    private void Awake()
    {
        if (playerRoot == null)
        {
            playerRoot = transform;
        }
    }

    public void SpawnSlashFireVFX()
    {
        SpawnVFX(slashFireVFXPrefab, slashFireYawOffset, false);
    }

    // Dùng hàm này nếu VFX bị ngược, nó sẽ xoay X thêm 180 độ
    public void SpawnSlashFireVFX_X180()
    {
        SpawnVFX(slashFireVFXPrefab, slashFireYawOffset, true);
    }

    public void SpawnMultipleSlashesVFX()
    {
        SpawnVFX(multipleSlashesVFXPrefab, multipleSlashesYawOffset, false);
    }

    public void SpawnMultipleSlashesVFX_X180()
    {
        SpawnVFX(multipleSlashesVFXPrefab, multipleSlashesYawOffset, true);
    }

    private void SpawnVFX(GameObject prefab, float yawOffset, bool rotateX180)
    {
        if (prefab == null)
        {
            Debug.LogWarning("VFX Prefab is not assigned!");
            return;
        }

        Vector3 spawnPosition = playerRoot.position + Vector3.up * heightOffset;

        Quaternion spawnRotation = Quaternion.Euler(
            0f,
            playerRoot.eulerAngles.y + yawOffset,
            0f
        );

        // Sửa hướng ngược bằng cách xoay LOCAL X thêm 180 độ
        if (rotateX180)
        {
            spawnRotation = spawnRotation * Quaternion.Euler(180f, 0f, 0f);
        }

        GameObject instance = Instantiate(prefab, spawnPosition, spawnRotation);
        instance.SetActive(true);

        instance.transform.localScale = Vector3.one * startScale;

        SetupAndPlayParticles(instance);

    }

    private void SetupAndPlayParticles(GameObject vfxObject)
    {
        ParticleSystem[] particleSystems = vfxObject.GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem ps in particleSystems)
        {
            ps.gameObject.SetActive(true);

            ParticleSystem.MainModule main = ps.main;
            main.simulationSpeed = simulationSpeed;

            ps.Clear(true);
            ps.Play(true);
        }
    }

    private void StopAllParticles(GameObject vfxObject)
    {
        if (vfxObject == null) return;

        ParticleSystem[] particleSystems = vfxObject.GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem ps in particleSystems)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private float GetParticleDuration(GameObject vfxObject)
    {
        ParticleSystem[] particleSystems = vfxObject.GetComponentsInChildren<ParticleSystem>(true);

        float maxDuration = 0f;

        foreach (ParticleSystem ps in particleSystems)
        {
            ParticleSystem.MainModule main = ps.main;

            float speed = Mathf.Max(main.simulationSpeed, 0.01f);
            float totalTime = (main.duration + main.startLifetime.constantMax) / speed;

            if (totalTime > maxDuration)
            {
                maxDuration = totalTime;
            }
        }

        return maxDuration > 0f ? maxDuration : fallbackDestroyTime;
    }
}