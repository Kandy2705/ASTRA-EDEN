using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class OverviewPrefabValidation
{
    private const string PrefabPath = "Assets/_Project/Prefab/Screens/Overview.prefab";
    private const string OutputDirectory = "/tmp/astra-overview-validation";

    [MenuItem("Overview Validation/Run")]
    public static void Run()
    {
        Directory.CreateDirectory(OutputDirectory);
        var report = new StringBuilder();
        var failed = false;

        var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefabAsset == null)
        {
            Debug.LogError("Overview validation: prefab could not be loaded.");
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
            return;
        }

        var scene = EditorSceneManager.NewPreviewScene();
        var cameraObject = new GameObject("ValidationCamera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        var camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(12, 10, 9, 255);
        camera.orthographic = true;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;

        var canvasObject = new GameObject("ValidationCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 10f;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(2048f, 1152f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, scene);
        instance.transform.SetParent(canvasObject.transform, false);
        var instanceRect = instance.GetComponent<RectTransform>();
        instanceRect.anchorMin = Vector2.zero;
        instanceRect.anchorMax = Vector2.one;
        instanceRect.offsetMin = Vector2.zero;
        instanceRect.offsetMax = Vector2.zero;

        var transforms = instance.GetComponentsInChildren<Transform>(true);
        var missingScripts = 0;
        foreach (var current in transforms)
            missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(current.gameObject);

        report.AppendLine($"Hierarchy objects: {transforms.Length}");
        report.AppendLine($"Missing scripts: {missingScripts}");
        if (missingScripts != 0)
            failed = true;

        var requiredTexts = new[]
        {
            "Hero", "Overview", "Abilities", "Equipment", "Armament",
            "Ravenous Butcher", "Bjorn", "HP", "Dmg", "Def", "Range", "Target",
            "91485", "1090", "1076", "Close", "Multi",
            "Frontline", "Legendary", "3,904,482", "800884", "Details", "Confirm", "Back"
        };
        var allText = instance.GetComponentsInChildren<TMP_Text>(true);
        foreach (var required in requiredTexts)
        {
            var found = false;
            foreach (var text in allText)
            {
                if (text.text == required)
                {
                    found = true;
                    break;
                }
            }

            report.AppendLine($"Text '{required}': {(found ? "OK" : "MISSING")}");
            if (!found)
                failed = true;
        }

        foreach (var text in allText)
            text.ForceMeshUpdate(true, true);
        Canvas.ForceUpdateCanvases();

        const int width = 2048;
        const int height = 1152;
        var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = renderTexture;
        camera.Render();

        var previous = RenderTexture.active;
        RenderTexture.active = renderTexture;
        var screenshot = new Texture2D(width, height, TextureFormat.RGBA32, false);
        screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        screenshot.Apply();
        File.WriteAllBytes(Path.Combine(OutputDirectory, "overview.png"), screenshot.EncodeToPNG());
        RenderTexture.active = previous;
        camera.targetTexture = null;
        Object.DestroyImmediate(screenshot);
        Object.DestroyImmediate(renderTexture);

        File.WriteAllText(Path.Combine(OutputDirectory, "report.txt"), report.ToString());
        Debug.Log(report.ToString());
        EditorSceneManager.ClosePreviewScene(scene);
        if (Application.isBatchMode)
            EditorApplication.Exit(failed ? 1 : 0);
    }
}
