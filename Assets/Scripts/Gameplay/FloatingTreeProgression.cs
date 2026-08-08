using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Tiến trình Floating Tree: trước khi hạ boss Ancient Forest cây ngủ (chỉ gợi ý);
/// sau khi hạ boss, tương tác sẽ kích hoạt phản ứng ma thuật và spawn Note #2
/// (ancient_note_floating_tree_02) đúng một lần. Trạng thái được lưu qua GameDataManager.
/// Gắn lên GameObject "Flying_Tree_Zone_2" trong scene (implement IWorldInteractable).
/// </summary>
[DisallowMultipleComponent]
public class FloatingTreeProgression : MonoBehaviour, IWorldInteractable
{
    [Header("Interaction")]
    [SerializeField, Min(0.5f)] private float interactionRange = 5f;
    [SerializeField] private string interactPrompt = "Examine the Ancient Tree [F]";

    [Header("Note 2 Spawn")]
    [Tooltip("Prefab AncientNotePickup cho Note #2. Nếu là prefab Note #1 sẽ tự chuyển sang Note 2 qua ConfigureNote2().")]
    [SerializeField] private AncientNotePickup note2Prefab;
    [Tooltip("Độ hở của gốc Note #2 so với mặt đất sau khi dò ground/NavMesh.")]
    [SerializeField, Range(0.02f, 0.3f)] private float note2GroundClearance = 0.08f;
    [Tooltip("Khoảng cách ngang dời Note ra ngoài bề mặt cây (tránh bị thân/gốc che khuất).")]
    [SerializeField, Min(0.5f)] private float note2SpawnOutwardDistance = 2.2f;

    [Header("Magical Reaction")]
    [Tooltip("Optional VFX phản ứng ma thuật khi cây thức dậy. Để trống sẽ tạo tia sáng + bụi sao runtime.")]
    [SerializeField] private GameObject reactionVfxPrefab;
    [SerializeField] private Vector3 reactionVfxLocalOffset = new(0f, 2.2f, 0f);
    [Tooltip("Optional audio khi cây thức dậy.")]
    [SerializeField] private AudioClip reactionSfx;
    [Tooltip("Optional audio khi cây còn ngủ (chưa hạ boss).")]
    [SerializeField] private AudioClip dormantSfx;
    [SerializeField] private Color reactionColor = new(0.58f, 0.24f, 1f, 1f);

    [Header("Dormant Hint")]
    [SerializeField] private string dormantHint =
        "The ancient tree lies dormant. A great guardian still stirs deep within the forest.";
    [SerializeField] private string dormantHintVietnamese =
        "Cái cây cổ thụ vẫn ngủ say. Một kẻ bảo vệ vẫn còn thức giấc sâu trong khu rừng.";

    private bool reactionPlayed;
    private Coroutine lightPulseRoutine;

    public float InteractionRange => interactionRange;

    public bool CanInteract(Transform interactor)
    {
        if (interactor == null || GameDataManager.Instance == null)
        {
            return false;
        }

        // Không đưa cây vào danh sách interactable trước khi hạ Boss 2.
        // Nhờ vậy InteractPromptUI cũng không hiện bảng [F] sớm.
        if (!GameDataManager.Instance.IsAncientForestBossDefeated)
        {
            return false;
        }

        if (GameDataManager.Instance.IsAncientNote2Collected)
        {
            return false;
        }

        if (GameDataManager.Instance.IsFloatingTreeSecondNoteSpawned)
        {
            return false;
        }

        return DistanceToTree(interactor.position) <= interactionRange;
    }

    public void Interact(Transform interactor)
    {
        if (!CanInteract(interactor) || GameDataManager.Instance == null)
        {
            return;
        }

        if (!GameDataManager.Instance.IsAncientForestBossDefeated)
        {
            ShowDormantHint();
            return;
        }

        SpawnNote2(interactor.position);
    }

    public string GetInteractPrompt()
    {
        if (GameDataManager.Instance == null ||
            !GameDataManager.Instance.IsAncientForestBossDefeated)
        {
            return string.Empty;
        }

        return interactPrompt;
    }

    /// <summary>Cây to và pivot có thể nằm trên tán cao — đo khoảng cách tới bề mặt collider gần nhất.</summary>
    private float DistanceToTree(Vector3 worldPoint)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        if (colliders == null || colliders.Length == 0)
        {
            return Vector3.Distance(transform.position, worldPoint);
        }

