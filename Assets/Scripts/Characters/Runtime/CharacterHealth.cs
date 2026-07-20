using System;
using UnityEngine;

public class CharacterHealth : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private CharacterData characterData;
    [SerializeField] private bool initializeFromCharacterData = true;

    [Header("Fallback Stats")]
    [SerializeField] private CharacterBaseStats fallbackBaseStats = new CharacterBaseStats();

    [Header("Runtime")]
    [SerializeField] private CharacterRuntimeStats runtimeStats;
    [SerializeField] private bool destroyOnDeath;

    public event Action<CharacterHealth> Changed;
    public event Action<CharacterHealth> Died;

    public CharacterData CharacterData => characterData;
    public CharacterRuntimeStats RuntimeStats => runtimeStats;
    public bool IsDead => runtimeStats.currentHP <= 0f;
    public float NormalizedHealth => runtimeStats.maxHP <= 0f ? 0f : runtimeStats.currentHP / runtimeStats.maxHP;

    private void Awake()
    {
        Initialize();
        RestoreFromGameData();

        // Player: đảm bảo có death controller (anim IsDead, không ragdoll).
        if (IsPlayerHealth() && GetComponent<PlayerDeathController>() == null)
        {
            gameObject.AddComponent<PlayerDeathController>();
        }
    }

    private void OnValidate()
    {
        if (runtimeStats == null)
        {
            runtimeStats = CharacterRuntimeStats.FromBaseStats(GetBaseStats());
        }
    }

    public void Initialize()
    {
        CharacterBaseStats baseStats = GetBaseStats();
        runtimeStats = CharacterRuntimeStats.FromBaseStats(baseStats);
        Changed?.Invoke(this);
    }

    /// <summary>Áp stats từ EnemyData khi spawn runtime (enemy spawn system).</summary>
    public void ApplyEnemyStats(EnemyBaseStats stats)
    {
        if (stats == null)
        {
            return;
        }

        initializeFromCharacterData = false;

        if (fallbackBaseStats == null)
        {
            fallbackBaseStats = new CharacterBaseStats();
        }

        fallbackBaseStats.maxHP = stats.maxHP;
        fallbackBaseStats.attack = stats.attack;
        fallbackBaseStats.defense = stats.defense;

        Initialize();
    }

    public void TakeDamage(float amount, bool triggerHitReaction = true)
    {
        if (amount <= 0f || IsDead)
        {
            return;
        }

        float finalDamage = ApplyDefenseMitigation(amount);
        float previousHP = runtimeStats.currentHP;
        runtimeStats.currentHP = Mathf.Max(0f, runtimeStats.currentHP - finalDamage);
        Changed?.Invoke(this);

        // Chỉ play effect cho player, enemy tự handle visual
        if (gameObject.CompareTag("Player"))
        {
            PlayPlayerDamageEffect(previousHP - runtimeStats.currentHP);
        }

        if (IsDead)
        {
            Die();
        }
    }

    /// <summary>
    /// Giảm dame theo DEF: factor = 100 / (100 + DEF).
    /// DEF 70 ≈ chặn ~41%; DEF 0 = full. Luôn lọt tối thiểu ~15% raw (trừ đòn 0).
    /// </summary>
    float ApplyDefenseMitigation(float rawAmount)
    {
        if (runtimeStats == null || rawAmount <= 0f)
        {
            return rawAmount;
        }

        float def = Mathf.Max(0f, runtimeStats.defense);
        float factor = 100f / (100f + def);
        float mitigated = rawAmount * factor;
        // Floor: vẫn nhận ít nhất 15% raw — DEF không tank tuyệt đối.
        return Mathf.Max(rawAmount * 0.15f, mitigated);
    }

    // Giữ overload cũ cho tương thích
    public void TakeDamage(float amount) => TakeDamage(amount, true);

    public void Heal(float amount)
    {
        if (amount <= 0f || IsDead)
        {
            return;
        }

        runtimeStats.currentHP = Mathf.Min(runtimeStats.maxHP, runtimeStats.currentHP + amount);
        Changed?.Invoke(this);
    }

    public void RestoreFull()
    {
        runtimeStats.currentHP = runtimeStats.maxHP;
        runtimeStats.currentStamina = runtimeStats.staminaMax;
        runtimeStats.currentEnergy = runtimeStats.energyMax;
        Changed?.Invoke(this);
    }

    public bool HasEnoughEnergy(float amount)
    {
        return runtimeStats != null && runtimeStats.currentEnergy >= amount;
    }

    public bool TryConsumeEnergy(float amount)
    {
        if (amount <= 0f || runtimeStats == null || runtimeStats.currentEnergy < amount)
        {
            return false;
        }

        runtimeStats.currentEnergy = Mathf.Max(0f, runtimeStats.currentEnergy - amount);
        Changed?.Invoke(this);
        return true;
    }

    public void TickEnergyRegen(float deltaTime)
    {
        if (runtimeStats == null || deltaTime <= 0f || runtimeStats.energyRegen <= 0f)
        {
            return;
        }

        if (runtimeStats.currentEnergy >= runtimeStats.energyMax)
        {
            return;
        }

        runtimeStats.currentEnergy = Mathf.Min(
            runtimeStats.energyMax,
            runtimeStats.currentEnergy + runtimeStats.energyRegen * deltaTime
        );
        Changed?.Invoke(this);
    }

    private CharacterBaseStats GetBaseStats()
    {
        if (initializeFromCharacterData && characterData != null && characterData.baseStats != null)
        {
            return characterData.baseStats;
        }

        if (fallbackBaseStats == null)
        {
            fallbackBaseStats = new CharacterBaseStats();
        }

        return fallbackBaseStats;
    }

    private void PlayPlayerDamageEffect(float actualDamage)
    {
        if (actualDamage <= 0f || !IsPlayerHealth())
        {
            return;
        }

        float intensity = runtimeStats.maxHP <= 0f ? 0.7f : Mathf.Clamp01(actualDamage / runtimeStats.maxHP);
        Effects.SpecialEffects.ScreenDamageEffect(Mathf.Max(0.7f, intensity));
    }

    private bool IsPlayerHealth()
    {
        return CompareTag("Player") || transform.root.CompareTag("Player");
    }

    private void RestoreFromGameData()
    {
        if (!IsPlayerHealth() || GameDataManager.Instance == null || !GameDataManager.Instance.HasPlayerData)
            return;

        ApplySavedVitals(
            GameDataManager.Instance.PlayerHP,
            GameDataManager.Instance.PlayerStamina,
            GameDataManager.Instance.PlayerEnergy);
    }

    /// <summary>Gọi từ PlayerPositionRestore / scene load để áp HP-Stamina-Energy đã save.</summary>
    public void ApplySavedVitals(float hp, float stamina, float energy)
    {
        if (runtimeStats == null)
        {
            return;
        }

        runtimeStats.currentHP = Mathf.Clamp(hp, 0f, runtimeStats.maxHP);
        runtimeStats.currentStamina = Mathf.Clamp(stamina, 0f, runtimeStats.staminaMax);
        runtimeStats.currentEnergy = Mathf.Clamp(energy, 0f, runtimeStats.energyMax);
        Changed?.Invoke(this);
    }

    private void Die()
    {
        Died?.Invoke(this);

        if (destroyOnDeath)
        {
            Destroy(gameObject);
        }
    }
}
