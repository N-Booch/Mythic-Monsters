using UnityEngine;
using System;
using TMPro;
using UnityEngine.UI;

public class HandUIManager : MonoBehaviour
{
    public static HandUIManager Instance;

    public GameObject handPanel;
    public Transform cardParent;
    public GameObject cardPrefab;
    public TMP_Text headerText;

    private PlayerPawn viewedPlayer;
    private string currentHeaderMessage;
    private Button cancelButton;
    private TMP_Text cancelButtonText;
    private RectTransform handPanelRect;
    private RectTransform frameRect;
    private RectTransform cardViewportRect;
    private RectTransform runtimeCardContainer;
    private HorizontalLayoutGroup cardLayout;
    private ContentSizeFitter cardContentFitter;
    private ScrollRect cardScrollRect;
    private TMP_Text emptyHandText;

    // Steal-mode state
    private bool isStealMode = false;
    private bool stealSessionActive = false;
    private bool forcedDiscardActive = false;
    private bool persistentCombatView = false;
    private bool hidPlayerHudForHand = false;
    private bool hidEndTurnForHand = false;
    private Action onSelectionCancelled;
    public bool IsStealSessionActive => stealSessionActive;
    public bool IsForcedDiscardActive => forcedDiscardActive;
    public bool IsInteractionModeActive => stealSessionActive || forcedDiscardActive;

    private Action<ReprieveCard> onCardSelected;

