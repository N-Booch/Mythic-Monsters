using UnityEngine;
using UnityEngine.UI;

public class BoardActionBarLayoutUI : MonoBehaviour
{
    private const string RuntimeObjectName = "BoardActionBarLayout";
    private const string PanelName = "BoardActionBarPanel";
    private const string AccentName = "ActionBarAccent";
    private const string InnerName = "ActionBarInner";
    private const string GlowName = "ActionBarGlow";
    private const string RollWellName = "RollWell";
    private const string DiceWellName = "DiceWell";
    private const string EndTurnWellName = "EndTurnWell";
    private const string HandWellName = "HandWell";
    private const string ConfirmWellName = "ConfirmWell";

    private RectTransform panelRect;
    private Image panelImage;
    private Image accentImage;
    private Image innerImage;
    private Image glowImage;

    public static BoardActionBarLayoutUI EnsureExists()
    {
        BoardActionBarLayoutUI existing = FindFirstObjectByType<BoardActionBarLayoutUI>();
        if (existing != null)
        {
            existing.ApplyLayout();
            return existing;
        }

        Canvas canvas = GetMainCanvas();
        GameObject host = new GameObject(RuntimeObjectName, typeof(RectTransform), typeof(BoardActionBarLayoutUI));
        if (canvas != null)
            host.transform.SetParent(canvas.transform, false);

        BoardActionBarLayoutUI layout = host.GetComponent<BoardActionBarLayoutUI>();
        layout.ApplyLayout();
        return layout;
    }

    private void Awake()
    {
        ApplyLayout();
    }

    public void SetVisible(bool visible)
    {
        if (panelRect == null)
            ApplyLayout();

        if (panelRect != null)
            panelRect.gameObject.SetActive(visible);
    }

    public void ApplyLayout()
    {
        Canvas canvas = GetMainCanvas();
        if (canvas == null)
            return;

        EnsurePanel(canvas.transform);

        RollButtonUI roll = FindFirstObjectByType<RollButtonUI>(FindObjectsInactive.Include);
        EndTurnButtonUI endTurn = FindFirstObjectByType<EndTurnButtonUI>(FindObjectsInactive.Include);
        SeeHandButtonUI seeHand = FindFirstObjectByType<SeeHandButtonUI>(FindObjectsInactive.Include);
        DiceUI dice = FindFirstObjectByType<DiceUI>(FindObjectsInactive.Include);
        ConfirmRollUI confirmRoll = FindFirstObjectByType<ConfirmRollUI>(FindObjectsInactive.Include);

        Vector2 rollAnchor = new Vector2(0.17f, 0f);
        Vector2 diceAnchor = new Vector2(0.33f, 0f);
        Vector2 endTurnAnchor = new Vector2(0.50f, 0f);
        Vector2 seeHandAnchor = new Vector2(0.83f, 0f);
        Vector2 confirmAnchor = new Vector2(0.50f, 0f);

        Vector2 controlPosition = new Vector2(0f, 88f);
        Vector2 confirmPosition = new Vector2(0f, 142f);

        PositionControl(roll != null ? roll.transform as RectTransform : null, rollAnchor, new Vector2(190f, 52f), controlPosition);
        PositionControl(dice != null ? dice.transform as RectTransform : null, diceAnchor, new Vector2(86f, 86f), controlPosition);
        PositionControl(endTurn != null ? endTurn.transform as RectTransform : null, endTurnAnchor, new Vector2(210f, 52f), controlPosition);
        PositionControl(confirmRoll != null ? confirmRoll.transform as RectTransform : null, confirmAnchor, new Vector2(240f, 52f), confirmPosition);
        PositionControl(seeHand != null ? seeHand.transform as RectTransform : null, seeHandAnchor, new Vector2(190f, 52f), controlPosition);

        BringForward(roll != null ? roll.transform : null);
        BringForward(dice != null ? dice.transform : null);
        BringForward(endTurn != null ? endTurn.transform : null);
        BringForward(confirmRoll != null ? confirmRoll.transform : null);
        BringForward(seeHand != null ? seeHand.transform : null);
    }

