using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class ResourceNode : MonoBehaviour, IWorldInteractable
{
    [SerializeField] private ResourceNodeData nodeData;
    [SerializeField] private float interactionRange = 2.5f;
    [SerializeField] private GameObject activeVisual;
    [SerializeField] private GameObject depletedVisual;

    bool isGathering;
    bool isDepleted;

    public float InteractionRange => interactionRange;
    public ResourceNodeData Data => nodeData;
    public bool IsDepleted => isDepleted;

    public bool CanInteract(Transform interactor)
    {
        return !isDepleted && !isGathering && nodeData != null && nodeData.outputItem != null;
    }

    public string GetInteractPrompt()
    {
        if (nodeData == null)
        {
            return "Gather [F]";
        }

        return $"Gather {nodeData.displayName} [F]";
    }

    public void Interact(Transform interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        PlayerInventoryService inventory = interactor.GetComponentInParent<PlayerInventoryService>();
        if (inventory == null)
        {
            Debug.LogWarning("[ResourceNode] Player không có PlayerInventoryService.", this);
            return;
        }

        StartCoroutine(GatherRoutine(interactor, inventory));
    }

    IEnumerator GatherRoutine(Transform interactor, PlayerInventoryService inventory)
    {
        isGathering = true;
        float duration = nodeData.gatherDuration;

        yield return new WaitForSeconds(duration);

        int amount = Random.Range(nodeData.minAmount, nodeData.maxAmount + 1);
        inventory.AddItem(nodeData.outputItem, amount);

        ZoneObjectiveManager objective = ZoneObjectiveManager.Instance;
        if (objective != null)
        {
            objective.NotifyResourceGathered(nodeData, amount);
        }

        SetDepleted(true);

        if (nodeData.respawnTime > 0f)
        {
            yield return new WaitForSeconds(nodeData.respawnTime);
            SetDepleted(false);
        }

        isGathering = false;
    }

    void SetDepleted(bool depleted)
    {
        isDepleted = depleted;
        if (activeVisual != null) activeVisual.SetActive(!depleted);
        if (depletedVisual != null) depletedVisual.SetActive(depleted);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.85f);
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}