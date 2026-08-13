using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Minimap real-time: camera top-down render terrain vào RenderTexture, xoay theo hướng player,
/// hiện icon player (giữa) + icon enemy trong tầm. Tự bootstrap camera/RT/RawImage/markers khi chạy.
[DisallowMultipleComponent]
public class MinimapController : MonoBehaviour
{
    [Header("UI refs (trong HUD_MinimapPanel)")]
    [Tooltip("RectTransform của MinimapMask — nơi chứa RawImage render texture và các marker.")]
    [SerializeField] private RectTransform minimapMask;
    [Tooltip("RectTransform của CompassGroup — xoay theo hướng thật để N/E/S/W trỏ đúng.")]
    [SerializeField] private RectTransform compassGroup;
    [Tooltip("Image tĩnh cũ, sẽ được ẩn khi minimap thật chạy.")]
    [SerializeField] private GameObject legacyStaticImage;

    [Header("Camera")]
    [Tooltip("Bán kính view theo mét (orthographicSize). Nhỏ = zoom gần.")]
    [SerializeField, Min(1f)] private float orthographicSize = 25f;
    [SerializeField, Min(1f)] private float cameraHeight = 60f;
    [SerializeField, Min(1f)] private float farClip = 250f;
    [Tooltip("Layer mà minimap camera render (terrain/environment). Loại UI, Player, Enemy để vẽ bằng icon.")]
    [SerializeField] private LayerMask minimapCullingMask = ~0;
    [SerializeField] private Color backgroundColor = new Color(0.15f, 0.18f, 0.12f, 1f);
    [SerializeField, Min(64)] private int renderTextureSize = 256;

    [Header("Player marker")]
    [SerializeField] private Sprite playerMarkerSprite;
    [SerializeField] private Color playerMarkerColor = new Color(0.4f, 0.85f, 1f, 1f);
    [SerializeField, Min(4f)] private float playerMarkerSize = 22f;

    [Header("Enemy markers")]
    [SerializeField] private Sprite enemyMarkerSprite;
    [SerializeField] private Color enemyMarkerColor = new Color(1f, 0.3f, 0.25f, 1f);
    [SerializeField, Min(4f)] private float enemyMarkerSize = 14f;
    [Tooltip("Khoảng cách tối đa (mét) enemy còn hiện trên minimap. 0 = dùng orthographicSize.")]
    [SerializeField, Min(0f)] private float enemyShowRange = 0f;
    [SerializeField, Range(0, 128)] private int enemyMarkerPool = 32;

    [Header("Objective marker")]
    [Tooltip("Tên GameObject đích trong scene. Marker chỉ hiện khi objective là Find the Floating Tree.")]
    [SerializeField] private string objectiveTargetName = "Flying_Tree_Zone_2";
    [SerializeField] private Sprite objectiveMarkerSprite;
    [SerializeField] private Color objectiveMarkerColor = new Color(1f, 0.72f, 0.24f, 1f);
    [SerializeField, Min(8f)] private float objectiveMarkerSize = 28f;
    [SerializeField, Min(0f)] private float objectiveEdgePadding = 12f;
    [SerializeField, Min(0f)] private float objectiveArrivalDistance = 8f;
    [SerializeField] private Color objectiveRouteColor = new Color(1f, 0.72f, 0.24f, 0.9f);
    [SerializeField] private Color objectiveRouteGlowColor = new Color(0.55f, 0.2f, 1f, 0.42f);
    [SerializeField, Min(1f)] private float objectiveRouteThickness = 3f;
    [Tooltip("Vị trí panel khoảng cách tính từ chính giữa mép trên HUD Canvas.")]
    [SerializeField] private Vector2 objectiveDistanceScreenOffset = new Vector2(0f, -48f);

    [Header("Ancient Map destination (optional)")]
    [Tooltip("Đích cho objective Follow the Ancient Map. Để trống thì objective vẫn hiện nhưng không có marker/route.")]
    [SerializeField] private Transform ancientMapDestination;
    [SerializeField] private string ancientMapDestinationName = "";
    [SerializeField] private string ancientMapDestinationLabel = "ANCIENT DESTINATION";

