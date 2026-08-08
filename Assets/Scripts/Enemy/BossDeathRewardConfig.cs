using System.Collections;
using UnityEngine;

/// <summary>
/// Cấu hình phần thưởng chắc chắn rơi khi boss chết: Health Potion drop bảo đảm
/// (one-time-only) + đánh dấu tiến trình mở Floating Tree / Note 2 ở GameDataManager.
/// Đặt lên chính GameObject boss; MiniBossMarker gọi NotifyBossDied khi boss chết.
/// </summary>
[DisallowMultipleComponent]
public class BossDeathRewardConfig : MonoBehaviour
{
    [Header("Guaranteed Health Potions")]
    [Tooltip("ItemId của bình máu được drop chắc chắn. Mặc định: item_health_potion_small.")]
    [SerializeField] private string healthPotionItemId = "item_health_potion_small";
    [Tooltip("Số lượng bình máu: BeachTyran = 1, AncientForest = 2.")]
    [SerializeField, Min(0)] private int healthPotionCount = 1;
    [Tooltip("Sau bao nhiêu giây kể từ lúc chết mới hiện bình máu (cho anim Die chạy trước). " +
             "Phải >= độ dài anim chết thật: BeachTyran = 2.0s, AncientForest = 2.5s.")]
    [SerializeField, Min(0f)] private float potionSpawnDelay = 2.5f;
    [Tooltip("Các offset nhỏ quanh vị trí boss để rải bình máu (mỗi offset = một bình).")]
    [SerializeField] private Vector3[] potionDropOffsets =
    {
        new Vector3(0f, 0.4f, 0.9f)
    };
    [Tooltip("Prefab pickup override. Để trống = dùng itemPrefab của ItemData.")]
    [SerializeField] private GameObject pickupPrefabOverride;
    [Tooltip("Bán kính nhặt theo world-space của bình máu.")]
    [SerializeField, Min(0.05f)] private float pickupWorldRadius = 0.4f;
    [SerializeField] private bool logDrops = true;

    [Header("Progression")]
    [Tooltip("Đánh dấu boss Ancient Forest đã bị hạ ở GameDataManager (mở Floating Tree + Note 2).")]
    [SerializeField] private bool markAncientForestBossDefeated;

    private bool potionsSpawned;

    public void NotifyBossDied(Transform bossRoot)
    {
        // Ghi nhận TRƯỚC khi mark: nếu flag đã đúng từ phiên chơi trước (boss hồi sinh
        // sau khi load save), không drop bình máu lần nữa — chỉ drop đúng lần đầu.
        bool alreadyDefeatedInPreviousSession =
            markAncientForestBossDefeated &&
            GameDataManager.Instance != null &&
            GameDataManager.Instance.IsAncientForestBossDefeated;

        if (markAncientForestBossDefeated)
        {
            GameDataManager.Instance?.MarkAncientForestBossDefeated();
        }

        if (potionsSpawned || alreadyDefeatedInPreviousSession || healthPotionCount <= 0)
        {
            potionsSpawned = true;
            return;
        }

        StartCoroutine(PotionSpawnRoutine(bossRoot));
    }

    private IEnumerator PotionSpawnRoutine(Transform bossRoot)
    {
        if (potionSpawnDelay > 0f)
        {
            yield return new WaitForSeconds(potionSpawnDelay);
        }

        SpawnGuaranteedPotions(bossRoot);
    }

    private void SpawnGuaranteedPotions(Transform bossRoot)
    {
        if (potionsSpawned)
        {
            return;
        }

        potionsSpawned = true;

        if (bossRoot == null)
        {
            return;
        }

        ItemData item = ItemRegistry.Get(healthPotionItemId);
        if (item == null)
        {
            if (logDrops)
            {
                Debug.LogWarning(
                    $"[BossReward] {name}: Không tìm thấy ItemData '{healthPotionItemId}' — " +
                    "ItemRegistry chưa được khởi tạo hoặc item không tồn tại.", this);
            }

            return;
        }

        int count = Mathf.Max(0, healthPotionCount);
        for (int i = 0; i < count; i++)
        {
            Vector3 offset = i < potionDropOffsets.Length
                ? potionDropOffsets[i]
                : new Vector3(0f, 0.4f, 0.9f);
            SpawnPotion(bossRoot, item, offset);
        }
    }

    private void SpawnPotion(Transform bossRoot, ItemData item, Vector3 offset)
    {
        Vector3 candidate = bossRoot.position + bossRoot.TransformDirection(offset);
        Vector3 spawnPosition = candidate;

        var agent = bossRoot.GetComponent<UnityEngine.AI.NavMeshAgent>();
        int areaMask = agent != null ? agent.areaMask : UnityEngine.AI.NavMesh.AllAreas;
        if (UnityEngine.AI.NavMesh.SamplePosition(candidate, out UnityEngine.AI.NavMeshHit hit, 3f, areaMask))
        {
            spawnPosition = hit.position + Vector3.up * offset.y;
        }

        GameObject prefab = pickupPrefabOverride != null
            ? pickupPrefabOverride
            : item.itemPrefab;

        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab, spawnPosition, prefab.transform.rotation);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.localScale = Vector3.one * 0.3f;
            go.transform.position = spawnPosition;
        }

        SetupPickup(go, item);

        if (logDrops)
        {
            Debug.Log($"[BossReward] {name} drop {item.displayName} tại {spawnPosition}", this);
        }
    }

    private void SetupPickup(GameObject go, ItemData item)
    {
        SphereCollider pickupCollider = go.GetComponent<SphereCollider>();
        if (pickupCollider == null)
        {
            pickupCollider = go.AddComponent<SphereCollider>();
        }

        Collider[] rootColliders = go.GetComponents<Collider>();
        for (int i = 0; i < rootColliders.Length; i++)
        {
            Collider collider = rootColliders[i];
            if (collider != null && collider != pickupCollider)
            {
                collider.enabled = false;
            }
        }

        pickupCollider.enabled = true;
        pickupCollider.isTrigger = true;

        float largestScale = Mathf.Max(
            Mathf.Abs(go.transform.lossyScale.x),
            Mathf.Abs(go.transform.lossyScale.y),
            Mathf.Abs(go.transform.lossyScale.z));
        largestScale = Mathf.Max(0.0001f, largestScale);
        pickupCollider.radius = pickupWorldRadius / largestScale;

        var pickup = go.GetComponent<PickupItem>();
        if (pickup == null)
        {
            pickup = go.AddComponent<PickupItem>();
        }

        if (pickup == null)
        {
            Debug.LogError(
                $"[BossReward] {name}: không thể add PickupItem vào '{go.name}'. " +
                $"Item '{item?.displayName}' sẽ không pickup được.", go);
            return;
        }

        pickup.Initialize(item, 1);
        pickup.ConfigurePickupWorldRadius(pickupWorldRadius);
    }
}
