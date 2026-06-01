using System.Collections.Generic;
using UnityEngine;

public class BillboardObjectList : MonoBehaviour
{
    private enum BillboardMode
    {
        CopyCameraRotation,
        LookAtCamera
    }

    [Header("Target Camera")]
    [SerializeField] private Camera targetCamera;

    [Header("Objects")]
    [SerializeField] private List<Transform> billboardObjects = new List<Transform>();

    [Header("Rotation")]
    [SerializeField] private BillboardMode mode = BillboardMode.CopyCameraRotation;
    [SerializeField] private bool onlyRotateY;
    [SerializeField] private bool flipForward = true;
    [SerializeField] private Vector3 rotationOffset;
    [SerializeField] private float smoothSpeed = 20f;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
        {
            return;
        }

        for (int i = 0; i < billboardObjects.Count; i++)
        {
            Transform billboard = billboardObjects[i];
            if (billboard == null)
            {
                continue;
            }

            FaceCamera(billboard);
        }
    }

    public void AddObject(Transform billboard)
    {
        if (billboard != null && !billboardObjects.Contains(billboard))
        {
            billboardObjects.Add(billboard);
        }
    }

    public void RemoveObject(Transform billboard)
    {
        billboardObjects.Remove(billboard);
    }

    private void FaceCamera(Transform billboard)
    {
        Quaternion targetRotation = mode == BillboardMode.CopyCameraRotation
            ? GetCameraRotation()
            : GetLookAtCameraRotation(billboard);

        targetRotation *= Quaternion.Euler(rotationOffset);

        if (smoothSpeed <= 0f)
        {
            billboard.rotation = targetRotation;
            return;
        }

        float damping = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        billboard.rotation = Quaternion.Slerp(billboard.rotation, targetRotation, damping);
    }

    private Quaternion GetCameraRotation()
    {
        Vector3 cameraEuler = targetCamera.transform.eulerAngles;
        Quaternion targetRotation;

        if (onlyRotateY)
        {
            targetRotation = Quaternion.Euler(0f, cameraEuler.y, 0f);
        }
        else
        {
            targetRotation = targetCamera.transform.rotation;
        }

        if (flipForward)
        {
            targetRotation *= Quaternion.Euler(0f, 180f, 0f);
        }

        return targetRotation;
    }

    private Quaternion GetLookAtCameraRotation(Transform billboard)
    {
        Vector3 direction = targetCamera.transform.position - billboard.position;

        if (onlyRotateY)
        {
            direction.y = 0f;
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return billboard.rotation;
        }

        if (flipForward)
        {
            direction = -direction;
        }

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }
}
