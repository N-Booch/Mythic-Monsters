using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Builds the first-pass board environment behind the playable spaces.
/// This keeps the board readable while giving it a more intentional playmat.
/// </summary>
public class BoardEnvironment : MonoBehaviour
{
    private const string RootName = "GeneratedBoardEnvironment";
    private const string BoardDecorRootName = "GeneratedBoardDecor";
    private const string GatePathRootName = "GeneratedGateToPeakPaths";
    private static Sprite whiteSprite;

    public void BuildEnvironment(BoardManager boardManager)
    {
        if (boardManager == null || boardManager.boardPath == null || boardManager.boardPath.Count == 0)
            return;

        ClearExistingEnvironment();
        ClearExistingBoardDecor(boardManager);

        Transform root = new GameObject(RootName).transform;
        root.SetParent(transform, false);

        Transform boardDecorRoot = new GameObject(BoardDecorRootName).transform;
        boardDecorRoot.SetParent(boardManager.GetOrCreateBoardViewRoot(), false);

        Bounds boardBounds = CalculateBoardBounds(boardManager);
        Bounds stageBounds = CalculateStageBounds(boardManager, boardBounds);
        Vector2 center = (Vector2)stageBounds.center + boardManager.environmentFrameOffset;
        Vector2 size = stageBounds.size;

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
            mainCamera.backgroundColor = new Color(0.08f, 0.12f, 0.17f, 1f);

        CreatePanel(root, "TableShadow", center + new Vector2(0.12f, -0.12f), size + boardManager.environmentShadowPadding, new Color(0.03f, 0.025f, 0.02f, 0.42f), -40);
        CreatePanel(root, "OuterPlaymat", center, size + boardManager.environmentOuterPadding, new Color(0.13f, 0.09f, 0.07f, 1f), -39);
        CreatePanel(root, "InnerPlaymat", center, size + boardManager.environmentInnerPadding, new Color(0.20f, 0.14f, 0.10f, 1f), -38);
        CreatePanel(root, "BoardField", center, size + boardManager.environmentFieldPadding, new Color(0.10f, 0.13f, 0.15f, 1f), -37);

        UpdateGateToPeakPaths(boardManager);
        CreateBiomePanels(boardDecorRoot, boardManager);
        CreateCenterFocus(root, boardManager, boardBounds.center);
        CreateCaveFocus(root, boardManager);
        CreateSubtleTableTexture(root, center, size);
    }

    public void UpdateGateToPeakPaths(BoardManager boardManager)
    {
        Transform root = FindNewestEnvironmentRoot();
        if (root == null || boardManager == null)
            return;

        ClearExistingGatePathRoot(root);

        Transform pathRoot = new GameObject(GatePathRootName).transform;
        pathRoot.SetParent(root, false);
        CreateGateToPeakPaths(pathRoot, boardManager);
    }

