using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum StandardCardVisualTheme
{
    Reprieve,
    Treasure,
    TreasureDisabled
}

public enum StandardCardVisualDensity
{
    Full,
    Compact
}

public class StandardCardVisual : MonoBehaviour
{
    public Image Background;
    public Image AccentBar;
    public Image ArtPanel;
    public Image FooterPanel;
    public TextMeshProUGUI TitleText;
    public TextMeshProUGUI TypeText;
    public TextMeshProUGUI DescriptionText;
    public TextMeshProUGUI FooterText;

    private const string ViewName = "StandardCardVisual";

    public static StandardCardVisual Ensure(Transform parent, StandardCardVisualDensity density = StandardCardVisualDensity.Full)
    {
        if (parent == null)
            return null;

        Transform existing = parent.Find(ViewName);
        if (existing != null)
        {
            existing.SetAsLastSibling();
            StandardCardVisual existingView = existing.GetComponent<StandardCardVisual>();
            if (existingView != null)
            {
                existingView.ApplyDensity(density);
                return existingView;
            }
        }

        GameObject rootObject = new GameObject(ViewName, typeof(RectTransform), typeof(Image));
        rootObject.transform.SetParent(parent, false);
        rootObject.transform.SetAsLastSibling();

        RectTransform root = rootObject.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        StandardCardVisual view = rootObject.AddComponent<StandardCardVisual>();
        view.Background = rootObject.GetComponent<Image>();
        view.Background.raycastTarget = false;

        view.AccentBar = CreatePanel("AccentBar", rootObject.transform, new Vector2(0f, 0.985f), Vector2.one);
        view.TitleText = CreateText("Title", rootObject.transform, FontStyles.Bold, TextAlignmentOptions.Center);
        view.ArtPanel = CreatePanel("ArtPanel", rootObject.transform, Vector2.zero, Vector2.zero);
        view.TypeText = CreateText("Type", view.ArtPanel.transform, FontStyles.Bold, TextAlignmentOptions.Center);
        view.DescriptionText = CreateText("Description", rootObject.transform, FontStyles.Normal, TextAlignmentOptions.Center);
        view.FooterPanel = CreatePanel("FooterPanel", rootObject.transform, Vector2.zero, Vector2.zero);
        view.FooterText = CreateText("Footer", view.FooterPanel.transform, FontStyles.Bold, TextAlignmentOptions.Center);

        view.ApplyDensity(density);
        return view;
    }

    public void ConfigureReprieve(string title, string description, bool compact = false)
    {
        Configure(
            title,
            "REPRIEVE",
            description,
            string.Empty,
            StandardCardVisualTheme.Reprieve,
            compact ? StandardCardVisualDensity.Compact : StandardCardVisualDensity.Full);
    }

    public void ConfigureTreasure(string title, string description, string footer, bool canUse = true, bool compact = false)
    {
        Configure(
            title,
            "TREASURE",
            description,
            footer,
            canUse ? StandardCardVisualTheme.Treasure : StandardCardVisualTheme.TreasureDisabled,
            compact ? StandardCardVisualDensity.Compact : StandardCardVisualDensity.Full);
    }

    public void Configure(
        string title,
        string typeLabel,
        string description,
        string footer,
        StandardCardVisualTheme theme,
        StandardCardVisualDensity density)
    {
        ApplyDensity(density);
        ApplyTheme(theme);

        if (TitleText != null)
            TitleText.text = string.IsNullOrWhiteSpace(title) ? "Card" : title;

        if (TypeText != null)
            TypeText.text = string.IsNullOrWhiteSpace(typeLabel) ? "CARD" : typeLabel.ToUpperInvariant();

        if (DescriptionText != null)
            DescriptionText.text = string.IsNullOrWhiteSpace(description) ? "No description." : description;

        bool showFooter = !string.IsNullOrWhiteSpace(footer);
        if (FooterPanel != null)
            FooterPanel.gameObject.SetActive(showFooter);

        if (FooterText != null)
            FooterText.text = footer ?? string.Empty;
    }

