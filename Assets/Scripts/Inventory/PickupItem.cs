using System.Collections.Generic;
using PrimeTween;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PickupItem : MonoBehaviour
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int LegacyColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTextureId = Shader.PropertyToID("_MainTex");

    private sealed class GlowMaterialTarget
    {
        public Renderer Renderer;
        public int MaterialIndex;
        public int ColorPropertyId;
        public Color OriginalColor;
        public bool UsesEmission;
    }

    [Header("Data")]
    [SerializeField] private ItemData item;
    [SerializeField, Min(1)] private int quantity = 1;

    [Header("Behaviour")]
    [Tooltip("Tag cua nhan vat se pickup. De trong = bat ky.")]
    [SerializeField] private string collectorTag = "Player";
    [Tooltip("Delay sau khi spawn moi cho phep pickup (tranh chen ngay khi vua roi ra).")]
    [SerializeField, Min(0f)] private float pickupArmDelay = 0.75f;
    [Tooltip("Ban kinh nhat do theo world-space, khong bi thay doi khi scale visual prefab.")]
    [SerializeField, Min(0.05f)] private float pickupWorldRadius = 0.4f;
    [Tooltip("Tu destroy sau bao nhieu giay neu khong ai nhat. <=0 = khong tu destroy.")]
    [SerializeField] private float autoDestroyAfter = 30f;
    [Tooltip("Bay len khoi mat dat khi spawn (visual).")]
    [SerializeField] private float bobHeight = 0.15f;
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float spinSpeed = 60f;

    [Header("Auto Magnet")]
    [Tooltip("Player chi can vao trong ban kinh nay, item se tu bay toi va duoc nhat.")]
    [SerializeField, Min(0.1f)] private float magnetRadius = 2.5f;
    [Tooltip("Thoi gian item bay tu vi tri roi den Player.")]
    [SerializeField, Min(0.05f)] private float magnetDuration = 0.35f;
    [Tooltip("Do cao dich tren than Player, tinh tu vi tri LootCollector.")]
    [SerializeField] private float magnetTargetHeight = 0.8f;
    [Tooltip("Do cong bay len khi item dang bi hut.")]
    [SerializeField, Min(0f)] private float magnetArcHeight = 0.35f;

    [Header("Visibility Glow")]
    [SerializeField] private bool pulseGlow = true;
    [SerializeField, Min(0f)] private float idleGlowMin = 0.8f;
    [SerializeField, Min(0f)] private float idleGlowMax = 3.5f;
    [SerializeField, Min(0.1f)] private float glowPulseDuration = 0.75f;
    [SerializeField, Range(1f, 1.2f)] private float pulseScale = 1.06f;

    [Header("Pickup Tween")]
    [SerializeField, Min(0.05f)] private float popDuration = 0.12f;
    [SerializeField, Range(1f, 1.5f)] private float popScale = 1.18f;
    [SerializeField, Min(0f)] private float pickupGlowIntensity = 5f;

    [Header("FX")]
    [SerializeField] private GameObject pickupVFX;
    [SerializeField] private AudioClip pickupSFX;

    private float armTimer;
    private float lifeTimer;
    private Vector3 baseLocalPos;
    private Vector3 originalVisualScale;
    private Vector3 magnetStartPosition;
    private Transform visualRoot;
    private Transform animationTarget;
    private GlowMaterialTarget[] glowTargets;
    private MaterialPropertyBlock glowPropertyBlock;
    private LootCollector cachedCollector;
    private LootCollector activeCollector;
    private Tween glowTween;
    private Tween scalePulseTween;
    private Sequence pickupSequence;
    private float nextCollectorSearchTime;
    private float currentGlowIntensity;
    private bool consumed;

    public ItemData Item => item;
    public int Quantity => quantity;

    public void Initialize(ItemData itemData, int qty)
    {
        item = itemData;
        quantity = Mathf.Max(1, qty);
    }

    public void ConfigurePickupWorldRadius(float worldRadius)
    {
        pickupWorldRadius = Mathf.Max(0.05f, worldRadius);
        NormalizePickupCollider();
    }

    private void Awake()
    {
        NormalizePickupCollider();
        // Chỉ dùng child làm visualRoot. Nếu không có child, KHÔNG dùng root để bob,
        // vì sẽ override worldPos do spawner set → item teleport về (0,0,0) localPos.
        visualRoot = transform.childCount > 0 ? transform.GetChild(0) : null;
        if (visualRoot != null) baseLocalPos = visualRoot.localPosition;

        animationTarget = visualRoot != null ? visualRoot : transform;
        originalVisualScale = animationTarget.localScale;
        PrepareGlowRenderers();
    }

    private void Start()
    {
        StartIdlePulse();
    }

    private void NormalizePickupCollider()
    {
        SphereCollider pickupCollider = GetComponent<SphereCollider>();
        if (pickupCollider == null)
        {
            pickupCollider = gameObject.AddComponent<SphereCollider>();
        }

        pickupCollider.enabled = true;
        pickupCollider.isTrigger = true;

        Vector3 worldScale = transform.lossyScale;
        float largestScale = Mathf.Max(
            Mathf.Abs(worldScale.x),
            Mathf.Abs(worldScale.y),
            Mathf.Abs(worldScale.z));
        largestScale = Mathf.Max(0.0001f, largestScale);

        // Collider radius là local-space. Chia cho scale để bán kính thật ngoài
        // world luôn giống nhau, kể cả VenomGland có root scale = 40.
        pickupCollider.radius = pickupWorldRadius / largestScale;
    }

    private void Update()
    {
        if (armTimer < pickupArmDelay) armTimer += Time.deltaTime;
        if (consumed) return;

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
            float parentWorldScaleY =
                Mathf.Max(0.0001f, Mathf.Abs(visualRoot.parent.lossyScale.y));
            float worldBob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            float localBob = worldBob / parentWorldScaleY;
            visualRoot.localPosition =
                baseLocalPos + Vector3.up * localBob;
            visualRoot.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
        }

        if (armTimer >= pickupArmDelay)
        {
            TryStartMagnetFromNearbyPlayer();
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

        BeginPickup(collector);
    }

    private void TryStartMagnetFromNearbyPlayer()
    {
        if (cachedCollector == null && Time.time >= nextCollectorSearchTime)
        {
            nextCollectorSearchTime = Time.time + 0.5f;
            cachedCollector = FindCollector();
        }

        if (cachedCollector == null)
        {
            return;
        }

        float distanceSqr =
            (cachedCollector.transform.position - transform.position).sqrMagnitude;
        if (distanceSqr <= magnetRadius * magnetRadius)
        {
            BeginPickup(cachedCollector);
        }
    }

    private LootCollector FindCollector()
    {
        if (string.IsNullOrEmpty(collectorTag))
        {
            return FindAnyObjectByType<LootCollector>();
        }

        GameObject collectorObject = GameObject.FindGameObjectWithTag(collectorTag);
        if (collectorObject == null)
        {
            return null;
        }

        return collectorObject.GetComponentInParent<LootCollector>() ??
               collectorObject.GetComponentInChildren<LootCollector>(true);
    }

    private void BeginPickup(LootCollector collector)
    {
        if (consumed || collector == null || item == null)
        {
            return;
        }

        consumed = true;
        activeCollector = collector;
        magnetStartPosition = transform.position;
        StopIdlePulse();
        animationTarget.localScale = originalVisualScale;
        DisablePhysicsWhileFlying();

        pickupSequence = Sequence.Create()
            .Group(Tween.Scale(
                animationTarget,
                originalVisualScale,
                originalVisualScale * popScale,
                popDuration,
                Ease.OutBack))
            .Group(Tween.Custom(
                this,
                currentGlowIntensity,
                pickupGlowIntensity,
                popDuration,
                static (target, intensity) => target.SetGlow(intensity),
                Ease.OutCubic))
            .Chain(Tween.Custom(
                this,
                0f,
                1f,
                magnetDuration,
                static (target, progress) => target.UpdateMagnetPosition(progress),
                Ease.InCubic))
            .Group(Tween.Scale(
                animationTarget,
                originalVisualScale * popScale,
                Vector3.zero,
                magnetDuration,
                Ease.InBack))
            .Group(Tween.Custom(
                this,
                pickupGlowIntensity,
                0f,
                magnetDuration,
                static (target, intensity) => target.SetGlow(intensity),
                Ease.InCubic))
            .OnComplete(this, static target => target.FinishPickup());
    }

    private void UpdateMagnetPosition(float progress)
    {
        if (activeCollector == null)
        {
            return;
        }

        Vector3 destination =
            activeCollector.transform.position + Vector3.up * magnetTargetHeight;
        Vector3 directPosition =
            Vector3.LerpUnclamped(magnetStartPosition, destination, progress);
        float arcOffset = Mathf.Sin(progress * Mathf.PI) * magnetArcHeight;
        transform.position = directPosition + Vector3.up * arcOffset;
    }

    private void FinishPickup()
    {
        if (activeCollector == null)
        {
            Destroy(gameObject);
            return;
        }

        activeCollector.Collect(item, quantity);

        if (pickupVFX != null) Instantiate(pickupVFX, transform.position, Quaternion.identity);
        if (pickupSFX != null) AudioSource.PlayClipAtPoint(pickupSFX, transform.position);

        Destroy(gameObject);
    }

    private void DisablePhysicsWhileFlying()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
        {
            return;
        }

        body.isKinematic = true;
        body.detectCollisions = false;
    }

    private void PrepareGlowRenderers()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        var targets = new List<GlowMaterialTarget>();
        glowPropertyBlock = new MaterialPropertyBlock();

        // Renderer.materials tao instance runtime, cho phep bat keyword emission
        // ma khong thay doi material asset dung chung trong project.
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer itemRenderer = renderers[rendererIndex];
            Material[] materials = itemRenderer.materials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null)
                {
                    continue;
                }

                Color baseColor = GetOriginalMaterialColor(material);
                bool usesEmission = material.HasProperty(EmissionColorId);
                int colorPropertyId = usesEmission
                    ? EmissionColorId
                    : ResolveBaseColorProperty(material);

                if (colorPropertyId < 0)
                {
                    continue;
                }

                if (usesEmission)
                {
                    Color originalEmission = material.GetColor(EmissionColorId);
                    if (originalEmission.maxColorComponent > 0.01f)
                    {
                        baseColor = originalEmission;
                    }

                    CopyBaseTextureToEmissionIfNeeded(material);
                    material.EnableKeyword("_EMISSION");
                }

                baseColor.a = 1f;
                targets.Add(new GlowMaterialTarget
                {
                    Renderer = itemRenderer,
                    MaterialIndex = materialIndex,
                    ColorPropertyId = colorPropertyId,
                    OriginalColor = baseColor,
                    UsesEmission = usesEmission
                });
            }
        }

        glowTargets = targets.ToArray();
    }

    private static Color GetOriginalMaterialColor(Material material)
    {
        if (material.HasProperty(BaseColorId))
        {
            return material.GetColor(BaseColorId);
        }

        if (material.HasProperty(LegacyColorId))
        {
            return material.GetColor(LegacyColorId);
        }

        return Color.white;
    }

    private static int ResolveBaseColorProperty(Material material)
    {
        if (material.HasProperty(BaseColorId))
        {
            return BaseColorId;
        }

        return material.HasProperty(LegacyColorId) ? LegacyColorId : -1;
    }

    private static void CopyBaseTextureToEmissionIfNeeded(Material material)
    {
        if (!material.HasProperty(EmissionMapId) ||
            material.GetTexture(EmissionMapId) != null)
        {
            return;
        }

        Texture baseTexture = null;
        if (material.HasProperty(BaseMapId))
        {
            baseTexture = material.GetTexture(BaseMapId);
        }
        else if (material.HasProperty(MainTextureId))
        {
            baseTexture = material.GetTexture(MainTextureId);
        }

        if (baseTexture != null)
        {
            material.SetTexture(EmissionMapId, baseTexture);
        }
    }

    private void StartIdlePulse()
    {
        StopIdlePulse();

        if (!pulseGlow)
        {
            SetGlow(0f);
            return;
        }

        SetGlow(idleGlowMin);
        glowTween = Tween.Custom(
            this,
            idleGlowMin,
            Mathf.Max(idleGlowMin, idleGlowMax),
            glowPulseDuration,
            static (target, intensity) => target.SetGlow(intensity),
            Ease.InOutSine,
            -1,
            CycleMode.Yoyo);

        // Root scale lien quan truc tiep toi world radius collider, nen chi pulse
        // child visual de ban kinh nhat do khong bi thay doi theo nhip.
        if (visualRoot != null && pulseScale > 1f)
        {
            scalePulseTween = Tween.Scale(
                visualRoot,
                originalVisualScale,
                originalVisualScale * pulseScale,
                glowPulseDuration,
                Ease.InOutSine,
                -1,
                CycleMode.Yoyo);
        }
    }

    private void StopIdlePulse()
    {
        if (glowTween.isAlive)
        {
            glowTween.Stop();
        }

        if (scalePulseTween.isAlive)
        {
            scalePulseTween.Stop();
        }
    }

    private void SetGlow(float intensity)
    {
        currentGlowIntensity = intensity;
        if (glowTargets == null || glowPropertyBlock == null)
        {
            return;
        }

        for (int i = 0; i < glowTargets.Length; i++)
        {
            GlowMaterialTarget target = glowTargets[i];
            if (target.Renderer == null)
            {
                continue;
            }

            // Emission dung mau goc cua material. Shader khong co emission se
            // tang brightness cua base color nhe, van giu nguyen hue/texture.
            float colorMultiplier = target.UsesEmission
                ? intensity
                : 1f + intensity * 0.15f;
            Color brightColor = target.OriginalColor * colorMultiplier;
            brightColor.a = target.OriginalColor.a;

            target.Renderer.GetPropertyBlock(
                glowPropertyBlock,
                target.MaterialIndex);
            glowPropertyBlock.SetColor(target.ColorPropertyId, brightColor);
            target.Renderer.SetPropertyBlock(
                glowPropertyBlock,
                target.MaterialIndex);
        }
    }

    private void OnDestroy()
    {
        StopIdlePulse();
        if (pickupSequence.isAlive)
        {
            pickupSequence.Stop();
        }
    }
}