    [Header("Hướng xoay")]
    [Tooltip("Tắt (mặc định): bản đồ cố định hướng Bắc (N luôn trên). Bật: bản đồ xoay theo player.")]
    [SerializeField] private bool rotateWithPlayer = false;
    [Tooltip("Bật: cụm N/E/S/W xoay theo hướng thật. Tắt (mặc định): 4 nhãn đứng yên như thiết kế gốc.")]
    [SerializeField] private bool rotateCompass = false;
    [Tooltip("Đảo dấu góc xoay compass nếu N/E/S/W trỏ ngược (chỉ khi rotateCompass bật).")]
    [SerializeField] private float compassSign = 1f;
    [Tooltip("Giữ chữ N/E/S/W luôn thẳng đứng khi compass xoay.")]
    [SerializeField] private bool keepLabelsUpright = true;
    [Tooltip("Đảo chiều xoay icon player nếu nó quay ngược hướng.")]
    [SerializeField] private float playerIconSign = 1f;

    private Camera minimapCamera;
    private RenderTexture renderTexture;
    private RawImage mapImage;
    private RectTransform playerMarker;
    private RectTransform objectiveMarker;
    private RectTransform objectiveRouteLine;
    private RectTransform objectiveRouteGlow;
    private Image objectiveRouteLineImage;
    private Image objectiveRouteGlowImage;
    private GameObject objectiveDistancePanel;
    private TMP_Text objectiveDistanceText;
    private readonly List<RectTransform> enemyMarkers = new List<RectTransform>();
    private Transform player;
    private Transform objectiveTarget;
    private float findPlayerTimer;
    private float findObjectiveTimer;
    private ObjectiveGuideKind activeGuideKind;

    private enum ObjectiveGuideKind
    {
        None,
        FloatingTree,
        AncientMap
    }

    private void Start()
    {
        if (minimapMask == null)
        {
            Debug.LogWarning("[Minimap] Chưa gán minimapMask — minimap không chạy.", this);
            enabled = false;
            return;
        }

        BuildRenderTexture();
        BuildCamera();
        BuildMapImage();
        BuildMarkers();

        if (legacyStaticImage != null) legacyStaticImage.SetActive(false);
        AcquirePlayer();
    }

    private void OnEnable()
    {
        PlayerLoadoutRuntime.ActivePlayerChanged += HandleActivePlayerChanged;
    }

    private void OnDisable()
    {
        PlayerLoadoutRuntime.ActivePlayerChanged -= HandleActivePlayerChanged;
    }

    private void HandleActivePlayerChanged(PlayerLoadoutRuntime activePlayer)
    {
        player = activePlayer != null ? activePlayer.transform : null;
    }

    private void BuildRenderTexture()
    {
        renderTexture = new RenderTexture(renderTextureSize, renderTextureSize, 16, RenderTextureFormat.Default)
        {
            name = "RT_Minimap",
            antiAliasing = 1,
            useMipMap = false,
            wrapMode = TextureWrapMode.Clamp
        };
        renderTexture.Create();
    }

    private void BuildCamera()
    {
        var camGo = new GameObject("MinimapCamera");
        minimapCamera = camGo.AddComponent<Camera>();
        minimapCamera.orthographic = true;
        minimapCamera.orthographicSize = orthographicSize;
        minimapCamera.clearFlags = CameraClearFlags.SolidColor;
        minimapCamera.backgroundColor = backgroundColor;
        minimapCamera.cullingMask = minimapCullingMask;
        minimapCamera.nearClipPlane = 0.1f;
        minimapCamera.farClipPlane = farClip;
        minimapCamera.targetTexture = renderTexture;
        minimapCamera.allowMSAA = false;
        minimapCamera.allowHDR = false;
        minimapCamera.depth = -50; // render trước Main Camera, không lên màn hình
    }

