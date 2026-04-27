using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopManager : MonoBehaviour
{
    private const int ShopTreasureCost = 800;
    private const int WanderingTraderTreasureCost = 1000;
    private const int ShopOfferCount = 3;
    private const int WanderingTraderOfferCount = 4;

    private enum TradeMode
    {
        None,
        Shop,
        WanderingTrader
    }

    [Header("UI Elements")]
    public GameObject shopUI;
    public Button leaveButton;
    public Button[] itemButtons;

    public static ShopManager Instance;

    private PlayerPawn currentPlayer;
    private int pendingFreeTreasurePurchases;
    private TradeMode currentMode = TradeMode.None;
    private TreasureCard[] activeOffers = new TreasureCard[0];
    private TreasureCard[] sharedWanderingTraderOffers = new TreasureCard[0];
    private bool tradeUISuspended;
    private readonly List<Button> runtimeItemButtons = new List<Button>();
    private readonly List<Vector2> originalButtonPositions = new List<Vector2>();
    private Button sellTreasureButton;
    private RectTransform runtimeHeaderPanel;
    private TextMeshProUGUI runtimeHeaderTitle;
    private TextMeshProUGUI runtimeHeaderSubtitle;
    private TextMeshProUGUI runtimeHeaderHint;
    private readonly Color treasureCardBackground = new Color(0.78f, 0.63f, 0.23f, 0.96f);
    private readonly Color treasureCardDisabledBackground = new Color(0.40f, 0.35f, 0.22f, 0.76f);
    private readonly Vector2 treasureOfferCardSize = new Vector2(238f, 298f);

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (shopUI != null) shopUI.SetActive(false);
        if (leaveButton != null) leaveButton.gameObject.SetActive(false);

        if (leaveButton != null)
            leaveButton.onClick.AddListener(CloseShop);
    }

    public void OpenShop(PlayerPawn player)
    {
        if (player == null)
            return;

        TreasureDeck.EnsureExists();

        currentPlayer = player;
        pendingFreeTreasurePurchases = 0;
        currentMode = TradeMode.Shop;
        activeOffers = DrawOffers(ShopOfferCount);

        if (shopUI != null) shopUI.SetActive(true);
        if (leaveButton != null) leaveButton.gameObject.SetActive(true);

        currentPlayer.isResolvingSpace = true;
        UpdateItemButtons();
    }

    public void OpenWanderingTrader(PlayerPawn player)
    {
        if (player == null)
            return;

        TreasureDeck.EnsureExists();

        currentPlayer = player;
        pendingFreeTreasurePurchases = 0;
        currentMode = TradeMode.WanderingTrader;

        if (sharedWanderingTraderOffers == null || sharedWanderingTraderOffers.Length == 0)
            sharedWanderingTraderOffers = DrawOffers(WanderingTraderOfferCount);

        activeOffers = sharedWanderingTraderOffers;

        if (shopUI != null) shopUI.SetActive(true);
        if (leaveButton != null) leaveButton.gameObject.SetActive(false);

        UpdateItemButtons();
    }

    public void BuyItemAtIndex(int index)
    {
        if (currentPlayer == null || index < 0 || index >= activeOffers.Length)
            return;

        TreasureCard offer = activeOffers[index];
        if (offer == null)
            return;

        int itemCost = GetDisplayCost();
        if (currentPlayer.gold < itemCost)
        {
            Debug.Log($"{currentPlayer.playerName} cannot afford {offer.cardName}!");
            return;
        }

        currentPlayer.SpendGold(itemCost);
        string prompt = $"{currentPlayer.playerName}: choose 1 treasure to replace, or keep your current treasures.";
        currentPlayer.AcquireTreasure(offer, prompt, acquired =>
        {
            if (!acquired)
            {
                currentPlayer.AddGold(itemCost);
                UpdateItemButtons();
                return;
            }

            Debug.Log($"{currentPlayer.playerName} bought {offer.cardName} for {itemCost} gold.");
            ConsumeFreeTreasurePurchaseIfNeeded();
            activeOffers[index] = null;
            UpdateItemButtons();
        });
    }

    public void CloseShop()
    {
        bool shouldNotifyPlayer = currentMode == TradeMode.Shop;
        CloseTradeInternal(shouldNotifyPlayer);
    }

    public void CloseTradeSilently()
    {
        CloseTradeInternal(false);
    }

    public void EndWanderingTraderSession()
    {
        if (TreasureDeck.Instance != null && sharedWanderingTraderOffers != null)
        {
            foreach (TreasureCard offer in sharedWanderingTraderOffers)
            {
                if (offer != null)
                    TreasureDeck.Instance.DiscardTreasure(offer);
            }
        }

        sharedWanderingTraderOffers = new TreasureCard[0];
        activeOffers = new TreasureCard[0];
        pendingFreeTreasurePurchases = 0;
        tradeUISuspended = false;
    }

    public List<TreasureCard> GetAvailableWanderingTraderOffers()
    {
        List<TreasureCard> offers = new List<TreasureCard>();
        if (sharedWanderingTraderOffers == null)
            return offers;

        foreach (TreasureCard offer in sharedWanderingTraderOffers)
        {
            if (offer != null)
                offers.Add(offer);
        }

        return offers;
    }

    public void StealWanderingTraderTreasure(PlayerPawn player, TreasureCard offer, System.Action<bool> onComplete)
    {
        if (player == null || offer == null || sharedWanderingTraderOffers == null)
        {
            onComplete?.Invoke(false);
            return;
        }

        int offerIndex = System.Array.IndexOf(sharedWanderingTraderOffers, offer);
        if (offerIndex < 0)
        {
            onComplete?.Invoke(false);
            return;
        }

        player.AcquireTreasure(
            offer,
            $"{player.playerName}: choose 1 treasure to replace, or keep your current treasures.",
            acquired =>
            {
                if (acquired)
                {
                    sharedWanderingTraderOffers[offerIndex] = null;
                    if (activeOffers == sharedWanderingTraderOffers)
                        activeOffers = sharedWanderingTraderOffers;

                    Debug.Log($"{player.playerName} stole {offer.cardName} from the Wandering Trader.");
                    UpdateItemButtons();
                }

                onComplete?.Invoke(acquired);
            });
    }

    public bool IsShopOpenFor(PlayerPawn player)
    {
        return player != null &&
               currentPlayer == player &&
               currentMode != TradeMode.None &&
               shopUI != null &&
               (shopUI.activeSelf || tradeUISuspended);
    }

    public bool CanUseTradeCard(PlayerPawn player)
    {
        return IsShopOpenFor(player);
    }

    public bool RedeemShopVoucher(PlayerPawn player)
    {
        if (!CanUseTradeCard(player))
            return false;

        pendingFreeTreasurePurchases++;
        Debug.Log($"{player.playerName} redeemed a Shop Voucher. Their next treasure is free.");
        UpdateItemButtons();
        return true;
    }

    public bool SellMythicMoonstone(PlayerPawn player, int goldAmount)
    {
        if (!CanUseTradeCard(player))
            return false;

        player.AddGold(goldAmount);
        Debug.Log($"{player.playerName} sold Mythic Moonstone for {goldAmount} gold.");
        UpdateItemButtons();
        return true;
    }

    public void SuspendTradeUI()
    {
        if (tradeUISuspended || currentMode == TradeMode.None)
            return;

        tradeUISuspended = true;

        if (shopUI != null)
            shopUI.SetActive(false);

        if (leaveButton != null)
            leaveButton.gameObject.SetActive(false);
    }

    public void ResumeTradeUI()
    {
        if (!tradeUISuspended || currentMode == TradeMode.None)
            return;

        tradeUISuspended = false;

        if (shopUI != null)
            shopUI.SetActive(true);

        if (leaveButton != null)
            leaveButton.gameObject.SetActive(currentMode == TradeMode.Shop);

        UpdateItemButtons();
    }

    public void RefreshTradeUI()
    {
        if (currentMode == TradeMode.None)
            return;

        UpdateItemButtons();
    }

    public void SellSelectedTreasure()
    {
        if (currentPlayer == null || currentPlayer.equippedTreasures.Count == 0)
            return;

        TreasureSelectionUI.EnsureExists();
        TreasureSelectionUI.Instance.BeginSelection(
            currentPlayer,
            $"{currentPlayer.playerName}: choose 1 treasure to sell for 500 gold",
            selectedTreasure =>
            {
                if (selectedTreasure == null)
                {
                    UpdateItemButtons();
                    return;
                }

                currentPlayer.equippedTreasures.Remove(selectedTreasure);
                TreasureDeck.Instance?.DiscardTreasure(selectedTreasure);
                currentPlayer.AddGold(500);
                Debug.Log($"{currentPlayer.playerName} sold {selectedTreasure.cardName} for 500 gold.");
                UpdateItemButtons();
            },
            null,
            true,
            "Cancel Sale");
    }

    private void CloseTradeInternal(bool notifyPlayer)
    {
        bool preserveWanderingTraderOffers = currentMode == TradeMode.WanderingTrader && !notifyPlayer;

        if (!preserveWanderingTraderOffers && TreasureDeck.Instance != null)
        {
            foreach (TreasureCard offer in activeOffers)
            {
                if (offer != null)
                    TreasureDeck.Instance.DiscardTreasure(offer);
            }
        }

        activeOffers = preserveWanderingTraderOffers ? sharedWanderingTraderOffers : new TreasureCard[0];
        pendingFreeTreasurePurchases = 0;
        tradeUISuspended = false;
        currentMode = TradeMode.None;

        if (shopUI != null) shopUI.SetActive(false);
        if (leaveButton != null) leaveButton.gameObject.SetActive(false);
        if (runtimeHeaderPanel != null) runtimeHeaderPanel.gameObject.SetActive(false);

        if (notifyPlayer && currentPlayer != null)
            currentPlayer.FinishShopResolution();

        currentPlayer = null;
    }

    private TreasureCard[] DrawOffers(int count)
    {
        if (TreasureDeck.Instance == null)
            return new TreasureCard[0];

        List<TreasureCard> offers = TreasureDeck.Instance.DrawTreasures(count);
        return offers.ToArray();
    }

    private int GetDisplayCost()
    {
        if (pendingFreeTreasurePurchases > 0)
            return 0;

        return currentMode == TradeMode.WanderingTrader
            ? WanderingTraderTreasureCost
            : ShopTreasureCost;
    }

    private void UpdateItemButtons()
    {
        EnsureButtonCapacity(activeOffers.Length);
        EnsureHeaderPanel();
        UpdateHeaderPanel();
        ApplyCurrentLayout();
        EnsureSellButton();

        for (int i = 0; i < runtimeItemButtons.Count; i++)
        {
            Button button = runtimeItemButtons[i];
            if (button == null)
                continue;

            if (i < activeOffers.Length && activeOffers[i] != null)
            {
                int itemIndex = i;
                TreasureCard offer = activeOffers[i];
                int displayCost = GetDisplayCost();

                button.gameObject.SetActive(true);
                button.interactable =
                    currentPlayer != null &&
                    currentPlayer.gold >= displayCost;

                UpdateButtonText(button, offer, displayCost);

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => BuyItemAtIndex(itemIndex));
            }
            else
            {
                button.gameObject.SetActive(false);
                button.onClick.RemoveAllListeners();
            }
        }

        if (sellTreasureButton != null)
        {
            sellTreasureButton.gameObject.SetActive(currentPlayer != null);
            sellTreasureButton.interactable =
                currentPlayer != null &&
                currentPlayer.equippedTreasures != null &&
                currentPlayer.equippedTreasures.Count > 0;
            ApplySellButtonLayout();
        }
    }

    private void ConsumeFreeTreasurePurchaseIfNeeded()
    {
        if (pendingFreeTreasurePurchases > 0)
            pendingFreeTreasurePurchases--;
    }

    private void EnsureButtonCapacity(int requiredCount)
    {
        if (itemButtons == null || itemButtons.Length == 0)
            return;

        if (runtimeItemButtons.Count == 0)
        {
            foreach (Button button in itemButtons)
            {
                if (button == null)
                    continue;

                runtimeItemButtons.Add(button);
                RectTransform rectTransform = button.transform as RectTransform;
                originalButtonPositions.Add(rectTransform != null ? rectTransform.anchoredPosition : Vector2.zero);
            }
        }

        Button templateButton = runtimeItemButtons.Count > 0 ? runtimeItemButtons[runtimeItemButtons.Count - 1] : null;
        while (runtimeItemButtons.Count < requiredCount && templateButton != null)
        {
            Button clonedButton = Instantiate(templateButton, templateButton.transform.parent);
            clonedButton.name = $"{templateButton.name}_Runtime{runtimeItemButtons.Count}";
            runtimeItemButtons.Add(clonedButton);

            RectTransform clonedRect = clonedButton.transform as RectTransform;
            originalButtonPositions.Add(clonedRect != null ? clonedRect.anchoredPosition : Vector2.zero);
        }
    }

    private void ApplyCurrentLayout()
    {
        if (runtimeItemButtons.Count == 0)
            return;

        if (currentMode != TradeMode.WanderingTrader)
        {
            Vector2 startPosition = new Vector2(700f, -138f);
            float horizontalSpacing = 258f;

            for (int i = 0; i < runtimeItemButtons.Count; i++)
            {
                RectTransform rectTransform = runtimeItemButtons[i].transform as RectTransform;
                if (rectTransform == null)
                    continue;

                rectTransform.anchorMin = new Vector2(0f, 1f);
                rectTransform.anchorMax = new Vector2(0f, 1f);
                rectTransform.pivot = new Vector2(0f, 1f);
                rectTransform.sizeDelta = treasureOfferCardSize;
                rectTransform.localScale = Vector3.one;
                rectTransform.anchoredPosition = startPosition + new Vector2(horizontalSpacing * i, 0f);
            }
            return;
        }

        RectTransform firstRect = runtimeItemButtons[0].transform as RectTransform;
        if (firstRect == null)
            return;

        Vector2 traderStartPosition = new Vector2(694f, -138f);
        float traderHorizontalSpacing = 276f;
        float verticalSpacing = 322f;

        for (int i = 0; i < runtimeItemButtons.Count; i++)
        {
            RectTransform rectTransform = runtimeItemButtons[i].transform as RectTransform;
            if (rectTransform == null)
                continue;

            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.sizeDelta = treasureOfferCardSize;
            rectTransform.localScale = Vector3.one;

            int column = i % 2;
            int row = i / 2;
            rectTransform.anchoredPosition = traderStartPosition + new Vector2(traderHorizontalSpacing * column, -verticalSpacing * row);
        }
    }

    private void UpdateButtonText(Button button, TreasureCard offer, int displayCost)
    {
        if (button == null || offer == null)
            return;

        TreasureCardOfferView view = EnsureTreasureCardView(button);
        if (view == null)
            return;

        bool canAfford = currentPlayer != null && currentPlayer.gold >= displayCost;
        Image buttonImage = button.GetComponent<Image>();
        if (buttonImage != null)
            buttonImage.color = canAfford ? treasureCardBackground : treasureCardDisabledBackground;

        view.Configure(offer, displayCost, canAfford);
    }

    private TreasureCardOfferView EnsureTreasureCardView(Button button)
    {
        if (button == null)
            return null;

        HideLegacyTreasureButtonText(button);

        Transform existing = button.transform.Find("TreasureCardView");
        if (existing != null)
        {
            existing.SetAsLastSibling();
            TreasureCardOfferView existingRefs = existing.GetComponent<TreasureCardOfferView>();
            if (existingRefs != null)
                return existingRefs;
        }

        GameObject viewObject = new GameObject("TreasureCardView", typeof(RectTransform));
        viewObject.transform.SetParent(button.transform, false);
        viewObject.transform.SetAsLastSibling();

        RectTransform root = viewObject.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = new Vector2(10f, 10f);
        root.offsetMax = new Vector2(-10f, -10f);

        TreasureCardOfferView refs = viewObject.AddComponent<TreasureCardOfferView>();
        refs.CardVisual = StandardCardVisual.Ensure(viewObject.transform);

        return refs;
    }

    private void HideLegacyTreasureButtonText(Button button)
    {
        Transform generatedView = button.transform.Find("TreasureCardView");

        TMP_Text[] textFields = button.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text textField in textFields)
        {
            if (textField == null || (generatedView != null && textField.transform.IsChildOf(generatedView)))
                continue;

            textField.gameObject.SetActive(false);
        }

        Text[] legacyTextFields = button.GetComponentsInChildren<Text>(true);
        foreach (Text textField in legacyTextFields)
        {
            if (textField == null || (generatedView != null && textField.transform.IsChildOf(generatedView)))
                continue;

            textField.gameObject.SetActive(false);
        }
    }

    private Image CreateCardPanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = panelObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private TextMeshProUGUI CreateCardText(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float maxSize,
        float minSize,
        FontStyles style,
        Color color,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.enableAutoSizing = false;
        text.fontSizeMax = maxSize;
        text.fontSizeMin = minSize;
        text.fontSize = maxSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.margin = new Vector4(4f, 2f, 4f, 2f);
        return text;
    }

    private void EnsureSellButton()
    {
        if (shopUI == null || sellTreasureButton != null)
            return;

        Transform parent = leaveButton != null ? leaveButton.transform.parent : shopUI.transform;
        GameObject buttonObject = new GameObject("SellTreasureButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(260f, 60f);
        rect.anchoredPosition = new Vector2(0f, 24f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.20f, 0.15f, 0.08f, 1f);

        sellTreasureButton = buttonObject.GetComponent<Button>();
        sellTreasureButton.onClick.AddListener(SellSelectedTreasure);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.fontSize = 24f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.text = "Sell Treasure";
    }

    private void ApplySellButtonLayout()
    {
        if (sellTreasureButton == null)
            return;

        RectTransform rect = sellTreasureButton.transform as RectTransform;
        if (rect == null)
            return;

        if (currentMode == TradeMode.WanderingTrader)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(260f, 58f);
            rect.anchoredPosition = new Vector2(976f, -796f);
        }
        else
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(250f, 56f);
            rect.anchoredPosition = new Vector2(1230f, -454f);
        }
    }

    private void EnsureHeaderPanel()
    {
        if (shopUI == null || runtimeHeaderPanel != null)
            return;

        Transform parent = leaveButton != null ? leaveButton.transform.parent : shopUI.transform;

        runtimeHeaderPanel = CreateCardPanel(
            "TradeHeaderPanel",
            parent,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Color(0.15f, 0.10f, 0.08f, 0.94f)).rectTransform;

        runtimeHeaderPanel.pivot = new Vector2(0f, 1f);
        runtimeHeaderPanel.sizeDelta = new Vector2(540f, 88f);
        runtimeHeaderPanel.anchoredPosition = new Vector2(694f, -34f);

        RectTransform accentRect = CreateCardPanel(
            "TradeHeaderAccent",
            runtimeHeaderPanel,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Color(0.80f, 0.38f, 0.13f, 1f)).rectTransform;
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(0f, 6f);

        runtimeHeaderTitle = CreateCardText(
            "TradeHeaderTitle",
            runtimeHeaderPanel,
            new Vector2(0f, 0.52f),
            new Vector2(1f, 1f),
            26f,
            18f,
            FontStyles.Bold,
            Color.white,
            TextAlignmentOptions.Left);
        runtimeHeaderTitle.margin = new Vector4(20f, 8f, 20f, 4f);

        runtimeHeaderSubtitle = CreateCardText(
            "TradeHeaderSubtitle",
            runtimeHeaderPanel,
            new Vector2(0f, 0.20f),
            new Vector2(1f, 0.58f),
            18f,
            12f,
            FontStyles.Normal,
            new Color(0.95f, 0.88f, 0.78f, 0.94f),
            TextAlignmentOptions.Left);
        runtimeHeaderSubtitle.margin = new Vector4(20f, 0f, 20f, 2f);

        runtimeHeaderHint = CreateCardText(
            "TradeHeaderHint",
            runtimeHeaderPanel,
            new Vector2(0f, 0f),
            new Vector2(1f, 0.24f),
            13f,
            10f,
            FontStyles.Italic,
            new Color(0.92f, 0.76f, 0.54f, 0.92f),
            TextAlignmentOptions.Left);
        runtimeHeaderHint.margin = new Vector4(20f, 0f, 20f, 6f);
    }

    private void UpdateHeaderPanel()
    {
        if (runtimeHeaderPanel == null)
            return;

        bool isVisible = currentMode != TradeMode.None && currentPlayer != null;
        runtimeHeaderPanel.gameObject.SetActive(isVisible);
        if (!isVisible)
            return;

        if (currentMode == TradeMode.WanderingTrader)
        {
            runtimeHeaderTitle.text = "Wandering Trader";
            runtimeHeaderSubtitle.text = $"{currentPlayer.playerName} is browsing the shared cave stock.";
            runtimeHeaderHint.text = "4 shared offers • 1000 gold each • buy as much as you can afford";
            return;
        }

        runtimeHeaderTitle.text = "Board Shop";
        runtimeHeaderSubtitle.text = $"{currentPlayer.playerName} is choosing from the town stock.";
        runtimeHeaderHint.text = "3 offers • 800 gold each • sold items disappear immediately";
    }

}
