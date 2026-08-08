using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

/// <summary>
/// Gắn lên enemy mini-boss sau khi spawn. Đăng ký Boss HUD + camera + zone objective.
/// Full boss có thể khóa Player và chính nó trong một bán kính chiến đấu,
/// dùng trực tiếp mặt đất/NavMesh đã đặt trong scene.
/// </summary>
[DefaultExecutionOrder(500)]
[DisallowMultipleComponent]
public class MiniBossMarker : MonoBehaviour
{
    [SerializeField] private string bossDisplayName = "Alpha Raptor";
    [SerializeField] private CharacterHealth health;
    [Tooltip("Tắt mặc định — boss camera khóa xoay/zoom của player. Chỉ bật HUD.")]
    [SerializeField] private bool registerCamera = false;
    [Tooltip("Chỉ bật boss camera/HUD khi player vào trong khoảng này — tránh khóa camera từ đầu map.")]
    [SerializeField, Min(1f)] private float engageDistance = 18f;
    [Tooltip("Tắt boss camera khi player ra xa hơn khoảng này.")]
    [SerializeField, Min(1f)] private float disengageDistance = 24f;

    [Header("Locked Combat Arena")]
    [Tooltip("Khóa Player + boss quanh đúng vị trí boss đã đặt trong scene. Không tạo platform/trụ.")]
    [SerializeField] private bool useLockedCombatArena;
    [SerializeField, Min(6f)] private float arenaRadius = 14f;
    [SerializeField, Min(2f)] private float arenaEngageDistance = 15.5f;
    [Tooltip("Player phải ở cùng tầng với tâm arena; tránh đứng bên dưới nhưng trùng X/Z vẫn kích hoạt boss.")]
    [SerializeField, Min(0.5f)] private float arenaMaxVerticalDifference = 2.5f;

    [Header("Arena Barrier Visual")]
    [Tooltip("Hiện tường năng lượng để Player nhận biết vùng đấu đang bị khóa.")]
    [SerializeField] private bool showArenaBarrier = true;
    [SerializeField, Min(1f)] private float arenaBarrierHeight = 4.5f;
    [SerializeField, Range(16, 128)] private int arenaBarrierSegments = 64;
    [SerializeField] private Color arenaBarrierColor = new(0.2f, 0.82f, 1f, 0.22f);
    [SerializeField, Range(0f, 0.2f)] private float arenaBarrierPulseAmount = 0.055f;
    [SerializeField, Min(0f)] private float arenaBarrierPulseSpeed = 2f;

    [Header("Boss Music")]
    [Tooltip("Nhạc loop được crossfade vào khi vùng boss bắt đầu khóa.")]
    [SerializeField] private AudioClip bossMusic;
    [SerializeField, Range(0f, 1f)] private float bossMusicVolume = 1f;
    [SerializeField, Min(0f)] private float bossMusicFadeIn = 1.2f;
    [SerializeField, Min(0f)] private float bossMusicFadeOut = 1.5f;

    [Header("Post-Boss Discovery")]
    [Tooltip("Ancient Note rơi cạnh xác boss. Để trống nếu boss này không mở tuyến Floating Tree.")]
    [SerializeField] private AncientNotePickup ancientNotePrefab;
    [SerializeField] private Vector3 ancientNoteDropOffset = new(0f, 0.3f, 2.2f);

    [Header("Post-Boss Rewards")]
    [Tooltip("Cấu hình phần thưởng chắc chắn (bình máu + đánh dấu tiến trình). Đặt trên cùng GameObject boss.")]
    [SerializeField] private BossDeathRewardConfig bossDeathRewardConfig;

    bool hudRegistered;
    bool cameraRegistered;
    bool arenaLocked;
    bool arenaFinished;
    Transform player;
    CharacterController playerCharacterController;
    NavMeshAgent bossAgent;
    Vector3 arenaCenter;
    GameObject arenaBarrier;
    Mesh arenaBarrierMesh;
    Material arenaBarrierMaterial;
    float arenaBarrierFade;
    bool bossMusicPlaying;
    bool ancientNoteSpawned;

    void Awake()
    {
        if (health == null)
        {
            health = GetComponent<CharacterHealth>();
        }

        bossAgent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        CachePlayer();

        // Dùng đúng vị trí phẳng người thiết kế đã kéo trong scene.
        // Không tạo platform và không dời boss sang tọa độ khác.
        arenaCenter = transform.position;
    }

