using AillieoUtils.LOS2D;
using UnityEngine;

/// <summary>
/// Bridge Aillieo UnityLOS2D (com.aillieo.los-2d) → enemy perception.
/// Gắn trên enemy root cùng EnemySensor / EnemyAIController.
///
/// Package dùng Physics.Raycast 3D + sector trên mặt phẳng XZ (tên "2D" = top-down sector).
/// ASTRA fork: <see cref="LOSManager.heightTolerance"/> = 8m (upstream 1m quá thấp cho dino).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemySensor))]
public sealed class EnemyLOS2DBridge : MonoBehaviour
{
    [Header("Aillieo LOS2D")]
    [SerializeField] private LOSSource losSource;
    [Tooltip("Mesh sector debug (optional). Nặng nếu nhiều enemy — chỉ bật boss/debug.")]
    [SerializeField] private LOSMesh losMesh;
    [SerializeField] private bool createMeshVisual;
    [SerializeField, Range(8, 128)] private int meshResolution = 36;
    [SerializeField] private bool drawSightMesh = true;
    [SerializeField] private bool drawHiddenMesh;

    [Header("Facing")]
    [Tooltip("Đồng bộ EnemyAIController.flipForward180 — forward model -Z.")]
    [SerializeField] private bool flipForward180;
    [Tooltip("Child transform mang LOSSource (để xoay đúng khi flip). Auto tạo nếu trống.")]
    [SerializeField] private Transform losFacingRoot;

    [Header("Masks")]
    [Tooltip("Layer raycast bắt target + obstacle (phải gồm layer Player).")]
    [SerializeField] private LayerMask maskForEvent;
    [SerializeField] private LayerMask maskForRender;

    [Header("Eye")]
    [SerializeField] private float eyeHeight = 1.2f;

    EnemySensor sensor;
    EnemyAIController ai;

    public LOSSource Source => losSource;

    void Awake()
    {
        sensor = GetComponent<EnemySensor>();
        ai = GetComponent<EnemyAIController>();
        EnsureSetup();
        ApplyFromSensorOrData();
    }

    void LateUpdate()
    {
        // Giữ facing đúng sau khi AI xoay body (flip model).
        UpdateLosFacing();
        ApplyFromSensorOrData();
    }

    public void ConfigureFromSensor(EnemySensor enemySensor, bool flipForward)
    {
        sensor = enemySensor != null ? enemySensor : GetComponent<EnemySensor>();
        flipForward180 = flipForward;
        EnsureSetup();
        ApplyFromSensorOrData();
    }

    public void SetFlipForward(bool flip)
    {
        flipForward180 = flip;
    }

    /// <summary>Player (có LOSTarget) có đang trong sight của source này không?</summary>
    public bool IsPlayerInSight()
    {
        if (losSource == null)
        {
            return false;
        }

        LOSTarget playerTarget = PlayerLOSTarget.Instance != null
            ? PlayerLOSTarget.Instance.Target
            : null;

        if (playerTarget == null)
        {
            LOSTarget[] all = FindObjectsByType<LOSTarget>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null)
                {
                    continue;
                }

                Transform root = all[i].transform.root;
                if (all[i].CompareTag("Player") || root.CompareTag("Player"))
                {
                    playerTarget = all[i];
                    break;
                }
            }
        }

        if (playerTarget == null)
        {
            return false;
        }

        return LOSManager.IsInSight(losSource, playerTarget);
    }

    void EnsureSetup()
    {
        if (maskForEvent.value == 0)
        {
            // Player + Default + Environment-ish
            int player = LayerMask.GetMask("Player");
            maskForEvent = player != 0 ? player | 1 : ~0;
        }

        if (maskForRender.value == 0)
        {
            maskForRender = maskForEvent;
        }

        if (losFacingRoot == null)
        {
            Transform existing = transform.Find("LOS2D_Facing");
            if (existing != null)
            {
                losFacingRoot = existing;
            }
            else
            {
                var go = new GameObject("LOS2D_Facing");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                losFacingRoot = go.transform;
            }
        }

        if (losSource == null)
        {
            losSource = losFacingRoot.GetComponent<LOSSource>();
        }

        if (losSource == null)
        {
            losSource = losFacingRoot.gameObject.AddComponent<LOSSource>();
        }

        // LOSSource nên nằm trên facing root (không phải body flipped).
        if (losSource.transform != losFacingRoot)
        {
            // Move component isn't free — ensure component is on facing root.
            if (losSource.gameObject != losFacingRoot.gameObject)
            {
                Destroy(losSource);
                losSource = losFacingRoot.gameObject.AddComponent<LOSSource>();
            }
        }

        losSource.maskForEvent = maskForEvent;
        losSource.maskForRender = maskForRender;
        losSource.eyeHeight = eyeHeight;

        if (createMeshVisual)
        {
            EnsureMesh();
        }
        else if (losMesh != null)
        {
            losMesh.enabled = false;
        }
    }

    void EnsureMesh()
    {
        if (losMesh == null)
        {
            losMesh = losFacingRoot.GetComponent<LOSMesh>();
        }

        if (losMesh == null)
        {
            losMesh = losFacingRoot.gameObject.AddComponent<LOSMesh>();
        }

        if (losFacingRoot.GetComponent<MeshFilter>() == null)
        {
            losFacingRoot.gameObject.AddComponent<MeshFilter>();
        }

        if (losFacingRoot.GetComponent<MeshRenderer>() == null)
        {
            var mr = losFacingRoot.gameObject.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        losMesh.enabled = true;
        losMesh.associatedLOSSource = losSource;
        losMesh.resolution = meshResolution;
        losMesh.drawSight = drawSightMesh;
        losMesh.drawHidden = drawHiddenMesh;
        losMesh.drawSimpleSector = false;
        losMesh.autoRegenerateMesh = true;
    }

    void ApplyFromSensorOrData()
    {
        if (losSource == null)
        {
            return;
        }

        float range = 14f;
        float angle = 110f;

        if (sensor != null)
        {
            range = sensor.SightRange;
            angle = sensor.SightAngle;
        }
        else if (ai != null && ai.Data != null)
        {
            range = ai.Data.sightRange;
            angle = ai.Data.sightAngle;
        }

        // Package FOV max 180.
        losSource.fov = Mathf.Clamp(angle, 1f, 180f);
        losSource.maxDist = Mathf.Max(0.5f, range);
        losSource.eyeHeight = eyeHeight;
        losSource.maskForEvent = maskForEvent;
        losSource.maskForRender = maskForRender != 0 ? maskForRender : maskForEvent;
    }

    void UpdateLosFacing()
    {
        if (losFacingRoot == null)
        {
            return;
        }

        Vector3 forward = transform.forward;
        if (flipForward180)
        {
            forward = -forward;
        }

        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            return;
        }

        losFacingRoot.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        losFacingRoot.position = transform.position;
    }
}
