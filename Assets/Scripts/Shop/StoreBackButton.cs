using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class StoreBackButton : MonoBehaviour
{
    [SerializeField] private Button button;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(Close);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(Close);
    }

    private static void Close() => ShopUIController.Active?.Close();
}
