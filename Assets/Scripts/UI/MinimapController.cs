using System.Collections.Generic;
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
    private readonly List<RectTransform> enemyMarkers = new List<RectTransform>();
    private Transform player;
    private float findPlayerTimer;

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
