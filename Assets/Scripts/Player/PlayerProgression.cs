using System;
using UnityEngine;

/// <summary>
/// Level/EXP toàn cục của Player. Nhận EXP từ EnemyData.expReward, phát sự kiện
/// số level đạt được để cấp Hero Upgrade Point, rồi lưu bằng GameDataManager.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10)]
public sealed class PlayerProgression : MonoBehaviour
{
    public static event Action<int> GlobalLevelsGained;

    [SerializeField, Min(1)] private int level = 1;
    [SerializeField, Min(0)] private int currentExperience;
    [SerializeField, Min(1)] private int baseExperienceRequired = 100;
    [SerializeField, Min(0)] private int experienceGrowthPerLevel = 50;

    CharacterHealth characterHealth;
    bool loadedFromSave;

    public event Action<PlayerProgression> Changed;
    public event Action<int> LevelUp;
    public event Action<int> LevelsGained;

    public int Level => level;
    public int MaxLevel => int.MaxValue;
    public int CurrentExperience => currentExperience;
    public int ExperienceToNextLevel => GetExperienceRequired(level);
    public float NormalizedExperience =>
        Mathf.Clamp01(currentExperience / (float)Mathf.Max(1, ExperienceToNextLevel));

    public static PlayerProgression FindForPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null
            ? player.GetComponent<PlayerProgression>() ??
              player.GetComponentInChildren<PlayerProgression>(true)
            : null;
    }

    void Awake()
    {
        characterHealth = GetComponent<CharacterHealth>();
        TryLoadFromSave();
    }

    void Start()
    {
        if (!loadedFromSave)
        {
            TryLoadFromSave();
        }

        Changed?.Invoke(this);
    }

    void Update()
    {
        if (loadedFromSave)
        {
            return;
        }

        TryLoadFromSave();
        if (loadedFromSave)
        {
            Changed?.Invoke(this);
        }
    }

    public void AddExperience(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        long experiencePool = (long)currentExperience + amount;
        int levelsGained = 0;

        while (level < int.MaxValue)
        {
            int required = GetExperienceRequired(level);
            if (experiencePool < required)
            {
                break;
            }

            experiencePool -= required;
            level++;
            levelsGained++;
        }

        currentExperience = (int)Math.Min(experiencePool, int.MaxValue);
        // Cập nhật level/XP vào GameDataManager trước khi phát level event.
        // Hero point handler sẽ flush cùng một snapshot nhất quán ngay sau đó.
        SaveProgression();

        if (levelsGained > 0)
        {
            LevelUp?.Invoke(level);
            LevelsGained?.Invoke(levelsGained);
            GlobalLevelsGained?.Invoke(levelsGained);
            Debug.Log($"[Level] LEVEL UP → Lv.{level} (+{levelsGained} level).", this);
        }

        Changed?.Invoke(this);
        Debug.Log(
            $"[Level] +{amount} EXP | Lv.{level} " +
            $"{currentExperience}/{ExperienceToNextLevel}",
            this);
    }

    public int GetExperienceRequired(int forLevel)
    {
        long required = (long)baseExperienceRequired +
                        (Math.Max(1, forLevel) - 1L) * experienceGrowthPerLevel;
        return (int)Math.Max(1L, Math.Min(required, int.MaxValue));
    }

    void TryLoadFromSave()
    {
        GameDataManager data = GameDataManager.Instance;
        if (data == null)
        {
            return;
        }

        level = Mathf.Max(data.PlayerLevel, 1);
        currentExperience = Mathf.Clamp(
            data.PlayerExperience,
            0,
            GetExperienceRequired(level) - 1);
        loadedFromSave = true;
    }

    void SaveProgression()
    {
        GameDataManager data = GameDataManager.Instance;
        if (data == null)
        {
            return;
        }

        data.SavePlayerProgression(level, currentExperience);
        if (characterHealth != null && characterHealth.RuntimeStats != null)
        {
            CharacterRuntimeStats stats = characterHealth.RuntimeStats;
            data.SavePlayerStats(
                stats.currentHP,
                stats.currentStamina,
                stats.currentEnergy);
        }
    }
}
