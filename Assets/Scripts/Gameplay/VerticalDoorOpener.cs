using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class VerticalDoorOpener : MonoBehaviour
{
    [Header("Tham chiếu Player")]
    [SerializeField] private Transform player;

    [Header("Cánh cửa")]
    [SerializeField] private Transform door;

    [Header("Cài đặt mở")]
    [SerializeField] private float openDistance = 4f;
    [SerializeField] private float raiseHeight = 3f;
    [SerializeField] private float raiseSpeed = 3f;

    [Header("Tự động đóng")]
    [SerializeField] private bool autoClose = true;

    [Header("Âm thanh")]
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;
    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 1f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLog = true;

    private Vector3 doorClosedPos;
    private float currentProgress;
    private bool hasOpened;
    private bool wasOpening;
    private AudioSource audioSource;

    private void Awake()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            else Debug.LogError($"[{name}] Không tìm thấy Player tag!", this);
        }

        if (door != null) doorClosedPos = door.localPosition;
        else Debug.LogWarning($"[{name}] Chưa gán 'door'.", this);

        // Setup AudioSource
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 1f;
        audioSource.maxDistance = 20f;

        if ((openSound != null || closeSound != null) && FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 0)
            Debug.LogWarning($"[{name}] Có AudioClip nhưng scene không có AudioListener! Hãy đảm bảo Main Camera có component AudioListener.", this);

        if (showDebugLog)
            Debug.Log($"[{name}] Khởi tạo xong. Player={player}, Door={door}, OpenClip={openSound}, CloseClip={closeSound}, AutoClose={autoClose}");
    }

    private void Update()
    {
        if (player == null || door == null) return;

        float distance = Vector3.Distance(transform.position, player.position);
        bool shouldOpen = distance <= openDistance;

        float target;
        if (autoClose)
        {
            target = shouldOpen ? 1f : 0f;
        }
        else
        {
            if (shouldOpen) hasOpened = true;
            target = hasOpened ? 1f : 0f;
        }

        bool isOpeningNow = target > 0.5f;
        bool isClosingNow = target < 0.5f;

        if (isOpeningNow && !wasOpening)
        {
            if (showDebugLog) Debug.Log($"[{name}] >>> MỞ cửa (Player vào vùng). Distance={distance:F1}");
            PlaySound(openSound, "mở");
        }
        else if (isClosingNow && wasOpening)
        {
            if (showDebugLog) Debug.Log($"[{name}] <<< ĐÓNG cửa (Player rời vùng). Distance={distance:F1}");
            PlaySound(closeSound, "đóng");
        }

        wasOpening = isOpeningNow;

        currentProgress = Mathf.MoveTowards(currentProgress, target, raiseSpeed * Time.deltaTime);
        door.localPosition = doorClosedPos + Vector3.up * (raiseHeight * currentProgress);
    }

    private void PlaySound(AudioClip clip, string debugName)
    {
        if (clip == null)
        {
            if (showDebugLog) Debug.LogWarning($"[{name}] Không có AudioClip '{debugName}' được gán. Bỏ qua.");
            return;
        }

        if (audioSource == null)
        {
            Debug.LogError($"[{name}] AudioSource bị null!", this);
            return;
        }

        audioSource.PlayOneShot(clip, soundVolume);
        if (showDebugLog) Debug.Log($"[{name}] Đã phát âm thanh '{debugName}': {clip.name} (volume={soundVolume})");
    }

    public void ResetDoor()
    {
        hasOpened = false;
        currentProgress = 0f;
        wasOpening = false;
        if (door != null) door.localPosition = doorClosedPos;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, openDistance);
    }
}
