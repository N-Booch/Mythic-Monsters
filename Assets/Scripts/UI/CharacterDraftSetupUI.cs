using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterDraftSetupUI : MonoBehaviour
{
    public static CharacterDraftSetupUI Instance;

    private GameObject panel;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI phaseText;
    private TextMeshProUGUI detailText;
    private RectTransform slotRoot;
    private RectTransform cardRoot;
    private Button primaryButton;
    private TextMeshProUGUI primaryButtonLabel;

    private readonly List<TextMeshProUGUI> slotLabels = new List<TextMeshProUGUI>();
    private readonly List<Button> cardButtons = new List<Button>();
    private readonly List<TextMeshProUGUI> cardTitles = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> cardDetails = new List<TextMeshProUGUI>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        EnsureRuntimeUI();
        Hide();
    }

    public static CharacterDraftSetupUI EnsureExists()
    {
        if (Instance != null)
            return Instance;

        CharacterDraftSetupUI existing = FindFirstObjectByType<CharacterDraftSetupUI>();
        if (existing != null)
        {
            Instance = existing;
            existing.EnsureRuntimeUI();
            existing.Hide();
            return existing;
        }

        GameObject uiObject = new GameObject("CharacterDraftSetupUI");
        CharacterDraftSetupUI ui = uiObject.AddComponent<CharacterDraftSetupUI>();
        ui.EnsureRuntimeUI();
        ui.Hide();
        return ui;
    }

    public void Show()
    {
        EnsureRuntimeUI();
        panel.SetActive(true);
    }

    public void Hide()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    public void SetHeading(string title, string phase, string detail)
    {
        EnsureRuntimeUI();
        titleText.text = title;
        phaseText.text = phase;
        detailText.text = detail;
    }

    public void SetPlayerSlots(IReadOnlyList<string> slotSummaries)
    {
        EnsureRuntimeUI();

        for (int i = 0; i < slotLabels.Count; i++)
        {
            string summary = (slotSummaries != null && i < slotSummaries.Count)
                ? slotSummaries[i]
                : string.Empty;

            slotLabels[i].text = summary;
        }
    }

    public void SetCards(IReadOnlyList<string> titles, IReadOnlyList<string> details, IReadOnlyList<bool> selectable, Action<int> onSelect)
    {
        EnsureRuntimeUI();

        int count = titles != null ? titles.Count : 0;
        EnsureCardCount(Mathf.Max(4, count));

        for (int i = 0; i < cardButtons.Count; i++)
        {
            bool visible = i < count;
            cardButtons[i].gameObject.SetActive(visible);
            if (!visible)
                continue;

            cardTitles[i].text = titles[i];
            cardDetails[i].text = details != null && i < details.Count ? details[i] : string.Empty;

            bool canSelect = selectable != null && i < selectable.Count && selectable[i];
            cardButtons[i].interactable = canSelect;

            Image background = cardButtons[i].GetComponent<Image>();
            background.color = canSelect
                ? new Color(0.34f, 0.20f, 0.15f, 0.97f)
                : new Color(0.22f, 0.16f, 0.14f, 0.95f);

            int capturedIndex = i;
            cardButtons[i].onClick.RemoveAllListeners();
            if (canSelect && onSelect != null)
                cardButtons[i].onClick.AddListener(() => onSelect(capturedIndex));
        }
    }

    public void SetPrimaryAction(string label, Action onClick, bool visible)
    {
        EnsureRuntimeUI();

        primaryButton.gameObject.SetActive(visible);
        if (!visible)
            return;

        primaryButtonLabel.text = label;
        primaryButton.onClick.RemoveAllListeners();
        if (onClick != null)
            primaryButton.onClick.AddListener(() => onClick());
    }

    private void EnsureRuntimeUI()
    {
        if (panel != null)
            return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("RuntimeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        panel = CreatePanel(canvas.transform, "CharacterDraftPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1320f, 960f), new Vector2(0f, -8f), new Color(0.10f, 0.06f, 0.05f, 0.96f));
        RectTransform panelRect = panel.GetComponent<RectTransform>();

        CreateRule(panel.transform, "TopRule", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(1100f, 8f));

        titleText = CreateText(panel.transform, "Title", "Match Setup", 54f, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(960f, 72f), Color.white);
        phaseText = CreateText(panel.transform, "Phase", string.Empty, 32f, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -154f), new Vector2(1020f, 42f), new Color(0.96f, 0.91f, 0.84f, 1f));
        detailText = CreateText(panel.transform, "Detail", string.Empty, 22f, FontStyles.Normal, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -212f), new Vector2(1080f, 72f), new Color(0.88f, 0.83f, 0.75f, 1f));

        GameObject slotsPanel = CreatePanel(panel.transform, "SlotsPanel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1120f, 248f), new Vector2(0f, -372f), new Color(0.20f, 0.11f, 0.09f, 0.94f));
        CreateRule(slotsPanel.transform, "SlotsRule", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(960f, 6f));
        CreateText(slotsPanel.transform, "SlotsHeader", "Player Seats", 28f, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(860f, 36f), new Color(0.95f, 0.88f, 0.80f, 1f));

        GameObject slotRootObject = new GameObject("SlotRoot", typeof(RectTransform));
        slotRootObject.transform.SetParent(slotsPanel.transform, false);
        slotRoot = slotRootObject.GetComponent<RectTransform>();
        slotRoot.anchorMin = new Vector2(0.5f, 0.5f);
        slotRoot.anchorMax = new Vector2(0.5f, 0.5f);
        slotRoot.pivot = new Vector2(0.5f, 0.5f);
        slotRoot.sizeDelta = new Vector2(980f, 156f);
        slotRoot.anchoredPosition = new Vector2(0f, -24f);

        for (int i = 0; i < 4; i++)
        {
            int row = i / 2;
            int column = i % 2;
            Vector2 anchoredPosition = new Vector2(column == 0 ? -248f : 248f, row == 0 ? 44f : -44f);

            GameObject slotCard = CreatePanel(slotRoot, $"Seat_{i + 1}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(448f, 84f), anchoredPosition, new Color(0.27f, 0.17f, 0.12f, 0.96f));
            TextMeshProUGUI slotText = CreateText(slotCard.transform, "Label", $"Seat {i + 1}", 21f, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(408f, 70f), Color.white);
            slotLabels.Add(slotText);
        }

        GameObject cardsPanel = CreatePanel(panel.transform, "CardsPanel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1120f, 286f), new Vector2(0f, -654f), new Color(0.20f, 0.11f, 0.09f, 0.94f));
        CreateRule(cardsPanel.transform, "CardsRule", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(960f, 6f));
        CreateText(cardsPanel.transform, "CardsHeader", "Face-Down Character Cards", 28f, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(900f, 36f), new Color(0.95f, 0.88f, 0.80f, 1f));

        GameObject cardRootObject = new GameObject("CardRoot", typeof(RectTransform));
        cardRootObject.transform.SetParent(cardsPanel.transform, false);
        cardRoot = cardRootObject.GetComponent<RectTransform>();
        cardRoot.anchorMin = new Vector2(0.5f, 0.5f);
        cardRoot.anchorMax = new Vector2(0.5f, 0.5f);
        cardRoot.pivot = new Vector2(0.5f, 0.5f);
        cardRoot.sizeDelta = new Vector2(988f, 174f);
        cardRoot.anchoredPosition = new Vector2(0f, -26f);

        EnsureCardCount(4);

        GameObject primaryButtonObject = CreateButton(panel.transform, "PrimaryAction", out primaryButtonLabel, "Continue", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -878f), new Vector2(320f, 68f));
        primaryButton = primaryButtonObject.GetComponent<Button>();
    }

    private void EnsureCardCount(int count)
    {
        while (cardButtons.Count < count)
        {
            int index = cardButtons.Count;
            Vector2 anchoredPosition = new Vector2(-372f + (248f * index), 0f);
            GameObject cardObject = CreateButton(cardRoot, $"Card_{index + 1}", out TextMeshProUGUI buttonText, "Face-Down Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, new Vector2(212f, 168f));
            Button button = cardObject.GetComponent<Button>();
            Image image = cardObject.GetComponent<Image>();
            image.color = new Color(0.22f, 0.16f, 0.14f, 0.95f);

            buttonText.fontSize = 24f;
            buttonText.fontStyle = FontStyles.Bold;

            TextMeshProUGUI detail = CreateText(cardObject.transform, "Detail", string.Empty, 16f, FontStyles.Normal, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(176f, 78f), new Color(0.90f, 0.85f, 0.79f, 1f));
            detail.textWrappingMode = TextWrappingModes.Normal;

            cardButtons.Add(button);
            cardTitles.Add(buttonText);
            cardDetails.Add(detail);
        }
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 position, Color color)
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Image image = panelObject.GetComponent<Image>();
        image.color = color;
        return panelObject;
    }

    private static void CreateRule(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        GameObject ruleObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        ruleObject.transform.SetParent(parent, false);

        RectTransform rect = ruleObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Image image = ruleObject.GetComponent<Image>();
        image.color = new Color(0.82f, 0.42f, 0.16f, 1f);
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string text, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.alignment = alignment;
        label.color = color;
        label.textWrappingMode = TextWrappingModes.Normal;
        return label;
    }

    private static GameObject CreateButton(Transform parent, string name, out TextMeshProUGUI label, string text, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.34f, 0.20f, 0.15f, 0.97f);

        label = CreateText(buttonObject.transform, "Label", text, 30f, FontStyles.Bold, TextAlignmentOptions.Center, new Vector2(0.5f, 0.68f), new Vector2(0.5f, 0.68f), Vector2.zero, new Vector2(size.x - 20f, 56f), Color.white);
        return buttonObject;
    }
}
