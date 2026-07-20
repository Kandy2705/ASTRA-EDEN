using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Toggle vùng sensor runtime (FOV cone + LOS ray) lúc Play.
/// Auto-spawn khi vào scene. Phím mặc định: <b>F3</b>.
/// </summary>
public sealed class EnemySensorVisionToggle : MonoBehaviour
{
    [SerializeField] private KeyCode toggleKey = KeyCode.F3;
    [SerializeField] private bool startEnabled = true;
    [SerializeField] private bool showOnScreenHint = true;

    static EnemySensorVisionToggle instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSpawn()
    {
        if (instance != null)
        {
            return;
        }

        var go = new GameObject("[EnemySensorVisionToggle]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<EnemySensorVisionToggle>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        EnemySensor.GlobalShowRuntimeVision = startEnabled;
    }

    void Update()
    {
        if (!WasTogglePressed())
        {
            return;
        }

        EnemySensor.GlobalShowRuntimeVision = !EnemySensor.GlobalShowRuntimeVision;
        Debug.Log($"[EnemySensor] Runtime vision = {EnemySensor.GlobalShowRuntimeVision} (phím {toggleKey})");
    }

    bool WasTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            // Map common debug key F3
            if (toggleKey == KeyCode.F3)
            {
                return Keyboard.current.f3Key.wasPressedThisFrame;
            }

            if (toggleKey == KeyCode.F4)
            {
                return Keyboard.current.f4Key.wasPressedThisFrame;
            }
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(toggleKey);
#else
        // New Input System only project without legacy: already handled F3/F4 above
        return false;
#endif
    }

    void OnGUI()
    {
        if (!showOnScreenHint || !Application.isPlaying)
        {
            return;
        }

        const float w = 340f;
        const float h = 28f;
        Rect r = new Rect(12f, Screen.height - h - 12f, w, h);
        string state = EnemySensor.GlobalShowRuntimeVision ? "ON" : "OFF";
        GUI.Box(r, $" Enemy FOV vision: {state}  [{toggleKey}] toggle");
    }
}
