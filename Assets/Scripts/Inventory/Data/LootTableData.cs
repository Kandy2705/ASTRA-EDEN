using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SO_LootTable_", menuName = "ASTRA EDEN/Inventory/Loot Table")]
public class LootTableData : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public ItemData item;
        [Min(0f)] public float weight = 1f;
        [Min(1)] public int minQuantity = 1;
        [Min(1)] public int maxQuantity = 1;
        [Tooltip("Bo qua weight, luon roi (vi du quest item).")]
        public bool guaranteed = false;
        [Range(0f, 1f), Tooltip("Xac suat roi rieng khi guaranteed = true. 1 = chac chan.")]
        public float guaranteedChance = 1f;
    }

    [System.Serializable]
    public struct Drop
    {
        public ItemData item;
        public int quantity;
    }

    [Header("Roll Settings")]
    [Tooltip("So lan roll weighted (khong tinh guaranteed). 0 = khong roll random, chi roi guaranteed.")]
    [Min(0)] public int rollCount = 1;

    [Tooltip("Xac suat moi lan roll thuc su roi item (0..1). Vi du 0.7 = 30% kha nang khong roi gi.")]
    [Range(0f, 1f)] public float rollChance = 1f;

    [Header("Entries")]
    public List<Entry> entries = new List<Entry>();

    /// <summary>Roll loot table -> list cac drop hop le. Khong instantiate, chi tra ve data.</summary>
    public List<Drop> Roll()
    {
        var result = new List<Drop>();

        // Guaranteed drops
        foreach (var e in entries)
        {
            if (e == null || e.item == null) continue;
            if (!e.guaranteed) continue;
            if (e.guaranteedChance < 1f && Random.value > e.guaranteedChance) continue;
            int qty = Random.Range(e.minQuantity, e.maxQuantity + 1);
            if (qty > 0) result.Add(new Drop { item = e.item, quantity = qty });
        }

        // Weighted rolls
        float totalWeight = 0f;
        foreach (var e in entries)
        {
            if (e == null || e.item == null || e.guaranteed) continue;
            if (e.weight > 0f) totalWeight += e.weight;
        }

        if (totalWeight <= 0f || rollCount <= 0) return result;

        for (int r = 0; r < rollCount; r++)
        {
            if (rollChance < 1f && Random.value > rollChance) continue;

            float pick = Random.value * totalWeight;
            float acc = 0f;
            foreach (var e in entries)
            {
                if (e == null || e.item == null || e.guaranteed || e.weight <= 0f) continue;
                acc += e.weight;
                if (pick <= acc)
                {
                    int qty = Random.Range(e.minQuantity, e.maxQuantity + 1);
                    if (qty > 0) result.Add(new Drop { item = e.item, quantity = qty });
                    break;
                }
            }
        }

        return result;
    }
}
