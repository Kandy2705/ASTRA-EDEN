using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Multi-ray FOV line-of-sight inspired by eoger/unity-lineofsight
/// (https://github.com/eoger/unity-lineofsight).
///
/// Algorithm:
/// 1) Cast N rays across the FOV cone (subdivisions).
/// 2) If two adjacent rays hit different colliders (or hit/miss differs),
///    recursively cast more rays between them (maxIterations).
/// 3) Missed rays stop at maxRange — open-world safe (original required closed scene).
/// 4) Optional: build a flat fan mesh for debug visualization.
///
/// Gameplay detection uses <see cref="CanSeePoint"/> (FOV + single LOS ray).
/// Mesh/fan rebuild is only for visualization / gizmos.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyLineOfSight : MonoBehaviour
{
    const float EpsilonSqr = 0.0001f;

    [Header("Source")]
    [Tooltip("Origin of rays. Empty = this transform + eyeHeight.")]
    [SerializeField] private Transform eye;
    [SerializeField, Min(0f)] private float eyeHeight = 1.6f;

    [Header("FOV")]
    [SerializeField, Min(0.5f)] private float maxRange = 14f;
    [SerializeField, Range(10f, 360f)] private float fovAngle = 110f;
    [Tooltip("Tick nếu model forward thật là -Z (giống EnemyAIController.flipForward180).")]
    [SerializeField] private bool flipForward180;

    [Header("Ray Fan (eoger-style edge refine)")]
    [SerializeField, Range(4, 64)] private int subdivisions = 20;
    [SerializeField, Range(1, 4)] private int maxIterations = 2;
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Rebuild")]
    [SerializeField, Min(0.02f)] private float rebuildInterval = 0.1f;
    [SerializeField] private bool rebuildOnlyWhenVisible = true;

    [Header("Mesh Visualization (optional)")]
    [SerializeField] private bool generateMesh;
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private Color meshColor = new Color(1f, 0.85f, 0.15f, 0.18f);
    [SerializeField] private float meshYOffset = 0.05f;

    [Header("Debug Gizmos")]
    [SerializeField] private bool drawGizmos;
    [SerializeField] private Color gizmoColor = new Color(1f, 0.85f, 0.15f, 0.9f);
    [SerializeField] private Color gizmoHitColor = new Color(1f, 0.4f, 0.1f, 0.9f);

    readonly List<Vector3> worldHits = new List<Vector3>(96);
    readonly List<Vector3> localHits = new List<Vector3>(96);
    Mesh runtimeMesh;
    Material runtimeMaterial;
    float nextRebuildTime;
    bool hasBuiltOnce;

    struct Sample
    {
        public Vector3 end;
        public Transform hitTransform; // null if miss
        public bool hit;
    }

    public float MaxRange
    {
        get => maxRange;
        set => maxRange = Mathf.Max(0.5f, value);
    }

    public float FovAngle
    {
        get => fovAngle;
        set => fovAngle = Mathf.Clamp(value, 10f, 360f);
    }

    public bool FlipForward180
    {
        get => flipForward180;
        set => flipForward180 = value;
    }

    public LayerMask ObstacleMask
    {
        get => obstacleMask;
        set => obstacleMask = value;
    }

    public bool GenerateMesh
    {
        get => generateMesh;
        set => generateMesh = value;
    }

    public bool DrawGizmos
    {
        get => drawGizmos;
        set => drawGizmos = value;
    }

    public IReadOnlyList<Vector3> WorldHitPoints => worldHits;

    public void Configure(float range, float angleDegrees, LayerMask obstacles, bool flipForward, bool enableMesh = false)
    {
        maxRange = Mathf.Max(0.5f, range);
        fovAngle = Mathf.Clamp(angleDegrees, 10f, 360f);
        obstacleMask = obstacles;
        flipForward180 = flipForward;
        generateMesh = enableMesh;
        Invalidate();
    }

    public void SetEye(Transform eyeTransform, float height)
    {
        eye = eyeTransform;
        eyeHeight = height;
        Invalidate();
    }

    public void Invalidate() => nextRebuildTime = 0f;

    public Vector3 GetEyePosition()
    {
        if (eye != null)
        {
            return eye.position;
        }

        return transform.position + Vector3.up * eyeHeight;
    }

    public Vector3 GetForwardFlat()
    {
        Vector3 forward = transform.forward;
        if (flipForward180)
        {
            forward = -forward;
        }

        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            return Vector3.forward;
        }

        return forward.normalized;
    }

    /// <summary>
    /// Gameplay LOS: point in FOV cone + no obstacle between eye and point.
    /// </summary>
    public bool CanSeePoint(Vector3 worldPoint, float targetHeightOffset = 0f)
    {
        Vector3 eyePos = GetEyePosition();
        Vector3 targetPos = worldPoint + Vector3.up * targetHeightOffset;
        Vector3 toTarget = targetPos - eyePos;
        float dist = toTarget.magnitude;
        if (dist <= 0.01f)
        {
            return true;
        }

        if (dist > maxRange)
        {
            return false;
        }

        Vector3 flat = toTarget;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        Vector3 forward = GetForwardFlat();
        float half = fovAngle * 0.5f;
        if (Vector3.Angle(forward, flat.normalized) > half)
        {
            return false;
        }

        Vector3 dir = toTarget / dist;
        if (Physics.Raycast(eyePos, dir, out RaycastHit hit, dist, obstacleMask, triggerInteraction))
        {
            // Hit something before the target → blocked.
            if (dist - hit.distance > 0.15f)
            {
                return false;
            }
        }

        return true;
    }

    public bool CanSeeTarget(Transform target, float chestHeight = 1f)
    {
        return target != null && CanSeePoint(target.position, chestHeight);
    }

    void LateUpdate()
    {
        if (!generateMesh && !drawGizmos)
        {
            return;
        }

        if (rebuildOnlyWhenVisible && !IsVisibleToCamera())
        {
            return;
        }

        if (Time.time < nextRebuildTime && hasBuiltOnce)
        {
            return;
        }

        nextRebuildTime = Time.time + rebuildInterval;
        RebuildFan();
        if (generateMesh)
        {
            ApplyMesh();
        }

        hasBuiltOnce = true;
    }

    void OnDisable()
    {
        DestroyRuntimeMesh();
    }

    void OnDestroy()
    {
        DestroyRuntimeMesh();
        if (runtimeMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(runtimeMaterial);
            }
            else
            {
                DestroyImmediate(runtimeMaterial);
            }

            runtimeMaterial = null;
        }
    }

    void DestroyRuntimeMesh()
    {
        if (runtimeMesh == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(runtimeMesh);
        }
        else
        {
            DestroyImmediate(runtimeMesh);
        }

        runtimeMesh = null;
    }

    bool IsVisibleToCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            return true;
        }

        Vector3 vp = cam.WorldToViewportPoint(transform.position);
        return vp.z > 0f && vp.x > -0.2f && vp.x < 1.2f && vp.y > -0.2f && vp.y < 1.2f;
    }

    public void RebuildFan()
    {
        worldHits.Clear();
        localHits.Clear();

        Vector3 eyePos = GetEyePosition();
        Vector3 forward = GetForwardFlat();
        float halfRad = fovAngle * 0.5f * Mathf.Deg2Rad;
        float centerAngle = Mathf.Atan2(forward.x, forward.z);
        float startAngle = centerAngle - halfRad;
        float endAngle = centerAngle + halfRad;

        CastSector(eyePos, startAngle, endAngle, 0, appendFirst: true);

        // Strip near-duplicates.
        for (int i = worldHits.Count - 1; i > 0; i--)
        {
            if ((worldHits[i] - worldHits[i - 1]).sqrMagnitude < EpsilonSqr)
            {
                worldHits.RemoveAt(i);
            }
        }

        for (int i = 0; i < worldHits.Count; i++)
        {
            Vector3 local = transform.InverseTransformPoint(worldHits[i]);
            localHits.Add(new Vector3(local.x, meshYOffset, local.z));
        }
    }

    /// <summary>
    /// Recursive sector cast (eoger CastRays).
    /// appendFirst: add sample 0; when refining a sub-sector, pass false to avoid duplicating the shared edge.
    /// </summary>
    void CastSector(Vector3 eyePos, float startAngle, float stopAngle, int iter, bool appendFirst)
    {
        float span = stopAngle - startAngle;
        if (span < 1e-5f)
        {
            return;
        }

        int rays = Mathf.Max(2, subdivisions + 1);
        float step = span / (rays - 1);
        Sample[] samples = new Sample[rays];

        for (int i = 0; i < rays; i++)
        {
            float angle = startAngle + step * i;
            samples[i] = SampleDirection(eyePos, angle);
        }

        if (appendFirst)
        {
            worldHits.Add(samples[0].end);
        }

        for (int i = 1; i < rays; i++)
        {
            if (NeedsRefine(samples[i - 1], samples[i]) && iter < maxIterations - 1)
            {
                float subStart = startAngle + step * (i - 1);
                float subStop = startAngle + step * i;
                // Skip first sample of sub-sector (already present as previous end / refined trail).
                CastSector(eyePos, subStart, subStop, iter + 1, appendFirst: false);
            }

            worldHits.Add(samples[i].end);
        }
    }

    static bool NeedsRefine(in Sample a, in Sample b)
    {
        if (a.hit != b.hit)
        {
            return true;
        }

        if (a.hit && b.hit && a.hitTransform != b.hitTransform)
        {
            return true;
        }

        // Large depth jump between adjacent rays → likely an outer edge.
        if (a.hit && b.hit)
        {
            float da = Vector3.Distance(a.end, b.end);
            if (da > 1.5f)
            {
                return true;
            }
        }

        return false;
    }

    Sample SampleDirection(Vector3 eyePos, float angle)
    {
        Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        Sample s = default;
        if (Physics.Raycast(eyePos, dir, out RaycastHit hit, maxRange, obstacleMask, triggerInteraction)
            && hit.collider != null)
        {
            s.hit = true;
            s.hitTransform = hit.transform;
            s.end = hit.point;
        }
        else
        {
            // Open world fix: miss → full range (original repo needed closed geometry).
            s.hit = false;
            s.hitTransform = null;
            s.end = eyePos + dir * maxRange;
        }

        return s;
    }

    void ApplyMesh()
    {
        EnsureMeshComponents();
        if (meshFilter == null)
        {
            return;
        }

        if (runtimeMesh == null)
        {
            runtimeMesh = new Mesh { name = "EnemyLOS_Mesh" };
            runtimeMesh.MarkDynamic();
        }

        int hitCount = localHits.Count;
        if (hitCount < 2)
        {
            runtimeMesh.Clear();
            meshFilter.sharedMesh = runtimeMesh;
            return;
        }

        // Fan from eye origin — correct for polar LOS (no external Triangulator needed).
        Vector3 originLocal = transform.InverseTransformPoint(GetEyePosition());
        originLocal.y = meshYOffset;

        int vertCount = hitCount + 1;
        var verts = new Vector3[vertCount];
        verts[0] = originLocal;
        for (int i = 0; i < hitCount; i++)
        {
            verts[i + 1] = localHits[i];
        }

        var tris = new int[(hitCount - 1) * 3];
        int t = 0;
        for (int i = 0; i < hitCount - 1; i++)
        {
            tris[t++] = 0;
            tris[t++] = i + 1;
            tris[t++] = i + 2;
        }

        runtimeMesh.Clear();
        runtimeMesh.vertices = verts;
        runtimeMesh.triangles = tris;
        runtimeMesh.RecalculateBounds();
        runtimeMesh.normals = new Vector3[vertCount];
        runtimeMesh.uv = new Vector2[vertCount];
        meshFilter.sharedMesh = runtimeMesh;
    }

    void EnsureMeshComponents()
    {
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }
        }

        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }
        }

        if (runtimeMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                return;
            }

            runtimeMaterial = new Material(shader);
            if (runtimeMaterial.HasProperty("_BaseColor"))
            {
                runtimeMaterial.SetColor("_BaseColor", meshColor);
            }
            else if (runtimeMaterial.HasProperty("_Color"))
            {
                runtimeMaterial.SetColor("_Color", meshColor);
            }

            runtimeMaterial.renderQueue = 3000;
            meshRenderer.sharedMaterial = runtimeMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }
        else if (meshRenderer.sharedMaterial != runtimeMaterial)
        {
            meshRenderer.sharedMaterial = runtimeMaterial;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        Vector3 eyePos = GetEyePosition();
        Vector3 forward = GetForwardFlat();
        float half = fovAngle * 0.5f;

        Gizmos.color = gizmoColor;
        Vector3 leftDir = Quaternion.AngleAxis(-half, Vector3.up) * forward;
        Vector3 rightDir = Quaternion.AngleAxis(half, Vector3.up) * forward;
        Gizmos.DrawLine(eyePos, eyePos + leftDir * maxRange);
        Gizmos.DrawLine(eyePos, eyePos + rightDir * maxRange);

        const int segments = 24;
        Vector3 prev = eyePos + leftDir * maxRange;
        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            Quaternion rot = Quaternion.AngleAxis(Mathf.Lerp(-half, half, t), Vector3.up);
            Vector3 cur = eyePos + (rot * forward) * maxRange;
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }

        if (worldHits.Count >= 2)
        {
            Gizmos.color = gizmoHitColor;
            Vector3 last = worldHits[0];
            Gizmos.DrawLine(eyePos, last);
            for (int i = 1; i < worldHits.Count; i++)
            {
                Gizmos.DrawLine(last, worldHits[i]);
                Gizmos.DrawLine(eyePos, worldHits[i]);
                last = worldHits[i];
            }
        }
    }
}
