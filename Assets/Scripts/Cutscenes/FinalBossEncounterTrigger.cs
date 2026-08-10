using UnityEngine;

/// <summary>
/// Scene trigger for the Commander introduction. Kept separate from the
/// director so its BoxCollider remains easy to resize in World_Eden7.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class FinalBossEncounterTrigger : MonoBehaviour
{
    [SerializeField] private FinalBossEncounterCutscene encounter;

    public void Configure(FinalBossEncounterCutscene controller)
    {
        encounter = controller;
    }

    void Reset()
    {
        BoxCollider trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other == null || !other.CompareTag("Player"))
        {
            return;
        }

        encounter?.TryStartCutscene(other.transform.root);
    }
}
