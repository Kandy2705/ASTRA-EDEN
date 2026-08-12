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

    [Tooltip("Root của panel Inventory trong Menu_Canvas.")]
    [SerializeField] private GameObject inventoryRoot;

    [Tooltip("Root của panel Overview trong cùng Panels container với Inventory.")]
    [SerializeField] private GameObject overviewRoot;

    [Header("Inventory Refresh")]
    [SerializeField] private InventoryScreenController inventoryScreenController;

    [Header("Options")]
    [SerializeField] private bool pauseGameWhenOpen = true;
    [SerializeField] private bool showCursorWhenOpen = true;

    private bool isOpen;
    private Button overviewTabButton;
    private Button inventoryTabButton;
    private readonly List<GameObject> activatedAncestors = new List<GameObject>();

    public bool IsOpen => isOpen;

    private void Awake()
    {
        ResolveInventoryReferences();
        WireOverviewTab();
        WireInventoryTab();


        SetInventoryOpen(false);
    }

    private void Start()
    {
        ResolveInventoryReferences();
        WireOverviewTab();
        WireInventoryTab();
    }

    private void ResolveInventoryReferences()
    {
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

        if (togglePressed)
        {
            ToggleInventory();
        }

        if (isOpen && escapePressed)
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

        if (!wasOpen)
        {
            EnsureAncestorsActive(overviewRoot);
        }
        EnsureMenuCanvasVisible(overviewRoot);
        overviewRoot.SetActive(true);
        isOpen = true;

        if (gameplayHudCanvas != null)
        {
            gameplayHudCanvas.SetActive(false);
        }

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

        if (isOpen && inventoryScreenController != null)
        {
            inventoryScreenController.RefreshNow();
        }


        if (gameplayHudCanvas != null)
        {
            gameplayHudCanvas.SetActive(!isOpen);
        }

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

    private void OnDestroy()
    {
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
