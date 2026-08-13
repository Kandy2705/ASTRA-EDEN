using UnityEngine;

[DisallowMultipleComponent]
public sealed class HeroUpgradeStationInteractable : MonoBehaviour, IWorldInteractable
{
    [SerializeField, Min(0.5f)] private float interactionRange = 3f;
    [SerializeField] private string prompt = "Open Hero Upgrades [F]";

    public float InteractionRange => interactionRange;

    public bool CanInteract(Transform interactor)
    {
        return interactor != null && GameDataManager.Instance != null;
    }

    public void Interact(Transform interactor)
    {
        GameDataManager.Instance?.RequestHeroScreenOpen();
    }

    public string GetInteractPrompt()
    {
        return prompt;
    }
}
