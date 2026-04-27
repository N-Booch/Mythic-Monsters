using UnityEngine;
using UnityEngine.UI;

public class RollButtonUI : MonoBehaviour
{
    public Button rollButton;

    private void Awake()
    {
        if (rollButton == null)
            rollButton = GetComponent<Button>();

        BoardActionButtonStyle.Attach(rollButton, BoardActionButtonStyle.Variant.Primary);
    }

    private void Update()
    {
        if (GameManager.Instance == null)
            return;

        rollButton.interactable = GameManager.Instance.CanRollButtonAct();
        if (DiceManager.Instance != null && DiceManager.Instance.diceUI != null)
            DiceManager.Instance.diceUI.SetRollAvailable(rollButton.interactable);
    }

    public void OnRollClicked()
    {
        GameManager.Instance.OnRollButtonPressed();
    }
}
