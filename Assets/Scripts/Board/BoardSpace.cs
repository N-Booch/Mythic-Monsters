using UnityEngine;
using UnityEngine.Serialization;
using System.Collections;
using TMPro;

/// <summary>
/// Represents a single space on the board and handles interactions when a player lands on it.
/// </summary>
public enum SpaceType
{
    Empty,
    Monster,
    Shop,
    Rest,
    Pitfall,
    Reprieve,
    Treasure,
    Gate,
    Peak,
    Cave
}

public enum BoardBiome
{
    None = -1,
    Mountain = 0,
    Forest = 1,
    Wasteland = 2,
    Ocean = 3
}

public class BoardSpace : MonoBehaviour
{
    [Header("Basic Info")]
    public SpaceType spaceType = SpaceType.Empty;
    [FormerlySerializedAs("pathIndex")]
    public int spaceIndex = 0;
    [FormerlySerializedAs("biome")]
    [InspectorName("Biome")]
    public BoardBiome biomeIndex = BoardBiome.None;

    [Header("Optional References")]
    [Tooltip("Monster associated with this space, if applicable.")]
    public Monster monster;

    [Tooltip("Reprieve card awarded on this space, if applicable.")]
    public ReprieveCard reprieveCard;

    [Tooltip("Treasure card awarded on this space, if applicable.")]
    public TreasureCard treasureCard;

    [Header("Trap State")]
    public bool hasMonsterSnare;
    public int monsterSnareDamage = 0;
    public PlayerPawn monsterSnareOwner;

    // Renderer and color for highlighting
    private Renderer rend;
    private SpriteRenderer tileRenderer;
    private SpriteRenderer borderRenderer;
    private SpriteRenderer accentRenderer;
    private SpriteRenderer iconPlateRenderer;
    private TextMeshPro iconText;
    private Color originalColor;
    private bool useBoardTileVisuals;
    private static readonly Color TrapColor = new Color(0.8f, 0.25f, 0.2f);

    public BoardBiome biome
    {
        get => biomeIndex;
        set => biomeIndex = value;
    }

    private void Awake()
    {
        rend = GetComponent<Renderer>();
        tileRenderer = GetComponent<SpriteRenderer>();
        borderRenderer = transform.Find("Border")?.GetComponent<SpriteRenderer>();
        useBoardTileVisuals = GetComponent<PlayerPawn>() == null;

        originalColor = useBoardTileVisuals
            ? GetBiomeColor()
            : rend != null ? rend.material.color : Color.white;

        if (!useBoardTileVisuals)
            return;

        EnsureBoardVisuals();
        ApplyBoardVisuals();
    }

    // =========================
    // PLAYER INTERACTIONS
    // =========================

    /// <summary>
    /// Entry point when a player lands on this space.
    /// Triggers the appropriate effect based on space type.
    /// </summary>
    public void OnPlayerLand(PlayerPawn player)
    {
        GameManager.Instance.BeginSpaceResolution();
        TryTriggerMonsterSnare(player);

        switch (spaceType)
        {
            case SpaceType.Empty:
                End();
                break;

            case SpaceType.Gate:
                ResolveGate(player);
                break;

            case SpaceType.Rest:
                player.Heal(player.GetEffectiveMaxHP() - player.currentHP);
                player.FillHandToMax();
                End();
                break;

            case SpaceType.Reprieve:
                player.DrawReprieveCards(2, true);
                UpdatePlayerHUD(player);
                End();
                break;

            case SpaceType.Treasure:
                ResolveTreasure(player);
                End();
                break;

            case SpaceType.Monster:
                ResolveMonster(player);
                break;

            case SpaceType.Shop:
                ShopManager.Instance.OpenShop(player);
                break;

            case SpaceType.Pitfall:
                GameManager.Instance.BeginPitfall(player);
                break;
        }
    }

    /// <summary>
    /// Refresh the player's HUD.
    /// </summary>
    public static void UpdatePlayerHUD(PlayerPawn player)
    {
        if (GameManager.Instance != null && GameManager.Instance.playerHUD != null)
            GameManager.Instance.playerHUD.SetPlayer(player);
    }

    // =========================
    // VISUAL INTERACTIONS
    // =========================

    /// <summary>
    /// Handles clicks on the space for targeting or other interactions.
    /// </summary>
    private void OnMouseDown()
    {
        if (BoardTargetingManager.Instance != null)
            BoardTargetingManager.Instance.SpaceClicked(this);
    }

    /// <summary>
    /// Highlights or unhighlights the space visually.
    /// </summary>
    public void Highlight(bool highlight)
    {
        if (rend == null) return;

        SetTileColor(highlight ? new Color(0.05f, 0.05f, 0.04f, 1f) : GetBaseColor());

        if (useBoardTileVisuals && borderRenderer != null)
            borderRenderer.color = highlight ? new Color(1f, 0.87f, 0.28f, 1f) : GetBiomeBorderColor();
    }

    // =========================
    // INTERNAL HELPERS
    // =========================

    /// <summary>
    /// Ends resolution of this space and tells GameManager to continue.
    /// </summary>
    private void End()
    {
        GameManager.Instance.EndSpaceResolution();
    }

