using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class InventoryToggleController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private Key toggleKey = Key.B;

    [Header("UI")]
    [Tooltip("Canvas HUD gameplay trong scene World_Eden7.")]
    [SerializeField] private GameObject gameplayHudCanvas;

    [Tooltip("Root nội dung gameplay HUD bên trong HUD_Canvas.")]
    [SerializeField] private GameObject gameplayHudRoot;

    [Tooltip("Root của panel Inventory trong Menu_Canvas.")]
    [SerializeField] private GameObject inventoryRoot;

    [Tooltip("Root của panel Overview trong cùng Panels container với Inventory.")]
    [SerializeField] private GameObject overviewRoot;

    [Tooltip("Root của Hero.prefab trong cùng Panels container. Dùng đúng instance hiện có.")]
    [SerializeField] private GameObject heroRoot;
    [Tooltip("Root của SpawnLoadout.prefab trong Panels.")]
    [SerializeField] private GameObject spawnLoadoutRoot;

    private GameObject panelContainer;

    [Header("Inventory Refresh")]
    [SerializeField] private InventoryScreenController inventoryScreenController;

    [Header("Options")]
    [SerializeField] private bool pauseGameWhenOpen = true;
    [SerializeField] private bool showCursorWhenOpen = true;

    private bool isOpen;
    private bool isHeroOpen;
    private bool isHeroClosing;
    private bool isSpawnLoadoutOpen;
    private bool isSpawnLoadoutClosing;
    private GameDataManager subscribedGameData;
    private PopupTween heroPopupTween;
    private PopupTween spawnLoadoutPopupTween;
    private SpawnLoadoutView spawnLoadoutView;
    private Button overviewTabButton;
    private Button inventoryTabButton;
    private readonly List<GameObject> activatedAncestors = new List<GameObject>();

    public bool IsOpen => isOpen;
    public bool IsHeroOpen => isHeroOpen;
    public bool IsSpawnLoadoutOpen => isSpawnLoadoutOpen;

    private void Awake()
    {
        ResolveInventoryReferences();
        WireOverviewTab();
        WireInventoryTab();

        SetInventoryOpen(false);
    }

    private void OnEnable()
    {
        SubscribeHeroScreenRequests();
    }

    private void OnDisable()
    {
        if (subscribedGameData != null)
        {
            subscribedGameData.HeroScreenOpenRequested -= OpenHero;
            subscribedGameData.SpawnLoadoutScreenOpenRequested -= OpenSpawnLoadout;
            subscribedGameData = null;
        }
    }

    private void Start()
    {
        ResolveInventoryReferences();
        WireOverviewTab();
        WireInventoryTab();
    }

    private void ResolveInventoryReferences()
    {
        ResolveGameplayHud();

        if (inventoryScreenController == null)
        {
            inventoryScreenController = FindFirstObjectByType<InventoryScreenController>(FindObjectsInactive.Include);
        }

        GameObject panelRoot = FindInventoryPanelRoot();
        if (panelRoot != null && ShouldUseInventoryPanelRoot(inventoryRoot, panelRoot))
        {
            inventoryRoot = panelRoot;
        }

        if (inventoryScreenController == null && inventoryRoot != null)
        {
            inventoryScreenController = inventoryRoot.GetComponentInChildren<InventoryScreenController>(true);
        }

        ResolveOverviewRoot();
        ResolveHeroRoot();
        ResolveSpawnLoadoutRoot();
        if (inventoryRoot != null && inventoryRoot.transform.parent != null)
        {
            panelContainer = inventoryRoot.transform.parent.gameObject;
        }
    }

    private void ResolveSpawnLoadoutRoot()
    {
        if (spawnLoadoutRoot == null)
        {
            Transform container = inventoryRoot != null ? inventoryRoot.transform.parent : null;
            Transform found = container != null ? container.Find("SpawnLoadout") : null;
            spawnLoadoutRoot = found != null ? found.gameObject : FindPanelRoot("SpawnLoadout");
        }

        if (spawnLoadoutRoot == null) return;
        spawnLoadoutPopupTween ??= spawnLoadoutRoot.GetComponent<PopupTween>();
        if (spawnLoadoutPopupTween == null) spawnLoadoutPopupTween = spawnLoadoutRoot.AddComponent<PopupTween>();
        spawnLoadoutView ??= spawnLoadoutRoot.GetComponent<SpawnLoadoutView>();
        if (spawnLoadoutView != null)
        {
            spawnLoadoutView.CloseRequested -= CloseSpawnLoadout;
            spawnLoadoutView.CloseRequested += CloseSpawnLoadout;
        }
    }

    private void ResolveGameplayHud()
    {
        if (gameplayHudCanvas == null)
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas != null && canvas.name == "HUD_Canvas")
                {
                    gameplayHudCanvas = canvas.gameObject;
                    break;
                }
            }
        }

        if (gameplayHudRoot == null && gameplayHudCanvas != null)
        {
            Transform hudRoot = gameplayHudCanvas.transform.Find("HUD_Root");
            if (hudRoot != null)
            {
                gameplayHudRoot = hudRoot.gameObject;
            }
        }
    }

    private void ResolveOverviewRoot()
    {
        if (overviewRoot != null)
        {
            return;
        }

        Transform panelContainer = inventoryRoot != null ? inventoryRoot.transform.parent : null;
        if (panelContainer != null && panelContainer.name == "Panels")
        {
            Transform overview = panelContainer.Find("Overview");
            if (overview != null)
            {
                overviewRoot = overview.gameObject;
                return;
            }
        }

        overviewRoot = FindPanelRoot("Overview");
    }

    private void ResolveHeroRoot()
    {
        if (heroRoot == null)
        {
            Transform container = inventoryRoot != null ? inventoryRoot.transform.parent : null;
            if (container != null && container.name == "Panels")
            {
                Transform hero = container.Find("Hero");
                if (hero != null)
                {
                    heroRoot = hero.gameObject;
                }
            }

            heroRoot ??= FindPanelRoot("Hero");
        }

        if (heroRoot != null && heroPopupTween == null)
        {
            heroPopupTween = heroRoot.GetComponent<PopupTween>();
            if (heroPopupTween == null)
            {
                heroPopupTween = heroRoot.AddComponent<PopupTween>();
            }
        }
    }

    private void SubscribeHeroScreenRequests()
    {
        GameDataManager current = GameDataManager.Instance;
        if (subscribedGameData == current)
        {
            return;
        }

        if (subscribedGameData != null)
        {
            subscribedGameData.HeroScreenOpenRequested -= OpenHero;
            subscribedGameData.SpawnLoadoutScreenOpenRequested -= OpenSpawnLoadout;
        }

        subscribedGameData = current;
        if (subscribedGameData != null)
        {
            subscribedGameData.HeroScreenOpenRequested += OpenHero;
            subscribedGameData.SpawnLoadoutScreenOpenRequested += OpenSpawnLoadout;
        }
    }

    private static bool ShouldUseInventoryPanelRoot(GameObject currentRoot, GameObject panelRoot)
    {
        return currentRoot == null
            || currentRoot.name == "Ingame_Inventory"
            || currentRoot != panelRoot;
    }

    private static GameObject FindInventoryPanelRoot()
    {
        return FindPanelRoot("Inventory");
    }

    private static GameObject FindPanelRoot(string panelName)
    {
        GameObject panels = GameObject.Find("Panels");
        if (panels == null)
        {
            return null;
        }

        foreach (Transform child in panels.transform)
        {
            if (child.name == panelName)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private void WireOverviewTab()
    {
        if (overviewTabButton != null || inventoryRoot == null)
        {
            return;
        }

        TMP_Text[] labels = inventoryRoot.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text label = labels[i];
            if (label == null || !string.Equals(label.text.Trim(), "Overview", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            overviewTabButton = label.GetComponent<Button>();
            if (overviewTabButton == null)
            {
                overviewTabButton = label.gameObject.AddComponent<Button>();
            }

            overviewTabButton.targetGraphic = label;
            overviewTabButton.transition = Selectable.Transition.ColorTint;
            overviewTabButton.onClick.AddListener(OpenOverview);
            return;
        }
    }

    private void WireInventoryTab()
    {
        if (inventoryTabButton != null || overviewRoot == null)
        {
            return;
        }

        TMP_Text[] labels = overviewRoot.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text label = labels[i];
            if (label == null || !string.Equals(label.text.Trim(), "Inventory", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            inventoryTabButton = label.GetComponent<Button>();
            if (inventoryTabButton == null)
            {
                inventoryTabButton = label.gameObject.AddComponent<Button>();
            }

            inventoryTabButton.targetGraphic = label;
            inventoryTabButton.transition = Selectable.Transition.ColorTint;
            inventoryTabButton.onClick.AddListener(OpenInventory);
            return;
        }
    }

    private void Update()
    {
        if (!TryGetTogglePressed(out bool togglePressed, out bool escapePressed))
        {
            return;
        }

        if (isHeroClosing || isSpawnLoadoutClosing)
        {
            return;
        }

        if (togglePressed)
        {
            if (isSpawnLoadoutOpen)
            {
                spawnLoadoutView?.CancelCandidate();
            }
            else if (isHeroOpen)
            {
                CloseHero();
            }
            else
            {
                ToggleInventory();
            }
        }

        if (isSpawnLoadoutOpen && escapePressed)
        {
            spawnLoadoutView?.CancelCandidate();
        }
        else if (isHeroOpen && escapePressed)
        {
            CloseHero();
        }
        else if (isOpen && escapePressed)
        {
            CloseInventory();
        }
    }

    private bool TryGetTogglePressed(out bool togglePressed, out bool escapePressed)
    {
        togglePressed = false;
        escapePressed = false;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }

        togglePressed = keyboard[toggleKey].wasPressedThisFrame;
        escapePressed = keyboard.escapeKey.wasPressedThisFrame;
        return true;
    }

    public void ToggleInventory()
    {
        SetInventoryOpen(!isOpen);
    }

    public void OpenInventory()
    {
        SetInventoryOpen(true);
    }

    public void OpenOverview()
    {
        bool wasOpen = isOpen;
        ResolveInventoryReferences();

        if (overviewRoot == null)
        {
            Debug.LogWarning("[Inventory] Không tìm thấy panel Overview để chuyển tab.", this);
            return;
        }

        if (inventoryRoot != null)
        {
            inventoryRoot.SetActive(false);
        }

        if (heroRoot != null)
        {
            SetHeroHiddenImmediate();
        }
        SetSpawnLoadoutHiddenImmediate();

        if (!wasOpen)
        {
            EnsureAncestorsActive(overviewRoot);
        }
        EnsureMenuCanvasVisible(overviewRoot);
        overviewRoot.SetActive(true);
        isOpen = true;

        SetGameplayHudVisible(false);

        if (pauseGameWhenOpen)
        {
            Time.timeScale = 0f;
        }

        if (showCursorWhenOpen)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    public void CloseInventory()
    {
        SetInventoryOpen(false);
    }

    public void SetInventoryOpen(bool open)
    {
        isOpen = open;

        if (open)
        {
            SetSpawnLoadoutHiddenImmediate();
        }

        if (open && heroRoot != null)
        {
            SetHeroHiddenImmediate();
        }

        if (open && overviewRoot != null)
        {
            overviewRoot.SetActive(false);
        }

        if (inventoryRoot != null)
        {
            if (isOpen)
            {
                EnsureAncestorsActive(inventoryRoot);
                EnsureMenuCanvasVisible(inventoryRoot);
                inventoryRoot.SetActive(true);
            }
            else
            {
                inventoryRoot.SetActive(false);
                RestoreActivatedAncestors();
            }
        }

        if (!open && overviewRoot != null)
        {
            overviewRoot.SetActive(false);
        }

        if (!open && panelContainer != null)
        {
            panelContainer.SetActive(false);
        }

        if (isOpen && inventoryScreenController != null)
        {
            inventoryScreenController.RefreshNow();
        }


        SetGameplayHudVisible(!isOpen);

        if (pauseGameWhenOpen)
        {
            Time.timeScale = isOpen ? 0f : 1f;
        }

        if (showCursorWhenOpen)
        {
            // Gameplay camera dùng giữ chuột phải để xoay — không lock cursor khi đóng inventory.
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    public void OpenHero()
    {
        ResolveInventoryReferences();
        SubscribeHeroScreenRequests();

        if (heroRoot == null)
        {
            Debug.LogWarning("[Hero] Không tìm thấy Hero.prefab instance trong Panels.", this);
            return;
        }

        if (inventoryRoot != null)
        {
            inventoryRoot.SetActive(false);
        }

        if (overviewRoot != null)
        {
            overviewRoot.SetActive(false);
        }

        SetSpawnLoadoutHiddenImmediate();

        EnsureAncestorsActive(heroRoot);
        EnsureMenuCanvasVisible(heroRoot);
        isHeroClosing = false;
        if (!heroRoot.activeSelf)
        {
            heroPopupTween.SetHiddenImmediate();
        }
        heroPopupTween.Show();
        isOpen = false;
        isHeroOpen = true;

        SetGameplayHudVisible(false);
        if (pauseGameWhenOpen)
        {
            Time.timeScale = 0f;
        }

        if (showCursorWhenOpen)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    public void OpenSpawnLoadout()
    {
        if (isSpawnLoadoutOpen || isSpawnLoadoutClosing) return;

        ResolveInventoryReferences();
        SubscribeHeroScreenRequests();
        if (spawnLoadoutRoot == null)
        {
            Debug.LogWarning("[SpawnLoadout] Không tìm thấy SpawnLoadout.prefab instance trong Panels.", this);
            return;
        }

        if (inventoryRoot != null) inventoryRoot.SetActive(false);
        if (overviewRoot != null) overviewRoot.SetActive(false);
        SetHeroHiddenImmediate();
        EnsureAncestorsActive(spawnLoadoutRoot);
        EnsureMenuCanvasVisible(spawnLoadoutRoot);
        isSpawnLoadoutClosing = false;
        // Always force one inactive -> active transition. SpawnLoadoutView refreshes
        // from OnEnable, so this guarantees exactly one candidate/preview rebuild.
        spawnLoadoutPopupTween.SetHiddenImmediate();
        spawnLoadoutPopupTween.Show();
        isOpen = false;
        isSpawnLoadoutOpen = true;
        SetGameplayHudVisible(false);
        if (pauseGameWhenOpen) Time.timeScale = 0f;
        if (showCursorWhenOpen)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    public void CloseSpawnLoadout()
    {
        if (!isSpawnLoadoutOpen || isSpawnLoadoutClosing) return;
        isSpawnLoadoutOpen = false;
        isSpawnLoadoutClosing = true;
        if (spawnLoadoutPopupTween != null)
        {
            spawnLoadoutPopupTween.Hide(FinishCloseSpawnLoadout);
            return;
        }
        if (spawnLoadoutRoot != null) spawnLoadoutRoot.SetActive(false);
        FinishCloseSpawnLoadout();
    }

    private void FinishCloseSpawnLoadout()
    {
        isSpawnLoadoutClosing = false;
        RestoreActivatedAncestors();
        if (panelContainer != null) panelContainer.SetActive(false);
        SetGameplayHudVisible(true);
        if (pauseGameWhenOpen) Time.timeScale = 1f;
        if (showCursorWhenOpen)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    public void CloseHero()
    {
        if (!isHeroOpen || isHeroClosing)
        {
            return;
        }

        isHeroOpen = false;
        isHeroClosing = true;
        if (heroPopupTween != null)
        {
            heroPopupTween.Hide(FinishCloseHero);
            return;
        }

        if (heroRoot != null)
        {
            heroRoot.SetActive(false);
        }

        FinishCloseHero();
    }

    private void FinishCloseHero()
    {
        isHeroClosing = false;
        RestoreActivatedAncestors();
        if (panelContainer != null)
        {
            panelContainer.SetActive(false);
        }

        SetGameplayHudVisible(true);
        if (pauseGameWhenOpen)
        {
            Time.timeScale = 1f;
        }

        if (showCursorWhenOpen)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    private void SetHeroHiddenImmediate()
    {
        if (heroPopupTween != null)
        {
            heroPopupTween.SetHiddenImmediate();
        }
        else if (heroRoot != null)
        {
            heroRoot.SetActive(false);
        }

        isHeroOpen = false;
        isHeroClosing = false;
    }

    private void SetSpawnLoadoutHiddenImmediate()
    {
        if (spawnLoadoutPopupTween != null) spawnLoadoutPopupTween.SetHiddenImmediate();
        else if (spawnLoadoutRoot != null) spawnLoadoutRoot.SetActive(false);
        isSpawnLoadoutOpen = false;
        isSpawnLoadoutClosing = false;
    }

    private void EnsureAncestorsActive(GameObject target)
    {
        activatedAncestors.Clear();

        Transform parent = target.transform.parent;
        while (parent != null)
        {
            if (!parent.gameObject.activeSelf)
            {
                activatedAncestors.Add(parent.gameObject);
                parent.gameObject.SetActive(true);
            }

            parent = parent.parent;
        }
    }

    private void RestoreActivatedAncestors()
    {
        for (int i = activatedAncestors.Count - 1; i >= 0; i--)
        {
            if (activatedAncestors[i] != null)
            {
                activatedAncestors[i].SetActive(false);
            }
        }

        activatedAncestors.Clear();
    }

    private void SetGameplayHudVisible(bool visible)
    {
        ResolveGameplayHud();
        if (gameplayHudCanvas == null)
        {
            return;
        }

        if (visible)
        {
            Transform parent = gameplayHudCanvas.transform.parent;
            while (parent != null)
            {
                parent.gameObject.SetActive(true);
                parent = parent.parent;
            }
        }

        gameplayHudCanvas.SetActive(visible);
        if (gameplayHudRoot != null)
        {
            gameplayHudRoot.SetActive(visible);
        }

        Canvas canvas = gameplayHudCanvas.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.enabled = visible;
        }

        CanvasGroup canvasGroup = gameplayHudCanvas.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }

    private void OnDestroy()
    {
        if (subscribedGameData != null)
        {
            subscribedGameData.HeroScreenOpenRequested -= OpenHero;
            subscribedGameData.SpawnLoadoutScreenOpenRequested -= OpenSpawnLoadout;
            subscribedGameData = null;
        }


        if (spawnLoadoutView != null)
        {
            spawnLoadoutView.CloseRequested -= CloseSpawnLoadout;
        }

        if (overviewTabButton != null)
        {
            overviewTabButton.onClick.RemoveListener(OpenOverview);
        }

        if (inventoryTabButton != null)
        {
            inventoryTabButton.onClick.RemoveListener(OpenInventory);
        }
    }

    private static void EnsureMenuCanvasVisible(GameObject inventoryPanel)
    {
        if (inventoryPanel == null)
        {
            return;
        }

        Canvas canvas = inventoryPanel.GetComponentInParent<Canvas>(true);
        if (canvas == null)
        {
            return;
        }

        if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
        {
            canvas.worldCamera = Camera.main;
        }

        if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        canvas.enabled = true;
        if (canvas.sortingOrder < 10)
        {
            canvas.sortingOrder = 10;
        }
    }
}
