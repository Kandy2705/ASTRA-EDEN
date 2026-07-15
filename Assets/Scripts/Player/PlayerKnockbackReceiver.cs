using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerKnockbackReceiver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Rigidbody targetRigidbody;

    Coroutine knockbackRoutine;

    public bool IsKnockedBack => knockbackRoutine != null;

    void Awake()
    {
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponent<Rigidbody>();
        }
    }

    /// <summary>Pushes the player away from an incoming hit direction.</summary>
    public void ApplyKnockback(Vector3 direction, float distance, float duration, float verticalLift)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = -transform.forward;
        }

        direction.Normalize();

        if (targetRigidbody != null && !targetRigidbody.isKinematic)
        {
            Vector3 velocityChange = direction * (distance / Mathf.Max(duration, 0.01f));
            velocityChange.y = verticalLift;
            targetRigidbody.AddForce(velocityChange, ForceMode.VelocityChange);
            return;
        }

        if (characterController == null)
        {
            transform.position += direction * distance;
            return;
        }

        if (knockbackRoutine != null)
        {
            StopCoroutine(knockbackRoutine);
        }

        knockbackRoutine = StartCoroutine(KnockbackRoutine(direction, distance, duration, verticalLift));
    }

    IEnumerator KnockbackRoutine(Vector3 direction, float distance, float duration, float verticalLift)
    {
        float elapsedTime = 0f;
        float safeDuration = Mathf.Max(duration, 0.01f);

        while (elapsedTime < safeDuration)
        {
            float deltaRatio = Time.deltaTime / safeDuration;
            float height = Mathf.Sin((elapsedTime / safeDuration) * Mathf.PI) * verticalLift;

            Vector3 moveDelta = direction * (distance * deltaRatio);
            moveDelta.y += height * deltaRatio;

            characterController.Move(moveDelta);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        knockbackRoutine = null;
    }
}