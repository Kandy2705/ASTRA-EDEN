using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Điều phối việc dùng hai Ancient Note/Map trong inventory. Pickup chỉ thêm item;
/// chỉ Use item mới mở parchment và mở khóa objective/đường chỉ dẫn tương ứng.
/// </summary>
[DefaultExecutionOrder(50)]
[DisallowMultipleComponent]
public sealed class AncientMapProgression : MonoBehaviour
{
    public const string ItemId = "ancient_map_note_01";
    public const string Item2Id = "ancient_map_note_02";
    public const string FirstObjective = "Find the Floating Tree";
    public const string DefaultObjective = "Follow the Ancient Map";

    private const string FirstTitle = "ANCIENT NOTE";
    private const string FirstSubtitle = "A whisper left behind by the fallen guardian";
    private const string FirstMessage =
        "To the one who survived,\n\n" +
        "If this note has found your hands, then the beast has fallen.\n\n" +
        "Seek the Floating Tree.\n" +
        "There, a secret awaits you — one you must learn before taking the next step.\n\n" +
        "Hidden near its roots lies a map that will guide you to the place where the great tyrant must fall.\n\n" +
        "Only then may peace return to this island.";

    private const string SecondTitle = "THE WHISPER BENEATH THE ROOTS";
    private const string SecondSubtitle = "THE ANCIENT MAP";
    private const string SecondMessage =
        "To the one who followed the whisper,\n\n" +
        "You have found the tree that refuses the earth. Its roots remember what this island tried to forget.\n\n" +
        "The creature you defeated was not the source of this corruption. It was only a guardian — twisted by the will of something far older.\n\n" +
        "Beneath these roots lies the path left behind by those who came before us.\n\n" +
        "Take the map. Follow the forgotten mark, beyond the lands we once called safe.\n\n" +
        "There, the truth of this island waits... and so does the one who must fall before peace can return.\n\n" +
        "Do not trust the silence ahead. It is watching you.";

    private static AncientMapProgression instance;
    private static readonly List<ItemData> RegistrationBuffer = new(2);

    [Header("Inventory Items")]
    [SerializeField] private ItemData ancientMapItem;
    [SerializeField] private ItemData ancientMapItem2;

    [Header("Parchment Presentation")]
    [SerializeField] private AncientNoteUIController noteUiPrefab;
    [SerializeField] private Sprite floatingTreeClueImage;
    [SerializeField] private Sprite mapImage;
    [Tooltip("Voice/SFX phát mỗi lần mở giấy #1 từ Inventory.")]
    [SerializeField] private AudioClip openSfx;
    [Tooltip("Voice/SFX riêng của giấy #2. Để trống nếu chưa có voice.")]
    [SerializeField] private AudioClip secondOpenSfx;

    [Header("Note #2 Guidance - Optional")]
    [Tooltip("Điểm đến thật sau Note #2. Có thể để trống cho tới khi khu vực kế tiếp được làm xong.")]
    [SerializeField] private Transform nextDestination;
    [Tooltip("Tên GameObject đích dùng làm fallback nếu không kéo Transform trực tiếp.")]
    [SerializeField] private string nextDestinationObjectName = "";
    [SerializeField] private string destinationLabel = "ANCIENT DESTINATION";
    [SerializeField] private string nextObjectiveText = DefaultObjective;

    public static bool IsMapItem(ItemData item)
    {
        return item != null &&
               (string.Equals(item.itemId, ItemId, System.StringComparison.Ordinal) ||
                string.Equals(item.itemId, Item2Id, System.StringComparison.Ordinal));
    }

    public static bool IsSecondMapItem(ItemData item)
    {
        return item != null && string.Equals(item.itemId, Item2Id, System.StringComparison.Ordinal);
    }

