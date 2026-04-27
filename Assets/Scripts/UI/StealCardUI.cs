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
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        if (instructionText == null)
            Debug.LogError("StealCardUI: InstructionText not assigned");

        if (cancelButton == null)
            Debug.LogError("StealCardUI: CancelButton not assigned");

        cancelButton.onClick.RemoveAllListeners();
        cancelButton.onClick.AddListener(OnCancelClicked);

        cancelButton.interactable = false;
        gameObject.SetActive(false);
    }

    public void Show(PlayerPawn user)
    {
        Show("Select a player to steal a card from");
    }

    public void Show(string instruction, Action cancelAction = null)
    {
        IsActive = true;
        gameObject.SetActive(true);

        instructionText.text = instruction;
        customCancelAction = cancelAction;
        cancelButton.interactable = true;

        GameManager.Instance.SetInputMode(GameManager.InputMode.SelectingPlayer);
        GameManager.Instance.LockEndTurn();
    }
    public void Hide()
    {
        IsActive = false;

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
