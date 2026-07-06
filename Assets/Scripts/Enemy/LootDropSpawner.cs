using UnityEngine;

[RequireComponent(typeof(CharacterHealth))]
public class LootDropSpawner : MonoBehaviour
{
    [Header("Loot")]
    [SerializeField] private LootTableData lootTable;
    [SerializeField] private int goldMin;
    [SerializeField] private int goldMax;
    [Tooltip("Override prefab dung cho moi drop. De trong = dung itemPrefab cua ItemData. Neu ItemData cung khong co prefab thi se canh bao.")]
    [SerializeField] private GameObject pickupPrefabOverride;

    [Header("Spawn")]
    [Tooltip("Cao do spawn so voi pivot enemy.")]
    [SerializeField] private float spawnHeight = 0.6f;
    [Tooltip("Ban kinh tan ra cua cac drop.")]
    [SerializeField] private float scatterRadius = 0.6f;
    [Tooltip("Luc bat len khi spawn (neu prefab co Rigidbody).")]
    [SerializeField] private float popUpForce = 2.5f;
    [Tooltip("Luc ngang khi spawn (neu prefab co Rigidbody).")]
    [SerializeField] private float popOutForce = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool logDrops = true;

    private CharacterHealth health;
    private bool dropped;

    public void ConfigureLootTable(LootTableData table)
    {
        lootTable = table;
    }

    public void ConfigureFromEnemyData(EnemyData data)
    {
        if (data == null)
        {
            return;
        }

        if (data.mainLootTable != null)
        {
            lootTable = data.mainLootTable;
        }

        goldMin = Mathf.Max(0, data.goldMin);
        goldMax = Mathf.Max(goldMin, data.goldMax);
    }

    private void Awake()
    {
        health = GetComponent<CharacterHealth>();
    }

    private void OnEnable()
    {
        if (health != null) health.Died += HandleDied;
    }

    private void OnDisable()
    {
        if (health != null) health.Died -= HandleDied;
    }

    private void HandleDied(CharacterHealth _)
    {
        if (dropped) return;
        dropped = true;
        SpawnLoot();
    }

    public void SpawnLoot()
    {
        Vector3 origin = transform.position + Vector3.up * spawnHeight;
        int dropIndex = 0;

        if (lootTable == null && goldMax <= 0)
        {
            if (logDrops)
            {
                Debug.LogWarning($"{name}: LootDropSpawner không có loot table và goldMin/Max = 0.", this);
            }

            return;
        }

        if (lootTable != null)
        {
            var drops = lootTable.Roll();
            if (drops != null)
            {
                for (int i = 0; i < drops.Count; i++)
                {
                    LootTableData.Drop drop = drops[i];
                    if (drop.item == null || drop.quantity <= 0)
                    {
                        continue;
                    }

                    SpawnDrop(drop.item, drop.quantity, origin, dropIndex++);
                }
            }
        }

        SpawnGoldDrop(origin, dropIndex);
    }

    private void SpawnGoldDrop(Vector3 origin, int dropIndex)
    {
        if (goldMax <= 0)
        {
            return;
        }

        ItemData goldItem = ResolveGoldItem();
        if (goldItem == null)
        {
            if (logDrops)
            {
                Debug.LogWarning($"{name}: Không tìm thấy SO_Item_Gold để drop tiền.", this);
            }

            return;
        }

        int quantity = Random.Range(Mathf.Max(0, goldMin), goldMax + 1);
        if (quantity <= 0)
        {
            return;
        }

        SpawnDrop(goldItem, quantity, origin, dropIndex);

        if (logDrops)
        {
            Debug.Log($"[Loot] {name} drop {quantity}x Gold");
        }
    }

    private void SpawnDrop(ItemData item, int quantity, Vector3 origin, int dropIndex)
    {
        GameObject prefab = pickupPrefabOverride != null ? pickupPrefabOverride : item.itemPrefab;
        Vector3 offset = Random.insideUnitSphere * scatterRadius;
        offset.y = 0f;
        Vector3 spawnPos = origin + offset + Vector3.right * (dropIndex * 0.15f);

        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab, spawnPos, prefab.transform.rotation);
            if (logDrops)
            {
                Debug.Log($"[Loot] Spawn '{item.displayName}' dùng prefab '{prefab.name}' tại {spawnPos}", item);
            }
        }
        else
        {
            if (logDrops)
            {
                Debug.LogWarning(
                    $"[Loot] item '{item.displayName}' KHÔNG có itemPrefab — fallback Sphere placeholder.",
                    item);
            }

            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.localScale = Vector3.one * 0.3f;
            go.transform.position = spawnPos;
        }

        EnsurePickup(go, item, quantity);
        ApplyPopForce(go);

        if (logDrops)
        {
            Debug.Log($"[Loot] {name} drop {quantity}x {item.displayName}");
        }
    }

    private static ItemData ResolveGoldItem()
    {
        ItemData gold = PlayerInventoryService.ResolveGoldItem();
        if (gold != null)
        {
            return gold;
        }

        return ItemRegistry.Get("gold");
    }

    private static void EnsurePickup(GameObject go, ItemData item, int quantity)
    {
        var col = go.GetComponent<Collider>();
        if (col == null)
        {
            var sc = go.AddComponent<SphereCollider>();
            sc.radius = 0.4f;
            sc.isTrigger = true;
            col = sc;
        }
        else
        {
            col.isTrigger = true;
        }

        var pickup = go.GetComponent<PickupItem>();
        if (pickup == null) pickup = go.AddComponent<PickupItem>();
        if (pickup == null)
        {
            Debug.LogError($"LootDropSpawner: không thể add PickupItem vào '{go.name}'. Item '{item?.displayName}' sẽ không pickup được.", go);
            return;
        }

        pickup.Initialize(item, quantity);
    }

    private void ApplyPopForce(GameObject go)
    {
        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) return;

        Vector3 horizontal = Random.insideUnitSphere;
        horizontal.y = 0f;
        if (horizontal.sqrMagnitude > 0.0001f) horizontal.Normalize();

        rb.AddForce(Vector3.up * popUpForce + horizontal * popOutForce, ForceMode.Impulse);
    }
}