    private void ResolveGate(PlayerPawn player)
    {
        bool isHomeGate = player.startingIndex == spaceIndex;
        if (isHomeGate && player.HasPeakAccess())
        {
            Debug.Log($"{player.playerName} reached their home gate with 3 titles and may travel to the Peak.");
            GameManager.Instance.BeginPeakDecision(player, this);
            return;
        }

        End();
    }

    private void ResolveTreasure(PlayerPawn player)
    {
        TreasureDeck.EnsureExists();
        TreasureCard reward = TreasureDeck.Instance != null
            ? TreasureDeck.Instance.DrawTreasure()
            : null;

        if (reward == null)
        {
            Debug.LogWarning("Treasure deck is empty. No treasure was awarded.");
            return;
        }

        string prompt = $"{player.playerName}: choose 1 treasure to replace, or keep your current treasures.";
        player.AcquireTreasure(reward, prompt, acquired =>
        {
            if (acquired)
                Debug.Log($"{player.playerName} gained Treasure: {reward.cardName}");
            else
                Debug.Log($"{player.playerName} did not equip Treasure: {reward.cardName}");

            End();
        });
    }

    private void ResolveMonster(PlayerPawn player)
    {
        if (player.IsInHomeBiome())
        {
            Debug.Log($"{player.playerName} is in their home biome, so this monster space does not trigger combat.");
            End();
            return;
        }

        int biomeIndex = player.GetCurrentBiomeIndex();
        if (biomeIndex < 0 || biomeIndex > 3)
        {
            Debug.LogWarning($"Could not determine a valid biome for monster space {spaceIndex}.");
            End();
            return;
        }

        CombatManager.Instance.StartBiomeCombat(player, (Monster.MonsterBiome)biomeIndex);
    }

    public void PlaceMonsterSnare(PlayerPawn owner, int damage)
    {
        hasMonsterSnare = true;
        monsterSnareOwner = owner;
        monsterSnareDamage = damage;
        UpdateVisualState();
    }

    private void TryTriggerMonsterSnare(PlayerPawn player)
    {
        if (!hasMonsterSnare)
            return;

        if (player == monsterSnareOwner)
            return;

        Debug.Log($"{player.playerName} triggered a Monster Snare on space {spaceIndex}.");
        player.TakeDamage(monsterSnareDamage);
        ClearMonsterSnare();
    }

    private void ClearMonsterSnare()
    {
        hasMonsterSnare = false;
        monsterSnareOwner = null;
        monsterSnareDamage = 0;
        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        ApplyBoardVisuals();
    }

    public void SyncIconViewRotation(float boardViewAngle)
    {
        if (!useBoardTileVisuals)
            return;

        transform.localRotation = Quaternion.identity;

        if (iconPlateRenderer != null)
            iconPlateRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        if (iconText != null)
            iconText.transform.localRotation = Quaternion.identity;
    }

    private Color GetBaseColor()
    {
        return hasMonsterSnare ? TrapColor : originalColor;
    }

    private void EnsureBoardVisuals()
    {
        if (tileRenderer == null)
            return;

        if (borderRenderer == null)
            borderRenderer = transform.Find("Border")?.GetComponent<SpriteRenderer>();

        accentRenderer = GetOrCreateSpriteChild(
            "TileAccent",
            new Vector3(0f, 0f, -0.01f),
            new Vector3(0.72f, 0.72f, 1f),
            1);

        iconPlateRenderer = GetOrCreateSpriteChild(
            "SpaceIconPlate",
            new Vector3(0f, 0f, -0.02f),
            new Vector3(0.62f, 0.62f, 1f),
            2);
        iconPlateRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        if (iconText == null)
        {
            Transform existingIcon = transform.Find("SpaceIconText");
            if (existingIcon != null)
                iconText = existingIcon.GetComponent<TextMeshPro>();
        }

        if (iconText == null)
        {
            GameObject iconObject = new GameObject("SpaceIconText", typeof(RectTransform), typeof(TextMeshPro));
            iconObject.transform.SetParent(transform, false);
            iconText = iconObject.GetComponent<TextMeshPro>();
        }

        iconText.transform.localPosition = new Vector3(0f, 0.01f, -0.03f);
        iconText.transform.localRotation = Quaternion.identity;
        iconText.transform.localScale = Vector3.one * 0.58f;
        iconText.rectTransform.sizeDelta = new Vector2(2.2f, 1.4f);
        iconText.alignment = TextAlignmentOptions.Center;
        iconText.fontSize = 8f;
        iconText.fontStyle = FontStyles.Bold;
        iconText.textWrappingMode = TextWrappingModes.NoWrap;
        iconText.overflowMode = TextOverflowModes.Overflow;

        MeshRenderer textRenderer = iconText.GetComponent<MeshRenderer>();
        if (textRenderer != null)
            textRenderer.sortingOrder = 3;
    }

