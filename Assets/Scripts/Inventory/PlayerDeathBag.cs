using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Túi chứa inventory tại vị trí Player chết. Tồn tại theo thời gian gameplay và
/// trả toàn bộ item trong một lần khi Player quay lại chạm túi.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider))]
public sealed class PlayerDeathBag : MonoBehaviour
{
    static readonly Color BagColor = new(0.24f, 0.11f, 0.045f, 1f);
    static readonly Color GlowColor = new(1f, 0.68f, 0.18f, 1f);

    readonly List<InventoryItemStack> contents = new List<InventoryItemStack>();

    float remainingLifetime;
    float initialY;
    float collectArmTimer = 2.75f;
    bool collected;
    Transform visualRoot;
    TMP_Text countdownText;
    Material bagMaterial;
    Material glowMaterial;

    public static PlayerDeathBag Create(
        Vector3 worldPosition,
        IReadOnlyList<InventoryItemStack> items,
        float lifetimeSeconds)
    {
        if (items == null || items.Count == 0)
        {
            return null;
        }

        GameObject root = new GameObject("Player Death Bag");
        root.transform.position = worldPosition + Vector3.up * 0.55f;
        SphereCollider trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 1.25f;

        PlayerDeathBag bag = root.AddComponent<PlayerDeathBag>();
        bag.Initialize(items, lifetimeSeconds);
        return bag;
    }

    void Awake()
    {
        SphereCollider trigger = GetComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = Mathf.Max(0.5f, trigger.radius);
        initialY = transform.position.y;
        BuildVisual();
    }

    public void Initialize(IReadOnlyList<InventoryItemStack> items, float lifetimeSeconds)
    {
        contents.Clear();
        for (int i = 0; i < items.Count; i++)
        {
            InventoryItemStack stack = items[i];
            if (stack?.itemData != null && stack.quantity > 0)
            {
                contents.Add(new InventoryItemStack(stack.itemData, stack.quantity));
            }
        }

        remainingLifetime = Mathf.Max(1f, lifetimeSeconds);
        RefreshCountdownText();
    }

    void Update()
    {
        if (collected)
        {
            return;
        }

        collectArmTimer -= Time.deltaTime;
        remainingLifetime -= Time.deltaTime;
        if (remainingLifetime <= 0f)
        {
            Debug.Log("[DeathBag] Túi đồ đã biến mất sau 10 phút — vật phẩm bị mất.", this);
            Destroy(gameObject);
            return;
        }

        float wave = Mathf.Sin(Time.unscaledTime * 2.2f);
        transform.position = new Vector3(
            transform.position.x,
            initialY + wave * 0.12f,
            transform.position.z);
        if (visualRoot != null)
        {
            visualRoot.Rotate(0f, 28f * Time.deltaTime, 0f, Space.World);
        }

        RefreshCountdownText();
        if (countdownText != null && Camera.main != null)
        {
            countdownText.transform.rotation = Quaternion.LookRotation(
                countdownText.transform.position - Camera.main.transform.position);
        }
    }

    void OnTriggerEnter(Collider other) => TryCollect(other);
    void OnTriggerStay(Collider other) => TryCollect(other);

    void TryCollect(Collider other)
    {
        if (collected || collectArmTimer > 0f || PlayerDeathController.IsPlayerDead)
        {
            return;
        }

        Transform root = other.transform.root;
        if (!other.CompareTag("Player") && !root.CompareTag("Player"))
        {
            return;
        }

        PlayerInventoryService inventory =
            other.GetComponentInParent<PlayerInventoryService>() ??
            root.GetComponent<PlayerInventoryService>();
        if (inventory == null)
        {
            return;
        }

        collected = true;
        inventory.RestoreDeathDropItems(contents);
        Debug.Log($"[DeathBag] Đã thu hồi {contents.Count} loại vật phẩm.", this);
        Destroy(gameObject);
    }

    void BuildVisual()
    {
        visualRoot = new GameObject("Bag Visual").transform;
        visualRoot.SetParent(transform, false);

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                        Shader.Find("Sprites/Default");
        bagMaterial = CreateMaterial(shader, BagColor, "Death Bag Brown");
        glowMaterial = CreateMaterial(shader, GlowColor, "Death Bag Glow");

        CreatePrimitiveChild(
            PrimitiveType.Sphere,
            "Pouch",
            visualRoot,
            new Vector3(0f, 0f, 0f),
            new Vector3(0.9f, 0.72f, 0.62f),
            bagMaterial);
        CreatePrimitiveChild(
            PrimitiveType.Cylinder,
            "Pouch Tie",
            visualRoot,
            new Vector3(0f, 0.43f, 0f),
            new Vector3(0.34f, 0.08f, 0.34f),
            glowMaterial);
        CreatePrimitiveChild(
            PrimitiveType.Cylinder,
            "Recovery Beacon",
            visualRoot,
            new Vector3(0f, 1.25f, 0f),
            new Vector3(0.035f, 0.75f, 0.035f),
            glowMaterial);
        GameObject label = new GameObject("Countdown", typeof(TextMeshPro));
        label.transform.SetParent(transform, false);
        label.transform.localPosition = new Vector3(0f, 2.25f, 0f);
        countdownText = label.GetComponent<TextMeshPro>();
        countdownText.font = TMP_Settings.defaultFontAsset;
        countdownText.fontSize = 3.2f;
        countdownText.alignment = TextAlignmentOptions.Center;
        countdownText.color = new Color(1f, 0.9f, 0.55f, 1f);
        countdownText.outlineWidth = 0.2f;
        countdownText.text = "TÚI ĐỒ\n10:00";
    }

    static GameObject CreatePrimitiveChild(
        PrimitiveType type,
        string objectName,
        Transform parent,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        GameObject child = GameObject.CreatePrimitive(type);
        child.name = objectName;
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        child.transform.localScale = localScale;
        Collider collider = child.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        Renderer renderer = child.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return child;
    }

    static Material CreateMaterial(Shader shader, Color color, string materialName)
    {
        Material material = new Material(shader) { name = materialName };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        return material;
    }

    void RefreshCountdownText()
    {
        if (countdownText == null)
        {
            return;
        }

        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(remainingLifetime));
        countdownText.text = $"TÚI ĐỒ\n{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    void OnDestroy()
    {
        if (bagMaterial != null) Destroy(bagMaterial);
        if (glowMaterial != null) Destroy(glowMaterial);
    }
}
