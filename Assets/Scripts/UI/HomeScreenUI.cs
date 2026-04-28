using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Lightweight runtime home + lobby scaffold.
/// Attach this to an empty GameObject in HomeScene.
/// </summary>
public class HomeScreenUI : MonoBehaviour
{
    [Header("Canvas")]
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private string titleText = "Mythical Monsters";
    [SerializeField] private string subtitleText = "Online-first prototype flow";

    [Header("Palette")]
    [SerializeField] private Color backgroundColor = new Color(0.09f, 0.07f, 0.05f, 1f);
    [SerializeField] private Color panelColor = new Color(0.16f, 0.10f, 0.08f, 0.96f);
    [SerializeField] private Color accentColor = new Color(0.66f, 0.33f, 0.13f, 1f);
    [SerializeField] private Color accentDark = new Color(0.26f, 0.14f, 0.08f, 1f);
    [SerializeField] private Color textColor = new Color(0.96f, 0.91f, 0.84f, 1f);
    [SerializeField] private Color secondaryTextColor = new Color(0.81f, 0.75f, 0.66f, 1f);
    [SerializeField] private Color cardColor = new Color(0.23f, 0.15f, 0.11f, 0.98f);
    [SerializeField] private Color sectionColor = new Color(0.20f, 0.12f, 0.09f, 0.96f);
    [SerializeField] private Color statusPanelColor = new Color(0.20f, 0.12f, 0.09f, 0.92f);

    private RectTransform rootPanel;
    private TextMeshProUGUI statusLabel;
    private TextMeshProUGUI lobbyHeadingLabel;
    private TextMeshProUGUI lobbySubheadingLabel;
    private Button startMatchButton;
    private Button backButton;
    private GameObject homePanel;
    private GameObject lobbyPanel;
    private readonly TextMeshProUGUI[] lobbySlotLabels = new TextMeshProUGUI[4];

    private void Awake()
    {
        GameSessionBootstrap.EnsureExists();
        EnsureCanvas();
        EnsureEventSystem();
        BuildLayout();
        RefreshView();
    }

    private void OnEnable()
    {
        if (GameSessionBootstrap.Instance != null)
            GameSessionBootstrap.Instance.FlowStateChanged += HandleFlowStateChanged;
    }

    private void OnDisable()
    {
        if (GameSessionBootstrap.Instance != null)
            GameSessionBootstrap.Instance.FlowStateChanged -= HandleFlowStateChanged;
    }

