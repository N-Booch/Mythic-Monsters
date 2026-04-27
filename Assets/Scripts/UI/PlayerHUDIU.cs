using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PlayerHUDUI : MonoBehaviour
{
    public TMP_Text playerNameText;
    public TMP_Text hpText;
    public TMP_Text mightText;
    public TMP_Text arcaneText;
    public TMP_Text goldText;
    public TMP_Text treasureText;

    private PlayerPawn observedPlayer;
    private RectTransform rootPanel;
    private RectTransform titleBadgeContainer;
    private RectTransform titleTooltipPanel;
    private TMP_Text titleTooltipText;
    private RectTransform runtimeTreasurePanel;
    private RectTransform treasureBadgeContainer;
    private RectTransform treasureTooltipPanel;
    private TMP_Text treasureTooltipText;
    private TMP_Text noTitleText;
    private TMP_Text treasureHeaderText;
    private Button useTreasureButton;
    private Image useTreasureButtonImage;
    private TMP_Text useTreasureButtonText;
    private readonly List<GameObject> titleBadgeObjects = new List<GameObject>();
    private readonly List<GameObject> treasureBadgeObjects = new List<GameObject>();
    private RectTransform hpCardRect;
    private RectTransform mightCardRect;
    private RectTransform arcaneCardRect;
    private RectTransform goldCardRect;
    private int currentTitleRows = 1;
    private float currentTooltipPanelHeight = 0f;
    private float currentTreasureTooltipPanelHeight = 0f;

    public void SetPlayer(PlayerPawn player)
    {
        observedPlayer = player;
        EnsureRuntimeLayout();
        Refresh();
    }

    public void Refresh()
    {
        if (observedPlayer == null)
            return;

        EnsureRuntimeLayout();

        playerNameText.text = observedPlayer.playerName;
        hpText.text = $"HP  {observedPlayer.currentHP}/{observedPlayer.GetEffectiveMaxHP()}";
        goldText.text = $"Gold  {observedPlayer.gold}";
        mightText.text = BuildMightText(observedPlayer);
        arcaneText.text = BuildArcaneText(observedPlayer);

        RebuildTitleBadges(observedPlayer);

        if (treasureHeaderText != null)
            treasureHeaderText.text = $"Treasures  {observedPlayer.equippedTreasures.Count}/{observedPlayer.GetEffectiveMaxEquipLoad()}";

        RebuildTreasureBadges(observedPlayer);

        if (useTreasureButton != null)
        {
            bool hasTreasures = observedPlayer.equippedTreasures != null && observedPlayer.equippedTreasures.Count > 0;
            bool canUseTreasure = hasTreasures &&
                GameManager.Instance != null &&
                GameManager.Instance.CanUseAnyActiveTreasure(observedPlayer);

            useTreasureButton.gameObject.SetActive(hasTreasures);
            useTreasureButton.interactable = canUseTreasure;

            if (useTreasureButtonImage != null)
            {
                useTreasureButtonImage.color = canUseTreasure
                    ? new Color(0.55f, 0.28f, 0.11f, 1f)
                    : new Color(0.09f, 0.08f, 0.08f, 0.78f);
            }

            if (useTreasureButtonText != null)
            {
                useTreasureButtonText.color = canUseTreasure
                    ? Color.white
                    : new Color(0.55f, 0.50f, 0.44f, 0.8f);
            }
        }
    }

    private void EnsureRuntimeLayout()
    {
        if (rootPanel != null)
            return;

        if (playerNameText == null || hpText == null || mightText == null || arcaneText == null || goldText == null)
            return;

        Transform originalParent = playerNameText.transform.parent;
        if (originalParent == null)
            return;

        HideLegacyHudElements(originalParent);

        Image oldHudBackground = originalParent.GetComponent<Image>();
        if (oldHudBackground != null)
            oldHudBackground.enabled = false;

        rootPanel = CreatePanel(
            originalParent,
            "PlayerHudPanel",
            new Vector2(24f, -24f),
            new Vector2(440f, 368f),
            new Color(0.11f, 0.08f, 0.07f, 0.94f));

        CreatePanel(
            rootPanel,
            "HeaderAccent",
            new Vector2(0f, 0f),
            new Vector2(440f, 8f),
            new Color(0.59f, 0.29f, 0.13f, 1f));

        titleBadgeContainer = CreatePanel(
            rootPanel,
            "TitleBadgeContainer",
            new Vector2(18f, -18f),
            new Vector2(404f, 52f),
            new Color(0.16f, 0.10f, 0.08f, 0.88f));

        noTitleText = CreateText(
            titleBadgeContainer,
            "NoTitleText",
            new Vector2(10f, -8f),
            new Vector2(384f, 24f),
            14f,
            new Color(0.88f, 0.73f, 0.51f, 0.9f),
            TextAlignmentOptions.Left);
        noTitleText.text = "No Titles";

        titleTooltipPanel = CreatePanel(
            rootPanel,
            "TitleTooltipPanel",
            new Vector2(18f, -74f),
            new Vector2(404f, 42f),
            new Color(0.18f, 0.12f, 0.10f, 0.96f));
        titleTooltipPanel.gameObject.SetActive(false);

        titleTooltipText = CreateText(
            titleTooltipPanel,
            "TitleTooltipText",
            new Vector2(10f, -8f),
            new Vector2(384f, 28f),
            14f,
            new Color(0.95f, 0.91f, 0.87f, 1f),
            TextAlignmentOptions.Left);
        titleTooltipText.enableAutoSizing = true;
        titleTooltipText.fontSizeMin = 11f;
        titleTooltipText.fontSizeMax = 14f;

        ReparentExistingText(
            playerNameText,
            rootPanel,
            new Vector2(18f, -128f),
            new Vector2(404f, 40f),
            34f,
            Color.white,
            TextAlignmentOptions.Left);
        playerNameText.enableAutoSizing = true;
        playerNameText.fontSizeMin = 24f;
        playerNameText.fontSizeMax = 34f;

        hpCardRect = CreateStatCard(new Vector2(18f, -184f), new Vector2(190f, 50f), hpText, 20f, new Color(0.41f, 0.15f, 0.15f, 0.92f));
        mightCardRect = CreateStatCard(new Vector2(232f, -184f), new Vector2(190f, 50f), mightText, 20f, new Color(0.31f, 0.18f, 0.11f, 0.92f));
        arcaneCardRect = CreateStatCard(new Vector2(18f, -244f), new Vector2(190f, 50f), arcaneText, 20f, new Color(0.18f, 0.16f, 0.28f, 0.92f));
        goldCardRect = CreateStatCard(new Vector2(232f, -244f), new Vector2(190f, 50f), goldText, 20f, new Color(0.29f, 0.22f, 0.10f, 0.92f));

        runtimeTreasurePanel = CreatePanel(
            rootPanel,
            "TreasurePanel",
            new Vector2(18f, -308f),
            new Vector2(404f, 132f),
            new Color(0.17f, 0.10f, 0.09f, 0.96f));

        treasureHeaderText = CreateText(
            runtimeTreasurePanel,
            "TreasureHeaderText",
            new Vector2(12f, -8f),
            new Vector2(200f, 20f),
            16f,
            new Color(0.98f, 0.82f, 0.56f, 1f),
            TextAlignmentOptions.Left);
        treasureHeaderText.fontStyle = FontStyles.Bold;

        if (treasureText == null)
        {
            GameObject textObject = new GameObject("TreasureText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(runtimeTreasurePanel, false);
            treasureText = textObject.GetComponent<TextMeshProUGUI>();
            treasureText.font = goldText.font;
            treasureText.fontSharedMaterial = goldText.fontSharedMaterial;
        }
        else
        {
            treasureText.transform.SetParent(runtimeTreasurePanel, false);
        }

        treasureText.gameObject.SetActive(false);

        treasureBadgeContainer = CreatePanel(
            runtimeTreasurePanel,
            "TreasureBadgeContainer",
            new Vector2(10f, -30f),
            new Vector2(384f, 52f),
            new Color(0f, 0f, 0f, 0f));

        treasureTooltipPanel = CreatePanel(
            runtimeTreasurePanel,
            "TreasureTooltipPanel",
            new Vector2(10f, -84f),
            new Vector2(384f, 30f),
            new Color(0.18f, 0.12f, 0.10f, 0.96f));
        treasureTooltipPanel.gameObject.SetActive(false);

        treasureTooltipText = CreateText(
            treasureTooltipPanel,
            "TreasureTooltipText",
            new Vector2(10f, -6f),
            new Vector2(364f, 18f),
            13f,
            new Color(0.95f, 0.91f, 0.87f, 1f),
            TextAlignmentOptions.Left);
        treasureTooltipText.enableAutoSizing = true;
        treasureTooltipText.fontSizeMin = 10f;
        treasureTooltipText.fontSizeMax = 13f;

        GameObject buttonObject = new GameObject("UseTreasureButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(runtimeTreasurePanel, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0f);
        buttonRect.anchorMax = new Vector2(1f, 0f);
        buttonRect.pivot = new Vector2(1f, 0f);
        buttonRect.anchoredPosition = new Vector2(-10f, 10f);
        buttonRect.sizeDelta = new Vector2(128f, 34f);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.36f, 0.20f, 0.11f, 1f);
        useTreasureButtonImage = buttonImage;

        useTreasureButton = buttonObject.GetComponent<Button>();
        useTreasureButton.onClick.AddListener(OnUseTreasureClicked);

        useTreasureButtonText = CreateText(
            buttonRect,
            "UseTreasureButtonText",
            Vector2.zero,
            buttonRect.sizeDelta,
            16f,
            Color.white,
            TextAlignmentOptions.Center);
        useTreasureButtonText.text = "Use Treasure";
        useTreasureButtonText.fontStyle = FontStyles.Bold;

        ColorBlock treasureButtonColors = useTreasureButton.colors;
        treasureButtonColors.normalColor = Color.white;
        treasureButtonColors.highlightedColor = new Color(1.14f, 1.08f, 1f, 1f);
        treasureButtonColors.pressedColor = new Color(0.82f, 0.68f, 0.56f, 1f);
        treasureButtonColors.selectedColor = Color.white;
        treasureButtonColors.disabledColor = new Color(0.32f, 0.30f, 0.28f, 0.78f);
        useTreasureButton.colors = treasureButtonColors;

        UpdateHudLayout();
    }

    private void HideLegacyHudElements(Transform originalParent)
    {
        for (int i = 0; i < originalParent.childCount; i++)
        {
            Transform child = originalParent.GetChild(i);
            if (child == null)
                continue;

            child.gameObject.SetActive(false);
        }
    }

    private void RebuildTitleBadges(PlayerPawn player)
    {
        foreach (GameObject badge in titleBadgeObjects)
        {
            if (badge != null)
                Destroy(badge);
        }

        titleBadgeObjects.Clear();
        HideTitleTooltip();

        if (titleBadgeContainer == null || player == null || player.titles == null || player.titles.Count == 0)
        {
            currentTitleRows = 1;
            if (noTitleText != null)
                noTitleText.gameObject.SetActive(true);
            UpdateHudLayout();
            return;
        }

        float x = 10f;
        float y = -8f;
        int row = 0;
        bool createdBadge = false;

        foreach (TitleData title in player.titles)
        {
            if (title == null || string.IsNullOrWhiteSpace(title.titleName))
                continue;

            float width = Mathf.Clamp(38f + (title.titleName.Length * 8f), 92f, 182f);
            if (x + width > 394f)
            {
                row++;
                if (row > 1)
                    break;

                x = 10f;
                y -= 24f;
            }

            RectTransform badgeRect = CreatePanel(
                titleBadgeContainer,
                $"{title.titleName}_Badge",
                new Vector2(x, y),
                new Vector2(width, 20f),
                player.cleansedTitles != null && player.cleansedTitles.Contains(title)
                    ? new Color(0.28f, 0.20f, 0.16f, 0.92f)
                    : new Color(0.34f, 0.19f, 0.10f, 0.96f));

            GameObject badgeObject = badgeRect.gameObject;
            titleBadgeObjects.Add(badgeObject);

            TMP_Text badgeText = CreateText(
                badgeRect,
                "Label",
                new Vector2(8f, -3f),
                new Vector2(width - 16f, 18f),
                13f,
                Color.white,
                TextAlignmentOptions.Center);
            badgeText.text = title.titleName;
            badgeText.enableAutoSizing = true;
            badgeText.fontSizeMin = 10f;
            badgeText.fontSizeMax = 13f;

            EventTrigger trigger = badgeObject.AddComponent<EventTrigger>();
            AddHoverEvent(trigger, EventTriggerType.PointerEnter, () => ShowTitleTooltip(title));
            AddHoverEvent(trigger, EventTriggerType.PointerExit, HideTitleTooltip);

            x += width + 6f;
            createdBadge = true;
        }

        currentTitleRows = createdBadge ? row + 1 : 1;
        if (noTitleText != null)
            noTitleText.gameObject.SetActive(!createdBadge);

        UpdateHudLayout();
    }

    private void AddHoverEvent(EventTrigger trigger, EventTriggerType eventType, UnityEngine.Events.UnityAction action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(_ => action.Invoke());
        trigger.triggers.Add(entry);
    }

    private void ShowTitleTooltip(TitleData title)
    {
        if (titleTooltipPanel == null || titleTooltipText == null || title == null)
            return;

        string description = string.IsNullOrWhiteSpace(title.description)
            ? "No description."
            : title.description;

        if (observedPlayer != null &&
            observedPlayer.cleansedTitles != null &&
            observedPlayer.cleansedTitles.Contains(title))
        {
            description += " (Cleansed)";
        }

        titleTooltipText.text = description;
        titleTooltipText.ForceMeshUpdate();
        float preferredHeight = Mathf.Clamp(titleTooltipText.preferredHeight + 14f, 28f, 72f);
        titleTooltipPanel.sizeDelta = new Vector2(titleTooltipPanel.sizeDelta.x, preferredHeight + 14f);
        titleTooltipText.rectTransform.sizeDelta = new Vector2(384f, preferredHeight);
        currentTooltipPanelHeight = preferredHeight + 14f;
        titleTooltipPanel.gameObject.SetActive(true);
        UpdateHudLayout();
    }

    private void HideTitleTooltip()
    {
        if (titleTooltipPanel != null)
            titleTooltipPanel.gameObject.SetActive(false);

        currentTooltipPanelHeight = 0f;
        UpdateHudLayout();
    }

    private RectTransform CreateStatCard(Vector2 position, Vector2 size, TMP_Text targetText, float fontSize, Color cardColor)
    {
        RectTransform card = CreatePanel(rootPanel, targetText.name + "Card", position, size, cardColor);

        ReparentExistingText(
            targetText,
            card,
            new Vector2(12f, -10f),
            new Vector2(size.x - 24f, size.y - 20f),
            fontSize,
            Color.white,
            TextAlignmentOptions.Left);
        targetText.enableAutoSizing = true;
        targetText.fontSizeMin = 14f;
        targetText.fontSizeMax = fontSize;
        return card;
    }

    private RectTransform CreatePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = panelObject.GetComponent<Image>();
        image.color = color;
        return rect;
    }

    private TMP_Text CreateText(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TMP_Text text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = playerNameText != null ? playerNameText.font : null;
        text.fontSharedMaterial = playerNameText != null ? playerNameText.fontSharedMaterial : null;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private void ReparentExistingText(TMP_Text text, Transform newParent, Vector2 anchoredPosition, Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        if (text == null)
            return;

        text.transform.SetParent(newParent, false);
        text.gameObject.SetActive(true);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.margin = Vector4.zero;
    }

    private void UpdateHudLayout()
    {
        if (rootPanel == null || titleBadgeContainer == null || titleTooltipPanel == null || runtimeTreasurePanel == null || playerNameText == null)
            return;

        float badgeHeight = currentTitleRows > 1 ? 64f : 40f;
        titleBadgeContainer.sizeDelta = new Vector2(404f, badgeHeight);
        titleTooltipPanel.anchoredPosition = new Vector2(18f, -(24f + badgeHeight));

        float reservedTooltipHeight = titleTooltipPanel.gameObject.activeSelf ? currentTooltipPanelHeight + 8f : 0f;
        float contentStartY = 24f + badgeHeight + reservedTooltipHeight;

        playerNameText.rectTransform.anchoredPosition = new Vector2(18f, -(contentStartY + 8f));

        if (hpCardRect != null) hpCardRect.anchoredPosition = new Vector2(18f, -(contentStartY + 56f));
        if (mightCardRect != null) mightCardRect.anchoredPosition = new Vector2(232f, -(contentStartY + 56f));
        if (arcaneCardRect != null) arcaneCardRect.anchoredPosition = new Vector2(18f, -(contentStartY + 116f));
        if (goldCardRect != null) goldCardRect.anchoredPosition = new Vector2(232f, -(contentStartY + 116f));
        runtimeTreasurePanel.anchoredPosition = new Vector2(18f, -(contentStartY + 180f));

        float treasurePanelHeight = 132f;
        if (treasureTooltipPanel != null && treasureTooltipPanel.gameObject.activeSelf)
            treasurePanelHeight += currentTreasureTooltipPanelHeight + 8f;

        runtimeTreasurePanel.sizeDelta = new Vector2(404f, treasurePanelHeight);

        float rootHeight = contentStartY + 180f + treasurePanelHeight + 18f;
        rootPanel.sizeDelta = new Vector2(440f, Mathf.Max(368f, rootHeight));
    }

    private string BuildMightText(PlayerPawn player)
    {
        int value = player.GetEffectiveMight();
        if (player.nextCombatMightBonus > 0)
            return $"Might  {value} <size=75%><color=#F0BB84>(+{player.nextCombatMightBonus})</color></size>";

        return $"Might  {value}";
    }

    private string BuildArcaneText(PlayerPawn player)
    {
        int value = player.GetEffectiveArcane();
        if (player.nextCombatArcaneBonus > 0)
            return $"Arcane  {value} <size=75%><color=#B8C6FF>(+{player.nextCombatArcaneBonus})</color></size>";

        return $"Arcane  {value}";
    }

    private string BuildTreasureList(PlayerPawn player)
    {
        if (player == null || player.equippedTreasures == null || player.equippedTreasures.Count == 0)
            return "No equipped treasures";

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < player.equippedTreasures.Count; i++)
        {
            TreasureCard treasure = player.equippedTreasures[i];
            if (treasure == null)
                continue;

            if (builder.Length > 0)
                builder.Append("\n");

            builder.Append("• ");
            builder.Append(treasure.cardName);
        }

        return builder.Length > 0 ? builder.ToString() : "No equipped treasures";
    }

    private void RebuildTreasureBadges(PlayerPawn player)
    {
        foreach (GameObject badge in treasureBadgeObjects)
        {
            if (badge != null)
                Destroy(badge);
        }

        treasureBadgeObjects.Clear();

        if (treasureBadgeContainer == null || player == null || player.equippedTreasures == null)
            return;

        for (int i = 0; i < player.equippedTreasures.Count && i < 4; i++)
        {
            TreasureCard treasure = player.equippedTreasures[i];
            if (treasure == null)
                continue;

            int column = i % 2;
            int row = i / 2;
            float x = column == 0 ? 0f : 188f;
            float y = row == 0 ? 0f : -26f;

            RectTransform badgeRect = CreatePanel(
                treasureBadgeContainer,
                $"{treasure.cardName}_Badge",
                new Vector2(x, y),
                new Vector2(180f, 22f),
                new Color(0.32f, 0.18f, 0.10f, 0.96f));

            TMP_Text badgeText = CreateText(
                badgeRect,
                "Label",
                new Vector2(6f, -2f),
                new Vector2(168f, 20f),
                12f,
                new Color(0.96f, 0.92f, 0.86f, 1f),
                TextAlignmentOptions.Center);
            badgeText.text = treasure.cardName;
            badgeText.enableAutoSizing = true;
            badgeText.fontSizeMin = 9f;
            badgeText.fontSizeMax = 12f;

            EventTrigger trigger = badgeRect.gameObject.AddComponent<EventTrigger>();
            AddHoverEvent(trigger, EventTriggerType.PointerEnter, () => ShowTreasureTooltip(treasure));
            AddHoverEvent(trigger, EventTriggerType.PointerExit, HideTreasureTooltip);

            treasureBadgeObjects.Add(badgeRect.gameObject);
        }
    }

    private void ShowTreasureTooltip(TreasureCard treasure)
    {
        if (treasureTooltipPanel == null || treasureTooltipText == null || treasure == null)
            return;

        string description = string.IsNullOrWhiteSpace(treasure.description)
            ? "No description."
            : treasure.description;

        treasureTooltipText.text = description;
        treasureTooltipText.rectTransform.sizeDelta = new Vector2(364f, 18f);
        treasureTooltipText.ForceMeshUpdate();
        float preferredHeight = Mathf.Clamp(treasureTooltipText.preferredHeight + 12f, 18f, 42f);
        treasureTooltipPanel.sizeDelta = new Vector2(384f, preferredHeight + 10f);
        treasureTooltipText.rectTransform.sizeDelta = new Vector2(364f, preferredHeight);
        currentTreasureTooltipPanelHeight = preferredHeight + 10f;
        treasureTooltipPanel.gameObject.SetActive(true);
        UpdateHudLayout();
    }

    private void HideTreasureTooltip()
    {
        if (treasureTooltipPanel != null)
            treasureTooltipPanel.gameObject.SetActive(false);

        currentTreasureTooltipPanelHeight = 0f;
        UpdateHudLayout();
    }

    private void OnUseTreasureClicked()
    {
        if (observedPlayer == null)
            return;

        GameManager.Instance?.OpenTreasureActivationSelection(observedPlayer);
    }
}
