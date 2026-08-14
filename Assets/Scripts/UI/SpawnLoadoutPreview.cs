using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SpawnLoadoutPreview : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private RawImage output;
    [SerializeField] private Transform previewRoot;
    [SerializeField] private Camera previewCamera;
    [SerializeField] private int textureSize = 768;
    [SerializeField] private Vector3 heroLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 heroLocalEuler = new Vector3(0f, 180f, 0f);
    [Header("Framing")]
    [SerializeField, Min(0f)] private float previewCenterHeight = 1.15f;
    [SerializeField, Range(0.1f, 0.95f)] private float viewportFill = 0.78f;
    [Header("Rotation")]
    [SerializeField] private bool autoRotate = true;
    [SerializeField] private float autoRotateDegreesPerSecond = 12f;
    [SerializeField] private float dragDegreesPerPixel = 0.35f;

    private RenderTexture renderTexture;
    private GameObject previewHero;
    private bool isDragging;

    private void OnEnable()
    {
        EnsureRenderTexture();
    }

    private void OnRectTransformDimensionsChange()
    {
        // The preview panels are not square. Keeping a square RenderTexture and
        // stretching it through RawImage distorts the character vertically.
        // Rebuild only when the actual UI aspect ratio changes.
        if (isActiveAndEnabled) EnsureRenderTexture();
    }

    private void OnDestroy()
    {
        ReleaseRenderTexture();
    }

    private void LateUpdate()
    {
        if (!autoRotate || isDragging || previewHero == null) return;
        previewHero.transform.Rotate(0f, autoRotateDegreesPerSecond * Time.unscaledDeltaTime, 0f, Space.Self);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) isDragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || previewHero == null) return;
        previewHero.transform.Rotate(0f, -eventData.delta.x * dragDegreesPerPixel, 0f, Space.Self);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) isDragging = false;
    }

    public void Show(CharacterData hero, WeaponData weapon)
    {
        ClearPreview();
        if (hero == null || hero.GameplayPrefab == null || previewRoot == null) return;

        bool wasActive = previewRoot.gameObject.activeSelf;
        previewRoot.gameObject.SetActive(false);
        previewHero = Instantiate(hero.GameplayPrefab, previewRoot, false);
        previewHero.name = $"Preview_{hero.HeroId}";
        previewHero.tag = "Untagged";
        previewHero.transform.localPosition = heroLocalPosition;
        previewHero.transform.localRotation = Quaternion.Euler(heroLocalEuler);

        previewHero.SetActive(false);
        ConfigurePreviewWeapon(previewHero, weapon);
        // The gameplay prefab contains input, health, joints and DrakkarTrail. Merely
        // disabling them is too late: Awake runs as soon as the inactive preview
        // hierarchy is enabled. Remove the runtime-only components while the clone is
        // still inactive so this object is a visual-only preview before any Awake runs.
        StripGameplayImmediately(previewHero);
        SetLayerRecursive(previewHero, previewRoot.gameObject.layer);
        FramePreview(previewHero);
        previewHero.SetActive(true);
        previewRoot.gameObject.SetActive(wasActive || gameObject.activeInHierarchy);
    }

    public void ShowWeapon(WeaponData weapon)
    {
        ClearPreview();
        if (weapon == null || weapon.prefab == null || previewRoot == null) return;

        bool wasActive = previewRoot.gameObject.activeSelf;
        previewRoot.gameObject.SetActive(false);
        previewHero = Instantiate(weapon.prefab, previewRoot, false);
        previewHero.name = $"Preview_{weapon.weaponId}";
        previewHero.tag = "Untagged";
        previewHero.transform.localPosition = heroLocalPosition;
        previewHero.transform.localRotation = Quaternion.Euler(heroLocalEuler);
        previewHero.transform.localScale = weapon.localScale == Vector3.zero ? Vector3.one : weapon.localScale;
        previewHero.SetActive(false);
        StripGameplayImmediately(previewHero);
        SetLayerRecursive(previewHero, previewRoot.gameObject.layer);
        FramePreview(previewHero);
        previewHero.SetActive(true);
        previewRoot.gameObject.SetActive(wasActive || gameObject.activeInHierarchy);
    }

    private void FramePreview(GameObject hero)
    {
        Renderer[] renderers = hero.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0 || previewCamera == null) return;

        bool hasBounds = false;
        Bounds bounds = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is TrailRenderer || renderer is ParticleSystemRenderer) continue;
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        if (!hasBounds) return;

        Vector3 desiredCenter = previewRoot.TransformPoint(new Vector3(0f, previewCenterHeight, 0f));
        hero.transform.position += desiredCenter - bounds.center;

        float halfHeight = Mathf.Max(0.1f, bounds.extents.y);
        float halfWidth = Mathf.Max(0.1f, bounds.extents.x);
        float verticalFov = previewCamera.fieldOfView * Mathf.Deg2Rad;
        float horizontalFov = 2f * Mathf.Atan(Mathf.Tan(verticalFov * 0.5f) * previewCamera.aspect);
        float verticalDistance = halfHeight / (Mathf.Tan(verticalFov * 0.5f) * viewportFill);
        float horizontalDistance = halfWidth / (Mathf.Tan(horizontalFov * 0.5f) * viewportFill);
        float distance = Mathf.Max(verticalDistance, horizontalDistance, 1f);

        previewCamera.transform.localPosition = new Vector3(0f, previewCenterHeight, -distance);
        previewCamera.transform.localRotation = Quaternion.identity;
    }

    private void EnsureRenderTexture()
    {
        int width = textureSize;
        int height = textureSize;

        if (output != null)
        {
            Rect rect = output.rectTransform.rect;
            if (rect.width > 1f && rect.height > 1f)
            {
                float aspect = rect.width / rect.height;
                if (aspect >= 1f)
                    height = Mathf.Max(64, Mathf.RoundToInt(textureSize / aspect));
                else
                    width = Mathf.Max(64, Mathf.RoundToInt(textureSize * aspect));
            }
        }

        if (renderTexture == null || renderTexture.width != width || renderTexture.height != height)
        {
            ReleaseRenderTexture();
            renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = $"SpawnLoadoutPreviewRT_{width}x{height}",
                antiAliasing = 4
            };
            renderTexture.Create();

            if (previewCamera != null) previewCamera.targetTexture = renderTexture;
            if (output != null) output.texture = renderTexture;

            // Camera horizontal FOV changes with the RenderTexture aspect, so an
            // already visible model must be framed again after a resolution change.
            if (previewHero != null) FramePreview(previewHero);
            return;
        }

        if (previewCamera != null) previewCamera.targetTexture = renderTexture;
        if (output != null) output.texture = renderTexture;
    }

    private void ReleaseRenderTexture()
    {
        if (renderTexture == null) return;

        if (previewCamera != null && previewCamera.targetTexture == renderTexture)
            previewCamera.targetTexture = null;
        if (output != null && output.texture == renderTexture)
            output.texture = null;

        renderTexture.Release();
        Destroy(renderTexture);
        renderTexture = null;
    }

    private void ClearPreview()
    {
        if (previewHero != null)
        {
            previewHero.SetActive(false);
            Destroy(previewHero);
        }
        previewHero = null;
    }

    private static void StripGameplayImmediately(GameObject root)
    {
        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);

        // Remove RequireComponent owners before the components they require. Unity
        // otherwise refuses to remove PlayerInputReader, CharacterHealth, etc.
        DestroyBehavioursWithPriority(behaviours, true);
        DestroyBehavioursWithPriority(behaviours, false);

        Joint[] joints = root.GetComponentsInChildren<Joint>(true);
        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i] != null) DestroyImmediate(joints[i]);
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null) DestroyImmediate(colliders[i]);
        }

        Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] != null) DestroyImmediate(bodies[i]);
        }

        AudioSource[] audio = root.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < audio.Length; i++)
        {
            if (audio[i] != null) DestroyImmediate(audio[i]);
        }

        Camera[] cameras = root.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null) DestroyImmediate(cameras[i]);
        }

        TrailRenderer[] trails = root.GetComponentsInChildren<TrailRenderer>(true);
        for (int i = 0; i < trails.Length; i++)
        {
            if (trails[i] != null) trails[i].enabled = false;
        }
    }

    private static void DestroyBehavioursWithPriority(MonoBehaviour[] behaviours, bool dependencyOwnerPass)
    {
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null) continue;

            bool ownsDependencies = IsDependencyOwner(behaviour.GetType().Name);
            if (ownsDependencies != dependencyOwnerPass) continue;

            behaviour.enabled = false;
            DestroyImmediate(behaviour);
        }
    }

    private static bool IsDependencyOwner(string typeName)
    {
        switch (typeName)
        {
            case "PlayerController":
            case "PlayerCombatController":
            case "PlayerLoadoutRuntime":
            case "LOSTarget":
            case "PlayerLOSTarget":
                return true;
            default:
                return false;
        }
    }

    private static void ConfigurePreviewWeapon(GameObject hero, WeaponData weapon)
    {
        if (weapon == null || weapon.useBuiltInVisual || weapon.prefab == null) return;
        Transform socket = FindSocket(hero.transform, weapon.socket);
        if (socket == null) return;
        GameObject instance = Instantiate(weapon.prefab, socket, false);
        instance.transform.localPosition = weapon.localPosition;
        instance.transform.localRotation = Quaternion.Euler(weapon.localEulerAngles);
        instance.transform.localScale = weapon.localScale == Vector3.zero ? Vector3.one : weapon.localScale;
    }

    private static Transform FindSocket(Transform root, WeaponSocket socket)
    {
        string boneName = socket == WeaponSocket.LeftHand ? "J_Bip_L_Hand" : "J_Bip_R_Hand";
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++) if (all[i].name == boneName) return all[i];
        return root;
    }

    private static void SetLayerRecursive(GameObject root, int layer)
    {
        root.layer = layer;
        for (int i = 0; i < root.transform.childCount; i++)
            SetLayerRecursive(root.transform.GetChild(i).gameObject, layer);
    }
}
