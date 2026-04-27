using System.Collections.Generic;
using UnityEngine;

public class ReprieveDeck : MonoBehaviour
{
    public static ReprieveDeck Instance;

    [Header("Deck Contents")]
    public List<ReprieveCard> deck = new List<ReprieveCard>();
    private List<ReprieveCard> drawPile = new List<ReprieveCard>();
    private List<ReprieveCard> discardPile = new List<ReprieveCard>();
    private bool isInitialized = false;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void InitializeDeck()
    {
        drawPile.Clear();

        // Fresh game start
        if (!isInitialized)
        {
            drawPile = new List<ReprieveCard>(deck);
        }
        else
        {
            // Reshuffle discard into draw pile
            drawPile = new List<ReprieveCard>(discardPile);
            discardPile.Clear();
        }

        Shuffle(drawPile);
        isInitialized = true;
    }

    public ReprieveCard DrawCard()
    {
        if (drawPile.Count == 0)
        {
            Debug.LogWarning("Reprieve deck empty — reshuffling discard pile");
            InitializeDeck();
        }

        ReprieveCard card = drawPile[0];
        drawPile.RemoveAt(0);
        return card;
    }

    private void Shuffle(List<ReprieveCard> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rand = Random.Range(i, list.Count);
            (list[i], list[rand]) = (list[rand], list[i]);
        }
    }

    public void Discard(ReprieveCard card)
    {
        if (card == null) return;

        discardPile.Add(card);
    }

}