    private Transform FindNewestEnvironmentRoot()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.name == RootName)
                return child;
        }

        return null;
    }

    private void ClearExistingEnvironment()
    {
        Transform existing = transform.Find(RootName);
        if (existing == null)
            return;

        if (Application.isPlaying)
            Destroy(existing.gameObject);
        else
            DestroyImmediate(existing.gameObject);
    }

    private void ClearExistingBoardDecor(BoardManager boardManager)
    {
        Transform boardRoot = boardManager != null ? boardManager.GetOrCreateBoardViewRoot() : null;
        Transform existing = boardRoot != null ? boardRoot.Find(BoardDecorRootName) : null;
        if (existing == null)
            return;

        if (Application.isPlaying)
            Destroy(existing.gameObject);
        else
            DestroyImmediate(existing.gameObject);
    }

    private void ClearExistingGatePathRoot(Transform root)
    {
        Transform existing = root != null ? root.Find(GatePathRootName) : null;
        if (existing == null)
            return;

        if (Application.isPlaying)
            Destroy(existing.gameObject);
        else
            DestroyImmediate(existing.gameObject);
    }

    private Bounds CalculateBoardBounds(BoardManager boardManager)
    {
        Bounds bounds = new Bounds(boardManager.boardPath[0].transform.position, Vector3.one);

        foreach (BoardSpace space in boardManager.boardPath)
        {
            if (space != null)
                bounds.Encapsulate(space.transform.position);
        }

        bounds.Expand(new Vector3(
            boardManager.environmentBoardPadding.x,
            boardManager.environmentBoardPadding.y,
            0f));

        Vector3 clampedSize = bounds.size;
        clampedSize.x = Mathf.Max(0.25f, clampedSize.x);
        clampedSize.y = Mathf.Max(0.25f, clampedSize.y);
        bounds.size = clampedSize;
        return bounds;
    }

    private Bounds CalculateStageBounds(BoardManager boardManager, Bounds boardBounds)
    {
        Vector2 center = boardBounds.center;
        float halfWidth = boardBounds.extents.x;
        float halfHeight = boardBounds.extents.y;

        if (boardManager.includeCaveInEnvironmentFrame && boardManager.caveSpace != null)
        {
            Vector2 cavePosition = boardManager.caveSpace.transform.position;
            halfWidth = Mathf.Max(halfWidth, Mathf.Abs(cavePosition.x - center.x) + boardManager.environmentCavePadding.x);
            halfHeight = Mathf.Max(halfHeight, Mathf.Abs(cavePosition.y - center.y) + boardManager.environmentCavePadding.y);
        }

        Bounds stageBounds = new Bounds(center, new Vector3(halfWidth * 2f, halfHeight * 2f, 1f));
        return stageBounds;
    }

    private void CreateBiomePanels(Transform root, BoardManager boardManager)
    {
        Dictionary<BoardBiome, Bounds> biomeBounds = new Dictionary<BoardBiome, Bounds>();

        foreach (BoardSpace space in boardManager.boardPath)
        {
            if (space == null || space.biome == BoardBiome.None)
                continue;

            if (!biomeBounds.ContainsKey(space.biome))
                biomeBounds[space.biome] = new Bounds(space.transform.position, Vector3.one);
            else
                biomeBounds[space.biome].Encapsulate(space.transform.position);
        }

        foreach (KeyValuePair<BoardBiome, Bounds> entry in biomeBounds)
        {
            Bounds bounds = entry.Value;
            bounds.Expand(0.72f);

            Color color = GetBiomeEnvironmentColor(entry.Key);
            CreatePanel(root, $"{entry.Key}Wash", bounds.center, bounds.size, color, -34);
            CreateBiomeLabel(root, entry.Key, bounds);
        }
    }

    private void CreateCenterFocus(Transform root, BoardManager boardManager, Vector2 boardCenter)
    {
        Vector2 focusPosition = boardManager.peakSpace != null
            ? (Vector2)boardManager.peakSpace.transform.position
            : boardCenter;

        SpriteRenderer halo = CreatePanel(root, "PeakHalo", focusPosition, new Vector2(1.85f, 1.85f), new Color(0.76f, 0.66f, 0.35f, 0.34f), -32);
        halo.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        SpriteRenderer core = CreatePanel(root, "PeakCore", focusPosition, new Vector2(1.1f, 1.1f), new Color(0.33f, 0.24f, 0.14f, 0.58f), -31);
        core.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
    }

    private void CreateGateToPeakPaths(Transform root, BoardManager boardManager)
    {
        if (boardManager.peakSpace == null)
            return;

        Vector2 peakPosition = boardManager.peakSpace.transform.position;
        foreach (BoardSpace gate in FindGateSpaces(boardManager))
            CreatePathSegments(root, gate.transform.position, peakPosition, gate.biome);
    }

    private List<BoardSpace> FindGateSpaces(BoardManager boardManager)
    {
        List<BoardSpace> gates = new List<BoardSpace>();

        foreach (BoardSpace space in boardManager.boardPath)
        {
            if (space != null && space.spaceType == SpaceType.Gate)
                gates.Add(space);
        }

        return gates;
    }

    private void CreatePathSegments(Transform root, Vector2 start, Vector2 end, BoardBiome biome)
    {
        Vector2 direction = end - start;
        float distance = direction.magnitude;
        if (distance <= 0.01f)
            return;

        Vector2 normal = new Vector2(-direction.y, direction.x).normalized;
        float curveDirection = Mathf.Sign(Vector2.Dot(normal, end));
        if (Mathf.Approximately(curveDirection, 0f))
            curveDirection = 1f;

        Vector2 previous = start;
        int segmentCount = 12;
        for (int i = 1; i <= segmentCount; i++)
        {
            float t = i / (float)segmentCount;
            Vector2 point = Vector2.Lerp(start, end, t);
            float curve = Mathf.Sin(t * Mathf.PI) * 0.28f * curveDirection;
            point += normal * curve;

            CreateLineSegment(root, $"GatePath_{biome}_{i}", previous, point, GetGatePathColor(biome));
            previous = point;
        }
    }

    private void CreateLineSegment(Transform root, string segmentName, Vector2 start, Vector2 end, Color color)
    {
        Vector2 midpoint = (start + end) * 0.5f;
        Vector2 direction = end - start;
        float distance = direction.magnitude;
        if (distance <= 0.01f)
            return;

        SpriteRenderer line = CreatePanel(root, segmentName, midpoint, new Vector2(distance, 0.92f), color, -33);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        line.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private Color GetGatePathColor(BoardBiome biome)
    {
        Color color = GetBiomeEnvironmentColor(biome);
        color.a = 0.34f;

        switch (biome)
        {
            case BoardBiome.Mountain:
                return new Color(0.82f, 0.84f, 0.78f, color.a);
            case BoardBiome.Forest:
                return new Color(0.42f, 0.78f, 0.38f, color.a);
            case BoardBiome.Wasteland:
                return new Color(0.88f, 0.48f, 0.20f, color.a);
            case BoardBiome.Ocean:
                return new Color(0.30f, 0.66f, 0.86f, color.a);
            default:
                return new Color(0.86f, 0.72f, 0.42f, color.a);
        }
    }

    private void CreateCaveFocus(Transform root, BoardManager boardManager)
    {
        if (boardManager.caveSpace == null)
            return;

        Vector2 cavePosition = boardManager.caveSpace.transform.position;
        SpriteRenderer shadow = CreatePanel(root, "CaveShadow", cavePosition + new Vector2(0.08f, -0.08f), new Vector2(1.42f, 1.42f), new Color(0.03f, 0.025f, 0.02f, 0.36f), -36);
        shadow.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        SpriteRenderer pad = CreatePanel(root, "CavePad", cavePosition, new Vector2(1.25f, 1.25f), new Color(0.21f, 0.15f, 0.10f, 0.82f), -35);
        pad.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
    }

    private void CreateSubtleTableTexture(Transform root, Vector2 center, Vector2 stageSize)
    {
        Vector2 size = stageSize + new Vector2(0.55f, 0.32f);

        for (int i = 0; i < 9; i++)
        {
            float y = center.y - (size.y * 0.42f) + (i * size.y / 8f);
            SpriteRenderer line = CreatePanel(
                root,
                $"PlaymatThread_{i + 1}",
                new Vector2(center.x, y),
                new Vector2(size.x * 0.92f, 0.025f),
                new Color(0.84f, 0.68f, 0.46f, 0.05f),
                -33);
            line.transform.localRotation = Quaternion.Euler(0f, 0f, -6f);
        }
    }

    private void CreateBiomeLabel(Transform root, BoardBiome biome, Bounds bounds)
    {
        GameObject labelObject = new GameObject($"{biome}Label", typeof(RectTransform), typeof(TextMeshPro));
        labelObject.transform.SetParent(root, false);
        labelObject.transform.position = new Vector3(bounds.center.x, bounds.center.y, 0.08f);
        labelObject.transform.localScale = Vector3.one * 0.28f;

        TextMeshPro label = labelObject.GetComponent<TextMeshPro>();
        label.text = biome.ToString().ToUpperInvariant();
        label.fontSize = 3.8f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.color = new Color(0.96f, 0.88f, 0.68f, 0.13f);

        MeshRenderer renderer = label.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.sortingOrder = -30;
    }

    private SpriteRenderer CreatePanel(Transform parent, string panelName, Vector2 position, Vector2 size, Color color, int sortingOrder)
    {
        GameObject panelObject = new GameObject(panelName, typeof(SpriteRenderer));
        panelObject.transform.SetParent(parent, false);
        panelObject.transform.position = new Vector3(position.x, position.y, 0.1f);
        panelObject.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer renderer = panelObject.GetComponent<SpriteRenderer>();
        renderer.sprite = GetWhiteSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private Sprite GetWhiteSprite()
    {
        if (whiteSprite != null)
            return whiteSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.name = "BoardEnvironmentWhitePixel";
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        whiteSprite.name = "BoardEnvironmentWhiteSprite";
        return whiteSprite;
    }

    private Color GetBiomeEnvironmentColor(BoardBiome biome)
    {
        switch (biome)
        {
            case BoardBiome.Mountain:
                return new Color(0.64f, 0.66f, 0.68f, 0.13f);
            case BoardBiome.Forest:
                return new Color(0.18f, 0.58f, 0.26f, 0.15f);
            case BoardBiome.Wasteland:
                return new Color(0.72f, 0.38f, 0.15f, 0.15f);
            case BoardBiome.Ocean:
                return new Color(0.11f, 0.46f, 0.70f, 0.16f);
            default:
                return new Color(0.35f, 0.30f, 0.24f, 0.12f);
        }
    }
}
