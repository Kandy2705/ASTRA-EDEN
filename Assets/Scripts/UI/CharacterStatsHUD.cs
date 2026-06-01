using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterStatsHUD : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private CharacterHealth characterHealth;

    [Header("Bars")]
    [SerializeField] private Image hpFill;
    [SerializeField] private Image staminaFill;
    [SerializeField] private Image energyFill;

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

    private void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public void SetCharacterHealth(CharacterHealth newCharacterHealth)
    {
        Unsubscribe();
        characterHealth = newCharacterHealth;
        Subscribe();
        Refresh();
    }

    public void Refresh()
    {
        if (characterHealth == null || characterHealth.RuntimeStats == null)
        {
            return;
        }

        CharacterRuntimeStats stats = characterHealth.RuntimeStats;
        SetFill(hpFill, stats.currentHP, stats.maxHP);
        SetFill(staminaFill, stats.currentStamina, stats.staminaMax);
        SetFill(energyFill, stats.currentEnergy, stats.energyMax);

        SetText(nameText, characterHealth.CharacterData != null ? characterHealth.CharacterData.displayName : characterHealth.name);
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
            characterHealth.Changed += HandleHealthChanged;
        }
    }

    private void Unsubscribe()
    {
        if (characterHealth != null)
        {
            characterHealth.Changed -= HandleHealthChanged;
        }
    }

    private void HandleHealthChanged(CharacterHealth changedHealth)
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
