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
    public const string Note2Id = "ancient_note_floating_tree_02";

    [Header("Ancient Note")]
    [SerializeField] private string noteId = DefaultNoteId;
    [SerializeField, Min(0.5f)] private float interactionRange = 2.8f;
    [SerializeField] private Sprite floatingTreeClue;
    [SerializeField] private Sprite tyrantMapClue;
    [Tooltip("Optional prefab UI đã author sẵn. Để trống sẽ dùng layout runtime mặc định.")]
    [SerializeField] private AncientNoteUIController noteUiPrefab;

    [Header("Note 2 - Content")]
    [Tooltip("Chỉ dùng khi noteId == \"ancient_note_floating_tree_02\".")]
    [SerializeField] private string note2Title = "THE WHISPER BENEATH THE ROOTS";
    [SerializeField] private string note2Subtitle = "LỜI THÌ THẦM DƯỚI NHỮNG BỘ RỄ";
    [SerializeField, TextArea(8, 24)] private string note2MessageEnglish =
        "To the one who has found this place,\n\n" +
        "If you are reading these words, then I may no longer have the chance to finish the path ahead myself.\n\n" +
        "I once believed the beast within this forest was the cause of the chaos that consumed this island.\n\n" +
        "I was wrong.\n\n" +
        "It, too, was only a victim.\n\n" +
        "A guardian twisted by a power that has been spreading silently beneath this island.\n\n" +
        "I have left a map beneath the roots of this tree. Upon it lies the path I was never able to finish.\n\n" +
        "Take it with you.\n\n" +
        "Follow the forgotten mark upon the map, and continue where I could not.\n\n" +
        "At the end of that path, you will find the truth — and perhaps the one responsible for all the suffering we have endured.\n\n" +
        "If you truly wish to bring peace back to this island, you will have to face him.\n\n" +
        "I ask only one thing of you...\n\n" +
        "Do not let the sacrifices of those who fell here become meaningless.\n\n" +
        "And be careful.\n\n" +
        "Something is still watching every step you take.";
    [SerializeField, TextArea(8, 24)] private string note2MessageVietnamese =
        "Gửi người đã tìm được đến nơi này,\n\n" +
        "Nếu ngươi đang đọc những dòng này, có lẽ ta đã không còn cơ hội để tự mình hoàn thành con đường phía trước.\n\n" +
        "Ta từng nghĩ con quái vật trong khu rừng này là nguyên nhân khiến hòn đảo rơi vào hỗn loạn. Nhưng ta đã lầm.\n\n" +
        "Nó cũng chỉ là một nạn nhân.\n\n" +
        "Một kẻ canh giữ bị biến đổi bởi thứ sức mạnh đang âm thầm lan rộng bên dưới hòn đảo.\n\n" +
        "Ta đã để lại một tấm bản đồ dưới những bộ rễ của cái cây này. Trên đó là con đường mà ta đã không thể đi hết.\n\n" +
        "Hãy mang nó theo.\n\n" +
        "Hãy lần theo dấu ấn trên bản đồ và tiếp tục thay phần của ta.\n\n" +
        "Ở cuối con đường ấy, ngươi sẽ tìm thấy sự thật — và có lẽ cả kẻ đã khiến tất cả chúng ta phải chịu đựng đến ngày hôm nay.\n\n" +
        "Nếu ngươi thực sự muốn mang sự bình yên trở lại hòn đảo này, ngươi sẽ phải đối mặt với hắn.\n\n" +
        "Ta chỉ xin ngươi một điều...\n\n" +
        "Đừng để sự hy sinh của những người đã nằm lại nơi đây trở nên vô nghĩa.\n\n" +
        "Và hãy cẩn thận.\n\n" +
        "Có thứ gì đó vẫn đang dõi theo từng bước chân của ngươi.";

    [Header("Note 2 - Next Destination")]
    [Tooltip("Điểm đến tiếp theo trên bản đồ cổ. Để trống: vẫn hoàn tất Note #2 và đặt objective nhưng KHÔNG tạo destination marker.")]
    [SerializeField] private Transform nextDestination;

    [Header("World Visual")]
    [SerializeField, Min(0f)] private float hoverHeight = 0.16f;
    [SerializeField, Min(0.1f)] private float hoverSpeed = 1.35f;
    [SerializeField] private float rotationSpeed = 18f;
    [Tooltip("Note #2 nằm sát rễ cây; Note #1 giữ độ cao cũ.")]
    [SerializeField, Min(0f)] private float note2VisualBaseHeight = 0.18f;
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

    public static bool WasCollectedNote2
    {
        get
        {
            GameDataManager data = GameDataManager.Instance;
            return data != null && data.IsAncientNote2Collected;
        }
    }

    private bool IsNote2 => string.Equals(noteId, Note2Id, StringComparison.Ordinal);

    /// <summary>
    /// Chuyển instance này thành Note #2. Dùng khi spawn cùng prefab cơ sở
    /// (Note #1) cho Floating Tree. Gọi ngay sau Instantiate trước khi Start chạy.
    /// </summary>
    public void ConfigureNote2()
    {
        noteId = Note2Id;
    }

    private bool IsCollectedForThisNote => IsNote2 ? WasCollectedNote2 : WasCollected;

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
        if (IsCollectedForThisNote)
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
        if (opening || consumed || interactor == null || IsCollectedForThisNote)
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
            noteUiPrefab,
            IsNote2 ? note2Title : null,
            IsNote2 ? note2Subtitle : null,
            IsNote2 ? BuildNote2Message() : null);
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

        if (IsNote2)
        {
            GameDataManager.Instance?.MarkAncientNote2Collected();
            SetObjective("Follow the Ancient Map");

            if (nextDestination == null)
            {
                Debug.Log(
                    "[AncientNote] nextDestination rỗng — vẫn hoàn tất Note #2, " +
                    "không tạo destination marker.", this);
            }
            else
            {
                Debug.Log(
                    $"[AncientNote] nextDestination = '{nextDestination.name}' — " +
                    "tạo destination marker chưa được implement, bỏ qua.", this);
            }
        }
        else
        {
            GameDataManager.Instance?.MarkAncientNoteCollected();
            SetObjective("Find the Floating Tree");
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

    private string BuildNote2Message()
    {
        return note2MessageEnglish + "\n\n────────────────────\n\n" + note2MessageVietnamese;
    }

    private static void SetObjective(string objective)
    {
        if (ZoneObjectiveManager.Instance != null)
        {
            ZoneObjectiveManager.Instance.SetCurrentObjective(objective, true);
        }
        else
        {
            GameDataManager.Instance?.SaveCurrentObjective(objective);
            ObjectiveHUDController.ShowObjective(objective);
        }
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
        visualRoot.localPosition = Vector3.up * (IsNote2 ? note2VisualBaseHeight : 0.75f);

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
