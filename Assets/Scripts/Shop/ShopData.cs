using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SO_Shop_", menuName = "ASTRA EDEN/Shop/Shop Data")]
public class ShopData : ScriptableObject
{
    public string shopId = "shop_beacon_camp";
    public string shopName = "Beacon Supply";

    [Header("Currency")]
    public ItemData currencyItem;

    public List<ShopEntry> entries = new List<ShopEntry>();
}

[Serializable]
public class ShopEntry
{
    public ItemData item;
    [Min(0)] public int price = 10;
    [Min(1)] public int quantity = 1;
}