    private SpriteRenderer GetOrCreateSpriteChild(string childName, Vector3 localPosition, Vector3 localScale, int sortingOrder)
    {
        Transform existing = transform.Find(childName);
        SpriteRenderer childRenderer = existing != null
            ? existing.GetComponent<SpriteRenderer>()
            : null;

        if (childRenderer == null)
        {
            GameObject childObject = new GameObject(childName, typeof(SpriteRenderer));
            childObject.transform.SetParent(transform, false);
            childRenderer = childObject.GetComponent<SpriteRenderer>();
        }

        childRenderer.transform.localPosition = localPosition;
        childRenderer.transform.localScale = localScale;
        childRenderer.transform.localRotation = Quaternion.identity;
        childRenderer.sprite = tileRenderer != null ? tileRenderer.sprite : childRenderer.sprite;
        childRenderer.sortingOrder = sortingOrder;
        return childRenderer;
    }

    private void ApplyBoardVisuals()
    {
        if (!useBoardTileVisuals)
            return;

        originalColor = GetBiomeColor();
        SetTileColor(GetBaseColor());

        if (borderRenderer != null)
            borderRenderer.color = GetBiomeBorderColor();

        if (accentRenderer != null)
        {
            Color accent = Color.Lerp(GetBaseColor(), Color.white, 0.28f);
            accent.a = hasMonsterSnare ? 0.18f : 0.26f;
            accentRenderer.color = accent;
        }

        string icon = GetSpaceIcon();
        bool showIcon = !string.IsNullOrEmpty(icon);

        if (iconPlateRenderer != null)
        {
            iconPlateRenderer.gameObject.SetActive(showIcon);
            iconPlateRenderer.color = GetSpaceIconPlateColor();
        }

        if (iconText != null)
        {
            iconText.gameObject.SetActive(showIcon);
            iconText.text = icon;
            iconText.color = GetSpaceIconTextColor();
        }
    }

    private void SetTileColor(Color color)
    {
        if (tileRenderer != null)
            tileRenderer.color = color;
        else if (rend != null)
            rend.material.color = color;
    }

    private Color GetBiomeColor()
    {
        switch (biomeIndex)
        {
            case BoardBiome.Mountain:
                return new Color(0.43f, 0.45f, 0.49f, 1f);
            case BoardBiome.Forest:
                return new Color(0.16f, 0.46f, 0.25f, 1f);
            case BoardBiome.Wasteland:
                return new Color(0.52f, 0.30f, 0.15f, 1f);
            case BoardBiome.Ocean:
                return new Color(0.08f, 0.36f, 0.56f, 1f);
            default:
                return new Color(0.31f, 0.29f, 0.26f, 1f);
        }
    }

    private Color GetBiomeBorderColor()
    {
        switch (biomeIndex)
        {
            case BoardBiome.Mountain:
                return new Color(0.78f, 0.80f, 0.82f, 1f);
            case BoardBiome.Forest:
                return new Color(0.45f, 0.72f, 0.38f, 1f);
            case BoardBiome.Wasteland:
                return new Color(0.82f, 0.53f, 0.25f, 1f);
            case BoardBiome.Ocean:
                return new Color(0.35f, 0.69f, 0.88f, 1f);
            default:
                return new Color(0.82f, 0.75f, 0.62f, 1f);
        }
    }

    private string GetSpaceIcon()
    {
        switch (spaceType)
        {
            case SpaceType.Monster:
                return "M";
            case SpaceType.Shop:
                return "$";
            case SpaceType.Rest:
                return "+";
            case SpaceType.Pitfall:
                return "!";
            case SpaceType.Reprieve:
                return "R";
            case SpaceType.Treasure:
                return "T";
            case SpaceType.Gate:
                return "G";
            case SpaceType.Peak:
                return "^";
            case SpaceType.Cave:
                return "C";
            default:
                return string.Empty;
        }
    }

    private Color GetSpaceIconPlateColor()
    {
        switch (spaceType)
        {
            case SpaceType.Monster:
                return new Color(0.43f, 0.08f, 0.08f, 0.96f);
            case SpaceType.Shop:
                return new Color(0.56f, 0.20f, 0.64f, 0.96f);
            case SpaceType.Rest:
                return new Color(0.14f, 0.46f, 0.28f, 0.96f);
            case SpaceType.Pitfall:
                return new Color(0.12f, 0.09f, 0.07f, 0.96f);
            case SpaceType.Reprieve:
                return new Color(0.18f, 0.25f, 0.58f, 0.96f);
            case SpaceType.Treasure:
                return new Color(0.78f, 0.55f, 0.16f, 0.96f);
            case SpaceType.Gate:
                return new Color(0.74f, 0.62f, 0.39f, 0.96f);
            case SpaceType.Peak:
                return new Color(0.43f, 0.29f, 0.62f, 0.96f);
            case SpaceType.Cave:
                return new Color(0.32f, 0.21f, 0.13f, 0.96f);
            default:
                return new Color(0f, 0f, 0f, 0f);
        }
    }

    private Color GetSpaceIconTextColor()
    {
        switch (spaceType)
        {
            case SpaceType.Treasure:
            case SpaceType.Shop:
            case SpaceType.Gate:
                return new Color(0.13f, 0.08f, 0.04f, 1f);
            default:
                return Color.white;
        }
    }
}
