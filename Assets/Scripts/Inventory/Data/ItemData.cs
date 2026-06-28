using UnityEngine;

[CreateAssetMenu(fileName = "SO_Item_", menuName = "ASTRA EDEN/Inventory/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Identity")]
    public string itemId;
    public string displayName;
    public ItemType type;
    public ItemRarity rarity;

    public GameObject itemPrefab;

    [Header("Stack")]
    public bool stackable = true;
    [Min(1)] public int maxStack = 99;

    [Header("Visual")]
    public Sprite icon;

    [Header("Info")]
    [TextArea(2, 4)] public string description;

    [Header("Consumable Effects (chi ap dung khi type = Consumable)")]
    [Min(0f)] public float restoreHP;
    [Min(0f)] public float restoreStamina;
    [Min(0f)] public float restoreEnergy;
}
