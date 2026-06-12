using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Quản lý cổng Recall/Teleport: scale up, xoay, phát sáng, và teleport
/// </summary>
public class RecallPortal : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private float scaleUpDuration = 1.5f;
    [SerializeField] private Vector3 startScale = new Vector3(0.1f, 0.1f, 0.1f);
    [SerializeField] private Vector3 endScale = Vector3.one;

    [Header("Glow/Emission")]
    [SerializeField] private Material portalMaterial;
    [SerializeField] private float emissionIntensity = 2f;

    [Header("Teleport")]
    [SerializeField] private Transform destinationPoint;
    [SerializeField] private bool useSceneLoad = false;
    [SerializeField] private string targetSceneName = "MainScene";
    [SerializeField] private float teleportDelay = 0.5f;

    [Header("Despawn")]
    [SerializeField] private float autoDestroyTime = 30f;

    private Collider triggerCollider;
    private bool hasPlayerEntered = false;
    private Coroutine scaleCoroutine;
    private Coroutine rotateCoroutine;

    private void Awake()
    {
        // Tìm Trigger Collider
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }

        // Set initial scale
        transform.localScale = startScale;
    }

    private void OnEnable()
    {
        StartAnimations();
    }

    /// <summary>
    /// Initialize cổng với hướng nhìn
    /// </summary>
    public void Initialize(Vector3 lookDirection)
    {
    }

    private void StartAnimations()
    {
        // Scale up animation
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
        }
        scaleCoroutine = StartCoroutine(ScaleUpCoroutine());

        // Rotation animation
        if (rotateCoroutine != null)
        {
            StopCoroutine(rotateCoroutine);
        }
        rotateCoroutine = StartCoroutine(RotateCoroutine());

        // Glow animation
        if (portalMaterial != null)
        {
            StartCoroutine(GlowCoroutine());
        }

        // Auto destroy timer
        Invoke(nameof(DestroyPortal), autoDestroyTime);
    }

    private IEnumerator ScaleUpCoroutine()
    {
        float elapsedTime = 0f;
        while (elapsedTime < scaleUpDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / scaleUpDuration);

            // Smooth scale
            transform.localScale = Vector3.Lerp(startScale, endScale, t);
            yield return null;
        }
        transform.localScale = endScale;
    }

    private IEnumerator RotateCoroutine()
    {
        while (true)
        {
            yield return null;
        }
    }

    private IEnumerator GlowCoroutine()
    {
        Material mat = new Material(portalMaterial);
        GetComponent<Renderer>().material = mat;

        float time = 0f;
        while (true)
        {
            time += Time.deltaTime;
            float pulse = Mathf.Sin(time * 2f) * 0.5f + 1f;
            mat.SetFloat("_EmissionIntensity", emissionIntensity * pulse);
            yield return null;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasPlayerEntered)
        {
            return;
        }

        // Kiểm tra xem có phải Player không
        if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null)
        {
            hasPlayerEntered = true;
            StartCoroutine(TeleportCoroutine(other.gameObject));
        }
    }

    private IEnumerator TeleportCoroutine(GameObject player)
    {
        yield return new WaitForSeconds(teleportDelay);

        if (useSceneLoad)
        {
            SceneManager.LoadScene(targetSceneName);
            Debug.Log($"[Recall Portal] Teleporting to scene: {targetSceneName}");
        }
        else
        {
            // Teleport tới destination point
            if (destinationPoint != null)
            {
                CharacterController controller = player.GetComponent<CharacterController>();
                if (controller != null)
                {
                    controller.enabled = false;
                    player.transform.position = destinationPoint.position;
                    player.transform.rotation = destinationPoint.rotation;
                    controller.enabled = true;
                    Debug.Log($"[Recall Portal] Player teleported to: {destinationPoint.name}");
                }
                else
                {
                    player.transform.position = destinationPoint.position;
                    player.transform.rotation = destinationPoint.rotation;
                }
            }
            else
            {
                Debug.LogWarning("[Recall Portal] Destination point not assigned!");
            }
        }

        DestroyPortal();
    }

    private void DestroyPortal()
    {
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
        }
        if (rotateCoroutine != null)
        {
            StopCoroutine(rotateCoroutine);
        }

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        // Vẽ sphere ở vị trí cổng để dễ visualize
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 1f);

        // Vẽ đường chỉ hướng destination
        if (destinationPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, destinationPoint.position);
            Gizmos.DrawWireSphere(destinationPoint.position, 0.5f);
        }
    }
}
