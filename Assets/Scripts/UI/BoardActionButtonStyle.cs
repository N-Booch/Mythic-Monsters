using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BoardActionButtonStyle : MonoBehaviour
{
    public enum Variant
    {
        Primary,
        Secondary,
        Confirm,
        Danger
    }

    [SerializeField] private Variant variant = Variant.Secondary;

    private Button button;
    private Image background;
    private Image accent;
    private TMP_Text tmpLabel;
    private Text legacyLabel;
    private bool lastInteractable;
    private Variant lastVariant;

    public static BoardActionButtonStyle Attach(Button target, Variant styleVariant)
    {
        if (target == null)
            return null;

        BoardActionButtonStyle style = target.GetComponent<BoardActionButtonStyle>();
        if (style == null)
            style = target.gameObject.AddComponent<BoardActionButtonStyle>();

        style.variant = styleVariant;
        style.CacheReferences();
        style.ApplyNow();
        return style;
    }

    private void Awake()
    {
        CacheReferences();
        ApplyNow();
    }

    private void OnEnable()
    {
        CacheReferences();
        ApplyNow();
    }

    private void Update()
    {
        if (button == null)
            CacheReferences();

        if (button == null)
            return;

        if (button.interactable != lastInteractable || variant != lastVariant)
            ApplyNow();
    }

    public void ApplyNow()
    {
        CacheReferences();
        if (button == null || background == null)
            return;

        lastInteractable = button.interactable;
        lastVariant = variant;

        ButtonColors colors = GetColors(variant, button.interactable);
        background.color = colors.Background;
        if (accent != null)
            accent.color = colors.Accent;

        ApplySelectableColors(button);
        ApplyLabel(colors.Text);
    }

    private void CacheReferences()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (background == null)
        {
            background = GetComponent<Image>();
            if (background == null)
                background = gameObject.AddComponent<Image>();
        }

        if (button != null && button.targetGraphic == null)
            button.targetGraphic = background;

        if (accent == null)
            accent = EnsureAccent();

        if (tmpLabel == null)
            tmpLabel = GetComponentInChildren<TMP_Text>(true);

        if (legacyLabel == null)
            legacyLabel = GetComponentInChildren<Text>(true);
    }

    private Image EnsureAccent()
    {
        const string accentName = "ActionButtonAccent";
        Transform existing = transform.Find(accentName);
        if (existing != null)
            return existing.GetComponent<Image>();

        GameObject accentObject = new GameObject(accentName, typeof(RectTransform), typeof(Image));
        accentObject.transform.SetParent(transform, false);
        accentObject.transform.SetAsFirstSibling();

        RectTransform rect = accentObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(0f, -4f);
        rect.offsetMax = Vector2.zero;

        return accentObject.GetComponent<Image>();
    }

    private void ApplySelectableColors(Button target)
    {
        ColorBlock colors = target.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = Color.white;
        colors.colorMultiplier = 1f;
        target.colors = colors;
        target.transition = Selectable.Transition.ColorTint;
    }

    private void ApplyLabel(Color labelColor)
    {
        if (tmpLabel != null)
        {
            tmpLabel.color = labelColor;
            tmpLabel.fontStyle = FontStyles.Bold;
            tmpLabel.fontSize = Mathf.Max(tmpLabel.fontSize, 22f);
            return;
        }

        if (legacyLabel != null)
        {
            legacyLabel.color = labelColor;
            legacyLabel.fontStyle = FontStyle.Bold;
            legacyLabel.fontSize = Mathf.Max(legacyLabel.fontSize, 20);
        }
    }

    private static ButtonColors GetColors(Variant styleVariant, bool interactable)
    {
        if (!interactable)
        {
            return new ButtonColors(
                new Color(0.08f, 0.07f, 0.06f, 0.78f),
                new Color(0.20f, 0.16f, 0.12f, 0.65f),
                new Color(0.56f, 0.50f, 0.42f, 0.72f));
        }

        switch (styleVariant)
        {
            case Variant.Primary:
                return new ButtonColors(
                    new Color(0.54f, 0.27f, 0.12f, 0.98f),
                    new Color(0.90f, 0.56f, 0.24f, 1f),
                    new Color(1f, 0.93f, 0.82f, 1f));
            case Variant.Confirm:
                return new ButtonColors(
                    new Color(0.44f, 0.24f, 0.10f, 0.98f),
                    new Color(0.78f, 0.48f, 0.20f, 1f),
                    new Color(1f, 0.92f, 0.78f, 1f));
            case Variant.Danger:
                return new ButtonColors(
                    new Color(0.34f, 0.11f, 0.09f, 0.98f),
                    new Color(0.74f, 0.26f, 0.18f, 1f),
                    new Color(1f, 0.88f, 0.82f, 1f));
            default:
                return new ButtonColors(
                    new Color(0.22f, 0.15f, 0.11f, 0.96f),
                    new Color(0.58f, 0.36f, 0.18f, 1f),
                    new Color(0.96f, 0.89f, 0.78f, 1f));
        }
    }

    private readonly struct ButtonColors
    {
        public ButtonColors(Color background, Color accent, Color text)
        {
            Background = background;
            Accent = accent;
            Text = text;
        }

        public Color Background { get; }
        public Color Accent { get; }
        public Color Text { get; }
    }
}
