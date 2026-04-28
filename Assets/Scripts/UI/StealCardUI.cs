using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class StealCardUI : MonoBehaviour
{
    public static StealCardUI Instance;

    public TMP_Text instructionText;
    public Button cancelButton;

    private Action onCancel;
    private Action customCancelAction;
    public bool IsActive { get; private set; }

    private void Awake()
    {
        // This legacy UI has been fully replaced by PlayerSelectionUI + HandUIManager.
        // Retire any scene-baked copies immediately so they never render or intercept input.
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    public void Show(PlayerPawn user)
    {
        Show("Select a player to steal a card from");
    }

    public void Show(string instruction, Action cancelAction = null)
    {
        Show(instruction, cancelAction, true);
    }

    public void Show(string instruction, Action cancelAction, bool showInstruction)
    {
        IsActive = true;
        gameObject.SetActive(true);

        if (instructionText != null)
        {
            instructionText.text = instruction;
            instructionText.gameObject.SetActive(showInstruction);
        }

        customCancelAction = cancelAction;
        cancelButton.interactable = true;

        GameManager.Instance.SetInputMode(GameManager.InputMode.SelectingPlayer);
        GameManager.Instance.LockEndTurn();
    }
    public void Hide()
    {
        IsActive = false;

        if (instructionText != null)
            instructionText.gameObject.SetActive(true);

        cancelButton.interactable = false;
        gameObject.SetActive(false);
        customCancelAction = null;

        GameManager.Instance.SetInputMode(GameManager.InputMode.Normal);
        GameManager.Instance.RefreshActionAvailability();
        ShopManager.Instance?.ResumeTradeUI();

        HandUIManager.Instance.EndStealSession();
    }

    public void OnCancelClicked()
    {
        if (customCancelAction != null)
        {
            Action callback = customCancelAction;
            Hide();
            callback?.Invoke();
            return;
        }

        PlayerTargetingManager.Instance.CancelSelection();
        GameManager.Instance.CancelPendingReprieve();
        Hide();
    }

}
