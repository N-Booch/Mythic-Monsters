using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ReprieveCardUI : MonoBehaviour
{
    public TMP_Text nameText;
    public TMP_Text descriptionText;
    public Button button;

    private ReprieveCard card;
    private PlayerPawn owner;
    private StandardCardVisual standardView;

    public void Setup(ReprieveCard newCard, PlayerPawn player, bool compact = false)
    {
        card = newCard;
        owner = player;

        HideLegacyVisuals();
        standardView = StandardCardVisual.Ensure(transform, compact ? StandardCardVisualDensity.Compact : StandardCardVisualDensity.Full);
        if (standardView != null)
            standardView.ConfigureReprieve(card.cardName, card.description, compact);

        // Clear old listeners
        button.onClick.RemoveAllListeners();

        // Wire button click to THIS instance
        button.onClick.AddListener(OnClick);
    }
    public void OnClick()
    {
        // Steal mode takes priority
        bool handledByInteractionMode = HandUIManager.Instance.OnCardClicked(card);
        if (handledByInteractionMode)
            return;

        // Normal play
        if (owner == HandUIManager.Instance.ViewedPlayer)
        {
            HandUIManager.Instance.PlayCard(card);
        }
    }

    private void HideLegacyVisuals()
    {
        if (nameText != null)
            nameText.gameObject.SetActive(false);

        if (descriptionText != null)
            descriptionText.gameObject.SetActive(false);

        foreach (Transform child in transform)
        {
            if (child == null || child.name == "StandardCardVisual")
                continue;

            if (nameText != null && child == nameText.transform)
                continue;

            if (descriptionText != null && child == descriptionText.transform)
                continue;

            child.gameObject.SetActive(false);
        }
    }

}
