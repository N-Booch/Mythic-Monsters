using UnityEngine;
using UnityEngine.UI;

public class ConfirmRollUI : MonoBehaviour
{
    public static ConfirmRollUI Instance;
    public Button button;

    private void Awake()
    {
        Instance = this;
        BoardActionButtonStyle.Attach(button, BoardActionButtonStyle.Variant.Confirm);
        Hide();
    }

    public void Show()
    {
        gameObject.SetActive(true);
        button.interactable = true;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void OnConfirmClicked()
    {
        GameManager.Instance?.OnConfirmRollButtonPressed();
    }

}