    private static Canvas GetMainCanvas()
    {
        RollButtonUI roll = FindFirstObjectByType<RollButtonUI>(FindObjectsInactive.Include);
        if (roll != null)
        {
            Canvas parentCanvas = roll.GetComponentInParent<Canvas>();
            if (parentCanvas != null)
                return parentCanvas;
        }

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas != null && canvas.name == "MainGameCanvas")
                return canvas;
        }

        return canvases.Length > 0 ? canvases[0] : null;
    }

    private void EnsurePanel(Transform canvasTransform)
    {
        if (panelRect == null)
        {
            Transform existingPanel = canvasTransform.Find(PanelName);
            if (existingPanel != null)
                panelRect = existingPanel as RectTransform;
        }

        if (panelRect == null)
        {
            GameObject panelObject = new GameObject(PanelName, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(canvasTransform, false);
            panelRect = panelObject.GetComponent<RectTransform>();
        }

        panelRect.SetAsFirstSibling();
        panelRect.anchorMin = new Vector2(0.06f, 0f);
        panelRect.anchorMax = new Vector2(0.94f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 24f);
        panelRect.sizeDelta = new Vector2(0f, 152f);

        panelImage = panelRect.GetComponent<Image>();
        if (panelImage == null)
            panelImage = panelRect.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.05f, 0.035f, 0.028f, 0.92f);
        panelImage.raycastTarget = false;

        RectTransform accentRect = EnsureChildRect(panelRect, AccentName);
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.offsetMin = new Vector2(0f, -5f);
        accentRect.offsetMax = Vector2.zero;

        accentImage = accentRect.GetComponent<Image>();
        if (accentImage == null)
            accentImage = accentRect.gameObject.AddComponent<Image>();
        accentImage.color = new Color(0.80f, 0.37f, 0.13f, 0.95f);
        accentImage.raycastTarget = false;

        RectTransform glowRect = EnsureChildRect(panelRect, GlowName);
        glowRect.anchorMin = new Vector2(0f, 1f);
        glowRect.anchorMax = new Vector2(1f, 1f);
        glowRect.pivot = new Vector2(0.5f, 1f);
        glowRect.offsetMin = new Vector2(24f, -16f);
        glowRect.offsetMax = new Vector2(-24f, -4f);
        glowImage = glowRect.GetComponent<Image>();
        if (glowImage == null)
            glowImage = glowRect.gameObject.AddComponent<Image>();
        glowImage.color = new Color(1f, 0.55f, 0.16f, 0.08f);
        glowImage.raycastTarget = false;

        RectTransform innerRect = EnsureChildRect(panelRect, InnerName);
        innerRect.anchorMin = new Vector2(0.02f, 0f);
        innerRect.anchorMax = new Vector2(0.98f, 1f);
        innerRect.pivot = new Vector2(0.5f, 0.5f);
        innerRect.offsetMin = new Vector2(0f, 16f);
        innerRect.offsetMax = new Vector2(0f, -18f);

        innerImage = innerRect.GetComponent<Image>();
        if (innerImage == null)
            innerImage = innerRect.gameObject.AddComponent<Image>();
        innerImage.color = new Color(0.08f, 0.10f, 0.11f, 0.82f);
        innerImage.raycastTarget = false;

        RemoveLegacyWell(RollWellName, canvasTransform);
        RemoveLegacyWell(DiceWellName, canvasTransform);
        RemoveLegacyWell(EndTurnWellName, canvasTransform);
        RemoveLegacyWell(HandWellName, canvasTransform);
        RemoveLegacyWell(ConfirmWellName, canvasTransform);
    }

    private static RectTransform EnsureChildRect(RectTransform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return existing as RectTransform;

        GameObject child = new GameObject(name, typeof(RectTransform), typeof(Image));
        child.transform.SetParent(parent, false);
        return child.GetComponent<RectTransform>();
    }

    private void RemoveLegacyWell(string name, Transform canvasTransform)
    {
        if (panelRect != null)
        {
            Transform nested = panelRect.Find(name);
            if (nested != null)
                DestroyImmediate(nested.gameObject);
        }

        if (canvasTransform != null)
        {
            Transform direct = canvasTransform.Find(name);
            if (direct != null)
                DestroyImmediate(direct.gameObject);
        }
    }

    private static void PositionControl(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
    {
        if (rect == null)
            return;

        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }

    private static void BringForward(Transform target)
    {
        if (target == null)
            return;

        target.SetAsLastSibling();
    }
}
