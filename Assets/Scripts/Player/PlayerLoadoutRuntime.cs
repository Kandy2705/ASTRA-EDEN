using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterHealth))]
public sealed class PlayerLoadoutRuntime : MonoBehaviour
{
    [Header("Definitions")]
    [SerializeField] private SpawnLoadoutCatalog catalog;
    [SerializeField] private HeroDefinition heroDefinition;

    [Header("Standard Weapon Sockets")]
    [SerializeField] private Transform rightHandSocket;
    [SerializeField] private Transform leftHandSocket;
    [SerializeField] private Transform backSocket;
    [SerializeField] private GameObject builtInWeaponVisual;

    private CharacterHealth characterHealth;
    private GameObject spawnedWeapon;
    private WeaponData equippedWeapon;
    private bool skipNextSceneRestore;

    public static PlayerLoadoutRuntime Active { get; private set; }
    public static event Action<PlayerLoadoutRuntime> ActivePlayerChanged;

    public SpawnLoadoutCatalog Catalog => catalog;
    public HeroDefinition HeroDefinition => heroDefinition;
    public WeaponData EquippedWeapon => equippedWeapon;
    public float BasicAttackDamageBonusPercent => equippedWeapon == null ? 0f :
        equippedWeapon.GetBasicAttackBonusPercent(GetWeaponUpgradeLevel());
    public float SkillDamageBonusPercent => equippedWeapon == null ? 0f :
        equippedWeapon.GetSkillDamageBonusPercent(GetWeaponUpgradeLevel());
    public bool SkipNextSceneRestore => skipNextSceneRestore;

    private void Awake()
    {
        characterHealth = GetComponent<CharacterHealth>();
    }

    private void OnEnable()
    {
        if (CompareTag("Player"))
        {
            Active = this;
            ActivePlayerChanged?.Invoke(this);
        }
    }

    private void Start()
    {
        ApplySavedLoadout();
    }

    private void OnDisable()
    {
        if (Active == this)
        {
            Active = null;
        }
    }

    public void Configure(SpawnLoadoutCatalog loadoutCatalog, HeroDefinition definition)
    {
        if (loadoutCatalog != null) catalog = loadoutCatalog;
        if (definition != null) heroDefinition = definition;
    }

    public void ApplySavedLoadout()
    {
        GameDataManager data = GameDataManager.Instance;
        if (catalog == null || data == null) return;

        HeroDefinition savedHero = catalog.ResolveHero(data.CurrentHeroId) ?? heroDefinition;
        WeaponData savedWeapon = catalog.ResolveValidWeapon(savedHero, data.CurrentWeaponId, data);
        if (savedHero != null && heroDefinition != null &&
            !string.Equals(savedHero.HeroId, heroDefinition.HeroId, StringComparison.Ordinal) &&
            savedWeapon != null && TryReplaceFromSave(savedHero, savedWeapon, data))
        {
            return;
        }

        if (savedHero != null)
        {
            heroDefinition = savedHero;
            characterHealth.ConfigurePlayerHero(savedHero, preserveVitalRatios: false);
        }

        savedWeapon = catalog.ResolveValidWeapon(heroDefinition, data.CurrentWeaponId, data);
        if (savedWeapon != null)
        {
            EquipWeapon(savedWeapon);
            if (!string.Equals(data.CurrentHeroId, heroDefinition.HeroId, StringComparison.Ordinal) ||
                !string.Equals(data.CurrentWeaponId, savedWeapon.weaponId, StringComparison.Ordinal))
            {
                data.SetCurrentLoadout(heroDefinition.HeroId, savedWeapon.weaponId);
            }
        }
    }

    private bool TryReplaceFromSave(HeroDefinition savedHero, WeaponData savedWeapon, GameDataManager data)
    {
        if (savedHero.GameplayPrefab == null) return false;

        Vector3 position = transform.position;
        Quaternion rotation = transform.rotation;
        gameObject.tag = "Untagged";
        SetGameplayEnabled(false);

        GameObject replacement = Instantiate(savedHero.GameplayPrefab, position, rotation);
        PlayerLoadoutRuntime runtime = replacement.GetComponent<PlayerLoadoutRuntime>();
        CharacterHealth health = replacement.GetComponent<CharacterHealth>();
        if (runtime == null || health == null)
        {
            Destroy(replacement);
            gameObject.tag = "Player";
            SetGameplayEnabled(true);
            return false;
        }

        replacement.tag = "Player";
        runtime.skipNextSceneRestore = true;
        runtime.Configure(catalog, savedHero);
        health.ConfigurePlayerHero(savedHero, preserveVitalRatios: false);
        if (data.HasPlayerData)
        {
            health.ApplySavedVitals(data.PlayerHP, data.PlayerStamina, data.PlayerEnergy);
        }
        if (!runtime.EquipWeapon(savedWeapon))
        {
            Destroy(replacement);
            gameObject.tag = "Player";
            SetGameplayEnabled(true);
            return false;
        }

        Active = runtime;
        ActivePlayerChanged?.Invoke(runtime);
        AutoSavePlayerPosition oldAutoSave = GetComponent<AutoSavePlayerPosition>();
        oldAutoSave?.SuppressNextDisableSave();
        Destroy(gameObject);
        return true;
    }

