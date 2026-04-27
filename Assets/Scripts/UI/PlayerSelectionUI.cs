using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerSelectionUI : MonoBehaviour
{
    public static PlayerSelectionUI Instance;

    private GameObject panel;
    private RectTransform contentRoot;
    private TextMeshProUGUI headerText;
    private Button cancelButton;
    private TextMeshProUGUI cancelButtonText;
    private Action<PlayerPawn> onPlayerSelected;
    private readonly List<GameObject> spawnedEntries = new List<GameObject>();
    private readonly Dictionary<PlayerPawn, bool> previousOutlineStates = new Dictionary<PlayerPawn, bool>();

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

    public static PlayerSelectionUI EnsureExists()
    {
        if (Instance != null)
            return Instance;

        PlayerSelectionUI existing = FindFirstObjectByType<PlayerSelectionUI>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject managerObject = new GameObject("PlayerSelectionUI");
        PlayerSelectionUI manager = managerObject.AddComponent<PlayerSelectionUI>();
        manager.EnsureRuntimeUI();
        manager.Hide();
        return manager;
    }

    public void BeginSelection(
        List<PlayerPawn> players,
        string prompt,
        Action<PlayerPawn> onSelected,
        string cancelLabel = "Cancel")
    {
        EnsureRuntimeUI();

        onPlayerSelected = onSelected;
        headerText.text = prompt;
        cancelButtonText.text = string.IsNullOrWhiteSpace(cancelLabel) ? "Cancel" : cancelLabel;
        RebuildEntries(players);
        HighlightPlayers(players);
        panel.SetActive(true);
    }

    public void Hide()
    {
        if (panel != null)
            panel.SetActive(false);

        RestoreHighlights();
        onPlayerSelected = null;
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

        panel = new GameObject("PlayerSelectionPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(760f, 520f);
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
        headerRect.anchoredPosition = new Vector2(0f, -18f);

        headerText = headerObject.GetComponent<TextMeshProUGUI>();
        headerText.fontSize = 28f;
        headerText.color = Color.white;
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.textWrappingMode = TextWrappingModes.Normal;

        GameObject contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.transform.SetParent(panel.transform, false);
        contentRoot = contentObject.GetComponent<RectTransform>();
        contentRoot.anchorMin = new Vector2(0.5f, 1f);
        contentRoot.anchorMax = new Vector2(0.5f, 1f);
        contentRoot.pivot = new Vector2(0.5f, 1f);
        contentRoot.sizeDelta = new Vector2(680f, 340f);
        contentRoot.anchoredPosition = new Vector2(0f, -110f);

        GameObject cancelObject = new GameObject("CancelButton", typeof(RectTransform), typeof(Image), typeof(Button));
        cancelObject.transform.SetParent(panel.transform, false);

        RectTransform cancelRect = cancelObject.GetComponent<RectTransform>();
        cancelRect.anchorMin = new Vector2(0.5f, 0f);
        cancelRect.anchorMax = new Vector2(0.5f, 0f);
        cancelRect.pivot = new Vector2(0.5f, 0f);
        cancelRect.sizeDelta = new Vector2(280f, 58f);
        cancelRect.anchoredPosition = new Vector2(0f, 20f);

        Image cancelImage = cancelObject.GetComponent<Image>();
        cancelImage.color = new Color(0.22f, 0.12f, 0.12f, 0.96f);

        cancelButton = cancelObject.GetComponent<Button>();
        cancelButton.onClick.AddListener(() => SelectPlayer(null));

        GameObject cancelLabelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        cancelLabelObject.transform.SetParent(cancelObject.transform, false);

        RectTransform cancelLabelRect = cancelLabelObject.GetComponent<RectTransform>();
        cancelLabelRect.anchorMin = Vector2.zero;
        cancelLabelRect.anchorMax = Vector2.one;
        cancelLabelRect.offsetMin = Vector2.zero;
        cancelLabelRect.offsetMax = Vector2.zero;

        cancelButtonText = cancelLabelObject.GetComponent<TextMeshProUGUI>();
        cancelButtonText.fontSize = 22f;
        cancelButtonText.color = Color.white;
        cancelButtonText.alignment = TextAlignmentOptions.Center;
        cancelButtonText.text = "Cancel";
    }

    private void RebuildEntries(List<PlayerPawn> players)
    {
        foreach (GameObject entry in spawnedEntries)
        {
            if (entry != null)
                Destroy(entry);
        }

        spawnedEntries.Clear();

        if (players == null)
            return;

        int displayIndex = 0;
        foreach (PlayerPawn player in players)
        {
            if (player == null)
                continue;

            spawnedEntries.Add(CreatePlayerEntry(player, displayIndex));
            displayIndex++;
        }
    }

    private GameObject CreatePlayerEntry(PlayerPawn player, int index)
    {
        GameObject entryObject = new GameObject($"PlayerEntry_{index}", typeof(RectTransform), typeof(Image), typeof(Button));
        entryObject.transform.SetParent(contentRoot, false);

        RectTransform entryRect = entryObject.GetComponent<RectTransform>();
        entryRect.anchorMin = new Vector2(0.5f, 1f);
        entryRect.anchorMax = new Vector2(0.5f, 1f);
        entryRect.pivot = new Vector2(0.5f, 1f);
        entryRect.sizeDelta = new Vector2(660f, 88f);
        entryRect.anchoredPosition = new Vector2(0f, -96f * index);

        Image background = entryObject.GetComponent<Image>();
        background.color = new Color(0.18f, 0.16f, 0.10f, 0.95f);

        Button button = entryObject.GetComponent<Button>();
        button.onClick.AddListener(() => SelectPlayer(player));

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(entryObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(14f, 8f);
        labelRect.offsetMax = new Vector2(-14f, -8f);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.fontSize = 20f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Left;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.text =
            $"{player.GetDisplayNameWithTitles(42)}\n" +
            $"HP {player.currentHP}/{player.GetEffectiveMaxHP()}  |  Might {player.GetCombatMight(false)}  |  Arcane {player.GetCombatArcane(false)}";

        return entryObject;
    }

    private void SelectPlayer(PlayerPawn player)
    {
        Action<PlayerPawn> callback = onPlayerSelected;
        Hide();
        callback?.Invoke(player);
    }

    private void HighlightPlayers(List<PlayerPawn> players)
    {
        RestoreHighlights();

        if (players == null)
            return;

        foreach (PlayerPawn player in players)
        {
            if (player == null)
                continue;

            bool wasActive = player.outlineObject != null && player.outlineObject.activeSelf;
            previousOutlineStates[player] = wasActive;
            player.SetActiveVisual(true);
        }
    }

    private void RestoreHighlights()
    {
        foreach (KeyValuePair<PlayerPawn, bool> pair in previousOutlineStates)
        {
            if (pair.Key == null)
                continue;

            pair.Key.SetActiveVisual(pair.Value);
        }

        previousOutlineStates.Clear();
    }
}
