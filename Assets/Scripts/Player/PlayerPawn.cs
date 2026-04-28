using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class PlayerPawn : MonoBehaviour
{
    // ==============================
    // PLAYER IDENTITY
    // ==============================
    [Header("Player Info")]
    public string playerName = "Player";


    // ==============================
    // BOARD POSITIONING
    // ==============================
    [Header("Board Position")]
    public int currentIndex = 0;     // Current board space index
    public int startingIndex = 0;    // Spawn position
    public float moveSpeed = 3f;     // Default movement speed between spaces


    // ==============================
    // BASE STATS (Permanent identity stats)
    // ==============================
    [Header("Stats")]
    public int baseMaxHP;
    public int baseMight;
    public int baseArcane;


    // ==============================
    // CURRENT STATS (Runtime values)
    // ==============================
    public int currentHP;
    public int currentMight;
    public int currentArcane;


    // ==============================
    // MAX STATS (Base + bonuses)
    // ==============================
    [HideInInspector] public int maxHP;
    [HideInInspector] public int maxMight;
    [HideInInspector] public int maxArcane;

    public const int hardCap = 12; // Absolute stat cap


    // ==============================
    // HAND RULES
    // ==============================
    public int maxHandSize = 4; // Base hand size (titles may modify)


    // ==============================
    // PERMANENT MODIFIERS
    // ==============================
    [Header("Bonuses")]
    public int bonusHP = 0;
    public int bonusMight = 0;
    public int bonusArcane = 0;


    // ==============================
    // CARDS, TREASURES, TITLES
    // ==============================
    [Header("Cards and Titles")]
    public List<ReprieveCard> hand = new List<ReprieveCard>();
    public List<TreasureCard> equippedTreasures = new List<TreasureCard>();
    public List<TitleData> titles = new List<TitleData>();
    [HideInInspector] public List<TitleData> cleansedTitles = new List<TitleData>();


    // ==============================
    // PHASE & BOARD STATE
    // ==============================
    [Header("Cave Phase Tracking")]
    public bool inCavePhase = false;
    public BoardSpace preCaveSpace;
    public BoardSpace currentSpace;


    // ==============================
    // ECONOMY
    // ==============================
    [Header("Currency")]
    public int gold = 200; // Starting gold
    public const int maxGold = 2000;


    // ==============================
    // TURN STATE FLAGS
    // ==============================
    [HideInInspector] public bool isMoving = false;
    [HideInInspector] public bool isResolvingSpace = false;
    [HideInInspector] public bool isDead = false;
    [HideInInspector] public int pendingPeakMovementSteps = 0;


    // ==============================
    // COMBAT BUFF SYSTEM
    // ==============================
    [Header("Next Combat Buffs")]
    public int nextCombatMightBonus;
    public int nextCombatArcaneBonus;
    public int monstersDefeatedInCombat;
    public int glisteningRingObservedMonsterSlays;
    public int pendingGlisteningRingRewards;


    // ==============================
    // VISUAL FEEDBACK
    // ==============================
    [Header("Visuals")]
    public GameObject outlineObject;
    public Sprite boardSpriteOverride;

    private SpriteRenderer boardSpriteRenderer;
    private SpriteRenderer outlineSpriteRenderer;
    private Sprite activeRingSprite;


    // ==============================
    // INITIALIZATION
    // ==============================
    private void Awake()
    {
        EnsureCollections();
        boardSpriteRenderer = GetComponent<SpriteRenderer>();
        outlineSpriteRenderer = outlineObject != null ? outlineObject.GetComponent<SpriteRenderer>() : null;
        activeRingSprite = Resources.Load<Sprite>("BoardPawns/active-ring");
        ApplyBoardSprite();

        // Calculate max stats including bonuses
        UpdateMaxStats();

        // Initialize current stats to base values
        currentHP = maxHP;
        currentMight = baseMight;
        currentArcane = baseArcane;
    }

    private void EnsureCollections()
    {
        hand ??= new List<ReprieveCard>();
        equippedTreasures ??= new List<TreasureCard>();
        titles ??= new List<TitleData>();
        cleansedTitles ??= new List<TitleData>();

        hand.RemoveAll(card => card == null);
        equippedTreasures.RemoveAll(treasure => treasure == null);
        titles.RemoveAll(title => title == null);
        cleansedTitles.RemoveAll(title => title == null || !titles.Contains(title));
    }

    private void ApplyBoardSprite()
    {
        if (boardSpriteRenderer == null)
            return;

        if (boardSpriteOverride != null)
        {
            boardSpriteRenderer.sprite = boardSpriteOverride;
            ConfigureOutlineVisual();
            return;
        }

        string resourceName = GetBoardSpriteResourceName();
        if (string.IsNullOrWhiteSpace(resourceName))
            return;

        Sprite loadedSprite = Resources.Load<Sprite>($"BoardPawns/{resourceName}");
        if (loadedSprite != null)
        {
            boardSpriteRenderer.sprite = loadedSprite;
            ConfigureOutlineVisual();
        }
    }

    public void SetBoardPresenceVisible(bool visible)
    {
        if (boardSpriteRenderer != null)
            boardSpriteRenderer.enabled = visible;

        if (outlineObject != null && !visible)
            outlineObject.SetActive(false);
    }

    public void ConfigureMatchIdentity(string characterName, int characterStartingIndex, int characterBaseMaxHp, int characterBaseMight, int characterBaseArcane, Sprite characterBoardSpriteOverride)
    {
        EnsureCollections();

        playerName = characterName;
        startingIndex = characterStartingIndex;
        baseMaxHP = characterBaseMaxHp;
        baseMight = characterBaseMight;
        baseArcane = characterBaseArcane;
        boardSpriteOverride = characterBoardSpriteOverride;

        bonusHP = 0;
        bonusMight = 0;
        bonusArcane = 0;
        nextCombatMightBonus = 0;
        nextCombatArcaneBonus = 0;
        monstersDefeatedInCombat = 0;
        glisteningRingObservedMonsterSlays = 0;
        pendingGlisteningRingRewards = 0;
        maxHandSize = 4;
        gold = 200;
        inCavePhase = false;
        preCaveSpace = null;
        pendingPeakMovementSteps = 0;
        isMoving = false;
        isResolvingSpace = false;
        isDead = false;

        hand.Clear();
        equippedTreasures.Clear();
        titles.Clear();
        cleansedTitles.Clear();

        UpdateMaxStats();
        currentHP = maxHP;
        currentMight = baseMight;
        currentArcane = baseArcane;

        ApplyBoardSprite();
        SetBoardPresenceVisible(true);
        PlaceAtIndex(startingIndex);
        SetActiveVisual(false);
        RefreshHUD();
    }

    private void ConfigureOutlineVisual()
    {
        if (outlineSpriteRenderer == null)
            return;

        if (activeRingSprite != null)
        {
            outlineSpriteRenderer.sprite = activeRingSprite;
            outlineSpriteRenderer.color = new Color(1f, 1f, 1f, 0.92f);
            UpdateOutlineMarkerTransform();
        }
    }

    private void UpdateOutlineMarkerTransform()
    {
        if (outlineObject == null || outlineSpriteRenderer == null || outlineSpriteRenderer.sprite == null)
            return;

        float targetWorldSize = 1.1f;
        if (currentSpace != null)
        {
            Renderer spaceRenderer = currentSpace.GetComponent<Renderer>();
            if (spaceRenderer != null)
                targetWorldSize = Mathf.Max(spaceRenderer.bounds.size.x, spaceRenderer.bounds.size.y);
        }

        float spriteWorldWidth = outlineSpriteRenderer.sprite.bounds.size.x;
        float parentWorldScale = Mathf.Abs(transform.lossyScale.x);
        if (spriteWorldWidth <= 0.0001f || parentWorldScale <= 0.0001f)
            return;

        float localScale = (targetWorldSize / spriteWorldWidth) / parentWorldScale;
        outlineObject.transform.localPosition = new Vector3(0f, 0f, 0.05f);
        outlineObject.transform.localScale = new Vector3(localScale, localScale, 1f);
    }

    private string GetBoardSpriteResourceName()
    {
        if (string.IsNullOrWhiteSpace(playerName))
            return null;

        switch (playerName.Trim().ToLowerInvariant())
        {
            case "goblin":
                return "goblin-pawn";
            case "orc":
                return "orc-pawn";
            case "fairy":
                return "fairy-pawn";
            case "siren":
                return "siren-pawn";
            default:
                return null;
        }
    }


    // ==============================
    // TITLE SYSTEM
    // ==============================

    // Adds a title to the player (handles stacking rules)
    public void AddTitle(TitleData title)
    {
        EnsureCollections();

        if (!title.stackable && titles.Exists(t => t.titleName == title.titleName))
            return;

        titles.Add(title);
        UpdateMaxStats();
        RefreshHUD();
    }

    // Removes a title
    public void RemoveTitle(TitleData title)
    {
        EnsureCollections();
        titles.Remove(title);
        cleansedTitles.Remove(title);
        UpdateMaxStats();
        RefreshHUD();
    }

    // Checks if player has any titles
    public bool HasTitles()
    {
        EnsureCollections();
        return titles.Count > 0;
    }

    public int GetTitleCount()
    {
        EnsureCollections();
        return titles.Count;
    }

    public bool HasPeakAccess()
    {
        return GetTitleCount() >= 3;
    }

    public bool HasTitle(string titleName)
    {
        if (titles == null || string.IsNullOrWhiteSpace(titleName))
            return false;

        foreach (TitleData title in titles)
        {
            if (title == null)
                continue;

            if (string.Equals(title.titleName, titleName, System.StringComparison.OrdinalIgnoreCase) &&
                IsTitleEffectActive(title))
                return true;
        }

        return false;
    }

    public bool CanHaveTitlePilfered()
    {
        return !HasTreasure("Cleansing Potion");
    }

    public bool CanBePilferTargeted()
    {
        return !HasTreasure("Blessed Bangle");
    }

    public bool IsTitleEffectActive(TitleData title)
    {
        EnsureCollections();
        return title != null && !cleansedTitles.Contains(title);
    }

    public bool TryCleanseTitle(TitleData title)
    {
        if (title == null || !titles.Contains(title))
            return false;

        if (cleansedTitles.Contains(title))
            return false;

        cleansedTitles.Add(title);
        UpdateMaxStats();
        RefreshHUD();
        return true;
    }

    public bool HasTreasure(string treasureName)
    {
        if (equippedTreasures == null || string.IsNullOrWhiteSpace(treasureName))
            return false;

        foreach (TreasureCard treasure in equippedTreasures)
        {
            if (treasure == null)
                continue;

            if (string.Equals(treasure.cardName, treasureName, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public int GetHomeBiomeIndex()
    {
        return BoardManager.Instance != null
            ? BoardManager.Instance.GetBiomeIndex(startingIndex)
            : -1;
    }

    public int GetCurrentBiomeIndex()
    {
        return BoardManager.Instance != null
            ? BoardManager.Instance.GetBiomeIndex(currentIndex)
            : -1;
    }

    public bool IsInHomeBiome()
    {
        return GetHomeBiomeIndex() >= 0 && GetHomeBiomeIndex() == GetCurrentBiomeIndex();
    }

    public bool HasOtherLivingPlayerInSameBiome()
    {
        if (GameManager.Instance == null || GameManager.Instance.players == null)
            return false;

        int currentBiome = GetCurrentBiomeIndex();
        if (currentBiome < 0)
            return false;

        foreach (PlayerPawn otherPlayer in GameManager.Instance.players)
        {
            if (otherPlayer == null || otherPlayer == this || otherPlayer.isDead)
                continue;

            if (otherPlayer.GetCurrentBiomeIndex() == currentBiome)
                return true;
        }

        return false;
    }

    public bool IsClass(string className)
    {
        return !string.IsNullOrWhiteSpace(className) &&
               string.Equals(playerName, className, System.StringComparison.OrdinalIgnoreCase);
    }

    public bool IsInBiome(Monster.MonsterBiome biome)
    {
        return GetCurrentBiomeIndex() == (int)biome;
    }


    // ==============================
    // VISUAL STATE
    // ==============================

    // Enables/disables selection outline
    public void SetActiveVisual(bool active)
    {
        UpdateOutlineMarkerTransform();
        if (outlineObject != null)
            outlineObject.SetActive(active);
    }

    public void SetBoardViewRotation(float viewAngle, bool keepSpriteUpright)
    {
        transform.localRotation = keepSpriteUpright
            ? Quaternion.Euler(0f, 0f, -viewAngle)
            : Quaternion.identity;

        if (outlineObject != null)
            outlineObject.transform.localRotation = Quaternion.identity;
    }


    // ==============================
    // MOVEMENT SYSTEM
    // ==============================

    // Begins player movement
    public void MoveSteps(int steps)
    {
        GameManager.Instance?.LockEndTurn();

        if (!isMoving)
            StartCoroutine(MoveCoroutine(steps));
    }

    // Movement coroutine handling smooth board movement
    private IEnumerator MoveCoroutine(int steps)
    {
        isMoving = true;
        BoardSpace finalSpace = null;
        pendingPeakMovementSteps = 0;

        for (int i = 0; i < steps; i++)
        {
            int nextIndex = (currentIndex + 1) % BoardManager.Instance.boardPath.Count;
            BoardSpace nextSpace = BoardManager.Instance.GetSpaceAt(nextIndex);

            // Smooth movement
            while (Vector3.Distance(transform.position, nextSpace.transform.position) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(transform.position, nextSpace.transform.position, moveSpeed * Time.deltaTime);
                yield return null;
            }

            transform.position = nextSpace.transform.position;
            currentIndex = nextIndex;
            currentSpace = nextSpace;
            UpdateOutlineMarkerTransform();
            finalSpace = nextSpace;

            ResolvePassByTreasureEffects(nextSpace);

            if (nextIndex == startingIndex)
            {
                int gainedGold = AddGold(200 + GetHomeGateGoldBonus());
                Debug.Log($"{playerName} passed their home gate and gained {gainedGold} gold.");
            }

            if (nextSpace.spaceType == SpaceType.Gate &&
                nextSpace.spaceIndex == startingIndex &&
                HasPeakAccess())
            {
                pendingPeakMovementSteps = Mathf.Max(0, steps - i - 1);
            }

            if (ShouldStopMovementAt(nextSpace))
            {
                Debug.Log($"{playerName} stopped movement early on {nextSpace.spaceType} space {nextSpace.spaceIndex}.");
                break;
            }

            yield return new WaitForSeconds(0.1f);
        }

        isMoving = false;

        // Trigger space resolution
        if (finalSpace != null)
        {
            isResolvingSpace = true;
            finalSpace.OnPlayerLand(this);
        }
    }


    // ==============================
    // CARD SYSTEM
    // ==============================

    // Draws a reprieve card
    public void DrawReprieveCard(bool ignoreHandLimit = false)
    {
        if (!ignoreHandLimit && hand.Count >= GetEffectiveMaxHandSize())
        {
            Debug.Log($"{playerName} cannot draw a reprieve card because their hand is full.");
            return;
        }

        ReprieveCard drawnCard = ReprieveDeck.Instance.DrawCard();
        if (drawnCard != null)
        {
            hand.Add(drawnCard);
            Debug.Log($"{playerName} drew Reprieve card: {drawnCard.cardName}");

            // Refresh UI if visible
            if (HandUIManager.Instance != null && HandUIManager.Instance.IsHandOpen())
                HandUIManager.Instance.Refresh();
        }
    }

    public void ResumeMovementAfterPeakDecline()
    {
        int remainingSteps = pendingPeakMovementSteps;
        pendingPeakMovementSteps = 0;
        isResolvingSpace = false;
        Debug.Log($"{playerName} declined Peak access with {remainingSteps} movement step(s) remaining.");

        if (remainingSteps > 0)
        {
            StartCoroutine(ResumeMovementAfterPeakDeclineCoroutine(remainingSteps));
            return;
        }

        GameManager.Instance?.EndSpaceResolution();
    }

    public void ClearPendingPeakMovement()
    {
        pendingPeakMovementSteps = 0;
    }

    private IEnumerator ResumeMovementAfterPeakDeclineCoroutine(int remainingSteps)
    {
        GameManager.Instance?.SetState(GameState.PlayerTurn);
        yield return null;
        MoveSteps(remainingSteps);
    }

    private bool ShouldStopMovementAt(BoardSpace space)
    {
        if (space == null)
            return false;

        if (space.spaceType == SpaceType.Monster)
            return !IsInHomeBiome();

        if (space.spaceType == SpaceType.Gate)
            return space.spaceIndex == startingIndex && HasPeakAccess();

        return false;
    }

    public void DrawReprieveCards(int count, bool ignoreHandLimit = false)
    {
        for (int i = 0; i < count; i++)
            DrawReprieveCard(ignoreHandLimit);
    }

    public void FillHandToMax()
    {
        while (hand.Count < GetEffectiveMaxHandSize())
            DrawReprieveCard(true);
    }

    // Uses a reprieve card by index
    public void UseReprieveCard(int index)
    {
        if (index < 0 || index >= hand.Count) return;

        ReprieveCard card = hand[index];
        card.Play(this);
    }


    // ==============================
    // POSITIONING
    // ==============================

    // Instantly place player on board index
    public void PlaceAtIndex(int index)
    {
        BoardSpace space = BoardManager.Instance.GetSpaceAt(index);
        if (space != null)
        {
            transform.position = space.transform.position;
            currentIndex = index;
            currentSpace = space;
            UpdateOutlineMarkerTransform();
        }
    }


    // ==============================
    // PITFALL MODIFIERS
    // ==============================

    // Treasure-based pitfall modifiers
    public int GetTreasurePitfallModifier()
    {
        int bonus = 0;
        foreach (TreasureCard card in equippedTreasures)
            bonus += card.pitfallBonus;
        return bonus;
    }

    // Reprieve-based pitfall modifiers
    public int GetReprievePitfallModifier()
    {
        int bonus = 0;
        foreach (ReprieveCard card in hand)
            bonus += card.pitfallBonus;
        return bonus;
    }

    // Total pitfall modifier
    public int GetPitfallModifier()
    {
        return GetTreasurePitfallModifier() + GetReprievePitfallModifier();
    }

    public bool ShouldAutomaticallyFailPitfalls()
    {
        return HasTreasure("Bunga Club");
    }

    public int GetPitfallFailureDamage()
    {
        if (HasTreasure("Grappling Hook"))
            return 0;

        if (HasTreasure("Banded Boots"))
            return 1;

        return 3;
    }


    // ==============================
    // SHOP / SPACE RESOLUTION
    // ==============================

    // Ends shop resolution and resumes turn flow
    public void FinishShopResolution()
    {
        if (inCavePhase)
        {
            Debug.Log($"{playerName} finished trading in the cave.");
            return;
        }

        if (isResolvingSpace)
        {
            isResolvingSpace = false;
            Debug.Log($"{playerName} finished shopping");
            GameManager.Instance.EndSpaceResolution();
        }
    }


    // ==============================
    // STAT SYSTEM
    // ==============================

    // Recalculates max stats from bonuses
    public void UpdateMaxStats()
    {
        maxHP = Mathf.Clamp(baseMaxHP + bonusHP + GetTitleHpModifier(), 1, hardCap);
        maxMight = Mathf.Clamp(baseMight + bonusMight, -hardCap, hardCap);
        maxArcane = Mathf.Clamp(baseArcane + bonusArcane, -hardCap, hardCap);

        // Clamp current values
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);
        currentMight = Mathf.Clamp(currentMight, -hardCap, hardCap);
        currentArcane = Mathf.Clamp(currentArcane, -hardCap, hardCap);
    }

    // Applies permanent stat bonuses
    public void ApplyBonus(int hpBonus, int mightBonus, int arcaneBonus)
    {
        bonusHP += hpBonus;
        bonusMight += mightBonus;
        bonusArcane += arcaneBonus;

        UpdateMaxStats();
    }


    // ==============================
    // HEALTH SYSTEM
    // ==============================

    public void Heal(int amount)
    {
        int before = currentHP;
        currentHP = Mathf.Min(currentHP + amount, GetEffectiveMaxHP());

        Debug.Log($"{playerName} healed {currentHP - before} HP");
        RefreshHUD();
    }

    public void TakeDamage(int amount)
    {
        currentHP = Mathf.Max(currentHP - amount, 0);
        Debug.Log($"{playerName} takes {amount} damage (HP {currentHP})");
        RefreshHUD();

        bool isActiveCombatPlayer =
            CombatManager.Instance != null &&
            CombatManager.Instance.IsCombatActive &&
            CombatManager.Instance.CurrentPlayer == this;
        bool isPilferParticipant =
            PilferManager.Instance != null &&
            PilferManager.Instance.IsPilferActive &&
            (PilferManager.Instance.CurrentAttacker == this || PilferManager.Instance.CurrentDefender == this);

        if (currentHP <= 0 && !isDead && !isActiveCombatPlayer && !isPilferParticipant)
            GameManager.Instance?.OnPlayerHealthDepleted(this);
    }

    public int AddGold(int amount)
    {
        if (amount <= 0)
            return 0;

        int before = gold;
        gold = Mathf.Clamp(gold + amount, 0, maxGold);
        int gained = gold - before;
        RefreshHUD();
        return gained;
    }

    public int SpendGold(int amount)
    {
        if (amount <= 0)
            return 0;

        int spent = Mathf.Min(gold, amount);
        gold -= spent;
        RefreshHUD();
        return spent;
    }


    // ==============================
    // COMBAT SYSTEM
    // ==============================

    // Final combat might calculation
    public int GetCombatMight(bool includeNextRollBuff = true)
    {
        int bonus = includeNextRollBuff ? nextCombatMightBonus : 0;
        return Mathf.Clamp(GetEffectiveMight() + bonus, -hardCap, hardCap);
    }

    // Final combat arcane calculation
    public int GetCombatArcane(bool includeNextRollBuff = true)
    {
        if (IsArcaneLockedToZero())
            return 0;

        int bonus = includeNextRollBuff ? nextCombatArcaneBonus : 0;
        return Mathf.Clamp(GetEffectiveArcane() + bonus, -hardCap, hardCap);
    }

    // Clears temporary combat buffs
    public void ClearNextCombatBuffs()
    {
        nextCombatMightBonus = 0;
        nextCombatArcaneBonus = 0;
    }

    public bool HasNextCombatRollBuffs()
    {
        return nextCombatMightBonus != 0 || nextCombatArcaneBonus != 0;
    }

    public void ConsumeNextCombatRollBuffs()
    {
        if (!HasNextCombatRollBuffs())
            return;

        ClearNextCombatBuffs();
        RefreshHUD();
    }


    // ==============================
    // UI INTEGRATION
    // ==============================

    private void RefreshHUD()
    {
        if (GameManager.Instance?.playerHUD != null)
            GameManager.Instance.playerHUD.Refresh();
    }


    // ==============================
    // INPUT / TARGETING
    // ==============================

    private void OnMouseDown()
    {
        if (PlayerTargetingManager.Instance != null)
        {
            PlayerTargetingManager.Instance.PlayerClicked(this);
        }
    }


    // ==============================
    // CARD STEALING SYSTEM
    // ==============================

    public bool HasReprieveCards()
    {
        return hand != null && hand.Count > 0;
    }

    public ReprieveCard RemoveRandomCardFromHand()
    {
        if (!HasReprieveCards())
            return null;

        int index = Random.Range(0, hand.Count);
        ReprieveCard stolen = hand[index];
        hand.RemoveAt(index);
        return stolen;
    }


    // ==============================
    // TITLE EFFECT CALCULATIONS
    // ==============================

    public int GetEffectiveMight()
    {
        EnsureCollections();
        int value = maxMight;

        foreach (var title in titles)
        {
            if (!IsTitleEffectActive(title))
                continue;
            value += title.mightModifier;
        }

        foreach (var treasure in equippedTreasures)
        {
            if (treasure == null)
                continue;
            value += treasure.mightModifier;
        }

        value += GetConditionalTreasureMightBonus();

        return Mathf.Clamp(value, -hardCap, hardCap);
    }

    public int GetEffectiveArcane()
    {
        EnsureCollections();

        if (IsArcaneLockedToZero())
            return 0;

        int value = maxArcane;

        foreach (var title in titles)
        {
            if (!IsTitleEffectActive(title))
                continue;
            value += title.arcaneModifier;
        }

        foreach (var treasure in equippedTreasures)
        {
            if (treasure == null)
                continue;
            value += treasure.arcaneModifier;
        }

        if (HasTitle("Envious") && HasOtherLivingPlayerInSameBiome())
            value += 2;

        value += GetConditionalTreasureArcaneBonus();

        return Mathf.Clamp(value, -hardCap, hardCap);
    }

    private bool IsArcaneLockedToZero()
    {
        return HasTreasure("Fallen Hero's Blade");
    }

    public int GetEffectiveMaxHP()
    {
        EnsureCollections();
        int treasureBonus = 0;
        foreach (var treasure in equippedTreasures)
        {
            if (treasure == null)
                continue;
            treasureBonus += treasure.hpModifier;
        }

        return Mathf.Clamp(maxHP + treasureBonus, 1, hardCap);
    }

    public int GetEffectiveMaxHandSize()
    {
        EnsureCollections();
        int value = Mathf.Max(4, maxHandSize);

        foreach (var title in titles)
        {
            if (!IsTitleEffectActive(title))
                continue;
            if (title.maxHandModifier > 0)
                value = Mathf.Max(value, title.maxHandModifier);
        }

        foreach (var treasure in equippedTreasures)
        {
            if (treasure == null)
                continue;
            value += treasure.maxHandModifier;
        }

        return value;
    }

    public int GetEffectiveMaxEquipLoad()
    {
        EnsureCollections();
        int value = 4;

        foreach (var title in titles)
        {
            if (!IsTitleEffectActive(title))
                continue;
            if (title.maxEquipModifier > 0)
                value = Mathf.Max(value, title.maxEquipModifier);
        }

        foreach (var treasure in equippedTreasures)
        {
            if (treasure == null)
                continue;
            value += treasure.maxEquipModifier;
        }

        return value;
    }

    public void AcquireTreasure(TreasureCard treasure, string replacementPrompt, System.Action<bool> onComplete = null)
    {
        if (treasure == null)
        {
            onComplete?.Invoke(false);
            return;
        }

        if (equippedTreasures.Count < GetEffectiveMaxEquipLoad())
        {
            equippedTreasures.Add(treasure);
            FinalizeTreasureAcquisition(treasure, onComplete);
            return;
        }

        TreasureSelectionUI.EnsureExists();
        TreasureSelectionUI.Instance.BeginSelection(
            equippedTreasures,
            replacementPrompt,
            selectedTreasure =>
            {
                if (selectedTreasure != null)
                {
                    equippedTreasures.Remove(selectedTreasure);
                    TreasureDeck.Instance?.DiscardTreasure(selectedTreasure);
                    equippedTreasures.Add(treasure);
                    Debug.Log($"{playerName} replaced {selectedTreasure.cardName} with {treasure.cardName}.");
                    FinalizeTreasureAcquisition(treasure, onComplete);
                    return;
                }

                TreasureDeck.Instance?.DiscardTreasure(treasure);
                onComplete?.Invoke(false);
            },
            treasure,
            true,
            $"Keep current treasures and discard {treasure.cardName}");
    }

    private void FinalizeTreasureAcquisition(TreasureCard treasure, System.Action<bool> onComplete)
    {
        if (treasure != null && treasure.cardName == "Cleansing Potion")
        {
            BeginCleansingPotionSelection(onComplete);
            return;
        }

        RefreshHUD();
        onComplete?.Invoke(true);
    }

    private void BeginCleansingPotionSelection(System.Action<bool> onComplete)
    {
        EnsureCollections();

        List<TitleData> cleanseableTitles = new List<TitleData>();
        foreach (TitleData title in titles)
        {
            if (title == null || !IsTitleEffectActive(title))
                continue;

            cleanseableTitles.Add(title);
        }

        if (cleanseableTitles.Count == 0)
        {
            RefreshHUD();
            onComplete?.Invoke(true);
            return;
        }

        TitleUIManager.EnsureExists();
        TitleUIManager.Instance.BeginTitleSelection(
            this,
            $"{playerName}: choose 1 title for Cleansing Potion to suppress",
            selectedTitle =>
            {
                if (selectedTitle != null && TryCleanseTitle(selectedTitle))
                    Debug.Log($"{playerName} cleansed the title {selectedTitle.titleName} with Cleansing Potion.");

                RefreshHUD();
                onComplete?.Invoke(true);
            });
    }

    public int GetEffectiveMaxReprievesPerTurn(int defaultLimit)
    {
        int value = defaultLimit;

        foreach (var title in titles)
        {
            if (!IsTitleEffectActive(title))
                continue;

            if (title.maxReprievesPerTurnModifier > 0)
                value = Mathf.Max(value, title.maxReprievesPerTurnModifier);
        }

        return value;
    }

    public int GetMovementRollModifier()
    {
        int value = 0;

        foreach (var title in titles)
        {
            if (!IsTitleEffectActive(title))
                continue;

            value += title.movementRollModifier;
        }

        foreach (var treasure in equippedTreasures)
        {
            if (treasure == null)
                continue;

            switch (treasure.cardName)
            {
                case "Banded Boots":
                    value -= 1;
                    break;

                case "Traveler's Canteen":
                    value += 1;
                    break;
            }
        }

        return value;
    }

    public int GetTitleHpModifier()
    {
        int value = 0;

        foreach (var title in titles)
        {
            if (!IsTitleEffectActive(title))
                continue;

            value += title.hpModifier;
        }

        return value;
    }

    public string GetDisplayNameWithTitles(int maxLength = 28)
    {
        if (titles == null || titles.Count == 0)
            return playerName;

        List<string> titleNames = new List<string>();
        foreach (var title in titles)
        {
            if (title == null || string.IsNullOrWhiteSpace(title.titleName))
                continue;

            titleNames.Add(title.titleName);
        }

        if (titleNames.Count == 0)
            return playerName;

        string fullName = string.Join(" ", titleNames) + " " + playerName;
        if (fullName.Length <= maxLength)
            return fullName;

        StringBuilder condensedName = new StringBuilder();
        for (int i = 0; i < titleNames.Count; i++)
        {
            string titleName = titleNames[i];
            int chunkLength = Mathf.Min(3, titleName.Length);
            condensedName.Append(titleName.Substring(0, chunkLength));

            if (i < titleNames.Count - 1)
                condensedName.Append(" ");
        }

        condensedName.Append(" ").Append(playerName);
        return condensedName.ToString();
    }


    // ==============================
    // RULE MODIFIERS (TITLES)
    // ==============================

    public bool CanPlayReprieves()
    {
        foreach (var title in titles)
            if (IsTitleEffectActive(title) && title.blocksReprieveUse)
                return false;

        foreach (var treasure in equippedTreasures)
            if (treasure.blocksReprieveUse)
                return false;

        return true;
    }

    public bool CanRunAway()
    {
        foreach (var title in titles)
            if (IsTitleEffectActive(title) && title.blocksRunaway)
                return false;

        foreach (var treasure in equippedTreasures)
            if (treasure.blocksRunaway)
                return false;

        return true;
    }

    public int GetRunawayModifier()
    {
        int value = 0;

        foreach (var title in titles)
        {
            if (!IsTitleEffectActive(title))
                continue;

            value += title.runawayModifier;
        }

        foreach (var treasure in equippedTreasures)
            value += treasure.runawayModifier;

        return value;
    }

    public void MarkDead()
    {
        isDead = true;
        isMoving = false;
        isResolvingSpace = false;
        SetActiveVisual(false);
    }

    public void RespawnForCavePhase()
    {
        isDead = false;
        isMoving = false;
        isResolvingSpace = false;
        currentHP = GetEffectiveMaxHP();
        ClearNextCombatBuffs();
        ClearHandToDiscard();
        DrawReprieveCards(2, true);
        PlaceAtIndex(startingIndex);
        RefreshHUD();
    }

    public void ClearHandToDiscard()
    {
        if (hand == null || hand.Count == 0)
            return;

        foreach (ReprieveCard card in hand)
        {
            if (card != null)
                ReprieveDeck.Instance.Discard(card);
        }

        hand.Clear();
    }

    public int GetHomeGateGoldBonus()
    {
        int bonus = 0;

        foreach (TreasureCard treasure in equippedTreasures)
        {
            if (treasure == null)
                continue;

            if (treasure.cardName == "Glowing Gloves")
                bonus += 200;
        }

        return bonus;
    }

    public void ResolveMonsterSlainTreasureRewards(System.Action onComplete)
    {
        if (HasTreasure("Bloody Bandage"))
            Heal(GetEffectiveMaxHP() - currentHP);

        if (HasTreasure("Gremlin's Grimiore"))
            DrawReprieveCard(true);

        if (HasTreasure("Skinning Knife"))
        {
            Heal(2);
            AddGold(200);
        }

        RefreshHUD();
        onComplete?.Invoke();
    }

    public void ResolveObservedMonsterSlainTreasureRewards(System.Action onComplete)
    {
        if (!HasTreasure("Glistening Ring"))
        {
            onComplete?.Invoke();
            return;
        }

        glisteningRingObservedMonsterSlays++;
        bool shouldGrantTreasure = glisteningRingObservedMonsterSlays % 2 == 0;
        Debug.Log($"{playerName} observed monster slay count for Glistening Ring: {glisteningRingObservedMonsterSlays}.");

        if (!shouldGrantTreasure)
        {
            RefreshHUD();
            onComplete?.Invoke();
            return;
        }

        pendingGlisteningRingRewards++;
        Debug.Log($"{playerName} queued a Glistening Ring treasure reward. Pending rewards: {pendingGlisteningRingRewards}.");
        RefreshHUD();
        onComplete?.Invoke();
    }

    public void ResolvePendingObservedMonsterSlainTreasureRewards(System.Action onComplete)
    {
        if (pendingGlisteningRingRewards <= 0)
        {
            onComplete?.Invoke();
            return;
        }

        TreasureDeck.EnsureExists();
        TreasureCard reward = TreasureDeck.Instance != null
            ? TreasureDeck.Instance.DrawTreasure()
            : null;

        if (reward == null)
        {
            pendingGlisteningRingRewards = Mathf.Max(0, pendingGlisteningRingRewards - 1);
            RefreshHUD();
            ResolvePendingObservedMonsterSlainTreasureRewards(onComplete);
            return;
        }

        AcquireTreasure(
            reward,
            $"{playerName}: Glistening Ring grants a treasure. Equip it or keep your current treasures.",
            acquired =>
            {
                pendingGlisteningRingRewards = Mathf.Max(0, pendingGlisteningRingRewards - 1);

                if (acquired)
                    Debug.Log($"{playerName} gained {reward.cardName} from Glistening Ring.");
                else
                    Debug.Log($"{playerName} declined {reward.cardName} from Glistening Ring.");

                RefreshHUD();
                ResolvePendingObservedMonsterSlainTreasureRewards(onComplete);
            });
    }

    private int GetConditionalTreasureMightBonus()
    {
        int bonus = 0;

        foreach (TreasureCard treasure in equippedTreasures)
        {
            if (treasure == null)
                continue;

            switch (treasure.cardName)
            {
                case "Forest Dweller's Dagger":
                    if (IsInBiome(Monster.MonsterBiome.Forest))
                        bonus += 2;
                    break;

                case "Mountain Pick":
                    if (IsInBiome(Monster.MonsterBiome.Mountain))
                        bonus += 1;
                    break;

                case "Ocean Conch":
                    if (IsInBiome(Monster.MonsterBiome.Ocean))
                        bonus += 1;
                    break;

                case "Scorched Staff":
                    if (IsInBiome(Monster.MonsterBiome.Wasteland))
                        bonus += 2;
                    break;

                case "Sword of the Sea":
                    if (IsInBiome(Monster.MonsterBiome.Ocean))
                        bonus += 2;
                    break;

                case "Thorn Bow":
                    if (IsInBiome(Monster.MonsterBiome.Forest))
                        bonus += 1;
                    break;

                case "Wanderer's Cane":
                    if (IsInBiome(Monster.MonsterBiome.Wasteland))
                        bonus += 1;
                    break;
            }
        }

        return bonus;
    }

    private int GetConditionalTreasureArcaneBonus()
    {
        int bonus = 0;

        foreach (TreasureCard treasure in equippedTreasures)
        {
            if (treasure == null)
                continue;

            switch (treasure.cardName)
            {
                case "Fairy Dust":
                    if (IsClass("Fairy"))
                        bonus += 1;
                    break;

                case "Goblin Fang":
                    if (IsClass("Goblin"))
                        bonus += 1;
                    break;

                case "Orc Musk":
                    if (IsClass("Orc"))
                        bonus += 1;
                    break;

                case "Siren Shell":
                    if (IsClass("Siren"))
                        bonus += 1;
                    break;
            }
        }

        return bonus;
    }

    private void ResolvePassByTreasureEffects(BoardSpace passedSpace)
    {
        if (passedSpace == null || !HasTreasure("Serrated Salve") || GameManager.Instance == null || inCavePhase)
            return;

        foreach (PlayerPawn otherPlayer in GameManager.Instance.players)
        {
            if (otherPlayer == null || otherPlayer == this || otherPlayer.isDead || otherPlayer.inCavePhase)
                continue;

            if (otherPlayer.currentIndex != passedSpace.spaceIndex)
                continue;

            int previousHp = currentHP;
            otherPlayer.TakeDamage(3);
            Heal(3);
            int healedAmount = currentHP - previousHp;
            Debug.Log($"{playerName} siphoned {healedAmount} HP from {otherPlayer.playerName} with Serrated Salve.");
        }
    }
}
