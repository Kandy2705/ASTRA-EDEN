using UnityEngine;

[DisallowMultipleComponent]
public class PlayerInteractController : MonoBehaviour
{
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private float scanRadius = 3f;
    [SerializeField] private LayerMask interactableMask = ~0;
    [SerializeField] private bool logInteract;

    IWorldInteractable currentTarget;
    static readonly Collider[] OverlapBuffer = new Collider[32];

    void Awake()
    {
        if (inputReader == null)
        {
            inputReader = GetComponent<PlayerInputReader>();
        }
    }

    void Update()
    {
        currentTarget = FindBestInteractable();

        if (inputReader != null && inputReader.InteractPressed && currentTarget != null)
        {
            if (currentTarget.CanInteract(transform))
            {
                currentTarget.Interact(transform);
                if (logInteract)
                {
                    Debug.Log($"[Interact] {currentTarget.GetInteractPrompt()}");
                }
            }
        }
    }

    IWorldInteractable FindBestInteractable()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            scanRadius,
            OverlapBuffer,
            interactableMask,
            QueryTriggerInteraction.Collide);

        IWorldInteractable best = null;
        float bestDist = float.MaxValue;
        Vector3 selfPosition = transform.position;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = OverlapBuffer[i];
            if (hit == null) continue;

            IWorldInteractable interactable = hit.GetComponentInParent<IWorldInteractable>();
            if (interactable == null || !interactable.CanInteract(transform))
            {
                continue;
            }

            float dist = Vector3.Distance(selfPosition, GetClosestPointSafe(hit, selfPosition));
            if (dist <= interactable.InteractionRange && dist < bestDist)
            {
                bestDist = dist;
                best = interactable;
            }
        }

        return best;
    }

    private static Vector3 GetClosestPointSafe(Collider collider, Vector3 position)
    {
        if (collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider)
            return collider.ClosestPoint(position);

        if (collider is MeshCollider meshCollider && meshCollider.convex)
            return collider.ClosestPoint(position);

        // Non-convex MeshCollider, TerrainCollider and custom collider types do not
        // support Physics.ClosestPoint. Bounds is sufficient for interaction ranking
        // and avoids a warning every scan frame.
        return collider.bounds.ClosestPoint(position);
    }

    public string GetCurrentPrompt()
    {
        return currentTarget != null ? currentTarget.GetInteractPrompt() : string.Empty;
    }
}
