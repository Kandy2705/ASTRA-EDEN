using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class SpawnStationInteractable : MonoBehaviour, IWorldInteractable
{
    [SerializeField, Min(0.5f)] private float interactionRange = 3f;
    [SerializeField] private string prompt = "Open Spawn Loadout [F]";
    [SerializeField] private string allowedScene = "Beacon_Camp";

    public float InteractionRange => interactionRange;

    public bool CanInteract(Transform interactor)
    {
        return interactor != null && GameDataManager.Instance != null &&
            string.Equals(SceneManager.GetActiveScene().name, allowedScene, System.StringComparison.Ordinal);
    }

    public void Interact(Transform interactor)
    {
        if (CanInteract(interactor)) GameDataManager.Instance.RequestSpawnLoadoutScreenOpen();
    }

    public string GetInteractPrompt() => prompt;
}
