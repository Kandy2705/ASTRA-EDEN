using UnityEngine;

[DisallowMultipleComponent]
public class ShopInteractable : MonoBehaviour, IWorldInteractable
{
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private ShopController shopController;

    public float InteractionRange => interactionRange;

    public bool CanInteract(Transform interactor)
    {
        return shopController != null;
    }

    public string GetInteractPrompt()
    {
        return "Open Shop [F]";
    }

    public void Interact(Transform interactor)
    {
        shopController?.OpenShop();
    }
}