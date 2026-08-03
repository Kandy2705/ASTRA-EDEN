using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD_PlayerStatusPanel: HP / Energy / EXP + level và name.
/// Prefab không serialize được CharacterHealth của player → tự tìm theo tag "Player".
/// </summary>
public class CharacterStatsHUD : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Để trống = tự tìm CharacterHealth trên object tag Player.")]
    [SerializeField] private CharacterHealth characterHealth;
    [SerializeField] private bool autoFindPlayer = true;
    [SerializeField, Min(0.1f)] private float rebindInterval = 0.5f;

    [Header("Bars")]
    [SerializeField] private Image hpFill;
    [SerializeField] private Image staminaFill;
    [SerializeField] private Image energyFill;
    [SerializeField] private Image experienceFill;

    [Header("Texts")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text attackText;
    [SerializeField] private TMP_Text defenseText;
    [SerializeField] private TMP_Text critText;
    [SerializeField] private TMP_Text staminaText;
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private TMP_Text moveSpeedText;
    [SerializeField] private TMP_Text attackSpeedText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text experienceText;

    float rebindTimer;
    PlayerProgression progression;

    private void Awake()
    {
        TryBindPlayerHealth(force: true);
    }

    private void OnEnable()
    {
        TryBindPlayerHealth(force: false);
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (!autoFindPlayer)
        {
            return;
        }

        // Player spawn trễ / đổi scene / DontDestroy → rebind.
        if (characterHealth == null || !characterHealth.isActiveAndEnabled)
        {
            rebindTimer -= Time.unscaledDeltaTime;
            if (rebindTimer <= 0f)
            {
                rebindTimer = rebindInterval;
                if (TryBindPlayerHealth(force: true))
                {
                    Refresh();
                }
            }
        }
    }

    public void SetCharacterHealth(CharacterHealth newCharacterHealth)
    {
        Unsubscribe();
        characterHealth = newCharacterHealth;
        progression = characterHealth != null
            ? characterHealth.GetComponent<PlayerProgression>() ??
              characterHealth.GetComponentInParent<PlayerProgression>()
            : null;
        Subscribe();
        Refresh();
    }

    /// <summary>Trả về true nếu đã có / vừa bind được CharacterHealth.</summary>
    public bool TryBindPlayerHealth(bool force)
    {
        if (!force && characterHealth != null)
        {
            return true;
        }

        if (!autoFindPlayer && characterHealth == null)
        {
            return false;
        }

        CharacterHealth found = FindPlayerCharacterHealth();
        if (found == null)
        {
            return characterHealth != null;
        }

        if (characterHealth == found)
        {
            return true;
        }

        SetCharacterHealth(found);
        return true;
    }

    static CharacterHealth FindPlayerCharacterHealth()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            return null;
        }

        CharacterHealth health = player.GetComponent<CharacterHealth>();
        if (health != null)
        {
            return health;
        }

        return player.GetComponentInChildren<CharacterHealth>(true)
               ?? player.GetComponentInParent<CharacterHealth>();
    }

    public void Refresh()
    {
        if (characterHealth == null || characterHealth.RuntimeStats == null)
        {
            return;
        }

        if (progression == null)
        {
            progression = characterHealth.GetComponent<PlayerProgression>() ??
                          characterHealth.GetComponentInParent<PlayerProgression>();
            if (progression != null)
            {
                progression.Changed -= HandleProgressionChanged;
                progression.Changed += HandleProgressionChanged;
            }
        }

        CharacterRuntimeStats stats = characterHealth.RuntimeStats;
        SetFill(hpFill, stats.currentHP, stats.maxHP);
        SetFill(staminaFill, stats.currentStamina, stats.staminaMax);
        SetFill(energyFill, stats.currentEnergy, stats.energyMax);
        if (progression != null)
        {
            if (experienceFill != null)
            {
                experienceFill.fillAmount = progression.NormalizedExperience;
            }

            SetText(
                experienceText,
                progression.Level >= progression.MaxLevel
                    ? "MAX"
                    : $"{progression.CurrentExperience} / {progression.ExperienceToNextLevel}");
            SetText(levelText, progression.Level.ToString());
        }
        else
        {
            if (experienceFill != null)
            {
                experienceFill.fillAmount = 0f;
            }
            SetText(experienceText, "0 / 100");
        }

        string displayName = characterHealth.CharacterData != null
            ? characterHealth.CharacterData.displayName
            : characterHealth.name;
        SetText(nameText, displayName);
        SetText(hpText, $"{Mathf.CeilToInt(stats.currentHP)} / {Mathf.CeilToInt(stats.maxHP)}");
        SetText(attackText, $"ATK {stats.attack:0}");
        SetText(defenseText, $"DEF {stats.defense:0}");
        SetText(critText, $"CRIT {stats.critRate:P0} / {stats.critDamage:P0}");
        SetText(staminaText, $"{Mathf.CeilToInt(stats.currentStamina)} / {Mathf.CeilToInt(stats.staminaMax)}");
        SetText(energyText, $"{Mathf.CeilToInt(stats.currentEnergy)} / {Mathf.CeilToInt(stats.energyMax)}");
        SetText(moveSpeedText, $"Move {stats.moveSpeed:0.0}");
        SetText(attackSpeedText, $"ASPD {stats.attackSpeed:0.0}");
    }

    private void Subscribe()
    {
        if (characterHealth != null)
        {
            characterHealth.Changed -= HandleHealthChanged;
            characterHealth.Changed += HandleHealthChanged;
        }

        if (progression != null)
        {
            progression.Changed -= HandleProgressionChanged;
            progression.Changed += HandleProgressionChanged;
        }
    }

    private void Unsubscribe()
    {
        if (characterHealth != null)
        {
            characterHealth.Changed -= HandleHealthChanged;
        }

        if (progression != null)
        {
            progression.Changed -= HandleProgressionChanged;
        }
    }

    private void HandleHealthChanged(CharacterHealth changedHealth)
    {
        Refresh();
    }

    private void HandleProgressionChanged(PlayerProgression changedProgression)
    {
        Refresh();
    }

    private static void SetFill(Image image, float current, float max)
    {
        if (image != null)
        {
            image.fillAmount = max <= 0f ? 0f : Mathf.Clamp01(current / max);
        }
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }
}
