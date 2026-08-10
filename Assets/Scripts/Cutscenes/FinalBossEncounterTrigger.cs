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
    [SerializeField, Min(0.1f)] private float stayRetryInterval = 0.35f;

    float nextStayAttemptTime;

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
        TryStartFromCollider(other);
    }

    // OnTriggerEnter không bắn lại nếu Debug reset trong lúc Player đang đứng sẵn
    // trong vùng. OnTriggerStay giúp tình huống demo đó tự chạy ngay sau khi reset.
    void OnTriggerStay(Collider other)
    {
        if (Time.unscaledTime < nextStayAttemptTime)
        {
            return;
        }

        nextStayAttemptTime = Time.unscaledTime + stayRetryInterval;
        TryStartFromCollider(other);
    }

    public bool TryStartPlayerAlreadyInside()
    {
        if (encounter == null)
        {
            return false;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        BoxCollider trigger = GetComponent<BoxCollider>();
        if (playerObject == null || trigger == null || !trigger.enabled)
        {
            return false;
        }

        Collider playerCollider = playerObject.GetComponentInChildren<Collider>();
        Vector3 probePoint = playerCollider != null
            ? trigger.ClosestPoint(playerCollider.bounds.center)
            : trigger.ClosestPoint(playerObject.transform.position);
        Vector3 playerPoint = playerCollider != null
            ? playerCollider.bounds.center
            : playerObject.transform.position;
        if ((probePoint - playerPoint).sqrMagnitude > 0.01f)
        {
            return false;
        }

        return encounter.TryStartCutscene(playerObject.transform);
    }

    bool TryStartFromCollider(Collider other)
    {
        if (other == null || encounter == null)
        {
            return false;
        }

        Transform player = other.CompareTag("Player")
            ? other.transform.root
            : other.transform.root.CompareTag("Player")
                ? other.transform.root
                : null;
        if (player != null)
        {
            return encounter.TryStartCutscene(player);
        }

        return false;
    }
}
