using UnityEngine;
using TMPro;

/// <summary>
/// Hiện canvas thoại trên đầu NPC khi Player lại gần,
/// tự ẩn khi Player đi xa. Canvas luôn quay về camera (billboard)
/// và fade mượt bằng CanvasGroup.
/// Gắn script này lên GameObject NPC.
/// </summary>
public class NPCDialogueTrigger : MonoBehaviour
{
    [Header("Tham chiếu")]
    [Tooltip("Transform của Player. Bỏ trống script sẽ tự tìm theo tag 'Player'.")]
    [SerializeField] private Transform player;

    [Tooltip("Camera chính. Bỏ trống script sẽ tự lấy Camera.main.")]
    [SerializeField] private Camera mainCamera;

    [Tooltip("Canvas World Space chứa khung thoại. Nên là con của NPC hoặc object riêng.")]
    [SerializeField] private GameObject dialogueCanvas;

    [Tooltip("Text TMP hiển thị lời thoại.")]
    [SerializeField] private TextMeshProUGUI dialogueText;

    [Header("Cài đặt khoảng cách")]
    [Tooltip("Player ở trong bán kính này (mét) sẽ thấy canvas.")]
    [SerializeField] private float showDistance = 3f;

    [Tooltip("Offset vị trí canvas so với NPC. Mặc định cao hơn đầu 2m.")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 2f, 0f);

    [Header("Nội dung thoại")]
    [TextArea(2, 5)]
    [SerializeField] private string message = "Chạy đi! Nơi này đã bị chiếm đóng bởi sinh vật tha hóa...";

    [Header("Voice khi lại gần")]
    [Tooltip("Bật nếu NPC chỉ được nói một lần trong mỗi lần load scene. Tắt để nói lại mỗi lần Player rời rồi quay lại.")]
    [SerializeField] private AudioClip proximityVoice;
    [SerializeField, Range(0f, 1f)] private float proximityVoiceVolume = 1f;
    [SerializeField] private bool playProximityVoiceOncePerScene = true;

    [Header("Hiệu ứng fade")]
    [Tooltip("Tốc độ fade in/out (alpha mỗi giây).")]
    [SerializeField] private float fadeSpeed = 4f;

    // CanvasGroup dùng để fade alpha
    private CanvasGroup canvasGroup;
    private AudioSource proximityVoiceSource;
    private bool playerWasNearby;
    private bool proximityVoicePlayed;

    private void Awake()
    {
        // Tự tìm Player theo tag nếu chưa gán
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            else Debug.LogWarning($"[NPCDialogueTrigger] Không tìm thấy GameObject có tag 'Player' cho NPC '{name}'.");
        }

        // Tự tìm camera nếu chưa gán
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
                Debug.LogWarning($"[NPCDialogueTrigger] Không tìm thấy Camera.main. Hãy gán tag 'MainCamera' cho camera chính.");
        }

        CreateProximityVoiceSource();

        if (dialogueCanvas == null)
        {
            Debug.LogWarning($"[NPCDialogueTrigger] Chưa gán 'dialogueCanvas' trên NPC '{name}'.");
            return;
        }

        // Gán nội dung thoại
        if (dialogueText != null)
            dialogueText.text = message;

        // Lấy hoặc thêm CanvasGroup để fade
        canvasGroup = dialogueCanvas.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = dialogueCanvas.AddComponent<CanvasGroup>();

        // Bắt đầu ẩn hoàn toàn
        canvasGroup.alpha = 0f;
        dialogueCanvas.SetActive(false);

    }

    private void Update()
    {
        if (player == null) return;

        // Tính khoảng cách giữa NPC và Player
        float distance = Vector3.Distance(transform.position, player.position);
        bool shouldShow = distance <= showDistance;

        if (shouldShow && !playerWasNearby)
        {
            PlayProximityVoice();
        }
        else if (!shouldShow && playerWasNearby && proximityVoiceSource != null)
        {
            // Rời vùng nghe thì dừng câu cũ, lần quay lại sẽ đọc lại sạch sẽ,
            // không bị hai voice chồng lên nhau.
            proximityVoiceSource.Stop();
        }
        playerWasNearby = shouldShow;

        if (dialogueCanvas == null)
        {
            return;
        }

        // Khi cần hiện mà canvas đang tắt, bật lên để bắt đầu fade in
        if (shouldShow && !dialogueCanvas.activeSelf)
            dialogueCanvas.SetActive(true);

        if (dialogueCanvas.activeSelf)
        {
            // Đặt vị trí canvas lên trên đầu NPC
            dialogueCanvas.transform.position = transform.position + offset;

            // Billboard: quay canvas về phía camera mà không bị ngược chữ.
            // Dùng forward = (canvas - camera) để mặt trước canvas hướng ra camera.
            if (mainCamera != null)
            {
                Vector3 dir = dialogueCanvas.transform.position - mainCamera.transform.position;
                dialogueCanvas.transform.rotation = Quaternion.LookRotation(dir);
            }

            // Fade alpha mượt về target (1 nếu gần, 0 nếu xa)
            float targetAlpha = shouldShow ? 1f : 0f;
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);

            // Đã fade out xong thì tắt hẳn để tiết kiệm
            if (!shouldShow && canvasGroup.alpha <= 0f)
                dialogueCanvas.SetActive(false);
        }
    }

    private void CreateProximityVoiceSource()
    {
        if (proximityVoice == null)
        {
            return;
        }

        GameObject sourceObject = new GameObject("NPCProximityVoice");
        sourceObject.transform.SetParent(transform, false);
        proximityVoiceSource = sourceObject.AddComponent<AudioSource>();
        proximityVoiceSource.playOnAwake = false;
        proximityVoiceSource.spatialBlend = 1f;
        proximityVoiceSource.rolloffMode = AudioRolloffMode.Linear;
        proximityVoiceSource.minDistance = 1.5f;
        proximityVoiceSource.maxDistance = Mathf.Max(8f, showDistance * 3f);
        proximityVoiceSource.volume = Mathf.Clamp01(proximityVoiceVolume);
    }

    private void PlayProximityVoice()
    {
        if (proximityVoice == null ||
            proximityVoiceSource == null ||
            (playProximityVoiceOncePerScene && proximityVoicePlayed))
        {
            return;
        }

        proximityVoicePlayed = true;
        proximityVoiceSource.PlayOneShot(proximityVoice, Mathf.Clamp01(proximityVoiceVolume));
    }

    // Vẽ vòng tròn bán kính trong Scene view để dễ tinh chỉnh
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, showDistance);
    }
}
