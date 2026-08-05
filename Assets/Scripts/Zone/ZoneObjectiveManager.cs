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
    [SerializeField] private string currentObjective = "";
    [SerializeField] private ObjectiveHUDController objectiveHudPrefab;

    [Header("Rewards")]
    [SerializeField] private ItemData bonusGoldItem;
    [SerializeField, Min(0)] private int bonusGoldAmount = 50;

    int enemyKills;
    int resourceGathers;
    bool miniBossDefeated;
    bool zoneCleared;
    bool waitingForAncientNote;
    bool resultPending;

    public event Action ZoneCleared;
    public event Action<string> ObjectiveChanged;

    public string ZoneId => zoneId;
    public bool IsZoneCleared => zoneCleared;
    public int EnemyKills => enemyKills;
    public int ResourceGathers => resourceGathers;
    public bool MiniBossDefeated => miniBossDefeated;
    public string CurrentObjective => currentObjective;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ObjectiveHUDController.RegisterPrefab(objectiveHudPrefab);

        if (GameDataManager.Instance != null && GameDataManager.Instance.IsZoneCleared(zoneId))
        {
            zoneCleared = true;
        }

        if (GameDataManager.Instance != null &&
            !string.IsNullOrWhiteSpace(GameDataManager.Instance.CurrentObjective))
        {
            currentObjective = GameDataManager.Instance.CurrentObjective;
        }
    }

    void Start()
    {
        if (GameDataManager.Instance != null &&
            !string.IsNullOrWhiteSpace(GameDataManager.Instance.CurrentObjective))
        {
            currentObjective = GameDataManager.Instance.CurrentObjective;
        }

        if (!string.IsNullOrWhiteSpace(currentObjective))
        {
            ObjectiveHUDController.ShowObjective(currentObjective);
            ObjectiveChanged?.Invoke(currentObjective);
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

    public void NotifyAncientNoteDropped()
    {
        waitingForAncientNote = true;
    }

    public void SetCurrentObjective(string objective, bool persist = true)
    {
        string normalized = objective?.Trim() ?? string.Empty;
        if (currentObjective == normalized)
        {
            if (!string.IsNullOrEmpty(normalized))
            {
                ObjectiveHUDController.ShowObjective(normalized);
            }
            TryShowPendingResult();
            return;
        }

        currentObjective = normalized;
        if (persist)
        {
            GameDataManager.Instance?.SaveCurrentObjective(currentObjective);
        }

        ObjectiveChanged?.Invoke(currentObjective);
        if (!string.IsNullOrEmpty(currentObjective))
        {
            ObjectiveHUDController.ShowObjective(currentObjective);
        }


        TryShowPendingResult();
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

        if (waitingForAncientNote &&
            (GameDataManager.Instance == null || !GameDataManager.Instance.IsAncientNoteCollected))
        {
            resultPending = true;
        }
        else
        {
            ShowResultScreen();
        }

        ZoneCleared?.Invoke();
        Debug.Log($"[Zone] Cleared '{zoneId}' — kills={enemyKills}, gather={resourceGathers}, boss={miniBossDefeated}");
    }

    void TryShowPendingResult()
    {
        if (!resultPending ||
            GameDataManager.Instance == null ||
            !GameDataManager.Instance.IsAncientNoteCollected)
        {
            return;
        }

        resultPending = false;
        waitingForAncientNote = false;
        ShowResultScreen();
    }

    void ShowResultScreen()
    {
        ZoneResultScreenController result =
            FindFirstObjectByType<ZoneResultScreenController>(FindObjectsInactive.Include);
        result?.Show(this);
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
