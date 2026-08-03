using UnityEngine;

/// <summary>
/// FOV + LOS + hearing. Tường / obstacle chặn LOS luôn.
/// Hearing cũng cần clear LOS (không "xuyên tường" nghe player).
/// Proximity sát (&lt; contactRange) = đụng thân, không cần LOS.
/// </summary>
[DisallowMultipleComponent]
public class EnemySensor : MonoBehaviour
{
    static readonly RaycastHit[] LosHits = new RaycastHit[12];

    [Header("Source")]
    [SerializeField] private EnemyData enemyData;

    [Header("Overrides (fallback khi không có EnemyData)")]
    [SerializeField, Min(0f)] private float sightRangeOverride = 14f;
    [SerializeField, Range(10f, 360f)] private float sightAngleOverride = 110f;
    [SerializeField, Min(0f)] private float hearingRangeOverride = 7f;

    [Header("Eye / Target Sensors")]
    [SerializeField] private Transform eyeSensor;
    [SerializeField, Min(0f)] private float eyeHeight = 1.6f;
    [SerializeField, Min(0f)] private float targetChestHeight = 1.0f;
    [SerializeField] private bool flipForward180;

    [Header("LOS")]
    [Tooltip("Layer vật cản. Nên gồm Default / Environment / Terrain. Player có thể nằm trong mask — code tự bỏ qua collider của target.")]
    [SerializeField] private LayerMask obstacleMask = ~0;
    [Tooltip("Khoảng sát (đụng thân) — thấy/cảm nhận không cần FOV/LOS.")]
    [SerializeField, Min(0f)] private float contactRange = 1.35f;
    [Tooltip("Hearing vẫn yêu cầu clear LOS (không nghe xuyên tường).")]
    [SerializeField] private bool hearingRequiresLineOfSight = true;

    [Header("Aillieo UnityLOS2D (optional visual / dual-check)")]
    [Tooltip("Bật = dùng package làm check phụ; LOS tường vẫn luôn qua ray chuẩn bên dưới.")]
    [SerializeField] private bool useAillieoLos2D;
    [SerializeField] private EnemyLOS2DBridge aillieoBridge;
    [SerializeField] private bool aillieoDrawMesh;

    [Header("Multi-ray FOV (custom — optional)")]
    [SerializeField] private EnemyLineOfSight lineOfSight;
    [SerializeField] private bool useMultiRayFov;
    [SerializeField] private bool generateVisionMesh;

    [Header("Performance")]
    [SerializeField, Min(0.05f)] private float senseInterval = 0.12f;

    [Header("Debug / Runtime vision (Game view)")]
    [Tooltip("Vẽ nón FOV + ray LOS lúc Play (LineRenderer, thấy trong Game view).")]
    [SerializeField] private bool showRuntimeVision;
    [SerializeField, Range(8, 64)] private int runtimeVisionRays = 28;
    [SerializeField, Min(0.02f)] private float runtimeVisionHeight = 0.15f;
    [SerializeField] private bool drawGizmos;
    [SerializeField] private Color sightColor = new Color(1f, 0.85f, 0.15f, 0.9f);
    [SerializeField] private Color sightAlertColor = new Color(1f, 0.25f, 0.15f, 0.95f);
    [SerializeField] private Color hearingColor = new Color(0f, 0.75f, 1f, 0.55f);
    [SerializeField] private Color losBlockedColor = new Color(1f, 0.2f, 0.2f, 0.95f);
    [SerializeField] private Color losClearColor = new Color(0.15f, 1f, 0.35f, 0.95f);

    /// <summary>Bật/tắt vision runtime cho MỌI EnemySensor (hotkey / debug).</summary>
    public static bool GlobalShowRuntimeVision;

    public float SightRange => enemyData != null ? enemyData.sightRange : sightRangeOverride;
    public float SightAngle => enemyData != null ? enemyData.sightAngle : sightAngleOverride;
    public float HearingRange => enemyData != null ? enemyData.hearingRange : hearingRangeOverride;
    public float ContactRange => contactRange;

    public bool FlipForward180
    {
        get => flipForward180;
        set
        {
            flipForward180 = value;
            SyncLineOfSightConfig();
        }
    }

