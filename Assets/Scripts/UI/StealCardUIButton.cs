using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class StealCardUIButton : MonoBehaviour
{
    public TMP_Text cardNameText;
    private ReprieveCard card;
    private Action<ReprieveCard> callback;

    public void Setup(ReprieveCard cardData, Action<ReprieveCard> onClick)
    {
        card = cardData;
        callback = onClick;
        cardNameText.text = card.cardName;
    }

    public void OnClick()
    {
        callback?.Invoke(card);
    }
}
