using UnityEngine;

public class FloatingLootVisual : MonoBehaviour
{
    [Header("Floating")]
    [SerializeField] private float floatHeight = 0.15f;
    [SerializeField] private float floatSpeed = 2f;

    [Header("Rotation")]
    [SerializeField] private float rotateSpeed = 45f;

    [Header("Physics")]
    [SerializeField] private float startFloatingDelay = 0.4f;

    private Vector3 startPos;
    private Rigidbody rb;
    private float timer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        startPos = transform.position;
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer < startFloatingDelay) return;

        // Nếu có Rigidbody thì tắt physics để không bị rơi / văng tiếp
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;

            startPos = transform.position;
        }

        // Lơ lửng lên xuống nhẹ
        float yOffset = Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        transform.position = startPos + Vector3.up * yOffset;

        // Xoay vòng vòng nhẹ
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
    }
}
