using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MonsterSelectionUI : MonoBehaviour
{
    public static MonsterSelectionUI Instance;

    private GameObject panel;
    private RectTransform contentRoot;
    private TextMeshProUGUI headerText;
    private Action<Monster> onMonsterSelected;

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

    public static MonsterSelectionUI EnsureExists()
    {
        if (Instance != null)
            return Instance;

        MonsterSelectionUI existing = FindFirstObjectByType<MonsterSelectionUI>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject managerObject = new GameObject("MonsterSelectionUI");
        MonsterSelectionUI manager = managerObject.AddComponent<MonsterSelectionUI>();
        manager.EnsureRuntimeUI();
        manager.Hide();
        return manager;
    }

    public void BeginSelection(PlayerPawn player, Monster.MonsterBiome biome, List<Monster> monsters, Action<Monster> onSelected)
    {
        EnsureRuntimeUI();

        onMonsterSelected = onSelected;
        headerText.text = $"{player.playerName}: choose a {(biome == Monster.MonsterBiome.Peak ? "Mythic Monster" : biome + " monster")} to fight";
        RebuildEntries(monsters);
        panel.SetActive(true);
    }

    public void Hide()
    {
        if (panel != null)
            panel.SetActive(false);

        onMonsterSelected = null;
    }

    private void EnsureRuntimeUI()
    {
        if (panel != null && contentRoot != null && headerText != null)
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

        panel = new GameObject("MonsterSelectionPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(780f, 640f);
        panelRect.anchoredPosition = Vector2.zero;

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.08f, 0.09f, 0.12f, 0.95f);

        GameObject headerObject = new GameObject("Header", typeof(RectTransform), typeof(TextMeshProUGUI));
        headerObject.transform.SetParent(panel.transform, false);
        RectTransform headerRect = headerObject.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.sizeDelta = new Vector2(0f, 80f);
        headerRect.anchoredPosition = new Vector2(0f, -20f);

        headerText = headerObject.GetComponent<TextMeshProUGUI>();
        headerText.fontSize = 30f;
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.color = Color.white;

        GameObject contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.transform.SetParent(panel.transform, false);
        contentRoot = contentObject.GetComponent<RectTransform>();
        contentRoot.anchorMin = new Vector2(0.5f, 0.5f);
        contentRoot.anchorMax = new Vector2(0.5f, 0.5f);
        contentRoot.pivot = new Vector2(0.5f, 0.5f);
        contentRoot.sizeDelta = new Vector2(700f, 500f);
        contentRoot.anchoredPosition = new Vector2(0f, -35f);
    }

    private void RebuildEntries(List<Monster> monsters)
    {
        foreach (GameObject entry in spawnedEntries)
        {
            if (entry != null)
                Destroy(entry);
        }

        spawnedEntries.Clear();

        if (monsters == null)
            return;

        for (int i = 0; i < monsters.Count; i++)
        {
            Monster monster = monsters[i];
            if (monster == null)
                continue;

            bool isRevealed = MonsterRoster.Instance != null && MonsterRoster.Instance.IsRevealed(monster);
            spawnedEntries.Add(CreateMonsterEntry(monster, i, isRevealed));
        }
    }

    private GameObject CreateMonsterEntry(Monster monster, int index, bool isRevealed)
    {
        GameObject entryObject = new GameObject($"MonsterEntry_{index}", typeof(RectTransform), typeof(Image), typeof(Button));
        entryObject.transform.SetParent(contentRoot, false);

        RectTransform entryRect = entryObject.GetComponent<RectTransform>();
        entryRect.anchorMin = new Vector2(0.5f, 1f);
        entryRect.anchorMax = new Vector2(0.5f, 1f);
        entryRect.pivot = new Vector2(0.5f, 1f);
        entryRect.sizeDelta = new Vector2(680f, 88f);
        entryRect.anchoredPosition = new Vector2(0f, -96f * index);

        Image background = entryObject.GetComponent<Image>();
        background.color = isRevealed
            ? new Color(0.16f, 0.20f, 0.18f, 0.95f)
            : new Color(0.18f, 0.14f, 0.12f, 0.95f);

        Button button = entryObject.GetComponent<Button>();
        button.onClick.AddListener(() => SelectMonster(monster));

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(entryObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 8f);
        labelRect.offsetMax = new Vector2(-12f, -8f);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.textWrappingMode = TextWrappingModes.Normal;
        label.fontSize = 22f;
        label.alignment = TextAlignmentOptions.Left;
        label.color = Color.white;
        label.text = BuildMonsterLabel(monster, index, isRevealed);

        return entryObject;
    }

    private string BuildMonsterLabel(Monster monster, int index, bool isRevealed)
    {
        if (!isRevealed)
            return $"Hidden Monster {index + 1}\nUnrevealed";

        string reward = string.IsNullOrWhiteSpace(monster.RewardTitleName)
            ? "Mythic"
            : monster.RewardTitleName;

        return $"{monster.monsterName}  |  HP {monster.maxHP}  |  Might {monster.might}  |  Reward: {reward}";
    }

    private void SelectMonster(Monster monster)
    {
        Action<Monster> callback = onMonsterSelected;
        Hide();
        callback?.Invoke(monster);
    }
}
