using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;

public class GameDataManager : MonoBehaviour
{
    public static GameDataManager Instance { get; private set; }

    private const string HasSaveKey = "ASTRA_HAS_SAVE";
    private const string LastSceneKey = "ASTRA_LAST_SCENE";

    private const string LastPosXKey = "ASTRA_LAST_POS_X";
    private const string LastPosYKey = "ASTRA_LAST_POS_Y";
    private const string LastPosZKey = "ASTRA_LAST_POS_Z";

    private const string LastRotYKey = "ASTRA_LAST_ROT_Y";
    private const string ContinueFlagKey = "ASTRA_LOAD_FROM_CONTINUE";
    private const string ScenePositionsJsonKey = "ASTRA_SCENE_POSITIONS_JSON";
    private const string GameTimeSecondsKey = "ASTRA_GAME_TIME_SECONDS";
    private const string PlayerLevelKey = "ASTRA_PLAYER_LEVEL";
    private const string PlayerExperienceKey = "ASTRA_PLAYER_EXPERIENCE";

    [Header("Tiền tệ")]
    [SerializeField] private int currency;

    [Header("Dữ liệu Player")]
    [SerializeField] private float playerHP = -1f;
    [SerializeField] private float playerStamina = -1f;
    [SerializeField] private float playerEnergy = -1f;

    [Header("Thời gian thế giới")]
    [Tooltip("Số giây trong ngày hiện tại (0-86399). -1 nghĩa là save cũ chưa có dữ liệu thời gian.")]
    [SerializeField] private float gameTimeSeconds = -1f;

    [Header("Tiến trình Level")]
    [SerializeField, Min(1)] private int playerLevel = 1;
    [SerializeField, Min(0)] private int playerExperience;

    [Header("Item Database")]
    [SerializeField] private List<ItemData> itemDatabase = new List<ItemData>();

    [Header("Vị trí Player theo scene")]
    [SerializeField] private List<string> posSceneNames = new List<string>();
    [SerializeField] private List<Vector3> posValues = new List<Vector3>();

    private Dictionary<string, Vector3> scenePositions = new Dictionary<string, Vector3>();

    private const string InventoryJsonKey = "ASTRA_INVENTORY_JSON";
    private const string ZoneProgressJsonKey = "ASTRA_ZONE_PROGRESS_JSON";

    [System.Serializable]
    private class InventorySaveEntry
    {
        public string itemId;
        public int quantity;
    }

    [System.Serializable]
    private class InventorySaveData
    {
        public List<InventorySaveEntry> entries = new List<InventorySaveEntry>();
    }

    [System.Serializable]
    private class ZoneProgressSaveData
    {
        public List<string> clearedZoneIds = new List<string>();
    }

    private HashSet<string> clearedZones = new HashSet<string>();

    private bool playerPrefsDirty;
    private float playerPrefsFlushTimer;
    private const float PlayerPrefsFlushInterval = 10f;

    public event Action<int> OnCurrencyChanged;

    public int Currency
    {
        get => currency;
        set
        {
            int newValue = Mathf.Max(0, value);
            if (newValue == currency) return;
            currency = newValue;
            OnCurrencyChanged?.Invoke(currency);
        }
    }

    public float PlayerHP
    {
        get => playerHP;
        set => playerHP = value;
    }

    public float PlayerStamina
    {
        get => playerStamina;
        set => playerStamina = value;
    }

    public float PlayerEnergy
    {
        get => playerEnergy;
        set => playerEnergy = value;
    }

    public bool HasPlayerData => playerHP >= 0f;

    public bool HasGameTime => gameTimeSeconds >= 0f;

    public float GameTimeSeconds => gameTimeSeconds;

    public int PlayerLevel => Mathf.Max(1, playerLevel);

    public int PlayerExperience => Mathf.Max(0, playerExperience);

