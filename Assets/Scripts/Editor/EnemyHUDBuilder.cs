#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tạo Canvas_EnemyHUD (world-space HP bar) + EnemyHUDRange trên enemy root,
/// theo template giống prefab Enemy.
/// </summary>
public static class EnemyHUDBuilder
{
    const string RankIconPath = "Assets/Textures/UI/UI_Icon_Rank_V.png";
    const string HealthFillPath = "Assets/Textures/UI/UI_HealthBar_Fill_Red.png";
    const string HealthFramePath = "Assets/Textures/UI/UI_HealthBar_Frame.png";
    const string ReticlePath = "Assets/Textures/UI/UI_Reticle_RedCircle.png";
    const string CanvasName = "Canvas_EnemyHUD";

    static readonly string[] EnemyPrefabPaths =
    {
        "Assets/_Project/Prefab/Enemy.prefab",
        "Assets/_Project/Prefab/Enemy_FangRaptor.prefab",
        "Assets/_Project/Prefab/Enemy_WildClawRaptor.prefab",
        "Assets/_Project/Prefab/Enemy_MiniBoss_Velociraptor.prefab",
        "Assets/_Project/Prefab/Enemy_Boss_BeachTyran.prefab",
    };

    [MenuItem("ASTRA EDEN/Enemies/Setup Enemy HUD On Selected")]
    public static void SetupSelected()
    {
        GameObject enemy = Selection.activeGameObject;
        if (enemy == null)
        {
            Debug.LogError("[EnemyHUD] Select the enemy root object in the Hierarchy first.");
            return;
        }

        // Prefer prefab root if user selected a child.
        GameObject root = PrefabUtility.GetNearestPrefabInstanceRoot(enemy);
        if (root == null)
        {
            root = enemy;
        }

        Undo.SetCurrentGroupName("Setup Enemy HUD");
        int undoGroup = Undo.GetCurrentGroup();

        SetupOnEnemyRoot(root, recordUndo: true, heightOverride: null);

        Undo.CollapseUndoOperations(undoGroup);
        EditorUtility.SetDirty(root);
        Debug.Log($"[EnemyHUD] Setup complete on '{root.name}'. Adjust Canvas_EnemyHUD Y if needed.");
    }