    private void ApplyDensity(StandardCardVisualDensity density)
    {
        RectTransform titleRect = TitleText != null ? TitleText.rectTransform : null;
        RectTransform artRect = ArtPanel != null ? ArtPanel.rectTransform : null;
        RectTransform typeRect = TypeText != null ? TypeText.rectTransform : null;
        RectTransform descriptionRect = DescriptionText != null ? DescriptionText.rectTransform : null;
        RectTransform footerRect = FooterPanel != null ? FooterPanel.rectTransform : null;
        RectTransform footerTextRect = FooterText != null ? FooterText.rectTransform : null;

        if (density == StandardCardVisualDensity.Compact)
        {
            SetAnchors(titleRect, new Vector2(0.06f, 0.74f), new Vector2(0.94f, 0.94f), new Vector4(3f, 1f, 3f, 1f));
            SetAnchors(artRect, new Vector2(0.22f, 0.44f), new Vector2(0.78f, 0.70f));
            SetAnchors(typeRect, Vector2.zero, Vector2.one, new Vector4(2f, 0f, 2f, 0f));
            SetAnchors(descriptionRect, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.40f), new Vector4(3f, 1f, 3f, 1f));
            SetAnchors(footerRect, new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.16f));
            SetAnchors(footerTextRect, Vector2.zero, Vector2.one, new Vector4(2f, 0f, 2f, 0f));
            ConfigureText(TitleText, 16f, 11f, TextOverflowModes.Ellipsis);
            ConfigureText(TypeText, 9f, 7f, TextOverflowModes.Ellipsis);
            ConfigureText(DescriptionText, 10.5f, 7.5f, TextOverflowModes.Ellipsis);
            ConfigureText(FooterText, 11f, 8f, TextOverflowModes.Ellipsis);
            return;
        }

        SetAnchors(titleRect, new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.96f), new Vector4(5f, 2f, 5f, 2f));
        SetAnchors(artRect, new Vector2(0.18f, 0.48f), new Vector2(0.82f, 0.78f));
        SetAnchors(typeRect, Vector2.zero, Vector2.one, new Vector4(4f, 2f, 4f, 2f));
        SetAnchors(descriptionRect, new Vector2(0.08f, 0.16f), new Vector2(0.92f, 0.45f), new Vector4(5f, 2f, 5f, 2f));
        SetAnchors(footerRect, new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.14f));
        SetAnchors(footerTextRect, Vector2.zero, Vector2.one, new Vector4(4f, 1f, 4f, 1f));
        ConfigureText(TitleText, 22f, 14f, TextOverflowModes.Ellipsis);
        ConfigureText(TypeText, 12f, 9f, TextOverflowModes.Ellipsis);
        ConfigureText(DescriptionText, 18f, 10f, TextOverflowModes.Ellipsis);
        ConfigureText(FooterText, 16f, 11f, TextOverflowModes.Ellipsis);
    }

    private void ApplyTheme(StandardCardVisualTheme theme)
    {
        Color background;
        Color accent;
        Color art;
        Color footer;
        Color title;
        Color body;
        Color type;

        switch (theme)
        {
            case StandardCardVisualTheme.Reprieve:
                background = new Color(0.42f, 0.78f, 0.86f, 1f);
                accent = new Color(0.82f, 0.96f, 1f, 1f);
                art = new Color(0.20f, 0.50f, 0.60f, 0.42f);
                footer = new Color(0.16f, 0.42f, 0.50f, 0.82f);
                title = Color.white;
                body = new Color(0.05f, 0.10f, 0.12f, 1f);
                type = new Color(0.88f, 0.98f, 1f, 0.92f);
                break;

            case StandardCardVisualTheme.TreasureDisabled:
                background = new Color(0.30f, 0.30f, 0.28f, 0.72f);
                accent = new Color(0.64f, 0.58f, 0.42f, 0.72f);
                art = new Color(0.29f, 0.25f, 0.17f, 0.42f);
                footer = new Color(0.20f, 0.16f, 0.10f, 0.70f);
                title = new Color(0.82f, 0.82f, 0.78f, 0.92f);
                body = new Color(0.08f, 0.08f, 0.07f, 0.92f);
                type = new Color(0.78f, 0.70f, 0.48f, 0.78f);
                break;

            default:
                background = new Color(0.78f, 0.68f, 0.36f, 0.95f);
                accent = new Color(0.98f, 0.88f, 0.42f, 1f);
                art = new Color(0.34f, 0.25f, 0.12f, 0.58f);
                footer = new Color(0.28f, 0.21f, 0.08f, 0.86f);
                title = Color.white;
                body = new Color(0.05f, 0.04f, 0.02f, 1f);
                type = new Color(0.98f, 0.86f, 0.56f, 0.92f);
                break;
        }

        if (Background != null) Background.color = background;
        if (AccentBar != null) AccentBar.color = accent;
        if (ArtPanel != null) ArtPanel.color = art;
        if (FooterPanel != null) FooterPanel.color = footer;
        if (TitleText != null) TitleText.color = title;
        if (DescriptionText != null) DescriptionText.color = body;
        if (TypeText != null) TypeText.color = type;
        if (FooterText != null) FooterText.color = Color.white;
    }

    private static Image CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        Image image = panelObject.GetComponent<Image>();
        image.raycastTarget = false;
        SetAnchors(panelObject.GetComponent<RectTransform>(), anchorMin, anchorMax);
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.enableAutoSizing = true;
        text.fontStyle = style;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static void ConfigureText(TextMeshProUGUI text, float maxSize, float minSize, TextOverflowModes overflow)
    {
        if (text == null)
            return;

        text.enableAutoSizing = true;
        text.fontSizeMax = maxSize;
        text.fontSizeMin = minSize;
        text.fontSize = maxSize;
        text.overflowMode = overflow;
        text.textWrappingMode = TextWrappingModes.Normal;
    }

    private static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        SetAnchors(rect, anchorMin, anchorMax, Vector4.zero);
    }

    private static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector4 margin)
    {
        if (rect == null)
            return;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(margin.x, margin.y);
        rect.offsetMax = new Vector2(-margin.z, -margin.w);
    }
}
