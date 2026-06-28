using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillCooldownUI : MonoBehaviour
{
    [Header("Cooldown Fill Images (Radial360 dark overlay)")]
    [SerializeField] private Image[] cooldownFillImages;

    [Header("Cooldown Text (so giay con lai)")]
    [Tooltip("Optional. Cung index voi cooldownFillImages. Hien thi so giay con lai.")]
    [SerializeField] private TMP_Text[] cooldownTexts;
    [Tooltip("> threshold giay -> in nguyen (vd 3); <= threshold -> in 1 chu so thap phan (vd 0.8).")]
    [SerializeField] private float decimalThreshold = 3f;
    [Tooltip("Format khi cooldown > decimalThreshold. {0} = giay (int).")]
    [SerializeField] private string formatInt = "{0}";
    [Tooltip("Format khi cooldown <= decimalThreshold. {0} = giay (float 1 decimal).")]
    [SerializeField] private string formatDecimal = "{0:0.0}";

    [Header("Reference")]
    [SerializeField] private PlayerSkillCooldown cooldownManager;
    [SerializeField] private bool findOnPlayer = true;

    private void Start()
    {
        if (cooldownManager == null && findOnPlayer)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) cooldownManager = player.GetComponent<PlayerSkillCooldown>();
        }
    }

    private void Update()
    {
        if (cooldownManager == null) return;

        int fillCount = cooldownFillImages != null ? cooldownFillImages.Length : 0;
        int textCount = cooldownTexts != null ? cooldownTexts.Length : 0;
        int max = Mathf.Max(fillCount, textCount);
        max = Mathf.Min(max, cooldownManager.SlotCount);

        for (int i = 0; i < max; i++)
        {
            bool onCd = cooldownManager.IsOnCooldown(i);
            float remaining = cooldownManager.GetRemaining(i);
            float total = cooldownManager.GetTotal(i);

            if (i < fillCount && cooldownFillImages[i] != null)
            {
                if (onCd)
                {
                    cooldownFillImages[i].fillAmount = total > 0f ? remaining / total : 0f;
                    if (!cooldownFillImages[i].gameObject.activeSelf)
                        cooldownFillImages[i].gameObject.SetActive(true);
                }
                else
                {
                    cooldownFillImages[i].fillAmount = 0f;
                }
            }

            if (i < textCount && cooldownTexts[i] != null)
            {
                if (onCd)
                {
                    cooldownTexts[i].text = FormatRemaining(remaining);
                    if (!cooldownTexts[i].gameObject.activeSelf)
                        cooldownTexts[i].gameObject.SetActive(true);
                }
                else if (cooldownTexts[i].gameObject.activeSelf)
                {
                    cooldownTexts[i].gameObject.SetActive(false);
                }
            }
        }
    }

    private string FormatRemaining(float remaining)
    {
        if (remaining > decimalThreshold)
        {
            int rounded = Mathf.CeilToInt(remaining);
            return string.Format(formatInt, rounded);
        }
        return string.Format(formatDecimal, remaining);
    }
}
