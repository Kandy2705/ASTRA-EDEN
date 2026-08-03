using System;
using UnityEngine;

/// <summary>
/// Level/EXP thật của Player. Nhận EXP từ EnemyData.expReward, tăng stats và lưu save.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10)]
public sealed class PlayerProgression : MonoBehaviour
{
    [SerializeField, Min(1)] private int level = 1;
    [SerializeField, Min(0)] private int currentExperience;
    [SerializeField, Min(1)] private int maxLevel = 50;
    [SerializeField, Min(1)] private int baseExperienceRequired = 100;
    [SerializeField, Min(0)] private int experienceGrowthPerLevel = 50;

    CharacterHealth characterHealth;
    bool loadedFromSave;

    public event Action<PlayerProgression> Changed;
    public event Action<int> LevelUp;

    public int Level => level;
    public int MaxLevel => maxLevel;
    public int CurrentExperience => currentExperience;
    public int ExperienceToNextLevel => GetExperienceRequired(level);
    public float NormalizedExperience => level >= maxLevel
        ? 1f
        : Mathf.Clamp01(currentExperience / (float)Mathf.Max(1, ExperienceToNextLevel));

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
        ApplyLevelToStats(restoreGainedCapacity: false);
    }

    void Start()
    {
        if (!loadedFromSave)
        {
            TryLoadFromSave();
            ApplyLevelToStats(restoreGainedCapacity: false);
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
            ApplyLevelToStats(restoreGainedCapacity: false);
            Changed?.Invoke(this);
        }
    }

    public void AddExperience(int amount)
    {
        if (amount <= 0 || level >= maxLevel)
        {
            return;
        }

        currentExperience += amount;
        int levelsGained = 0;

        while (level < maxLevel)
        {
            int required = GetExperienceRequired(level);
            if (currentExperience < required)
            {
                break;
            }

            currentExperience -= required;
            level++;
            levelsGained++;
        }

        if (level >= maxLevel)
        {
            currentExperience = 0;
        }

        if (levelsGained > 0)
        {
            ApplyLevelToStats(restoreGainedCapacity: true);
            LevelUp?.Invoke(level);
            Debug.Log($"[Level] LEVEL UP → Lv.{level} (+{levelsGained} level).", this);
        }

        SaveProgression();
        Changed?.Invoke(this);
        Debug.Log(
            $"[Level] +{amount} EXP | Lv.{level} " +
            $"{currentExperience}/{ExperienceToNextLevel}",
            this);
    }

    public int GetExperienceRequired(int forLevel)
    {
        return Mathf.Max(
            1,
            baseExperienceRequired + (Mathf.Max(1, forLevel) - 1) *
            experienceGrowthPerLevel);
    }

    void TryLoadFromSave()
    {
        GameDataManager data = GameDataManager.Instance;
        if (data == null)
        {
            return;
        }

        level = Mathf.Clamp(data.PlayerLevel, 1, maxLevel);
        currentExperience = level >= maxLevel
            ? 0
            : Mathf.Clamp(data.PlayerExperience, 0, GetExperienceRequired(level) - 1);
        loadedFromSave = true;
    }

    void ApplyLevelToStats(bool restoreGainedCapacity)
    {
        if (characterHealth == null)
        {
            characterHealth = GetComponent<CharacterHealth>();
        }

        characterHealth?.ApplyProgressionLevel(level, restoreGainedCapacity);
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