        float minDistance = float.MaxValue;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            float distance = Vector3.Distance(collider.ClosestPoint(worldPoint), worldPoint);
            if (distance < minDistance)
            {
                minDistance = distance;
            }
        }

        return minDistance;
    }

    private void ShowDormantHint()
    {
        if (dormantSfx != null)
        {
            AudioSource.PlayClipAtPoint(dormantSfx, transform.position);
        }

        Debug.Log($"[FloatingTree] {name}: {dormantHint}", this);
        Debug.Log($"[FloatingTree] {name}: {dormantHintVietnamese}", this);
    }

    private void SpawnNote2(Vector3 playerPosition)
    {
        if (reactionPlayed)
        {
            return;
        }

        reactionPlayed = true;

        // Đánh dấu trước khi spawn để không thể interact/lặp lại trong cùng khung hình.
        GameDataManager.Instance.MarkFloatingTreeSecondNoteSpawned();

        PlayMagicalReaction();

        if (note2Prefab == null)
        {
            Debug.LogWarning(
                $"[FloatingTree] {name}: note2Prefab chưa được gán trong Inspector — không spawn được Note #2.",
                this);
            return;
        }

        Vector3 spawnPosition = ResolveSpawnPosition(playerPosition);
        AncientNotePickup spawned = Instantiate(note2Prefab, spawnPosition, Quaternion.identity);
        spawned.ConfigureNote2();

        Debug.Log($"[FloatingTree] {name}: đã spawn Note #2 '{AncientNotePickup.Note2Id}' tại {spawnPosition}.", this);
    }

    private Vector3 ResolveSpawnPosition(Vector3 playerPosition)
    {
        Vector3 treePos = transform.position;

        // Tìm điểm bề mặt cây gần player nhất (thường là gốc/thân) để làm điểm gốc
        // rồi dời Note RA NGOÀI theo phương ngang — tránh bị thân/gốc cây che khuất.
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        Vector3 surfacePoint = treePos;
        float bestDistance = float.MaxValue;
        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                Vector3 closest = collider.ClosestPoint(playerPosition);
                float distance = Vector3.Distance(closest, playerPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    surfacePoint = closest;
                }
            }
        }

        // Phương ngang từ tâm cây qua điểm bề mặt gần player (hướng ra ngoài).
        Vector3 outward = surfacePoint - treePos;
        outward.y = 0f;
        if (outward.sqrMagnitude < 0.01f)
        {
            outward = playerPosition - treePos;
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.01f)
            {
                outward = transform.forward;
                outward.y = 0f;
            }
        }
        outward.Normalize();

        Vector3 spawn = surfacePoint + outward * note2SpawnOutwardDistance;
        spawn.y = ResolveGroundHeight(spawn, playerPosition) + note2GroundClearance;
        return spawn;
    }

    /// <summary>
    /// Đặt Note sát mặt đất thật thay vì cộng chiều cao từ pivot/collider của cây.
    /// Ưu tiên raycast mặt đất, fallback NavMesh rồi mới dùng độ cao Player.
    /// </summary>
    private float ResolveGroundHeight(Vector3 horizontalPosition, Vector3 playerPosition)
    {
        float rayStartY = Mathf.Max(horizontalPosition.y, playerPosition.y) + 4f;
        Vector3 rayOrigin = new(horizontalPosition.x, rayStartY, horizontalPosition.z);
        RaycastHit[] hits = Physics.RaycastAll(
            rayOrigin,
            Vector3.down,
            12f,
            ~0,
            QueryTriggerInteraction.Ignore);

        float bestGroundY = float.NegativeInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;
            if (hitTransform == null ||
                hitTransform == transform ||
                hitTransform.IsChildOf(transform) ||
                hitTransform.root.CompareTag("Player"))
            {
                continue;
            }

            if (hits[i].point.y > bestGroundY)
            {
                bestGroundY = hits[i].point.y;
            }
        }

        if (!float.IsNegativeInfinity(bestGroundY))
        {
            return bestGroundY;
        }

        Vector3 navCandidate = new(horizontalPosition.x, playerPosition.y, horizontalPosition.z);
        if (NavMesh.SamplePosition(navCandidate, out NavMeshHit navHit, 2.5f, NavMesh.AllAreas))
        {
            return navHit.position.y;
        }

        return playerPosition.y;
    }

    private void PlayMagicalReaction()
    {
        if (reactionVfxPrefab != null)
        {
            Instantiate(
                reactionVfxPrefab,
                transform.position + transform.TransformDirection(reactionVfxLocalOffset),
                Quaternion.identity);
        }
        else
        {
            CreateRuntimeReaction();
        }

        if (reactionSfx != null)
        {
            AudioSource.PlayClipAtPoint(reactionSfx, transform.position);
        }
    }

    private void CreateRuntimeReaction()
    {
        Transform sparkleRoot = new GameObject("FloatingTreeReaction").transform;
        sparkleRoot.SetParent(transform, false);
        sparkleRoot.localPosition = reactionVfxLocalOffset;

        ParticleSystem particles = sparkleRoot.gameObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.duration = 1.1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(reactionColor, Color.white);
        main.maxParticles = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 40),
            new ParticleSystem.Burst(0.45f, 18),
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.7f;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = CreateGlowMaterial(reactionColor, reactionColor * 4f);

        GameObject lightObject = new("FloatingTreeReactionLight", typeof(Light));
        lightObject.transform.SetParent(sparkleRoot, false);
        lightObject.transform.localPosition = Vector3.zero;
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Point;
        light.color = reactionColor;
        light.range = 7f;
        light.intensity = 0f;
        light.shadows = LightShadows.None;

        if (lightPulseRoutine != null)
        {
            StopCoroutine(lightPulseRoutine);
        }

        lightPulseRoutine = StartCoroutine(LightPulseRoutine(light, sparkleRoot.gameObject));
    }

    private IEnumerator LightPulseRoutine(Light light, GameObject cleanupTarget)
    {
        if (light == null)
        {
            yield break;
        }

        float elapsed = 0f;
        const float duration = 1.1f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float pulse = Mathf.Sin(t * Mathf.PI);
            light.intensity = 2.2f + pulse * 3.4f;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (light != null)
        {
            light.intensity = 0f;
        }

        if (cleanupTarget != null)
        {
            Destroy(cleanupTarget, 1.5f);
        }

        lightPulseRoutine = null;
    }

    private static Material CreateGlowMaterial(Color baseColor, Color emission)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new(shader);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", baseColor);
        if (material.HasProperty("_Color")) material.SetColor("_Color", baseColor);
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission);
        material.EnableKeyword("_EMISSION");
        return material;
    }
}
