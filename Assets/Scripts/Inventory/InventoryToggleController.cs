using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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

    [Header("Inventory Refresh")]
    [SerializeField] private InventoryScreenController inventoryScreenController;

    [Header("Options")]
    [SerializeField] private bool pauseGameWhenOpen = true;
    [SerializeField] private bool showCursorWhenOpen = true;

    private bool isOpen;
    private readonly List<GameObject> activatedAncestors = new List<GameObject>();

    public bool IsOpen => isOpen;

    private void Awake()
    {
        ResolveInventoryReferences();


        SetInventoryOpen(false);
    }

    private void Start()
    {
        ResolveInventoryReferences();
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
    }

    private static bool ShouldUseInventoryPanelRoot(GameObject currentRoot, GameObject panelRoot)
    {
        return currentRoot == null
            || currentRoot.name == "Ingame_Inventory"
            || currentRoot != panelRoot;
    }

    private static GameObject FindInventoryPanelRoot()
    {
        GameObject panels = GameObject.Find("Panels");
        if (panels == null)
        {
            return null;
        }

        foreach (Transform child in panels.transform)
        {
            if (child.name == "Inventory")
            {
                return child.gameObject;
            }
        }

        return null;
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

    public void CloseInventory()
    {
        SetInventoryOpen(false);
    }

    public void SetInventoryOpen(bool open)
    {
        isOpen = open;

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