    [MenuItem("ASTRA EDEN/Enemies/Setup Enemy HUD On ALL Prefabs")]
    public static void SetupAllEnemyPrefabs()
    {
        int ok = 0;
        foreach (string path in EnemyPrefabPaths)
        {
            if (SetupOnPrefabAsset(path))
            {
                ok++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[EnemyHUD] Wired Canvas_EnemyHUD on {ok}/{EnemyPrefabPaths.Length} enemy prefabs.");
    }

    /// <summary>Batchmode: -executeMethod EnemyHUDBuilder.SetupAllBatch</summary>
    public static void SetupAllBatch()
    {
        SetupAllEnemyPrefabs();
        EditorApplication.Exit(0);
    }

    /// <summary>Gọi từ builder khác (Raptor / Boss / Demo) khi đang edit prefab contents.</summary>
    public static void EnsureHudOnRoot(GameObject enemyRoot, float? canvasLocalY = null, float? canvasScale = null, float? showDistance = null)
    {
        if (enemyRoot == null)
        {
            return;
        }

        SetupOnEnemyRoot(enemyRoot, recordUndo: false, heightOverride: canvasLocalY, scaleOverride: canvasScale, showDistanceOverride: showDistance);
    }

    public static bool SetupOnPrefabAsset(string prefabPath)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (asset == null)
        {
            Debug.LogWarning($"[EnemyHUD] Prefab not found: {prefabPath}");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            float? y = null;
            float? scale = null;
            float? showDist = null;

            // Boss / larger models: HUD cao hơn + scale nhỏ hơn một chút (root scale lớn).
            string name = root.name.ToLowerInvariant();
            if (name.Contains("boss") || name.Contains("tyran"))
            {
                y = 0.22f;
                scale = 0.004f;
                showDist = 18f;
            }
            else if (name.Contains("miniboss") || name.Contains("velociraptor"))
            {
                y = 1.9f;
                scale = 0.01f;
                showDist = 8f;
            }
            else if (name.Contains("raptor") || name.Contains("fang") || name.Contains("wildclaw"))
            {
                y = 1.7f;
                scale = 0.01f;
                showDist = 6f;
            }
            else
            {
                // Default Enemy / Compy-like
                y = 1.6f;
                scale = 0.01f;
                showDist = 5f;
            }

            // Nếu prefab đã có HUD với scale/pos custom (Enemy.prefab), giữ Y/scale hiện tại.
            Transform existingHud = FindHudCanvas(root.transform);
            if (existingHud != null)
            {
                var rt = existingHud as RectTransform ?? existingHud.GetComponent<RectTransform>();
                if (rt != null)
                {
                    // Rename legacy "Canvas_EnemyHUD (1)"
                    if (existingHud.name != CanvasName)
                    {
                        existingHud.name = CanvasName;
                    }

                    y = rt.localPosition.y;
                    float s = rt.localScale.x;
                    if (s > 0.0001f)
                    {
                        scale = s;
                    }
                }
            }

            SetupOnEnemyRoot(root, recordUndo: false, heightOverride: y, scaleOverride: scale, showDistanceOverride: showDist);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log($"[EnemyHUD] OK → {prefabPath}");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void SetupOnEnemyRoot(
        GameObject enemy,
        bool recordUndo,
        float? heightOverride,
        float? scaleOverride = null,
        float? showDistanceOverride = null)
    {
        CharacterHealth characterHealth = enemy.GetComponent<CharacterHealth>();
        if (characterHealth == null)
        {
            characterHealth = recordUndo
                ? Undo.AddComponent<CharacterHealth>(enemy)
                : enemy.AddComponent<CharacterHealth>();
        }

        EnemyHUDRange hudRange = enemy.GetComponent<EnemyHUDRange>();
        if (hudRange == null)
        {
            hudRange = recordUndo
                ? Undo.AddComponent<EnemyHUDRange>(enemy)
                : enemy.AddComponent<EnemyHUDRange>();
        }

        // Rename legacy duplicate name from older edits.
        Transform legacy = enemy.transform.Find("Canvas_EnemyHUD (1)");
        if (legacy != null)
        {
            legacy.name = CanvasName;
        }

        Canvas canvas = GetOrCreateCanvas(enemy.transform, recordUndo);
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        ConfigureCanvas(canvas, canvasRect, heightOverride ?? 1.6f, scaleOverride ?? 0.01f);

        Image reticle = GetOrCreateImage(canvasRect, "TargetReticle", LoadSprite(ReticlePath), recordUndo);
        ConfigureRect(reticle.rectTransform, Vector2.zero, new Vector2(72f, 72f));
        reticle.transform.SetAsFirstSibling();

        RectTransform healthBar = GetOrCreateRect(canvasRect, "HealthBar", recordUndo);
        ConfigureRect(healthBar, new Vector2(0f, -42f), new Vector2(132f, 24f));

        Image healthFill = GetOrCreateImage(healthBar, "HealthBar_Fill", LoadSprite(HealthFillPath), recordUndo);
        ConfigureRect(healthFill.rectTransform, Vector2.zero, new Vector2(112f, 10f));
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Horizontal;
        healthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        healthFill.fillAmount = 1f;

        Image healthFrame = GetOrCreateImage(healthBar, "HealthBar_Frame", LoadSprite(HealthFramePath), recordUndo);
        ConfigureRect(healthFrame.rectTransform, Vector2.zero, new Vector2(132f, 24f));
        healthFrame.raycastTarget = false;

        Image rankIcon = GetOrCreateImage(canvasRect, "RankIcon_V", LoadSprite(RankIconPath), recordUndo);
        ConfigureRect(rankIcon.rectTransform, new Vector2(-78f, -42f), new Vector2(28f, 28f));

        float showDist = showDistanceOverride ?? 5f;
        ApplyHUDRangeReferences(hudRange, characterHealth, canvas.gameObject, healthFill, showDist);

        // Runtime: EnemyHUDRange tự bật khi player gần. Prefab để inactive mặc định.
        canvas.gameObject.SetActive(false);

        if (recordUndo)
        {
            EditorUtility.SetDirty(enemy);
            EditorUtility.SetDirty(canvas.gameObject);
            EditorUtility.SetDirty(hudRange);
        }
    }

    static Transform FindHudCanvas(Transform enemy)
    {
        Transform t = enemy.Find(CanvasName);
        if (t != null)
        {
            return t;
        }

        t = enemy.Find("Canvas_EnemyHUD (1)");
        return t;
    }

    static Canvas GetOrCreateCanvas(Transform enemy, bool recordUndo)
    {
        Transform existing = FindHudCanvas(enemy);
        if (existing != null)
        {
            if (existing.name != CanvasName)
            {
                existing.name = CanvasName;
            }

            if (existing.TryGetComponent(out Canvas existingCanvas))
            {
                return existingCanvas;
            }
        }

        GameObject canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        if (recordUndo)
        {
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create Enemy HUD Canvas");
        }

        canvasObject.transform.SetParent(enemy, false);
        return canvasObject.GetComponent<Canvas>();
    }

    static void ConfigureCanvas(Canvas canvas, RectTransform rectTransform, float localY, float localScale)
    {
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 20;

        rectTransform.localPosition = new Vector3(0f, localY, 0f);
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.localScale = Vector3.one * localScale;
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

    static RectTransform GetOrCreateRect(Transform parent, string name, bool recordUndo)
    {
        Transform existing = parent.Find(name);
        if (existing != null && existing.TryGetComponent(out RectTransform existingRect))
        {
            return existingRect;
        }

        GameObject rectObject = new GameObject(name, typeof(RectTransform));
        if (recordUndo)
        {
            Undo.RegisterCreatedObjectUndo(rectObject, $"Create {name}");
        }

        rectObject.transform.SetParent(parent, false);
        return rectObject.GetComponent<RectTransform>();
    }

    static Image GetOrCreateImage(Transform parent, string name, Sprite sprite, bool recordUndo)
    {
        RectTransform rectTransform = GetOrCreateRect(parent, name, recordUndo);
        Image image = rectTransform.GetComponent<Image>();
        if (image == null)
        {
            image = recordUndo
                ? Undo.AddComponent<Image>(rectTransform.gameObject)
                : rectTransform.gameObject.AddComponent<Image>();
        }

        image.sprite = sprite;
        image.raycastTarget = false;
        image.preserveAspect = true;
        return image;
    }

    static void ConfigureRect(RectTransform rectTransform, Vector2 anchoredPosition, Vector2 size)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.localScale = Vector3.one;
    }

    static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            Debug.LogWarning($"[EnemyHUD] Sprite not found or not imported as Sprite: {path}");
        }

        return sprite;
    }

    static void ApplyHUDRangeReferences(
        EnemyHUDRange hudRange,
        CharacterHealth characterHealth,
        GameObject enemyHUD,
        Image healthFill,
        float showDistance)
    {
        SerializedObject so = new SerializedObject(hudRange);
        so.FindProperty("characterHealth").objectReferenceValue = characterHealth;
        so.FindProperty("enemyHUD").objectReferenceValue = enemyHUD;
        so.FindProperty("healthFill").objectReferenceValue = healthFill;

        // targetReticle đã bỏ khỏi EnemyHUDRange — không gán nữa.
        SerializedProperty reticleProp = so.FindProperty("targetReticle");
        if (reticleProp != null)
        {
            reticleProp.objectReferenceValue = null;
        }

        so.FindProperty("showDistance").floatValue = showDistance;
        so.FindProperty("targetDistance").floatValue = Mathf.Max(1f, showDistance * 0.6f);
        so.FindProperty("faceCamera").boolValue = true;
        so.FindProperty("copyCameraRotation").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hudRange);
    }
}
#endif
