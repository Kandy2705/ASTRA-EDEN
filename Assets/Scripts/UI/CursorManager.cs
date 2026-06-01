using UnityEngine;

public class CursorManager : MonoBehaviour
{
    [Header("Cursor Image")]
    [SerializeField] private Texture2D cursorTexture;

    [Header("Cursor Size")]
    [SerializeField] private int cursorWidth = 32;
    [SerializeField] private int cursorHeight = 32;

    [Header("Hot Spot")]
    [Tooltip("Điểm bấm của chuột. Với mũi tên thường để (0,0). Với tâm ngắm để ở giữa ảnh.")]
    [SerializeField] private Vector2 hotSpot = Vector2.zero;

    [SerializeField] private CursorMode cursorMode = CursorMode.Auto;

    private Texture2D resizedCursor;

    private void Start()
    {
        ApplyCursor();
    }

    private void OnEnable()
    {
        ApplyCursor();
    }

    private void ApplyCursor()
    {
        if (cursorTexture == null)
        {
            Debug.LogWarning("Chưa gán ảnh chuột vào Cursor Texture!");
            return;
        }

        resizedCursor = ResizeTexture(cursorTexture, cursorWidth, cursorHeight);

        // Nếu muốn auto chỉnh tâm bấm theo ảnh đã resize thì dùng dòng này cho crosshair:
        // hotSpot = new Vector2(cursorWidth / 2f, cursorHeight / 2f);

        Cursor.SetCursor(resizedCursor, hotSpot, cursorMode);
    }

    private Texture2D ResizeTexture(Texture2D source, int width, int height)
    {
        RenderTexture rt = RenderTexture.GetTemporary(width, height);
        RenderTexture.active = rt;

        Graphics.Blit(source, rt);

        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        return result;
    }

    public void ResetCursor()
    {
        Cursor.SetCursor(null, Vector2.zero, cursorMode);
    }
}