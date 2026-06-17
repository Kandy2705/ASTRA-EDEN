using UnityEngine;

[RequireComponent(typeof(CharacterHealth))]
public class LootDropSpawner : MonoBehaviour
{
    [Header("Loot")]
    [SerializeField] private LootTableData lootTable;
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
        if (lootTable == null)
        {
            if (logDrops) Debug.LogWarning($"{name}: LootDropSpawner khong co LootTableData.", this);
            return;
        }

        var drops = lootTable.Roll();
        if (drops == null || drops.Count == 0) return;

        Vector3 origin = transform.position + Vector3.up * spawnHeight;

        foreach (var drop in drops)
        {
            if (drop.item == null || drop.quantity <= 0) continue;

            GameObject prefab = pickupPrefabOverride != null ? pickupPrefabOverride : drop.item.itemPrefab;
            Vector3 offset = Random.insideUnitSphere * scatterRadius;
            offset.y = 0f;
            Vector3 spawnPos = origin + offset;

            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, spawnPos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            }
            else
            {
                if (logDrops) Debug.LogWarning($"LootDropSpawner: item '{drop.item.displayName}' khong co prefab, tao placeholder.", drop.item);
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.localScale = Vector3.one * 0.3f;
                go.transform.position = spawnPos;
            }

            EnsurePickup(go, drop.item, drop.quantity);
            ApplyPopForce(go);

            if (logDrops) Debug.Log($"[Loot] {name} drop {drop.quantity}x {drop.item.displayName}");
        }
    }

    private static void EnsurePickup(GameObject go, ItemData item, int quantity)
    {
        var pickup = go.GetComponent<PickupItem>();
        if (pickup == null) pickup = go.AddComponent<PickupItem>();
        pickup.Initialize(item, quantity);

        var col = go.GetComponent<Collider>();
        if (col == null)
        {
            var sc = go.AddComponent<SphereCollider>();
            sc.radius = 0.4f;
            sc.isTrigger = true;
        }
        else
        {
            col.isTrigger = true;
        }
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
