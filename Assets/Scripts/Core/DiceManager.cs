using System;
using System.Collections;
using UnityEngine;

public class DiceManager : MonoBehaviour
{
    public static DiceManager Instance;

    [Header("Visuals")]
    public DiceUI diceUI;

    [SerializeField] private float rollDuration = 1.2f;
    [SerializeField] private float faceChangeInterval = 0.12f;

    public bool IsRolling { get; private set; }
    public DiceRollContext CurrentContext { get; private set; } = DiceRollContext.None;

    private int lastRoll;
    public int LastRoll => lastRoll;

    private PlayerPawn currentPlayer;
    private Action<int> currentCallback;
    private bool awaitingDecision;

    public bool HasPendingRoll => awaitingDecision;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void ResetTurnPresentation()
    {
        diceUI?.ResetTurnPresentation();
    }

    public void Roll(
        DiceRollContext context,
        PlayerPawn player,
        Action<int> onComplete
    )
    {
        if (IsRolling)
        {
            Debug.LogWarning("Dice is already rolling.");
            return;
        }

        CurrentContext = context;
        currentPlayer = player;
        currentCallback = onComplete;

        StartCoroutine(RollCoroutine());
    }

    private IEnumerator RollCoroutine()
    {
        IsRolling = true;
        diceUI?.BeginRollPresentation();

        float elapsed = 0f;
        float faceTimer = 0f;

        while (elapsed < rollDuration)
        {
            elapsed += Time.deltaTime;
            faceTimer += Time.deltaTime;

            if (faceTimer >= faceChangeInterval)
            {
                faceTimer = 0f;
                Show(UnityEngine.Random.Range(1, 7));
            }

            yield return null;
        }

        int result = UnityEngine.Random.Range(1, 7);
        Show(result);
        diceUI?.ShowFinalResult(result);

        IsRolling = false;
        lastRoll = result;
        awaitingDecision = true;
        Debug.Log($"Dice rolled -> {result}");

        if (GameManager.Instance != null && GameManager.Instance.PlayerHasPlayableDiceCards(currentPlayer))
        {
            GameManager.Instance.OnDiceDecisionRequired();
            ConfirmRollUI.Instance?.Show();
        }
        else
        {
            ResolveRoll();
        }
    }

    public void RerollPending()
    {
        if (!HasPendingRoll || IsRolling)
        {
            Debug.LogWarning("Cannot reroll: no pending roll or dice is rolling.");
            return;
        }

        StartCoroutine(RerollCoroutine());
    }

    private IEnumerator RerollCoroutine()
    {
        IsRolling = true;
        diceUI?.BeginRollPresentation();

        float elapsed = 0f;
        float faceTimer = 0f;
        float rerollDuration = 0.8f;

        while (elapsed < rerollDuration)
        {
            elapsed += Time.deltaTime;
            faceTimer += Time.deltaTime;

            if (faceTimer >= 0.1f)
            {
                faceTimer = 0f;
                Show(UnityEngine.Random.Range(1, 7));
            }

            yield return null;
        }

        lastRoll = UnityEngine.Random.Range(1, 7);
        Show(lastRoll);
        diceUI?.ShowFinalResult(lastRoll);
        Debug.Log($"Rerolled -> {lastRoll}");

        IsRolling = false;
        awaitingDecision = true;
        if (GameManager.Instance != null && GameManager.Instance.PlayerHasPlayableDiceCards(currentPlayer))
        {
            GameManager.Instance.OnDiceDecisionRequired();
            ConfirmRollUI.Instance?.Show();
        }
        else
        {
            ResolveRoll();
        }
    }

    public void ForceResult(int value)
    {
        if (IsRolling)
            return;

        int clamped = Mathf.Clamp(value, 1, 6);
        Show(clamped);
        diceUI?.ShowFinalResult(clamped);

        Debug.Log($"Dice forced -> {clamped}");

        lastRoll = clamped;

        if (!HasPendingRoll)
            ResolveRoll();
    }

    private void Show(int value)
    {
        if (diceUI != null)
            diceUI.ShowRoll(value);
    }

    private void ClearState()
    {
        CurrentContext = DiceRollContext.None;
        currentPlayer = null;
        currentCallback = null;
        awaitingDecision = false;

        ConfirmRollUI.Instance?.Hide();
    }

    public void ConfirmRoll()
    {
        if (PilferManager.Instance != null && PilferManager.Instance.HasPendingPilferRoll)
        {
            PilferManager.Instance.ConfirmPendingPilferRoll();
            return;
        }

        if (CombatManager.Instance != null && CombatManager.Instance.HasPendingCombatRoll)
        {
            CombatManager.Instance.ConfirmPendingCombatRoll();
            return;
        }

        if (!HasPendingRoll || IsRolling)
        {
            Debug.LogWarning("ConfirmRoll ignored - no pending roll.");
            return;
        }

        Debug.Log($"Roll confirmed -> {lastRoll}");
        ResolveRoll();
    }

    private void ResolveRoll()
    {
        Debug.Log($"Resolving roll {lastRoll} for {CurrentContext}");

        Action<int> callback = currentCallback;
        PlayerPawn resolvedPlayer = currentPlayer;
        awaitingDecision = false;
        ClearState();
        GameManager.Instance?.ResolveTreasureRolledDieEffects(resolvedPlayer, new[] { lastRoll });
        callback?.Invoke(lastRoll);
        diceUI?.CompleteRollPresentation();
        GameManager.Instance?.RefreshActionAvailability();
    }

    public bool CanRerollPending()
    {
        return HasPendingRoll && !IsRolling;
    }

    public bool CanForceResult()
    {
        return HasPendingRoll && !IsRolling;
    }
}
