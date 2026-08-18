using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterHealth))]
public sealed class PlayerLoadoutRuntime : MonoBehaviour
{
    [Header("Definitions")]
    [SerializeField] private SpawnLoadoutCatalog catalog;
    [SerializeField] private CharacterData heroDefinition;

    [Header("Standard Weapon Sockets")]
    [SerializeField] private Transform rightHandSocket;
    [SerializeField] private Transform leftHandSocket;
    [SerializeField] private Transform backSocket;
    [SerializeField] private GameObject builtInWeaponVisual;

    private CharacterHealth characterHealth;
    private GameObject spawnedWeapon;
    private GameObject runtimeVisual;
    private WeaponData equippedWeapon;
    private bool skipNextSceneRestore;
    private Animator baseAnimator;
    private Renderer[] baseRenderers;

    public static PlayerLoadoutRuntime Active { get; private set; }
    public static event Action<PlayerLoadoutRuntime> ActivePlayerChanged;

    public SpawnLoadoutCatalog Catalog => catalog;
    public CharacterData CharacterData => heroDefinition;
    public WeaponData EquippedWeapon => equippedWeapon;
    public float BasicAttackDamageBonusPercent => equippedWeapon == null ? 0f :
        equippedWeapon.GetBasicAttackBonusPercent(GetWeaponUpgradeLevel());
    public float SkillDamageBonusPercent => equippedWeapon == null ? 0f :
        equippedWeapon.GetSkillDamageBonusPercent(GetWeaponUpgradeLevel());
    public bool SkipNextSceneRestore => skipNextSceneRestore;

