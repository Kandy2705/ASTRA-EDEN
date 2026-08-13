using System;
using UnityEngine;

public class CharacterHealth : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private CharacterData characterData;
    [SerializeField] private bool initializeFromCharacterData = true;
    [SerializeField] private HeroDefinition playerHeroDefinition;

    [Header("Fallback Stats")]
    [SerializeField] private CharacterBaseStats fallbackBaseStats = new CharacterBaseStats();

    [Header("Runtime")]
    [SerializeField] private CharacterRuntimeStats runtimeStats;
    [SerializeField] private bool destroyOnDeath;

    private GameDataManager boundHeroData;

    public event Action<CharacterHealth> Changed;
    public event Action<CharacterHealth> Died;

    public CharacterData CharacterData => characterData;
    public HeroDefinition PlayerHeroDefinition => playerHeroDefinition;
    public CharacterRuntimeStats RuntimeStats => runtimeStats;
    public bool IsDead => runtimeStats.currentHP <= 0f;
    public float NormalizedHealth => runtimeStats.maxHP <= 0f ? 0f : runtimeStats.currentHP / runtimeStats.maxHP;

    private void Awake()
    {
        Initialize();

        // Final Hero stats đã được lấy từ HeroDefinition + save trước khi restore
        // HP/Mana, nên dữ liệu Continue được clamp theo đúng giới hạn đã nâng cấp.
        if (IsPlayerHealth() && GetComponent<PlayerProgression>() == null)
        {
            gameObject.AddComponent<PlayerProgression>();
        }

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

    private void Start()
    {
        BindHeroProgression();
        ApplyHeroProgressionStats(restoreGainedCapacity: false);
    }

    private void OnEnable()
    {
        BindHeroProgression();
    }

    private void OnDisable()
    {
        if (boundHeroData != null)
        {
            boundHeroData.HeroProgressChanged -= HandleHeroProgressChanged;
            boundHeroData = null;
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
            PlayerAudioController audioController =
                GetComponentInParent<PlayerAudioController>();
            audioController?.PlayHurtSound();

            if (triggerHitReaction && !IsDead)
            {
                PlayerCombatController combatController =
                    GetComponentInParent<PlayerCombatController>();
                bool isUsingSpecialSkill =
                    combatController != null &&
                    combatController.IsUsingSpecialSkill;

                // Ba skill Q/E/R có hyper armor: vẫn nhận damage nhưng không bị
                // animation hit ngắt chiêu. Đánh thường vẫn nhận hit reaction.
                if (!isUsingSpecialSkill)
                {
                    PlayerAnimatorBridge animatorBridge =
                        GetComponentInParent<PlayerAnimatorBridge>();
                    if (animatorBridge != null)
                    {
                        animatorBridge.TriggerHit();
                    }

                    PlayerController playerController =
                        GetComponentInParent<PlayerController>();
                    if (playerController != null)
                    {
                        playerController.LockMovementForHit();
                    }
                }
            }
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

    public void SetCurrentHealthForDebug(float health)
    {
        if (runtimeStats == null)
        {
            return;
        }

        runtimeStats.currentHP =
            Mathf.Clamp(health, 1f, runtimeStats.maxHP);
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

    public bool DrainEnergy(float amount)
    {
        if (amount <= 0f || runtimeStats == null || runtimeStats.currentEnergy <= 0f)
        {
            return false;
        }

        runtimeStats.currentEnergy = Mathf.Max(0f, runtimeStats.currentEnergy - amount);
        Changed?.Invoke(this);
        return runtimeStats.currentEnergy > 0f;
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
        CharacterBaseStats source = fallbackBaseStats;
        if (initializeFromCharacterData && characterData != null && characterData.baseStats != null)
        {
            source = characterData.baseStats;
        }

        if (source == null)
        {
            source = new CharacterBaseStats();
            fallbackBaseStats = source;
        }

        if (!IsPlayerHealth() || playerHeroDefinition == null)
        {
            return source;
        }

        CharacterBaseStats result = CopyBaseStats(source);
        GameDataManager data = GameDataManager.Instance;
        result.maxHP = GetHeroFinalStat(data, HeroStatType.Health);
        result.attack = GetHeroFinalStat(data, HeroStatType.Damage);
        result.defense = GetHeroFinalStat(data, HeroStatType.Defense);
        result.moveSpeed = GetHeroFinalStat(data, HeroStatType.MoveSpeed);
        result.energyMax = GetHeroFinalStat(data, HeroStatType.Mana);
        return result;
    }

    private void BindHeroProgression()
    {
        if (!IsPlayerHealth() || playerHeroDefinition == null || boundHeroData == GameDataManager.Instance)
        {
            return;
        }

        if (boundHeroData != null)
        {
            boundHeroData.HeroProgressChanged -= HandleHeroProgressChanged;
        }

        boundHeroData = GameDataManager.Instance;
        if (boundHeroData != null)
        {
            boundHeroData.HeroProgressChanged += HandleHeroProgressChanged;
        }
    }

    private void HandleHeroProgressChanged()
    {
        ApplyHeroProgressionStats(restoreGainedCapacity: true);
    }

    public void ApplyHeroProgressionStats(bool restoreGainedCapacity)
    {
        if (!IsPlayerHealth() || playerHeroDefinition == null || runtimeStats == null)
        {
            return;
        }

        BindHeroProgression();

        float previousMaxHealth = runtimeStats.maxHP;
        float previousMaxMana = runtimeStats.energyMax;
        GameDataManager data = GameDataManager.Instance;

        runtimeStats.maxHP = GetHeroFinalStat(data, HeroStatType.Health);
        runtimeStats.attack = GetHeroFinalStat(data, HeroStatType.Damage);
        runtimeStats.defense = GetHeroFinalStat(data, HeroStatType.Defense);
        runtimeStats.moveSpeed = GetHeroFinalStat(data, HeroStatType.MoveSpeed);
        runtimeStats.energyMax = GetHeroFinalStat(data, HeroStatType.Mana);

        float gainedHealth = Mathf.Max(0f, runtimeStats.maxHP - previousMaxHealth);
        float gainedMana = Mathf.Max(0f, runtimeStats.energyMax - previousMaxMana);
        runtimeStats.currentHP = restoreGainedCapacity
            ? Mathf.Min(runtimeStats.maxHP, runtimeStats.currentHP + gainedHealth)
            : Mathf.Clamp(runtimeStats.currentHP, 0f, runtimeStats.maxHP);
        runtimeStats.currentEnergy = restoreGainedCapacity
            ? Mathf.Min(runtimeStats.energyMax, runtimeStats.currentEnergy + gainedMana)
            : Mathf.Clamp(runtimeStats.currentEnergy, 0f, runtimeStats.energyMax);

        Changed?.Invoke(this);
    }

    public void ConfigurePlayerHero(HeroDefinition definition, bool preserveVitalRatios)
    {
        if (definition == null || !IsPlayerHealth()) return;

        float healthRatio = runtimeStats == null || runtimeStats.maxHP <= 0f
            ? 1f : Mathf.Clamp01(runtimeStats.currentHP / runtimeStats.maxHP);
        float manaRatio = runtimeStats == null || runtimeStats.energyMax <= 0f
            ? 1f : Mathf.Clamp01(runtimeStats.currentEnergy / runtimeStats.energyMax);

        playerHeroDefinition = definition;
        ApplyHeroProgressionStats(restoreGainedCapacity: false);
        if (preserveVitalRatios && runtimeStats != null)
        {
            runtimeStats.currentHP = runtimeStats.maxHP * healthRatio;
            runtimeStats.currentEnergy = runtimeStats.energyMax * manaRatio;
            Changed?.Invoke(this);
        }
    }

    public void ApplyVitalRatios(float healthRatio, float manaRatio, float staminaRatio)
    {
        if (runtimeStats == null) return;
        runtimeStats.currentHP = runtimeStats.maxHP * Mathf.Clamp01(healthRatio);
        runtimeStats.currentEnergy = runtimeStats.energyMax * Mathf.Clamp01(manaRatio);
        runtimeStats.currentStamina = runtimeStats.staminaMax * Mathf.Clamp01(staminaRatio);
        Changed?.Invoke(this);
    }

    private float GetHeroFinalStat(GameDataManager data, HeroStatType statType)
    {
        return data != null
            ? data.GetHeroFinalStat(playerHeroDefinition, statType)
            : playerHeroDefinition.GetBaseStat(statType);
    }

    private static CharacterBaseStats CopyBaseStats(CharacterBaseStats source)
    {
        source = source ?? new CharacterBaseStats();
        return new CharacterBaseStats
        {
            maxHP = source.maxHP,
            attack = source.attack,
            defense = source.defense,
            critRate = source.critRate,
            critDamage = source.critDamage,
            moveSpeed = source.moveSpeed,
            attackSpeed = source.attackSpeed,
            staminaMax = source.staminaMax,
            staminaRegen = source.staminaRegen,
            energyMax = source.energyMax,
            energyRegen = source.energyRegen,
            cooldownReduction = source.cooldownReduction,
            companionSynergy = source.companionSynergy,
            statusResistance = source.statusResistance
        };
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
