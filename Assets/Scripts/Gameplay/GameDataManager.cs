using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;

[DefaultExecutionOrder(-1000)]
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
    private const string HeroProgressJsonKey = "ASTRA_HERO_PROGRESS_JSON";
    public const string DefaultHeroId = "seeker_default_01";
    private const string LegacyDemoHeroId = "hero_ravenous_butcher";
    private const string CurrentObjectiveKey = "ASTRA_CURRENT_OBJECTIVE";
    private const string AncientNoteCollectedKey = "ASTRA_ANCIENT_NOTE_FLOATING_TREE_COLLECTED";
    private const string AncientNote2CollectedKey = "ASTRA_ANCIENT_NOTE_FLOATING_TREE_02_COLLECTED";
    private const string AncientForestBossDefeatedKey = "ASTRA_ANCIENT_FOREST_BOSS_DEFEATED";
    private const string FloatingTreeSecondNoteSpawnedKey = "ASTRA_FLOATING_TREE_SECOND_NOTE_SPAWNED";
    private const string AncientMapUsedKey = "ASTRA_ANCIENT_MAP_USED";
    private const string AncientMapGuidanceUnlockedKey = "ASTRA_ANCIENT_MAP_GUIDANCE_UNLOCKED";
    private const string AncientMap2UsedKey = "ASTRA_ANCIENT_MAP_02_USED";
    private const string AncientMap2GuidanceUnlockedKey = "ASTRA_ANCIENT_MAP_02_GUIDANCE_UNLOCKED";
    private const string FinalBossEncounterSeenKey = "ASTRA_FINAL_BOSS_ENCOUNTER_SEEN";
    private const string FinalBossDefeatedKey = "ASTRA_FINAL_BOSS_DEFEATED";

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

    [Header("Tiến trình Hero")]
    [SerializeField, Min(0)] private int availableHeroUpgradePoints;
    [SerializeField] private List<string> ownedHeroIds = new List<string>();
    [SerializeField] private List<HeroProgressData> heroProgress = new List<HeroProgressData>();

    [Header("Tiến trình cốt truyện")]
    [SerializeField] private string currentObjective = "";
    [SerializeField] private bool ancientNoteCollected;
    [SerializeField] private bool ancientNote2Collected;
    [SerializeField] private bool ancientForestBossDefeated;
    [SerializeField] private bool floatingTreeSecondNoteSpawned;
    [SerializeField] private bool ancientMapUsed;
    [SerializeField] private bool ancientMapGuidanceUnlocked;
    [SerializeField] private bool ancientMap2Used;
    [SerializeField] private bool ancientMap2GuidanceUnlocked;
    [SerializeField] private bool finalBossEncounterSeen;
    [SerializeField] private bool finalBossDefeated;

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

    [System.Serializable]
    private class HeroProgressSaveData
    {
        public int version = 1;
        public int availableUpgradePoints;
        public List<string> ownedHeroIds = new List<string>();
        public List<HeroProgressData> heroes = new List<HeroProgressData>();
    }

    private HashSet<string> clearedZones = new HashSet<string>();

    private bool playerPrefsDirty;
    private float playerPrefsFlushTimer;
    private const float PlayerPrefsFlushInterval = 10f;

    public event Action<int> OnCurrencyChanged;
    public event Action HeroProgressChanged;
    public event Action HeroOwnershipChanged;
    public event Action HeroScreenOpenRequested;

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

    public int AvailableHeroUpgradePoints => Mathf.Max(0, availableHeroUpgradePoints);

    public IReadOnlyList<string> OwnedHeroIds => ownedHeroIds;

    public string CurrentObjective => currentObjective;

    public bool IsAncientNoteCollected => ancientNoteCollected;

    public bool IsAncientNote2Collected => ancientNote2Collected;

    public bool IsAncientForestBossDefeated => ancientForestBossDefeated;

    public bool IsFloatingTreeSecondNoteSpawned => floatingTreeSecondNoteSpawned;

    public bool IsAncientMapUsed => ancientMapUsed;

    public bool IsAncientMapGuidanceUnlocked => ancientMapGuidanceUnlocked;

    public bool IsAncientMap2Used => ancientMap2Used;

    public bool IsAncientMap2GuidanceUnlocked => ancientMap2GuidanceUnlocked;

    public bool IsFinalBossEncounterSeen => finalBossEncounterSeen;

    public bool IsFinalBossDefeated => finalBossDefeated;

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
        PlayerProgression.GlobalLevelsGained += HandlePlayerLevelsGained;
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
            PlayerProgression.GlobalLevelsGained -= HandlePlayerLevelsGained;
            FlushPlayerPrefs();
        }
    }

    private void HandlePlayerLevelsGained(int levelsGained)
    {
        GrantHeroUpgradePoints(levelsGained, requestScreenOpen: true);
    }

    public void GrantHeroUpgradePoints(int amount, bool requestScreenOpen = false)
    {
        if (amount <= 0)
        {
            return;
        }

        long updatedPoints = (long)availableHeroUpgradePoints + amount;
        availableHeroUpgradePoints = (int)Math.Min(updatedPoints, int.MaxValue);
        SaveHeroProgression(flushImmediately: true);
        HeroProgressChanged?.Invoke();

        if (requestScreenOpen)
        {
            HeroScreenOpenRequested?.Invoke();
        }
    }

    public void RequestHeroScreenOpen()
    {
        HeroScreenOpenRequested?.Invoke();
    }

    public bool OwnHero(string heroId)
    {
        if (string.IsNullOrWhiteSpace(heroId) || IsHeroOwned(heroId))
        {
            return false;
        }

        ownedHeroIds.Add(heroId);
        GetOrCreateHeroProgress(heroId);
        SaveHeroProgression(flushImmediately: true);
        HeroOwnershipChanged?.Invoke();
        HeroProgressChanged?.Invoke();
        return true;
    }

    public bool IsHeroOwned(string heroId)
    {
        return !string.IsNullOrWhiteSpace(heroId) && ownedHeroIds.Contains(heroId);
    }

    public IReadOnlyList<string> GetOwnedHeroIds()
    {
        return ownedHeroIds;
    }

    public HeroProgressData GetHeroProgress(string heroId)
    {
        if (string.IsNullOrWhiteSpace(heroId))
        {
            return null;
        }

        return GetOrCreateHeroProgress(heroId);
    }

    public int GetHeroUpgradeLevel(string heroId, HeroStatType statType)
    {
        HeroProgressData progress = GetHeroProgress(heroId);
        return progress != null ? progress.GetUpgradeLevel(statType) : 0;
    }

    public float GetHeroFinalStat(HeroDefinition definition, HeroStatType statType)
    {
        if (definition == null)
        {
            return 0f;
        }

        return definition.CalculateFinalStat(
            statType,
            GetHeroUpgradeLevel(definition.HeroId, statType));
    }

    public bool TryUpgradeHeroStat(string heroId, HeroStatType statType, out int newUpgradeLevel)
    {
        newUpgradeLevel = 0;
        if (!IsHeroOwned(heroId) || availableHeroUpgradePoints <= 0)
        {
            return false;
        }

        HeroProgressData progress = GetOrCreateHeroProgress(heroId);
        if (progress == null)
        {
            return false;
        }

        newUpgradeLevel = progress.IncrementUpgradeLevel(statType);
        availableHeroUpgradePoints--;
        SaveHeroProgression(flushImmediately: true);
        HeroProgressChanged?.Invoke();
        return true;
    }

    private HeroProgressData GetOrCreateHeroProgress(string heroId)
    {
        for (int i = 0; i < heroProgress.Count; i++)
        {
            HeroProgressData entry = heroProgress[i];
            if (entry != null && string.Equals(entry.heroId, heroId, StringComparison.Ordinal))
            {
                return entry;
            }
        }

        HeroProgressData created = new HeroProgressData(heroId);
        heroProgress.Add(created);
        return created;
    }

    private void SaveHeroProgression(bool flushImmediately)
    {
        HeroProgressSaveData save = new HeroProgressSaveData
        {
            availableUpgradePoints = Mathf.Max(0, availableHeroUpgradePoints),
            ownedHeroIds = new List<string>(ownedHeroIds),
            heroes = new List<HeroProgressData>(heroProgress)
        };

        PlayerPrefs.SetString(HeroProgressJsonKey, JsonUtility.ToJson(save));
        PlayerPrefs.SetInt(HasSaveKey, 1);
        MarkPlayerPrefsDirty();
        if (flushImmediately)
        {
            FlushPlayerPrefs();
        }
    }

    private void LoadHeroProgression()
    {
        availableHeroUpgradePoints = 0;
        ownedHeroIds.Clear();
        heroProgress.Clear();

        string json = PlayerPrefs.GetString(HeroProgressJsonKey, string.Empty);
        if (!string.IsNullOrEmpty(json))
        {
            HeroProgressSaveData save = JsonUtility.FromJson<HeroProgressSaveData>(json);
            if (save != null)
            {
                availableHeroUpgradePoints = Mathf.Max(0, save.availableUpgradePoints);
                if (save.ownedHeroIds != null)
                {
                    for (int i = 0; i < save.ownedHeroIds.Count; i++)
                    {
                        string id = save.ownedHeroIds[i];
                        if (!string.IsNullOrWhiteSpace(id) && !ownedHeroIds.Contains(id))
                        {
                            ownedHeroIds.Add(id);
                        }
                    }
                }

                if (save.heroes != null)
                {
                    for (int i = 0; i < save.heroes.Count; i++)
                    {
                        HeroProgressData progress = save.heroes[i];
                        if (progress == null || string.IsNullOrWhiteSpace(progress.heroId))
                        {
                            continue;
                        }

                        progress.Sanitize();
                        if (heroProgress.Find(x => x != null && x.heroId == progress.heroId) == null)
                        {
                            heroProgress.Add(progress);
                        }
                    }
                }
            }
        }
        else
        {
            // Save cũ: các level đã đạt trước khi hệ Hero tồn tại vẫn nhận đúng số point.
            availableHeroUpgradePoints = Mathf.Max(0, playerLevel - 1);
        }

        MigrateLegacyHeroId(LegacyDemoHeroId, DefaultHeroId);

        if (ownedHeroIds.Count == 0)
        {
            ownedHeroIds.Add(DefaultHeroId);
        }

        for (int i = 0; i < ownedHeroIds.Count; i++)
        {
            GetOrCreateHeroProgress(ownedHeroIds[i]);
        }
    }

    private void MigrateLegacyHeroId(string legacyId, string currentId)
    {
        if (string.IsNullOrWhiteSpace(legacyId) || string.IsNullOrWhiteSpace(currentId) ||
            string.Equals(legacyId, currentId, StringComparison.Ordinal))
        {
            return;
        }

        int legacyOwnedIndex = ownedHeroIds.IndexOf(legacyId);
        if (legacyOwnedIndex >= 0)
        {
            if (ownedHeroIds.Contains(currentId))
            {
                ownedHeroIds.RemoveAt(legacyOwnedIndex);
            }
            else
            {
                ownedHeroIds[legacyOwnedIndex] = currentId;
            }
        }

        HeroProgressData legacy = heroProgress.Find(x => x != null && x.heroId == legacyId);
        if (legacy == null)
        {
            return;
        }

        HeroProgressData current = heroProgress.Find(x => x != null && x.heroId == currentId);
        if (current == null)
        {
            legacy.heroId = currentId;
            return;
        }

        current.healthUpgradeLevel = Math.Max(current.healthUpgradeLevel, legacy.healthUpgradeLevel);
        current.damageUpgradeLevel = Math.Max(current.damageUpgradeLevel, legacy.damageUpgradeLevel);
        current.defenseUpgradeLevel = Math.Max(current.defenseUpgradeLevel, legacy.defenseUpgradeLevel);
        current.moveSpeedUpgradeLevel = Math.Max(current.moveSpeedUpgradeLevel, legacy.moveSpeedUpgradeLevel);
        current.manaUpgradeLevel = Math.Max(current.manaUpgradeLevel, legacy.manaUpgradeLevel);
        heroProgress.Remove(legacy);
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

    public void SaveCurrentObjective(string objective)
    {
        currentObjective = objective ?? string.Empty;
        PlayerPrefs.SetString(CurrentObjectiveKey, currentObjective);
        PlayerPrefs.SetInt(HasSaveKey, 1);
        MarkPlayerPrefsDirty();
    }

    public void MarkAncientNoteCollected()
    {
        if (ancientNoteCollected)
        {
            return;
        }

        ancientNoteCollected = true;
        PlayerPrefs.SetInt(AncientNoteCollectedKey, 1);
        PlayerPrefs.SetInt(HasSaveKey, 1);
        MarkPlayerPrefsDirty();
        FlushPlayerPrefs();
    }

    public void MarkAncientNote2Collected()
    {
        if (ancientNote2Collected)
        {
            return;
        }

        ancientNote2Collected = true;
        PlayerPrefs.SetInt(AncientNote2CollectedKey, 1);
        PlayerPrefs.SetInt(HasSaveKey, 1);
        MarkPlayerPrefsDirty();
        FlushPlayerPrefs();
    }

    public void MarkAncientForestBossDefeated()
    {
        if (ancientForestBossDefeated)
        {
            return;
        }

        ancientForestBossDefeated = true;
        PlayerPrefs.SetInt(AncientForestBossDefeatedKey, 1);
        PlayerPrefs.SetInt(HasSaveKey, 1);
        MarkPlayerPrefsDirty();
        FlushPlayerPrefs();
    }

    public void MarkFloatingTreeSecondNoteSpawned()
    {
        if (floatingTreeSecondNoteSpawned)
        {
            return;
        }

        floatingTreeSecondNoteSpawned = true;
        PlayerPrefs.SetInt(FloatingTreeSecondNoteSpawnedKey, 1);
        PlayerPrefs.SetInt(HasSaveKey, 1);
        MarkPlayerPrefsDirty();
        FlushPlayerPrefs();
    }

    public void MarkAncientMapUsed()
    {
        bool changed = !ancientMapUsed || !ancientMapGuidanceUnlocked;
        ancientMapUsed = true;
        ancientMapGuidanceUnlocked = true;
        PlayerPrefs.SetInt(AncientMapUsedKey, 1);
        PlayerPrefs.SetInt(AncientMapGuidanceUnlockedKey, 1);
        PlayerPrefs.SetInt(HasSaveKey, 1);
        MarkPlayerPrefsDirty();

        if (changed)
        {
            FlushPlayerPrefs();
        }
    }

    public void MarkAncientMap2Used()
    {
        bool changed = !ancientMap2Used || !ancientMap2GuidanceUnlocked;
        ancientMap2Used = true;
        ancientMap2GuidanceUnlocked = true;
        PlayerPrefs.SetInt(AncientMap2UsedKey, 1);
        PlayerPrefs.SetInt(AncientMap2GuidanceUnlockedKey, 1);
        PlayerPrefs.SetInt(HasSaveKey, 1);
        MarkPlayerPrefsDirty();

        if (changed)
        {
            FlushPlayerPrefs();
        }
    }

    public void MarkFinalBossEncounterSeen()
    {
        if (finalBossEncounterSeen)
        {
            return;
        }

        finalBossEncounterSeen = true;
        PlayerPrefs.SetInt(FinalBossEncounterSeenKey, 1);
        PlayerPrefs.SetInt(HasSaveKey, 1);
        MarkPlayerPrefsDirty();
        FlushPlayerPrefs();
    }

    public void MarkFinalBossDefeated()
    {
        if (finalBossDefeated)
        {
            return;
        }

        finalBossDefeated = true;
        PlayerPrefs.SetInt(FinalBossDefeatedKey, 1);
        PlayerPrefs.SetInt(HasSaveKey, 1);
        MarkPlayerPrefsDirty();
        FlushPlayerPrefs();
    }

    /// <summary>
    /// Chỉ reset cờ cinematic Commander để demo lại Encounter/Victory, không đụng
    /// vào inventory, level, tiền hay tiến trình các boss khác.
    /// </summary>
    public void ResetFinalBossCutsceneProgressForDemo()
    {
        finalBossEncounterSeen = false;
        finalBossDefeated = false;
        PlayerPrefs.DeleteKey(FinalBossEncounterSeenKey);
        PlayerPrefs.DeleteKey(FinalBossDefeatedKey);
        MarkPlayerPrefsDirty();
        FlushPlayerPrefs();
    }

    /// <summary>
    /// Reset trạng thái "chưa nhặt tờ giấy hướng dẫn" (Note #1 + Note #2 + cờ tree đã
    /// spawn Note + cờ boss đã hạ) để demo lại khúc: hạ boss Ancient Forest → cây →
    /// bấm F → Note #2 rơi. Xóa luôn cờ boss hạ (ancientForestBossDefeated) để bắt
    /// buộc phải hạ lại boss thứ 2 thì F mới nhả giấy.
    /// </summary>
    public void ResetAncientNoteProgressForDemo()
    {
        ancientNoteCollected = false;
        PlayerPrefs.DeleteKey(AncientNoteCollectedKey);

        ancientNote2Collected = false;
        PlayerPrefs.DeleteKey(AncientNote2CollectedKey);

        floatingTreeSecondNoteSpawned = false;
        PlayerPrefs.DeleteKey(FloatingTreeSecondNoteSpawnedKey);

        ancientMapUsed = false;
        ancientMapGuidanceUnlocked = false;
        PlayerPrefs.DeleteKey(AncientMapUsedKey);
        PlayerPrefs.DeleteKey(AncientMapGuidanceUnlockedKey);

        ancientMap2Used = false;
        ancientMap2GuidanceUnlocked = false;
        PlayerPrefs.DeleteKey(AncientMap2UsedKey);
        PlayerPrefs.DeleteKey(AncientMap2GuidanceUnlockedKey);
        PlayerPrefs.DeleteKey(FinalBossEncounterSeenKey);
        PlayerPrefs.DeleteKey(FinalBossDefeatedKey);

        PlayerInventoryService inventory = PlayerInventoryService.FindForPlayer();
        ItemData ancientMap = AncientMapProgression.ResolveMapItem();
        ItemData ancientMap2 = AncientMapProgression.ResolveMapItem(null, true);
        if (inventory != null)
        {
            RemoveAllFromInventory(inventory, ancientMap);
            RemoveAllFromInventory(inventory, ancientMap2);
        }
        else
        {
            Dictionary<string, int> savedInventory = LoadInventory();
            bool removed = savedInventory.Remove(AncientMapProgression.ItemId);
            removed |= savedInventory.Remove(AncientMapProgression.Item2Id);
            if (removed)
            {
                SaveInventory(savedInventory);
            }
        }

        static void RemoveAllFromInventory(PlayerInventoryService targetInventory, ItemData item)
        {
            if (targetInventory == null || item == null)
            {
                return;
            }

            int quantity = targetInventory.GetQuantity(item);
            if (quantity > 0)
            {
                targetInventory.RemoveItem(item, quantity);
            }
        }

        ancientForestBossDefeated = false;
        PlayerPrefs.DeleteKey(AncientForestBossDefeatedKey);

        MarkPlayerPrefsDirty();
        FlushPlayerPrefs();
        Debug.Log("[GameDataManager] Đã reset trạng thái nhặt giấy + boss đã hạ (demo).");
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

    }

    public void SaveLastPlayerTransform(string sceneName, Transform playerTransform)
    {
        if (playerTransform == null) return;

        Vector3 pos = playerTransform.position;
        float rotY = playerTransform.eulerAngles.y;

        SaveScenePosition(sceneName, pos);

        PlayerPrefs.SetFloat(LastRotYKey, rotY);
        MarkPlayerPrefsDirty();

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
        PlayerPrefs.DeleteKey(HeroProgressJsonKey);
        playerLevel = 1;
        playerExperience = 0;
        availableHeroUpgradePoints = 0;
        ownedHeroIds.Clear();
        ownedHeroIds.Add(DefaultHeroId);
        heroProgress.Clear();
        GetOrCreateHeroProgress(DefaultHeroId);
        PlayerPrefs.DeleteKey(CurrentObjectiveKey);
        PlayerPrefs.DeleteKey(AncientNoteCollectedKey);
        PlayerPrefs.DeleteKey(AncientNote2CollectedKey);
        PlayerPrefs.DeleteKey(AncientForestBossDefeatedKey);
        PlayerPrefs.DeleteKey(FloatingTreeSecondNoteSpawnedKey);
        PlayerPrefs.DeleteKey(AncientMapUsedKey);
        PlayerPrefs.DeleteKey(AncientMapGuidanceUnlockedKey);
        PlayerPrefs.DeleteKey(AncientMap2UsedKey);
        PlayerPrefs.DeleteKey(AncientMap2GuidanceUnlockedKey);
        currentObjective = string.Empty;
        ancientNoteCollected = false;
        ancientNote2Collected = false;
        ancientForestBossDefeated = false;
        floatingTreeSecondNoteSpawned = false;
        ancientMapUsed = false;
        ancientMapGuidanceUnlocked = false;
        ancientMap2Used = false;
        ancientMap2GuidanceUnlocked = false;
        finalBossEncounterSeen = false;
        finalBossDefeated = false;

        MarkPlayerPrefsDirty();
        FlushPlayerPrefs();

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
        SaveHeroProgression(flushImmediately: false);
        PlayerPrefs.SetString(CurrentObjectiveKey, currentObjective ?? string.Empty);
        PlayerPrefs.SetInt(AncientNoteCollectedKey, ancientNoteCollected ? 1 : 0);
        PlayerPrefs.SetInt(AncientNote2CollectedKey, ancientNote2Collected ? 1 : 0);
        PlayerPrefs.SetInt(AncientForestBossDefeatedKey, ancientForestBossDefeated ? 1 : 0);
        PlayerPrefs.SetInt(FloatingTreeSecondNoteSpawnedKey, floatingTreeSecondNoteSpawned ? 1 : 0);
        PlayerPrefs.SetInt(AncientMapUsedKey, ancientMapUsed ? 1 : 0);
        PlayerPrefs.SetInt(AncientMapGuidanceUnlockedKey, ancientMapGuidanceUnlocked ? 1 : 0);
        PlayerPrefs.SetInt(AncientMap2UsedKey, ancientMap2Used ? 1 : 0);
        PlayerPrefs.SetInt(AncientMap2GuidanceUnlockedKey, ancientMap2GuidanceUnlocked ? 1 : 0);
        PlayerPrefs.SetInt(FinalBossEncounterSeenKey, finalBossEncounterSeen ? 1 : 0);
        PlayerPrefs.SetInt(FinalBossDefeatedKey, finalBossDefeated ? 1 : 0);

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
        LoadHeroProgression();
        currentObjective = PlayerPrefs.GetString(CurrentObjectiveKey, string.Empty);
        ancientNoteCollected = PlayerPrefs.GetInt(AncientNoteCollectedKey, 0) == 1;
        ancientNote2Collected = PlayerPrefs.GetInt(AncientNote2CollectedKey, 0) == 1;
        ancientForestBossDefeated = PlayerPrefs.GetInt(AncientForestBossDefeatedKey, 0) == 1;
        floatingTreeSecondNoteSpawned = PlayerPrefs.GetInt(FloatingTreeSecondNoteSpawnedKey, 0) == 1;
        ancientMapUsed = PlayerPrefs.GetInt(AncientMapUsedKey, 0) == 1;
        ancientMapGuidanceUnlocked = PlayerPrefs.GetInt(AncientMapGuidanceUnlockedKey, ancientMapUsed ? 1 : 0) == 1;
        ancientMap2Used = PlayerPrefs.GetInt(AncientMap2UsedKey, 0) == 1;
        ancientMap2GuidanceUnlocked = PlayerPrefs.GetInt(AncientMap2GuidanceUnlockedKey, ancientMap2Used ? 1 : 0) == 1;
        finalBossEncounterSeen = PlayerPrefs.GetInt(FinalBossEncounterSeenKey, 0) == 1;
        finalBossDefeated = PlayerPrefs.GetInt(FinalBossDefeatedKey, 0) == 1;

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