    public EnemyLineOfSight LineOfSight => lineOfSight;
    public bool LastLosClear => lastLosClear;
    /// <summary>True nếu frame sense gần nhất đang detect target.</summary>
    public bool IsCurrentlySensing => cachedCanSense;

    Transform cachedTarget;
    float cachedDistance = float.PositiveInfinity;
    bool cachedCanSense;
    float senseTimer;
    bool lastLosClear;
    Vector3 lastEyePos;
    Vector3 lastTargetPos;

    LineRenderer fovLine;
    LineRenderer losLine;
    LineRenderer hearLine;
    Material runtimeLineMat;
    readonly Vector3[] fovPoints = new Vector3[66];
    readonly Vector3[] hearPoints = new Vector3[33];

    public void Configure(EnemyData data)
    {
        enemyData = data;
        InvalidateSenseCache();
        EnsureLosBackends();
        SyncLineOfSightConfig();
    }

    public void SetFlipForward(bool flip)
    {
        flipForward180 = flip;
        if (aillieoBridge != null)
        {
            aillieoBridge.SetFlipForward(flip);
        }

        SyncLineOfSightConfig();
    }

    public void InvalidateSenseCache()
    {
        senseTimer = 0f;
        cachedTarget = null;
    }

    void Awake()
    {
        EnsureLosBackends();
        SyncLineOfSightConfig();
        if (showRuntimeVision && GlobalShowRuntimeVision)
        {
            EnsureRuntimeVisionRenderers();
        }
    }

    void OnDisable()
    {
        SetRuntimeVisionActive(false);
    }

    void OnDestroy()
    {
        if (runtimeLineMat != null)
        {
            if (Application.isPlaying)
            {
                Destroy(runtimeLineMat);
            }
            else
            {
                DestroyImmediate(runtimeLineMat);
            }

            runtimeLineMat = null;
        }
    }

    void LateUpdate()
    {
        UpdateRuntimeVision();
    }

    void OnValidate()
    {
        if (useMultiRayFov && lineOfSight == null)
        {
            lineOfSight = GetComponent<EnemyLineOfSight>();
        }

        if (useAillieoLos2D && aillieoBridge == null)
        {
            aillieoBridge = GetComponent<EnemyLOS2DBridge>();
        }

        SyncLineOfSightConfig();
    }

    void EnsureLosBackends()
    {
        if (useAillieoLos2D)
        {
            if (aillieoBridge == null)
            {
                aillieoBridge = GetComponent<EnemyLOS2DBridge>();
            }

            if (aillieoBridge == null)
            {
                aillieoBridge = gameObject.AddComponent<EnemyLOS2DBridge>();
            }

            aillieoBridge.ConfigureFromSensor(this, flipForward180);
        }

        if (useMultiRayFov)
        {
            if (lineOfSight == null)
            {
                lineOfSight = GetComponent<EnemyLineOfSight>();
            }

            if (lineOfSight == null)
            {
                lineOfSight = gameObject.AddComponent<EnemyLineOfSight>();
            }
        }
    }

    void SyncLineOfSightConfig()
    {
        if (useAillieoLos2D && aillieoBridge != null)
        {
            aillieoBridge.ConfigureFromSensor(this, flipForward180);
        }

        if (lineOfSight == null)
        {
            return;
        }

        lineOfSight.Configure(SightRange, SightAngle, obstacleMask, flipForward180, generateVisionMesh);
        lineOfSight.SetEye(eyeSensor, eyeHeight);
        lineOfSight.GenerateMesh = generateVisionMesh;
        lineOfSight.DrawGizmos = drawGizmos;
    }

    /// <summary>Player có bị phát hiện không? (FOV+LOS, hearing+LOS, hoặc contact).</summary>
    public bool CanSense(Transform target, out float distance)
    {
        if (target == null)
        {
            distance = float.PositiveInfinity;
            return false;
        }

        if (cachedTarget != target)
        {
            InvalidateSenseCache();
            cachedTarget = target;
        }

        senseTimer -= Time.deltaTime;
        if (senseTimer <= 0f)
        {
            senseTimer = senseInterval;
            cachedCanSense = EvaluateSense(target, out cachedDistance);
        }

        distance = cachedDistance;
        return cachedCanSense;
    }

