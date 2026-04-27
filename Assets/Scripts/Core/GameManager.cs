using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    private sealed class PendingSinnersSceptreEffect
    {
        public PlayerPawn owner;
        public int burstsRemaining;
    }

    // ============================
    // Singleton
    // ============================

    public static GameManager Instance;

    // ============================
    // UI References
    // ============================

    [Header("UI")]
    public EndTurnButtonUI endTurnButtonUI;
    public PlayerHUDUI playerHUD;
    public GameObject confirmRollButton;

    // ============================
    // Player Management
    // ============================

    [Header("Players")]
    public PlayerPawn[] players;
    public bool randomizeTurnOrderAtGameStart = true;
    private int activePlayerIndex = 0;

    // ============================
    // Reprieve Rules / Limits
    // ============================

    [Header("Reprieve Rules")]
    public int reprievesUsedThisTurn = 0;
    public int maxReprievesPerTurn = 2;

    // Multi-step reprieve tracking
    public ReprieveCard PendingReprieve { get; private set; }
    public PlayerPawn PendingReprieveUser { get; private set; }

    public bool IsPendingReprieveActive => PendingReprieve != null;

    // ============================
    // Turn / State Control
    // ============================

    private bool hasRolledForMovementThisTurn = false;
    private bool hasResolvedSpaceAfterMovementThisTurn = false;
    private bool pilferCheckedThisTurn = false;
    private bool isAdvancingTurn = false;
    private bool isResolvingDeath = false;
    private bool isResolvingSinnersSceptreEffects = false;
    private bool pendingEndTurnAfterDiscard = false;
    private bool pendingCaveAdvanceAfterDiscard = false;
    private bool isGameOver = false;
    private PlayerPawn temporaryActionPlayer;
    private readonly Queue<PendingSinnersSceptreEffect> pendingSinnersSceptreEffects = new Queue<PendingSinnersSceptreEffect>();

    public GameState CurrentState { get; private set; }
    public bool IsGameOver => isGameOver;
    private readonly List<PlayerPawn> caveTurnOrder = new List<PlayerPawn>();
    private int caveTurnIndex = -1;

    // ============================
    // Input Mode Control
    // ============================

    public enum InputMode
    {
        Normal,           // free play
        SelectingPlayer,  // targeting a player
        SelectingCard,     // targeting a card
        SelectingBoard     // targeting a board
    }

    public InputMode CurrentInputMode { get; private set; } = InputMode.Normal;

    public void SetInputMode(InputMode mode)
    {
        CurrentInputMode = mode;
    }

    // ============================
    // Unity Lifecycle
    // ============================

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        EnsureTitleSystemExists();
        TreasureDeck.EnsureExists();
        MonsterRoster.EnsureExists();
        PilferManager.EnsureExists();
    }

    private void Start()
    {
        // Initialize deck
        ReprieveDeck.Instance.InitializeDeck();

        // Setup players
        foreach (PlayerPawn player in players)
        {
            // Starting hand
            for (int i = 0; i < 2; i++)
                player.hand.Add(ReprieveDeck.Instance.DrawCard());

            // Starting position
            player.PlaceAtIndex(player.startingIndex);
            player.SetActiveVisual(false);
        }

        RandomizeInitialTurnOrder();
        activePlayerIndex = 0;
        StartTurn(GetActivePlayer());
        BoardActionBarLayoutUI.EnsureExists();
    }

    private void RandomizeInitialTurnOrder()
    {
        if (!randomizeTurnOrderAtGameStart || players == null || players.Length <= 1)
            return;

        for (int i = players.Length - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            (players[i], players[swapIndex]) = (players[swapIndex], players[i]);
        }

        List<string> orderedNames = new List<string>();
        foreach (PlayerPawn player in players)
        {
            if (player != null)
                orderedNames.Add(player.playerName);
        }

        Debug.Log($"Randomized turn order: {string.Join(" -> ", orderedNames)}");
    }

    // =========================================================
    // TURN FLOW SYSTEM
    // =========================================================

    /// <summary>
    /// Initializes a player's turn
    /// </summary>
    private void StartTurn(PlayerPawn player)
    {
        if (player == null)
            return;

        if (player.isDead)
        {
            AdvancePastDeadPlayer();
            return;
        }

        isAdvancingTurn = false;
        pendingEndTurnAfterDiscard = false;
        CurrentState = GameState.PlayerTurn;
        hasRolledForMovementThisTurn = false;
        hasResolvedSpaceAfterMovementThisTurn = false;
        pilferCheckedThisTurn = false;
        reprievesUsedThisTurn = 0;
        DiceManager.Instance?.ResetTurnPresentation();
        UpdateEndTurnAvailability();

        // Reset movement/space flags
        player.isMoving = false;
        player.isResolvingSpace = false;

        // Draw start-of-turn card
        DrawReprieveCard(player);

        // UI updates
        playerHUD.SetPlayer(player);
        player.SetActiveVisual(true);
        BoardManager.Instance?.OrientViewToPlayer(player);
        TriggerTitleEvent(TitleData.TitleTriggerType.StartTurn, player);
        StartCoroutine(HandleStartTurnSpecialTitles(player));

        // Hide confirm roll button
        if (confirmRollButton != null)
            confirmRollButton.SetActive(false);
    }
    private void DrawReprieveCard(PlayerPawn player)
    {
        player.DrawReprieveCard(true);

        if (playerHUD != null)
            playerHUD.Refresh();
    }


    // =========================================================
    // MOVEMENT / DICE FLOW
    // =========================================================

    public void RollForMovement()
    {
        if (!CanPlayerAct())
            return;

        if (hasRolledForMovementThisTurn)
        {
            Debug.Log("Movement already rolled this turn.");
            return;
        }

        PlayerPawn player = GetActivePlayer();

        hasRolledForMovementThisTurn = true;
        hasResolvedSpaceAfterMovementThisTurn = false;
        CurrentState = GameState.AwaitingDiceDecision;

        DiceManager.Instance.Roll(
            DiceRollContext.Movement,
            player,
            roll =>
            {
                roll += player.GetMovementRollModifier();
                roll = Mathf.Max(0, roll);
                player.MoveSteps(roll);
                CurrentState = GameState.PlayerTurn;
                UpdateEndTurnAvailability();
            }
        );

        TriggerTitleEvent(TitleData.TitleTriggerType.OnMoveRoll, player);
    }

    // =========================================================
    // SPACE RESOLUTION
    // =========================================================

    public void BeginSpaceResolution()
    {
        CurrentState = GameState.ResolvingSpace;
    }

    public void EndSpaceResolution()
    {
        PlayerPawn activePlayer = GetActivePlayer();
        if (activePlayer != null)
            activePlayer.isResolvingSpace = false;

        if (hasRolledForMovementThisTurn)
            hasResolvedSpaceAfterMovementThisTurn = true;

        CurrentState = GameState.PlayerTurn;
        UpdateEndTurnAvailability();

    }

    // =========================================================
    // TURN ADVANCEMENT
    // =========================================================

    public void AdvanceTurnFromButton()
    {
        AdvanceTurnFromButtonInternal(false);
    }

    private void AdvanceTurnFromButtonInternal(bool skipUnusedReprievePrompt)
    {
        if (isGameOver)
            return;

        DebugGameState("AdvanceTurnButton");

        if (CurrentState == GameState.CavePhase)
        {
            AdvanceCavePhaseFromButton();
            return;
        }

        if (isAdvancingTurn)
        {
            Debug.Log("Turn advance already in progress.");
            return;
        }

        PlayerPawn activePlayer = GetActivePlayer();
        if (ShouldOfferPilferAtEndOfTurn(activePlayer))
        {
            pilferCheckedThisTurn = true;
            TryOfferPilfer(activePlayer);
            return;
        }

        if (!hasRolledForMovementThisTurn)
        {
            Debug.Log("Cannot end turn — movement roll required.");
            return;
        }

        if (CurrentState != GameState.PlayerTurn)
        {
            Debug.Log("Cannot end turn during unresolved actions.");
            return;
        }

        if (!skipUnusedReprievePrompt &&
            activePlayer != null &&
            TryShowUnusedReprieveEndTurnPrompt(activePlayer, false))
        {
            return;
        }

        if (activePlayer != null && activePlayer.hand.Count > activePlayer.GetEffectiveMaxHandSize())
        {
            pendingEndTurnAfterDiscard = true;
            BeginForcedEndTurnDiscard(activePlayer);
            return;
        }

        LockEndTurn();
        isAdvancingTurn = true;
        StartCoroutine(AdvanceTurn());
    }

    private IEnumerator AdvanceTurn()
    {
        PlayerPawn currentPlayer = GetActivePlayer();
        if (currentPlayer == null)
        {
            isAdvancingTurn = false;
            yield break;
        }

        TriggerTitleEvent(TitleData.TitleTriggerType.EndTurn, currentPlayer);
        ResolveTreasureEndTurnEffects(currentPlayer);
        yield return ResolveChaoticEndTurnIfNeeded(currentPlayer);
        ClearTurnScopedCombatBuffs(currentPlayer);

        // Deactivate visual
        currentPlayer.isResolvingSpace = false;
        currentPlayer.SetActiveVisual(false);
        activePlayerIndex++;

        // Cave phase check
        if (activePlayerIndex >= players.Length)
        {
            StartCavePhase();
            yield break;
        }

        while (activePlayerIndex < players.Length && players[activePlayerIndex] != null && players[activePlayerIndex].isDead)
        {
            Debug.Log($"Skipping {players[activePlayerIndex].playerName}'s turn because they are dead.");
            activePlayerIndex++;
        }

        if (activePlayerIndex >= players.Length)
        {
            StartCavePhase();
            yield break;
        }

        yield return null;
        StartTurn(GetActivePlayer());
    }

    private void ResolveTreasureEndTurnEffects(PlayerPawn player)
    {
        if (player == null || player.isDead)
            return;

        if (player.HasTreasure("Rations"))
        {
            player.Heal(2);
            Debug.Log($"{player.playerName} healed 2 HP from Rations at end of turn.");
        }

        if (player.HasTreasure("Royal Crown") &&
            !player.inCavePhase &&
            BoardManager.Instance != null &&
            players != null)
        {
            int totalTribute = 0;
            foreach (PlayerPawn otherPlayer in players)
            {
                if (otherPlayer == null || otherPlayer == player || otherPlayer.isDead || otherPlayer.inCavePhase)
                    continue;

                int distance = BoardManager.Instance.GetCircularDistance(player.currentIndex, otherPlayer.currentIndex);
                if (distance > 3)
                    continue;

                int paid = otherPlayer.SpendGold(200);
                totalTribute += paid;
                if (paid > 0)
                    Debug.Log($"{otherPlayer.playerName} paid {paid} gold to {player.playerName}'s Royal Crown.");
            }

            if (totalTribute > 0)
            {
                player.AddGold(totalTribute);
                Debug.Log($"{player.playerName} collected {totalTribute} gold from Royal Crown.");
            }
        }
    }

    private void ClearTurnScopedCombatBuffs(PlayerPawn player)
    {
        if (player == null || !player.HasNextCombatRollBuffs())
            return;

        player.ClearNextCombatBuffs();
        playerHUD?.Refresh();
        Debug.Log($"{player.playerName}'s unused next-combat roll buffs expired at end of turn.");
    }

    public void ResolveTreasureRolledDieEffects(PlayerPawn sourcePlayer, IEnumerable<int> rolledValues)
    {
        if (players == null || rolledValues == null)
            return;

        int foursRolled = 0;
        int onesRolled = 0;
        foreach (int value in rolledValues)
        {
            if (value == 4)
                foursRolled++;
            else if (value == 1)
                onesRolled++;
        }

        if (foursRolled > 0)
        {
            foreach (PlayerPawn player in players)
            {
                if (player == null || player.isDead || !player.HasTreasure("Harmonic Harp"))
                    continue;

                int totalHealing = 2 * foursRolled;
                player.Heal(totalHealing);
                Debug.Log($"{player.playerName} healed {totalHealing} HP from Harmonic Harp because a 4 was rolled.");
            }
        }

        if (onesRolled > 0)
        {
            foreach (PlayerPawn player in players)
            {
                if (player == null || player.isDead || !player.HasTreasure("Sinner's Sceptre"))
                    continue;

                player.Heal(onesRolled);

                int otherLivingPlayers = CountOtherLivingPlayers(player);
                int totalBursts = otherLivingPlayers * onesRolled;
                if (totalBursts <= 0)
                    continue;

                pendingSinnersSceptreEffects.Enqueue(new PendingSinnersSceptreEffect
                {
                    owner = player,
                    burstsRemaining = totalBursts
                });

                Debug.Log($"{player.playerName}'s Sinner's Sceptre gained {totalBursts} damage burst(s) because {onesRolled} one(s) were rolled.");
            }

            TryResolvePendingSinnersSceptreEffects();
        }
    }

    private int CountOtherLivingPlayers(PlayerPawn owner)
    {
        if (players == null || owner == null)
            return 0;

        int count = 0;
        foreach (PlayerPawn player in players)
        {
            if (player == null || player == owner || player.isDead)
                continue;

            count++;
        }

        return count;
    }

    private void TryResolvePendingSinnersSceptreEffects()
    {
        if (isResolvingSinnersSceptreEffects || pendingSinnersSceptreEffects.Count == 0)
            return;

        StartCoroutine(ResolvePendingSinnersSceptreEffects());
    }

    private IEnumerator ResolvePendingSinnersSceptreEffects()
    {
        isResolvingSinnersSceptreEffects = true;

        while (pendingSinnersSceptreEffects.Count > 0)
        {
            while (!CanResolvePendingSinnersSceptreEffect())
                yield return null;

            PendingSinnersSceptreEffect effect = pendingSinnersSceptreEffects.Dequeue();
            if (effect == null || effect.owner == null || effect.owner.isDead || effect.burstsRemaining <= 0)
                continue;

            yield return ResolveSingleSinnersSceptreEffect(effect);
        }

        isResolvingSinnersSceptreEffects = false;
        RefreshActionAvailability();
    }

    private bool CanResolvePendingSinnersSceptreEffect()
    {
        if (isGameOver || isResolvingDeath || IsPendingReprieveActive || CurrentInputMode != InputMode.Normal)
            return false;

        if (HandUIManager.Instance != null && HandUIManager.Instance.IsForcedDiscardActive)
            return false;

        if (CurrentState == GameState.Animation ||
            CurrentState == GameState.ResolvingSpace ||
            CurrentState == GameState.AwaitingDiceDecision)
            return false;

        if (CombatManager.Instance != null)
        {
            if (CombatManager.Instance.IsRollingCombatDice ||
                CombatManager.Instance.HasPendingCombatRoll ||
                CombatManager.Instance.IsCombatActive ||
                CombatManager.Instance.IsAwaitingRunawayRoll ||
                CombatManager.Instance.IsAwaitingSceneContinue ||
                CombatManager.Instance.IsResolvingCombatChoice)
            {
                return false;
            }
        }

        if (PilferManager.Instance != null && PilferManager.Instance.IsPilferActive)
            return false;

        return CurrentState == GameState.PlayerTurn ||
               CurrentState == GameState.CavePhase ||
               CurrentState == GameState.Combat;
    }

    private IEnumerator ResolveSingleSinnersSceptreEffect(PendingSinnersSceptreEffect effect)
    {
        while (effect.burstsRemaining > 0 && effect.owner != null && !effect.owner.isDead)
        {
            List<PlayerPawn> eligibleTargets = GetLivingPlayers();
            if (eligibleTargets.Count == 0)
                yield break;

            bool selectionResolved = false;
            PlayerPawn selectedTarget = null;

            SetInputMode(InputMode.SelectingPlayer);
            LockActionButtons();
            PlayerSelectionUI.EnsureExists();
            PlayerSelectionUI.Instance.BeginSelection(
                eligibleTargets,
                $"{effect.owner.playerName}: Sinner's Sceptre deal 2 HP ({effect.burstsRemaining} remaining)",
                selectedPlayer =>
                {
                    selectedTarget = selectedPlayer;
                    selectionResolved = true;
                },
                "Finish");

            while (!selectionResolved)
                yield return null;

            SetInputMode(InputMode.Normal);
            RefreshActionAvailability();

            if (selectedTarget == null)
                yield break;

            selectedTarget.TakeDamage(2);
            Debug.Log($"{effect.owner.playerName}'s Sinner's Sceptre dealt 2 damage to {selectedTarget.playerName}. {effect.burstsRemaining - 1} burst(s) remain.");

            if (CombatManager.Instance != null)
                CombatManager.Instance.HandleExternalDamageToCurrentPlayer(selectedTarget);

            effect.burstsRemaining--;

            yield return null;

            while (isResolvingDeath)
                yield return null;
        }
    }

    private List<PlayerPawn> GetLivingPlayers()
    {
        List<PlayerPawn> livingPlayers = new List<PlayerPawn>();
        if (players == null)
            return livingPlayers;

        foreach (PlayerPawn player in players)
        {
            if (player == null || player.isDead)
                continue;

            livingPlayers.Add(player);
        }

        return livingPlayers;
    }

    // =========================================================
    // CAVE PHASE
    // =========================================================

    private void StartCavePhase()
    {
        CurrentState = GameState.CavePhase;
        reprievesUsedThisTurn = 0;
        hasRolledForMovementThisTurn = false;
        hasResolvedSpaceAfterMovementThisTurn = false;
        pendingEndTurnAfterDiscard = false;
        pendingCaveAdvanceAfterDiscard = false;
        caveTurnOrder.Clear();
        caveTurnIndex = -1;

        RespawnDeadPlayersForCavePhase();

        foreach (PlayerPawn player in players)
        {
            player.SetActiveVisual(false);
            player.inCavePhase = true;
            player.preCaveSpace = BoardManager.Instance.GetSpaceAt(player.currentIndex);
        }

        PositionPlayersForCavePhase();

        StartCoroutine(CavePhase());
    }

    private IEnumerator CavePhase()
    {
        Debug.Log("Cave phase begins.");

        foreach (PlayerPawn player in players)
            player.Heal(2);

        yield return new WaitForSeconds(0.75f);

        BuildCaveTurnOrder();
        BeginNextCaveTurn();
    }

    // =========================================================
    // HELPERS / QUERIES
    // =========================================================

    public PlayerPawn GetActivePlayer()
    {
        if ((CurrentState == GameState.CavePhase || IsCaveTurnActive()) &&
            caveTurnIndex >= 0 &&
            caveTurnIndex < caveTurnOrder.Count)
        {
            return caveTurnOrder[caveTurnIndex];
        }

        if (activePlayerIndex >= 0 && activePlayerIndex < players.Length)
            return players[activePlayerIndex];
        return null;
    }

    public void RestartTurnForPlayer(PlayerPawn player)
    {
        if (player == null || players == null)
            return;

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null)
                continue;

            players[i].SetActiveVisual(false);
            if (players[i] == player)
                activePlayerIndex = i;
        }

        SetInputMode(InputMode.Normal);
        StartTurn(player);
    }

    private void TriggerTitleEvent(TitleData.TitleTriggerType triggerType, PlayerPawn player)
    {
        if (TitleSystem.Instance != null)
        {
            TitleSystem.Instance.Trigger(triggerType, player);
            return;
        }

        Debug.LogWarning($"TitleSystem missing in scene. Skipping trigger {triggerType}.");
    }

    private void EnsureTitleSystemExists()
    {
        if (TitleSystem.Instance != null)
            return;

        TitleSystem existingSystem = FindFirstObjectByType<TitleSystem>();
        if (existingSystem != null)
        {
            TitleSystem.Instance = existingSystem;
            return;
        }

        GameObject titleSystemObject = new GameObject("TitleSystem");
        titleSystemObject.AddComponent<TitleSystem>();
    }

    public bool PlayerHasDiceCards(PlayerPawn player)
    {
        foreach (var card in player.hand)
        {
            if (card.effectType == ReprieveEffectType.DiceReroll ||
                card.effectType == ReprieveEffectType.DiceSet)
                return true;
        }
        return false;
    }

    public bool PlayerHasPlayableDiceCards(PlayerPawn player)
    {
        if (player == null)
            return false;

        foreach (var card in player.hand)
        {
            if (card == null)
                continue;

            if (card.effectType != ReprieveEffectType.DiceReroll &&
                card.effectType != ReprieveEffectType.DiceSet)
                continue;

            if (card.CanPlay(player))
                return true;
        }

        if (FindTreasureByName(player, "Gambler's Coin") != null &&
            player.hand != null &&
            player.hand.Count > 0 &&
            CanUseActiveTreasure(player, FindTreasureByName(player, "Gambler's Coin")))
        {
            return true;
        }

        return false;
    }

    public bool CanPlayerAct()
    {
        if (isGameOver)
            return false;

        if (CurrentState != GameState.PlayerTurn)
            return false;

        if (CurrentInputMode != InputMode.Normal)
            return false;

        PlayerPawn player = GetActivePlayer();
        if (player == null || player.isMoving || player.isDead)
            return false;

        return true;
    }

    public bool CanRollButtonAct()
    {
        if (isGameOver)
            return false;

        if (CurrentState == GameState.Pilfer && PilferManager.Instance != null)
            return PilferManager.Instance.CanRollPilfer();

        if (CurrentState == GameState.Combat && CombatManager.Instance != null)
            return CombatManager.Instance.CanRollCombat();

        return CanPlayerAct();
    }

    public bool CanEndTurnButtonAct()
    {
        if (isGameOver)
            return false;

        PlayerPawn activePlayer = GetActivePlayer();
        return
            (CurrentState == GameState.CavePhase &&
             CurrentInputMode == InputMode.Normal &&
             !IsPendingReprieveActive &&
             !HasOpenDiceDecision()) ||
            (
                hasRolledForMovementThisTurn &&
                hasResolvedSpaceAfterMovementThisTurn &&
                activePlayer != null &&
                !activePlayer.isMoving &&
                !activePlayer.isResolvingSpace &&
                CurrentInputMode == InputMode.Normal &&
                CurrentState == GameState.PlayerTurn
            );
    }
    // =========================================================
    // REPRIEVE RESOLUTION SYSTEM (SINGLE AUTHORITY)
    // =========================================================

    public void ResolveReprieveCard(ReprieveCard card, PlayerPawn user, bool restoreFlowState = true)
    {
        if (card == null || user == null)
            return;

        bool resolvesCurrentPendingReprieve = PendingReprieve == card && PendingReprieveUser == user;

        // Remove from hand
        if (user.hand.Contains(card))
            user.hand.Remove(card);

        // Discard unless flagged to return
        if (!card.ShouldReturnToHandOnResolve())
        {
            ReprieveDeck.Instance.Discard(card);
        }
        else
        {
            user.hand.Add(card);
        }

        card.ResetRuntimeState();

        // Register usage
        if (ShouldCountReprieveUse(user))
            RegisterReprieveUse();
        if (CurrentState == GameState.Combat && CombatManager.Instance != null)
            CombatManager.Instance.OnCombatReprieveUsed(user);
        TriggerTitleEvent(TitleData.TitleTriggerType.AfterReprieveUse, user);

        // Dice manipulation cards can resolve while another card, like Gold Pouch,
        // is still waiting on the dice result. Do not clear that original action.
        if (resolvesCurrentPendingReprieve)
        {
            PendingReprieve = null;
            PendingReprieveUser = null;
        }

        // Restore flow state
        if (restoreFlowState &&
            !HasOpenDiceDecision() &&
            (CurrentState == GameState.AwaitingDiceDecision ||
             CurrentState == GameState.ResolvingSpace))
        {
            CurrentState = GetPostReprieveFlowState(user);
        }

        UpdateEndTurnAvailability();

        // UI refresh
        if (playerHUD != null)
            playerHUD.Refresh();

        if (CurrentState == GameState.Combat &&
            CombatManager.Instance != null &&
            CombatManager.Instance.CurrentMonster != null)
        {
            CombatHUDUI.Instance?.Refresh(user, CombatManager.Instance.CurrentMonster);
        }

        HandUIManager.Instance.Refresh();

        if (ShopManager.Instance != null && ShopManager.Instance.CanUseTradeCard(user))
            ShopManager.Instance.RefreshTradeUI();
    }

    // =========================================================
    // REPRIEVE TRACKING
    // =========================================================

    public void RegisterReprieveUse()
    {
        reprievesUsedThisTurn++;
        PlayerPawn activePlayer = GetActivePlayer();
        int effectiveLimit = activePlayer != null
            ? activePlayer.GetEffectiveMaxReprievesPerTurn(maxReprievesPerTurn)
            : maxReprievesPerTurn;

        if (IsCaveActionContext(activePlayer))
        {
            Debug.Log($"Cave reprieve used. Count this cave turn: {reprievesUsedThisTurn}");
            UpdateEndTurnAvailability();
            return;
        }

        Debug.Log($"Reprieve used. Count this turn: {reprievesUsedThisTurn}/{effectiveLimit}");

        // If limit reached, prevent further usage
        if (reprievesUsedThisTurn >= effectiveLimit)
        {
            Debug.Log("Reprieve limit reached for this turn.");
        }

        UpdateEndTurnAvailability();
    }

    public bool CanPlayReprieve(PlayerPawn player)
    {
        if (isGameOver)
            return false;

        if (CurrentState == GameState.Pilfer)
        {
            return PilferManager.Instance != null &&
                   PilferManager.Instance.CanPlayerPlayPilferReprieve(player);
        }

        if (player != GetCurrentActionPlayer())
            return false;

        if (player.isMoving || player.isDead)
            return false;

        bool isCaveActionContext = IsCaveActionContext(player);
        if (CurrentState == GameState.CavePhase || isCaveActionContext)
        {
            if (IsPendingReprieveActive && !HasOpenDiceDecision())
                return false;

            if (!player.CanPlayReprieves())
                return false;

            if (HandUIManager.Instance != null && HandUIManager.Instance.IsForcedDiscardActive)
                return false;

            return true;
        }

        bool isShopTradeContext =
            CurrentState == GameState.ResolvingSpace &&
            ShopManager.Instance != null &&
            ShopManager.Instance.IsShopOpenFor(player);
        bool isCombatContext = CurrentState == GameState.Combat;

        if (isCombatContext && CombatManager.Instance != null && CombatManager.Instance.IsResolvingCombatChoice)
            return false;

        if (CurrentState != GameState.PlayerTurn &&
            CurrentState != GameState.AwaitingDiceDecision &&
            !isCombatContext &&
            !isShopTradeContext)
            return false;

        if (IsPendingReprieveActive && !HasOpenDiceDecision())
            return false;

        if (!player.CanPlayReprieves())
            return false;

        if (HandUIManager.Instance != null && HandUIManager.Instance.IsForcedDiscardActive)
            return false;

        int effectiveLimit = player.GetEffectiveMaxReprievesPerTurn(maxReprievesPerTurn);
        if (isCombatContext && CombatManager.Instance != null)
            effectiveLimit = CombatManager.Instance.GetCombatReprieveLimit(player, effectiveLimit);

        return reprievesUsedThisTurn < effectiveLimit;
    }

    private bool HasOpenDiceDecision()
    {
        bool standardDicePending = DiceManager.Instance != null && DiceManager.Instance.HasPendingRoll;
        bool combatDicePending = CombatManager.Instance != null && CombatManager.Instance.HasPendingCombatRoll;
        return standardDicePending || combatDicePending;
    }

    public void OnForcedDiscardCardChosen(PlayerPawn player)
    {
        if (player == null)
            return;

        int maxHand = player.GetEffectiveMaxHandSize();
        if (player.hand.Count > maxHand)
        {
            Debug.Log($"{player.playerName} must discard {player.hand.Count - maxHand} more card(s).");
            return;
        }

        HandUIManager.Instance?.EndForcedDiscard();
        SetInputMode(InputMode.Normal);

        if (pendingCaveAdvanceAfterDiscard)
        {
            pendingCaveAdvanceAfterDiscard = false;
            AdvanceCaveTurn();
            return;
        }

        if (!pendingEndTurnAfterDiscard)
        {
            UpdateEndTurnAvailability();
            return;
        }

        pendingEndTurnAfterDiscard = false;
        LockEndTurn();
        isAdvancingTurn = true;
        StartCoroutine(AdvanceTurn());
    }

    private void BeginForcedEndTurnDiscard(PlayerPawn player)
    {
        int discardCount = player.hand.Count - player.GetEffectiveMaxHandSize();
        if (discardCount <= 0)
            return;

        Debug.Log($"{player.playerName} must discard {discardCount} card(s) before ending their turn.");
        LockEndTurn();
        SetInputMode(InputMode.SelectingCard);
        HandUIManager.Instance?.BeginForcedDiscard(player);
    }

    // =========================================================
    // PENDING MULTI-STEP ACTIONS
    // =========================================================

    public void BeginPendingReprieve(ReprieveCard card, PlayerPawn user)
    {
        PendingReprieve = card;
        PendingReprieveUser = user;
    }

    public void ResolvePendingReprieve(bool restoreFlowState = true)
    {
        if (PendingReprieve == null || PendingReprieveUser == null)
            return;

        ResolveReprieveCard(PendingReprieve, PendingReprieveUser, restoreFlowState);
    }

    public void CancelPendingReprieve()
    {
        PendingReprieve = null;
        PendingReprieveUser = null;
        UpdateEndTurnAvailability();
    }

    // =========================================================
    // SPECIAL SPACE EFFECTS
    // =========================================================

    public void BeginPitfall(PlayerPawn player)
    {
        CurrentState = GameState.AwaitingDiceDecision;
        endTurnButtonUI.endTurnButton.interactable = false;

        if (player.ShouldAutomaticallyFailPitfalls())
        {
            int forcedDamage = player.GetPitfallFailureDamage();
            if (forcedDamage > 0)
                player.TakeDamage(forcedDamage);

            Debug.Log($"{player.playerName} automatically failed the pitfall due to Bunga Club and took {forcedDamage} damage.");
            EndSpaceResolution();
            return;
        }

        DiceManager.Instance.Roll(
            DiceRollContext.Pitfall,
            player,
            roll =>
            {
                roll += player.GetPitfallModifier();

                if (roll < 4)
                    player.TakeDamage(player.GetPitfallFailureDamage());

                EndSpaceResolution();
            }
        );
    }

    public void OnTeleportResolved()
    {
        CurrentState = GameState.PlayerTurn;
        UpdateEndTurnAvailability();
    }

    public void OnDiceDecisionRequired()
    {
        if (CurrentState != GameState.Combat &&
            CurrentState != GameState.CavePhase &&
            CurrentState != GameState.Pilfer)
        {
            CurrentState = GameState.AwaitingDiceDecision;
        }

        if (playerHUD != null)
            playerHUD.Refresh();

        RefreshActionAvailability();
    }

    public void OnDiceGoldResolved()
    {
        Debug.Log("Gold roll resolved");
        UpdateEndTurnAvailability();
    }

    // =========================================================
    // UI LOCKING SYSTEM
    // =========================================================

    public void LockEndTurn()
    {
        if (endTurnButtonUI != null)
            endTurnButtonUI.endTurnButton.interactable = false;
    }

    public void UnlockEndTurn()
    {
        if (endTurnButtonUI != null)
            endTurnButtonUI.endTurnButton.interactable = true;
    }

    private void UpdateEndTurnAvailability()
    {
        if (CanEndTurnButtonAct())
            UnlockEndTurn();
        else
            LockEndTurn();
    }

    public void LockActionButtons()
    {
        if (endTurnButtonUI != null)
            endTurnButtonUI.endTurnButton.interactable = false;

        if (confirmRollButton != null)
            confirmRollButton.SetActive(false);
    }

    public void UnlockActionButtons()
    {
        UpdateEndTurnAvailability();

        if (confirmRollButton != null)
            confirmRollButton.SetActive(false);
    }

    public void RefreshActionAvailability()
    {
        UpdateEndTurnAvailability();
    }

    public void OnPlayerHealthDepleted(PlayerPawn player)
    {
        if (player == null || player.isDead || isResolvingDeath)
            return;

        BeginPlayerDeath(player);
    }

    public void BeginPlayerDeath(PlayerPawn player)
    {
        if (player == null || player.isDead || isResolvingDeath)
            return;

        StartCoroutine(ResolvePlayerDeath(player));
    }

    // =========================================================
    // UI BUTTON HOOKS
    // =========================================================

    public void OnRollButtonPressed()
    {
        if (isGameOver)
            return;

        if (IsPendingReprieveActive)
        {
            Debug.LogWarning("Cannot roll while a multi-step card action is active.");
            return;
        }

        if (CurrentState == GameState.Pilfer && PilferManager.Instance != null)
        {
            PilferManager.Instance.StartPilferRoll();
            return;
        }

        if (CurrentState == GameState.Combat && CombatManager.Instance != null)
        {
            CombatManager.Instance.StartCombatRoll();
            return;
        }

        RollForMovement();
    }

    public void OnUseTreasureButtonPressed()
    {
        OpenTreasureActivationSelection(GetCurrentTreasureUser());
    }

    public void SetTemporaryActionPlayer(PlayerPawn player)
    {
        temporaryActionPlayer = player;
        RefreshActionAvailability();
    }

    public void ClearTemporaryActionPlayer(PlayerPawn player = null)
    {
        if (player != null && temporaryActionPlayer != player)
            return;

        temporaryActionPlayer = null;
        RefreshActionAvailability();
    }

    public PlayerPawn GetCurrentActionPlayer()
    {
        return temporaryActionPlayer != null ? temporaryActionPlayer : GetActivePlayer();
    }

    public PlayerPawn GetCurrentTreasureUser()
    {
        return GetCurrentHandOwner();
    }

    public bool CanUseAnyActiveTreasure(PlayerPawn player)
    {
        if (player == null || player.equippedTreasures == null)
            return false;

        foreach (TreasureCard treasure in player.equippedTreasures)
        {
            if (CanUseActiveTreasure(player, treasure))
                return true;
        }

        return false;
    }

    public bool CanUseActiveTreasure(PlayerPawn player, TreasureCard treasure)
    {
        if (isGameOver || player == null || treasure == null)
            return false;

        if (player != GetCurrentTreasureUser() || player.isDead || IsPendingReprieveActive)
            return false;

        if (CurrentInputMode != InputMode.Normal)
            return false;

        if (HandUIManager.Instance != null && HandUIManager.Instance.IsForcedDiscardActive)
            return false;

        if (CombatManager.Instance != null && CombatManager.Instance.IsAwaitingSceneContinue)
            return false;

        switch (treasure.cardName)
        {
            case "Gambler's Coin":
                return player.hand != null &&
                       player.hand.Count > 0 &&
                       ((PilferManager.Instance != null && PilferManager.Instance.CanRerollPendingPilferRoll()) ||
                        (CombatManager.Instance != null && CombatManager.Instance.CanRerollPendingCombatRoll()) ||
                        (DiceManager.Instance != null && DiceManager.Instance.CanRerollPending()));

            case "Mushroom Cauldron":
                if (player.hand == null || player.hand.Count == 0)
                    return false;

                if (player.currentHP >= player.GetEffectiveMaxHP())
                    return false;

                if (player.isMoving)
                    return false;

                if (CurrentState == GameState.Pilfer)
                    return PilferManager.Instance != null &&
                           PilferManager.Instance.CurrentRollPlayer == player &&
                           !PilferManager.Instance.IsRollingPilferDice;

                if (CurrentState == GameState.Combat)
                    return CombatManager.Instance != null &&
                           CombatManager.Instance.CurrentPlayer == player &&
                           !CombatManager.Instance.IsRollingCombatDice;

                return CurrentState == GameState.PlayerTurn ||
                       CurrentState == GameState.CavePhase ||
                       CurrentState == GameState.AwaitingDiceDecision;

            default:
                return false;
        }
    }

    public void OpenTreasureActivationSelection(PlayerPawn player)
    {
        if (player == null)
            return;

        List<TreasureCard> usableTreasures = new List<TreasureCard>();
        if (player.equippedTreasures != null)
        {
            foreach (TreasureCard treasure in player.equippedTreasures)
            {
                if (CanUseActiveTreasure(player, treasure))
                    usableTreasures.Add(treasure);
            }
        }

        if (usableTreasures.Count == 0)
        {
            RefreshActionAvailability();
            return;
        }

        SetInputMode(InputMode.SelectingCard);
        LockActionButtons();
        TreasureSelectionUI.EnsureExists();
        TreasureSelectionUI.Instance.BeginSelection(
            usableTreasures,
            $"{player.playerName}: choose a treasure to use",
            selectedTreasure =>
            {
                SetInputMode(InputMode.Normal);

                if (selectedTreasure == null)
                {
                    RefreshActionAvailability();
                    return;
                }

                ActivateTreasure(player, selectedTreasure);
            },
            null,
            true,
            "Cancel");
    }

    private void ActivateTreasure(PlayerPawn player, TreasureCard treasure)
    {
        if (player == null || treasure == null)
        {
            RefreshActionAvailability();
            return;
        }

        switch (treasure.cardName)
        {
            case "Gambler's Coin":
                BeginGamblersCoinUse(player);
                return;

            case "Mushroom Cauldron":
                UseMushroomCauldron(player);
                return;

            default:
                RefreshActionAvailability();
                return;
        }
    }

    private void BeginGamblersCoinUse(PlayerPawn player)
    {
        if (!CanUseActiveTreasure(player, FindTreasureByName(player, "Gambler's Coin")))
        {
            RefreshActionAvailability();
            return;
        }

        SetInputMode(InputMode.SelectingCard);
        LockActionButtons();
        HandUIManager.Instance?.BeginCardSelection(
            player,
            $"{player.playerName}: choose 1 reprieve to discard for Gambler's Coin",
            selectedCard =>
            {
                SetInputMode(InputMode.Normal);

                if (selectedCard == null)
                {
                    RefreshActionAvailability();
                    return;
                }

                if (player.hand.Contains(selectedCard))
                {
                    player.hand.Remove(selectedCard);
                    ReprieveDeck.Instance.Discard(selectedCard);
                    Debug.Log($"{player.playerName} discarded {selectedCard.cardName} for Gambler's Coin.");
                }

                if (PilferManager.Instance != null && PilferManager.Instance.CanRerollPendingPilferRoll())
                    PilferManager.Instance.RerollPendingPilferRoll();
                else if (CombatManager.Instance != null && CombatManager.Instance.CanRerollPendingCombatRoll())
                    CombatManager.Instance.RerollPendingCombatRoll();
                else if (DiceManager.Instance != null && DiceManager.Instance.CanRerollPending())
                    DiceManager.Instance.RerollPending();

                playerHUD?.Refresh();
                if (CombatManager.Instance != null && CombatManager.Instance.CurrentPlayer == player && CombatManager.Instance.CurrentMonster != null)
                    CombatHUDUI.Instance?.Refresh(player, CombatManager.Instance.CurrentMonster);

                RefreshActionAvailability();
            },
            true,
            "Cancel Coin");
    }

    private void UseMushroomCauldron(PlayerPawn player)
    {
        if (!CanUseActiveTreasure(player, FindTreasureByName(player, "Mushroom Cauldron")))
        {
            RefreshActionAvailability();
            return;
        }

        int discardedCards = player.hand != null ? player.hand.Count : 0;
        if (discardedCards <= 0)
        {
            RefreshActionAvailability();
            return;
        }

        while (player.hand.Count > 0)
        {
            ReprieveCard card = player.hand[0];
            player.hand.RemoveAt(0);
            if (card != null)
                ReprieveDeck.Instance.Discard(card);
        }

        int healAmount = player.GetEffectiveMaxHP() - player.currentHP;
        player.Heal(healAmount);
        Debug.Log($"{player.playerName} used Mushroom Cauldron, discarded {discardedCards} reprieve card(s), and fully restored HP.");

        HandUIManager.Instance?.Refresh();
        playerHUD?.Refresh();
        if (CombatManager.Instance != null && CombatManager.Instance.CurrentPlayer == player && CombatManager.Instance.CurrentMonster != null)
            CombatHUDUI.Instance?.Refresh(player, CombatManager.Instance.CurrentMonster);

        RefreshActionAvailability();
    }

    private TreasureCard FindTreasureByName(PlayerPawn player, string treasureName)
    {
        if (player == null || player.equippedTreasures == null)
            return null;

        foreach (TreasureCard treasure in player.equippedTreasures)
        {
            if (treasure == null)
                continue;

            if (string.Equals(treasure.cardName, treasureName, System.StringComparison.OrdinalIgnoreCase))
                return treasure;
        }

        return null;
    }

    public void SetState(GameState state)
    {
        CurrentState = state;
        RefreshActionAvailability();
    }

    public void BeginPeakDecision(PlayerPawn player, BoardSpace gateSpace)
    {
        if (isGameOver || player == null || gateSpace == null)
        {
            EndSpaceResolution();
            return;
        }

        PeakDecisionUI.EnsureExists();
        LockActionButtons();
        SetInputMode(InputMode.SelectingBoard);

        PeakDecisionUI.Instance.Show(
            $"{player.playerName}: travel from your gate to the Peak and fight the hidden Mythic Monster?",
            () =>
            {
                SetInputMode(InputMode.Normal);
                PeakDecisionUI.Instance.Hide();
                player.ClearPendingPeakMovement();
                CombatManager.Instance?.StartPeakCombat(player, gateSpace);
            },
            () =>
            {
                SetInputMode(InputMode.Normal);
                PeakDecisionUI.Instance.Hide();
                player.ResumeMovementAfterPeakDecline();
            });
    }

    public void BeginGameVictory(PlayerPawn winner, Monster mythicMonster)
    {
        if (isGameOver)
            return;

        isGameOver = true;
        SetInputMode(InputMode.Normal);
        SetState(GameState.Animation);
        LockActionButtons();
        HandUIManager.Instance?.HideHand();
        ShopManager.Instance?.CloseTradeSilently();
        PeakDecisionUI.Instance?.Hide();
        VictoryScreenUI.EnsureExists();

        foreach (PlayerPawn player in players)
            player?.SetActiveVisual(player == winner);

        string mythicName = mythicMonster != null ? mythicMonster.monsterName : "the Mythic Monster";
        Debug.Log($"{winner?.playerName} wins the game by defeating {mythicName} on the Peak!");
        VictoryScreenUI.Instance?.ShowVictory(winner, mythicMonster);
    }

    // =========================================================
    // Debugging
    // =========================================================
    public void DebugGameState(string source)
    {
        Debug.Log(
            $"[STATE DEBUG - {source}] " +
            $"State={CurrentState}, " +
            $"HasRolled={hasRolledForMovementThisTurn}, " +
            $"PendingReprieve={(PendingReprieve != null)}, " +
            $"ReprievesUsed={reprievesUsedThisTurn}/{GetActivePlayer()?.GetEffectiveMaxReprievesPerTurn(maxReprievesPerTurn)}, " +
            $"ActivePlayer={GetActivePlayer()?.playerName}"
        );
    }

    private void BuildCaveTurnOrder()
    {
        Dictionary<PlayerPawn, int> finalRolls = new Dictionary<PlayerPawn, int>();
        List<PlayerPawn> playersToRoll = new List<PlayerPawn>(players);

        while (playersToRoll.Count > 0)
        {
            Dictionary<int, List<PlayerPawn>> groupedByRoll = new Dictionary<int, List<PlayerPawn>>();

            foreach (PlayerPawn player in playersToRoll)
            {
                int roll = Random.Range(1, 7);
                finalRolls[player] = roll;

                if (!groupedByRoll.ContainsKey(roll))
                    groupedByRoll[roll] = new List<PlayerPawn>();

                groupedByRoll[roll].Add(player);
            }

            playersToRoll.Clear();

            foreach (KeyValuePair<int, List<PlayerPawn>> pair in groupedByRoll)
            {
                int roll = pair.Key;
                List<PlayerPawn> rolledPlayers = pair.Value;

                if (rolledPlayers.Count == 1)
                {
                    Debug.Log($"{rolledPlayers[0].playerName} rolled {roll} for cave trader order.");
                    continue;
                }

                string tiedNames = string.Join(", ", rolledPlayers.ConvertAll(player => player.playerName));
                Debug.Log($"Cave trader tie on {roll}: {tiedNames}. Rerolling tied players.");
                playersToRoll.AddRange(rolledPlayers);
            }
        }

        caveTurnOrder.Clear();
        caveTurnOrder.AddRange(players);
        caveTurnOrder.Sort((left, right) =>
        {
            int comparison = finalRolls[right].CompareTo(finalRolls[left]);
            if (comparison != 0)
                return comparison;

            return left.startingIndex.CompareTo(right.startingIndex);
        });

        if (caveTurnOrder.Count > 0)
        {
            foreach (PlayerPawn player in caveTurnOrder)
                Debug.Log($"{player.playerName} final cave trader roll: {finalRolls[player]}");

            string finalOrder = string.Join(" -> ", caveTurnOrder.ConvertAll(player => player.playerName));
            Debug.Log($"Cave trader order: {finalOrder}");
        }
    }

    private void BeginNextCaveTurn()
    {
        caveTurnIndex++;
        reprievesUsedThisTurn = 0;

        while (caveTurnIndex < caveTurnOrder.Count && caveTurnOrder[caveTurnIndex] != null && caveTurnOrder[caveTurnIndex].isDead)
        {
            Debug.Log($"Skipping {caveTurnOrder[caveTurnIndex].playerName}'s cave turn because they are dead.");
            caveTurnIndex++;
        }

        if (caveTurnIndex >= caveTurnOrder.Count)
        {
            StartCoroutine(EndCavePhaseRoutine());
            return;
        }

        foreach (PlayerPawn player in players)
            player.SetActiveVisual(false);

        PlayerPawn cavePlayer = caveTurnOrder[caveTurnIndex];
        cavePlayer.SetActiveVisual(true);
        BoardManager.Instance?.OrientViewToPlayer(cavePlayer);
        playerHUD.SetPlayer(cavePlayer);
        UpdateEndTurnAvailability();

        if (ShopManager.Instance != null)
            ShopManager.Instance.OpenWanderingTrader(cavePlayer);

        Debug.Log($"{cavePlayer.playerName} begins their cave phase turn.");
    }

    private void AdvanceCavePhaseFromButton()
    {
        AdvanceCavePhaseFromButtonInternal(false);
    }

    private void AdvanceCavePhaseFromButtonInternal(bool skipUnusedReprievePrompt)
    {
        PlayerPawn cavePlayer = GetActivePlayer();
        if (cavePlayer == null)
            return;

        if (cavePlayer.hand.Count > cavePlayer.GetEffectiveMaxHandSize())
        {
            pendingCaveAdvanceAfterDiscard = true;
            BeginForcedEndTurnDiscard(cavePlayer);
            return;
        }

        AdvanceCaveTurn();
    }

    private void AdvanceCaveTurn()
    {
        PlayerPawn currentCavePlayer = GetActivePlayer();
        if (currentCavePlayer != null)
        {
            ClearTurnScopedCombatBuffs(currentCavePlayer);
            currentCavePlayer.SetActiveVisual(false);
        }

        if (ShopManager.Instance != null)
            ShopManager.Instance.CloseTradeSilently();

        BeginNextCaveTurn();
    }

    private bool TryShowUnusedReprieveEndTurnPrompt(PlayerPawn player, bool isCaveTurn)
    {
        if (player == null || !CanPlayReprieve(player))
            return false;

        int playableReprieveCount = CountPlayableReprieves(player);
        if (playableReprieveCount <= 0)
            return false;

        int effectiveLimit = player.GetEffectiveMaxReprievesPerTurn(maxReprievesPerTurn);
        if (CurrentState == GameState.Combat && CombatManager.Instance != null)
            effectiveLimit = CombatManager.Instance.GetCombatReprieveLimit(player, effectiveLimit);

        int remainingUses = Mathf.Max(0, effectiveLimit - reprievesUsedThisTurn);
        if (remainingUses <= 0)
            return false;

        PeakDecisionUI.EnsureExists();
        SetInputMode(InputMode.SelectingCard);
        LockActionButtons();

        string prompt = $"{player.playerName} can still use {remainingUses} reprieve card(s) this turn.";

        PeakDecisionUI.Instance.Show(
            prompt,
            "End Turn",
            "Return",
            () =>
            {
                SetInputMode(InputMode.Normal);
                if (isCaveTurn)
                    AdvanceCavePhaseFromButtonInternal(true);
                else
                    AdvanceTurnFromButtonInternal(true);
            },
            () =>
            {
                SetInputMode(InputMode.Normal);
                RefreshActionAvailability();
            });

        return true;
    }

    private int CountPlayableReprieves(PlayerPawn player)
    {
        if (player == null || player.hand == null)
            return 0;

        int count = 0;
        foreach (ReprieveCard card in player.hand)
        {
            if (card != null && card.CanPlay(player))
                count++;
        }

        return count;
    }

    private IEnumerator EndCavePhaseRoutine()
    {
        yield return ResolveCretinsCrookEndOfCaveEffects();

        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.CloseTradeSilently();
            ShopManager.Instance.EndWanderingTraderSession();
        }

        foreach (PlayerPawn player in players)
        {
            player.SetActiveVisual(false);
            player.inCavePhase = false;

            if (player.preCaveSpace != null)
                player.PlaceAtIndex(player.preCaveSpace.spaceIndex);
        }

        caveTurnOrder.Clear();
        caveTurnIndex = -1;
        activePlayerIndex = 0;
        CurrentState = GameState.PlayerTurn;
        reprievesUsedThisTurn = 0;

        StartTurn(players[activePlayerIndex]);
    }

    private IEnumerator ResolveCretinsCrookEndOfCaveEffects()
    {
        if (players == null || ShopManager.Instance == null)
            yield break;

        List<PlayerPawn> crookOwners = new List<PlayerPawn>();
        foreach (PlayerPawn player in players)
        {
            if (player == null || player.isDead || !player.HasTreasure("Cretin's Crook"))
                continue;

            crookOwners.Add(player);
        }

        foreach (PlayerPawn player in crookOwners)
        {
            List<TreasureCard> availableOffers = ShopManager.Instance.GetAvailableWanderingTraderOffers();
            if (availableOffers.Count == 0)
                yield break;

            bool rollResolved = false;
            int crookRoll = 0;

            playerHUD?.SetPlayer(player);
            HighlightPlayersForSelection(player, null);
            SetTemporaryActionPlayer(player);
            SetState(GameState.AwaitingDiceDecision);
            LockActionButtons();
            Debug.Log($"{player.playerName} rolls for Cretin's Crook at the end of cave phase.");

            DiceManager.Instance.Roll(
                DiceRollContext.Gold,
                player,
                roll =>
                {
                    crookRoll = roll;
                    rollResolved = true;
                    Debug.Log($"{player.playerName} rolled {roll} for Cretin's Crook.");
                });

            while (!rollResolved)
                yield return null;

            bool continueResolved = false;
            bool shouldContinueToTheft = crookRoll == 6;
            string prompt = crookRoll == 6
                ? $"{player.playerName} rolled 6 for Cretin's Crook. Click Continue to steal from the Wandering Trader."
                : $"{player.playerName} rolled {crookRoll} for Cretin's Crook. Click Continue.";

            PeakDecisionUI.EnsureExists();
            PeakDecisionUI.Instance.Show(
                prompt,
                "Continue",
                crookRoll == 6 ? "Skip Theft" : "Continue",
                () =>
                {
                    shouldContinueToTheft = crookRoll == 6;
                    continueResolved = true;
                },
                () =>
                {
                    shouldContinueToTheft = false;
                    continueResolved = true;
                });

            while (!continueResolved)
                yield return null;

            PeakDecisionUI.Instance.Hide();
            ClearTemporaryActionPlayer(player);

            if (!shouldContinueToTheft)
                continue;

            availableOffers = ShopManager.Instance.GetAvailableWanderingTraderOffers();
            if (availableOffers.Count == 0)
                continue;

            bool selectionResolved = false;
            TreasureSelectionUI.EnsureExists();
            TreasureSelectionUI.Instance.BeginSelection(
                availableOffers,
                $"{player.playerName}: Cretin's Crook lets you steal 1 treasure from the Wandering Trader.",
                selectedTreasure =>
                {
                    if (selectedTreasure == null)
                    {
                        selectionResolved = true;
                        return;
                    }

                    ShopManager.Instance.StealWanderingTraderTreasure(
                        player,
                        selectedTreasure,
                        _ => selectionResolved = true);
                },
                null,
                true,
                "Skip Theft");

            while (!selectionResolved)
                yield return null;
        }

        ClearTemporaryActionPlayer();
        ClearPlayerHighlights();
        RestoreActivePlayerVisual();
    }

    private void PositionPlayersForCavePhase()
    {
        BoardSpace caveSpace = BoardManager.Instance != null ? BoardManager.Instance.GetCaveSpace() : null;
        if (caveSpace == null)
            return;

        Vector3 center = caveSpace.transform.position;
        Vector3[] offsets =
        {
            new Vector3(0f, 2.5f, 0f),
            new Vector3(2.5f, 0f, 0f),
            new Vector3(0f, -2.5f, 0f),
            new Vector3(-2.5f, 0f, 0f)
        };

        for (int i = 0; i < players.Length; i++)
        {
            Vector3 offset = i < offsets.Length ? offsets[i] : new Vector3((i - offsets.Length + 1) * 1.5f, -3.5f, 0f);
            players[i].transform.position = center + offset;
        }
    }

    private IEnumerator HandleStartTurnSpecialTitles(PlayerPawn player)
    {
        yield return null;

        if (player == null || player.isDead || !player.HasTitle("Lustful"))
            yield break;

        List<PlayerPawn> eligibleTargets = GetPlayersWithinDistance(player, 4);
        if (eligibleTargets.Count == 0)
            yield break;

        bool promptResolved = false;
        bool shouldAttemptPitfall = false;
        SetInputMode(InputMode.SelectingBoard);
        LockActionButtons();
        PeakDecisionUI.EnsureExists();
        PeakDecisionUI.Instance.Show(
            $"{player.playerName}: attempt a Lustful pitfall on a player within 4 spaces?",
            "Pitfall",
            "Skip",
            () =>
            {
                shouldAttemptPitfall = true;
                promptResolved = true;
            },
            () =>
            {
                promptResolved = true;
            });

        while (!promptResolved)
            yield return null;

        PeakDecisionUI.Instance.Hide();

        if (!shouldAttemptPitfall)
        {
            SetInputMode(InputMode.Normal);
            RefreshActionAvailability();
            yield break;
        }

        bool selectionResolved = false;
        SetInputMode(InputMode.SelectingPlayer);
        LockActionButtons();
        PlayerSelectionUI.EnsureExists();
        PlayerSelectionUI.Instance.BeginSelection(
            eligibleTargets,
            $"{player.playerName}: choose a player within 4 spaces to attempt to pitfall",
            selectedTarget =>
            {
                SetInputMode(InputMode.Normal);
                selectionResolved = true;

                if (selectedTarget != null)
                    StartCoroutine(ResolveLustfulPitfall(player, selectedTarget));
                else
                    RefreshActionAvailability();
            },
            "Skip Pitfall");

        while (!selectionResolved)
            yield return null;
    }

    private IEnumerator ResolveLustfulPitfall(PlayerPawn sourcePlayer, PlayerPawn targetPlayer)
    {
        if (sourcePlayer == null || targetPlayer == null)
        {
            RefreshActionAvailability();
            yield break;
        }

        bool rollResolved = false;
        SetState(GameState.AwaitingDiceDecision);
        LockActionButtons();
        Debug.Log($"{sourcePlayer.playerName} attempts to pitfall {targetPlayer.playerName} due to Lustful.");

        DiceManager.Instance.Roll(
            DiceRollContext.Pitfall,
            targetPlayer,
            roll =>
            {
                int totalRoll = roll + targetPlayer.GetPitfallModifier();
                if (totalRoll < 4)
                {
                    targetPlayer.TakeDamage(3);
                    Debug.Log($"{targetPlayer.playerName} failed the Lustful pitfall roll and took 3 damage.");
                }
                else
                {
                    Debug.Log($"{targetPlayer.playerName} avoided the Lustful pitfall effect.");
                }

                rollResolved = true;
            });

        while (!rollResolved)
            yield return null;

        if (targetPlayer.currentHP > 0 && !targetPlayer.isDead)
        {
            SetState(GameState.PlayerTurn);
            RestoreActivePlayerVisual();
        }
    }

    private IEnumerator ResolveChaoticEndTurnIfNeeded(PlayerPawn player)
    {
        if (player == null || player.isDead || !player.HasTitle("Chaotic"))
            yield break;

        TreasureDeck.EnsureExists();
        TreasureCard replacementTreasure = TreasureDeck.Instance != null
            ? TreasureDeck.Instance.DrawTreasure()
            : null;

        if (replacementTreasure == null)
            yield break;

        if (player.equippedTreasures.Count == 0)
        {
            player.AcquireTreasure(
                replacementTreasure,
                $"{player.playerName}: Chaotic grants a new treasure.",
                acquired =>
                {
                    if (acquired)
                        Debug.Log($"{player.playerName} gained {replacementTreasure.cardName} due to Chaotic.");
                });
            yield break;
        }

        bool choiceResolved = false;
        SetInputMode(InputMode.SelectingCard);
        LockActionButtons();
        TreasureSelectionUI.EnsureExists();
        TreasureSelectionUI.Instance.BeginSelection(
            player,
            $"{player.playerName}: Chaotic makes you discard 1 treasure and gain a new one.",
            selectedTreasure =>
            {
                if (selectedTreasure != null && player.equippedTreasures.Contains(selectedTreasure))
                {
                    player.equippedTreasures.Remove(selectedTreasure);
                    TreasureDeck.Instance?.DiscardTreasure(selectedTreasure);
                    Debug.Log($"{player.playerName} discarded {selectedTreasure.cardName} due to Chaotic.");
                }

                player.AcquireTreasure(
                    replacementTreasure,
                    $"{player.playerName}: equip the new Chaotic treasure.",
                    acquired =>
                    {
                        if (acquired)
                            Debug.Log($"{player.playerName} gained {replacementTreasure.cardName} due to Chaotic.");
                        else
                            Debug.Log($"{player.playerName} discarded {replacementTreasure.cardName} due to Chaotic.");

                        SetInputMode(InputMode.Normal);
                        choiceResolved = true;
                    });
            },
            replacementTreasure,
            false);

        while (!choiceResolved)
            yield return null;
    }

    private List<PlayerPawn> GetPlayersWithinDistance(PlayerPawn sourcePlayer, int maxDistance)
    {
        List<PlayerPawn> nearbyPlayers = new List<PlayerPawn>();
        if (sourcePlayer == null || players == null || BoardManager.Instance == null)
            return nearbyPlayers;

        foreach (PlayerPawn candidate in players)
        {
            if (candidate == null || candidate == sourcePlayer || candidate.isDead)
                continue;

            int distance = BoardManager.Instance.GetCircularDistance(sourcePlayer.currentIndex, candidate.currentIndex);
            if (distance <= maxDistance)
                nearbyPlayers.Add(candidate);
        }

        return nearbyPlayers;
    }

    private IEnumerator ResolvePlayerDeath(PlayerPawn deadPlayer)
    {
        isResolvingDeath = true;
        bool diedDuringCavePhase = CurrentState == GameState.CavePhase;

        if (deadPlayer == null)
        {
            isResolvingDeath = false;
            yield break;
        }

        deadPlayer.MarkDead();
        deadPlayer.PlaceAtIndex(deadPlayer.startingIndex);
        SetInputMode(InputMode.Normal);
        SetState(GameState.Animation);
        LockActionButtons();
        ShopManager.Instance?.CloseTradeSilently();
        HandUIManager.Instance?.HideHand();
        CombatHUDUI.Instance?.Hide();

        Debug.Log($"{deadPlayer.playerName} died. Resolving death penalties.");

        List<PlayerPawn> looters = GetDeathLootOrder(deadPlayer);
        foreach (PlayerPawn looter in looters)
        {
            if (deadPlayer.hand.Count == 0)
                break;

            bool lootChosen = false;
            HighlightPlayersForSelection(looter, deadPlayer);
            Debug.Log($"{looter.playerName} may loot 1 reprieve card from {deadPlayer.playerName}.");

            HandUIManager.Instance?.BeginCardSelection(
                deadPlayer,
                $"{looter.playerName}: choose 1 reprieve to loot from {deadPlayer.playerName}",
                selectedCard =>
                {
                    if (selectedCard == null)
                    {
                        lootChosen = true;
                        return;
                    }

                    deadPlayer.hand.Remove(selectedCard);
                    looter.hand.Add(selectedCard);
                    Debug.Log($"{looter.playerName} looted {selectedCard.cardName} from {deadPlayer.playerName}.");
                    lootChosen = true;
                });

            while (!lootChosen)
                yield return null;
        }

        if (deadPlayer.titles.Count > 0)
        {
            bool titleChosen = false;
            TitleUIManager.EnsureExists();
            HighlightPlayersForSelection(deadPlayer, null);
            Debug.Log($"{deadPlayer.playerName} must choose 1 title to lose.");
            TitleUIManager.Instance.BeginTitleSelection(deadPlayer, $"{deadPlayer.playerName}: choose 1 title to lose", selectedTitle =>
            {
                if (selectedTitle != null)
                {
                    deadPlayer.RemoveTitle(selectedTitle);
                    Debug.Log($"{deadPlayer.playerName} lost the title {selectedTitle.titleName}.");
                }

                titleChosen = true;
            });

            while (!titleChosen)
                yield return null;
        }

        if (deadPlayer.equippedTreasures.Count > 0)
        {
            bool treasureChosen = false;
            TreasureSelectionUI.EnsureExists();
            HighlightPlayersForSelection(deadPlayer, null);
            Debug.Log($"{deadPlayer.playerName} must choose 1 treasure to lose.");
            TreasureSelectionUI.Instance.BeginSelection(deadPlayer, $"{deadPlayer.playerName}: choose 1 treasure to lose", selectedTreasure =>
            {
                if (selectedTreasure != null)
                {
                    deadPlayer.equippedTreasures.Remove(selectedTreasure);
                    TreasureDeck.Instance?.DiscardTreasure(selectedTreasure);
                    Debug.Log($"{deadPlayer.playerName} lost the treasure {selectedTreasure.cardName}.");
                }

                treasureChosen = true;
            });

            while (!treasureChosen)
                yield return null;
        }

        int goldLost = Mathf.Min(deadPlayer.gold, 400);
        deadPlayer.SpendGold(goldLost);
        Debug.Log($"{deadPlayer.playerName} lost {goldLost} gold.");
        ClearPlayerHighlights();
        RestoreActivePlayerVisual();

        if (playerHUD != null)
            playerHUD.Refresh();

        yield return null;

        isResolvingDeath = false;

        if (diedDuringCavePhase)
        {
            SetState(GameState.CavePhase);
            if (deadPlayer == GetActivePlayer())
                AdvanceCaveTurn();
            else
            {
                UpdateEndTurnAvailability();
                RestoreActivePlayerVisual();
            }

            yield break;
        }

        if (deadPlayer == GetActivePlayer())
        {
            AdvancePastDeadPlayer();
            yield break;
        }

        SetState(GameState.PlayerTurn);
        UpdateEndTurnAvailability();
        RestoreActivePlayerVisual();
    }

    private List<PlayerPawn> GetDeathLootOrder(PlayerPawn deadPlayer)
    {
        List<PlayerPawn> candidates = new List<PlayerPawn>();
        foreach (PlayerPawn player in players)
        {
            if (player == null || player == deadPlayer || player.isDead)
                continue;

            candidates.Add(player);
        }

        List<PlayerPawn> ordered = new List<PlayerPawn>();
        while (candidates.Count > 0)
        {
            int lowestHp = int.MaxValue;
            foreach (PlayerPawn candidate in candidates)
                lowestHp = Mathf.Min(lowestHp, candidate.currentHP);

            List<PlayerPawn> tied = candidates.FindAll(player => player.currentHP == lowestHp);
            while (tied.Count > 0)
            {
                int selectedIndex = Random.Range(0, tied.Count);
                PlayerPawn selected = tied[selectedIndex];
                ordered.Add(selected);
                candidates.Remove(selected);
                tied.RemoveAt(selectedIndex);
            }
        }

        return ordered;
    }

    private void RespawnDeadPlayersForCavePhase()
    {
        foreach (PlayerPawn player in players)
        {
            if (player == null || !player.isDead)
                continue;

            player.RespawnForCavePhase();
            Debug.Log($"{player.playerName} respawned at their gate with full HP and a new starting hand.");
        }
    }

    private void AdvancePastDeadPlayer()
    {
        PlayerPawn currentPlayer = GetActivePlayer();
        if (currentPlayer != null)
            currentPlayer.SetActiveVisual(false);

        activePlayerIndex++;

        while (activePlayerIndex < players.Length && players[activePlayerIndex] != null && players[activePlayerIndex].isDead)
        {
            Debug.Log($"Skipping {players[activePlayerIndex].playerName}'s turn because they are dead.");
            activePlayerIndex++;
        }

        if (activePlayerIndex >= players.Length)
        {
            StartCavePhase();
            return;
        }

        StartTurn(players[activePlayerIndex]);
    }

    private void HighlightPlayersForSelection(PlayerPawn primary, PlayerPawn secondary)
    {
        foreach (PlayerPawn player in players)
        {
            if (player == null)
                continue;

            bool isHighlighted = player == primary || player == secondary;
            player.SetActiveVisual(isHighlighted);
        }
    }

    private void ClearPlayerHighlights()
    {
        foreach (PlayerPawn player in players)
        {
            if (player == null)
                continue;

            player.SetActiveVisual(false);
        }
    }

    public void RestoreActivePlayerVisual()
    {
        PlayerPawn activePlayer = GetActivePlayer();
        if (activePlayer == null || activePlayer.isDead)
            return;

        foreach (PlayerPawn player in players)
        {
            if (player == null)
                continue;

            player.SetActiveVisual(player == activePlayer);
        }

        if (playerHUD != null)
            playerHUD.SetPlayer(activePlayer);
    }

    public PlayerPawn GetCurrentHandOwner()
    {
        if (temporaryActionPlayer != null)
            return temporaryActionPlayer;

        if (CurrentState == GameState.Pilfer && PilferManager.Instance != null && PilferManager.Instance.IsPilferActive)
            return PilferManager.Instance.CurrentRollPlayer;

        return GetActivePlayer();
    }

    private bool IsCaveTurnActive()
    {
        if (caveTurnIndex < 0 || caveTurnIndex >= caveTurnOrder.Count)
            return false;

        PlayerPawn cavePlayer = caveTurnOrder[caveTurnIndex];
        return cavePlayer != null && cavePlayer.inCavePhase;
    }

    private bool IsCaveActionContext(PlayerPawn player)
    {
        return player != null &&
               IsCaveTurnActive() &&
               caveTurnOrder[caveTurnIndex] == player;
    }

    private GameState GetPostReprieveFlowState(PlayerPawn user)
    {
        return IsCaveActionContext(user) ? GameState.CavePhase : GameState.PlayerTurn;
    }

    private bool ShouldCountReprieveUse(PlayerPawn user)
    {
        if (user == null)
            return false;

        if (CurrentState == GameState.Pilfer && PilferManager.Instance != null)
            return PilferManager.Instance.ShouldCountReprieveUse(user);

        return user == GetActivePlayer();
    }

    private void TryOfferPilfer(PlayerPawn activePlayer)
    {
        if (activePlayer == null ||
            activePlayer.isDead ||
            activePlayer.inCavePhase ||
            CurrentState != GameState.PlayerTurn ||
            CurrentInputMode != InputMode.Normal ||
            IsPendingReprieveActive ||
            CombatManager.Instance != null && CombatManager.Instance.IsCombatActive ||
            PilferManager.Instance == null ||
            !PilferManager.Instance.HasEligiblePilferTargets(activePlayer))
        {
            return;
        }

        LockActionButtons();
        SetInputMode(InputMode.SelectingPlayer);
        PeakDecisionUI.EnsureExists();
        PeakDecisionUI.Instance.Show(
            $"{activePlayer.playerName}: you are within 2 spaces of another player. Attempt to pilfer?",
            "Pilfer",
            "Skip",
            () =>
            {
                PeakDecisionUI.Instance.Hide();
                StartCoroutine(BeginPilferTargetSelection(activePlayer));
            },
            () =>
            {
                SetInputMode(InputMode.Normal);
                PeakDecisionUI.Instance.Hide();
                RefreshActionAvailability();
            });
    }

    private bool ShouldOfferPilferAtEndOfTurn(PlayerPawn activePlayer)
    {
        if (pilferCheckedThisTurn)
            return false;

        if (activePlayer == null ||
            activePlayer.isDead ||
            activePlayer.inCavePhase ||
            CurrentState != GameState.PlayerTurn ||
            CurrentInputMode != InputMode.Normal ||
            IsPendingReprieveActive ||
            (CombatManager.Instance != null && CombatManager.Instance.IsCombatActive) ||
            PilferManager.Instance == null)
        {
            return false;
        }

        return PilferManager.Instance.HasEligiblePilferTargets(activePlayer);
    }

    private IEnumerator BeginPilferTargetSelection(PlayerPawn activePlayer)
    {
        yield return null;

        if (activePlayer == null || PilferManager.Instance == null)
        {
            SetInputMode(InputMode.Normal);
            RefreshActionAvailability();
            yield break;
        }

        SetInputMode(InputMode.SelectingPlayer);
        PlayerSelectionUI.EnsureExists();
        PlayerSelectionUI.Instance.BeginSelection(
            PilferManager.Instance.GetEligiblePilferTargets(activePlayer),
            $"{activePlayer.playerName}: choose a nearby player to pilfer",
            selectedPlayer =>
            {
                SetInputMode(InputMode.Normal);

                if (selectedPlayer == null || !PilferManager.Instance.IsValidPilferTarget(activePlayer, selectedPlayer))
                {
                    RefreshActionAvailability();
                    return;
                }

                PilferManager.Instance.BeginPilfer(activePlayer, selectedPlayer);
            },
            "Skip Pilfer");

        RefreshActionAvailability();
    }

}
