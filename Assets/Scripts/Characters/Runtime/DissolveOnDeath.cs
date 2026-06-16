using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Khi CharacterHealth chết: đợi delay (cho anim Die chạy) → spawn VFX → swap renderer materials
/// sang dissolveMaterialTemplate → tăng property _Dissolve từ 0→1 → Destroy GameObject.
/// </summary>
[DisallowMultipleComponent]
public class DissolveOnDeath : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterHealth characterHealth;

    [Header("Dissolve Material")]
    [Tooltip("Material dissolve mẫu (vd: Shader Graphs_Dissolve_Dissolve_Metallic.mat). Mỗi renderer sẽ được clone material này để dissolve.")]
    [SerializeField] private Material dissolveMaterialTemplate;
    [Tooltip("Tên property float điều khiển dissolve trong shader (mặc định _Dissolve).")]
    [SerializeField] private string dissolveProperty = "_Dissolve";
    [Tooltip("Tên property texture base trong shader để copy từ material gốc (mặc định _BaseMap cho URP).")]
    [SerializeField] private string baseMapProperty = "_BaseMap";
    [Tooltip("Tên property color base trong shader (mặc định _BaseColor cho URP).")]
    [SerializeField] private string baseColorProperty = "_BaseColor";

    [Header("Timing")]
    [Tooltip("Đợi N giây sau khi enemy chết rồi mới bắt đầu dissolve (cho anim Die chạy xong).")]
    [SerializeField] private float startDelay = 2.0f;
    [Tooltip("Thời lượng dissolve từ 0 → 1 (giây).")]
    [SerializeField] private float dissolveDuration = 1.5f;
    [Tooltip("Đợi thêm N giây sau khi dissolve xong rồi mới Destroy (cho VFX kịp tan).")]
    [SerializeField] private float destroyDelayAfterDissolve = 0.5f;

    [Header("VFX")]
    [Tooltip("Prefab VFX spawn lúc bắt đầu dissolve (tạo dưới chân enemy). Để trống nếu không cần.")]
    [SerializeField] private GameObject dissolveVfxPrefab;
    [SerializeField] private Vector3 vfxLocalOffset = new Vector3(0f, 0.2f, 0f);
    [Tooltip("VFX có tự destroy không. Tắt nếu prefab VFX chưa có auto-destroy.")]
    [SerializeField] private bool vfxAutoDestroy = true;
    [SerializeField] private float vfxLifetime = 3f;

    [Header("Misc")]
    [Tooltip("Disable NavMeshAgent ngay khi bắt đầu dissolve (để enemy không di chuyển trong lúc tan).")]
    [SerializeField] private bool disableNavMeshAgentOnStart = true;
    [Tooltip("Tắt collider trong lúc dissolve. TẮT (mặc định) để enemy không rơi xuống đất khi đang tan.")]
    [SerializeField] private bool disableCollidersOnStart = false;
    [Tooltip("Tắt RagdollOnDeath để dissolve không xung đột với ragdoll.")]
    [SerializeField] private bool disableRagdollOnStart = true;

    private bool dissolveStarted;

    private void Reset()
    {
        characterHealth = GetComponent<CharacterHealth>();
    }

    private void Awake()
    {
        if (characterHealth == null)
        {
            characterHealth = GetComponent<CharacterHealth>();
        }
    }

    private void OnEnable()
    {
        if (characterHealth != null)
        {
            characterHealth.Died -= HandleDied;
            characterHealth.Died += HandleDied;
        }
    }

    private void OnDisable()
    {
        if (characterHealth != null)
        {
            characterHealth.Died -= HandleDied;
        }
    }

    private void HandleDied(CharacterHealth h)
    {
        BeginDissolve();
    }

    public void BeginDissolve()
    {
        if (dissolveStarted) return;
        dissolveStarted = true;
        StartCoroutine(DissolveRoutine());
    }

    private IEnumerator DissolveRoutine()
    {
        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        if (disableRagdollOnStart)
        {
            var ragdoll = GetComponent<RagdollOnDeath>();
            if (ragdoll != null) ragdoll.enabled = false;
        }

        if (disableNavMeshAgentOnStart)
        {
            var nav = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (nav != null) nav.enabled = false;
        }

        if (disableCollidersOnStart)
        {
            DisableColliders();
        }

        SpawnVfx();

        List<Material> dissolveInstances = SwapToDissolveMaterials();

        if (dissolveInstances.Count == 0)
        {
            Debug.LogWarning($"[DissolveOnDeath] {name}: Không tìm thấy Renderer hoặc dissolveMaterialTemplate null. Destroy ngay.", this);
            Destroy(gameObject);
            yield break;
        }

        int dissolveHash = Shader.PropertyToID(dissolveProperty);

        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, dissolveDuration);
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            for (int i = 0; i < dissolveInstances.Count; i++)
            {
                if (dissolveInstances[i] != null)
                {
                    dissolveInstances[i].SetFloat(dissolveHash, t);
                }
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < dissolveInstances.Count; i++)
        {
            if (dissolveInstances[i] != null)
            {
                dissolveInstances[i].SetFloat(dissolveHash, 1f);
            }
        }

        if (destroyDelayAfterDissolve > 0f)
        {
            yield return new WaitForSeconds(destroyDelayAfterDissolve);
        }

        Destroy(gameObject);
    }

    private List<Material> SwapToDissolveMaterials()
    {
        var result = new List<Material>();
        if (dissolveMaterialTemplate == null) return result;

        int baseMapId = Shader.PropertyToID(baseMapProperty);
        int baseColorId = Shader.PropertyToID(baseColorProperty);
        bool templateHasBaseMap = dissolveMaterialTemplate.HasProperty(baseMapId);
        bool templateHasBaseColor = dissolveMaterialTemplate.HasProperty(baseColorId);

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            Material[] originals = r.sharedMaterials;
            if (originals == null || originals.Length == 0) continue;

            Material[] swapped = new Material[originals.Length];
            for (int i = 0; i < originals.Length; i++)
            {
                Material instance = new Material(dissolveMaterialTemplate);

                Material src = originals[i];
                if (src != null)
                {
                    if (templateHasBaseMap && src.HasProperty(baseMapId))
                    {
                        instance.SetTexture(baseMapId, src.GetTexture(baseMapId));
                    }
                    else if (templateHasBaseMap && src.HasProperty("_MainTex"))
                    {
                        instance.SetTexture(baseMapId, src.GetTexture("_MainTex"));
                    }

                    if (templateHasBaseColor && src.HasProperty(baseColorId))
                    {
                        instance.SetColor(baseColorId, src.GetColor(baseColorId));
                    }
                    else if (templateHasBaseColor && src.HasProperty("_Color"))
                    {
                        instance.SetColor(baseColorId, src.GetColor("_Color"));
                    }
                }

                swapped[i] = instance;
                result.Add(instance);
            }
            r.materials = swapped;
        }
        return result;
    }

    private void SpawnVfx()
    {
        if (dissolveVfxPrefab == null) return;
        Vector3 spawnPos = transform.TransformPoint(vfxLocalOffset);
        GameObject vfx = Instantiate(dissolveVfxPrefab, spawnPos, transform.rotation);
        if (vfxAutoDestroy && vfxLifetime > 0f)
        {
            Destroy(vfx, vfxLifetime);
        }
    }

    private void DisableColliders()
    {
        Collider[] cols = GetComponentsInChildren<Collider>(true);
        foreach (var c in cols)
        {
            if (c != null) c.enabled = false;
        }
    }
}
