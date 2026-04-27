using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CombatSceneController : MonoBehaviour
{
    public static CombatSceneController Instance { get; private set; }

    [Header("Preview")]
    public bool showPreviewDataWhenEmpty = true;

    private Canvas canvas;
    private GameObject root;
    private TextMeshProUGUI playerNameText;
    private TextMeshProUGUI playerStatsText;
    private TextMeshProUGUI opponentNameText;
    private TextMeshProUGUI opponentStatsText;
    private TextMeshProUGUI rewardText;
    private TextMeshProUGUI effectText;
    private TextMeshProUGUI diceCountText;
    private TextMeshProUGUI playerAttackText;
    private TextMeshProUGUI monsterMightText;
    private TextMeshProUGUI rollText;
    private TextMeshProUGUI statusText;
    private Image resultBoxImage;
    private Image resultAccentImage;
    private Button rollButton;
    private TextMeshProUGUI rollButtonLabel;
    private Button confirmButton;
    private Button useTreasureButton;
    private RectTransform handDock;
    private Image playerPortraitImage;
    private TextMeshProUGUI playerPortraitLabel;
    private Image monsterPortraitImage;
    private TextMeshProUGUI monsterPortraitLabel;
    private RectTransform diceTray;
    private readonly List<Image> diceSlotImages = new List<Image>();
    private readonly List<TextMeshProUGUI> diceSlotTexts = new List<TextMeshProUGUI>();
    private readonly List<GameObject> combatHandCards = new List<GameObject>();
    private PlayerPawn lastHandOwner;
    private int lastHandSignature;
    private Sprite placeholderMonsterSprite;

    private enum ResultTone
    {
        Neutral,
        Ready,
        Success,
        Danger,
        Warning
    }

    public RectTransform HandDock => handDock;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        EnsureSceneShell();
        LoadPlaceholderSprite();
        EnsureLayout();
        RefreshFromState();
    }

    private void LateUpdate()
    {
        RefreshFromState();
        UpdateButtons();
        UpdateCombatHand();
        UpdateDiceTray();
    }

    [ContextMenu("Refresh From Snapshot")]
    public void RefreshFromState()
    {
        CombatPresentationSnapshot snapshot = CombatSceneState.HasSnapshot
            ? CombatSceneState.Snapshot
            : GetPreviewSnapshot();

        if (snapshot == null)
            return;

        playerNameText.text = string.IsNullOrWhiteSpace(snapshot.playerName) ? "Player" : snapshot.playerName;
        playerStatsText.text = snapshot.playerStatsText;
        opponentNameText.text = string.IsNullOrWhiteSpace(snapshot.opponentName) ? "Opponent" : snapshot.opponentName;
        opponentStatsText.text = snapshot.opponentStatsText;
        rewardText.text = string.IsNullOrWhiteSpace(snapshot.rewardText) ? "Reward: -" : snapshot.rewardText;
        effectText.text = string.IsNullOrWhiteSpace(snapshot.effectText) ? "Effect: -" : snapshot.effectText;
        diceCountText.text = string.Empty;

        UpdatePortraits(snapshot);
        UpdateVersusDisplay(snapshot);
        ApplyResultPresentation(snapshot);
    }

    private void EnsureSceneShell()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            Camera createdCamera = cameraObject.AddComponent<Camera>();
            createdCamera.clearFlags = CameraClearFlags.SolidColor;
            createdCamera.backgroundColor = new Color(0.08f, 0.05f, 0.04f);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        }

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }
    }

    private void EnsureLayout()
    {
        if (root != null)
            return;

        canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("CombatSceneCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        root = CreatePanel(
            "CombatSceneRoot",
            canvas.transform,
            new Color(0.10f, 0.07f, 0.05f, 1f),
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            Vector2.zero,
            Vector2.zero);

        GameObject backdropGlow = CreatePanel(
            "BackdropGlow",
            root.transform,
            new Color(0.44f, 0.21f, 0.11f, 0.55f),
            new Vector2(0.08f, 0.10f),
            new Vector2(0.92f, 0.88f),
            Vector2.zero,
            Vector2.zero);
        backdropGlow.GetComponent<Image>().raycastTarget = false;

        GameObject arenaStrip = CreatePanel(
            "ArenaStrip",
            root.transform,
            new Color(0.15f, 0.08f, 0.07f, 0.88f),
            new Vector2(0.08f, 0.36f),
            new Vector2(0.92f, 0.85f),
            Vector2.zero,
            Vector2.zero);

        GameObject playerCard = CreatePanel(
            "PlayerCard",
            arenaStrip.transform,
            new Color(0.20f, 0.14f, 0.11f, 0.94f),
            new Vector2(0.03f, 0.08f),
            new Vector2(0.36f, 0.92f),
            Vector2.zero,
            Vector2.zero);

        playerNameText = CreateText(
            "PlayerName",
            playerCard.transform,
            new Vector2(0.08f, 0.72f),
            new Vector2(0.92f, 0.90f),
            31f,
            FontStyles.Bold,
            Color.white);
        playerNameText.enableAutoSizing = true;
        playerNameText.fontSizeMin = 20f;
        playerNameText.fontSizeMax = 31f;

        playerStatsText = CreateText(
            "PlayerStats",
            playerCard.transform,
            new Vector2(0.08f, 0.56f),
            new Vector2(0.92f, 0.68f),
            20f,
            FontStyles.Normal,
            new Color(0.92f, 0.90f, 0.86f));

        CreatePortraitBox(playerCard.transform, true);

        GameObject versusMedallion = CreatePanel(
            "VersusMedallion",
            arenaStrip.transform,
            new Color(0.56f, 0.26f, 0.13f, 0.98f),
            new Vector2(0.39f, 0.38f),
            new Vector2(0.61f, 0.72f),
            Vector2.zero,
            Vector2.zero);
        CreateText(
            "VersusText",
            versusMedallion.transform,
            new Vector2(0.30f, 0.52f),
            new Vector2(0.70f, 0.88f),
            42f,
            FontStyles.Bold,
            Color.white).text = "VS";

        playerAttackText = CreateText(
            "PlayerAttackText",
            versusMedallion.transform,
            new Vector2(0.05f, 0.08f),
            new Vector2(0.34f, 0.48f),
            19f,
            FontStyles.Bold,
            new Color(1f, 0.88f, 0.68f));
        playerAttackText.alignment = TextAlignmentOptions.Center;
        playerAttackText.enableAutoSizing = true;
        playerAttackText.fontSizeMin = 12f;
        playerAttackText.fontSizeMax = 19f;

        monsterMightText = CreateText(
            "MonsterMightText",
            versusMedallion.transform,
            new Vector2(0.66f, 0.08f),
            new Vector2(0.95f, 0.48f),
            19f,
            FontStyles.Bold,
            new Color(1f, 0.88f, 0.68f));
        monsterMightText.alignment = TextAlignmentOptions.Center;
        monsterMightText.enableAutoSizing = true;
        monsterMightText.fontSizeMin = 12f;
        monsterMightText.fontSizeMax = 19f;

        diceCountText = CreateText(
            "DiceCountText",
            versusMedallion.transform,
            new Vector2(0.20f, 0.00f),
            new Vector2(0.80f, 0.14f),
            16f,
            FontStyles.Bold,
            new Color(1f, 0.88f, 0.68f));
        diceCountText.alignment = TextAlignmentOptions.Center;

        GameObject opponentCard = CreatePanel(
            "OpponentCard",
            arenaStrip.transform,
            new Color(0.20f, 0.12f, 0.10f, 0.94f),
            new Vector2(0.64f, 0.08f),
            new Vector2(0.97f, 0.92f),
            Vector2.zero,
            Vector2.zero);

        opponentNameText = CreateText(
            "OpponentName",
            opponentCard.transform,
            new Vector2(0.08f, 0.72f),
            new Vector2(0.92f, 0.90f),
            31f,
            FontStyles.Bold,
            Color.white);
        opponentNameText.enableAutoSizing = true;
        opponentNameText.fontSizeMin = 20f;
        opponentNameText.fontSizeMax = 31f;

        opponentStatsText = CreateText(
            "OpponentStats",
            opponentCard.transform,
            new Vector2(0.08f, 0.56f),
            new Vector2(0.92f, 0.68f),
            20f,
            FontStyles.Normal,
            new Color(0.92f, 0.90f, 0.86f));

        CreatePortraitBox(opponentCard.transform, false);

        GameObject resultBox = CreatePanel(
            "ResultBox",
            arenaStrip.transform,
            new Color(0.18f, 0.11f, 0.09f, 0.98f),
            new Vector2(0.38f, 0.12f),
            new Vector2(0.62f, 0.31f),
            Vector2.zero,
            Vector2.zero);
        resultBoxImage = resultBox.GetComponent<Image>();

        GameObject resultAccent = CreatePanel(
            "ResultAccent",
            resultBox.transform,
            new Color(0.80f, 0.38f, 0.13f, 1f),
            new Vector2(0f, 0.92f),
            new Vector2(1f, 1f),
            Vector2.zero,
            Vector2.zero);
        resultAccentImage = resultAccent.GetComponent<Image>();

        statusText = CreateText(
            "StatusText",
            resultBox.transform,
            new Vector2(0.08f, 0.36f),
            new Vector2(0.92f, 0.84f),
            24f,
            FontStyles.Bold,
            Color.white);
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.enableAutoSizing = true;
        statusText.fontSizeMin = 16f;
        statusText.fontSizeMax = 24f;

        rollText = CreateText(
            "RollText",
            resultBox.transform,
            new Vector2(0.08f, 0.08f),
            new Vector2(0.92f, 0.32f),
            15f,
            FontStyles.Normal,
            new Color(0.92f, 0.90f, 0.86f));
        rollText.alignment = TextAlignmentOptions.Center;
        rollText.enableAutoSizing = true;
        rollText.fontSizeMin = 11f;
        rollText.fontSizeMax = 15f;

        GameObject lowerRail = CreatePanel(
            "LowerRail",
            root.transform,
            new Color(0.14f, 0.08f, 0.07f, 0.96f),
            new Vector2(0.08f, 0.22f),
            new Vector2(0.92f, 0.31f),
            Vector2.zero,
            Vector2.zero);

        rewardText = CreateText(
            "RewardText",
            lowerRail.transform,
            new Vector2(0.03f, 0.56f),
            new Vector2(0.63f, 0.92f),
            23f,
            FontStyles.Bold,
            new Color(0.98f, 0.82f, 0.56f));

        effectText = CreateText(
            "EffectText",
            lowerRail.transform,
            new Vector2(0.03f, 0.10f),
            new Vector2(0.63f, 0.52f),
            20f,
            FontStyles.Italic,
            new Color(0.90f, 0.84f, 0.78f));

        GameObject controlsRail = CreatePanel(
            "ControlsRail",
            root.transform,
            new Color(0.14f, 0.08f, 0.07f, 0.96f),
            new Vector2(0.32f, 0.03f),
            new Vector2(0.68f, 0.09f),
            Vector2.zero,
            Vector2.zero);

        rollButton = CreateButton(
            "RollButton",
            controlsRail.transform,
            "Roll",
            new Vector2(0.04f, 0.14f),
            new Vector2(0.46f, 0.86f),
            OnPrimaryButtonPressed);
        rollButtonLabel = rollButton.GetComponentInChildren<TextMeshProUGUI>();

        confirmButton = CreateButton(
            "ConfirmButton",
            controlsRail.transform,
            "Confirm Roll",
            new Vector2(0.50f, 0.14f),
            new Vector2(0.96f, 0.86f),
            () => ConfirmRollUI.Instance?.OnConfirmClicked());

        GameObject handRail = CreatePanel(
            "HandRail",
            root.transform,
            new Color(0.17f, 0.10f, 0.08f, 0.88f),
            new Vector2(0.08f, 0.11f),
            new Vector2(0.92f, 0.22f),
            Vector2.zero,
            Vector2.zero);

        GameObject handDockObject = new GameObject("HandDock", typeof(RectTransform));
        handDockObject.transform.SetParent(handRail.transform, false);
        handDock = handDockObject.GetComponent<RectTransform>();
        handDock.anchorMin = new Vector2(0.02f, 0.08f);
        handDock.anchorMax = new Vector2(0.82f, 0.92f);
        handDock.offsetMin = Vector2.zero;
        handDock.offsetMax = Vector2.zero;

        useTreasureButton = CreateButton(
            "UseTreasureButton",
            handRail.transform,
            "Use Treasure",
            new Vector2(0.84f, 0.18f),
            new Vector2(0.98f, 0.82f),
            OnUseTreasurePressed);

        GameObject diceRail = CreatePanel(
            "DiceRail",
            lowerRail.transform,
            new Color(0.17f, 0.10f, 0.08f, 0.88f),
            new Vector2(0.66f, 0.10f),
            new Vector2(0.97f, 0.90f),
            Vector2.zero,
            Vector2.zero);

        diceCountText.transform.SetParent(diceRail.transform, false);
        RectTransform diceCountRect = diceCountText.rectTransform;
        diceCountRect.anchorMin = new Vector2(0.06f, 0.78f);
        diceCountRect.anchorMax = new Vector2(0.94f, 0.99f);
        diceCountRect.offsetMin = Vector2.zero;
        diceCountRect.offsetMax = Vector2.zero;
        diceCountText.fontSize = 15f;
        diceCountText.alignment = TextAlignmentOptions.Center;

        GameObject diceTrayObject = new GameObject("DiceTray", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        diceTrayObject.transform.SetParent(diceRail.transform, false);
        diceTray = diceTrayObject.GetComponent<RectTransform>();
        diceTray.anchorMin = new Vector2(0.10f, 0.08f);
        diceTray.anchorMax = new Vector2(0.90f, 0.52f);
        diceTray.offsetMin = Vector2.zero;
        diceTray.offsetMax = Vector2.zero;

        HorizontalLayoutGroup diceLayout = diceTrayObject.GetComponent<HorizontalLayoutGroup>();
        diceLayout.spacing = 10f;
        diceLayout.childAlignment = TextAnchor.MiddleCenter;
        diceLayout.childControlWidth = false;
        diceLayout.childControlHeight = false;
        diceLayout.childForceExpandWidth = false;
        diceLayout.childForceExpandHeight = false;

        CreateDiceSlots();
    }

    private CombatPresentationSnapshot GetPreviewSnapshot()
    {
        if (!showPreviewDataWhenEmpty)
            return null;

        return new CombatPresentationSnapshot
        {
            mode = "combat",
            playerName = "Greedy Lucky Goblin",
            playerStatsText = "HP 4/4\nMight 2\nArcane 6",
            opponentName = "Flaming Phoenix",
            opponentStatsText = "Monster HP 25/25\nMonster Might 14",
            rewardText = "Reward: Victory",
            effectText = "Effect: After each failed combat roll, burn the player for 2 more HP.",
            diceCountText = "Dice 3",
            rollText = "Dice: [3] [6] [4]   Sum 13",
            statusText = "Flaming Phoenix takes 13 damage.",
            playerAttackValue = 15,
            monsterDefenseValue = 14,
            diceCountValue = 3
        };
    }

    private GameObject CreatePanel(
        string objectName,
        Transform parent,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject panelObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        Image image = panelObject.GetComponent<Image>();
        image.color = color;
        return panelObject;
    }

    private TextMeshProUGUI CreateText(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float fontSize,
        FontStyles fontStyle,
        Color color)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private Button CreateButton(
        string objectName,
        Transform parent,
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.47f, 0.23f, 0.14f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(onClick);

        TextMeshProUGUI labelText = CreateText(
            $"{objectName}Label",
            buttonObject.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            24f,
            FontStyles.Bold,
            Color.white);
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.text = label;

        return button;
    }

    private void CreatePortraitBox(Transform parent, bool isPlayer)
    {
        GameObject portraitFrame = CreatePanel(
            isPlayer ? "PlayerPortraitFrame" : "MonsterPortraitFrame",
            parent,
            new Color(0.16f, 0.10f, 0.09f, 0.96f),
            new Vector2(0.08f, 0.10f),
            new Vector2(0.92f, 0.54f),
            Vector2.zero,
            Vector2.zero);

        GameObject portraitImageObject = CreatePanel(
            isPlayer ? "PlayerPortraitImage" : "MonsterPortraitImage",
            portraitFrame.transform,
            new Color(0.23f, 0.16f, 0.13f, 1f),
            new Vector2(0.06f, 0.08f),
            new Vector2(0.94f, 0.92f),
            Vector2.zero,
            Vector2.zero);

        Image portraitImage = portraitImageObject.GetComponent<Image>();
        portraitImage.preserveAspect = true;

        TextMeshProUGUI portraitLabel = CreateText(
            isPlayer ? "PlayerPortraitLabel" : "MonsterPortraitLabel",
            portraitImageObject.transform,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            24f,
            FontStyles.Bold,
            new Color(0.95f, 0.87f, 0.74f));
        portraitLabel.alignment = TextAlignmentOptions.Center;
        portraitLabel.text = isPlayer ? "PLAYER" : "MONSTER";

        if (isPlayer)
        {
            playerPortraitImage = portraitImage;
            playerPortraitLabel = portraitLabel;
        }
        else
        {
            monsterPortraitImage = portraitImage;
            monsterPortraitLabel = portraitLabel;
        }
    }

    private void CreateDiceSlots()
    {
        if (diceTray == null || diceSlotImages.Count > 0)
            return;

        for (int i = 0; i < 5; i++)
        {
            GameObject slot = new GameObject($"DiceSlot{i + 1}", typeof(RectTransform), typeof(Image));
            slot.transform.SetParent(diceTray, false);

            RectTransform rect = slot.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(56f, 56f);

            Image image = slot.GetComponent<Image>();
            image.color = new Color(0.33f, 0.18f, 0.13f, 1f);

            TextMeshProUGUI pipText = CreateText(
                $"DiceSlot{i + 1}Pips",
                slot.transform,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                28f,
                FontStyles.Bold,
                Color.white);
            pipText.alignment = TextAlignmentOptions.Center;
            pipText.text = "•";

            diceSlotImages.Add(image);
            diceSlotTexts.Add(pipText);
        }
    }

    private void UpdateButtons()
    {
        if (rollButton != null)
        {
            bool awaitingContinue = CombatManager.Instance != null && CombatManager.Instance.IsAwaitingSceneContinue;
            bool inputNormal = GameManager.Instance == null || GameManager.Instance.CurrentInputMode == GameManager.InputMode.Normal;
            if (rollButtonLabel != null)
                rollButtonLabel.text = awaitingContinue ? "Continue" : "Roll";

            rollButton.interactable = awaitingContinue ||
                                      (inputNormal && GameManager.Instance != null && GameManager.Instance.CanRollButtonAct());

            RectTransform rollRect = rollButton.GetComponent<RectTransform>();
            if (rollRect != null)
            {
                bool showConfirm = ConfirmRollUI.Instance != null &&
                                   ConfirmRollUI.Instance.gameObject.activeSelf &&
                                   (CombatManager.Instance == null || !CombatManager.Instance.IsAwaitingSceneContinue);

                if (showConfirm)
                {
                    rollRect.anchorMin = new Vector2(0.04f, 0.14f);
                    rollRect.anchorMax = new Vector2(0.46f, 0.86f);
                }
                else
                {
                    rollRect.anchorMin = new Vector2(0.18f, 0.14f);
                    rollRect.anchorMax = new Vector2(0.82f, 0.86f);
                }

                rollRect.offsetMin = Vector2.zero;
                rollRect.offsetMax = Vector2.zero;
            }
        }

        if (confirmButton != null)
        {
            bool showConfirm = CombatManager.Instance == null || !CombatManager.Instance.IsAwaitingSceneContinue;
            showConfirm = showConfirm && ConfirmRollUI.Instance != null && ConfirmRollUI.Instance.gameObject.activeSelf;
            confirmButton.gameObject.SetActive(showConfirm);
            confirmButton.interactable = showConfirm &&
                                         (GameManager.Instance == null || GameManager.Instance.CurrentInputMode == GameManager.InputMode.Normal);
        }

        if (useTreasureButton != null)
        {
            PlayerPawn treasureUser = GameManager.Instance != null ? GameManager.Instance.GetCurrentTreasureUser() : null;
            bool hasTreasure = treasureUser != null &&
                               treasureUser.equippedTreasures != null &&
                               treasureUser.equippedTreasures.Count > 0;
            useTreasureButton.gameObject.SetActive(hasTreasure);
            useTreasureButton.interactable = hasTreasure &&
                                             GameManager.Instance != null &&
                                             GameManager.Instance.CanUseAnyActiveTreasure(treasureUser);
        }

        bool allowCards = CombatManager.Instance == null || !CombatManager.Instance.IsAwaitingSceneContinue;
        allowCards = allowCards && (GameManager.Instance == null || GameManager.Instance.CurrentInputMode == GameManager.InputMode.Normal);
        foreach (GameObject cardObject in combatHandCards)
        {
            if (cardObject == null)
                continue;

            Button cardButton = cardObject.GetComponent<Button>();
            if (cardButton != null)
                cardButton.interactable = allowCards;
        }
    }

    private void OnPrimaryButtonPressed()
    {
        if (CombatManager.Instance != null && CombatManager.Instance.IsAwaitingSceneContinue)
        {
            CombatManager.Instance.RequestCombatSceneContinue();
            return;
        }

        GameManager.Instance?.OnRollButtonPressed();
    }

    private void OnUseTreasurePressed()
    {
        GameManager.Instance?.OnUseTreasureButtonPressed();
    }

    private void UpdateCombatHand()
    {
        if (CombatSceneDirector.Instance != null && CombatSceneDirector.Instance.IsTransitioning)
            return;

        if (CombatManager.Instance == null || !CombatManager.Instance.IsCombatActive)
            return;

        PlayerPawn handOwner = GameManager.Instance != null ? GameManager.Instance.GetCurrentHandOwner() : null;
        HandUIManager.Instance?.EnterCombatPresentation(handOwner);

        int signature = GetHandSignature(handOwner);
        if (handOwner == lastHandOwner && signature == lastHandSignature)
            return;

        lastHandOwner = handOwner;
        lastHandSignature = signature;
        RebuildCombatHand(handOwner);
    }

    private void RebuildCombatHand(PlayerPawn handOwner)
    {
        foreach (GameObject cardObject in combatHandCards)
        {
            if (cardObject != null)
                Destroy(cardObject);
        }

        combatHandCards.Clear();

        if (handDock == null || handOwner == null || handOwner.hand == null)
            return;

        float cardWidth = 145f;
        float spacing = 14f;
        float startX = 0f;

        for (int i = 0; i < handOwner.hand.Count; i++)
        {
            ReprieveCard card = handOwner.hand[i];
            if (card == null)
                continue;

            GameObject cardObject = CreatePanel(
                $"CombatHandCard{i}",
                handDock,
                new Color(0.29f, 0.19f, 0.16f, 0.98f),
                Vector2.zero,
                Vector2.zero,
                Vector2.zero,
                Vector2.zero);

            RectTransform rect = cardObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(cardWidth, 110f);
            rect.anchoredPosition = new Vector2(startX + (cardWidth + spacing) * i, 0f);

            Button button = cardObject.AddComponent<Button>();
            ReprieveCard capturedCard = card;
            button.onClick.AddListener(() => OnCombatCardClicked(capturedCard));

            StandardCardVisual visual = StandardCardVisual.Ensure(cardObject.transform, StandardCardVisualDensity.Compact);
            visual?.ConfigureReprieve(card.cardName, card.description, true);

            combatHandCards.Add(cardObject);
        }
    }

    private void OnCombatCardClicked(ReprieveCard card)
    {
        if (card == null || HandUIManager.Instance == null)
            return;

        bool handledByInteractionMode = HandUIManager.Instance.OnCardClicked(card);
        if (handledByInteractionMode)
            return;

        HandUIManager.Instance.PlayCard(card);
        lastHandSignature = -1;
    }

    private void UpdateDiceTray()
    {
        int diceCount = 0;
        int[] displayedResults = null;
        if (CombatManager.Instance != null && CombatManager.Instance.IsCombatActive && CombatManager.Instance.CurrentPlayer != null)
        {
            diceCount = CombatManager.Instance.GetCurrentCombatDiceCount();
            displayedResults = CombatManager.Instance.GetDisplayedCombatRollResults();
        }

        for (int i = 0; i < diceSlotImages.Count; i++)
        {
            if (diceSlotImages[i] == null)
                continue;

            bool active = i < diceCount;
            diceSlotImages[i].color = active
                ? new Color(0.78f, 0.42f, 0.21f, 1f)
                : new Color(0.24f, 0.13f, 0.11f, 0.75f);

            if (i >= diceSlotTexts.Count || diceSlotTexts[i] == null)
                continue;

            if (!active)
            {
                diceSlotTexts[i].text = string.Empty;
            }
            else if (displayedResults != null && i < displayedResults.Length)
            {
                diceSlotTexts[i].text = displayedResults[i] > 0 ? displayedResults[i].ToString() : "•";
            }
            else
            {
                diceSlotTexts[i].text = "•";
            }
        }
    }

    private void UpdateVersusDisplay(CombatPresentationSnapshot snapshot)
    {
        if (playerAttackText == null || monsterMightText == null || diceCountText == null)
            return;

        int playerAttack = snapshot.playerAttackValue;
        int playerMight = snapshot.playerMightValue;
        int playerRoll = snapshot.playerRollValue;
        int monsterDefense = snapshot.monsterDefenseValue;
        int diceCount = snapshot.diceCountValue > 0 ? snapshot.diceCountValue : 0;

        string playerRollTextValue;
        string playerTotalTextValue;

        if (snapshot.hasResolvedRoll || diceCount <= 0)
        {
            playerRollTextValue = playerRoll.ToString();
            playerTotalTextValue = playerAttack.ToString();
        }
        else
        {
            playerRollTextValue = "-";
            playerTotalTextValue = "-";
        }

        playerAttackText.text =
            $"Might\n{playerMight}\n" +
            $"Roll\n{playerRollTextValue}\n" +
            $"Total\n{playerTotalTextValue}";

        monsterMightText.text =
            $"Might\n{monsterDefense}\n" +
            $"Total\n{monsterDefense}";

        diceCountText.text = diceCount > 0 ? $"Dice: {diceCount}" : string.Empty;
    }

    private void ApplyResultPresentation(CombatPresentationSnapshot snapshot)
    {
        if (statusText == null || rollText == null)
            return;

        string rawStatus = string.IsNullOrWhiteSpace(snapshot.statusText)
            ? "Combat ready."
            : snapshot.statusText.Trim();

        string headline = rawStatus;
        string detail = string.Empty;

        int splitIndex = rawStatus.IndexOf(". ");
        if (splitIndex >= 0)
        {
            headline = rawStatus.Substring(0, splitIndex + 1).Trim();
            detail = rawStatus.Substring(splitIndex + 2).Trim();
        }
        else if (rawStatus.EndsWith("Click Continue."))
        {
            headline = rawStatus.Replace("Click Continue.", string.Empty).Trim();
            detail = "Click Continue.";
        }

        bool showInlineRollBreakdown = string.IsNullOrWhiteSpace(snapshot.mode) ||
                                       (snapshot.mode != "combat" && snapshot.mode != "pilfer");

        if (showInlineRollBreakdown && !string.IsNullOrWhiteSpace(snapshot.rollText))
            detail = AppendDetail(detail, snapshot.rollText.Trim());

        statusText.text = headline;
        rollText.text = detail;
        rollText.gameObject.SetActive(!string.IsNullOrWhiteSpace(detail));

        ApplyResultTone(DetermineResultTone(snapshot, rawStatus));
    }

    private static string AppendDetail(string existing, string next)
    {
        if (string.IsNullOrWhiteSpace(next))
            return existing;

        if (string.IsNullOrWhiteSpace(existing))
            return next;

        if (existing.Contains(next))
            return existing;

        return $"{existing}\n{next}";
    }

    private ResultTone DetermineResultTone(CombatPresentationSnapshot snapshot, string status)
    {
        string lowered = status.ToLowerInvariant();
        if (lowered.Contains("rolling") || lowered.Contains("ready") || lowered.Contains("confirm"))
            return ResultTone.Ready;

        if (lowered.Contains("tie"))
            return ResultTone.Warning;

        if (lowered.Contains("takes") || lowered.Contains("burns") || lowered.Contains("failed") || lowered.Contains("fell to 0 hp") || lowered.Contains("prevents runaway"))
        {
            if (ContainsSnapshotName(status, snapshot.opponentName) && lowered.Contains("takes"))
                return ResultTone.Success;

            if (ContainsSnapshotName(status, snapshot.playerName) && lowered.Contains("takes"))
                return ResultTone.Danger;

            if (lowered.Contains("failed"))
                return ResultTone.Danger;

            return ResultTone.Warning;
        }

        if (lowered.Contains("escaped") || lowered.Contains("was defeated") || lowered.Contains("victory") || lowered.Contains("defeated"))
        {
            if (ContainsSnapshotName(status, snapshot.playerName) && lowered.Contains("was defeated"))
                return ResultTone.Danger;

            if (ContainsSnapshotName(status, snapshot.opponentName) && lowered.Contains("was defeated"))
                return ResultTone.Success;

            if (lowered.Contains("escaped"))
                return ResultTone.Success;

            return ResultTone.Success;
        }

        return ResultTone.Neutral;
    }

    private static bool ContainsSnapshotName(string status, string snapshotName)
    {
        if (string.IsNullOrWhiteSpace(status) || string.IsNullOrWhiteSpace(snapshotName))
            return false;

        if (status.Contains(snapshotName))
            return true;

        string[] parts = snapshotName.Split(' ');
        if (parts.Length == 0)
            return false;

        string tail = parts[parts.Length - 1];
        return !string.IsNullOrWhiteSpace(tail) && status.Contains(tail);
    }

    private void ApplyResultTone(ResultTone tone)
    {
        if (resultBoxImage == null || resultAccentImage == null || statusText == null || rollText == null)
            return;

        switch (tone)
        {
            case ResultTone.Ready:
                resultBoxImage.color = new Color(0.15f, 0.16f, 0.22f, 0.98f);
                resultAccentImage.color = new Color(0.40f, 0.60f, 0.90f, 1f);
                statusText.color = new Color(0.92f, 0.96f, 1f, 1f);
                rollText.color = new Color(0.78f, 0.86f, 0.96f, 1f);
                break;
            case ResultTone.Success:
                resultBoxImage.color = new Color(0.11f, 0.19f, 0.14f, 0.98f);
                resultAccentImage.color = new Color(0.44f, 0.78f, 0.42f, 1f);
                statusText.color = new Color(0.94f, 1f, 0.92f, 1f);
                rollText.color = new Color(0.84f, 0.95f, 0.82f, 1f);
                break;
            case ResultTone.Danger:
                resultBoxImage.color = new Color(0.24f, 0.11f, 0.11f, 0.98f);
                resultAccentImage.color = new Color(0.86f, 0.33f, 0.28f, 1f);
                statusText.color = new Color(1f, 0.94f, 0.92f, 1f);
                rollText.color = new Color(0.96f, 0.80f, 0.76f, 1f);
                break;
            case ResultTone.Warning:
                resultBoxImage.color = new Color(0.24f, 0.16f, 0.08f, 0.98f);
                resultAccentImage.color = new Color(0.92f, 0.62f, 0.20f, 1f);
                statusText.color = new Color(1f, 0.97f, 0.90f, 1f);
                rollText.color = new Color(0.98f, 0.86f, 0.72f, 1f);
                break;
            default:
                resultBoxImage.color = new Color(0.18f, 0.11f, 0.09f, 0.98f);
                resultAccentImage.color = new Color(0.80f, 0.38f, 0.13f, 1f);
                statusText.color = Color.white;
                rollText.color = new Color(0.92f, 0.90f, 0.86f, 1f);
                break;
        }
    }

    private void UpdatePortraits(CombatPresentationSnapshot snapshot)
    {
        if (playerPortraitImage != null)
        {
            Sprite playerSprite = null;
            if (CombatManager.Instance != null && CombatManager.Instance.CurrentPlayer != null)
            {
                SpriteRenderer renderer = CombatManager.Instance.CurrentPlayer.GetComponent<SpriteRenderer>();
                if (renderer != null)
                    playerSprite = renderer.sprite;
            }

            playerPortraitImage.sprite = playerSprite;
            playerPortraitImage.color = playerSprite != null
                ? Color.white
                : new Color(0.26f, 0.19f, 0.16f, 1f);
        }

        if (playerPortraitLabel != null)
        {
            bool hasPlayerSprite = playerPortraitImage != null && playerPortraitImage.sprite != null;
            if (hasPlayerSprite)
            {
                playerPortraitLabel.text = string.Empty;
            }
            else
            {
                string[] playerNameParts = !string.IsNullOrWhiteSpace(snapshot.playerName)
                    ? snapshot.playerName.Split(' ')
                    : null;
                playerPortraitLabel.text = playerNameParts != null && playerNameParts.Length > 0
                    ? playerNameParts[playerNameParts.Length - 1].ToUpperInvariant()
                    : "PLAYER";
            }
        }

        if (monsterPortraitImage != null)
        {
            monsterPortraitImage.sprite = placeholderMonsterSprite;
            monsterPortraitImage.color = placeholderMonsterSprite != null ? Color.white : new Color(0.26f, 0.19f, 0.16f, 1f);
        }

        if (monsterPortraitLabel != null)
            monsterPortraitLabel.text = placeholderMonsterSprite == null ? "MONSTER" : string.Empty;
    }

    private void LoadPlaceholderSprite()
    {
        if (placeholderMonsterSprite != null)
            return;

        placeholderMonsterSprite = Resources.Load<Sprite>("Combat/monster-placeholder");
    }

    private int GetHandSignature(PlayerPawn handOwner)
    {
        if (handOwner == null || handOwner.hand == null)
            return 0;

        int signature = handOwner.hand.Count;
        foreach (ReprieveCard card in handOwner.hand)
            signature = (signature * 397) ^ (card != null ? card.GetInstanceID() : 0);

        return signature;
    }

    private int GetCombatDiceCount(int arcane)
    {
        if (arcane <= -3)
            return 0;
        if (arcane <= 2)
            return 1;
        if (arcane <= 5)
            return 2;
        if (arcane <= 8)
            return 3;
        if (arcane <= 11)
            return 4;
        return 5;
    }
}
