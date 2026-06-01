using UnityEngine;
using UnityEngine.AI;

public class CharacterKnockback : MonoBehaviour
{
    [SerializeField] private float defaultDuration = 0.18f;

    private NavMeshAgent agent;
    private Vector3 velocity;
    private float timer;

    public bool IsKnockedBack => timer > 0f;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Update()
    {
        if (timer <= 0f)
        {
            return;
        }

        timer -= Time.deltaTime;
        Vector3 movement = velocity * Time.deltaTime;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.Move(movement);
            agent.velocity = Vector3.zero;
        }
        else
        {
            transform.position += movement;
        }
    }

    public void Apply(Vector3 direction, float distance, float duration = 0f)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f || distance <= 0f)
        {
            return;
        }

        float actualDuration = duration > 0f ? duration : defaultDuration;
        timer = actualDuration;
        velocity = direction.normalized * (distance / Mathf.Max(0.001f, actualDuration));
    }
}
