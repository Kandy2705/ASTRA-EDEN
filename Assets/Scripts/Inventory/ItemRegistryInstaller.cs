using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class ItemRegistryInstaller : MonoBehaviour
{
    [Header("All Item Data Assets")]
    [SerializeField] private List<ItemData> allItems = new List<ItemData>();

    private void Awake()
    {
        if (allItems == null || allItems.Count == 0)
        {
            Debug.LogWarning("[ItemRegistryInstaller] allItems trống — bỏ qua, không ghi đè registry.", this);
            return;
        }

        ItemRegistry.Initialize(allItems);
    }
}