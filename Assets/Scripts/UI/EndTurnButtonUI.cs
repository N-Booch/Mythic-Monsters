using UnityEngine;
using UnityEngine.UI;

public class EndTurnButtonUI : MonoBehaviour
{
    public Button endTurnButton;
    private BoardActionButtonStyle style;

    private void Awake()
    {
        if (endTurnButton != null)
        {
            endTurnButton.onClick.AddListener(OnEndTurnClicked);
            style = BoardActionButtonStyle.Attach(endTurnButton, BoardActionButtonStyle.Variant.Primary);
        }
        else
            Debug.LogError("EndTurnButton not assigned in inspector!");
    }

    private void Update()
    {
        if (endTurnButton == null || GameManager.Instance == null)
            return;

        endTurnButton.interactable = GameManager.Instance.CanEndTurnButtonAct();
        if (style != null)
            style.ApplyNow();
    }

    private void OnEndTurnClicked()
    {
        if (GameManager.Instance != null)
        {
            // Only advance the turn through GameManager
            GameManager.Instance.AdvanceTurnFromButton();
            Debug.Log("End Turn button clicked");
        }
    }
}
