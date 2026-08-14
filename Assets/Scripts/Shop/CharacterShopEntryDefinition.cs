using UnityEngine;

[CreateAssetMenu(fileName = "SO_CharacterShopEntry_", menuName = "ASTRA EDEN/Shop/Character Entry")]
public sealed class CharacterShopEntryDefinition : ScriptableObject
{
    [SerializeField] private CharacterData character;

    public CharacterData Character => character;
    public int GoldPrice => character != null ? character.StoreGoldPrice : 0;
    public bool IsAvailableInStore => character != null && character.IsAvailableInStore;
}
