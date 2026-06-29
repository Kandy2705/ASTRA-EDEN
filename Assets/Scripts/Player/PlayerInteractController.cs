using UnityEngine;

[DisallowMultipleComponent]
public class PlayerInteractController : MonoBehaviour
{
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private float scanRadius = 3f;
    [SerializeField] private LayerMask interactableMask = ~0;
    [SerializeField] private bool logInteract;

    IWorldInteractable currentTarget;

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
        Collider[] hits = Physics.OverlapSphere(transform.position, scanRadius, interactableMask, QueryTriggerInteraction.Collide);
        IWorldInteractable best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null) continue;

            IWorldInteractable interactable = hits[i].GetComponentInParent<IWorldInteractable>();
            if (interactable == null || !interactable.CanInteract(transform))
            {
                continue;
            }

            float dist = Vector3.Distance(transform.position, hits[i].transform.position);
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