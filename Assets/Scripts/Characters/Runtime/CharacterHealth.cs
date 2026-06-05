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

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || IsDead)
        {
            return;
        }

        float previousHP = runtimeStats.currentHP;
        runtimeStats.currentHP = Mathf.Max(0f, runtimeStats.currentHP - amount);
        Changed?.Invoke(this);
        PlayPlayerDamageEffect(previousHP - runtimeStats.currentHP);

        if (IsDead)
        {
            Die();
        }
    }

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

        runtimeStats.currentHP = Mathf.Min(runtimeStats.maxHP, GameDataManager.Instance.PlayerHP);
        runtimeStats.currentStamina = Mathf.Min(runtimeStats.staminaMax, GameDataManager.Instance.PlayerStamina);
        runtimeStats.currentEnergy = Mathf.Min(runtimeStats.energyMax, GameDataManager.Instance.PlayerEnergy);
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
