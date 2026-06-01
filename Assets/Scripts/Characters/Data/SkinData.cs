using UnityEngine;

[CreateAssetMenu(fileName = "SO_Skin_", menuName = "ASTRA EDEN/Characters/Skin Data")]
public class SkinData : ScriptableObject
{
    public string skinId;
    public string displayName;
    public CharacterData character;
    public GameObject skinPrefab;
    public Sprite icon;
    public bool unlockedByDefault;
}
