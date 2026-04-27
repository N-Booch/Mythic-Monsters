using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class VictoryScreenUI : MonoBehaviour
{
    public static VictoryScreenUI Instance;

    private GameObject panel;
    private TextMeshProUGUI headerText;
    private TextMeshProUGUI summaryText;
    private TextMeshProUGUI detailsText;

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

    public static VictoryScreenUI EnsureExists()
    {
        if (Instance != null)
            return Instance;

        VictoryScreenUI existing = FindFirstObjectByType<VictoryScreenUI>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject managerObject = new GameObject("VictoryScreenUI");
        VictoryScreenUI manager = managerObject.AddComponent<VictoryScreenUI>();
        manager.EnsureRuntimeUI();
        manager.Hide();
        return manager;
    }

    public void ShowVictory(PlayerPawn winner, Monster mythicMonster)
    {
        EnsureRuntimeUI();

        string winnerName = winner != null ? winner.GetDisplayNameWithTitles(48) : "A hero";
        string mythicName = mythicMonster != null ? mythicMonster.monsterName : "the Mythic Monster";
        string mythicEffect = mythicMonster != null && !string.IsNullOrWhiteSpace(mythicMonster.mythicEffectDescription)
            ? mythicMonster.mythicEffectDescription
            : "None";

        headerText.text = "Victory";
        summaryText.text = $"{winnerName} defeated {mythicName} and won the game.";

        if (winner != null)
        {
            detailsText.text =
                $"Titles: {winner.GetTitleCount()}\n" +
                $"Gold: {winner.gold}/{PlayerPawn.maxGold}\n" +
                $"Treasures: {winner.equippedTreasures.Count}/{winner.GetEffectiveMaxEquipLoad()}\n" +
                $"Mythic Effect: {mythicEffect}";
        }
        else
        {
            detailsText.text = $"Mythic Effect: {mythicEffect}";
        }

        panel.SetActive(true);
    }

    public void Hide()
    {
        if (panel != null)
            panel.SetActive(false);
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

        panel = new GameObject("VictoryPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.05f, 0.04f, 0.08f, 0.94f);

        headerText = CreateText("Header", panel.transform, new Vector2(0f, -120f), new Vector2(960f, 90f), 56f, FontStyles.Bold);
        summaryText = CreateText("Summary", panel.transform, new Vector2(0f, -250f), new Vector2(1080f, 140f), 30f, FontStyles.Normal);
        detailsText = CreateText("Details", panel.transform, new Vector2(0f, -430f), new Vector2(900f, 220f), 24f, FontStyles.Normal);

        CreateButton(panel.transform, "Play Again", new Vector2(-140f, -680f), RestartScene);
        CreateButton(panel.transform, "Quit", new Vector2(140f, -680f), QuitGame);
    }

    private TextMeshProUGUI CreateText(string objectName, Transform parent, Vector2 anchoredPosition, Vector2 size, float fontSize, FontStyles style)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = style;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.color = Color.white;
        return text;
    }

    private void CreateButton(Transform parent, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(220f, 74f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.25f, 0.18f, 0.09f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(onClick);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI labelText = labelObject.GetComponent<TextMeshProUGUI>();
        labelText.fontSize = 28f;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = Color.white;
        labelText.text = label;
    }

    private void RestartScene()
    {
        Hide();
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
