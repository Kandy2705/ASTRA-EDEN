using TMPro;
using UnityEngine;

/// <summary>
/// Binds Overview stat values to the current player.
/// Range and Target remain display metadata because CharacterRuntimeStats does not expose them.
/// </summary>
[DisallowMultipleComponent]
public sealed class OverviewPlayerStatsController : MonoBehaviour
{
    [SerializeField] private bool autoFindPlayer = true;
    [SerializeField, Min(0.1f)] private float rebindInterval = 0.5f;

    private CharacterHealth characterHealth;
    private TMP_Text hpValue;
    private TMP_Text damageValue;
    private TMP_Text defenseValue;
    private float rebindTimer;

    private void Awake()
    {
        CacheStatTexts();
        TryBindPlayer(true);
    }

    private void OnEnable()
    {
        CacheStatTexts();
        TryBindPlayer(false);
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (!autoFindPlayer || characterHealth != null)
        {
            return;
        }

        rebindTimer -= Time.unscaledDeltaTime;
        if (rebindTimer <= 0f)
        {
            rebindTimer = rebindInterval;
            if (TryBindPlayer(true))
            {
                Refresh();
            }
        }
    }

    private void CacheStatTexts()
    {
        hpValue = FindValueForLabel("HP");
        damageValue = FindValueForLabel("Dmg");
        defenseValue = FindValueForLabel("Def");
    }

    private TMP_Text FindValueForLabel(string labelText)
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text label = texts[i];
            if (label == null || !string.Equals(label.text.Trim(), labelText, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            TMP_Text[] siblings = label.transform.parent != null
                ? label.transform.parent.GetComponentsInChildren<TMP_Text>(true)
                : texts;
            for (int j = 0; j < siblings.Length; j++)
            {
                if (siblings[j] != null && siblings[j] != label)
                {
                    return siblings[j];
                }
            }
        }

        return null;
    }

    private bool TryBindPlayer(bool force)
    {
        if (!force && characterHealth != null)
        {
            return true;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        CharacterHealth found = player != null
            ? player.GetComponent<CharacterHealth>() ?? player.GetComponentInChildren<CharacterHealth>(true)
            : null;
        if (found == null || found == characterHealth)
        {
            return characterHealth != null;
        }

        Unsubscribe();
        characterHealth = found;
        Subscribe();
        Refresh();
        return true;
    }

    private void Subscribe()
    {
        if (characterHealth != null)
        {
            characterHealth.Changed -= HandleStatsChanged;
            characterHealth.Changed += HandleStatsChanged;
        }
    }

    private void Unsubscribe()
    {
        if (characterHealth != null)
        {
            characterHealth.Changed -= HandleStatsChanged;
        }
    }

    private void HandleStatsChanged(CharacterHealth _)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (characterHealth == null || characterHealth.RuntimeStats == null)
        {
            return;
        }

        CharacterRuntimeStats stats = characterHealth.RuntimeStats;
        SetText(hpValue, Mathf.CeilToInt(stats.maxHP).ToString());
        SetText(damageValue, Mathf.CeilToInt(stats.attack).ToString());
        SetText(defenseValue, Mathf.CeilToInt(stats.defense).ToString());
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }
}