    public PlayerPawn ViewedPlayer => viewedPlayer;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        RemoveLegacyStealCardUI();
        EnsureRuntimeLayout();
        EnsureHeaderExists();
        EnsureCancelButtonExists();
        handPanel.SetActive(false);
    }

    // =====================
    // NORMAL HAND DISPLAY
    // =====================

    public void ShowHand(PlayerPawn player)
    {
        EnsureHeaderExists();
        viewedPlayer = player;
        ShowHandPanel();
        Refresh();
    }

    public void HideHand()
    {
        if (persistentCombatView && !handPanel.activeSelf)
            return;

        Clear();
        handPanel.SetActive(false);
        RestorePlayerHudAfterHand();
        RestoreEndTurnAfterHand();
        if (SeeHandButtonUI.Instance != null)
            SeeHandButtonUI.Instance.SetHandOverlayMode(false);
        if (!persistentCombatView)
            viewedPlayer = null;
        forcedDiscardActive = false;
        currentHeaderMessage = string.Empty;
        ConfigureCancelButton(false, string.Empty);
    }

    public void ToggleHand(PlayerPawn player)
    {
        if (IsInteractionModeActive)
        {
            Debug.Log("Cannot toggle hand while a multi-step card action is active.");
            return;
        }

        if (persistentCombatView)
            return;

        if (handPanel.activeSelf && viewedPlayer == player)
            HideHand();
        else
            ShowHand(player);
    }

    public bool IsHandOpen()
    {
        return handPanel.activeSelf;
    }

    public void Refresh()
    {
        RemoveLegacyStealCardUI();
        EnsureRuntimeLayout();
        RemoveLegacyStealInstructionTexts();
        Clear();

        if (viewedPlayer == null)
            return;

        UpdateHeader();
        ConfigureCardLayout(viewedPlayer.hand.Count);

        if (emptyHandText != null)
            emptyHandText.gameObject.SetActive(viewedPlayer.hand.Count == 0);

        foreach (var card in viewedPlayer.hand)
        {
            var go = Instantiate(cardPrefab, cardParent);
            ConfigureCardInstance(go, viewedPlayer.hand.Count);
            go.GetComponent<ReprieveCardUI>().Setup(card, viewedPlayer, ShouldUseCompactCards(viewedPlayer.hand.Count));
        }
    }

    private void Clear()
    {
        if (cardParent == null)
            return;

        foreach (Transform child in cardParent)
            Destroy(child.gameObject);
    }

    // =====================
    // PLAYING CARDS
    // =====================

    public void PlayCard(ReprieveCard card)
    {
        if (viewedPlayer == null)
            return;

        if (!card.Play(viewedPlayer))
            return;

        Refresh();
    }

    // =====================
    // STEAL MODE
    // =====================

    public void BeginCardSelection(PlayerPawn target, Action<ReprieveCard> onSelected)
    {
        string prompt = target != null
            ? $"{target.playerName}'s Hand"
            : "Select a card";

        BeginCardSelection(target, prompt, onSelected, false);
    }

    public void BeginCardSelection(PlayerPawn target, string prompt, Action<ReprieveCard> onSelected)
    {
        BeginCardSelection(target, prompt, onSelected, false);
    }

    public void BeginCardSelection(PlayerPawn target, string prompt, Action<ReprieveCard> onSelected, bool allowCancel, string cancelLabel = "Cancel", Action onCancelled = null)
    {
        stealSessionActive = true;
        isStealMode = true;
        forcedDiscardActive = false;

        currentHeaderMessage = prompt;
        onCardSelected = onSelected;
        onSelectionCancelled = onCancelled;
        ConfigureCancelButton(allowCancel, cancelLabel);
        ShowHand(target);
    }

    public void BeginForcedDiscard(PlayerPawn target)
    {
        viewedPlayer = target;
        forcedDiscardActive = true;
        stealSessionActive = false;
        isStealMode = false;
        onCardSelected = null;
        currentHeaderMessage = target != null
            ? $"{target.playerName}: discard down to your hand limit"
            : "Discard cards";

        ShowHandPanel();
        Refresh();
    }

    public void EndStealSession()
    {
        stealSessionActive = false;
        forcedDiscardActive = false;
        isStealMode = false;
        onCardSelected = null;
        currentHeaderMessage = string.Empty;
        onSelectionCancelled = null;
        ConfigureCancelButton(false, string.Empty);
        HideHand();
    }

    public void EndForcedDiscard()
    {
        forcedDiscardActive = false;
        isStealMode = false;
        onCardSelected = null;
        currentHeaderMessage = string.Empty;
        onSelectionCancelled = null;
        ConfigureCancelButton(false, string.Empty);
        HideHand();
    }

    public void EnterCombatPresentation(PlayerPawn player)
    {
        persistentCombatView = true;
        viewedPlayer = player;
        currentHeaderMessage = player != null ? $"{player.playerName}'s Reprieves" : "Reprieves";
        if (handPanel != null)
            handPanel.SetActive(false);
        RestorePlayerHudAfterHand();
        RestoreEndTurnAfterHand();
        if (SeeHandButtonUI.Instance != null)
            SeeHandButtonUI.Instance.SetHandOverlayMode(false);
    }

    public void ExitCombatPresentation()
    {
        if (!persistentCombatView)
            return;

        persistentCombatView = false;
        currentHeaderMessage = string.Empty;
        viewedPlayer = null;
        if (handPanel != null)
            handPanel.SetActive(false);
        RestorePlayerHudAfterHand();
        RestoreEndTurnAfterHand();
        if (SeeHandButtonUI.Instance != null)
            SeeHandButtonUI.Instance.SetHandOverlayMode(false);
    }

    public bool OnCardClicked(ReprieveCard card)
    {
        if (forcedDiscardActive)
        {
            if (viewedPlayer == null || card == null)
                return true;

            viewedPlayer.hand.Remove(card);
            ReprieveDeck.Instance.Discard(card);
            Refresh();
            GameManager.Instance?.OnForcedDiscardCardChosen(viewedPlayer);
            return true;
        }

        if (!isStealMode)
            return false;

        onCardSelected?.Invoke(card);

        stealSessionActive = false;
        isStealMode = false;
        onCardSelected = null;
        onSelectionCancelled = null;
        ConfigureCancelButton(false, string.Empty);
        HideHand();
        return true;
    }

    private void EnsureHeaderExists()
    {
        EnsureRuntimeLayout();
        if (handPanel == null || headerText != null)
            return;

        Transform headerParent = frameRect != null ? frameRect : handPanel.transform as RectTransform;
        Transform existingHeader = headerParent != null ? headerParent.Find("HeaderText") : null;
        if (existingHeader != null)
        {
            headerText = existingHeader.GetComponent<TMP_Text>();
            if (headerText != null)
                return;
        }

        GameObject headerObject = new GameObject("HeaderText", typeof(RectTransform), typeof(TextMeshProUGUI));
        headerObject.transform.SetParent(headerParent != null ? headerParent : handPanel.transform, false);

        RectTransform rect = headerObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.06f, 0.84f);
        rect.anchorMax = new Vector2(0.94f, 0.97f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        headerText = headerObject.GetComponent<TextMeshProUGUI>();
        headerText.fontSize = 34f;
        headerText.fontSizeMax = 34f;
        headerText.fontSizeMin = 18f;
        headerText.enableAutoSizing = true;
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.textWrappingMode = TextWrappingModes.Normal;
        headerText.color = new Color(0.96f, 0.90f, 0.78f, 1f);
    }

    private void ShowHandPanel()
    {
        if (handPanel == null)
            return;

        RemoveLegacyStealCardUI();
        RemoveLegacyStealInstructionTexts();
        handPanel.SetActive(true);
        handPanel.transform.SetAsLastSibling();
        HideEndTurnForHand();
        HidePlayerHudForHand();

        if (SeeHandButtonUI.Instance != null && !IsInteractionModeActive)
            SeeHandButtonUI.Instance.SetHandOverlayMode(true);
    }

    private void HidePlayerHudForHand()
    {
        if (GameManager.Instance == null || GameManager.Instance.playerHUD == null)
            return;

        GameObject hudObject = GameManager.Instance.playerHUD.gameObject;
        if (!hudObject.activeSelf)
            return;

        hudObject.SetActive(false);
        hidPlayerHudForHand = true;
    }

    private void RestorePlayerHudAfterHand()
    {
        if (!hidPlayerHudForHand)
            return;

        hidPlayerHudForHand = false;

        if (GameManager.Instance == null || GameManager.Instance.playerHUD == null)
            return;

        GameManager.Instance.playerHUD.gameObject.SetActive(true);
    }

    private void HideEndTurnForHand()
    {
        if (GameManager.Instance == null || GameManager.Instance.endTurnButtonUI == null)
            return;

        GameObject endTurnObject = GameManager.Instance.endTurnButtonUI.gameObject;
        if (!endTurnObject.activeSelf)
            return;

        endTurnObject.SetActive(false);
        hidEndTurnForHand = true;
    }

    private void RestoreEndTurnAfterHand()
    {
        if (!hidEndTurnForHand)
            return;

        hidEndTurnForHand = false;

        if (GameManager.Instance == null || GameManager.Instance.endTurnButtonUI == null)
            return;

        GameManager.Instance.endTurnButtonUI.gameObject.SetActive(true);
    }

    private void EnsureCancelButtonExists()
    {
        EnsureRuntimeLayout();
        if (handPanel == null || cancelButton != null)
            return;

        Transform cancelParent = frameRect != null ? frameRect : handPanel.transform as RectTransform;
        GameObject cancelObject = new GameObject("RuntimeCancelButton", typeof(RectTransform), typeof(Image), typeof(Button));
        cancelObject.transform.SetParent(cancelParent != null ? cancelParent : handPanel.transform, false);

        RectTransform rect = cancelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(220f, 54f);
        rect.anchoredPosition = new Vector2(-28f, -28f);

        Image image = cancelObject.GetComponent<Image>();
        image.color = new Color(0.24f, 0.10f, 0.10f, 0.96f);

        cancelButton = cancelObject.GetComponent<Button>();
        cancelButton.onClick.AddListener(CancelSelection);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(cancelObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        cancelButtonText = labelObject.GetComponent<TextMeshProUGUI>();
        cancelButtonText.fontSize = 20f;
        cancelButtonText.color = Color.white;
        cancelButtonText.alignment = TextAlignmentOptions.Center;
        cancelButtonText.text = "Cancel";

        BoardActionButtonStyle.Attach(cancelButton, BoardActionButtonStyle.Variant.Danger);
        ConfigureCancelButton(false, string.Empty);
    }

    private void EnsureRuntimeLayout()
    {
        if (handPanel == null)
            return;

        handPanelRect = handPanel.GetComponent<RectTransform>();
        if (handPanelRect != null)
        {
            handPanelRect.anchorMin = Vector2.zero;
            handPanelRect.anchorMax = Vector2.one;
            handPanelRect.offsetMin = Vector2.zero;
            handPanelRect.offsetMax = Vector2.zero;
        }

        Image dimImage = handPanel.GetComponent<Image>();
        if (dimImage == null)
            dimImage = handPanel.AddComponent<Image>();

        dimImage.color = new Color(0.05f, 0.055f, 0.06f, 0.82f);
        dimImage.raycastTarget = true;

        frameRect = EnsureChildRect(handPanel.transform, "HandContentFrame");
        frameRect.anchorMin = new Vector2(0.06f, 0.14f);
        frameRect.anchorMax = new Vector2(0.94f, 0.88f);
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;

        Image frameImage = frameRect.GetComponent<Image>();
        if (frameImage == null)
            frameImage = frameRect.gameObject.AddComponent<Image>();
        frameImage.color = new Color(0.08f, 0.065f, 0.055f, 0.96f);
        frameImage.raycastTarget = true;

        RectTransform accentRect = EnsureChildRect(frameRect, "FrameAccent");
        accentRect.anchorMin = new Vector2(0f, 0.985f);
        accentRect.anchorMax = Vector2.one;
        accentRect.offsetMin = Vector2.zero;
        accentRect.offsetMax = Vector2.zero;

        Image accentImage = accentRect.GetComponent<Image>();
        if (accentImage == null)
            accentImage = accentRect.gameObject.AddComponent<Image>();
        accentImage.color = new Color(0.82f, 0.36f, 0.12f, 1f);
        accentImage.raycastTarget = false;

        cardViewportRect = EnsureChildRect(frameRect, "CardViewport");
        cardViewportRect.anchorMin = new Vector2(0.04f, 0.08f);
        cardViewportRect.anchorMax = new Vector2(0.96f, 0.78f);
        cardViewportRect.offsetMin = Vector2.zero;
        cardViewportRect.offsetMax = Vector2.zero;

        Image viewportImage = cardViewportRect.GetComponent<Image>();
        if (viewportImage == null)
            viewportImage = cardViewportRect.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(0.05f, 0.04f, 0.035f, 0.22f);
        viewportImage.raycastTarget = true;

        Mask viewportMask = cardViewportRect.GetComponent<Mask>();
        if (viewportMask == null)
            viewportMask = cardViewportRect.gameObject.AddComponent<Mask>();
        viewportMask.showMaskGraphic = true;

        cardScrollRect = cardViewportRect.GetComponent<ScrollRect>();
        if (cardScrollRect == null)
            cardScrollRect = cardViewportRect.gameObject.AddComponent<ScrollRect>();
        cardScrollRect.horizontal = true;
        cardScrollRect.vertical = false;
        cardScrollRect.movementType = ScrollRect.MovementType.Elastic;
        cardScrollRect.inertia = true;
        cardScrollRect.scrollSensitivity = 28f;
        cardScrollRect.viewport = cardViewportRect;

        runtimeCardContainer = EnsureChildRect(cardViewportRect, "CardDock");
        runtimeCardContainer.anchorMin = new Vector2(0f, 0f);
        runtimeCardContainer.anchorMax = new Vector2(0f, 1f);
        runtimeCardContainer.pivot = new Vector2(0f, 0.5f);
        runtimeCardContainer.anchoredPosition = Vector2.zero;
        runtimeCardContainer.offsetMin = Vector2.zero;
        runtimeCardContainer.offsetMax = new Vector2(0f, 0f);
        cardParent = runtimeCardContainer;
        cardScrollRect.content = runtimeCardContainer;

        cardLayout = runtimeCardContainer.GetComponent<HorizontalLayoutGroup>();
        if (cardLayout == null)
            cardLayout = runtimeCardContainer.gameObject.AddComponent<HorizontalLayoutGroup>();

        cardLayout.childAlignment = TextAnchor.MiddleCenter;
        cardLayout.childControlWidth = false;
        cardLayout.childControlHeight = false;
        cardLayout.childForceExpandWidth = false;
        cardLayout.childForceExpandHeight = false;

        cardContentFitter = runtimeCardContainer.GetComponent<ContentSizeFitter>();
        if (cardContentFitter == null)
            cardContentFitter = runtimeCardContainer.gameObject.AddComponent<ContentSizeFitter>();
        cardContentFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        cardContentFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        EnsureEmptyHandText();
    }

    private void RemoveLegacyStealInstructionTexts()
    {
        foreach (Transform child in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (child == null || child.name != "StealCardInstructionsText")
                continue;

            TMP_Text legacyText = child.GetComponent<TMP_Text>();
            if (legacyText != null)
                legacyText.text = string.Empty;

            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }

    private void RemoveLegacyStealCardUI()
    {
        foreach (StealCardUI legacyUi in FindObjectsByType<StealCardUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (legacyUi == null)
                continue;

            TMP_Text legacyText = legacyUi.GetComponentInChildren<TMP_Text>(true);
            if (legacyText != null)
                legacyText.text = string.Empty;

            legacyUi.gameObject.SetActive(false);
            Destroy(legacyUi.gameObject);
        }

        foreach (Transform child in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (child == null || child.name != "StealCardUI")
                continue;

            TMP_Text legacyText = child.GetComponentInChildren<TMP_Text>(true);
            if (legacyText != null)
                legacyText.text = string.Empty;

            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }

    private RectTransform EnsureChildRect(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return existing.GetComponent<RectTransform>();

        GameObject child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child.GetComponent<RectTransform>();
    }

    private void EnsureEmptyHandText()
    {
        if (frameRect == null || emptyHandText != null)
            return;

        GameObject emptyObject = new GameObject("EmptyHandText", typeof(RectTransform), typeof(TextMeshProUGUI));
        emptyObject.transform.SetParent(frameRect, false);

        RectTransform rect = emptyObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.04f, 0.08f);
        rect.anchorMax = new Vector2(0.96f, 0.78f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        emptyHandText = emptyObject.GetComponent<TextMeshProUGUI>();
        emptyHandText.text = "No reprieve cards in hand.";
        emptyHandText.fontSize = 30f;
        emptyHandText.color = new Color(0.90f, 0.84f, 0.72f, 0.88f);
        emptyHandText.alignment = TextAlignmentOptions.Center;
        emptyHandText.textWrappingMode = TextWrappingModes.Normal;
        emptyHandText.raycastTarget = false;
        emptyHandText.gameObject.SetActive(false);
    }

    private void ConfigureCardLayout(int cardCount)
    {
        if (cardLayout == null)
            return;

        cardLayout.spacing = cardCount <= 4 ? 28f : cardCount <= 6 ? 18f : 10f;
        cardLayout.padding = new RectOffset(18, 18, 12, 12);

        Vector2 cardSize = GetHandCardSize(cardCount);
        float totalCardWidth = cardCount <= 0
            ? 0f
            : cardCount * cardSize.x + Mathf.Max(0, cardCount - 1) * cardLayout.spacing + cardLayout.padding.left + cardLayout.padding.right;
        float viewportWidth = cardViewportRect != null && cardViewportRect.rect.width > 0f
            ? cardViewportRect.rect.width
            : Mathf.Max(800f, Screen.width * 0.82f);
        bool shouldScroll = totalCardWidth > viewportWidth;

        cardLayout.childAlignment = shouldScroll ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;

        if (runtimeCardContainer != null)
            runtimeCardContainer.sizeDelta = new Vector2(Mathf.Max(viewportWidth, totalCardWidth), 0f);

        if (cardScrollRect != null)
        {
            cardScrollRect.enabled = shouldScroll;
            cardScrollRect.horizontalNormalizedPosition = 0f;
        }
    }

    private void ConfigureCardInstance(GameObject cardObject, int cardCount)
    {
        if (cardObject == null)
            return;

        Vector2 size = GetHandCardSize(cardCount);
        RectTransform rect = cardObject.GetComponent<RectTransform>();
        if (rect != null)
            rect.sizeDelta = size;

        LayoutElement layout = cardObject.GetComponent<LayoutElement>();
        if (layout == null)
            layout = cardObject.AddComponent<LayoutElement>();

        layout.preferredWidth = size.x;
        layout.preferredHeight = size.y;
        layout.flexibleWidth = 0f;
        layout.flexibleHeight = 0f;
    }

    private Vector2 GetHandCardSize(int cardCount)
    {
        if (cardCount <= 3)
            return new Vector2(300f, 470f);
        if (cardCount <= 5)
            return new Vector2(250f, 420f);
        if (cardCount <= 7)
            return new Vector2(205f, 360f);

        return new Vector2(175f, 320f);
    }

    private bool ShouldUseCompactCards(int cardCount)
    {
        return cardCount >= 6;
    }

    private void ConfigureCancelButton(bool visible, string label)
    {
        EnsureCancelButtonExists();
        if (cancelButton == null)
            return;

        cancelButton.gameObject.SetActive(visible);
        if (cancelButtonText != null)
            cancelButtonText.text = string.IsNullOrWhiteSpace(label) ? "Cancel" : label;
    }

    private void CancelSelection()
    {
        if (!stealSessionActive)
            return;

        Action<ReprieveCard> cardCallback = onCardSelected;
        Action cancelCallback = onSelectionCancelled;

        stealSessionActive = false;
        isStealMode = false;
        onCardSelected = null;
        onSelectionCancelled = null;
        ConfigureCancelButton(false, string.Empty);
        HideHand();

        cancelCallback?.Invoke();
        cardCallback?.Invoke(null);
    }

    private void UpdateHeader()
    {
        EnsureHeaderExists();
        if (headerText == null)
            return;

        if (!string.IsNullOrWhiteSpace(currentHeaderMessage))
        {
            headerText.text = currentHeaderMessage;
            return;
        }

        headerText.text = viewedPlayer != null
            ? $"{viewedPlayer.playerName}'s Hand"
            : string.Empty;
    }

}