    void Update()
    {
        if (health == null || health.IsDead)
        {
            return;
        }

        if (player == null)
        {
            CachePlayer();
            return;
        }

        TickCombatArena();
        UpdateArenaBarrierVisual();

        float distance = Vector3.Distance(transform.position, player.position);
        // Boss có arena khóa chỉ hiện HUD/camera sau khi Player thật sự bước vào
        // đúng tầng của arena. Không cho khoảng cách gần xuyên sàn kích hoạt UI.
        bool canPresentBoss = !useLockedCombatArena || arenaLocked;
        if (canPresentBoss && distance <= engageDistance)
        {
            RegisterHud();
            if (registerCamera)
            {
                RegisterCamera();
            }
        }
        else if (cameraRegistered && distance > disengageDistance)
        {
            UnregisterCamera();
        }
    }

    void LateUpdate()
    {
        if (!useLockedCombatArena || !arenaLocked || arenaFinished)
        {
            return;
        }

        KeepPlayerInsideArena();
        KeepBossInsideArena();
    }

    void OnEnable()
    {
        if (health != null)
        {
            health.Died += HandleDied;
        }
    }

    void OnDisable()
    {
        if (health != null)
        {
            health.Died -= HandleDied;
        }

        CleanupArenaBarrier();
        StopBossMusic();
    }

    public void Configure(string displayName, CharacterHealth targetHealth)
    {
        bossDisplayName = displayName;
        health = targetHealth;
    }

    public void ConfigureLockedArena(bool enabled)
    {
        useLockedCombatArena = enabled;
    }

    public void ConfigureBossMusic(AudioClip clip)
    {
        bossMusic = clip;
    }

    public void ConfigureBossDeathReward(BossDeathRewardConfig config)
    {
        bossDeathRewardConfig = config;
    }

    /// <summary>Player thua trận: mở arena, trả boss về tâm và cho phép đánh lại.</summary>
    public void NotifyPlayerDefeated()
    {
        if (!useLockedCombatArena || !arenaLocked || arenaFinished)
        {
            return;
        }

        arenaLocked = false;
        HideArenaBarrier();
        StopBossMusic();
        UnregisterCamera();
        hudRegistered = false;

        BossHUDController hud =
            FindFirstObjectByType<BossHUDController>(FindObjectsInactive.Include);
        hud?.ClearBoss();

        health?.RestoreFull();
        if (bossAgent != null && bossAgent.enabled && bossAgent.isOnNavMesh &&
            NavMesh.SamplePosition(arenaCenter, out NavMeshHit hit, 2.5f, bossAgent.areaMask))
        {
            bossAgent.Warp(hit.position);
        }
        else
        {
            transform.position = arenaCenter;
        }

        Debug.Log($"[BossArena] Player thua — reset trận '{bossDisplayName}'.", this);
    }

    void CachePlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
        {
            return;
        }

