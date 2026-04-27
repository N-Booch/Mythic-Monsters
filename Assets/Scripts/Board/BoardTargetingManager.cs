using System;
using System.Collections.Generic;
using UnityEngine;

public class BoardTargetingManager : MonoBehaviour
{
    // =========================
    // SINGLETON
    // =========================
    public static BoardTargetingManager Instance;

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

    private bool isSelecting = false;                     // Are we in space selection mode?
    private bool canCancel = false;                       // Is cancel allowed?
    private Action<BoardSpace> onSpaceSelected;           // Callback when a space is chosen
    private Func<BoardSpace, bool> spaceFilter;           // Filter for valid spaces

    // =========================
    // SELECTION ENTRY POINT
    // =========================

    /// <summary>
    /// Starts board space selection mode.
    /// </summary>
    /// <param name="callback">Called when a space is selected</param>
    /// <param name="filter">Optional filter to limit valid spaces</param>
    /// <param name="allowCancel">Can the player cancel this selection?</param>
    public void StartSelectSpace(
        Action<BoardSpace> callback,
        Func<BoardSpace, bool> filter = null,
        bool allowCancel = true
    )
    {
        if (isSelecting)
        {
            Debug.LogWarning("BoardTargetingManager: Selection already active.");
            return;
        }

        isSelecting = true;
        canCancel = allowCancel;
        onSpaceSelected = callback;
        spaceFilter = filter;

        GameManager.Instance.SetInputMode(GameManager.InputMode.SelectingBoard);

        HighlightSelectableSpaces();
    }

    // =========================
    // VISUAL FEEDBACK
    // =========================

    /// <summary>
    /// Highlights valid selectable spaces.
    /// </summary>
    private void HighlightSelectableSpaces()
    {
        foreach (BoardSpace space in BoardManager.Instance.boardPath)
        {
            bool selectable = (spaceFilter == null || spaceFilter(space));
            space.Highlight(selectable);
        }
    }

    // =========================
    // INPUT HANDLER
    // =========================

    /// <summary>
    /// Called by BoardSpace.OnMouseDown()
    /// </summary>
    public void SpaceClicked(BoardSpace space)
    {
        if (!isSelecting)
            return;

        if (spaceFilter != null && !spaceFilter(space))
            return; // Invalid target

        CompleteSelection(space);
    }

    // =========================
    // SELECTION FLOW
    // =========================

    /// <summary>
    /// Finalizes a valid space selection.
    /// </summary>
    private void CompleteSelection(BoardSpace space)
    {
        isSelecting = false;

        onSpaceSelected?.Invoke(space);
        onSpaceSelected = null;
        spaceFilter = null;

        ClearHighlights();

        GameManager.Instance.SetInputMode(GameManager.InputMode.Normal);
    }

    // =========================
    // CANCELLATION
    // =========================

    /// <summary>
    /// Cancel if allowed.
    /// </summary>
    public void CancelSelection()
    {
        if (!isSelecting)
            return;

        if (!canCancel)
        {
            Debug.Log("BoardTargetingManager: Cancel blocked.");
            return;
        }

        CancelInternal();
    }

    /// <summary>
    /// Forced cancel (system-level).
    /// </summary>
    public void Cancel()
    {
        if (!isSelecting)
            return;

        CancelInternal();
    }

    private void CancelInternal()
    {
        isSelecting = false;
        onSpaceSelected = null;
        spaceFilter = null;

        ClearHighlights();

        GameManager.Instance.SetInputMode(GameManager.InputMode.Normal);
    }

    // =========================
    // CLEANUP
    // =========================

    /// <summary>
    /// Clears all board highlights.
    /// </summary>
    private void ClearHighlights()
    {
        foreach (BoardSpace space in BoardManager.Instance.boardPath)
            space.Highlight(false);
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
}
