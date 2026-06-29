using UnityEngine;

public interface IWorldInteractable
{
    float InteractionRange { get; }
    bool CanInteract(Transform interactor);
    void Interact(Transform interactor);
    string GetInteractPrompt();
}