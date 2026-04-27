using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class CombatManager : MonoBehaviour
{
    public static CombatManager Instance;

    public PlayerPawn CurrentPlayer { get; private set; }
    public Monster CurrentMonster { get; private set; }
    public Monster.MonsterBiome CurrentBiome { get; private set; }
    public bool HasPendingCombatRoll => pendingCombatRollResults != null;
    public bool IsRollingCombatDice { get; private set; }
    public bool IsCombatActive => CurrentPlayer != null && CurrentMonster != null;
    public bool IsAwaitingRunawayRoll { get; private set; }
    public bool IsResolvingCombatChoice { get; private set; }
    public bool IsAwaitingSceneContinue { get; private set; }

    private int[] pendingCombatRollResults;
    private int[] displayedCombatRollResults;
    private int pendingCombatRollTotal;
    private int combatRollCount;
    private int failedCombatRollCount;
    private int monsterMightModifier;
    private int combatReprievesUsed;
    private bool isPeakCombat;
    private bool terraTarantulaThresholdTriggered;
    private bool cautiousRunawayTriggeredThisCombat;
    private bool isForcedTitleRunaway;
    private bool showRunawayRollDisplay;
    private int lastRunawayRollValue;
    private int peakReturnIndex = -1;
    private PlayerPawn pendingVictoryPlayer;
    private Monster pendingVictoryMonster;
    private PlayerPawn pendingDefeatedPlayer;
    private bool pendingRestartTurnAfterMythicArmor;
    private bool pendingCombatWon;
    private bool pendingCombatEndedGame;
    private string pendingSceneContinueMessage;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        MonsterRoster.EnsureExists();
        CombatSceneDirector.EnsureExists();
    }

    public void StartBiomeCombat(PlayerPawn player, Monster.MonsterBiome biome)
    {
        if (player == null)
            return;

        MonsterRoster.EnsureExists();
        MonsterSelectionUI.EnsureExists();
        CombatHUDUI.EnsureExists();

        List<Monster> availableMonsters = MonsterRoster.Instance != null
            ? MonsterRoster.Instance.GetAvailableMonsters(biome)
            : new List<Monster>();

        if (availableMonsters.Count == 0)
        {
            Debug.LogWarning($"No available monsters were found for biome {biome}.");
            GameManager.Instance.EndSpaceResolution();
            return;
        }

        CurrentPlayer = player;
        CurrentBiome = biome;
        CurrentMonster = null;
        pendingCombatRollResults = null;
        displayedCombatRollResults = null;
        pendingCombatRollTotal = 0;
        combatRollCount = 0;
        failedCombatRollCount = 0;
        monsterMightModifier = 0;
        combatReprievesUsed = 0;
        terraTarantulaThresholdTriggered = false;
        cautiousRunawayTriggeredThisCombat = false;
        isForcedTitleRunaway = false;
        showRunawayRollDisplay = false;
        lastRunawayRollValue = 0;
        IsAwaitingRunawayRoll = false;
        IsResolvingCombatChoice = false;
        IsAwaitingSceneContinue = false;
        pendingDefeatedPlayer = null;
        pendingCombatWon = false;
        pendingCombatEndedGame = false;
        pendingRestartTurnAfterMythicArmor = false;
        pendingSceneContinueMessage = null;

        GameManager.Instance.SetState(GameState.Combat);
        GameManager.Instance.LockActionButtons();
        player.isResolvingSpace = true;

        MonsterSelectionUI.Instance.BeginSelection(
            player,
            biome,
            availableMonsters,
            BeginCombatAgainstMonster);
    }

    public void StartPeakCombat(PlayerPawn player, BoardSpace returnGateSpace)
    {
        if (player == null)
            return;

        MonsterRoster.EnsureExists();
        CombatHUDUI.EnsureExists();

        Monster peakMythic = MonsterRoster.Instance != null
            ? MonsterRoster.Instance.CurrentPeakMythic
            : null;

        if (peakMythic == null || MonsterRoster.Instance.IsDefeated(peakMythic))
        {
            Debug.LogWarning("No available Mythic Monster is waiting on the Peak.");
            GameManager.Instance.EndSpaceResolution();
            return;
        }

        CurrentPlayer = player;
        CurrentBiome = Monster.MonsterBiome.Peak;
        CurrentMonster = null;
        pendingCombatRollResults = null;
        displayedCombatRollResults = null;
        pendingCombatRollTotal = 0;
        combatRollCount = 0;
        failedCombatRollCount = 0;
        monsterMightModifier = 0;
        combatReprievesUsed = 0;
        terraTarantulaThresholdTriggered = false;
        cautiousRunawayTriggeredThisCombat = false;
        isForcedTitleRunaway = false;
        showRunawayRollDisplay = false;
        lastRunawayRollValue = 0;
        IsAwaitingRunawayRoll = false;
        IsResolvingCombatChoice = false;
        IsAwaitingSceneContinue = false;
        pendingDefeatedPlayer = null;
        pendingCombatWon = false;
        pendingCombatEndedGame = false;
        pendingRestartTurnAfterMythicArmor = false;
        pendingSceneContinueMessage = null;
        isPeakCombat = true;
        peakReturnIndex = returnGateSpace != null ? returnGateSpace.spaceIndex : player.currentIndex;

        BoardSpace peakSpace = BoardManager.Instance != null ? BoardManager.Instance.GetPeakSpace() : null;
        if (peakSpace != null)
            player.transform.position = peakSpace.transform.position;

        GameManager.Instance.SetState(GameState.Combat);
        GameManager.Instance.LockActionButtons();
        player.isResolvingSpace = true;

        BeginCombatAgainstMonster(peakMythic);
    }

    public bool CanRollCombat()
    {
        return IsCombatActive &&
               GameManager.Instance != null &&
               GameManager.Instance.CurrentState == GameState.Combat &&
               !IsRollingCombatDice &&
               !IsResolvingCombatChoice &&
               !IsAwaitingSceneContinue &&
               !HasPendingCombatRoll &&
               !GameManager.Instance.IsPendingReprieveActive;
    }

    public void StartCombatRoll()
    {
        if (!CanRollCombat())
            return;

        if (IsAwaitingRunawayRoll)
        {
            BeginRunawayRoll();
            return;
        }

        int diceCount = GetCombatDiceCount(CurrentPlayer.GetCombatArcane());
        if (diceCount <= 0)
        {
            pendingCombatRollResults = new int[0];
            displayedCombatRollResults = new int[0];
            pendingCombatRollTotal = 0;
            CombatHUDUI.Instance?.SetPendingRoll(pendingCombatRollResults, pendingCombatRollTotal);
            CombatHUDUI.Instance?.SetStatus("Arcane gives 0 dice. Resolving from base Might.");
            ResolveCombatRoll();
            return;
        }

        CombatHUDUI.Instance?.SetStatus($"Rolling {diceCount} combat die{(diceCount == 1 ? "" : "s")}...");
        StartCoroutine(RollCombatDiceCoroutine(diceCount));
    }

    public bool CanRerollPendingCombatRoll()
    {
        return IsCombatActive && HasPendingCombatRoll && !IsRollingCombatDice;
    }

    public void RerollPendingCombatRoll()
    {
        if (!CanRerollPendingCombatRoll())
            return;

        StartCoroutine(RollCombatDiceCoroutine(pendingCombatRollResults.Length));
    }

    public bool CanForcePendingCombatRoll()
    {
        return IsCombatActive && HasPendingCombatRoll && !IsRollingCombatDice;
    }

    public void ForcePendingCombatRoll(int value)
    {
        if (!CanForcePendingCombatRoll())
            return;

        int clamped = Mathf.Clamp(value, 1, 6);
        for (int i = 0; i < pendingCombatRollResults.Length; i++)
            pendingCombatRollResults[i] = clamped;

        pendingCombatRollTotal = clamped * pendingCombatRollResults.Length;
        displayedCombatRollResults = (int[])pendingCombatRollResults.Clone();
        DiceManager.Instance?.diceUI?.ShowRoll(clamped);
        CombatHUDUI.Instance?.SetPendingRoll(pendingCombatRollResults, pendingCombatRollTotal);
        CombatHUDUI.Instance?.SetStatus($"Combat dice set to {clamped}.");
        Debug.Log($"Combat roll forced to {DescribePendingRoll()}");
    }

    public int[] GetDisplayedCombatRollResults()
    {
        if (showRunawayRollDisplay)
            return new[] { lastRunawayRollValue };

        return displayedCombatRollResults != null ? (int[])displayedCombatRollResults.Clone() : null;
    }

    public int GetCurrentCombatDiceCount()
    {
        if (showRunawayRollDisplay || IsAwaitingRunawayRoll)
            return 1;

        return CurrentPlayer != null ? GetCombatDiceCount(CurrentPlayer.GetCombatArcane()) : 0;
    }

    public void ConfirmPendingCombatRoll()
    {
        if (!HasPendingCombatRoll || IsRollingCombatDice)
            return;

        ResolveCombatRoll();
    }

    public void OnMonsterDamagedExternally()
    {
        if (CurrentMonster != null && CurrentMonster.IsDead)
            HandleMonsterDefeated();
    }

    public void RequestCombatSceneContinue()
    {
        if (!IsAwaitingSceneContinue)
            return;

        IsAwaitingSceneContinue = false;
        StartCoroutine(EndCombatRoutine(pendingCombatWon, pendingCombatEndedGame));
    }

    private void BeginCombatAgainstMonster(Monster monster)
    {
        if (monster == null || CurrentPlayer == null)
        {
            AbortCombatSelection();
            return;
        }

        CurrentMonster = monster;
        monsterMightModifier = 0;
        pendingCombatRollResults = null;
        displayedCombatRollResults = null;
        pendingCombatRollTotal = 0;
        combatRollCount = 0;
        failedCombatRollCount = 0;
        terraTarantulaThresholdTriggered = false;

        CurrentMonster.PrepareForCombat();
        MonsterRoster.Instance?.RevealMonster(CurrentMonster);

        Debug.Log(
            $"{CurrentPlayer.playerName} revealed {CurrentMonster.monsterName} " +
            $"(HP {CurrentMonster.maxHP}, Might {CurrentMonster.might}).");

        if (!string.IsNullOrWhiteSpace(CurrentMonster.RewardTitleName))
            Debug.Log($"Reward title: {CurrentMonster.RewardTitleName}");
        else if (CurrentMonster.isMythicMonster)
            Debug.Log("This is the Mythic Monster. Defeating it wins the game.");

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetState(GameState.Combat);
            GameManager.Instance.RefreshActionAvailability();
        }

        if (CurrentPlayer != null && GameManager.Instance?.playerHUD != null)
            GameManager.Instance.playerHUD.SetPlayer(CurrentPlayer);

        CombatHUDUI.Instance?.ShowCombat(CurrentPlayer, CurrentMonster);
        CombatSceneDirector.Instance?.BeginCombatScene(CurrentPlayer);
        ApplyMythicCombatOpeningRules();

        if (TryForceCautiousRunaway())
            return;

        Debug.Log($"{CurrentPlayer.playerName} may play reprieves, then click Roll to start combat.");
    }

    private IEnumerator RollCombatDiceCoroutine(int diceCount)
    {
        IsRollingCombatDice = true;
        pendingCombatRollResults = null;
        displayedCombatRollResults = null;
        pendingCombatRollTotal = 0;
        GameManager.Instance.LockActionButtons();

        int[] results = new int[diceCount];
        displayedCombatRollResults = new int[diceCount];
        for (int i = 0; i < diceCount; i++)
        {
            float elapsed = 0f;
            while (elapsed < 0.2f)
            {
                elapsed += Time.deltaTime;
                DiceManager.Instance?.diceUI?.ShowRoll(Random.Range(1, 7));
                yield return null;
            }

            results[i] = Random.Range(1, 7);
            displayedCombatRollResults[i] = results[i];
            DiceManager.Instance?.diceUI?.ShowRoll(results[i]);
            yield return new WaitForSeconds(0.08f);
        }

        pendingCombatRollResults = results;
        displayedCombatRollResults = (int[])results.Clone();
        foreach (int value in results)
            pendingCombatRollTotal += value;

        IsRollingCombatDice = false;
        CombatHUDUI.Instance?.SetPendingRoll(pendingCombatRollResults, pendingCombatRollTotal);
        bool canModifyRoll = GameManager.Instance != null && GameManager.Instance.PlayerHasPlayableDiceCards(CurrentPlayer);

        if (canModifyRoll)
        {
            ConfirmRollUI.Instance?.Show();
            CombatHUDUI.Instance?.SetStatus("Combat roll ready. Confirm or use dice cards.");
        }
        else
        {
            ConfirmRollUI.Instance?.Hide();
            CombatHUDUI.Instance?.SetStatus("Combat roll ready. No dice cards available, resolving now.");
            ResolveCombatRoll();
            yield break;
        }

        Debug.Log($"Combat roll ready: {DescribePendingRoll()}");
    }

    private void ResolveCombatRoll()
    {
        int playerMight = CurrentPlayer.GetCombatMight();
        int monsterMight = CurrentMonster.might + monsterMightModifier;
        int attackValue = playerMight + pendingCombatRollTotal;
        bool consumedNextRollBuff = CurrentPlayer != null && CurrentPlayer.HasNextCombatRollBuffs();

        StringBuilder summary = new StringBuilder();
        summary.Append($"{CurrentPlayer.playerName} total {attackValue} ");
        summary.Append($"(Might {playerMight}");
        if (pendingCombatRollResults != null && pendingCombatRollResults.Length > 0)
            summary.Append($" + dice {DescribePendingRoll()}");
        summary.Append($") vs {CurrentMonster.monsterName} Might {monsterMight}.");
        Debug.Log(summary.ToString());

        if (pendingCombatRollResults != null && pendingCombatRollResults.Length > 0)
            GameManager.Instance?.ResolveTreasureRolledDieEffects(CurrentPlayer, pendingCombatRollResults);

        ConfirmRollUI.Instance?.Hide();
        pendingCombatRollResults = null;
        pendingCombatRollTotal = 0;
        showRunawayRollDisplay = false;
        lastRunawayRollValue = 0;
        combatRollCount++;

        if (consumedNextRollBuff)
            CurrentPlayer.ConsumeNextCombatRollBuffs();

        if (attackValue > monsterMight)
        {
            int damage = attackValue - monsterMight;
            CurrentMonster.TakeDamage(damage);
            CombatHUDUI.Instance?.Refresh(CurrentPlayer, CurrentMonster);
            CombatHUDUI.Instance?.SetPendingRoll(null, 0);
            CombatHUDUI.Instance?.SetStatus($"{CurrentMonster.monsterName} takes {damage} damage.");

            if (CurrentMonster.IsDead)
            {
                HandleMonsterDefeated();
                return;
            }

            ResolveMythicMonsterDamageEffects();
        }
        else if (attackValue < monsterMight)
        {
            int damage = monsterMight - attackValue;
            CurrentPlayer.TakeDamage(damage);
            failedCombatRollCount++;
            CombatHUDUI.Instance?.Refresh(CurrentPlayer, CurrentMonster);
            CombatHUDUI.Instance?.SetPendingRoll(null, 0);
            CombatHUDUI.Instance?.SetStatus($"{CurrentPlayer.playerName} takes {damage} damage.");
            ResolveMythicFailedRollEffects();

            if (CurrentPlayer.currentHP <= 0)
            {
                AttemptRunAway();
                return;
            }

            if (TryForceCautiousRunaway())
                return;
        }
        else
        {
            CombatHUDUI.Instance?.SetPendingRoll(null, 0);
            CombatHUDUI.Instance?.SetStatus("Tie. No damage dealt.");
            Debug.Log("Combat roll tied. No damage dealt.");
        }

        if (ResolveMythicEndOfRollEffects())
            return;

        CombatHUDUI.Instance?.Refresh(CurrentPlayer, CurrentMonster);
        Debug.Log($"{CurrentPlayer.playerName} may play reprieves or click Roll for the next exchange.");
    }

    private void ResolveMythicFailedRollEffects()
    {
        if (CurrentMonster == null || !CurrentMonster.isMythicMonster)
            return;

        switch (CurrentMonster.mythicEffectType)
        {
            case Monster.MythicEffectType.BurnOnFailedRoll:
                int burnDamage = Mathf.Max(0, CurrentMonster.mythicEffectValue);
                if (burnDamage > 0)
                {
                    CurrentPlayer.TakeDamage(burnDamage);
                    CombatHUDUI.Instance?.Refresh(CurrentPlayer, CurrentMonster);
                    CombatHUDUI.Instance?.SetStatus($"{CurrentMonster.monsterName} burns for {burnDamage} extra HP.");
                    Debug.Log($"{CurrentMonster.monsterName} burns {CurrentPlayer.playerName} for {burnDamage} extra HP.");
                }
                break;

            case Monster.MythicEffectType.Special:
                ResolveSpecialMythicFailedRollEffects();
                break;
        }
    }

    private void AttemptRunAway()
    {
        if (IsRunawayBlockedByCurrentMythic())
        {
            Debug.Log($"{CurrentPlayer.playerName} cannot run away from {CurrentMonster.monsterName}.");
            CombatHUDUI.Instance?.SetStatus($"{CurrentMonster.monsterName} prevents runaway.");
            HandlePlayerDefeated();
            return;
        }

        if (!CurrentPlayer.CanRunAway())
        {
            Debug.Log($"{CurrentPlayer.playerName} cannot run away from this combat.");
            HandlePlayerDefeated();
            return;
        }

        IsAwaitingRunawayRoll = true;
        ConfirmRollUI.Instance?.Hide();
        CombatHUDUI.Instance?.SetPendingRoll(null, 0);
        CombatHUDUI.Instance?.SetStatus($"{CurrentPlayer.playerName} fell to 0 HP. Click Roll to attempt runaway.");
        Debug.Log($"{CurrentPlayer.playerName} fell to 0 HP and must roll to attempt runaway.");
    }

    public void HandleExternalDamageToCurrentPlayer(PlayerPawn targetPlayer)
    {
        if (!IsCombatActive || targetPlayer == null || CurrentPlayer != targetPlayer)
            return;

        if (IsAwaitingRunawayRoll || IsAwaitingSceneContinue || CurrentPlayer.currentHP > 0)
            return;

        AttemptRunAway();
    }

    private void BeginRunawayRoll()
    {
        if (!IsAwaitingRunawayRoll || CurrentPlayer == null)
            return;

        CombatHUDUI.Instance?.SetStatus("Rolling runaway die...");
        showRunawayRollDisplay = true;
        lastRunawayRollValue = 0;
        displayedCombatRollResults = null;

        DiceManager.Instance.Roll(
            DiceRollContext.Special,
            CurrentPlayer,
            ResolveRunawayRoll);
    }

    private void ResolveRunawayRoll(int roll)
    {
        int runawayModifier = CurrentPlayer.GetRunawayModifier();
        int total = roll + runawayModifier;
        string runawayRollText = runawayModifier == 0
            ? $"Run away roll: {roll}"
            : $"Run away roll: {roll} + {runawayModifier} = {total}";

        IsAwaitingRunawayRoll = false;
        lastRunawayRollValue = roll;
        displayedCombatRollResults = new[] { roll };
        CombatHUDUI.Instance?.SetStatus(runawayRollText);
        Debug.Log($"{CurrentPlayer.playerName} attempts to run away. {runawayRollText}.");

        if (total >= 4)
        {
            if (isForcedTitleRunaway)
            {
                CombatHUDUI.Instance?.Refresh(CurrentPlayer, CurrentMonster);
                CombatHUDUI.Instance?.SetStatus($"{CurrentPlayer.playerName} escaped combat.");
                Debug.Log($"{CurrentPlayer.playerName} escaped combat due to Cautious.");
                isForcedTitleRunaway = false;
                pendingSceneContinueMessage = $"{CurrentPlayer.playerName} escaped combat. {runawayRollText}. Click Continue.";
                EndCombat(false, false);
                return;
            }

            CurrentPlayer.currentHP = 1;
            if (GameManager.Instance?.playerHUD != null)
                GameManager.Instance.playerHUD.Refresh();

            CombatHUDUI.Instance?.Refresh(CurrentPlayer, CurrentMonster);
            CombatHUDUI.Instance?.SetStatus($"{CurrentPlayer.playerName} escaped combat with 1 HP.");
            Debug.Log($"{CurrentPlayer.playerName} escaped combat with 1 HP.");
            pendingSceneContinueMessage = $"{CurrentPlayer.playerName} escaped with 1 HP. {runawayRollText}. Click Continue.";
            EndCombat(false, false);
            return;
        }

        if (isForcedTitleRunaway && CurrentPlayer.currentHP > 0)
        {
            isForcedTitleRunaway = false;
            showRunawayRollDisplay = false;
            lastRunawayRollValue = 0;
            displayedCombatRollResults = null;
            CombatHUDUI.Instance?.Refresh(CurrentPlayer, CurrentMonster);
            CombatHUDUI.Instance?.SetStatus($"{CurrentPlayer.playerName} failed to run away and must continue fighting.");
            Debug.Log($"{CurrentPlayer.playerName} failed their forced Cautious runaway attempt.");
            return;
        }

        Debug.Log($"{CurrentPlayer.playerName} failed to run away and died.");
        pendingSceneContinueMessage = $"{CurrentPlayer.playerName} failed to run away. {runawayRollText}. Click Continue.";
        HandlePlayerDefeated();
    }

    private void HandleMonsterDefeated()
    {
        if (CurrentPlayer == null || CurrentMonster == null)
            return;

        Debug.Log($"{CurrentPlayer.playerName} defeated {CurrentMonster.monsterName}.");
        ResolveMonsterDefeatTreasureRewards(() =>
        {
            if (CurrentMonster == null || CurrentPlayer == null)
                return;

            if (CurrentMonster.isMythicMonster)
            {
                Debug.Log($"{CurrentPlayer.playerName} defeated the Mythic Monster and wins the game!");
                pendingVictoryPlayer = CurrentPlayer;
                pendingVictoryMonster = CurrentMonster;
                EndCombat(true, true);
                return;
            }

            MonsterRoster.Instance?.MarkDefeated(CurrentMonster);

            if (CurrentMonster.rewardTitle != null)
            {
                CurrentPlayer.AddTitle(CurrentMonster.rewardTitle);
                Debug.Log($"{CurrentPlayer.playerName} gained the title {CurrentMonster.rewardTitle.titleName}.");
            }

            EndCombat(true, false);
        });
    }

    private void HandlePlayerDefeated()
    {
        if (isPeakCombat &&
            CurrentMonster != null &&
            CurrentMonster.isMythicMonster &&
            CurrentPlayer != null &&
            CurrentPlayer.HasTreasure("Mythic Armor"))
        {
            CurrentPlayer.currentHP = CurrentPlayer.GetEffectiveMaxHP();
            CurrentPlayer.ClearNextCombatBuffs();
            CurrentPlayer.PlaceAtIndex(CurrentPlayer.startingIndex);
            pendingRestartTurnAfterMythicArmor = true;
            pendingSceneContinueMessage = $"{CurrentPlayer.playerName}'s Mythic Armor returned them home. Click Continue to restart the turn.";
            Debug.Log($"{CurrentPlayer.playerName}'s Mythic Armor prevented defeat by the Mythic Monster and will restart their turn.");
            EndCombat(false, false);
            return;
        }

        pendingDefeatedPlayer = CurrentPlayer;
        EndCombat(false, false);
    }

    private void EndCombat(bool playerWon, bool endedGame)
    {
        pendingCombatWon = playerWon;
        pendingCombatEndedGame = endedGame;

        if (CombatSceneDirector.Instance != null && CombatSceneDirector.Instance.IsCombatSceneLoaded)
        {
            IsAwaitingSceneContinue = true;
            GameManager.Instance?.LockActionButtons();
            ConfirmRollUI.Instance?.Hide();

            if (!string.IsNullOrWhiteSpace(pendingSceneContinueMessage))
                CombatHUDUI.Instance?.SetStatus(pendingSceneContinueMessage);
            else if (endedGame)
                CombatHUDUI.Instance?.SetStatus("Mythic Monster defeated. Click Continue.");
            else if (pendingDefeatedPlayer != null)
                CombatHUDUI.Instance?.SetStatus($"{pendingDefeatedPlayer.playerName} was defeated. Click Continue.");
            else if (playerWon && CurrentMonster != null)
                CombatHUDUI.Instance?.SetStatus($"{CurrentMonster.monsterName} was defeated. Click Continue.");
            else
                CombatHUDUI.Instance?.SetStatus("Combat ended. Click Continue.");

            return;
        }

        StartCoroutine(EndCombatRoutine(playerWon, endedGame));
    }

    private IEnumerator EndCombatRoutine(bool playerWon, bool endedGame)
    {
        PlayerPawn resolvedPlayer = CurrentPlayer;
        Monster resolvedMonster = CurrentMonster;
        bool wasPeakCombat = isPeakCombat;
        int returnIndex = peakReturnIndex;

        bool transitionComplete = false;
        CombatSceneDirector.EnsureExists().EndCombatScene(() => transitionComplete = true);
        while (!transitionComplete)
            yield return null;

        if (CurrentPlayer != null)
        {
            CurrentPlayer.isResolvingSpace = false;
        }

        CurrentPlayer = null;
        CurrentMonster = null;
        pendingCombatRollResults = null;
        displayedCombatRollResults = null;
        pendingCombatRollTotal = 0;
        combatRollCount = 0;
        failedCombatRollCount = 0;
        monsterMightModifier = 0;
        combatReprievesUsed = 0;
        IsAwaitingRunawayRoll = false;
        IsResolvingCombatChoice = false;
        IsAwaitingSceneContinue = false;
        isPeakCombat = false;
        terraTarantulaThresholdTriggered = false;
        cautiousRunawayTriggeredThisCombat = false;
        isForcedTitleRunaway = false;
        showRunawayRollDisplay = false;
        lastRunawayRollValue = 0;
        peakReturnIndex = -1;
        CombatHUDUI.Instance?.Hide();

        if (!endedGame)
        {
            if (wasPeakCombat && resolvedPlayer != null && !resolvedPlayer.isDead && returnIndex >= 0)
                resolvedPlayer.PlaceAtIndex(returnIndex);

            GameManager.Instance.SetInputMode(GameManager.InputMode.Normal);
            GameManager.Instance.SetState(GameState.PlayerTurn);
            yield return ResolveDeferredObservedTreasureRewards();

            if (pendingRestartTurnAfterMythicArmor && resolvedPlayer != null)
            {
                pendingRestartTurnAfterMythicArmor = false;
                GameManager.Instance.RestartTurnForPlayer(resolvedPlayer);
                pendingVictoryPlayer = null;
                pendingVictoryMonster = null;
                pendingCombatWon = false;
                pendingCombatEndedGame = false;
                pendingSceneContinueMessage = null;
                yield break;
            }

            if (!playerWon)
                Debug.Log("The monster remains revealed and may be fought again.");

            GameManager.Instance.EndSpaceResolution();
            GameManager.Instance.RestoreActivePlayerVisual();
        }
        else
        {
            GameManager.Instance?.BeginGameVictory(
                pendingVictoryPlayer != null ? pendingVictoryPlayer : resolvedPlayer,
                pendingVictoryMonster != null ? pendingVictoryMonster : resolvedMonster);
        }

        if (pendingDefeatedPlayer != null)
        {
            PlayerPawn defeatedPlayer = pendingDefeatedPlayer;
            pendingDefeatedPlayer = null;
            GameManager.Instance?.BeginPlayerDeath(defeatedPlayer);
        }

        pendingVictoryPlayer = null;
        pendingVictoryMonster = null;
        pendingCombatWon = false;
        pendingCombatEndedGame = false;
        pendingRestartTurnAfterMythicArmor = false;
        pendingSceneContinueMessage = null;
    }

    private IEnumerator ResolveDeferredObservedTreasureRewards()
    {
        if (GameManager.Instance == null || GameManager.Instance.players == null)
            yield break;

        bool waitingForReward = false;
        foreach (PlayerPawn player in GameManager.Instance.players)
        {
            if (player == null || player.pendingGlisteningRingRewards <= 0)
                continue;

            waitingForReward = true;
            bool rewardResolved = false;
            player.ResolvePendingObservedMonsterSlainTreasureRewards(() => rewardResolved = true);

            while (!rewardResolved)
                yield return null;
        }

        if (waitingForReward)
            GameManager.Instance.playerHUD?.Refresh();
    }

    private void AbortCombatSelection()
    {
        MonsterSelectionUI.Instance?.Hide();

        if (CurrentPlayer != null)
            CurrentPlayer.isResolvingSpace = false;

        CurrentPlayer = null;
        CurrentMonster = null;
        pendingCombatRollResults = null;
        displayedCombatRollResults = null;
        IsAwaitingRunawayRoll = false;
        IsResolvingCombatChoice = false;
        IsAwaitingSceneContinue = false;
        isPeakCombat = false;
        terraTarantulaThresholdTriggered = false;
        showRunawayRollDisplay = false;
        lastRunawayRollValue = 0;
        peakReturnIndex = -1;
        CombatHUDUI.Instance?.Hide();
        pendingDefeatedPlayer = null;
        pendingCombatWon = false;
        pendingCombatEndedGame = false;
        pendingRestartTurnAfterMythicArmor = false;
        pendingSceneContinueMessage = null;
        GameManager.Instance.SetState(GameState.PlayerTurn);
        GameManager.Instance.EndSpaceResolution();
    }

    private string DescribePendingRoll()
    {
        if (pendingCombatRollResults == null)
            return string.Empty;

        if (pendingCombatRollResults.Length == 0)
            return "0 dice";

        return $"{string.Join(", ", pendingCombatRollResults)} (sum {pendingCombatRollTotal})";
    }

    private int GetCombatDiceCount(int arcane)
    {
        if (arcane <= -3)
            return 0;
        if (arcane <= 2)
            return 1;
        if (arcane <= 5)
            return 2;
        if (arcane <= 8)
            return 3;
        if (arcane <= 11)
            return 4;
        return 5;
    }

    public void OnCombatReprieveUsed(PlayerPawn player)
    {
        if (!IsCombatActive || player == null || player != CurrentPlayer)
            return;

        combatReprievesUsed++;
    }

    public int GetCombatReprieveLimit(PlayerPawn player, int defaultLimit)
    {
        if (!IsCombatActive || player == null || player != CurrentPlayer)
            return defaultLimit;

        if (IsFrigidFossaActive())
            return Mathf.Min(defaultLimit, 1);

        return defaultLimit;
    }

    private void ApplyMythicCombatOpeningRules()
    {
        if (CurrentMonster == null || !CurrentMonster.isMythicMonster)
            return;

        if (IsFrigidFossaActive())
        {
            CombatHUDUI.Instance?.SetStatus("Frigid Fossa blocks runaway and limits you to 1 reprieve in this combat.");
            Debug.Log("Frigid Fossa blocks runaway and limits reprieve use to 1 in this combat.");
        }
    }

    private void ResolveSpecialMythicFailedRollEffects()
    {
        if (CurrentMonster == null)
            return;

        switch (CurrentMonster.monsterName)
        {
            case "Slithering Stormcaller":
                ForceCombatCardDiscard();
                break;

            case "Dynasty Dragon":
                if (failedCombatRollCount % 2 == 0)
                    ForceCombatTreasureLoss();
                break;

            case "Gale Grizzly":
                if (failedCombatRollCount % 2 == 0)
                {
                    monsterMightModifier += Mathf.Max(0, CurrentMonster.mythicEffectValue);
                    CombatHUDUI.Instance?.SetStatus($"{CurrentMonster.monsterName} gains +{CurrentMonster.mythicEffectValue} Might.");
                    Debug.Log($"{CurrentMonster.monsterName} gains +{CurrentMonster.mythicEffectValue} Might.");
                }
                break;

            default:
                CombatHUDUI.Instance?.SetStatus($"{CurrentMonster.monsterName}: {CurrentMonster.mythicEffectDescription}");
                Debug.Log($"{CurrentMonster.monsterName} special mythic effect pending: {CurrentMonster.mythicEffectDescription}");
                break;
        }
    }

    private void ResolveMythicMonsterDamageEffects()
    {
        if (CurrentMonster == null || !CurrentMonster.isMythicMonster)
            return;

        if (CurrentMonster.monsterName == "Terra Tarantula" &&
            !terraTarantulaThresholdTriggered &&
            CurrentMonster.health < 15)
        {
            terraTarantulaThresholdTriggered = true;
            int roll = Random.Range(1, 7);
            bool healed = roll % 2 == 0;
            if (healed)
            {
                CurrentMonster.health = Mathf.Min(CurrentMonster.health + Mathf.Max(0, CurrentMonster.mythicEffectValue), CurrentMonster.maxHP);
                CombatHUDUI.Instance?.SetStatus($"Terra Tarantula rolled {roll} and healed {CurrentMonster.mythicEffectValue} HP.");
                Debug.Log($"Terra Tarantula rolled {roll} and healed {CurrentMonster.mythicEffectValue} HP.");
            }
            else
            {
                CombatHUDUI.Instance?.SetStatus($"Terra Tarantula rolled {roll} and failed to heal.");
                Debug.Log($"Terra Tarantula rolled {roll} and failed to heal.");
            }
        }
    }

    private bool ResolveMythicEndOfRollEffects()
    {
        if (CurrentMonster == null || !CurrentMonster.isMythicMonster)
            return false;

        if (CurrentMonster.monsterName == "Time Titan" &&
            combatRollCount >= Mathf.Max(1, CurrentMonster.mythicEffectValue) &&
            !CurrentMonster.IsDead)
        {
            CombatHUDUI.Instance?.SetStatus("Time Titan survived 8 combat rolls. The combat is lost.");
            Debug.Log("Time Titan survived the time limit. The player dies.");
            HandlePlayerDefeated();
            return true;
        }

        return false;
    }

    private bool IsFrigidFossaActive()
    {
        return CurrentMonster != null &&
               CurrentMonster.isMythicMonster &&
               CurrentMonster.monsterName == "Frigid Fossa";
    }

    private bool IsRunawayBlockedByCurrentMythic()
    {
        return IsFrigidFossaActive();
    }

    private bool TryForceCautiousRunaway()
    {
        if (CurrentPlayer == null ||
            cautiousRunawayTriggeredThisCombat ||
            TitleSystem.Instance == null ||
            !TitleSystem.Instance.ShouldForceCautiousRunaway(CurrentPlayer))
        {
            return false;
        }

        cautiousRunawayTriggeredThisCombat = true;
        isForcedTitleRunaway = true;
        IsAwaitingRunawayRoll = true;
        ConfirmRollUI.Instance?.Hide();
        CombatHUDUI.Instance?.SetPendingRoll(null, 0);
        CombatHUDUI.Instance?.SetStatus($"{CurrentPlayer.playerName} is Cautious and must attempt to run away.");
        Debug.Log($"{CurrentPlayer.playerName} is Cautious and must attempt to run away at 3 HP or lower.");
        return true;
    }

    private void ResolveMonsterDefeatTreasureRewards(System.Action onComplete)
    {
        System.Collections.Generic.List<PlayerPawn> glisteningOwners = new System.Collections.Generic.List<PlayerPawn>();

        if (GameManager.Instance != null && GameManager.Instance.players != null)
        {
            foreach (PlayerPawn player in GameManager.Instance.players)
            {
                if (player == null || player.isDead)
                    continue;

                if (player.HasTreasure("Coveter's Cape"))
                {
                    player.AddGold(200);
                    Debug.Log($"{player.playerName} gained 200 gold from Coveter's Cape because a monster was slain.");
                }

                if (player.HasTreasure("Glistening Ring"))
                    glisteningOwners.Add(player);
            }
        }

        void ResolveObservedRingReward(int index)
        {
            if (index >= glisteningOwners.Count)
            {
                CurrentPlayer.ResolveMonsterSlainTreasureRewards(onComplete);
                return;
            }

            PlayerPawn owner = glisteningOwners[index];
            owner.ResolveObservedMonsterSlainTreasureRewards(() => ResolveObservedRingReward(index + 1));
        }

        ResolveObservedRingReward(0);
    }

    private void ForceCombatCardDiscard()
    {
        if (CurrentPlayer == null)
            return;

        if (CurrentPlayer.hand == null || CurrentPlayer.hand.Count == 0)
        {
            CombatHUDUI.Instance?.SetStatus($"{CurrentMonster.monsterName} forces a discard, but no reprieve cards remain.");
            return;
        }

        IsResolvingCombatChoice = true;
        GameManager.Instance?.LockActionButtons();
        GameManager.Instance?.SetInputMode(GameManager.InputMode.SelectingCard);

        HandUIManager.Instance?.BeginCardSelection(
            CurrentPlayer,
            $"{CurrentPlayer.playerName}: choose 1 reprieve to discard",
            selectedCard =>
            {
                if (selectedCard != null && CurrentPlayer.hand.Contains(selectedCard))
                {
                    CurrentPlayer.hand.Remove(selectedCard);
                    ReprieveDeck.Instance.Discard(selectedCard);
                    Debug.Log($"{CurrentPlayer.playerName} discarded {selectedCard.cardName} due to {CurrentMonster.monsterName}.");
                }

                GameManager.Instance?.SetInputMode(GameManager.InputMode.Normal);
                IsResolvingCombatChoice = false;
                CombatHUDUI.Instance?.Refresh(CurrentPlayer, CurrentMonster);
            });
    }

    private void ForceCombatTreasureLoss()
    {
        if (CurrentPlayer == null)
            return;

        if (CurrentPlayer.equippedTreasures == null || CurrentPlayer.equippedTreasures.Count == 0)
        {
            CombatHUDUI.Instance?.SetStatus($"{CurrentMonster.monsterName} forces treasure loss, but none are equipped.");
            return;
        }

        IsResolvingCombatChoice = true;
        GameManager.Instance?.LockActionButtons();
        GameManager.Instance?.SetInputMode(GameManager.InputMode.SelectingCard);
        TreasureSelectionUI.EnsureExists();
        TreasureSelectionUI.Instance.BeginSelection(
            CurrentPlayer,
            $"{CurrentPlayer.playerName}: choose 1 treasure to lose",
            selectedTreasure =>
            {
                if (selectedTreasure != null && CurrentPlayer.equippedTreasures.Contains(selectedTreasure))
                {
                    CurrentPlayer.equippedTreasures.Remove(selectedTreasure);
                    TreasureDeck.Instance?.DiscardTreasure(selectedTreasure);
                    Debug.Log($"{CurrentPlayer.playerName} lost {selectedTreasure.cardName} due to {CurrentMonster.monsterName}.");
                }

                GameManager.Instance?.SetInputMode(GameManager.InputMode.Normal);
                IsResolvingCombatChoice = false;
                CombatHUDUI.Instance?.Refresh(CurrentPlayer, CurrentMonster);
            });
    }
}
