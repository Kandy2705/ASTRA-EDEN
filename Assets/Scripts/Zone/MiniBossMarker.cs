using UnityEngine;

/// <summary>
/// Gắn lên enemy mini-boss sau khi spawn. Đăng ký Boss HUD + camera + zone objective.
/// </summary>
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

    bool hudRegistered;
    bool cameraRegistered;
    Transform player;

    void Awake()
    {
        if (health == null)
        {
            health = GetComponent<CharacterHealth>();
        }
    }

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
    }

    void Update()
    {
        if (health == null || health.IsDead)
        {
            return;
        }

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }

            return;
        }

        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= engageDistance)
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
    }

    public void Configure(string displayName, CharacterHealth targetHealth)
    {
        bossDisplayName = displayName;
        health = targetHealth;
    }

    void RegisterHud()
    {
        if (hudRegistered || health == null)
        {
            return;
        }

        hudRegistered = true;

        BossHUDController hud = FindFirstObjectByType<BossHUDController>(FindObjectsInactive.Include);
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
        ZoneObjectiveManager.Instance?.NotifyMiniBossDefeated();

        BossHUDController hud = FindFirstObjectByType<BossHUDController>(FindObjectsInactive.Include);
        hud?.ClearBoss();

        UnregisterCamera();
        hudRegistered = false;
    }
}