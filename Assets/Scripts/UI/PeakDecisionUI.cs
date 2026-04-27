using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PeakDecisionUI : MonoBehaviour
{
    public static PeakDecisionUI Instance;

    private GameObject panel;
    private TextMeshProUGUI promptText;
    private Button yesButton;
    private Button noButton;

    private Action onConfirm;
    private Action onCancel;

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

    public static PeakDecisionUI EnsureExists()
    {
        if (Instance != null)
            return Instance;

        PeakDecisionUI existing = FindFirstObjectByType<PeakDecisionUI>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject managerObject = new GameObject("PeakDecisionUI");
        PeakDecisionUI manager = managerObject.AddComponent<PeakDecisionUI>();
        manager.EnsureRuntimeUI();
        manager.Hide();
        return manager;
    }

    public void Show(string prompt, Action confirmAction, Action cancelAction)
    {
        Show(prompt, "Fight", "Stay", confirmAction, cancelAction);
    }

    public void Show(string prompt, string confirmLabel, string cancelLabel, Action confirmAction, Action cancelAction)
    {
        EnsureRuntimeUI();

        onConfirm = confirmAction;
        onCancel = cancelAction;
        promptText.text = prompt;
        SetButtonLabel(yesButton, confirmLabel);
        SetButtonLabel(noButton, cancelLabel);
        panel.SetActive(true);
    }

    public void Hide()
    {
        if (panel != null)
            panel.SetActive(false);

        onConfirm = null;
        onCancel = null;
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

        panel = new GameObject("PeakDecisionPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(760f, 320f);
        panelRect.anchoredPosition = Vector2.zero;

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.10f, 0.09f, 0.14f, 0.96f);

        GameObject promptObject = new GameObject("Prompt", typeof(RectTransform), typeof(TextMeshProUGUI));
        promptObject.transform.SetParent(panel.transform, false);

        RectTransform promptRect = promptObject.GetComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(0f, 1f);
        promptRect.anchorMax = new Vector2(1f, 1f);
        promptRect.pivot = new Vector2(0.5f, 1f);
        promptRect.sizeDelta = new Vector2(0f, 150f);
        promptRect.anchoredPosition = new Vector2(0f, -28f);

        promptText = promptObject.GetComponent<TextMeshProUGUI>();
        promptText.fontSize = 30f;
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.textWrappingMode = TextWrappingModes.Normal;
        promptText.color = Color.white;

        yesButton = CreateButton(panel.transform, "Fight", new Vector2(-130f, -110f), OnYesClicked);
        noButton = CreateButton(panel.transform, "Stay", new Vector2(130f, -110f), OnNoClicked);
    }

    private Button CreateButton(Transform parent, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(200f, 68f);
        buttonRect.anchoredPosition = anchoredPosition;

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.23f, 0.19f, 0.11f, 1f);

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
        labelText.fontSize = 26f;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = Color.white;
        labelText.text = label;

        return button;
    }

    private void OnYesClicked()
    {
        Action callback = onConfirm;
        Hide();
        callback?.Invoke();
    }

    private void OnNoClicked()
    {
        Action callback = onCancel;
        Hide();
        callback?.Invoke();
    }

    private void SetButtonLabel(Button button, string label)
    {
        if (button == null)
            return;

        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
            text.text = label;
    }
}