    public bool HasSave => PlayerPrefs.GetInt(HasSaveKey, 0) == 1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Instance.MergeItemDatabase(itemDatabase);
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        ItemRegistry.Initialize(itemDatabase);
        LoadRuntimePositionLists();
        LoadPersistentData();
        LoadZoneProgress();
    }

    private void Update()
    {
        if (!playerPrefsDirty)
        {
            return;
        }

        playerPrefsFlushTimer += Time.unscaledDeltaTime;
        if (playerPrefsFlushTimer >= PlayerPrefsFlushInterval)
        {
            FlushPlayerPrefs();
        }
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            FlushPlayerPrefs();
        }
    }

    private void OnApplicationQuit()
    {
        FlushPlayerPrefs();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            FlushPlayerPrefs();
        }
    }

    private void MarkPlayerPrefsDirty()
    {
        playerPrefsDirty = true;
    }

    public void FlushPlayerPrefs()
    {
        if (!playerPrefsDirty)
        {
            return;
        }

        PlayerPrefs.Save();
        playerPrefsDirty = false;
        playerPrefsFlushTimer = 0f;
    }

    public void MergeItemDatabase(List<ItemData> extraItems)
    {
        if (extraItems == null || extraItems.Count == 0)
        {
            return;
        }

        for (int i = 0; i < extraItems.Count; i++)
        {
            ItemData item = extraItems[i];
            if (item == null || string.IsNullOrEmpty(item.itemId))
            {
                continue;
            }

            bool exists = false;
            for (int j = 0; j < itemDatabase.Count; j++)
            {
                if (itemDatabase[j] != null && itemDatabase[j].itemId == item.itemId)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                itemDatabase.Add(item);
            }
        }

        ItemRegistry.Initialize(itemDatabase);
    }

    public ItemData ResolveItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return null;
        }

        ItemData fromRegistry = ItemRegistry.Get(itemId);
        if (fromRegistry != null)
        {
            return fromRegistry;
        }

        for (int i = 0; i < itemDatabase.Count; i++)
        {
            ItemData item = itemDatabase[i];
            if (item != null && item.itemId == itemId)
            {
                return item;
            }
        }

        return null;
    }

    public bool IsZoneCleared(string zoneId)
    {
        if (string.IsNullOrEmpty(zoneId)) return false;
        return clearedZones.Contains(zoneId);
    }

    public void MarkZoneCleared(string zoneId)
    {
        if (string.IsNullOrEmpty(zoneId)) return;
        clearedZones.Add(zoneId);
        SaveZoneProgress();
        SavePersistentData();
    }

    public void SaveZoneProgress()
    {
        ZoneProgressSaveData data = new ZoneProgressSaveData();
        foreach (string zoneId in clearedZones)
        {
            data.clearedZoneIds.Add(zoneId);
        }

        PlayerPrefs.SetString(ZoneProgressJsonKey, JsonUtility.ToJson(data));
        MarkPlayerPrefsDirty();
    }

    private void LoadZoneProgress()
    {
        clearedZones.Clear();
        string json = PlayerPrefs.GetString(ZoneProgressJsonKey, "");
        if (string.IsNullOrEmpty(json)) return;

        ZoneProgressSaveData data = JsonUtility.FromJson<ZoneProgressSaveData>(json);
        if (data?.clearedZoneIds == null) return;

        for (int i = 0; i < data.clearedZoneIds.Count; i++)
        {
            if (!string.IsNullOrEmpty(data.clearedZoneIds[i]))
            {
                clearedZones.Add(data.clearedZoneIds[i]);
            }
        }
    }

    private void LoadRuntimePositionLists()
    {
        scenePositions.Clear();

        for (int i = 0; i < Mathf.Min(posSceneNames.Count, posValues.Count); i++)
        {
            scenePositions[posSceneNames[i]] = posValues[i];
        }
    }

    /// <summary>
    /// Cộng/trừ gold qua inventory item (single source).
    /// Fallback wallet int chỉ khi chưa có player inventory trong scene.
    /// </summary>
    public void AddCurrency(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        PlayerInventoryService inventory = PlayerInventoryService.FindForPlayer();
        ItemData gold = PlayerInventoryService.ResolveGoldItem();
        if (inventory != null && gold != null)
        {
            if (delta > 0)
            {
                inventory.AddItem(gold, delta);
            }
            else
            {
                inventory.RemoveItem(gold, -delta);
            }

            return;
        }

        // MainMenu / chưa có player: mirror int tạm (sẽ migrate vào inventory khi load player).
        Currency = currency + delta;
        SavePersistentData();
    }

    /// <summary>
    /// Mirror số gold inventory → field Currency (HUD main menu / API cũ).
    /// Không dùng làm wallet độc lập.
    /// </summary>
    public void SetCurrencyMirror(int goldFromInventory)
    {
        int newValue = Mathf.Max(0, goldFromInventory);
        if (newValue == currency)
        {
            // Vẫn persist để ASTRA_CURRENCY khớp inventory sau migrate.
            PlayerPrefs.SetInt("ASTRA_CURRENCY", currency);
            MarkPlayerPrefsDirty();
            return;
        }

        currency = newValue;
        OnCurrencyChanged?.Invoke(currency);
        PlayerPrefs.SetInt("ASTRA_CURRENCY", currency);
        MarkPlayerPrefsDirty();
    }

    /// <summary>UI/HUD subscribe vao day, va goi ngay 1 lan voi gia tri hien tai de sync khi enable.</summary>
    public void SubscribeAndFireCurrency(Action<int> handler)
    {
        if (handler == null) return;
        OnCurrencyChanged += handler;
        handler.Invoke(currency);
    }

    public void UnsubscribeCurrency(Action<int> handler)
    {
        if (handler == null) return;
        OnCurrencyChanged -= handler;
    }

    public void SaveInventoryItem(string itemId, int quantity)
    {
        string json = PlayerPrefs.GetString(InventoryJsonKey, "{}");
        InventorySaveData data = JsonUtility.FromJson<InventorySaveData>(json);
        if (data == null) data = new InventorySaveData();

        bool found = false;
        for (int i = 0; i < data.entries.Count; i++)
        {
            if (data.entries[i].itemId == itemId)
            {
                data.entries[i].quantity = quantity;
                found = true;
                break;
            }
        }
        if (!found) data.entries.Add(new InventorySaveEntry { itemId = itemId, quantity = quantity });

        PlayerPrefs.SetString(InventoryJsonKey, JsonUtility.ToJson(data));
        MarkPlayerPrefsDirty();
    }

    public void SaveInventory(Dictionary<string, int> inventory)
    {
        InventorySaveData data = new InventorySaveData();
        foreach (var kvp in inventory)
        {
            if (string.IsNullOrEmpty(kvp.Key)) continue;
            data.entries.Add(new InventorySaveEntry { itemId = kvp.Key, quantity = kvp.Value });
        }

        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(InventoryJsonKey, json);
        PlayerPrefs.SetInt(HasSaveKey, 1);
        MarkPlayerPrefsDirty();

        Debug.Log($"[GameDataManager] Saved inventory ({data.entries.Count} entries).");
    }

    public Dictionary<string, int> LoadInventory()
    {
        Dictionary<string, int> result = new Dictionary<string, int>();
        string json = PlayerPrefs.GetString(InventoryJsonKey, "");
        if (string.IsNullOrEmpty(json)) return result;

        InventorySaveData data = JsonUtility.FromJson<InventorySaveData>(json);
        if (data == null) return result;

        foreach (var entry in data.entries)
        {
            if (string.IsNullOrEmpty(entry.itemId)) continue;
            result[entry.itemId] = entry.quantity;
        }
        return result;
    }

    public void DeleteInventoryData()
    {
        PlayerPrefs.DeleteKey(InventoryJsonKey);
        MarkPlayerPrefsDirty();
    }

    public void SavePlayerStats(float hp, float stamina, float energy)
    {
        playerHP = hp;
        playerStamina = stamina;
        playerEnergy = energy;

        SavePersistentData();
    }

    public void ClearPlayerStats()
    {
        playerHP = -1f;
        playerStamina = -1f;
        playerEnergy = -1f;

        SavePersistentData();
    }

    /// <summary>
    /// Đồng bộ giờ thế giới từ hệ thống HUD. Chỉ đánh dấu save khi sang phút mới
    /// để tránh ghi PlayerPrefs ở mọi frame.
    /// </summary>
    public void UpdateGameTime(float secondsInDay, bool forcePersist = false)
    {
        const float secondsPerDay = 86400f;
        float normalized = Mathf.Repeat(secondsInDay, secondsPerDay);
        int previousMinute = gameTimeSeconds >= 0f
            ? Mathf.FloorToInt(gameTimeSeconds / 60f)
            : -1;
        int currentMinute = Mathf.FloorToInt(normalized / 60f);

        gameTimeSeconds = normalized;
        if (!forcePersist && previousMinute == currentMinute)
        {
            return;
        }

        PlayerPrefs.SetFloat(GameTimeSecondsKey, gameTimeSeconds);
        MarkPlayerPrefsDirty();
    }

    public void SavePlayerProgression(int level, int experience)
    {
        playerLevel = Mathf.Max(1, level);
        playerExperience = Mathf.Max(0, experience);
        PlayerPrefs.SetInt(PlayerLevelKey, playerLevel);
        PlayerPrefs.SetInt(PlayerExperienceKey, playerExperience);
        PlayerPrefs.SetInt(HasSaveKey, 1);
        MarkPlayerPrefsDirty();
    }

    public void SaveScenePosition(string sceneName, Vector3 position)
    {
        scenePositions[sceneName] = position;
        SyncPosToLists();

        PlayerPrefs.SetInt(HasSaveKey, 1);
        PlayerPrefs.SetString(LastSceneKey, sceneName);

        PlayerPrefs.SetFloat(LastPosXKey, position.x);
        PlayerPrefs.SetFloat(LastPosYKey, position.y);
        PlayerPrefs.SetFloat(LastPosZKey, position.z);

        SaveAllScenePositionsToPrefs();
        MarkPlayerPrefsDirty();

        // Debug.Log($"[GameDataManager] Save position scene={sceneName}, pos={position}");
    }

    private void SaveAllScenePositionsToPrefs()
    {
        ScenePositionSaveData data = new ScenePositionSaveData();

        foreach (var kvp in scenePositions)
        {
            data.entries.Add(new ScenePositionEntry
            {
                sceneName = kvp.Key,
                position = kvp.Value
            });
        }

        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(ScenePositionsJsonKey, json);

        // Debug.Log($"[GameDataManager] Saved scene positions json: {json}");
    }

    private void LoadAllScenePositionsFromPrefs()
    {
        scenePositions.Clear();

        string json = PlayerPrefs.GetString(ScenePositionsJsonKey, "");

        if (string.IsNullOrEmpty(json))
        {
            SyncPosToLists();
            return;
        }

        ScenePositionSaveData data = JsonUtility.FromJson<ScenePositionSaveData>(json);

        if (data == null || data.entries == null)
        {
            SyncPosToLists();
            return;
        }

        foreach (var entry in data.entries)
        {
            if (string.IsNullOrEmpty(entry.sceneName)) continue;

            scenePositions[entry.sceneName] = entry.position;
        }

        SyncPosToLists();

        Debug.Log($"[GameDataManager] Loaded {scenePositions.Count} scene positions from save.");
    }

    public void SaveLastPlayerTransform(string sceneName, Transform playerTransform)
    {
        if (playerTransform == null) return;

        Vector3 pos = playerTransform.position;
        float rotY = playerTransform.eulerAngles.y;

        SaveScenePosition(sceneName, pos);

        PlayerPrefs.SetFloat(LastRotYKey, rotY);
        MarkPlayerPrefsDirty();

        // Debug.Log($"[GameDataManager] Save transform scene={sceneName}, pos={pos}, rotY={rotY}");
    }

    public bool TryGetScenePosition(string sceneName, out Vector3 position)
    {
        return scenePositions.TryGetValue(sceneName, out position);
    }

    public string GetLastSceneName(string fallbackScene)
    {
        return PlayerPrefs.GetString(LastSceneKey, fallbackScene);
    }

    public Vector3 GetLastSavedPosition()
    {
        float x = PlayerPrefs.GetFloat(LastPosXKey, 0f);
        float y = PlayerPrefs.GetFloat(LastPosYKey, 1f);
        float z = PlayerPrefs.GetFloat(LastPosZKey, 0f);

        return new Vector3(x, y, z);
    }

    public float GetLastSavedRotationY()
    {
        return PlayerPrefs.GetFloat(LastRotYKey, 0f);
    }

    public void MarkLoadFromContinue()
    {
        PlayerPrefs.SetInt(ContinueFlagKey, 1);
        MarkPlayerPrefsDirty();
    }

    public bool ShouldLoadFromContinue()
    {
        return PlayerPrefs.GetInt(ContinueFlagKey, 0) == 1;
    }

    public void ClearContinueFlag()
    {
        PlayerPrefs.DeleteKey(ContinueFlagKey);
        MarkPlayerPrefsDirty();
    }

    public void DeleteSaveData()
    {
        currency = 0;
        ClearPlayerStats();

        scenePositions.Clear();
        SyncPosToLists();

        PlayerPrefs.DeleteKey(HasSaveKey);
        PlayerPrefs.DeleteKey(LastSceneKey);

        PlayerPrefs.DeleteKey(LastPosXKey);
        PlayerPrefs.DeleteKey(LastPosYKey);
        PlayerPrefs.DeleteKey(LastPosZKey);

        PlayerPrefs.DeleteKey(LastRotYKey);
        PlayerPrefs.DeleteKey(ContinueFlagKey);

        PlayerPrefs.DeleteKey(ScenePositionsJsonKey);

        DeleteInventoryData();
        clearedZones.Clear();
        PlayerPrefs.DeleteKey(ZoneProgressJsonKey);

        PlayerPrefs.DeleteKey("ASTRA_CURRENCY");
        PlayerPrefs.DeleteKey("ASTRA_PLAYER_HP");
        PlayerPrefs.DeleteKey("ASTRA_PLAYER_STAMINA");
        PlayerPrefs.DeleteKey("ASTRA_PLAYER_ENERGY");
        PlayerPrefs.DeleteKey(GameTimeSecondsKey);
        gameTimeSeconds = -1f;
        PlayerPrefs.DeleteKey(PlayerLevelKey);
        PlayerPrefs.DeleteKey(PlayerExperienceKey);
        playerLevel = 1;
        playerExperience = 0;

        MarkPlayerPrefsDirty();
        FlushPlayerPrefs();

        Debug.Log("[GameDataManager] Delete save data.");
    }

    private void SavePersistentData()
    {
        PlayerPrefs.SetInt(HasSaveKey, 1);

        PlayerPrefs.SetInt("ASTRA_CURRENCY", currency);
        PlayerPrefs.SetFloat("ASTRA_PLAYER_HP", playerHP);
        PlayerPrefs.SetFloat("ASTRA_PLAYER_STAMINA", playerStamina);
        PlayerPrefs.SetFloat("ASTRA_PLAYER_ENERGY", playerEnergy);
        if (gameTimeSeconds >= 0f)
        {
            PlayerPrefs.SetFloat(GameTimeSecondsKey, gameTimeSeconds);
        }
        PlayerPrefs.SetInt(PlayerLevelKey, playerLevel);
        PlayerPrefs.SetInt(PlayerExperienceKey, playerExperience);

        MarkPlayerPrefsDirty();
    }

    private void LoadPersistentData()
    {
        currency = PlayerPrefs.GetInt("ASTRA_CURRENCY", currency);
        playerHP = PlayerPrefs.GetFloat("ASTRA_PLAYER_HP", playerHP);
        playerStamina = PlayerPrefs.GetFloat("ASTRA_PLAYER_STAMINA", playerStamina);
        playerEnergy = PlayerPrefs.GetFloat("ASTRA_PLAYER_ENERGY", playerEnergy);
        gameTimeSeconds = PlayerPrefs.GetFloat(GameTimeSecondsKey, -1f);
        playerLevel = Mathf.Max(1, PlayerPrefs.GetInt(PlayerLevelKey, 1));
        playerExperience = Mathf.Max(0, PlayerPrefs.GetInt(PlayerExperienceKey, 0));

        LoadAllScenePositionsFromPrefs();
    }

    private void SyncPosToLists()
    {
        posSceneNames.Clear();
        posValues.Clear();

        foreach (var kvp in scenePositions)
        {
            posSceneNames.Add(kvp.Key);
            posValues.Add(kvp.Value);
        }
    }
}
[System.Serializable]
public class ScenePositionSaveData
{
    public List<ScenePositionEntry> entries = new List<ScenePositionEntry>();
}

[System.Serializable]
public class ScenePositionEntry
{
    public string sceneName;
    public Vector3 position;
}
