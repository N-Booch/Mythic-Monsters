using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TreasureDeck : MonoBehaviour
{
    public static TreasureDeck Instance { get; private set; }

    private readonly List<TreasureCard> drawPile = new List<TreasureCard>();
    private readonly List<TreasureCard> discardPile = new List<TreasureCard>();
    private readonly List<TreasureCard> masterDeck = new List<TreasureCard>();

    public int DrawPileCount => drawPile.Count;
    public int DiscardPileCount => discardPile.Count;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeDeck();
            return;
        }

        if (Instance != this)
            Destroy(gameObject);
    }

    private void Start()
    {
        if (drawPile.Count == 0 && masterDeck.Count == 0)
            InitializeDeck();
    }

    public static void EnsureExists()
    {
        if (Instance != null)
            return;

        TreasureDeck existingDeck = FindFirstObjectByType<TreasureDeck>();
        if (existingDeck != null)
        {
            Instance = existingDeck;
            existingDeck.InitializeDeck();
            return;
        }

        GameObject deckObject = new GameObject("TreasureDeck");
        deckObject.AddComponent<TreasureDeck>();
    }

    public void InitializeDeck()
    {
        masterDeck.Clear();
        drawPile.Clear();
        discardPile.Clear();

        TreasureCard[] discoveredCards = LoadTreasureAssets();
        if (discoveredCards == null || discoveredCards.Length == 0)
        {
            Debug.LogWarning("TreasureDeck could not find any TreasureCard assets.");
            return;
        }

        HashSet<TreasureCard> uniqueCards = new HashSet<TreasureCard>(discoveredCards);
        foreach (TreasureCard card in uniqueCards)
        {
            if (card == null)
                continue;

            int copies = GetCopiesFor(card);
            for (int i = 0; i < copies; i++)
                masterDeck.Add(card);
        }

        ShuffleInto(drawPile, masterDeck);
        Debug.Log($"TreasureDeck initialized with {masterDeck.Count} cards.");
    }

    private TreasureCard[] LoadTreasureAssets()
    {
#if UNITY_EDITOR
        string absoluteFolderPath = Path.Combine(Application.dataPath, "ScriptableObjects", "Treasure");
        if (!Directory.Exists(absoluteFolderPath))
        {
            Debug.LogWarning($"TreasureDeck folder not found: {absoluteFolderPath}");
            return System.Array.Empty<TreasureCard>();
        }

        List<TreasureCard> cards = new List<TreasureCard>();
        string[] assetFiles = Directory.GetFiles(absoluteFolderPath, "*.asset", SearchOption.TopDirectoryOnly);

        foreach (string assetFile in assetFiles)
        {
            string assetPath = "Assets" + assetFile.Replace(Application.dataPath, string.Empty).Replace('\\', '/');
            TreasureCard card = AssetDatabase.LoadAssetAtPath<TreasureCard>(assetPath);
            if (card != null)
                cards.Add(card);
        }

        return cards.ToArray();
#else
        return Resources.FindObjectsOfTypeAll<TreasureCard>();
#endif
    }

    public TreasureCard DrawTreasure()
    {
        if (drawPile.Count == 0)
            ReshuffleDiscardIntoDrawPile();

        if (drawPile.Count == 0)
            return null;

        TreasureCard card = drawPile[0];
        drawPile.RemoveAt(0);
        return card;
    }

    public List<TreasureCard> DrawTreasures(int count)
    {
        List<TreasureCard> results = new List<TreasureCard>();
        for (int i = 0; i < count; i++)
        {
            TreasureCard card = DrawTreasure();
            if (card == null)
                break;

            results.Add(card);
        }

        return results;
    }

    public void DiscardTreasure(TreasureCard card)
    {
        if (card == null)
            return;

        discardPile.Add(card);
    }

    public void DiscardTreasures(IEnumerable<TreasureCard> cards)
    {
        if (cards == null)
            return;

        foreach (TreasureCard card in cards)
            DiscardTreasure(card);
    }

    private int GetCopiesFor(TreasureCard card)
    {
        if (card != null && card.cardName == "Cleansing Potion")
            return 5;

        return 1;
    }

    private void ReshuffleDiscardIntoDrawPile()
    {
        if (discardPile.Count == 0)
            return;

        ShuffleInto(drawPile, discardPile);
        discardPile.Clear();
    }

    private void ShuffleInto(List<TreasureCard> target, List<TreasureCard> source)
    {
        target.Clear();
        target.AddRange(source);

        for (int i = 0; i < target.Count; i++)
        {
            int swapIndex = Random.Range(i, target.Count);
            (target[i], target[swapIndex]) = (target[swapIndex], target[i]);
        }
    }
}