    public static bool IsGuidanceObjective(string objective)
    {
        string secondObjective = instance != null && !string.IsNullOrWhiteSpace(instance.nextObjectiveText)
            ? instance.nextObjectiveText.Trim()
            : DefaultObjective;
        return string.Equals(objective, FirstObjective, System.StringComparison.OrdinalIgnoreCase) ||
               string.Equals(objective, secondObjective, System.StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryUse(ItemData item)
    {
        if (!IsMapItem(item))
        {
            return false;
        }

        AncientMapProgression controller = ResolveInstance();
        bool isSecond = IsSecondMapItem(item);
        controller.RegisterItems(item);
        controller.ActivateGuidance(isSecond);
        controller.OpenMap(item, isSecond);
        return true;
    }

    public static ItemData ResolveMapItem(ItemData assigned = null, bool second = false)
    {
        string expectedId = second ? Item2Id : ItemId;
        if (assigned != null && string.Equals(assigned.itemId, expectedId, System.StringComparison.Ordinal))
        {
            return assigned;
        }

        ItemData fromManager = GameDataManager.Instance != null
            ? GameDataManager.Instance.ResolveItem(expectedId)
            : null;
        return fromManager != null ? fromManager : ItemRegistry.Get(expectedId);
    }

    private void Awake()
    {
        instance = this;
        RegisterItems(ancientMapItem, ancientMapItem2);
    }

    private void Start()
    {
        if (GameDataManager.Instance != null && GameDataManager.Instance.IsAncientMap2GuidanceUnlocked)
        {
            PushSecondDestinationToMinimaps();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private static AncientMapProgression ResolveInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindFirstObjectByType<AncientMapProgression>(FindObjectsInactive.Include);
        if (instance != null)
        {
            return instance;
        }

        GameObject runtime = new("AncientMapProgression");
        return runtime.AddComponent<AncientMapProgression>();
    }

    private void RegisterItems(params ItemData[] items)
    {
        if (GameDataManager.Instance == null)
        {
            return;
        }

        RegistrationBuffer.Clear();
        for (int i = 0; i < items.Length; i++)
        {
            ItemData item = items[i];
            if (!IsMapItem(item))
            {
                continue;
            }

            RegistrationBuffer.Add(item);
            if (IsSecondMapItem(item)) ancientMapItem2 = item;
            else ancientMapItem = item;
        }

        if (RegistrationBuffer.Count > 0)
        {
            GameDataManager.Instance.MergeItemDatabase(RegistrationBuffer);
        }
    }

    private void ActivateGuidance(bool isSecond)
    {
        string objective;
        if (isSecond)
        {
            GameDataManager.Instance?.MarkAncientMap2Used();
            objective = string.IsNullOrWhiteSpace(nextObjectiveText)
                ? DefaultObjective
                : nextObjectiveText.Trim();
        }
        else
        {
            GameDataManager.Instance?.MarkAncientMapUsed();
            objective = FirstObjective;
        }

        if (ZoneObjectiveManager.Instance != null)
        {
            ZoneObjectiveManager.Instance.SetCurrentObjective(objective, true);
        }
        else
        {
            GameDataManager.Instance?.SaveCurrentObjective(objective);
            ObjectiveHUDController.ShowObjective(objective);
        }

        GameDataManager.Instance?.FlushPlayerPrefs();
        if (isSecond)
        {
            PushSecondDestinationToMinimaps();
        }
    }

    private void OpenMap(ItemData item, bool isSecond)
    {
        Sprite resolvedMap = mapImage != null ? mapImage : item.icon;
        Sprite resolvedClue = floatingTreeClueImage != null ? floatingTreeClueImage : resolvedMap;
        AncientNoteUIController.Show(
            resolvedClue,
            resolvedMap,
            isSecond ? PushSecondDestinationToMinimaps : null,
            null,
            isSecond ? secondOpenSfx : openSfx,
            noteUiPrefab,
            isSecond ? SecondTitle : FirstTitle,
            isSecond ? SecondSubtitle : FirstSubtitle,
            isSecond ? SecondMessage : FirstMessage);
    }

    private void PushSecondDestinationToMinimaps()
    {
        Transform resolved = ResolveDestination();
        MinimapController[] minimaps = FindObjectsByType<MinimapController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < minimaps.Length; i++)
        {
            minimaps[i].SetAncientMapDestination(resolved, destinationLabel);
        }

        if (resolved == null)
        {
            Debug.Log("[AncientMap] Note #2 guidance đã mở khóa; chưa gán destination nên không tạo marker.", this);
        }
    }

    private Transform ResolveDestination()
    {
        if (nextDestination != null)
        {
            return nextDestination;
        }

        if (string.IsNullOrWhiteSpace(nextDestinationObjectName))
        {
            return null;
        }

        GameObject destinationObject = GameObject.Find(nextDestinationObjectName.Trim());
        nextDestination = destinationObject != null ? destinationObject.transform : null;
        return nextDestination;
    }
}
