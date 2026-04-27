using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TitleUIManager : MonoBehaviour
{
    public static TitleUIManager Instance;

    public GameObject panel;
    public Transform titleParent;
    public GameObject titleEntryPrefab;
    public TMP_Text headerText;

    private PlayerPawn viewedPlayer;
    private Action<TitleData> onTitleSelected;
    private bool selectionMode;
    private string currentHeaderMessage;

    private GameObject runtimeSelectionPanel;
    private RectTransform runtimeSelectionContent;
    private TextMeshProUGUI runtimeSelectionHeader;
    private Button runtimeCancelButton;
    private TextMeshProUGUI runtimeCancelButtonText;
    private bool runtimeAllowCancel;
    private readonly List<GameObject> spawnedRuntimeEntries = new List<GameObject>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        AutoBindReferences();

        if (panel != null)
            panel.SetActive(false);
    }

    public static TitleUIManager EnsureExists()
    {
        if (Instance != null)
            return Instance;

        TitleUIManager existing = FindFirstObjectByType<TitleUIManager>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject managerObject = new GameObject("TitleUIManager");
        TitleUIManager manager = managerObject.AddComponent<TitleUIManager>();
        manager.AutoBindReferences();
        return manager;
    }

    public void ShowTitles(PlayerPawn player)
    {
        AutoBindReferences();
        selectionMode = false;
        viewedPlayer = player;
        currentHeaderMessage = player != null ? $"{player.playerName}'s Titles" : "Titles";

        HideRuntimeSelectionPanel();

        if (panel != null)
            panel.SetActive(true);

        RefreshStandardPanel();
    }

    public void BeginTitleSelection(PlayerPawn target, Action<TitleData> onSelected)
    {
        string prompt = target != null
            ? $"{target.playerName}: choose a title"
            : "Choose a title";

        BeginTitleSelection(target, prompt, onSelected, false);
    }

    public void BeginTitleSelection(PlayerPawn target, string prompt, Action<TitleData> onSelected)
    {
        BeginTitleSelection(target, prompt, onSelected, false);
    }

    public void BeginTitleSelection(PlayerPawn target, string prompt, Action<TitleData> onSelected, bool allowCancel, string cancelLabel = "Cancel")
    {
        AutoBindReferences();
        selectionMode = true;
        viewedPlayer = target;
        currentHeaderMessage = prompt;
        onTitleSelected = onSelected;
        runtimeAllowCancel = allowCancel;

        if (panel != null)
            panel.SetActive(false);

        ShowRuntimeSelectionPanel();
        if (runtimeCancelButton != null)
        {
            runtimeCancelButton.gameObject.SetActive(allowCancel);
            runtimeCancelButtonText.text = string.IsNullOrWhiteSpace(cancelLabel) ? "Cancel" : cancelLabel;
        }
        RebuildRuntimeSelectionEntries();
    }

    public void OnTitleClicked(TitleData title)
    {
        if (!selectionMode)
            return;

        onTitleSelected?.Invoke(title);
        EndSelection();
    }

    public void EndSelection()
    {
        selectionMode = false;
        onTitleSelected = null;
        currentHeaderMessage = string.Empty;
        HideRuntimeSelectionPanel();

        if (panel != null)
            panel.SetActive(false);
    }

    public void Hide()
    {
        selectionMode = false;
        onTitleSelected = null;
        viewedPlayer = null;
        currentHeaderMessage = string.Empty;
        HideRuntimeSelectionPanel();

        if (panel != null)
            panel.SetActive(false);
    }

    private void AutoBindReferences()
    {
        if (panel == null)
        {
            GameObject panelObject = GameObject.Find("TitlesPanel");
            if (panelObject != null)
                panel = panelObject;
        }

        if (titleParent == null && panel != null)
            titleParent = panel.transform;

        EnsureHeaderExists();
        EnsureRuntimeSelectionUI();
    }

    private void RefreshStandardPanel()
    {
        EnsureHeaderExists();
        if (titleParent == null || viewedPlayer == null)
            return;

        if (headerText != null)
            headerText.text = currentHeaderMessage;

        foreach (Transform child in titleParent)
        {
            if (headerText != null && child == headerText.transform)
                continue;

            Destroy(child.gameObject);
        }

        foreach (TitleData title in viewedPlayer.titles)
        {
            if (titleEntryPrefab != null)
            {
                GameObject entry = Instantiate(titleEntryPrefab, titleParent);
                entry.GetComponent<TitleUIEntry>().Setup(title, this);
            }
            else
            {
                CreateSimpleRuntimeEntry(title);
            }
        }
    }

    private void EnsureHeaderExists()
    {
        if (panel == null || headerText != null)
            return;

        Transform existingHeader = panel.transform.Find("HeaderText");
        if (existingHeader != null)
        {
            headerText = existingHeader.GetComponent<TMP_Text>();
            if (headerText != null)
                return;
        }

        GameObject headerObject = new GameObject("HeaderText", typeof(RectTransform), typeof(TextMeshProUGUI));
        headerObject.transform.SetParent(panel.transform, false);

        RectTransform headerRect = headerObject.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0.5f, 1f);
        headerRect.anchorMax = new Vector2(0.5f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.sizeDelta = new Vector2(900f, 80f);
        headerRect.anchoredPosition = new Vector2(0f, -18f);

        headerText = headerObject.GetComponent<TextMeshProUGUI>();
        headerText.fontSize = 28f;
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.textWrappingMode = TextWrappingModes.Normal;
        headerText.color = Color.white;
    }

    private void CreateSimpleRuntimeEntry(TitleData title)
    {
        GameObject entry = new GameObject($"Title_{title.titleName}", typeof(RectTransform), typeof(Image), typeof(Button));
        entry.transform.SetParent(titleParent, false);

        Image background = entry.GetComponent<Image>();
        background.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);

        Button button = entry.GetComponent<Button>();
        button.onClick.AddListener(() => OnTitleClicked(title));

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(entry.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 4f);
        textRect.offsetMax = new Vector2(-8f, -4f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = title.titleName;
        text.fontSize = 24f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
    }

    private void EnsureRuntimeSelectionUI()
    {
        if (runtimeSelectionPanel != null)
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

        runtimeSelectionPanel = new GameObject("TitleSelectionPanel", typeof(RectTransform), typeof(Image));
        runtimeSelectionPanel.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = runtimeSelectionPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(820f, 640f);
        panelRect.anchoredPosition = Vector2.zero;

        Image panelImage = runtimeSelectionPanel.GetComponent<Image>();
        panelImage.color = new Color(0.09f, 0.08f, 0.11f, 0.96f);

        GameObject headerObject = new GameObject("Header", typeof(RectTransform), typeof(TextMeshProUGUI));
        headerObject.transform.SetParent(runtimeSelectionPanel.transform, false);
        RectTransform headerRect = headerObject.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.sizeDelta = new Vector2(0f, 72f);
        headerRect.anchoredPosition = new Vector2(0f, -16f);

        runtimeSelectionHeader = headerObject.GetComponent<TextMeshProUGUI>();
        runtimeSelectionHeader.fontSize = 28f;
        runtimeSelectionHeader.color = Color.white;
        runtimeSelectionHeader.alignment = TextAlignmentOptions.Center;
        runtimeSelectionHeader.textWrappingMode = TextWrappingModes.Normal;

        GameObject contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.transform.SetParent(runtimeSelectionPanel.transform, false);
        runtimeSelectionContent = contentObject.GetComponent<RectTransform>();
        runtimeSelectionContent.anchorMin = new Vector2(0.5f, 1f);
        runtimeSelectionContent.anchorMax = new Vector2(0.5f, 1f);
        runtimeSelectionContent.pivot = new Vector2(0.5f, 1f);
        runtimeSelectionContent.sizeDelta = new Vector2(740f, 430f);
        runtimeSelectionContent.anchoredPosition = new Vector2(0f, -96f);

        GameObject cancelObject = new GameObject("CancelButton", typeof(RectTransform), typeof(Image), typeof(Button));
        cancelObject.transform.SetParent(runtimeSelectionPanel.transform, false);

        RectTransform cancelRect = cancelObject.GetComponent<RectTransform>();
        cancelRect.anchorMin = new Vector2(0.5f, 0f);
        cancelRect.anchorMax = new Vector2(0.5f, 0f);
        cancelRect.pivot = new Vector2(0.5f, 0f);
        cancelRect.sizeDelta = new Vector2(280f, 58f);
        cancelRect.anchoredPosition = new Vector2(0f, 18f);

        Image cancelImage = cancelObject.GetComponent<Image>();
        cancelImage.color = new Color(0.22f, 0.12f, 0.12f, 0.96f);

        runtimeCancelButton = cancelObject.GetComponent<Button>();
        runtimeCancelButton.onClick.AddListener(() => SelectTitle(null));

        GameObject cancelLabelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        cancelLabelObject.transform.SetParent(cancelObject.transform, false);

        RectTransform cancelLabelRect = cancelLabelObject.GetComponent<RectTransform>();
        cancelLabelRect.anchorMin = Vector2.zero;
        cancelLabelRect.anchorMax = Vector2.one;
        cancelLabelRect.offsetMin = Vector2.zero;
        cancelLabelRect.offsetMax = Vector2.zero;

        runtimeCancelButtonText = cancelLabelObject.GetComponent<TextMeshProUGUI>();
        runtimeCancelButtonText.fontSize = 22f;
        runtimeCancelButtonText.color = Color.white;
        runtimeCancelButtonText.alignment = TextAlignmentOptions.Center;
        runtimeCancelButtonText.text = "Cancel";

        runtimeSelectionPanel.SetActive(false);
    }

    private void ShowRuntimeSelectionPanel()
    {
        EnsureRuntimeSelectionUI();
        runtimeSelectionHeader.text = currentHeaderMessage;
        runtimeSelectionPanel.SetActive(true);
    }

    private void HideRuntimeSelectionPanel()
    {
        if (runtimeSelectionPanel != null)
            runtimeSelectionPanel.SetActive(false);

        runtimeAllowCancel = false;
    }

    private void RebuildRuntimeSelectionEntries()
    {
        foreach (GameObject entry in spawnedRuntimeEntries)
        {
            if (entry != null)
                Destroy(entry);
        }

        spawnedRuntimeEntries.Clear();

        if (viewedPlayer == null)
            return;

        int displayIndex = 0;
        foreach (TitleData title in viewedPlayer.titles)
        {
            if (title == null)
                continue;

            spawnedRuntimeEntries.Add(CreateRuntimeSelectionEntry(title, displayIndex));
            displayIndex++;
        }
    }

    private GameObject CreateRuntimeSelectionEntry(TitleData title, int index)
    {
        GameObject entryObject = new GameObject($"TitleEntry_{index}", typeof(RectTransform), typeof(Image), typeof(Button));
        entryObject.transform.SetParent(runtimeSelectionContent, false);

        RectTransform entryRect = entryObject.GetComponent<RectTransform>();
        entryRect.anchorMin = new Vector2(0.5f, 1f);
        entryRect.anchorMax = new Vector2(0.5f, 1f);
        entryRect.pivot = new Vector2(0.5f, 1f);
        entryRect.sizeDelta = new Vector2(720f, 92f);
        entryRect.anchoredPosition = new Vector2(0f, -100f * index);

        Image background = entryObject.GetComponent<Image>();
        background.color = new Color(0.18f, 0.16f, 0.10f, 0.95f);

        Button button = entryObject.GetComponent<Button>();
        button.onClick.AddListener(() => SelectTitle(title));

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(entryObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(14f, 8f);
        labelRect.offsetMax = new Vector2(-14f, -8f);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.fontSize = 21f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Left;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.text = $"{title.titleName}\n{title.description}";

        return entryObject;
    }

    private void SelectTitle(TitleData title)
    {
        if (title == null && !runtimeAllowCancel)
            return;

        Action<TitleData> callback = onTitleSelected;
        EndSelection();
        callback?.Invoke(title);
    }
}
