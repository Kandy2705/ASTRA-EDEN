using UnityEngine;

[CreateAssetMenu(fileName = "SO_Item_", menuName = "ASTRA EDEN/Inventory/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Identity")]
    public string itemId;
    public string displayName;
    public ItemType type;
    public ItemRarity rarity;

    [Header("Stack")]
    public bool stackable = true;
    [Min(1)] public int maxStack = 99;

    [Header("Visual")]
    public Sprite icon;

    [Header("Info")]
    [TextArea(2, 4)] public string description;
}
