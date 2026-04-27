using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DiceValueSelectionUI : MonoBehaviour
{
    public static DiceValueSelectionUI Instance;

    private GameObject panel;
    private TextMeshProUGUI headerText;
    private readonly List<Button> buttons = new List<Button>();
    private Action<int> onValueSelected;

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

    public static DiceValueSelectionUI EnsureExists()
    {
        if (Instance != null)
            return Instance;

        DiceValueSelectionUI existing = FindFirstObjectByType<DiceValueSelectionUI>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject managerObject = new GameObject("DiceValueSelectionUI");
        DiceValueSelectionUI manager = managerObject.AddComponent<DiceValueSelectionUI>();
        manager.EnsureRuntimeUI();
        manager.Hide();
        return manager;
    }

    public void Show(string prompt, Action<int> onSelected)
    {
        EnsureRuntimeUI();
        onValueSelected = onSelected;
        headerText.text = prompt;
        panel.SetActive(true);
    }

    public void Hide()
    {
        if (panel != null)
            panel.SetActive(false);

        onValueSelected = null;
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

        panel = new GameObject("DiceValueSelectionPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(560f, 220f);
        panelRect.anchoredPosition = new Vector2(0f, 220f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.10f, 0.10f, 0.14f, 0.96f);

        GameObject headerObject = new GameObject("Header", typeof(RectTransform), typeof(TextMeshProUGUI));
        headerObject.transform.SetParent(panel.transform, false);
        RectTransform headerRect = headerObject.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.sizeDelta = new Vector2(0f, 56f);
        headerRect.anchoredPosition = new Vector2(0f, -12f);

        headerText = headerObject.GetComponent<TextMeshProUGUI>();
        headerText.fontSize = 24f;
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.color = Color.white;

        for (int i = 0; i < 6; i++)
        {
            int value = i + 1;
            GameObject buttonObject = new GameObject($"Value_{value}", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(panel.transform, false);

            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0f, 0f);
            buttonRect.anchorMax = new Vector2(0f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(72f, 72f);
            buttonRect.anchoredPosition = new Vector2(-180f + (i * 72f), -54f);

            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = new Color(0.26f, 0.20f, 0.13f, 0.98f);

            Button button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(() => SelectValue(value));

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.fontSize = 28f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.text = value.ToString();

            buttons.Add(button);
        }
    }

    private void SelectValue(int value)
    {
        Action<int> callback = onValueSelected;
        Hide();
        callback?.Invoke(value);
    }
}
