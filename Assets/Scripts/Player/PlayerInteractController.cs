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

            float dist = Vector3.Distance(selfPosition, hit.transform.position);
            if (dist <= interactable.InteractionRange && dist < bestDist)
            {
                bestDist = dist;
                best = interactable;
            }
        }

        return best;
    }

    public string GetCurrentPrompt()
    {
        return currentTarget != null ? currentTarget.GetInteractPrompt() : string.Empty;
    }
}