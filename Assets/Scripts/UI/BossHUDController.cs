using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BossHUDController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text bossNameText;
    [SerializeField] private Image healthFill;
    [SerializeField] private TMP_Text healthText;

    CharacterHealth boundHealth;
    string boundName;

    void Awake()
    {
        SetVisible(false);
    }

    public void BindBoss(string displayName, CharacterHealth health)
    {
        if (health == null)
        {
            return;
        }

        if (boundHealth != null)
        {
            boundHealth.Changed -= HandleHealthChanged;
            boundHealth.Died -= HandleDied;
        }

        boundHealth = health;
        boundName = displayName;

        boundHealth.Changed += HandleHealthChanged;
        boundHealth.Died += HandleDied;

        if (bossNameText != null)
        {
            bossNameText.text = displayName;
        }

        SetVisible(true);
        RefreshHealth();
    }

    public void ClearBoss()
    {
        if (boundHealth != null)
        {
            boundHealth.Changed -= HandleHealthChanged;
            boundHealth.Died -= HandleDied;
        }

        boundHealth = null;
        boundName = string.Empty;
        SetVisible(false);
    }

    void HandleHealthChanged(CharacterHealth _)
    {
        RefreshHealth();
    }

    void HandleDied(CharacterHealth _)
    {
        ClearBoss();
    }

    void RefreshHealth()
    {
        if (boundHealth == null || boundHealth.RuntimeStats == null)
        {
            return;
        }

        float current = boundHealth.RuntimeStats.currentHP;
        float max = Mathf.Max(1f, boundHealth.RuntimeStats.maxHP);
        float norm = current / max;

        if (healthFill != null)
        {
            healthFill.fillAmount = norm;
        }

        if (healthText != null)
        {
            healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }
    }

    void SetVisible(bool visible)
    {
        if (root != null)
        {
            root.SetActive(visible);
        }
    }
}