    /// <summary>Chỉ kiểm tra tường (không FOV). Dùng khi đang chase để mất target sau tường.</summary>
    public bool HasLineOfSightTo(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        return HasClearLineOfSight(target, out _, out _);
    }

    bool EvaluateSense(Transform target, out float distance)
    {
        distance = float.PositiveInfinity;
        lastLosClear = false;

        Vector3 flat = target.position - transform.position;
        flat.y = 0f;
        distance = flat.magnitude;

        // 1) Đụng sát — không cần LOS (player dính vào enemy).
        if (distance <= contactRange)
        {
            lastLosClear = true;
            CacheDebugRay(target, clear: true);
            return true;
        }

        // 2) Hearing: trong tầm nghe NHƯNG không xuyên tường (trừ khi tắt cờ).
        if (distance <= HearingRange)
        {
            if (!hearingRequiresLineOfSight)
            {
                lastLosClear = true;
                CacheDebugRay(target, clear: true);
                return true;
            }

            if (HasClearLineOfSight(target, out Vector3 eyeH, out Vector3 aimH))
            {
                lastLosClear = true;
                lastEyePos = eyeH;
                lastTargetPos = aimH;
                return true;
            }

            // Nghe bị tường chặn → không detect bằng hearing; vẫn có thể sight FOV.
        }

        if (distance > SightRange)
        {
            CacheDebugRay(target, clear: false);
            return false;
        }

        // 3) FOV cone
        Vector3 forward = GetForwardFlat();
        Vector3 dirFlat = flat.sqrMagnitude > 0.0001f ? flat.normalized : forward;
        float halfAngle = SightAngle * 0.5f;
        if (Vector3.Angle(forward, dirFlat) > halfAngle)
        {
            CacheDebugRay(target, clear: false);
            return false;
        }

        // 4) LOS tường — luôn bắt buộc (ray chuẩn).
        if (!HasClearLineOfSight(target, out Vector3 eye, out Vector3 aim))
        {
            lastLosClear = false;
            lastEyePos = eye;
            lastTargetPos = aim;
            return false;
        }

        lastLosClear = true;
        lastEyePos = eye;
        lastTargetPos = aim;

        // Optional: Aillieo dual-check (visual package). Nếu package nói không thấy mà ray clear → vẫn tin ray.
        // Nếu package nói thấy nhưng ray blocked — đã return false ở trên.
        EnsureLosBackends();
        return true;
    }

    /// <summary>
    /// Ray từ mắt → ngực target. Hit obstacle (không thuộc target) = bị chặn.
    /// Không hit gì / hit target hierarchy = clear.
    /// </summary>
    bool HasClearLineOfSight(Transform target, out Vector3 eyePos, out Vector3 aimPos)
    {
        eyePos = eyeSensor != null ? eyeSensor.position : transform.position + Vector3.up * eyeHeight;
        aimPos = target.position + Vector3.up * targetChestHeight;

        Vector3 delta = aimPos - eyePos;
        float dist = delta.magnitude;
        if (dist <= 0.01f)
        {
            return true;
        }

        Vector3 dir = delta / dist;
        int mask = obstacleMask.value != 0 ? obstacleMask : Physics.DefaultRaycastLayers;

        int count = Physics.RaycastNonAlloc(
            eyePos,
            dir,
            LosHits,
            dist,
            mask,
            QueryTriggerInteraction.Ignore);

        if (count <= 0)
        {
            // Không hit gì — open air tới target.
            return true;
        }

        // Lấy hit gần nhất không phải chính enemy
        float nearest = float.MaxValue;
        RaycastHit best = default;
        bool any = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit h = LosHits[i];
            if (h.collider == null)
            {
                continue;
            }

            Transform ht = h.transform;
            if (ht == transform || ht.IsChildOf(transform))
            {
                continue; // collider của chính enemy
            }

            if (h.distance < nearest)
            {
                nearest = h.distance;
                best = h;
                any = true;
            }
        }

        if (!any)
        {
            return true;
        }

        // Hit gần nhất thuộc target?
        if (IsPartOfTarget(best.transform, target))
        {
            aimPos = best.point;
            return true;
        }

