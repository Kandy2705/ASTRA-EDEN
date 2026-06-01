using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class EnemyHUDBuilder
{
    private const string RankIconPath = "Assets/Textures/UI/UI_Icon_Rank_V.png";
    private const string HealthFillPath = "Assets/Textures/UI/UI_HealthBar_Fill_Red.png";
    private const string HealthFramePath = "Assets/Textures/UI/UI_HealthBar_Frame.png";
    private const string ReticlePath = "Assets/Textures/UI/UI_Reticle_RedCircle.png";

    [MenuItem("ASTRA EDEN/Enemies/Setup Enemy HUD On Selected")]
    public static void SetupSelected()
    {
        GameObject enemy = Selection.activeGameObject;
        if (enemy == null)
        {
            Debug.LogError("Select the enemy root object in the Hierarchy first.");
            return;
        }

        Undo.SetCurrentGroupName("Setup Enemy HUD");
        int undoGroup = Undo.GetCurrentGroup();

        CharacterHealth characterHealth = enemy.GetComponent<CharacterHealth>();
        if (characterHealth == null)
        {
            characterHealth = Undo.AddComponent<CharacterHealth>(enemy);
        }

        EnemyHUDRange hudRange = enemy.GetComponent<EnemyHUDRange>();
        if (hudRange == null)
        {
            hudRange = Undo.AddComponent<EnemyHUDRange>(enemy);
        }

        Canvas canvas = GetOrCreateCanvas(enemy.transform);
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        ConfigureCanvas(canvas, canvasRect);

        Image reticle = GetOrCreateImage(canvasRect, "TargetReticle", LoadSprite(ReticlePath));
        ConfigureRect(reticle.rectTransform, Vector2.zero, new Vector2(72f, 72f));
        reticle.transform.SetAsFirstSibling();

        RectTransform healthBar = GetOrCreateRect(canvasRect, "HealthBar");
        ConfigureRect(healthBar, new Vector2(0f, -42f), new Vector2(132f, 24f));

        Image healthFill = GetOrCreateImage(healthBar, "HealthBar_Fill", LoadSprite(HealthFillPath));
        ConfigureRect(healthFill.rectTransform, Vector2.zero, new Vector2(112f, 10f));
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Horizontal;
        healthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        healthFill.fillAmount = 1f;

        Image healthFrame = GetOrCreateImage(healthBar, "HealthBar_Frame", LoadSprite(HealthFramePath));
        ConfigureRect(healthFrame.rectTransform, Vector2.zero, new Vector2(132f, 24f));
        healthFrame.raycastTarget = false;

        Image rankIcon = GetOrCreateImage(canvasRect, "RankIcon_V", LoadSprite(RankIconPath));
        ConfigureRect(rankIcon.rectTransform, new Vector2(-78f, -42f), new Vector2(28f, 28f));

        ApplyHUDRangeReferences(hudRange, characterHealth, canvas.gameObject, healthFill, reticle.gameObject);

        canvas.gameObject.SetActive(false);
        Undo.CollapseUndoOperations(undoGroup);
        EditorUtility.SetDirty(enemy);
        EditorUtility.SetDirty(canvas.gameObject);

        Debug.Log($"Enemy HUD setup complete on {enemy.name}. Drag the Canvas_EnemyHUD upward/downward if the height needs tuning.");
    }

    private static Canvas GetOrCreateCanvas(Transform enemy)
    {
        Transform existing = enemy.Find("Canvas_EnemyHUD");
        if (existing != null && existing.TryGetComponent(out Canvas existingCanvas))
        {
            return existingCanvas;
        }

        GameObject canvasObject = new GameObject("Canvas_EnemyHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasObject, "Create Enemy HUD Canvas");
        canvasObject.transform.SetParent(enemy, false);
        return canvasObject.GetComponent<Canvas>();
    }

    private static void ConfigureCanvas(Canvas canvas, RectTransform rectTransform)
    {
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 20;

        rectTransform.localPosition = new Vector3(0f, 1.6f, 0f);
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.localScale = Vector3.one * 0.01f;
        rectTransform.sizeDelta = new Vector2(180f, 120f);

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.dynamicPixelsPerUnit = 10f;
            scaler.referencePixelsPerUnit = 100f;
        }

        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
        {
            raycaster.enabled = false;
        }
    }

    private static RectTransform GetOrCreateRect(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null && existing.TryGetComponent(out RectTransform existingRect))
        {
            return existingRect;
        }

        GameObject rectObject = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(rectObject, $"Create {name}");
        rectObject.transform.SetParent(parent, false);
        return rectObject.GetComponent<RectTransform>();
    }

    private static Image GetOrCreateImage(Transform parent, string name, Sprite sprite)
    {
        RectTransform rectTransform = GetOrCreateRect(parent, name);
        Image image = rectTransform.GetComponent<Image>();
        if (image == null)
        {
            image = Undo.AddComponent<Image>(rectTransform.gameObject);
        }

        image.sprite = sprite;
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    private static void ConfigureRect(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 size)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.localScale = Vector3.one;
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            Debug.LogWarning($"Enemy HUD sprite not found or not imported as Sprite: {path}");
        }

        return sprite;
    }

    private static void ApplyHUDRangeReferences(EnemyHUDRange hudRange, CharacterHealth characterHealth, GameObject enemyHUD, Image healthFill, GameObject targetReticle)
    {
        SerializedObject serializedObject = new SerializedObject(hudRange);
        serializedObject.FindProperty("characterHealth").objectReferenceValue = characterHealth;
        serializedObject.FindProperty("enemyHUD").objectReferenceValue = enemyHUD;
        serializedObject.FindProperty("healthFill").objectReferenceValue = healthFill;
        serializedObject.FindProperty("targetReticle").objectReferenceValue = targetReticle;
        serializedObject.FindProperty("showDistance").floatValue = 5f;
        serializedObject.FindProperty("targetDistance").floatValue = 3f;
        serializedObject.FindProperty("faceCamera").boolValue = true;
        serializedObject.FindProperty("copyCameraRotation").boolValue = true;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(hudRange);
    }
}
