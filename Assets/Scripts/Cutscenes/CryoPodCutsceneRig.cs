using UnityEngine;

/// <summary>
/// Reference container for a reusable cutscene-only cryopod. It deliberately
/// contains no gameplay or interaction logic; Timeline can animate the exposed
/// transforms and lights directly.
/// </summary>
[DisallowMultipleComponent]
public sealed class CryoPodCutsceneRig : MonoBehaviour
{
    [Header("Cutscene Anchors")]
    [SerializeField] private Transform playerAnchor;
    [SerializeField] private Transform doorPivot;
    [SerializeField] private Transform controlPanel;
    [SerializeField] private Transform vfxRoot;

    [Header("Timeline Lights")]
    [SerializeField] private Light interiorLight;
    [SerializeField] private Light statusLight;

    [Header("Door Preview")]
    [SerializeField] private Vector3 closedDoorEulerAngles = Vector3.zero;
    [SerializeField] private Vector3 openDoorEulerAngles = new(-72f, 0f, 0f);

    public Transform PlayerAnchor => playerAnchor;
    public Transform DoorPivot => doorPivot;
    public Transform ControlPanel => controlPanel;
    public Transform VfxRoot => vfxRoot;
    public Light InteriorLight => interiorLight;
    public Light StatusLight => statusLight;

    [ContextMenu("Preview Door Closed")]
    private void PreviewDoorClosed()
    {
        if (doorPivot != null)
        {
            doorPivot.localRotation = Quaternion.Euler(closedDoorEulerAngles);
        }
    }

    [ContextMenu("Preview Door Open")]
    private void PreviewDoorOpen()
    {
        if (doorPivot != null)
        {
            doorPivot.localRotation = Quaternion.Euler(openDoorEulerAngles);
        }
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        Transform playerPlacement,
        Transform door,
        Transform panel,
        Transform effectsRoot,
        Light cyanInteriorLight,
        Light amberStatusLight)
    {
        playerAnchor = playerPlacement;
        doorPivot = door;
        controlPanel = panel;
        vfxRoot = effectsRoot;
        interiorLight = cyanInteriorLight;
        statusLight = amberStatusLight;
        closedDoorEulerAngles = Vector3.zero;
        openDoorEulerAngles = new Vector3(-72f, 0f, 0f);
    }
#endif
}
