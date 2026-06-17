using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PickupItem : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private ItemData item;
    [SerializeField, Min(1)] private int quantity = 1;

    [Header("Behaviour")]
    [Tooltip("Tag cua nhan vat se pickup. De trong = bat ky.")]
    [SerializeField] private string collectorTag = "Player";
    [Tooltip("Delay sau khi spawn moi cho phep pickup (tranh chen ngay khi vua roi ra).")]
    [SerializeField, Min(0f)] private float pickupArmDelay = 0.3f;
    [Tooltip("Tu destroy sau bao nhieu giay neu khong ai nhat. <=0 = khong tu destroy.")]
    [SerializeField] private float autoDestroyAfter = 30f;
    [Tooltip("Bay len khoi mat dat khi spawn (visual).")]
    [SerializeField] private float bobHeight = 0.15f;
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float spinSpeed = 60f;

    [Header("FX")]
    [SerializeField] private GameObject pickupVFX;
    [SerializeField] private AudioClip pickupSFX;

    private float armTimer;
    private float lifeTimer;
    private Vector3 baseLocalPos;
    private Transform visualRoot;
    private bool consumed;

    public ItemData Item => item;
    public int Quantity => quantity;

    public void Initialize(ItemData itemData, int qty)
    {
        item = itemData;
        quantity = Mathf.Max(1, qty);
    }

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
        visualRoot = transform.childCount > 0 ? transform.GetChild(0) : transform;
        baseLocalPos = visualRoot.localPosition;
    }

    private void Update()
    {
        if (armTimer < pickupArmDelay) armTimer += Time.deltaTime;

        if (autoDestroyAfter > 0f)
        {
            lifeTimer += Time.deltaTime;
            if (lifeTimer >= autoDestroyAfter)
            {
                Destroy(gameObject);
                return;
            }
        }

        if (visualRoot != null)
        {
            float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            visualRoot.localPosition = baseLocalPos + Vector3.up * bob;
            visualRoot.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryPickup(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryPickup(other);
    }

    private void TryPickup(Collider other)
    {
        if (consumed) return;
        if (armTimer < pickupArmDelay) return;
        if (item == null) return;

        if (!string.IsNullOrEmpty(collectorTag))
        {
            if (!other.CompareTag(collectorTag) && !other.transform.root.CompareTag(collectorTag))
            {
                return;
            }
        }

        var collector = other.GetComponentInParent<LootCollector>();
        if (collector == null) return;

        consumed = true;
        collector.Collect(item, quantity);

        if (pickupVFX != null) Instantiate(pickupVFX, transform.position, Quaternion.identity);
        if (pickupSFX != null) AudioSource.PlayClipAtPoint(pickupSFX, transform.position);

        Destroy(gameObject);
    }
}
