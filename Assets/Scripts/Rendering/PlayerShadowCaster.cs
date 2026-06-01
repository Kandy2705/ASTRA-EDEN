using UnityEngine;
using UnityEngine.Rendering;

public class PlayerShadowCaster : MonoBehaviour
{
    [SerializeField] private bool includeInactiveRenderers;
    [SerializeField] private bool createOnStart = true;

    private Material shadowMaterial;
    private bool created;

    private void Start()
    {
        if (createOnStart)
        {
            CreateShadowCasters();
        }
    }

    [ContextMenu("Create Shadow Casters")]
    public void CreateShadowCasters()
    {
        if (created)
        {
            return;
        }

        SkinnedMeshRenderer[] sourceRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(includeInactiveRenderers);
        if (sourceRenderers.Length == 0)
        {
            return;
        }

        shadowMaterial = CreateShadowMaterial();

        foreach (SkinnedMeshRenderer source in sourceRenderers)
        {
            if (source == null || source.sharedMesh == null || source.shadowCastingMode == ShadowCastingMode.ShadowsOnly)
            {
                continue;
            }

            CreateShadowCasterFor(source);
        }

        created = true;
    }

    private void CreateShadowCasterFor(SkinnedMeshRenderer source)
    {
        GameObject shadowObject = new GameObject(source.name + " ShadowCaster");
        shadowObject.transform.SetParent(source.transform, false);

        SkinnedMeshRenderer shadowRenderer = shadowObject.AddComponent<SkinnedMeshRenderer>();
        shadowRenderer.sharedMesh = source.sharedMesh;
        shadowRenderer.rootBone = source.rootBone;
        shadowRenderer.bones = source.bones;
        shadowRenderer.localBounds = source.localBounds;
        shadowRenderer.quality = source.quality;
        shadowRenderer.updateWhenOffscreen = source.updateWhenOffscreen;
        shadowRenderer.skinnedMotionVectors = false;

        Material[] materials = new Material[Mathf.Max(1, source.sharedMaterials.Length)];
        for (int i = 0; i < materials.Length; i++)
        {
            materials[i] = shadowMaterial;
        }

        shadowRenderer.sharedMaterials = materials;
        shadowRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        shadowRenderer.receiveShadows = false;
        shadowRenderer.lightProbeUsage = LightProbeUsage.Off;
        shadowRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private static Material CreateShadowMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.name = "Runtime Character Shadow Caster";
        material.color = Color.white;

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 0f);
        }

        if (material.HasProperty("_AlphaClip"))
        {
            material.SetFloat("_AlphaClip", 0f);
        }

        return material;
    }

    private void OnDestroy()
    {
        if (shadowMaterial != null)
        {
            Destroy(shadowMaterial);
        }
    }
}
