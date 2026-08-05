using System;
using PrimeTween;
using UnityEngine;

/// <summary>
/// Mảnh ghi chú cổ được boss đánh rơi. Tự tạo visual fantasy nếu prefab chỉ có
/// component gốc, đồng thời chỉ hoàn tất pickup sau khi người chơi đóng parchment.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider))]
public sealed class AncientNotePickup : MonoBehaviour, IWorldInteractable
{
    public const string DefaultNoteId = "ancient_note_floating_tree";

    [Header("Ancient Note")]
    [SerializeField] private string noteId = DefaultNoteId;
    [SerializeField, Min(0.5f)] private float interactionRange = 2.8f;
    [SerializeField] private Sprite floatingTreeClue;
    [SerializeField] private Sprite tyrantMapClue;
    [Tooltip("Optional prefab UI đã author sẵn. Để trống sẽ dùng layout runtime mặc định.")]
    [SerializeField] private AncientNoteUIController noteUiPrefab;

    [Header("World Visual")]
    [SerializeField, Min(0f)] private float hoverHeight = 0.16f;
    [SerializeField, Min(0.1f)] private float hoverSpeed = 1.35f;
    [SerializeField] private float rotationSpeed = 18f;
    [SerializeField] private Color parchmentColor = new(1f, 0.82f, 0.45f, 1f);
    [SerializeField] private Color magicColor = new(0.58f, 0.24f, 1f, 1f);

    [Header("Optional Audio")]
    [SerializeField] private AudioClip openSfx;
    [SerializeField] private AudioClip collectSfx;

    private Transform visualRoot;
    private Light magicLight;
    private Vector3 visualOrigin;
    private float hoverPhase;
    private bool opening;
    private bool consumed;
    private Sequence collectTween;

    public float InteractionRange => interactionRange;
    public Sprite FloatingTreeClue => floatingTreeClue;
    public Sprite TyrantMapClue => tyrantMapClue;

    public static bool WasCollected
    {
        get
        {
            GameDataManager data = GameDataManager.Instance;
            return data != null && data.IsAncientNoteCollected;
        }
    }

    private void Awake()
    {
        SphereCollider trigger = GetComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = Mathf.Max(1.15f, interactionRange * 0.48f);

        BuildVisualIfMissing();
        visualOrigin = visualRoot.localPosition;
        hoverPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
    }

    private void Start()
    {
        if (WasCollected)
        {
            Destroy(gameObject);
            return;
        }

        transform.localScale = Vector3.zero;
        Tween.Scale(transform, Vector3.zero, Vector3.one, 0.55f, Ease.OutBack);
    }

    private void Update()
    {
        if (visualRoot == null || consumed)
        {
            return;
        }

        float wave = Mathf.Sin(Time.time * hoverSpeed + hoverPhase);
        visualRoot.localPosition = visualOrigin + Vector3.up * (wave * hoverHeight);
        visualRoot.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);

        if (magicLight != null)
        {
            magicLight.intensity = 1.3f + (wave + 1f) * 0.45f;
        }
    }

    public bool CanInteract(Transform interactor)
    {
        if (opening || consumed || interactor == null || WasCollected)
        {
            return false;
        }

        return Vector3.Distance(transform.position, interactor.position) <= interactionRange;
    }

    public void Interact(Transform interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        opening = true;
        AncientNoteUIController.Show(
            floatingTreeClue,
            tyrantMapClue,
            CompleteCollection,
            () => opening = false,
            openSfx,
            noteUiPrefab);
    }

    public string GetInteractPrompt()
    {
        return "Read Ancient Note [F]";
    }

    private void CompleteCollection()
    {
        if (consumed)
        {
            return;
        }

        consumed = true;
        opening = false;
        Debug.Log($"[AncientNote] Collected '{noteId}'.", this);
        GameDataManager.Instance?.MarkAncientNoteCollected();
        if (ZoneObjectiveManager.Instance != null)
        {
            ZoneObjectiveManager.Instance.SetCurrentObjective("Find the Floating Tree", true);
        }
        else
        {
            GameDataManager.Instance?.SaveCurrentObjective("Find the Floating Tree");
            ObjectiveHUDController.ShowObjective("Find the Floating Tree");
        }

        if (collectSfx != null)
        {
            AudioSource.PlayClipAtPoint(collectSfx, transform.position);
        }

        SphereCollider trigger = GetComponent<SphereCollider>();
        trigger.enabled = false;
        Vector3 currentScale = transform.localScale;
        collectTween = Sequence.Create(useUnscaledTime: true)
            .Group(Tween.Scale(transform, currentScale, currentScale * 1.25f, 0.18f, Ease.OutBack))
            .Chain(Tween.Scale(transform, currentScale * 1.25f, Vector3.zero, 0.3f, Ease.InBack))
            .OnComplete(this, static target => Destroy(target.gameObject));
    }

    private void BuildVisualIfMissing()
    {
        Transform existing = transform.Find("Visual");
        if (existing != null)
        {
            visualRoot = existing;
            magicLight = existing.GetComponentInChildren<Light>(true);
            return;
        }

        GameObject root = new("Visual");
        visualRoot = root.transform;
        visualRoot.SetParent(transform, false);
        visualRoot.localPosition = Vector3.up * 0.75f;

        GameObject paper = GameObject.CreatePrimitive(PrimitiveType.Cube);
        paper.name = "AncientParchment";
        paper.transform.SetParent(visualRoot, false);
        paper.transform.localScale = new Vector3(0.9f, 0.055f, 0.58f);
        paper.transform.localRotation = Quaternion.Euler(8f, 0f, -5f);
        Collider paperCollider = paper.GetComponent<Collider>();
        if (paperCollider != null) Destroy(paperCollider);
        paper.GetComponent<Renderer>().material = CreateGlowMaterial(parchmentColor, parchmentColor * 1.2f);

        GameObject rune = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rune.name = "PurpleRuneCore";
        rune.transform.SetParent(visualRoot, false);
        rune.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        rune.transform.localScale = Vector3.one * 0.2f;
        Collider runeCollider = rune.GetComponent<Collider>();
        if (runeCollider != null) Destroy(runeCollider);
        rune.GetComponent<Renderer>().material = CreateGlowMaterial(magicColor, magicColor * 3.2f);

        GameObject lightObject = new("AncientNoteGlow", typeof(Light));
        lightObject.transform.SetParent(visualRoot, false);
        lightObject.transform.localPosition = Vector3.up * 0.25f;
        magicLight = lightObject.GetComponent<Light>();
        magicLight.type = LightType.Point;
        magicLight.color = magicColor;
        magicLight.range = 4.5f;
        magicLight.intensity = 1.8f;
        magicLight.shadows = LightShadows.None;

        CreateSparkles();
    }

    private void CreateSparkles()
    {
        GameObject sparkleObject = new("MagicSparkles", typeof(ParticleSystem));
        sparkleObject.transform.SetParent(visualRoot, false);
        ParticleSystem particles = sparkleObject.GetComponent<ParticleSystem>();

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.28f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.075f);
        main.startColor = new ParticleSystem.MinMaxGradient(parchmentColor, magicColor);
        main.maxParticles = 22;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 8f;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.55f;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = CreateGlowMaterial(magicColor, magicColor * 4f);
    }

    private static Material CreateGlowMaterial(Color baseColor, Color emission)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new(shader);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", baseColor);
        if (material.HasProperty("_Color")) material.SetColor("_Color", baseColor);
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission);
        material.EnableKeyword("_EMISSION");
        return material;
    }

    private void OnDestroy()
    {
        if (collectTween.isAlive)
        {
            collectTween.Stop();
        }
    }
}
