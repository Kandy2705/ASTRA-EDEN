using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class StoreTabButton : MonoBehaviour
{
    [SerializeField] private StoreTab tab;
    [SerializeField] private Button button;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(Select);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(Select);
    }

    private void Select()
    {
        ShopUIController.Active?.ShowTab(tab);
        AudioManager.EnsureInstance()?.PlayUiClick();
    }
}
