using UnityEngine;

[CreateAssetMenu(fileName = "SO_StatusEffect_", menuName = "ASTRA EDEN/Combat/Status Effect Data")]
public class StatusEffectData : ScriptableObject
{
    public string statusEffectId;
    public string displayName;

    [TextArea(3, 6)]
    public string description;

    [Min(0f)] public float duration;
    [Min(0f)] public float tickInterval;
}