    public bool ConfirmLoadout(HeroDefinition selectedHero, WeaponData selectedWeapon)
    {
        GameDataManager data = GameDataManager.Instance;
        if (catalog == null || data == null || selectedHero == null || selectedWeapon == null ||
            !data.IsHeroOwned(selectedHero.HeroId) ||
            !catalog.IsAvailableForHero(selectedHero, selectedWeapon, data))
        {
            return false;
        }

        bool requiresReplacement = heroDefinition == null ||
            !string.Equals(heroDefinition.HeroId, selectedHero.HeroId, StringComparison.Ordinal);

        if (!requiresReplacement)
        {
            heroDefinition = selectedHero;
            characterHealth.ConfigurePlayerHero(selectedHero, preserveVitalRatios: true);
            if (!EquipWeapon(selectedWeapon)) return false;
            return data.SetCurrentLoadout(selectedHero.HeroId, selectedWeapon.weaponId);
        }

        GameObject prefab = selectedHero.GameplayPrefab;
        if (prefab == null) return false;

        CharacterRuntimeStats oldStats = characterHealth.RuntimeStats;
        float hpRatio = GetRatio(oldStats.currentHP, oldStats.maxHP);
        float manaRatio = GetRatio(oldStats.currentEnergy, oldStats.energyMax);
        float staminaRatio = GetRatio(oldStats.currentStamina, oldStats.staminaMax);
        Vector3 position = transform.position;
        Quaternion rotation = transform.rotation;

        gameObject.tag = "Untagged";
        SetGameplayEnabled(false);

        GameObject replacement = Instantiate(prefab, position, rotation);
        PlayerLoadoutRuntime replacementRuntime = replacement.GetComponent<PlayerLoadoutRuntime>();
        if (replacementRuntime == null)
        {
            Destroy(replacement);
            gameObject.tag = "Player";
            SetGameplayEnabled(true);
            Debug.LogError("[SpawnLoadout] Gameplay prefab cần PlayerLoadoutRuntime ở root.", prefab);
            return false;
        }

        replacement.tag = "Player";
        replacementRuntime.skipNextSceneRestore = true;
        replacementRuntime.Configure(catalog, selectedHero);
        CharacterHealth replacementHealth = replacement.GetComponent<CharacterHealth>();
        replacementHealth.ConfigurePlayerHero(selectedHero, preserveVitalRatios: false);
        replacementHealth.ApplyVitalRatios(hpRatio, manaRatio, staminaRatio);
        if (!replacementRuntime.EquipWeapon(selectedWeapon))
        {
            Destroy(replacement);
            gameObject.tag = "Player";
            SetGameplayEnabled(true);
            return false;
        }

        Active = replacementRuntime;
        ActivePlayerChanged?.Invoke(replacementRuntime);
        data.SetCurrentLoadout(selectedHero.HeroId, selectedWeapon.weaponId);
        CharacterRuntimeStats transferred = replacementHealth.RuntimeStats;
        data.SavePlayerStats(
            transferred.currentHP,
            transferred.currentStamina,
            transferred.currentEnergy);
        AutoSavePlayerPosition oldAutoSave = GetComponent<AutoSavePlayerPosition>();
        oldAutoSave?.SuppressNextDisableSave();
        Destroy(gameObject);
        return true;
    }

    public void ConsumeSceneRestoreSkip()
    {
        skipNextSceneRestore = false;
    }

    public bool EquipWeapon(WeaponData weapon)
    {
        if (weapon == null) return false;

        if (spawnedWeapon != null)
        {
            Destroy(spawnedWeapon);
            spawnedWeapon = null;
        }

        if (builtInWeaponVisual != null)
        {
            builtInWeaponVisual.SetActive(weapon.useBuiltInVisual);
        }

        if (!weapon.useBuiltInVisual)
        {
            Transform socket = ResolveSocket(weapon.socket);
            if (weapon.prefab == null || socket == null) return false;

            spawnedWeapon = Instantiate(weapon.prefab, socket, false);
            Transform weaponTransform = spawnedWeapon.transform;
            weaponTransform.localPosition = weapon.localPosition;
            weaponTransform.localRotation = Quaternion.Euler(weapon.localEulerAngles);
            weaponTransform.localScale = weapon.localScale == Vector3.zero ? Vector3.one : weapon.localScale;
        }

        equippedWeapon = weapon;
        return true;
    }

    private Transform ResolveSocket(WeaponSocket socket)
    {
        switch (socket)
        {
            case WeaponSocket.LeftHand: return leftHandSocket;
            case WeaponSocket.Back: return backSocket;
            default: return rightHandSocket;
        }
    }

    private int GetWeaponUpgradeLevel()
    {
        return equippedWeapon == null || GameDataManager.Instance == null
            ? 0
            : GameDataManager.Instance.GetWeaponUpgradeLevel(equippedWeapon.weaponId);
    }

    private void SetGameplayEnabled(bool value)
    {
        PlayerInputReader input = GetComponent<PlayerInputReader>();
        PlayerController controller = GetComponent<PlayerController>();
        PlayerCombatController combat = GetComponent<PlayerCombatController>();
        PlayerInteractController interact = GetComponent<PlayerInteractController>();
        if (input != null) input.enabled = value;
        if (controller != null) controller.enabled = value;
        if (combat != null) combat.enabled = value;
        if (interact != null) interact.enabled = value;
    }

    private static float GetRatio(float current, float maximum)
    {
        return maximum <= 0f ? 0f : Mathf.Clamp01(current / maximum);
    }
}
