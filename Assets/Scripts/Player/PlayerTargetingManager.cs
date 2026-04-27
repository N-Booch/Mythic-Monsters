using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerTargetingManager : MonoBehaviour
{
    // =========================
    // SINGLETON
    // =========================
    public static PlayerTargetingManager Instance;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    // =========================
    // STATE
    // =========================

    private bool isSelecting = false;                  // Are we currently in targeting mode?
    private bool canCancel = false;                    // Can the player cancel this selection?
    private Action<PlayerPawn> selectedCallback;       // What happens when a player is selected
    private Func<PlayerPawn, bool> canSelectPlayer;
    private readonly Dictionary<PlayerPawn, bool> previousOutlineStates = new Dictionary<PlayerPawn, bool>();

    // =========================
    // INPUT HANDLER
    // =========================

    /// <summary>
    /// Called by PlayerPawn when clicked.
    /// </summary>
    public void PlayerClicked(PlayerPawn player)
    {
        if (!isSelecting)
            return;

        if (player == null || player.isDead)
            return;

        if (canSelectPlayer != null && !canSelectPlayer(player))
            return;

        CompleteSelection(player);
    }

    // =========================
    // SELECTION FLOW
    // =========================

    /// <summary>
    /// Starts player selection mode.
    /// </summary>
    /// <param name="onSelected">Callback when a player is chosen</param>
    /// <param name="allowCancel">Can the player cancel this action?</param>
    public void StartSelectPlayer(Action<PlayerPawn> onSelected, bool allowCancel = true)
    {
        StartSelectPlayer(onSelected, null, allowCancel);
    }

    public void StartSelectPlayer(Action<PlayerPawn> onSelected, Func<PlayerPawn, bool> canSelect, bool allowCancel = true)
    {
        if (isSelecting)
        {
            Debug.LogWarning("PlayerTargetingManager: Selection already active.");
            return;
        }

        isSelecting = true;
        canCancel = allowCancel;
        selectedCallback = onSelected;
        canSelectPlayer = canSelect;

        // Switch game input mode
        GameManager.Instance.SetInputMode(GameManager.InputMode.SelectingPlayer);
        HighlightSelectablePlayers();
    }

    /// <summary>
    /// Finalizes a valid selection.
    /// </summary>
    private void CompleteSelection(PlayerPawn selectedPlayer)
    {
        isSelecting = false;
        RestorePreviousHighlights();

        selectedCallback?.Invoke(selectedPlayer);
        selectedCallback = null;
        canSelectPlayer = null;

        GameManager.Instance.SetInputMode(GameManager.InputMode.Normal);
    }

    // =========================
    // CANCELLATION
    // =========================

    /// <summary>
    /// Cancels selection if allowed.
    /// </summary>
    public void CancelSelection()
    {
        if (!isSelecting)
            return;

        if (!canCancel)
        {
            Debug.Log("PlayerTargetingManager: Cancel blocked (not allowed).");
            return;
        }

        CancelInternal();
    }

    /// <summary>
    /// Forced cancel (system-level cancel, ignores canCancel).
    /// </summary>
    public void Cancel()
    {
        if (!isSelecting)
            return;

        CancelInternal();
    }

    /// <summary>
    /// Internal cancel logic.
    /// </summary>
    private void CancelInternal()
    {
        isSelecting = false;
        selectedCallback = null;
        canSelectPlayer = null;
        RestorePreviousHighlights();

        GameManager.Instance.SetInputMode(GameManager.InputMode.Normal);
    }

    // =========================
    // STATE QUERIES
    // =========================

    public bool IsSelecting()
    {
        return isSelecting;
    }

    public bool CanCancel()
    {
        return canCancel;
    }

    private void HighlightSelectablePlayers()
    {
        RestorePreviousHighlights();

        if (GameManager.Instance == null || GameManager.Instance.players == null)
            return;

        foreach (PlayerPawn player in GameManager.Instance.players)
        {
            if (player == null || player.isDead)
                continue;

            bool selectable = canSelectPlayer == null || canSelectPlayer(player);
            if (!selectable)
                continue;

            bool wasActive = player.outlineObject != null && player.outlineObject.activeSelf;
            previousOutlineStates[player] = wasActive;
            player.SetActiveVisual(true);
        }
    }

    private void RestorePreviousHighlights()
    {
        foreach (KeyValuePair<PlayerPawn, bool> pair in previousOutlineStates)
        {
            if (pair.Key == null)
                continue;

            pair.Key.SetActiveVisual(pair.Value);
        }

        previousOutlineStates.Clear();
    }
}
