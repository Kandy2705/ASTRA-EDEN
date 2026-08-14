using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class StoreEntryCardView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image portrait;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text typeLabel;
    [SerializeField] private TMP_Text rarityLabel;
    [SerializeField] private GameObject ownedBadge;
    [SerializeField] private GameObject selectedVisual;

    private Action clickAction;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(HandleClick);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(HandleClick);
    }

    public void Configure(Button cardButton, Image cardPortrait, TMP_Text cardTitle,
        TMP_Text cardType, TMP_Text cardRarity, GameObject cardOwnedBadge, GameObject cardSelectedVisual)
    {
        if (button != null) button.onClick.RemoveListener(HandleClick);
        button = cardButton != null ? cardButton : GetComponent<Button>();
        portrait = cardPortrait;
        title = cardTitle;
        typeLabel = cardType;
        rarityLabel = cardRarity;
        ownedBadge = cardOwnedBadge;
        selectedVisual = cardSelectedVisual;
        if (button != null) button.onClick.AddListener(HandleClick);
    }

    public void Bind(Sprite image, string displayName, string type, string rarity, bool owned, bool selected, Action onClick)
    {
        clickAction = onClick;
        if (portrait != null)
        {
            portrait.sprite = image;
            portrait.enabled = image != null;
            portrait.preserveAspect = true;
        }
        if (title != null) title.text = displayName;
        if (typeLabel != null) typeLabel.text = type;
        if (rarityLabel != null) rarityLabel.text = rarity;
        if (ownedBadge != null) ownedBadge.SetActive(owned);
        if (selectedVisual != null) selectedVisual.SetActive(selected);
        if (button != null) button.interactable = true;
    }

    private void HandleClick() => clickAction?.Invoke();
}
