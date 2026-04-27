using TMPro;
using UnityEngine;

public class TreasureCardOfferView : MonoBehaviour
{
    public StandardCardVisual CardVisual;

    public TextMeshProUGUI Title => CardVisual != null ? CardVisual.TitleText : null;
    public TextMeshProUGUI Description => CardVisual != null ? CardVisual.DescriptionText : null;
    public TextMeshProUGUI Cost => CardVisual != null ? CardVisual.FooterText : null;

    public void Configure(TreasureCard treasure, int displayCost, bool canAfford, bool compact = false)
    {
        if (treasure == null)
            return;

        if (CardVisual == null)
            CardVisual = StandardCardVisual.Ensure(transform, compact ? StandardCardVisualDensity.Compact : StandardCardVisualDensity.Full);

        CardVisual?.ConfigureTreasure(
            treasure.cardName,
            treasure.description,
            $"{displayCost} Gold",
            canAfford,
            compact);
    }
}