    private void BuildMapImage()
    {
        var go = new GameObject("MinimapRenderImage", typeof(RectTransform), typeof(RawImage));
        var rt = (RectTransform)go.transform;
        rt.SetParent(minimapMask, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.SetAsFirstSibling(); // nằm dưới các marker

        mapImage = go.GetComponent<RawImage>();
        mapImage.texture = renderTexture;
        mapImage.raycastTarget = false;
    }

    private void BuildMarkers()
    {
        playerMarker = CreateMarker("PlayerMarker", playerMarkerSprite, playerMarkerColor, playerMarkerSize);
        playerMarker.anchoredPosition = Vector2.zero;

        for (int i = 0; i < enemyMarkerPool; i++)
        {
            var m = CreateMarker("EnemyMarker_" + i, enemyMarkerSprite, enemyMarkerColor, enemyMarkerSize);
            m.gameObject.SetActive(false);
            enemyMarkers.Add(m);
        }

        BuildObjectiveMarker();
    }

    private void BuildObjectiveMarker()
    {
        BuildObjectiveRoute();

        Sprite markerSprite = objectiveMarkerSprite != null
            ? objectiveMarkerSprite
            : playerMarkerSprite;
        objectiveMarker = CreateMarker(
            "FloatingTreeObjectiveMarker",
            markerSprite,
            objectiveMarkerColor,
            objectiveMarkerSize);
        objectiveMarker.SetAsLastSibling();

        Image markerImage = objectiveMarker.GetComponent<Image>();
        Outline outline = markerImage.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.48f, 0.16f, 0.75f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);

        BuildObjectiveDistancePanel();
        objectiveMarker.gameObject.SetActive(false);
    }

    private void BuildObjectiveRoute()
    {
        objectiveRouteGlow = CreateRouteLine(
            "FloatingTreeRouteGlow",
            objectiveRouteGlowColor,
            objectiveRouteThickness * 2.6f,
            out objectiveRouteGlowImage);
        objectiveRouteLine = CreateRouteLine(
            "FloatingTreeRouteLine",
            objectiveRouteColor,
            objectiveRouteThickness,
            out objectiveRouteLineImage);

        // Render texture nằm ở sibling 0; hai đường nằm trên map nhưng dưới marker.
        objectiveRouteGlow.SetSiblingIndex(Mathf.Min(1, minimapMask.childCount - 1));
        objectiveRouteLine.SetSiblingIndex(Mathf.Min(2, minimapMask.childCount - 1));
        objectiveRouteGlow.gameObject.SetActive(false);
        objectiveRouteLine.gameObject.SetActive(false);
    }

