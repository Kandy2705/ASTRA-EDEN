using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class SeekerMaterialConverter
{
    private const string SourcePrefabPath = "Assets/Prefabs/Vroids/Seeker Prototype/Seeker Prototype Nu.prefab";
    private const string GeneratedMaterialFolder = "Assets/_Project/Generated/Materials/SeekerPrototype_URPLit";
    private const string GeneratedPrefabFolder = "Assets/_Project/Generated/Prefabs";
    private const string GeneratedPrefabPath = GeneratedPrefabFolder + "/Seeker Prototype Nu URPLit.prefab";

    [MenuItem("ASTRA EDEN/Characters/Create Seeker URP Lit Copy")]
    public static void CreateSeekerUrpLitCopy()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath) == null)
        {
            Debug.LogError($"Could not find Seeker prefab at: {SourcePrefabPath}");
            return;
        }

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null)
        {
            Debug.LogError("Could not find shader: Universal Render Pipeline/Lit");
            return;
        }

        EnsureFolder(GeneratedMaterialFolder);
        EnsureFolder(GeneratedPrefabFolder);

        Dictionary<Material, Material> convertedMaterials = new Dictionary<Material, Material>();
        GameObject prefabInstance = PrefabUtility.LoadPrefabContents(SourcePrefabPath);

        try
        {
            Renderer[] renderers = prefabInstance.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                bool changedAny = false;

                for (int i = 0; i < materials.Length; i++)
                {
                    Material sourceMaterial = materials[i];
                    if (sourceMaterial == null)
                    {
                        continue;
                    }

                    if (!convertedMaterials.TryGetValue(sourceMaterial, out Material convertedMaterial))
                    {
                        convertedMaterial = CreateOrUpdateUrpLitMaterial(sourceMaterial, litShader);
                        convertedMaterials.Add(sourceMaterial, convertedMaterial);
                    }

                    materials[i] = convertedMaterial;
                    changedAny = true;
                }

                if (changedAny)
                {
                    renderer.sharedMaterials = materials;
                }
            }

            PrefabUtility.SaveAsPrefabAsset(prefabInstance, GeneratedPrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Created URP Lit Seeker copy: {GeneratedPrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabInstance);
        }
    }

    private static Material CreateOrUpdateUrpLitMaterial(Material sourceMaterial, Shader litShader)
    {
        string materialPath = $"{GeneratedMaterialFolder}/{sourceMaterial.name}_URPLit.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

        if (material == null)
        {
            material = new Material(litShader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else
        {
            material.shader = litShader;
        }

        CopyTexture(sourceMaterial, material, "_MainTex", "_BaseMap");
        CopyTexture(sourceMaterial, material, "_BumpMap", "_BumpMap");
        CopyTexture(sourceMaterial, material, "_EmissionMap", "_EmissionMap");

        CopyColor(sourceMaterial, material, "_Color", "_BaseColor", Color.white);
        CopyColor(sourceMaterial, material, "_EmissionColor", "_EmissionColor", Color.black);

        if (sourceMaterial.HasProperty("_Cutoff") && material.HasProperty("_Cutoff"))
        {
            material.SetFloat("_Cutoff", sourceMaterial.GetFloat("_Cutoff"));
        }

        bool alphaClip = sourceMaterial.HasProperty("_BlendMode") && Mathf.Approximately(sourceMaterial.GetFloat("_BlendMode"), 1f);
        ConfigureUrpLitSurface(material, alphaClip);

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureUrpLitSurface(Material material, bool alphaClip)
    {
        SetFloatIfExists(material, "_Surface", 0f);
        SetFloatIfExists(material, "_Blend", 0f);
        SetFloatIfExists(material, "_SrcBlend", (float)BlendMode.One);
        SetFloatIfExists(material, "_DstBlend", (float)BlendMode.Zero);
        SetFloatIfExists(material, "_ZWrite", 1f);
        SetFloatIfExists(material, "_ReceiveShadows", 1f);
        SetFloatIfExists(material, "_AlphaClip", alphaClip ? 1f : 0f);

        if (alphaClip)
        {
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.AlphaTest;
        }
        else
        {
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = -1;
        }

        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    }

    private static void CopyTexture(Material source, Material target, string sourceProperty, string targetProperty)
    {
        if (!source.HasProperty(sourceProperty) || !target.HasProperty(targetProperty))
        {
            return;
        }

        target.SetTexture(targetProperty, source.GetTexture(sourceProperty));
        target.SetTextureScale(targetProperty, source.GetTextureScale(sourceProperty));
        target.SetTextureOffset(targetProperty, source.GetTextureOffset(sourceProperty));
    }

    private static void CopyColor(Material source, Material target, string sourceProperty, string targetProperty, Color fallback)
    {
        if (!target.HasProperty(targetProperty))
        {
            return;
        }

        Color color = source.HasProperty(sourceProperty) ? source.GetColor(sourceProperty) : fallback;
        target.SetColor(targetProperty, color);
    }

    private static void SetFloatIfExists(Material material, string property, float value)
    {
        if (material.HasProperty(property))
        {
            material.SetFloat(property, value);
        }
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string[] parts = folderPath.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