        player = playerObject.transform;
        playerCharacterController = playerObject.GetComponent<CharacterController>();
    }

    void TickCombatArena()
    {
        if (!useLockedCombatArena || arenaLocked || arenaFinished)
        {
            return;
        }

        Vector3 fromCenter = player.position - arenaCenter;
        if (Mathf.Abs(fromCenter.y) > arenaMaxVerticalDifference)
        {
            return;
        }

        fromCenter.y = 0f;
        if (fromCenter.sqrMagnitude > arenaEngageDistance * arenaEngageDistance)
        {
            return;
        }

        arenaLocked = true;
        KeepPlayerInsideArena();
        ShowArenaBarrier();
        StartBossMusic();
        RegisterHud();

        if (registerCamera)
        {
            RegisterCamera();
        }

        Debug.Log(
            $"[BossArena] Đã khóa vùng đấu '{bossDisplayName}' tại vị trí scene, " +
            $"radius={arenaRadius:0.#}m.",
            this);
    }

    void KeepPlayerInsideArena()
    {
        if (player == null)
        {
            return;
        }

        float safeRadius = Mathf.Max(1f, arenaRadius - 1.25f);
        Vector3 clamped = ClampToArena(player.position, safeRadius);
        if ((clamped - player.position).sqrMagnitude < 0.0001f)
        {
            return;
        }

        bool controllerWasEnabled =
            playerCharacterController != null && playerCharacterController.enabled;
        if (controllerWasEnabled)
        {
            playerCharacterController.enabled = false;
        }

        // Giữ nguyên Y để CharacterController tiếp tục bám đúng mặt đất phẳng.
        player.position = clamped;

        if (controllerWasEnabled)
        {
            playerCharacterController.enabled = true;
        }
    }

    void KeepBossInsideArena()
    {
        float padding =
            bossAgent != null ? Mathf.Max(1f, bossAgent.radius + 0.5f) : 1.5f;
        float safeRadius = Mathf.Max(1f, arenaRadius - padding);
        Vector3 clamped = ClampToArena(transform.position, safeRadius);
        if ((clamped - transform.position).sqrMagnitude < 0.0001f)
        {
            return;
        }

        // Ưu tiên NavMesh hiện có tại vị trí phẳng mà designer đã đặt.
        if (bossAgent != null && bossAgent.enabled && bossAgent.isOnNavMesh &&
            NavMesh.SamplePosition(clamped, out NavMeshHit hit, 2.5f, bossAgent.areaMask))
        {
            bossAgent.Warp(hit.position);
        }
        else
        {
            transform.position = clamped;
        }
    }

    Vector3 ClampToArena(Vector3 position, float safeRadius)
    {
        Vector3 planar = position - arenaCenter;
        planar.y = 0f;
        if (planar.sqrMagnitude <= safeRadius * safeRadius)
        {
            return position;
        }

        planar = planar.normalized * safeRadius;
        position.x = arenaCenter.x + planar.x;
        position.z = arenaCenter.z + planar.z;
        return position;
    }

    void ShowArenaBarrier()
    {
        if (!showArenaBarrier)
        {
            return;
        }

        if (arenaBarrier != null)
        {
            arenaBarrier.SetActive(true);
            arenaBarrierFade = 0f;
            ApplyArenaBarrierColor(0f);
            return;
        }

        arenaBarrier = new GameObject($"{bossDisplayName} - Arena Barrier");
        arenaBarrier.transform.SetPositionAndRotation(arenaCenter, Quaternion.identity);

        MeshFilter filter = arenaBarrier.AddComponent<MeshFilter>();
        MeshRenderer renderer = arenaBarrier.AddComponent<MeshRenderer>();

        arenaBarrierMesh = BuildArenaBarrierMesh();
        arenaBarrierMesh.name = $"{bossDisplayName} Arena Barrier Mesh";
        filter.sharedMesh = arenaBarrierMesh;

        arenaBarrierMaterial = BuildArenaBarrierMaterial();
        renderer.sharedMaterial = arenaBarrierMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        // Camera cần bị chặn bởi tường arena, nếu không sẽ nhìn xuyên qua
        // khi camera boss (distance 7m) bị đẩy ra ngoài bán kính arena (14m).
        MeshCollider barrierCollider = arenaBarrier.AddComponent<MeshCollider>();
        barrierCollider.sharedMesh = arenaBarrierMesh;

        arenaBarrierFade = 0f;
        ApplyArenaBarrierColor(0f);
    }

    Mesh BuildArenaBarrierMesh()
    {
        int segments = Mathf.Clamp(arenaBarrierSegments, 16, 128);
        float radius = Mathf.Max(1f, arenaRadius);
        float bottom = -0.35f;
        float top = Mathf.Max(1f, arenaBarrierHeight);

        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[segments * 12];

        for (int i = 0; i <= segments; i++)
        {
            float ratio = i / (float)segments;
            float angle = ratio * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            int vertex = i * 2;

            vertices[vertex] = new Vector3(x, bottom, z);
            vertices[vertex + 1] = new Vector3(x, top, z);
            uvs[vertex] = new Vector2(ratio * 8f, 0f);
            uvs[vertex + 1] = new Vector2(ratio * 8f, 1f);
        }

        for (int i = 0; i < segments; i++)
        {
            int vertex = i * 2;
            int triangle = i * 12;

            // Hai mặt để tường luôn thấy được từ cả trong và ngoài vòng đấu.
            triangles[triangle] = vertex;
            triangles[triangle + 1] = vertex + 1;
            triangles[triangle + 2] = vertex + 2;
            triangles[triangle + 3] = vertex + 2;
            triangles[triangle + 4] = vertex + 1;
            triangles[triangle + 5] = vertex + 3;

            triangles[triangle + 6] = vertex + 2;
            triangles[triangle + 7] = vertex + 1;
            triangles[triangle + 8] = vertex;
            triangles[triangle + 9] = vertex + 3;
            triangles[triangle + 10] = vertex + 1;
            triangles[triangle + 11] = vertex + 2;
        }

        Mesh mesh = new();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    Material BuildArenaBarrierMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new(shader)
        {
            name = $"{bossDisplayName} Arena Barrier Material",
            renderQueue = (int)RenderQueue.Transparent
        };

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", (float)CullMode.Off);
        }

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        return material;
    }

    void UpdateArenaBarrierVisual()
    {
        if (arenaBarrierMaterial == null || !arenaLocked || arenaFinished)
        {
            return;
        }

        arenaBarrierFade = Mathf.MoveTowards(arenaBarrierFade, 1f, Time.deltaTime * 3f);
        float pulse = Mathf.Sin(Time.unscaledTime * arenaBarrierPulseSpeed) *
                      arenaBarrierPulseAmount;
        ApplyArenaBarrierColor(Mathf.Clamp01(arenaBarrierFade * (1f + pulse)));
    }

    void ApplyArenaBarrierColor(float opacityMultiplier)
    {
        if (arenaBarrierMaterial == null)
        {
            return;
        }

        Color color = arenaBarrierColor;
        color.a *= opacityMultiplier;

        if (arenaBarrierMaterial.HasProperty("_BaseColor"))
        {
            arenaBarrierMaterial.SetColor("_BaseColor", color);
        }

        if (arenaBarrierMaterial.HasProperty("_Color"))
        {
            arenaBarrierMaterial.SetColor("_Color", color);
        }
    }

    void HideArenaBarrier()
    {
        if (arenaBarrier != null)
        {
            arenaBarrier.SetActive(false);
        }
    }

    void CleanupArenaBarrier()
    {
        if (arenaBarrier != null)
        {
            Destroy(arenaBarrier);
            arenaBarrier = null;
        }

        if (arenaBarrierMesh != null)
        {
            Destroy(arenaBarrierMesh);
            arenaBarrierMesh = null;
        }

        if (arenaBarrierMaterial != null)
        {
            Destroy(arenaBarrierMaterial);
            arenaBarrierMaterial = null;
        }
    }

    void StartBossMusic()
    {
        if (bossMusicPlaying || bossMusic == null)
        {
            return;
        }

        AudioManager manager = AudioManager.EnsureInstance();
        if (manager == null)
        {
            return;
        }

        bossMusicPlaying = true;
        manager.PlayMusicOverride(bossMusic, bossMusicVolume, bossMusicFadeIn);
    }

    void StopBossMusic()
    {
        if (!bossMusicPlaying)
        {
            return;
        }

        bossMusicPlaying = false;
        AudioManager.Instance?.StopMusicOverride(bossMusic, bossMusicFadeOut);
    }

    void RegisterHud()
    {
        if (hudRegistered || health == null)
        {
            return;
        }

        hudRegistered = true;

        BossHUDController hud =
            FindFirstObjectByType<BossHUDController>(FindObjectsInactive.Include);
        hud?.BindBoss(bossDisplayName, health);
    }

    void RegisterCamera()
    {
        if (cameraRegistered || health == null)
        {
            return;
        }

        cameraRegistered = true;

        CameraController camera = FindFirstObjectByType<CameraController>();
        camera?.SetBossTarget(health.transform);
    }

    void UnregisterCamera()
    {
        if (!cameraRegistered)
        {
            return;
        }

        cameraRegistered = false;

        CameraController camera = FindFirstObjectByType<CameraController>();
        camera?.ClearBossTarget();
    }

    void HandleDied(CharacterHealth _)
    {
        arenaFinished = true;
        arenaLocked = false;
        HideArenaBarrier();
        StopBossMusic();

        if (SpawnAncientNote())
        {
            ZoneObjectiveManager.Instance?.NotifyAncientNoteDropped();
        }

        bossDeathRewardConfig?.NotifyBossDied(transform);

        ZoneObjectiveManager.Instance?.NotifyMiniBossDefeated();

        BossHUDController hud =
            FindFirstObjectByType<BossHUDController>(FindObjectsInactive.Include);
        hud?.ClearBoss();

        UnregisterCamera();
        hudRegistered = false;
    }

    bool SpawnAncientNote()
    {
        if (ancientNoteSpawned || ancientNotePrefab == null || AncientNotePickup.WasCollected)
        {
            return false;
        }

        ancientNoteSpawned = true;
        Vector3 candidate = transform.position +
                            transform.right * ancientNoteDropOffset.x +
                            transform.forward * ancientNoteDropOffset.z;
        Vector3 spawnPosition = candidate + Vector3.up * ancientNoteDropOffset.y;

        int areaMask = bossAgent != null ? bossAgent.areaMask : NavMesh.AllAreas;
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, areaMask))
        {
            spawnPosition = hit.position + Vector3.up * ancientNoteDropOffset.y;
        }

        Instantiate(ancientNotePrefab, spawnPosition, Quaternion.identity);
        Debug.Log($"[Boss] '{bossDisplayName}' dropped the Ancient Note.", this);
        return true;
    }
}
