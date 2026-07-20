using System;
using TMPro;
using UnityEngine;

public class HUDTopStatusController : MonoBehaviour
{
    [Header("Time")]
    [SerializeField] private TMP_Text timeText;

    [Tooltip("Giờ bắt đầu trong game khi scene load (0-23).")]
    [Range(0, 23)]
    [SerializeField] private int startHour = 6;

    [Range(0, 59)]
    [SerializeField] private int startMinute = 0;

    [Tooltip("1 ngày game = bao nhiêu giây thực. Mặc định 3600 = 1 giờ thực.")]
    [SerializeField] private float realSecondsPerGameDay = 3600f;

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

    private void Awake()
    {
        gameSecondsElapsed = (startHour * 3600f) + (startMinute * 60f);
        smoothedFrameMs = Time.unscaledDeltaTime * 1000f;

        if (inventoryService == null)
        {
            inventoryService = PlayerInventoryService.FindForPlayer();
        }

        RefreshTime();
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
    }

    private void Update()
    {
        TickTime();
        TickNetwork();
    }

    private void TickTime()
    {
        if (realSecondsPerGameDay <= 0f)
        {
            return;
        }

        float gameSecondsPerRealSecond = SecondsPerGameDay / realSecondsPerGameDay;
        gameSecondsElapsed = (gameSecondsElapsed + Time.deltaTime * gameSecondsPerRealSecond) % SecondsPerGameDay;

        RefreshTime();
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