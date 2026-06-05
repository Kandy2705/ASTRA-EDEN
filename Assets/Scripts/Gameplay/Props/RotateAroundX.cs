using UnityEngine;

public class RotateAroundX : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("Tốc độ xoay quanh trục X, đơn vị là độ/giây")]
    public float rotateSpeed = 60f;

    [Tooltip("Bật nếu muốn xoay theo trục thế giới, tắt nếu muốn xoay theo trục local của object")]
    public bool useWorldSpace = false;

    private void Update()
    {
        Space rotateSpace = useWorldSpace ? Space.World : Space.Self;

        transform.Rotate(rotateSpeed * Time.deltaTime, 0f, 0f, rotateSpace);
    }
}