    private void Awake()
    {
        characterHealth = GetComponent<CharacterHealth>();
        baseAnimator = GetComponentInChildren<Animator>(true);
        baseRenderers = baseAnimator != null
            ? baseAnimator.GetComponentsInChildren<Renderer>(true)
            : GetComponentsInChildren<Renderer>(true);
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

    public void Configure(SpawnLoadoutCatalog loadoutCatalog, CharacterData definition)
    {
        if (loadoutCatalog != null) catalog = loadoutCatalog;
        if (definition != null) heroDefinition = definition;
    }

    public void ApplySavedLoadout()
    {
        GameDataManager data = GameDataManager.Instance;
        if (catalog == null || data == null) return;

        CharacterData savedHero = catalog.ResolveHero(data.CurrentHeroId) ?? heroDefinition;
        WeaponData savedWeapon = catalog.ResolveValidWeapon(savedHero, data.CurrentWeaponId, data);
        bool savedHeroDiffers = savedHero != null && heroDefinition != null &&
            !string.Equals(savedHero.HeroId, heroDefinition.HeroId, StringComparison.Ordinal);
        if (savedHeroDiffers && savedWeapon != null &&
            HasGameplayRuntime(savedHero.GameplayPrefab) &&
            TryReplaceFromSave(savedHero, savedWeapon, data))
        {
            return;
        }

        if (savedHeroDiffers && savedWeapon != null &&
            !HasGameplayRuntime(savedHero.GameplayPrefab) &&
            TryApplyVisualOnlyHero(savedHero, savedWeapon, preserveVitalRatios: false, persistLoadout: false))
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

    private bool TryReplaceFromSave(CharacterData savedHero, WeaponData savedWeapon, GameDataManager data)
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

    public bool ConfirmLoadout(CharacterData selectedHero, WeaponData selectedWeapon)
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

        if (!HasGameplayRuntime(selectedHero.GameplayPrefab))
        {
            return TryApplyVisualOnlyHero(
                selectedHero,
                selectedWeapon,
                preserveVitalRatios: true,
                persistLoadout: true);
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

        EnsureBuiltInVisualReference();
        bool canUseBuiltInVisual = runtimeVisual == null && weapon.useBuiltInVisual;
        if (builtInWeaponVisual != null)
        {
            builtInWeaponVisual.SetActive(canUseBuiltInVisual);
        }

        Transform socket = ResolveSocket(weapon.socket);
        if (socket != null)
        {
            for (int i = 0; i < socket.childCount; i++)
            {
                Transform child = socket.GetChild(i);
                if (child != null && (child.name == "MagicSword_Iron" || child.name.StartsWith("MagicSword")))
                {
                    child.gameObject.SetActive(canUseBuiltInVisual);
                }
            }
        }

        if (!canUseBuiltInVisual)
        {
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

    private void EnsureBuiltInVisualReference()
    {
        if (builtInWeaponVisual != null) return;
        Transform socket = rightHandSocket != null ? rightHandSocket : ResolveSocket(WeaponSocket.RightHand);
        if (socket != null)
        {
            Transform found = socket.Find("MagicSword_Iron");
            if (found == null)
            {
                for (int i = 0; i < socket.childCount; i++)
                {
                    Transform child = socket.GetChild(i);
                    if (child.name.StartsWith("MagicSword") || child.name.Contains("Sword"))
                    {
                        found = child;
                        break;
                    }
                }
            }
            if (found != null) builtInWeaponVisual = found.gameObject;
        }

        if (builtInWeaponVisual == null)
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == "MagicSword_Iron" || all[i].name.StartsWith("MagicSword"))
                {
                    builtInWeaponVisual = all[i].gameObject;
                    break;
                }
            }
        }
    }

    private bool TryApplyVisualOnlyHero(
        CharacterData selectedHero,
        WeaponData selectedWeapon,
        bool preserveVitalRatios,
        bool persistLoadout)
    {
        GameObject visualPrefab = selectedHero != null ? selectedHero.GameplayPrefab : null;
        if (visualPrefab == null || selectedWeapon == null)
        {
            return false;
        }

        Animator prefabAnimator = visualPrefab.GetComponentInChildren<Animator>(true);
        if (prefabAnimator == null)
        {
            Debug.LogError("[SpawnLoadout] Visual prefab cần có Animator để dùng shared Player runtime.", visualPrefab);
            return false;
        }

        // Visual-only Hero không có vũ khí built-in của Player gốc, vì vậy cần model vũ khí thật.
        if (selectedWeapon.prefab == null)
        {
            Debug.LogError("[SpawnLoadout] Visual-only Hero cần WeaponData có prefab để gắn vào hand socket.", selectedWeapon);
            return false;
        }

        GameObject newVisual = Instantiate(visualPrefab, transform, false);
        newVisual.name = $"RuntimeVisual_{selectedHero.HeroId}";
        newVisual.tag = "Untagged";
        newVisual.transform.localPosition = Vector3.zero;
        newVisual.transform.localRotation = Quaternion.identity;
        newVisual.transform.localScale = Vector3.one;
        SetLayerRecursive(newVisual, gameObject.layer);
        DisableVisualPhysics(newVisual);

        Animator visualAnimator = newVisual.GetComponentInChildren<Animator>(true);
        RuntimeAnimatorController controller = selectedHero.animatorController != null
            ? selectedHero.animatorController
            : baseAnimator != null ? baseAnimator.runtimeAnimatorController : null;
        visualAnimator.runtimeAnimatorController = controller;
        visualAnimator.applyRootMotion = false;
        visualAnimator.Rebind();
        visualAnimator.Update(0f);

        if (visualAnimator.GetComponent<PlayerAnimationEventRelay>() == null)
        {
            visualAnimator.gameObject.AddComponent<PlayerAnimationEventRelay>();
        }

        GameObject previousVisual = runtimeVisual;
        runtimeVisual = newVisual;
        SetBaseVisualVisible(false);
        if (baseAnimator != null) baseAnimator.enabled = false;

        PlayerAnimatorBridge bridge = GetComponent<PlayerAnimatorBridge>();
        bridge?.SetAnimator(visualAnimator);
        rightHandSocket = ResolveHumanoidBone(visualAnimator, HumanBodyBones.RightHand, "J_Bip_R_Hand") ?? newVisual.transform;
        leftHandSocket = ResolveHumanoidBone(visualAnimator, HumanBodyBones.LeftHand, "J_Bip_L_Hand") ?? newVisual.transform;
        backSocket = ResolveHumanoidBone(visualAnimator, HumanBodyBones.Chest, "J_Bip_C_Chest") ?? newVisual.transform;

        heroDefinition = selectedHero;
        characterHealth.ConfigurePlayerHero(selectedHero, preserveVitalRatios);
        if (!EquipWeapon(selectedWeapon))
        {
            Destroy(newVisual);
            runtimeVisual = previousVisual;
            return false;
        }

        if (previousVisual != null) Destroy(previousVisual);
        ActivePlayerChanged?.Invoke(this);

        if (persistLoadout)
        {
            GameDataManager data = GameDataManager.Instance;
            if (data == null || !data.SetCurrentLoadout(selectedHero.HeroId, selectedWeapon.weaponId))
            {
                return false;
            }

            CharacterRuntimeStats stats = characterHealth.RuntimeStats;
            data.SavePlayerStats(stats.currentHP, stats.currentStamina, stats.currentEnergy);
        }

        return true;
    }

    private static bool HasGameplayRuntime(GameObject prefab)
    {
        return prefab != null && prefab.GetComponent<PlayerLoadoutRuntime>() != null;
    }

    private void SetBaseVisualVisible(bool visible)
    {
        if (baseRenderers == null) return;
        for (int i = 0; i < baseRenderers.Length; i++)
        {
            if (baseRenderers[i] != null) baseRenderers[i].enabled = visible;
        }
    }

    private static void DisableVisualPhysics(GameObject visual)
    {
        Collider[] colliders = visual.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null) colliders[i].enabled = false;
        }

        Rigidbody[] rigidbodies = visual.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            if (rigidbodies[i] == null) continue;
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].detectCollisions = false;
        }
    }

    private static Transform FindBone(Transform root, string boneName)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == boneName) return all[i];
        }
        return null;
    }

    private static Transform ResolveHumanoidBone(
        Animator animator,
        HumanBodyBones humanoidBone,
        string fallbackName)
    {
        if (animator != null && animator.isHuman)
        {
            Transform bone = animator.GetBoneTransform(humanoidBone);
            if (bone != null) return bone;
        }

        return animator == null ? null : FindBone(animator.transform, fallbackName);
    }

    private static void SetLayerRecursive(GameObject root, int layer)
    {
        root.layer = layer;
        for (int i = 0; i < root.transform.childCount; i++)
            SetLayerRecursive(root.transform.GetChild(i).gameObject, layer);
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
