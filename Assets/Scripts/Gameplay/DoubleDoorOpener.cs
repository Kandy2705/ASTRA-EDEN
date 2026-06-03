using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class DoubleDoorOpener : MonoBehaviour
{
    [Header("Tham chiếu Player")]
    [SerializeField] private Transform player;

    [Header("Cánh cửa")]
    [SerializeField] private Transform leftDoor;
    [SerializeField] private Transform rightDoor;

    [Header("Cài đặt mở")]
    [SerializeField] private float openDistance = 4f;
    [SerializeField] private float slideAmount = 1.5f;
    [SerializeField] private float slideSpeed = 3f;

    [Header("Âm thanh")]
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;
    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 1f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLog = true;

    private Vector3 leftClosedPos;
    private Vector3 rightClosedPos;
    private float currentProgress;
    private bool wasPlayerNear;
    private AudioSource audioSource;

    private void Awake()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            else Debug.LogError($"[{name}] Không tìm thấy Player tag!", this);
        }

        if (leftDoor != null) leftClosedPos = leftDoor.localPosition;
        if (rightDoor != null) rightClosedPos = rightDoor.localPosition;

        if (leftDoor == null || rightDoor == null)
            Debug.LogWarning($"[{name}] Chưa gán đủ LeftDoor/RightDoor.", this);

        // Setup AudioSource
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 1f;
        audioSource.maxDistance = 20f;

        // Cảnh báo nếu gán clip nhưng thiếu AudioListener trong scene
        if ((openSound != null || closeSound != null) && FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 0)
            Debug.LogWarning($"[{name}] Có AudioClip nhưng scene không có AudioListener! Hãy đảm bảo Main Camera có component AudioListener.", this);

        if (showDebugLog)
            Debug.Log($"[{name}] Khởi tạo xong. Player={player}, LeftDoor={leftDoor}, RightDoor={rightDoor}, OpenClip={openSound}, CloseClip={closeSound}");
    }

    private void Update()
    {
        if (player == null || leftDoor == null || rightDoor == null) return;

        float distance = Vector3.Distance(transform.position, player.position);
        bool isPlayerNear = distance <= openDistance;

        // Phát âm thanh khi trạng thái thay đổi
        if (isPlayerNear && !wasPlayerNear)
        {
            if (showDebugLog) Debug.Log($"[{name}] >>> MỞ cửa (Player vào vùng). Distance={distance:F1}");
            PlaySound(openSound, "mở");
        }
        else if (!isPlayerNear && wasPlayerNear)
        {
            if (showDebugLog) Debug.Log($"[{name}] <<< ĐÓNG cửa (Player rời vùng). Distance={distance:F1}");
            PlaySound(closeSound, "đóng");
        }

        wasPlayerNear = isPlayerNear;

        float target = isPlayerNear ? 1f : 0f;
        currentProgress = Mathf.MoveTowards(currentProgress, target, slideSpeed * Time.deltaTime);

        Vector3 slideDir = Vector3.forward;

        if (leftDoor != null)
            leftDoor.localPosition = leftClosedPos - slideDir * (slideAmount * currentProgress);

        if (rightDoor != null)
            rightDoor.localPosition = rightClosedPos + slideDir * (slideAmount * currentProgress);
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, openDistance);
    }
}
