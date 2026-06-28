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

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (inventoryScreenController == null && inventoryRoot != null)
        {
            inventoryScreenController = inventoryRoot.GetComponentInChildren<InventoryScreenController>(true);
        }

        SetInventoryOpen(false);
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            ToggleInventory();
        }

        if (isOpen && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseInventory();
        }
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
            inventoryRoot.SetActive(isOpen);
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
            Cursor.visible = isOpen;
            Cursor.lockState = isOpen ? CursorLockMode.None : CursorLockMode.Locked;
        }
    }
}