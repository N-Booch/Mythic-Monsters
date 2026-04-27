using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class PilferManager : MonoBehaviour
{
    public static PilferManager Instance;

    public PlayerPawn CurrentAttacker { get; private set; }
    public PlayerPawn CurrentDefender { get; private set; }
    public PlayerPawn CurrentRollPlayer { get; private set; }
    public bool IsPilferActive => CurrentAttacker != null && CurrentDefender != null;
    public bool HasPendingPilferRoll => pendingPilferRollResults != null;
    public bool IsRollingPilferDice { get; private set; }

    private int[] pendingPilferRollResults;
    private int pendingPilferRollTotal;
    private int attackerRollTotal = int.MinValue;
    private int defenderRollTotal = int.MinValue;
    private bool resolvingPilferChoice;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    public static void EnsureExists()
    {
        if (Instance != null)
            return;

        PilferManager existing = FindFirstObjectByType<PilferManager>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        GameObject managerObject = new GameObject("PilferManager");
        managerObject.AddComponent<PilferManager>();
    }

    public bool HasEligiblePilferTargets(PlayerPawn player)
    {
        return GetEligiblePilferTargets(player).Count > 0;
    }

    public List<PlayerPawn> GetEligiblePilferTargets(PlayerPawn player)
    {
        List<PlayerPawn> targets = new List<PlayerPawn>();
        if (player == null || player.isDead || player.inCavePhase || BoardManager.Instance == null || GameManager.Instance == null)
            return targets;

        foreach (PlayerPawn candidate in GameManager.Instance.players)
        {
            if (!IsValidPilferTarget(player, candidate))
                continue;

            targets.Add(candidate);
        }

        return targets;
    }

    public bool IsValidPilferTarget(PlayerPawn attacker, PlayerPawn candidate)
    {
        if (attacker == null || candidate == null)
            return false;

        if (attacker == candidate || attacker.isDead || candidate.isDead)
            return false;

        if (!candidate.CanBePilferTargeted())
            return false;

        if (attacker.inCavePhase || candidate.inCavePhase)
            return false;

        int distance = BoardManager.Instance != null
            ? BoardManager.Instance.GetCircularDistance(attacker.currentIndex, candidate.currentIndex)
            : int.MaxValue;

        return distance <= 2;
    }

    public void BeginPilfer(PlayerPawn attacker, PlayerPawn defender)
    {
        if (!IsValidPilferTarget(attacker, defender))
            return;

        CurrentAttacker = attacker;
        CurrentDefender = defender;
        CurrentRollPlayer = attacker;
        pendingPilferRollResults = null;
        pendingPilferRollTotal = 0;
        attackerRollTotal = int.MinValue;
        defenderRollTotal = int.MinValue;
        resolvingPilferChoice = false;
        IsRollingPilferDice = false;

        GameManager.Instance?.SetState(GameState.Pilfer);
        GameManager.Instance?.LockActionButtons();
        HandUIManager.Instance?.HideHand();

        HighlightPilferPlayers();
        RefreshPilferUI("Pilfer active. The attacker may play dice cards or click Roll.");

        Debug.Log($"{attacker.playerName} may attempt to pilfer {defender.playerName}.");
    }

    public bool CanRollPilfer()
    {
        return IsPilferActive &&
               GameManager.Instance != null &&
               GameManager.Instance.CurrentState == GameState.Pilfer &&
               !IsRollingPilferDice &&
               !HasPendingPilferRoll &&
               !resolvingPilferChoice &&
               !GameManager.Instance.IsPendingReprieveActive &&
               CurrentRollPlayer != null;
    }

    public void StartPilferRoll()
    {
        if (!CanRollPilfer())
            return;

        int diceCount = GetPilferDiceCount(CurrentRollPlayer.GetCombatArcane(false));
        if (diceCount <= 0)
        {
            pendingPilferRollResults = new int[0];
            pendingPilferRollTotal = 0;
            CombatHUDUI.Instance?.SetPendingRoll(pendingPilferRollResults, pendingPilferRollTotal);
            ResolvePilferRoll();
            return;
        }

        CombatHUDUI.Instance?.SetStatus($"Rolling {diceCount} pilfer die{(diceCount == 1 ? "" : "s")} for {CurrentRollPlayer.playerName}...");
        StartCoroutine(RollPilferDiceCoroutine(diceCount));
    }

    public bool CanRerollPendingPilferRoll()
    {
        return IsPilferActive && HasPendingPilferRoll && !IsRollingPilferDice;
    }

    public void RerollPendingPilferRoll()
    {
        if (!CanRerollPendingPilferRoll())
            return;

        StartCoroutine(RollPilferDiceCoroutine(pendingPilferRollResults.Length));
    }

    public bool CanForcePendingPilferRoll()
    {
        return IsPilferActive && HasPendingPilferRoll && !IsRollingPilferDice;
    }

    public void ForcePendingPilferRoll(int value)
    {
        if (!CanForcePendingPilferRoll())
            return;

        int clampedValue = Mathf.Clamp(value, 1, 6);
        for (int i = 0; i < pendingPilferRollResults.Length; i++)
            pendingPilferRollResults[i] = clampedValue;

        pendingPilferRollTotal = clampedValue * pendingPilferRollResults.Length;
        DiceManager.Instance?.diceUI?.ShowRoll(clampedValue);
        CombatHUDUI.Instance?.SetPendingRoll(pendingPilferRollResults, pendingPilferRollTotal);
        CombatHUDUI.Instance?.SetStatus($"{CurrentRollPlayer.playerName}'s pilfer dice were set to {clampedValue}.");
        Debug.Log($"Pilfer roll forced to {DescribePendingRoll()} for {CurrentRollPlayer.playerName}.");
    }

    public void ConfirmPendingPilferRoll()
    {
        if (!HasPendingPilferRoll || IsRollingPilferDice)
            return;

        ResolvePilferRoll();
    }

    public bool CanPlayerPlayPilferReprieve(PlayerPawn player)
    {
        if (!IsPilferActive || player == null || player != CurrentRollPlayer)
            return false;

        if (player.isDead || IsRollingPilferDice || resolvingPilferChoice)
            return false;

        if (HandUIManager.Instance != null && HandUIManager.Instance.IsForcedDiscardActive)
            return false;

        return true;
    }

    public bool ShouldCountReprieveUse(PlayerPawn player)
    {
        return GameManager.Instance != null && player == GameManager.Instance.GetActivePlayer();
    }

    private IEnumerator RollPilferDiceCoroutine(int diceCount)
    {
        IsRollingPilferDice = true;
        pendingPilferRollResults = null;
        pendingPilferRollTotal = 0;
        GameManager.Instance?.LockActionButtons();

        int[] results = new int[diceCount];
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
            DiceManager.Instance?.diceUI?.ShowRoll(results[i]);
            yield return new WaitForSeconds(0.08f);
        }

        pendingPilferRollResults = results;
        foreach (int value in results)
            pendingPilferRollTotal += value;

        IsRollingPilferDice = false;
        CombatHUDUI.Instance?.SetPendingRoll(pendingPilferRollResults, pendingPilferRollTotal);

        bool canModifyRoll = GameManager.Instance != null && GameManager.Instance.PlayerHasPlayableDiceCards(CurrentRollPlayer);
        if (canModifyRoll)
        {
            ConfirmRollUI.Instance?.Show();
            CombatHUDUI.Instance?.SetStatus($"{CurrentRollPlayer.playerName}'s pilfer roll is ready. Confirm or use dice cards.");
        }
        else
        {
            ConfirmRollUI.Instance?.Hide();
            ResolvePilferRoll();
            yield break;
        }

        Debug.Log($"Pilfer roll ready for {CurrentRollPlayer.playerName}: {DescribePendingRoll()}");
    }

    private void ResolvePilferRoll()
    {
        if (CurrentRollPlayer == null)
            return;

        int total = CurrentRollPlayer.GetCombatMight(false) + pendingPilferRollTotal;
        string summary =
            $"{CurrentRollPlayer.playerName} pilfer total {total} " +
            $"(Might {CurrentRollPlayer.GetCombatMight(false)}" +
            $"{(pendingPilferRollResults != null && pendingPilferRollResults.Length > 0 ? $" + dice {DescribePendingRoll()}" : string.Empty)})";
        Debug.Log(summary);

        if (pendingPilferRollResults != null && pendingPilferRollResults.Length > 0)
            GameManager.Instance?.ResolveTreasureRolledDieEffects(CurrentRollPlayer, pendingPilferRollResults);

        ConfirmRollUI.Instance?.Hide();
        pendingPilferRollResults = null;
        pendingPilferRollTotal = 0;
        CombatHUDUI.Instance?.SetPendingRoll(null, 0);

        if (CurrentRollPlayer == CurrentAttacker)
        {
            attackerRollTotal = total;
            CurrentRollPlayer = CurrentDefender;
            RefreshPilferUI($"{CurrentDefender.playerName} may now play dice cards or click Roll.");
            return;
        }

        defenderRollTotal = total;
        StartCoroutine(ResolvePilferOutcome());
    }

    private IEnumerator ResolvePilferOutcome()
    {
        resolvingPilferChoice = true;

        if (attackerRollTotal == defenderRollTotal)
        {
            Debug.Log("Pilfer tied. Both players must roll again.");
            attackerRollTotal = int.MinValue;
            defenderRollTotal = int.MinValue;
            CurrentRollPlayer = CurrentAttacker;
            resolvingPilferChoice = false;
            RefreshPilferUI("Pilfer tied. Roll again.");
            yield break;
        }

        PlayerPawn winner = attackerRollTotal > defenderRollTotal ? CurrentAttacker : CurrentDefender;
        PlayerPawn loser = winner == CurrentAttacker ? CurrentDefender : CurrentAttacker;
        int damage = Mathf.Abs(attackerRollTotal - defenderRollTotal);

        loser.TakeDamage(damage);
        RefreshPilferUI($"{winner.playerName} wins the pilfer. {loser.playerName} takes {damage} damage.");
        Debug.Log($"{winner.playerName} won the pilfer against {loser.playerName}. {loser.playerName} takes {damage} damage.");

        if (loser.hand != null && loser.hand.Count > 0)
        {
            bool reprieveChosen = false;
            HandUIManager.Instance?.BeginCardSelection(
                loser,
                $"{winner.playerName}: choose 1 reprieve to pilfer from {loser.playerName}",
                selectedCard =>
                {
                    if (selectedCard != null && loser.hand.Contains(selectedCard))
                    {
                        loser.hand.Remove(selectedCard);
                        winner.hand.Add(selectedCard);
                        Debug.Log($"{winner.playerName} pilfered {selectedCard.cardName} from {loser.playerName}.");
                    }

                    reprieveChosen = true;
                });

            while (!reprieveChosen)
                yield return null;
        }

        if (loser.currentHP <= 0 && loser.equippedTreasures != null && loser.equippedTreasures.Count > 0)
        {
            bool treasureResolved = false;
            TreasureSelectionUI.EnsureExists();
            TreasureSelectionUI.Instance.BeginSelection(
                loser,
                $"{winner.playerName}: choose 1 treasure to steal from {loser.playerName}",
                selectedTreasure =>
                {
                    if (selectedTreasure != null)
                        BeginTreasurePilferTransfer(winner, loser, selectedTreasure, () => treasureResolved = true);
                    else
                        treasureResolved = true;
                });

            while (!treasureResolved)
                yield return null;
        }

        PlayerPawn defeatedPlayer = loser.currentHP <= 0 ? loser : null;
        EndPilfer();

        if (defeatedPlayer != null)
            GameManager.Instance?.BeginPlayerDeath(defeatedPlayer);
    }

    private void BeginTreasurePilferTransfer(PlayerPawn winner, PlayerPawn loser, TreasureCard stolenTreasure, System.Action onComplete)
    {
        if (winner == null || loser == null || stolenTreasure == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (winner.equippedTreasures.Count < winner.GetEffectiveMaxEquipLoad())
        {
            loser.equippedTreasures.Remove(stolenTreasure);
            winner.equippedTreasures.Add(stolenTreasure);
            Debug.Log($"{winner.playerName} stole {stolenTreasure.cardName} from {loser.playerName}.");
            onComplete?.Invoke();
            return;
        }

        TreasureSelectionUI.EnsureExists();
        TreasureSelectionUI.Instance.BeginSelection(
            winner.equippedTreasures,
            $"{winner.playerName}: choose 1 treasure to replace, or keep your current treasures.",
            selectedTreasure =>
            {
                if (selectedTreasure != null)
                {
                    winner.equippedTreasures.Remove(selectedTreasure);
                    TreasureDeck.Instance?.DiscardTreasure(selectedTreasure);
                    loser.equippedTreasures.Remove(stolenTreasure);
                    winner.equippedTreasures.Add(stolenTreasure);
                    Debug.Log($"{winner.playerName} replaced {selectedTreasure.cardName} with stolen treasure {stolenTreasure.cardName}.");
                }
                else
                {
                    Debug.Log($"{winner.playerName} declined to steal {stolenTreasure.cardName} from {loser.playerName}.");
                }

                onComplete?.Invoke();
            },
            stolenTreasure,
            true,
            $"Keep current treasures and leave {stolenTreasure.cardName} with {loser.playerName}");
    }

    private void EndPilfer()
    {
        PlayerPawn activeTurnPlayer = GameManager.Instance != null ? GameManager.Instance.GetActivePlayer() : null;

        CurrentAttacker = null;
        CurrentDefender = null;
        CurrentRollPlayer = null;
        pendingPilferRollResults = null;
        pendingPilferRollTotal = 0;
        attackerRollTotal = int.MinValue;
        defenderRollTotal = int.MinValue;
        resolvingPilferChoice = false;
        IsRollingPilferDice = false;

        ConfirmRollUI.Instance?.Hide();
        CombatHUDUI.Instance?.Hide();
        ClearPilferHighlights();

        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Pilfer)
        {
            GameManager.Instance.SetState(GameState.PlayerTurn);
            GameManager.Instance.RefreshActionAvailability();
        }

        if (GameManager.Instance != null &&
            GameManager.Instance.playerHUD != null &&
            activeTurnPlayer != null &&
            !activeTurnPlayer.isDead)
        {
            GameManager.Instance.playerHUD.SetPlayer(activeTurnPlayer);
        }

        GameManager.Instance?.RestoreActivePlayerVisual();
    }

    private void RefreshPilferUI(string status)
    {
        CombatHUDUI.Instance?.ShowPilfer(CurrentAttacker, CurrentDefender, CurrentRollPlayer);
        CombatHUDUI.Instance?.RefreshPilfer(CurrentAttacker, CurrentDefender, CurrentRollPlayer);
        CombatHUDUI.Instance?.SetStatus(status);

        if (GameManager.Instance?.playerHUD != null)
            GameManager.Instance.playerHUD.SetPlayer(CurrentRollPlayer ?? CurrentAttacker);

        HighlightPilferPlayers();
    }

    private void HighlightPilferPlayers()
    {
        if (GameManager.Instance == null || GameManager.Instance.players == null)
            return;

        foreach (PlayerPawn player in GameManager.Instance.players)
        {
            if (player == null)
                continue;

            bool active = player == CurrentRollPlayer;
            bool involved = player == CurrentAttacker || player == CurrentDefender;
            player.SetActiveVisual(active || involved);
        }
    }

    private void ClearPilferHighlights()
    {
        if (GameManager.Instance == null || GameManager.Instance.players == null)
            return;

        foreach (PlayerPawn player in GameManager.Instance.players)
        {
            if (player == null)
                continue;

            player.SetActiveVisual(false);
        }
    }

    private string DescribePendingRoll()
    {
        if (pendingPilferRollResults == null)
            return string.Empty;

        if (pendingPilferRollResults.Length == 0)
            return "0 dice";

        return $"{string.Join(", ", pendingPilferRollResults)} (sum {pendingPilferRollTotal})";
    }

    private int GetPilferDiceCount(int arcane)
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
}
