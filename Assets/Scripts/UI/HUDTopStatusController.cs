using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class HUDTopStatusController : MonoBehaviour
{
    private const string DayNightSceneName = "World_Eden7";

    [Header("Time")]
    [SerializeField] private TMP_Text timeText;

    [Tooltip("Giờ bắt đầu trong game khi scene load (0-23).")]
    [Range(0, 23)]
    [SerializeField] private int startHour = 6;

    [Range(0, 59)]
    [SerializeField] private int startMinute = 0;

    [Tooltip("1 ngày game = bao nhiêu giây thực. Mặc định 3600 = 1 giờ thực.")]
    [SerializeField] private float realSecondsPerGameDay = 3600f;

    [Header("Day / Night Lighting")]
    [SerializeField] private bool driveDayNightLighting = true;
    [Tooltip("Để trống sẽ dùng RenderSettings.sun hoặc tự tìm Directional Light đang bật.")]
    [SerializeField] private Light sunLight;
    [SerializeField] private Material morningSkybox;
    [SerializeField] private Material daySkybox;
    [SerializeField] private Material sunsetSkybox;
    [SerializeField] private Material nightSkybox;
    [SerializeField] private Material midnightSkybox;
    [Tooltip("Tại giờ này ánh sáng đạt mức ban ngày đầy đủ.")]
    [Range(6f, 12f)]
    [SerializeField] private float fullyBrightHour = 10f;
    [Tooltip("Từ giờ này trở đi là ban đêm.")]
    [Range(18f, 23f)]
    [SerializeField] private float nightStartsHour = 20f;
    [Tooltip("Cường độ ánh sáng thấp nhất vào ban đêm.")]
    [SerializeField, Range(0f, 1f)] private float minimumLightingIntensity = 0.1f;
    [Tooltip("Cường độ ánh sáng cao nhất vào ban ngày.")]
    [SerializeField, Range(0f, 1f)] private float maximumLightingIntensity = 1f;
    [Tooltip("Màu cố định của Directional Light trong toàn bộ chu kỳ ngày/đêm.")]
    [SerializeField] private Color lightingColor = new(1f, 0.9294118f, 0.7803922f, 1f);

    [Header("Network / FPS")]
    [SerializeField] private TMP_Text networkText;

    [Tooltip("Khoảng thời gian giữa 2 lần cập nhật ms (giây).")]
    [SerializeField] private float networkUpdateInterval = 0.5f;

    [Tooltip("Hệ số làm mượt frame time (0 = không mượt, 1 = đứng im).")]
    [Range(0f, 0.99f)]
    [SerializeField] private float frameTimeSmoothing = 0.9f;

    [Header("Currency")]
    [SerializeField] private TMP_Text currencyText;

    [Tooltip("Inventory của Player. Nếu để trống thì script tự tìm trong scene.")]
    [SerializeField] private PlayerInventoryService inventoryService;

    [Tooltip("Kéo SO_Item_Gold vào đây nếu muốn HUD hiển thị coin/gold.")]
    [SerializeField] private ItemData currencyItemData;

    private const float SecondsPerGameDay = 86400f;

    private float gameSecondsElapsed;
    private float smoothedFrameMs;
    private float networkUpdateTimer;
    private int lastDisplayedClockKey = -1;
    private int lastSkyboxPeriod = -1;
    private bool timeSyncedWithSave;
    private bool lightingCached;
    private Color dayFogColor;
    private float sunYaw;

    private void Awake()
    {
        gameSecondsElapsed = (startHour * 3600f) + (startMinute * 60f);
        SyncTimeFromSave();
        smoothedFrameMs = Time.unscaledDeltaTime * 1000f;

        if (inventoryService == null)
        {
            inventoryService = PlayerInventoryService.FindForPlayer();
        }

        RefreshTime();
        CacheLighting();
        UpdateDayNightLighting();
        RefreshNetwork();
        RefreshCurrency();
    }

    private void OnEnable()
    {
        if (inventoryService == null)
        {
            inventoryService = PlayerInventoryService.FindForPlayer();
        }

        if (inventoryService != null)
        {
            inventoryService.OnInventoryChanged += RefreshCurrency;
        }

        RefreshCurrency();
    }

    private void OnDisable()
    {
        if (inventoryService != null)
        {
            inventoryService.OnInventoryChanged -= RefreshCurrency;
        }

        SaveCurrentTime(true);
    }

    private void Update()
    {
        TickTime();
        TickNetwork();
    }

    private void TickTime()
    {
        if (!timeSyncedWithSave)
        {
            SyncTimeFromSave();
        }

        if (realSecondsPerGameDay <= 0f)
        {
            return;
        }

        float gameSecondsPerRealSecond = SecondsPerGameDay / realSecondsPerGameDay;
        gameSecondsElapsed = (gameSecondsElapsed + Time.deltaTime * gameSecondsPerRealSecond) % SecondsPerGameDay;

        RefreshTime();
        SaveCurrentTime(false);
        UpdateDayNightLighting();
    }

    private void TickNetwork()
    {
        float frameMs = Time.unscaledDeltaTime * 1000f;
        smoothedFrameMs = Mathf.Lerp(frameMs, smoothedFrameMs, frameTimeSmoothing);

        networkUpdateTimer += Time.unscaledDeltaTime;

        if (networkUpdateTimer >= networkUpdateInterval)
        {
            networkUpdateTimer = 0f;
            RefreshNetwork();
        }
    }

    public TimeSpan CurrentGameTime => TimeSpan.FromSeconds(gameSecondsElapsed);

    /// <summary>
    /// Chỉnh tốc độ chu kỳ ngày/đêm. 1x = bình thường (3600s thực / ngày game).
    /// Demo: 60x = 1 ngày game trong 60 giây thực.
    /// </summary>
    public void SetTimeMultiplier(float multiplier)
    {
        realSecondsPerGameDay = 3600f / Mathf.Max(0.01f, multiplier);
    }

    public void SetGameTime(int hour, int minute = 0, bool persist = true)
    {
        int safeHour = ((hour % 24) + 24) % 24;
        int safeMinute = Mathf.Clamp(minute, 0, 59);
        gameSecondsElapsed = safeHour * 3600f + safeMinute * 60f;
        lastDisplayedClockKey = -1;

        RefreshTime();
        SaveCurrentTime(persist);
        UpdateDayNightLighting();
    }

    private void SyncTimeFromSave()
    {
        GameDataManager data = GameDataManager.Instance;
        if (data == null)
        {
            return;
        }

        if (data.HasGameTime)
        {
            gameSecondsElapsed = Mathf.Repeat(data.GameTimeSeconds, SecondsPerGameDay);
        }
        else
        {
            data.UpdateGameTime(gameSecondsElapsed, true);
        }

        timeSyncedWithSave = true;
        lastDisplayedClockKey = -1;
    }

    private void SaveCurrentTime(bool forcePersist)
    {
        GameDataManager data = GameDataManager.Instance;
        if (data == null)
        {
            return;
        }

        data.UpdateGameTime(gameSecondsElapsed, forcePersist);
        if (forcePersist)
        {
            data.FlushPlayerPrefs();
        }
    }

    private void CacheLighting()
    {
        if (!ShouldDriveDayNightLighting() || lightingCached)
        {
            return;
        }

        if (sunLight == null)
        {
            sunLight = RenderSettings.sun;
        }

        if (sunLight == null)
        {
            Light[] lights = FindObjectsByType<Light>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            float strongestIntensity = -1f;
            for (int i = 0; i < lights.Length; i++)
            {
                Light candidate = lights[i];
                if (candidate.type != LightType.Directional ||
                    !candidate.isActiveAndEnabled ||
                    candidate.intensity <= strongestIntensity)
                {
                    continue;
                }

                strongestIntensity = candidate.intensity;
                sunLight = candidate;
            }
        }

        if (sunLight != null)
        {
            RenderSettings.sun = sunLight;
            sunYaw = sunLight.transform.eulerAngles.y;
        }

        dayFogColor = RenderSettings.fogColor;
        lightingCached = true;
    }

    private void UpdateDayNightLighting()
    {
        if (!ShouldDriveDayNightLighting())
        {
            return;
        }

        CacheLighting();

        float hour = gameSecondsElapsed / 3600f;
        float daylight = CalculateDaylight(hour);
        float minimum = Mathf.Min(minimumLightingIntensity, maximumLightingIntensity);
        float maximum = Mathf.Max(minimumLightingIntensity, maximumLightingIntensity);
        float lightingIntensity = Mathf.Lerp(minimum, maximum, daylight);

        if (sunLight != null)
        {
            float solarAngle = (hour - 6f) * 15f;
            sunLight.transform.rotation = Quaternion.Euler(solarAngle, sunYaw, 0f);
            sunLight.intensity = lightingIntensity;
            sunLight.color = lightingColor;
        }

        RenderSettings.ambientIntensity = lightingIntensity;
        RenderSettings.reflectionIntensity = lightingIntensity;
        RenderSettings.fogColor = Color.Lerp(
            new Color(0.025f, 0.045f, 0.09f, 1f),
            dayFogColor,
            daylight);

        ApplySkyboxForHour(hour);
    }

    private bool ShouldDriveDayNightLighting()
    {
        return driveDayNightLighting &&
               SceneManager.GetActiveScene().name == DayNightSceneName;
    }

    private float CalculateDaylight(float hour)
    {
        const float dawnStartsHour = 6f;
        const float sunsetStartsHour = 18f;

        if (hour < dawnStartsHour || hour >= nightStartsHour)
        {
            return 0f;
        }

        if (hour < fullyBrightHour)
        {
            return Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(dawnStartsHour, fullyBrightHour, hour));
        }

        if (hour < sunsetStartsHour)
        {
            return 1f;
        }

        return Mathf.SmoothStep(
            1f,
            0f,
            Mathf.InverseLerp(sunsetStartsHour, nightStartsHour, hour));
    }

    private void ApplySkyboxForHour(float hour)
    {
        int period;
        Material target;

        if (hour < 5f)
        {
            period = 0;
            target = midnightSkybox != null ? midnightSkybox : nightSkybox;
        }
        else if (hour < fullyBrightHour)
        {
            period = 1;
            target = morningSkybox;
        }
        else if (hour < 17f)
        {
            period = 2;
            target = daySkybox;
        }
        else if (hour < nightStartsHour)
        {
            period = 3;
            target = sunsetSkybox;
        }
        else
        {
            period = 4;
            target = nightSkybox;
        }

        if (period == lastSkyboxPeriod)
        {
            return;
        }

        lastSkyboxPeriod = period;
        if (target == null || RenderSettings.skybox == target)
        {
            return;
        }

        RenderSettings.skybox = target;
        DynamicGI.UpdateEnvironment();
    }

    private void RefreshTime()
    {
        if (timeText == null)
        {
            return;
        }

        int totalSeconds = Mathf.FloorToInt(gameSecondsElapsed);
        int hour24 = (totalSeconds / 3600) % 24;
        int minute = (totalSeconds / 60) % 60;
        int clockKey = (hour24 * 60) + minute;
        if (clockKey == lastDisplayedClockKey)
        {
            return;
        }

        lastDisplayedClockKey = clockKey;

        int hour12 = hour24 % 12;
        if (hour12 == 0)
        {
            hour12 = 12;
        }

        string suffix = hour24 < 12 ? "AM" : "PM";
        timeText.text = $"{hour12:00}:{minute:00} {suffix}";
    }

    private void RefreshNetwork()
    {
        if (networkText == null)
        {
            return;
        }

        networkText.text = $"{Mathf.RoundToInt(smoothedFrameMs)}ms";
    }

    /// <summary>Gọi từ GameplayUISceneBootstrap khi vào hub/camp (player spawn trễ).</summary>
    public void ForceRefreshCurrency()
    {
        RefreshCurrency();
    }

    private void RefreshCurrency()
    {
        if (currencyText == null)
        {
            return;
        }

        // Luôn ưu tiên inventory gold (SO_Item_Gold). Tìm lại service nếu scene camp load trễ.
        if (inventoryService == null)
        {
            inventoryService = PlayerInventoryService.FindForPlayer();
            if (inventoryService != null)
            {
                inventoryService.OnInventoryChanged -= RefreshCurrency;
                inventoryService.OnInventoryChanged += RefreshCurrency;
            }
        }

        if (inventoryService != null)
        {
            currencyText.text = inventoryService.GetGoldQuantity(currencyItemData).ToString("N0");
            return;
        }

        // Chưa có player/inventory trong scene (MainMenu) → mirror cũ hoặc 0.
        if (GameDataManager.Instance != null)
        {
            currencyText.text = GameDataManager.Instance.Currency.ToString("N0");
            return;
        }

        currencyText.text = "0";
    }
}