    private RectTransform CreateRouteLine(
        string objectName,
        Color color,
        float thickness,
        out Image lineImage)
    {
        GameObject lineObject = new(objectName, typeof(RectTransform), typeof(Image));
        RectTransform line = lineObject.GetComponent<RectTransform>();
        line.SetParent(minimapMask, false);
        line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.5f);
        line.pivot = new Vector2(0f, 0.5f);
        line.anchoredPosition = Vector2.zero;
        line.sizeDelta = new Vector2(0f, thickness);
        lineImage = lineObject.GetComponent<Image>();
        lineImage.color = color;
        lineImage.raycastTarget = false;
        return line;
    }

    private void BuildObjectiveDistancePanel()
    {
        objectiveDistancePanel = new GameObject(
            "FloatingTreeDistancePanel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Outline));
        RectTransform panelRect = objectiveDistancePanel.GetComponent<RectTransform>();
        Canvas hudCanvas = GetComponentInParent<Canvas>();
        Transform panelParent = hudCanvas != null ? hudCanvas.transform : transform;
        panelRect.SetParent(panelParent, false);
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = objectiveDistanceScreenOffset;
        panelRect.sizeDelta = new Vector2(300f, 44f);
        panelRect.SetAsLastSibling();

        Image panelImage = objectiveDistancePanel.GetComponent<Image>();
        panelImage.color = new Color(0.055f, 0.018f, 0.09f, 0.94f);
        panelImage.raycastTarget = false;
        Outline panelOutline = objectiveDistancePanel.GetComponent<Outline>();
        panelOutline.effectColor = new Color(0.95f, 0.68f, 0.24f, 0.95f);
        panelOutline.effectDistance = new Vector2(2f, -2f);

        GameObject distanceObject = new("Distance", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform distanceRect = distanceObject.GetComponent<RectTransform>();
        distanceRect.SetParent(panelRect, false);
        distanceRect.anchorMin = Vector2.zero;
        distanceRect.anchorMax = Vector2.one;
        distanceRect.offsetMin = new Vector2(8f, 3f);
        distanceRect.offsetMax = new Vector2(-8f, -3f);
        objectiveDistanceText = distanceObject.GetComponent<TextMeshProUGUI>();
        objectiveDistanceText.font = TMP_Settings.defaultFontAsset;
        objectiveDistanceText.fontSize = 16f;
        objectiveDistanceText.fontStyle = FontStyles.Bold;
        objectiveDistanceText.alignment = TextAlignmentOptions.Center;
        objectiveDistanceText.color = new Color(1f, 0.88f, 0.56f, 1f);
        objectiveDistanceText.raycastTarget = false;
        objectiveDistanceText.text = "FLOATING TREE  |  LOCATING...";
        objectiveDistancePanel.SetActive(false);
    }

    private RectTransform CreateMarker(string markerName, Sprite sprite, Color color, float size)
    {
        var go = new GameObject(markerName, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(minimapMask, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    private void LateUpdate()
    {
        if (minimapCamera == null) return;

        if (player == null)
        {
            findPlayerTimer -= Time.deltaTime;
            if (findPlayerTimer <= 0f) { AcquirePlayer(); findPlayerTimer = 1f; }
            if (player == null) return;
        }

        float playerYaw = player.eulerAngles.y;
        float camYaw = rotateWithPlayer ? playerYaw : 0f;

        // Camera bám player. North-up: camYaw = 0 (Bắc luôn trên). Rotate-with-player: camYaw = playerYaw.
        minimapCamera.transform.SetPositionAndRotation(
            player.position + Vector3.up * cameraHeight,
            Quaternion.Euler(90f, camYaw, 0f));

        // Icon player xoay theo hướng đi so với hướng "lên" của bản đồ.
        if (playerMarker != null)
            playerMarker.localRotation = Quaternion.Euler(0f, 0f, (camYaw - playerYaw) * playerIconSign);

        UpdateCompass(camYaw);
        UpdateEnemyMarkers();
        UpdateObjectiveMarker();
    }

    private void UpdateCompass(float yaw)
    {
        if (compassGroup == null || !rotateCompass) return;
        float z = yaw * compassSign;
        compassGroup.localRotation = Quaternion.Euler(0f, 0f, z);

        if (keepLabelsUpright)
        {
            var counter = Quaternion.Euler(0f, 0f, -z);
            int count = compassGroup.childCount;
            for (int i = 0; i < count; i++)
                compassGroup.GetChild(i).localRotation = counter;
        }
    }

    private void UpdateEnemyMarkers()
    {
        float uiRadius = minimapMask.rect.width * 0.5f;
        float worldToUi = uiRadius / orthographicSize;
        float range = enemyShowRange > 0f ? enemyShowRange : orthographicSize;
        float rangeSqr = range * range;

        Vector3 right = rotateWithPlayer ? player.right : Vector3.right;
        Vector3 forward = rotateWithPlayer ? player.forward : Vector3.forward;

        var list = EnemyAIController.Active;
        int slot = 0;
        for (int i = 0; i < list.Count && slot < enemyMarkers.Count; i++)
        {
            var enemy = list[i];
            if (enemy == null) continue;

            Vector3 offset = enemy.transform.position - player.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > rangeSqr) continue;

            float localX = Vector3.Dot(offset, right);
            float localY = Vector3.Dot(offset, forward);
            Vector2 uiPos = new Vector2(localX, localY) * worldToUi;

            // Clamp vào mép vòng tròn.
            if (uiPos.magnitude > uiRadius) uiPos = uiPos.normalized * uiRadius;

            var marker = enemyMarkers[slot];
            if (!marker.gameObject.activeSelf) marker.gameObject.SetActive(true);
            marker.anchoredPosition = uiPos;
            slot++;
        }

        for (int i = slot; i < enemyMarkers.Count; i++)
        {
            if (enemyMarkers[i].gameObject.activeSelf)
                enemyMarkers[i].gameObject.SetActive(false);
        }
    }

    private void UpdateObjectiveMarker()
    {
        ObjectiveGuideKind requestedKind = GetObjectiveGuideKind();
        if (objectiveMarker == null || requestedKind == ObjectiveGuideKind.None)
        {
            activeGuideKind = ObjectiveGuideKind.None;
            objectiveTarget = null;
            SetObjectiveGuideActive(false, false);
            return;
        }

        if (activeGuideKind != requestedKind)
        {
            activeGuideKind = requestedKind;
            objectiveTarget = null;
            findObjectiveTimer = 0f;
        }

        if (!TryResolveObjectiveTarget(requestedKind, out Transform resolvedTarget))
        {
            bool showLocating = requestedKind == ObjectiveGuideKind.FloatingTree;
            SetObjectiveGuideActive(false, showLocating);
            if (showLocating && objectiveDistanceText != null)
            {
                objectiveDistanceText.text = "FLOATING TREE  |  LOCATING...";
            }

            return;
        }

        objectiveTarget = resolvedTarget;
        if (objectiveDistancePanel != null && !objectiveDistancePanel.activeSelf)
        {
            objectiveDistancePanel.SetActive(true);
        }

        Vector3 offset = objectiveTarget.position - player.position;
        offset.y = 0f;
        float distance = offset.magnitude;
        if (distance <= objectiveArrivalDistance)
        {
            SetObjectiveGuideActive(false, true);
            if (objectiveDistanceText != null)
            {
                objectiveDistanceText.text = $"{GetObjectiveLabel(requestedKind)}  |  ARRIVED";
            }
            return;
        }

        float uiRadius = Mathf.Max(1f, minimapMask.rect.width * 0.5f - objectiveEdgePadding);
        float worldToUi = uiRadius / orthographicSize;
        Vector3 right = rotateWithPlayer ? player.right : Vector3.right;
        Vector3 forward = rotateWithPlayer ? player.forward : Vector3.forward;
        Vector2 uiPosition = new(
            Vector3.Dot(offset, right) * worldToUi,
            Vector3.Dot(offset, forward) * worldToUi);

        bool outsideMap = uiPosition.magnitude > uiRadius;
        if (outsideMap)
        {
            uiPosition = uiPosition.normalized * uiRadius;
        }

        if (!objectiveMarker.gameObject.activeSelf)
        {
            objectiveMarker.gameObject.SetActive(true);
        }

        objectiveMarker.anchoredPosition = uiPosition;
        UpdateRouteLine(objectiveRouteGlow, uiPosition, objectiveRouteThickness * 2.6f);
        UpdateRouteLine(objectiveRouteLine, uiPosition, objectiveRouteThickness);
        float pulse = 1f + Mathf.Sin(Time.unscaledTime * 4f) * 0.12f;
        objectiveMarker.localScale = Vector3.one * pulse;

        float routePulse = 0.78f + (Mathf.Sin(Time.unscaledTime * 3.2f) + 1f) * 0.11f;
        if (objectiveRouteLineImage != null)
        {
            Color color = objectiveRouteColor;
            color.a *= routePulse;
            objectiveRouteLineImage.color = color;
        }
        if (objectiveRouteGlowImage != null)
        {
            Color color = objectiveRouteGlowColor;
            color.a *= routePulse;
            objectiveRouteGlowImage.color = color;
        }

        if (objectiveDistanceText != null)
        {
            string label = GetObjectiveLabel(requestedKind);
            objectiveDistanceText.text = distance >= 1000f
                ? $"{label}  |  {distance / 1000f:0.0} km"
                : $"{label}  |  {Mathf.RoundToInt(distance)} m";
        }
    }

    public void SetAncientMapDestination(Transform destination, string label = null)
    {
        ancientMapDestination = destination;
        if (!string.IsNullOrWhiteSpace(label))
        {
            ancientMapDestinationLabel = label.Trim();
        }

        if (activeGuideKind == ObjectiveGuideKind.AncientMap)
        {
            objectiveTarget = destination;
            findObjectiveTimer = 0f;
        }
    }

    private bool TryResolveObjectiveTarget(ObjectiveGuideKind kind, out Transform target)
    {
        if (kind == ObjectiveGuideKind.AncientMap && ancientMapDestination != null)
        {
            target = ancientMapDestination;
            return true;
        }

        string targetName = kind == ObjectiveGuideKind.FloatingTree
            ? objectiveTargetName
            : ancientMapDestinationName;
        if (string.IsNullOrWhiteSpace(targetName))
        {
            target = null;
            return false;
        }

        if (objectiveTarget == null)
        {
            findObjectiveTimer -= Time.unscaledDeltaTime;
            if (findObjectiveTimer <= 0f)
            {
                GameObject targetObject = GameObject.Find(targetName.Trim());
                objectiveTarget = targetObject != null ? targetObject.transform : null;
                findObjectiveTimer = 1f;
            }
        }

        target = objectiveTarget;
        return target != null;
    }

    private string GetObjectiveLabel(ObjectiveGuideKind kind)
    {
        if (kind == ObjectiveGuideKind.FloatingTree)
        {
            return "FLOATING TREE";
        }

        return string.IsNullOrWhiteSpace(ancientMapDestinationLabel)
            ? "ANCIENT DESTINATION"
            : ancientMapDestinationLabel.Trim().ToUpperInvariant();
    }

    private void UpdateRouteLine(RectTransform line, Vector2 destination, float thickness)
    {
        if (line == null)
        {
            return;
        }

        if (!line.gameObject.activeSelf)
        {
            line.gameObject.SetActive(true);
        }

        line.anchoredPosition = Vector2.zero;
        line.sizeDelta = new Vector2(destination.magnitude, thickness);
        float angle = Mathf.Atan2(destination.y, destination.x) * Mathf.Rad2Deg;
        line.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void SetObjectiveGuideActive(bool showRouteAndMarker, bool showDistancePanel)
    {
        if (objectiveMarker != null) objectiveMarker.gameObject.SetActive(showRouteAndMarker);
        if (objectiveRouteLine != null) objectiveRouteLine.gameObject.SetActive(showRouteAndMarker);
        if (objectiveRouteGlow != null) objectiveRouteGlow.gameObject.SetActive(showRouteAndMarker);
        if (objectiveDistancePanel != null) objectiveDistancePanel.SetActive(showDistancePanel);
    }

    private static ObjectiveGuideKind GetObjectiveGuideKind()
    {
        string currentObjective = ZoneObjectiveManager.Instance != null
            ? ZoneObjectiveManager.Instance.CurrentObjective
            : GameDataManager.Instance != null
                ? GameDataManager.Instance.CurrentObjective
                : string.Empty;
        if (string.Equals(currentObjective, "Find the Floating Tree", System.StringComparison.OrdinalIgnoreCase))
        {
            return GameDataManager.Instance != null &&
                   GameDataManager.Instance.IsAncientMapGuidanceUnlocked
                ? ObjectiveGuideKind.FloatingTree
                : ObjectiveGuideKind.None;
        }

        if (AncientMapProgression.IsGuidanceObjective(currentObjective) &&
            GameDataManager.Instance != null &&
            GameDataManager.Instance.IsAncientMap2GuidanceUnlocked)
        {
            return ObjectiveGuideKind.AncientMap;
        }

        return ObjectiveGuideKind.None;
    }

    private void AcquirePlayer()
    {
        var go = GameObject.FindGameObjectWithTag("Player");
        if (go != null) player = go.transform;
    }

    private void OnDestroy()
    {
        if (minimapCamera != null) Destroy(minimapCamera.gameObject);
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }
}