        // Tường / đồ vật chặn
        aimPos = best.point;
        return false;
    }

    static bool IsPartOfTarget(Transform hit, Transform target)
    {
        if (hit == null || target == null)
        {
            return false;
        }

        if (hit == target || hit.IsChildOf(target) || target.IsChildOf(hit))
        {
            return true;
        }

        // Cùng root player
        if (hit.root == target.root && target.root.CompareTag("Player"))
        {
            return true;
        }

        return false;
    }

    void CacheDebugRay(Transform target, bool clear)
    {
        lastLosClear = clear;
        lastEyePos = eyeSensor != null ? eyeSensor.position : transform.position + Vector3.up * eyeHeight;
        lastTargetPos = target != null ? target.position + Vector3.up * targetChestHeight : lastEyePos;
    }

    Vector3 GetForwardFlat()
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

    Vector3 GetEyeWorld()
    {
        return eyeSensor != null ? eyeSensor.position : transform.position + Vector3.up * eyeHeight;
    }

    // ---------- Runtime vision (Play mode / Game view) ----------

    void EnsureRuntimeVisionRenderers()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (runtimeLineMat == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader != null)
            {
                runtimeLineMat = new Material(shader);
            }
        }

        fovLine = GetOrCreateLine("SensorVision_FOV", 0.06f, 0.02f);
        losLine = GetOrCreateLine("SensorVision_LOS", 0.08f, 0.04f);
        hearLine = GetOrCreateLine("SensorVision_Hear", 0.04f, 0.02f);
    }

    LineRenderer GetOrCreateLine(string childName, float startWidth, float endWidth)
    {
        Transform child = transform.Find(childName);
        GameObject go;
        if (child == null)
        {
            go = new GameObject(childName);
            go.transform.SetParent(transform, false);
        }
        else
        {
            go = child.gameObject;
        }

        LineRenderer lr = go.GetComponent<LineRenderer>();
        if (lr == null)
        {
            lr = go.AddComponent<LineRenderer>();
        }

        lr.sharedMaterial = runtimeLineMat;
        lr.widthMultiplier = 1f;
        lr.startWidth = startWidth;
        lr.endWidth = endWidth;
        lr.useWorldSpace = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.loop = false;
        lr.positionCount = 0;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;
        return lr;
    }

    void SetRuntimeVisionActive(bool active)
    {
        if (fovLine != null)
        {
            fovLine.enabled = active;
        }

        if (losLine != null)
        {
            losLine.enabled = active;
        }

        if (hearLine != null)
        {
            hearLine.enabled = active;
        }
    }

    void UpdateRuntimeVision()
    {
        bool show = Application.isPlaying && showRuntimeVision && GlobalShowRuntimeVision;
        if (!show)
        {
            SetRuntimeVisionActive(false);
            return;
        }

        EnsureRuntimeVisionRenderers();
        SetRuntimeVisionActive(true);

        Color coneColor = cachedCanSense ? sightAlertColor : sightColor;
        Vector3 origin = transform.position + Vector3.up * runtimeVisionHeight;
        Vector3 eye = GetEyeWorld();
        Vector3 forward = GetForwardFlat();
        float half = SightAngle * 0.5f;
        int rays = Mathf.Clamp(runtimeVisionRays, 8, 64);
        int mask = obstacleMask.value != 0 ? obstacleMask : Physics.DefaultRaycastLayers;

        // FOV fan: origin → ray hits (ôm tường) → back to origin style polyline
        // Points: left edge, arc samples, right edge (open polyline)
        int fovCount = rays + 2; // center + samples + close optional
        if (fovCount > fovPoints.Length)
        {
            fovCount = fovPoints.Length;
            rays = fovCount - 2;
        }

        fovPoints[0] = origin;
        for (int i = 0; i < rays; i++)
        {
            float t = rays == 1 ? 0.5f : (float)i / (rays - 1);
            float angle = Mathf.Lerp(-half, half, t);
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            float maxR = SightRange;
            Vector3 end = origin + dir * maxR;

            // Ray from eye height along flat dir — block on walls
            Vector3 rayOrigin = new Vector3(origin.x, eye.y, origin.z);
            if (Physics.Raycast(rayOrigin, dir, out RaycastHit hit, maxR, mask, QueryTriggerInteraction.Ignore))
            {
                if (!(hit.transform == transform || hit.transform.IsChildOf(transform)))
                {
                    end = hit.point;
                    end.y = origin.y;
                }
            }

            fovPoints[i + 1] = end;
        }

        // Close fan back to origin for filled-looking outline
        int last = rays + 1;
        if (last < fovPoints.Length)
        {
            fovPoints[last] = origin;
            fovLine.positionCount = last + 1;
            fovLine.SetPositions(fovPoints);
        }
        else
        {
            fovLine.positionCount = rays + 1;
            fovLine.SetPositions(fovPoints);
        }

        fovLine.startColor = coneColor;
        fovLine.endColor = coneColor;
        if (runtimeLineMat != null && runtimeLineMat.HasProperty("_Color"))
        {
            // per-line vertex colors
        }

        ApplyLineColor(fovLine, coneColor);

        // LOS ray to player (if any)
        Transform player = cachedTarget;
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                player = p.transform;
            }
        }

        if (player != null && HorizontalDist(player.position) <= SightRange * 1.25f)
        {
            Vector3 aim = player.position + Vector3.up * targetChestHeight;
            bool clear = HasClearLineOfSight(player, out Vector3 e, out Vector3 a);
            losLine.positionCount = 2;
            losLine.SetPosition(0, e);
            losLine.SetPosition(1, a);
            ApplyLineColor(losLine, clear ? losClearColor : losBlockedColor);
            losLine.enabled = true;
        }
        else
        {
            losLine.positionCount = 0;
            losLine.enabled = false;
        }

        // Hearing ring (flat circle)
        int hSeg = 24;
        for (int i = 0; i <= hSeg; i++)
        {
            float ang = (i / (float)hSeg) * Mathf.PI * 2f;
            hearPoints[i] = origin + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * HearingRange;
        }

        hearLine.positionCount = hSeg + 1;
        hearLine.SetPositions(hearPoints);
        hearLine.loop = true;
        ApplyLineColor(hearLine, hearingColor);
    }

    static void ApplyLineColor(LineRenderer lr, Color c)
    {
        if (lr == null)
        {
            return;
        }

        lr.startColor = c;
        lr.endColor = c;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
            new[] { new GradientAlphaKey(c.a, 0f), new GradientAlphaKey(c.a, 1f) });
        lr.colorGradient = g;
    }

    float HorizontalDist(Vector3 world)
    {
        Vector3 d = world - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    void OnDrawGizmos()
    {
        // Luôn vẽ khi Gizmos bật (Scene + Game view) — không cần select enemy.
        if (!drawGizmos)
        {
            return;
        }

        DrawVisionGizmos();
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        DrawVisionGizmos();
    }

    void DrawVisionGizmos()
    {
        Vector3 origin = transform.position;
        Vector3 forward = GetForwardFlat();
        float half = SightAngle * 0.5f;
        Color cone = Application.isPlaying && cachedCanSense ? sightAlertColor : sightColor;

        Gizmos.color = cone;
        Gizmos.DrawWireSphere(origin, SightRange);

        Quaternion left = Quaternion.Euler(0f, -half, 0f);
        Quaternion right = Quaternion.Euler(0f, half, 0f);
        Vector3 leftDir = left * forward * SightRange;
        Vector3 rightDir = right * forward * SightRange;
        Gizmos.DrawLine(origin, origin + leftDir);
        Gizmos.DrawLine(origin, origin + rightDir);

        const int arcSegments = 24;
        Vector3 prev = origin + leftDir;
        for (int i = 1; i <= arcSegments; i++)
        {
            float t = (float)i / arcSegments;
            Quaternion rot = Quaternion.AngleAxis(Mathf.Lerp(-half, half, t), Vector3.up);
            Vector3 cur = origin + (rot * forward) * SightRange;
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }

        Gizmos.color = hearingColor;
        Gizmos.DrawWireSphere(origin, HearingRange);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(origin, contactRange);

        if (Application.isPlaying && lastEyePos.sqrMagnitude > 0.001f)
        {
            Gizmos.color = lastLosClear ? losClearColor : losBlockedColor;
            Gizmos.DrawLine(lastEyePos, lastTargetPos);
            Gizmos.DrawSphere(lastTargetPos, 0.08f);
        }
    }
}
