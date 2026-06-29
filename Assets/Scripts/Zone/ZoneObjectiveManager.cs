using System;
using UnityEngine;

[DisallowMultipleComponent]
public class ZoneObjectiveManager : MonoBehaviour
{
    public static ZoneObjectiveManager Instance { get; private set; }

    [Header("Zone")]
    [SerializeField] private string zoneId = "zone_eden7_forest";

    [Header("Objectives")]
    [SerializeField, Min(0)] private int requiredEnemyKills = 3;
    [SerializeField, Min(0)] private int requiredResourceGathers = 2;
    [SerializeField] private bool requireMiniBossDefeat = true;

    [Header("Rewards")]
    [SerializeField] private ItemData bonusGoldItem;
    [SerializeField, Min(0)] private int bonusGoldAmount = 50;

    int enemyKills;
    int resourceGathers;
    bool miniBossDefeated;
    bool zoneCleared;

    public event Action ZoneCleared;

    public string ZoneId => zoneId;
    public bool IsZoneCleared => zoneCleared;
    public int EnemyKills => enemyKills;
    public int ResourceGathers => resourceGathers;
    public bool MiniBossDefeated => miniBossDefeated;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (GameDataManager.Instance != null && GameDataManager.Instance.IsZoneCleared(zoneId))
        {
            zoneCleared = true;
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void NotifyEnemyKilled()
    {
        if (zoneCleared) return;
        enemyKills++;
        CheckCompletion();
    }

    public void NotifyResourceGathered(ResourceNodeData data, int amount)
    {
        if (zoneCleared) return;
        resourceGathers++;
        CheckCompletion();
    }

    public void NotifyMiniBossDefeated()
    {
        if (zoneCleared) return;
        miniBossDefeated = true;
        CheckCompletion();
    }

    void CheckCompletion()
    {
        if (zoneCleared) return;

        bool killsOk = enemyKills >= requiredEnemyKills;
        bool gatherOk = resourceGathers >= requiredResourceGathers;
        bool bossOk = !requireMiniBossDefeat || miniBossDefeated;

        if (!killsOk || !gatherOk || !bossOk)
        {
            return;
        }

        zoneCleared = true;
        GrantRewards();
        GameDataManager.Instance?.MarkZoneCleared(zoneId);

        ZoneResultScreenController result = FindFirstObjectByType<ZoneResultScreenController>(FindObjectsInactive.Include);
        if (result != null)
        {
            result.Show(this);
        }

        ZoneCleared?.Invoke();
        Debug.Log($"[Zone] Cleared '{zoneId}' — kills={enemyKills}, gather={resourceGathers}, boss={miniBossDefeated}");
    }

    void GrantRewards()
    {
        if (bonusGoldItem == null || bonusGoldAmount <= 0)
        {
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        PlayerInventoryService inventory = player.GetComponent<PlayerInventoryService>();
        inventory?.AddItem(bonusGoldItem, bonusGoldAmount);
    }
}