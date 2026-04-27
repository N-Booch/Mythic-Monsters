using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TreasureSelectionUI : MonoBehaviour
{
    public static TreasureSelectionUI Instance;

    private GameObject panel;
    private Canvas uiCanvas;
    private RectTransform contentRoot;
    private TextMeshProUGUI headerText;
    private TextMeshProUGUI previewText;
    private GameObject previewCardObject;
    private Button declineButton;
    private TextMeshProUGUI declineButtonText;
    private Action<TreasureCard> onTreasureSelected;
    private readonly List<GameObject> spawnedEntries = new List<GameObject>();

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

    public static TreasureSelectionUI EnsureExists()
    {
        if (Instance != null)
            return Instance;

        TreasureSelectionUI existing = FindFirstObjectByType<TreasureSelectionUI>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject managerObject = new GameObject("TreasureSelectionUI");
        TreasureSelectionUI manager = managerObject.AddComponent<TreasureSelectionUI>();
        manager.EnsureRuntimeUI();
        manager.Hide();
        return manager;
    }

    public void BeginSelection(PlayerPawn player, string prompt, Action<TreasureCard> onSelected)
    {
        BeginSelection(player != null ? player.equippedTreasures : null, prompt, onSelected);
    }

    public void BeginSelection(
        PlayerPawn player,
        string prompt,
        Action<TreasureCard> onSelected,
        TreasureCard incomingTreasure,
        bool allowDecline,
        string declineLabel = "Keep Current Treasures")
    {
        BeginSelection(
            player != null ? player.equippedTreasures : null,
            prompt,
            onSelected,
            incomingTreasure,
            allowDecline,
            declineLabel);
    }

    public void BeginSelection(List<TreasureCard> treasures, string prompt, Action<TreasureCard> onSelected)
    {
        BeginSelection(treasures, prompt, onSelected, null, false);
    }

    public void BeginSelection(
        List<TreasureCard> treasures,
        string prompt,
        Action<TreasureCard> onSelected,
        TreasureCard incomingTreasure,
        bool allowDecline,
        string declineLabel = "Keep Current Treasures")
    {
        EnsureRuntimeUI();

        onTreasureSelected = onSelected;
        headerText.text = prompt;
        UpdatePreview(incomingTreasure);
        ConfigureDeclineButton(allowDecline, declineLabel);
        RebuildEntries(treasures);
        panel.SetActive(true);
    }

    public void Hide()
    {
        if (panel != null)
            panel.SetActive(false);

        onTreasureSelected = null;
    }

    private void EnsureRuntimeUI()
    {
        if (panel != null)
            return;

        GameObject canvasObject = new GameObject("TreasureSelectionCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        uiCanvas = canvasObject.GetComponent<Canvas>();
        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        uiCanvas.sortingOrder = 200;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        panel = new GameObject("TreasureSelectionPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(uiCanvas.transform, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(1040f, 820f);
        panelRect.anchoredPosition = Vector2.zero;

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.09f, 0.08f, 0.11f, 0.96f);

        GameObject headerObject = new GameObject("Header", typeof(RectTransform), typeof(TextMeshProUGUI));
        headerObject.transform.SetParent(panel.transform, false);
        RectTransform headerRect = headerObject.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.sizeDelta = new Vector2(0f, 72f);
        headerRect.anchoredPosition = new Vector2(0f, -16f);

        headerText = headerObject.GetComponent<TextMeshProUGUI>();
        headerText.fontSize = 28f;
        headerText.color = Color.white;
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.textWrappingMode = TextWrappingModes.Normal;

        GameObject previewObject = new GameObject("Preview", typeof(RectTransform), typeof(TextMeshProUGUI));
        previewObject.transform.SetParent(panel.transform, false);
        RectTransform previewRect = previewObject.GetComponent<RectTransform>();
        previewRect.anchorMin = new Vector2(0.5f, 1f);
        previewRect.anchorMax = new Vector2(0.5f, 1f);
        previewRect.pivot = new Vector2(0.5f, 1f);
        previewRect.sizeDelta = new Vector2(740f, 120f);
        previewRect.anchoredPosition = new Vector2(0f, -94f);

        previewText = previewObject.GetComponent<TextMeshProUGUI>();
        previewText.fontSize = 20f;
        previewText.color = new Color(0.95f, 0.90f, 0.70f, 1f);
        previewText.alignment = TextAlignmentOptions.TopLeft;
        previewText.textWrappingMode = TextWrappingModes.Normal;
        previewText.gameObject.SetActive(false);

        previewCardObject = new GameObject("IncomingTreasurePreview", typeof(RectTransform), typeof(Image));
        previewCardObject.transform.SetParent(panel.transform, false);
        RectTransform previewCardRect = previewCardObject.GetComponent<RectTransform>();
        previewCardRect.anchorMin = new Vector2(0.5f, 1f);
        previewCardRect.anchorMax = new Vector2(0.5f, 1f);
        previewCardRect.pivot = new Vector2(0.5f, 1f);
        previewCardRect.sizeDelta = new Vector2(300f, 230f);
        previewCardRect.anchoredPosition = new Vector2(0f, -86f);
        previewCardObject.GetComponent<Image>().raycastTarget = false;
        previewCardObject.SetActive(false);
        StandardCardVisual.Ensure(previewCardObject.transform);

        GameObject contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.transform.SetParent(panel.transform, false);
        contentRoot = contentObject.GetComponent<RectTransform>();
        contentRoot.anchorMin = new Vector2(0.5f, 1f);
        contentRoot.anchorMax = new Vector2(0.5f, 1f);
        contentRoot.pivot = new Vector2(0.5f, 1f);
        contentRoot.sizeDelta = new Vector2(920f, 420f);
        contentRoot.anchoredPosition = new Vector2(0f, -104f);

        GameObject declineObject = new GameObject("DeclineButton", typeof(RectTransform), typeof(Image), typeof(Button));
        declineObject.transform.SetParent(panel.transform, false);

        RectTransform declineRect = declineObject.GetComponent<RectTransform>();
        declineRect.anchorMin = new Vector2(0.5f, 0f);
        declineRect.anchorMax = new Vector2(0.5f, 0f);
        declineRect.pivot = new Vector2(0.5f, 0f);
        declineRect.sizeDelta = new Vector2(360f, 58f);
        declineRect.anchoredPosition = new Vector2(0f, 18f);

        Image declineImage = declineObject.GetComponent<Image>();
        declineImage.color = new Color(0.24f, 0.10f, 0.10f, 0.96f);

        declineButton = declineObject.GetComponent<Button>();
        declineButton.onClick.AddListener(() => SelectTreasure(null));

        GameObject declineLabelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        declineLabelObject.transform.SetParent(declineObject.transform, false);
        RectTransform declineLabelRect = declineLabelObject.GetComponent<RectTransform>();
        declineLabelRect.anchorMin = Vector2.zero;
        declineLabelRect.anchorMax = Vector2.one;
        declineLabelRect.offsetMin = Vector2.zero;
        declineLabelRect.offsetMax = Vector2.zero;

        declineButtonText = declineLabelObject.GetComponent<TextMeshProUGUI>();
        declineButtonText.fontSize = 20f;
        declineButtonText.color = Color.white;
        declineButtonText.alignment = TextAlignmentOptions.Center;
        declineButtonText.text = "Keep Current Treasures";
    }

    private void RebuildEntries(List<TreasureCard> treasures)
    {
        foreach (GameObject entry in spawnedEntries)
        {
            if (entry != null)
                Destroy(entry);
        }

        spawnedEntries.Clear();

        if (treasures == null)
            return;

        for (int i = 0; i < treasures.Count; i++)
        {
            TreasureCard treasure = treasures[i];
            if (treasure == null)
                continue;

            spawnedEntries.Add(CreateTreasureEntry(treasure, i));
        }
    }

    private GameObject CreateTreasureEntry(TreasureCard treasure, int index)
    {
        GameObject entryObject = new GameObject($"TreasureEntry_{index}", typeof(RectTransform), typeof(Image), typeof(Button));
        entryObject.transform.SetParent(contentRoot, false);

        RectTransform entryRect = entryObject.GetComponent<RectTransform>();
        entryRect.anchorMin = new Vector2(0f, 1f);
        entryRect.anchorMax = new Vector2(0f, 1f);
        entryRect.pivot = new Vector2(0f, 1f);
        entryRect.sizeDelta = new Vector2(280f, 190f);

        const int columns = 3;
        const float horizontalSpacing = 32f;
        const float verticalSpacing = 24f;
        int column = index % columns;
        int row = index / columns;
        entryRect.anchoredPosition = new Vector2(column * (entryRect.sizeDelta.x + horizontalSpacing), -row * (entryRect.sizeDelta.y + verticalSpacing));

        Image background = entryObject.GetComponent<Image>();
        background.color = new Color(0.18f, 0.16f, 0.10f, 0.95f);

        Button button = entryObject.GetComponent<Button>();
        button.onClick.AddListener(() => SelectTreasure(treasure));

        StandardCardVisual visual = StandardCardVisual.Ensure(entryObject.transform, StandardCardVisualDensity.Compact);
        visual?.ConfigureTreasure(treasure.cardName, treasure.description, "Select", true, true);

        return entryObject;
    }

    private void SelectTreasure(TreasureCard treasure)
    {
        Action<TreasureCard> callback = onTreasureSelected;
        Hide();
        callback?.Invoke(treasure);
    }

    private void UpdatePreview(TreasureCard incomingTreasure)
    {
        if (previewText == null)
            return;

        if (incomingTreasure == null)
        {
            if (previewCardObject != null)
                previewCardObject.SetActive(false);

            if (contentRoot != null)
                contentRoot.anchoredPosition = new Vector2(0f, -104f);

            previewText.gameObject.SetActive(false);
            previewText.text = string.Empty;
            return;
        }

        if (previewCardObject != null)
        {
            previewCardObject.SetActive(true);
            StandardCardVisual visual = StandardCardVisual.Ensure(previewCardObject.transform);
            visual?.ConfigureTreasure(incomingTreasure.cardName, incomingTreasure.description, "New Treasure");
        }

        if (contentRoot != null)
            contentRoot.anchoredPosition = new Vector2(0f, -336f);

        previewText.gameObject.SetActive(false);
        previewText.text = string.Empty;
    }

    private void ConfigureDeclineButton(bool allowDecline, string declineLabel)
    {
        if (declineButton == null)
            return;

        declineButton.gameObject.SetActive(allowDecline);
        if (declineButtonText != null)
            declineButtonText.text = string.IsNullOrWhiteSpace(declineLabel)
                ? "Keep Current Treasures"
                : declineLabel;
    }
}
