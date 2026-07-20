using AillieoUtils.LOS2D;
using UnityEngine;

/// <summary>
/// Gắn trên Player (hoặc object có Collider) để Aillieo LOS2D nhận diện.
/// <see cref="LOSTarget"/> require Collider — dùng collider trên player root / capsule.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerLOSTarget : MonoBehaviour
{
    public static PlayerLOSTarget Instance { get; private set; }

    [SerializeField] private LOSTarget losTarget;

    public LOSTarget Target => losTarget;

    void Awake()
    {
        Instance = this;
        EnsureLosTarget();
    }

    void OnEnable()
    {
        Instance = this;
        EnsureLosTarget();
    }

    void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void EnsureLosTarget()
    {
        if (losTarget == null)
        {
            losTarget = GetComponent<LOSTarget>();
        }

        if (losTarget == null)
        {
            // RequireComponent(Collider) trên LOSTarget — player thường có CharacterController/Capsule.
            if (GetComponent<Collider>() == null && GetComponent<CharacterController>() == null)
            {
                // CharacterController is not Collider for RequireComponent check of LOSTarget
                // LOSTarget requires Collider — add a trigger capsule if missing.
                var capsule = gameObject.AddComponent<CapsuleCollider>();
                capsule.isTrigger = true;
                capsule.height = 1.8f;
                capsule.radius = 0.35f;
                capsule.center = new Vector3(0f, 0.9f, 0f);
                Debug.LogWarning("[PlayerLOSTarget] Added CapsuleCollider trigger for LOSTarget.", this);
            }

            // CharacterController alone: LOSTarget RequireComponent(Collider) may fail at edit time.
            // Runtime AddComponent still works if a Collider exists.
            if (GetComponent<Collider>() != null)
            {
                losTarget = gameObject.AddComponent<LOSTarget>();
            }
            else
            {
                // Child proxy with collider for ray hits
                Transform proxy = transform.Find("LOS2D_TargetProxy");
                GameObject proxyGo;
                if (proxy == null)
                {
                    proxyGo = new GameObject("LOS2D_TargetProxy");
                    proxyGo.transform.SetParent(transform, false);
                    proxyGo.transform.localPosition = new Vector3(0f, 1f, 0f);
                    var box = proxyGo.AddComponent<BoxCollider>();
                    box.size = new Vector3(0.6f, 1.6f, 0.6f);
                    box.isTrigger = true;
                }
                else
                {
                    proxyGo = proxy.gameObject;
                }

                // Tag proxy so bridge can find player root
                if (!proxyGo.CompareTag("Player") && gameObject.CompareTag("Player"))
                {
                    // Keep untagged; IsChildOf player works in LOSManager
                }

                losTarget = proxyGo.GetComponent<LOSTarget>();
                if (losTarget == null)
                {
                    losTarget = proxyGo.AddComponent<LOSTarget>();
                }
            }
        }
    }
}
