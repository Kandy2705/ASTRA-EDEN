using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyHUDRange : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private CharacterHealth characterHealth;
    [SerializeField] private GameObject enemyHUD;
    [SerializeField] private Image healthFill;
    // [SerializeField] private GameObject targetReticle;
    [SerializeField] private TMP_Text healthText;

    [Header("Range Settings")]
    [SerializeField] private float showDistance = 5f;
    [SerializeField] private float targetDistance = 3f;

    [Header("Billboard")]
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private bool copyCameraRotation = true;

    [Header("Performance")]
    [Tooltip("Giảm tần suất check khoảng cách + bật/tắt HUD.")]
    [SerializeField, Min(0.05f)] private float rangeCheckInterval = 0.12f;

    private Camera mainCamera;
    private float showDistanceSqr;
    private float rangeCheckTimer;
    private bool hudVisible;

    private void Awake()
    {
        if (characterHealth == null)
        {
            characterHealth = GetComponent<CharacterHealth>();
        }

        mainCamera = Camera.main;
        showDistanceSqr = showDistance * showDistance;
        FindPlayerIfMissing();
    }

    private void OnEnable()
    {
        if (characterHealth != null)
        {
            characterHealth.Changed += HandleHealthChanged;
            characterHealth.Died += HandleDied;
        }

        RefreshHealth();
        SetHUDVisible(false);
    }

    private void OnDisable()
    {
        if (characterHealth != null)
        {
            characterHealth.Changed -= HandleHealthChanged;
            characterHealth.Died -= HandleDied;
        }
    }

    private void LateUpdate()
    {
        if (characterHealth != null && characterHealth.IsDead)
        {
            SetHUDVisible(false);
            hudVisible = false;
            return;
        }

        if (player == null)
        {
            FindPlayerIfMissing();
        }

        if (player == null || enemyHUD == null)
        {
            return;
        }

        rangeCheckTimer -= Time.deltaTime;
        if (rangeCheckTimer <= 0f)
        {
            rangeCheckTimer = rangeCheckInterval;
            Vector3 offset = player.position - transform.position;
            offset.y = 0f;
            bool shouldShow = offset.sqrMagnitude <= showDistanceSqr;
            SetHUDVisible(shouldShow);
            hudVisible = shouldShow;
        }

        if (!hudVisible)
        {
            return;
        }

        if (faceCamera)
        {
            FaceCamera();
        }
    }

    public void SetCharacterHealth(CharacterHealth newCharacterHealth)
    {
        if (characterHealth != null)
        {
            characterHealth.Changed -= HandleHealthChanged;
            characterHealth.Died -= HandleDied;
        }

        characterHealth = newCharacterHealth;

        if (characterHealth != null)
        {
            characterHealth.Changed += HandleHealthChanged;
            characterHealth.Died += HandleDied;
        }

        RefreshHealth();
    }

    private void RefreshHealth()
    {
        if (characterHealth == null || characterHealth.RuntimeStats == null)
        {
            SetFill(healthFill, 0f);
            SetHealthText(0f, 0f);
            return;
        }

        CharacterRuntimeStats stats = characterHealth.RuntimeStats;
        float normalizedHealth = stats.maxHP <= 0f ? 0f : stats.currentHP / stats.maxHP;
        SetFill(healthFill, normalizedHealth);
        SetHealthText(stats.currentHP, stats.maxHP);
    }

    private void FaceCamera()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null || enemyHUD == null)
        {
            return;
        }

        if (copyCameraRotation)
        {
            enemyHUD.transform.rotation = mainCamera.transform.rotation;
        }
        else
        {
            Vector3 direction = enemyHUD.transform.position - mainCamera.transform.position;
            if (direction.sqrMagnitude > 0.001f)
            {
                enemyHUD.transform.rotation = Quaternion.LookRotation(direction);
            }
        }
    }

    private void FindPlayerIfMissing()
    {
        if (player != null)
        {
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }

    private void HandleHealthChanged(CharacterHealth changedHealth)
    {
        RefreshHealth();
    }

    private void HandleDied(CharacterHealth deadHealth)
    {
        SetHUDVisible(false);
    }

    private void SetHUDVisible(bool visible)
    {
        hudVisible = visible;
        if (enemyHUD != null && enemyHUD.activeSelf != visible)
        {
            enemyHUD.SetActive(visible);
        }

        // if (!visible && targetReticle != null && targetReticle.activeSelf)
        // {
        //     targetReticle.SetActive(false);
        // }
    }

    private static void SetFill(Image image, float normalizedValue)
    {
        if (image != null)
        {
            image.fillAmount = Mathf.Clamp01(normalizedValue);
        }
    }

    private void SetHealthText(float currentHP, float maxHP)
    {
        if (healthText != null)
        {
            healthText.text = $"{Mathf.CeilToInt(currentHP)} / {Mathf.CeilToInt(maxHP)}";
        }
    }
}
