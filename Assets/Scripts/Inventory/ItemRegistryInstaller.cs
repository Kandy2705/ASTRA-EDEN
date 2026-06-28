using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ItemRegistryInstaller : MonoBehaviour
{
    [Header("All Item Data Assets")]
    [SerializeField] private List<ItemData> allItems = new List<ItemData>();

    private void Awake()
    {
        ItemRegistry.Initialize(allItems);
    }
}