    private void EnsureCanvas()
    {
        if (rootCanvas != null)
            return;

        rootCanvas = GetComponentInChildren<Canvas>();
        if (rootCanvas != null)
            return;

        GameObject canvasObject = new GameObject("HomeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        rootCanvas = canvasObject.GetComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystemObject.transform.SetParent(transform, false);
    }

    private void BuildLayout()
    {
        RectTransform canvasRect = rootCanvas.GetComponent<RectTransform>();
        DestroyChildren(canvasRect);

        Image background = CreateImage("Background", canvasRect, backgroundColor);
        Stretch(background.rectTransform, 0f);

        Image panel = CreateImage("Panel", canvasRect, panelColor);
        rootPanel = panel.rectTransform;
        rootPanel.anchorMin = new Vector2(0.5f, 0.5f);
        rootPanel.anchorMax = new Vector2(0.5f, 0.5f);
        rootPanel.sizeDelta = new Vector2(920f, 820f);
        rootPanel.anchoredPosition = Vector2.zero;

        Image topRule = CreateImage("TopRule", rootPanel, accentColor);
        topRule.rectTransform.anchorMin = new Vector2(0f, 1f);
        topRule.rectTransform.anchorMax = new Vector2(1f, 1f);
        topRule.rectTransform.pivot = new Vector2(0.5f, 1f);
        topRule.rectTransform.sizeDelta = new Vector2(-36f, 10f);
        topRule.rectTransform.anchoredPosition = new Vector2(0f, -18f);

        TextMeshProUGUI title = CreateText("Title", rootPanel, titleText, 54f, FontStyles.Bold, TextAlignmentOptions.Center, textColor);
        title.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.sizeDelta = new Vector2(700f, 80f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -50f);

        TextMeshProUGUI subtitle = CreateText("Subtitle", rootPanel, subtitleText, 20f, FontStyles.Normal, TextAlignmentOptions.Center, secondaryTextColor);
        subtitle.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        subtitle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        subtitle.rectTransform.pivot = new Vector2(0.5f, 1f);
        subtitle.rectTransform.sizeDelta = new Vector2(700f, 44f);
        subtitle.rectTransform.anchoredPosition = new Vector2(0f, -108f);

        RectTransform statusPanel = CreateFramedSection(rootPanel, "StatusPanel", null, new Vector2(0.5f, 0f), new Vector2(720f, 80f), new Vector2(0f, 25f), statusPanelColor, 0f);
        statusPanel.pivot = new Vector2(0.5f, 0f);
        statusPanel.anchoredPosition = new Vector2(0f, 25f);
        statusLabel = CreateText("Status", rootPanel, string.Empty, 22f, FontStyles.Italic, TextAlignmentOptions.Center, secondaryTextColor);
        Stretch(statusLabel.rectTransform, 12f);
        statusLabel.rectTransform.SetParent(statusPanel, false);

        BuildHomePanel();
        BuildLobbyPanel();
    }

    private void BuildHomePanel()
    {
        RectTransform homeSection = CreateFramedSection(rootPanel, "HomePanel", "Choose Your Entry", new Vector2(0.5f, 0.54f), new Vector2(650f, 375f), new Vector2(0f, -12f), sectionColor, 18f);
        homePanel = homeSection.gameObject;

        RectTransform buttonColumn = CreateLayoutRoot("HomeButtonColumn", homeSection, new Vector2(0.5f, 0.40f), new Vector2(520f, 244f), new Vector2(0f, -2f));
        VerticalLayoutGroup layout = buttonColumn.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreatePrimaryButton(buttonColumn, "Host Game", () =>
        {
            GameSessionBootstrap.Instance?.BeginHostFlow();
            RefreshView();
        }, 460f, 58f);

        CreatePrimaryButton(buttonColumn, "Join Game", () =>
        {
            GameSessionBootstrap.Instance?.BeginJoinFlow();
            RefreshView();
        }, 460f, 58f);

        CreateSecondaryButton(buttonColumn, "Local Prototype", () =>
        {
            statusLabel.text = "Opening the current board prototype.";
            GameSessionBootstrap.Instance?.BeginLocalPrototypeFlow();
        }, 460f, 58f);

        CreateSecondaryButton(buttonColumn, "Quit", QuitGame, 460f, 58f);
    }

    private void BuildLobbyPanel()
    {
        lobbyPanel = CreateContainer("LobbyPanel", rootPanel, new Vector2(0.5f, 0.45f), new Vector2(780f, 580f), new Vector2(0f, -2f));

        lobbyHeadingLabel = CreateText("LobbyHeading", lobbyPanel.GetComponent<RectTransform>(), "Lobby", 40f, FontStyles.Bold, TextAlignmentOptions.Center, textColor);
        lobbyHeadingLabel.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        lobbyHeadingLabel.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        lobbyHeadingLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
        lobbyHeadingLabel.rectTransform.sizeDelta = new Vector2(660f, 50f);
        lobbyHeadingLabel.rectTransform.anchoredPosition = new Vector2(0f, -2f);

        lobbySubheadingLabel = CreateText("LobbySubheading", lobbyPanel.GetComponent<RectTransform>(), string.Empty, 22f, FontStyles.Normal, TextAlignmentOptions.Center, secondaryTextColor);
        lobbySubheadingLabel.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        lobbySubheadingLabel.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        lobbySubheadingLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
        lobbySubheadingLabel.rectTransform.sizeDelta = new Vector2(690f, 56f);
        lobbySubheadingLabel.rectTransform.anchoredPosition = new Vector2(0f, -48f);

        RectTransform slotSection = CreateFramedSection(lobbyPanel.GetComponent<RectTransform>(), "SlotSection", "Connected Players", new Vector2(0.5f, 0.53f), new Vector2(690f, 248f), new Vector2(0f, 34f), sectionColor, 18f);
        RectTransform slotRoot = CreateLayoutRoot("SlotRoot", slotSection, new Vector2(0.5f, 0.36f), new Vector2(650f, 186f), new Vector2(0f, -8f));
        GridLayoutGroup grid = slotRoot.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(300f, 76f);
        grid.spacing = new Vector2(18f, 18f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.childAlignment = TextAnchor.MiddleCenter;

        for (int i = 0; i < lobbySlotLabels.Length; i++)
        {
            Image slotCard = CreateImage($"Slot{i + 1}", slotRoot, cardColor);
            TextMeshProUGUI slotText = CreateText("Label", slotCard.rectTransform, string.Empty, 22f, FontStyles.Bold, TextAlignmentOptions.Center, textColor);
            Stretch(slotText.rectTransform, 12f);
            lobbySlotLabels[i] = slotText;
        }

        RectTransform actionSection = CreateFramedSection(lobbyPanel.GetComponent<RectTransform>(), "ActionSection", "Match Controls", new Vector2(0.5f, 0f), new Vector2(690f, 156f), new Vector2(0f, 106f), sectionColor, 24f);
        RectTransform actionRow = CreateLayoutRoot("LobbyActionRow", actionSection, new Vector2(0.5f, 0.22f), new Vector2(650f, 82f), new Vector2(0f, -2f));
        HorizontalLayoutGroup actionLayout = actionRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        actionLayout.spacing = 18f;
        actionLayout.childAlignment = TextAnchor.MiddleCenter;
        actionLayout.childControlWidth = false;
        actionLayout.childControlHeight = false;
        actionLayout.childForceExpandWidth = false;
        actionLayout.childForceExpandHeight = false;

        startMatchButton = CreatePrimaryButton(actionRow, "Start Match", () =>
        {
            statusLabel.text = "Starting the local board scene from the lobby.";
            GameSessionBootstrap.Instance?.StartMatchFromLobby();
        }, 260f, 68f);

        backButton = CreateSecondaryButton(actionRow, "Back", () =>
        {
            GameSessionBootstrap.Instance?.OpenHome();
            RefreshView();
        }, 260f, 68f);
    }

    private void HandleFlowStateChanged(SessionFlowState flowState)
    {
        RefreshView();
    }

    private void RefreshView()
    {
        if (GameSessionBootstrap.Instance == null || statusLabel == null || homePanel == null || lobbyPanel == null)
            return;

        SessionFlowState flowState = GameSessionBootstrap.Instance.FlowState;
        SessionEntryMode entryMode = GameSessionBootstrap.Instance.EntryMode;

        bool showLobby = flowState == SessionFlowState.Lobby;
        homePanel.SetActive(!showLobby);
        lobbyPanel.SetActive(showLobby);

        if (!showLobby)
        {
            statusLabel.text = "Choose how you want to enter the match.";
            return;
        }

        bool isHost = entryMode == SessionEntryMode.Host;
        bool isJoin = entryMode == SessionEntryMode.Join;

        lobbyHeadingLabel.text = isHost ? "Host Lobby" : isJoin ? "Join Lobby" : "Lobby";
        lobbySubheadingLabel.text = isHost
            ? "You are the host. Networking and ready-state will plug in here next."
            : "You joined as a client. Waiting-state and code entry will plug in here next.";

        statusLabel.text = isHost
            ? "Host can start the local prototype match from here."
            : "Join flow scaffold is active. The host will eventually control match start.";

        for (int i = 0; i < lobbySlotLabels.Length; i++)
        {
            if (i == 0)
            {
                lobbySlotLabels[i].text = isHost ? "Player 1\nYou (Host)" : "Player 1\nYou (Client)";
                continue;
            }

            lobbySlotLabels[i].text = $"Player {i + 1}\nWaiting...";
        }

        if (startMatchButton != null)
        {
            startMatchButton.gameObject.SetActive(isHost);
            startMatchButton.interactable = isHost;
        }

        if (backButton != null)
            backButton.gameObject.SetActive(true);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private Button CreatePrimaryButton(RectTransform parent, string label, UnityEngine.Events.UnityAction callback, float width = 460f, float height = 68f)
    {
        return CreateButton(parent, label, callback, accentColor, textColor, width, height);
    }

    private Button CreateSecondaryButton(RectTransform parent, string label, UnityEngine.Events.UnityAction callback, float width = 460f, float height = 68f)
    {
        return CreateButton(parent, label, callback, accentDark, textColor, width, height);
    }

    private Button CreateButton(RectTransform parent, string label, UnityEngine.Events.UnityAction callback, Color background, Color foreground, float width, float height)
    {
        GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(width, height);

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = width;
        layoutElement.preferredHeight = height;

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = background;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = background;
        colors.highlightedColor = Color.Lerp(background, Color.white, 0.08f);
        colors.pressedColor = Color.Lerp(background, Color.black, 0.14f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(background.r * 0.5f, background.g * 0.5f, background.b * 0.5f, 0.8f);
        button.colors = colors;
        button.onClick.AddListener(callback);

        TextMeshProUGUI text = CreateText("Label", buttonRect, label, 28f, FontStyles.Bold, TextAlignmentOptions.Center, foreground);
        Stretch(text.rectTransform, 0f);
        return button;
    }

    private static GameObject CreateContainer(string name, RectTransform parent, Vector2 anchor, Vector2 size, Vector2 anchoredPosition)
    {
        GameObject container = new GameObject(name, typeof(RectTransform));
        container.transform.SetParent(parent, false);
        RectTransform rect = container.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        return container;
    }

    private RectTransform CreateFramedSection(RectTransform parent, string name, string title, Vector2 anchor, Vector2 size, Vector2 anchoredPosition, Color fillColor, float titleOffset)
    {
        Image section = CreateImage(name, parent, fillColor);
        RectTransform rect = section.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image rule = CreateImage("TopRule", rect, accentColor);
        rule.rectTransform.anchorMin = new Vector2(0f, 1f);
        rule.rectTransform.anchorMax = new Vector2(1f, 1f);
        rule.rectTransform.pivot = new Vector2(0.5f, 1f);
        rule.rectTransform.sizeDelta = new Vector2(-18f, 6f);
        rule.rectTransform.anchoredPosition = new Vector2(0f, -10f);

        if (!string.IsNullOrEmpty(title))
        {
            TextMeshProUGUI heading = CreateText("Heading", rect, title, 20f, FontStyles.Bold, TextAlignmentOptions.Center, secondaryTextColor);
            heading.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            heading.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            heading.rectTransform.pivot = new Vector2(0.5f, 1f);
            heading.rectTransform.sizeDelta = new Vector2(size.x - 48f, 32f);
            heading.rectTransform.anchoredPosition = new Vector2(0f, -titleOffset);
        }

        return rect;
    }

    private static Image CreateImage(string name, RectTransform parent, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, RectTransform parent, string content, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        return text;
    }

    private static RectTransform CreateLayoutRoot(string name, RectTransform parent, Vector2 anchor, Vector2 sizeDelta, Vector2 anchoredPosition)
    {
        GameObject layoutObject = new GameObject(name, typeof(RectTransform));
        layoutObject.transform.SetParent(parent, false);
        RectTransform rect = layoutObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition = anchoredPosition;
        return rect;
    }

    private static void Stretch(RectTransform rectTransform, float inset)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = new Vector2(inset, inset);
        rectTransform.offsetMax = new Vector2(-inset, -inset);
    }

    private static void DestroyChildren(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.Destroy(parent.GetChild(i).gameObject);
    }
}
