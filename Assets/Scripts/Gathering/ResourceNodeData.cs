using UnityEngine;

[CreateAssetMenu(fileName = "SO_ResourceNode_", menuName = "ASTRA EDEN/Gathering/Resource Node Data")]
public class ResourceNodeData : ScriptableObject
{
    [Header("Identity")]
    public string nodeId;
    public string displayName;

    [Header("Output")]
    public ItemData outputItem;
    [Min(1)] public int minAmount = 1;
    [Min(1)] public int maxAmount = 3;

    [Header("Gathering")]
    [Min(0.2f)] public float gatherDuration = 2f;
    [Tooltip("<= 0 = không respawn.")]
    [Min(0f)] public float respawnTime = 45f;
}