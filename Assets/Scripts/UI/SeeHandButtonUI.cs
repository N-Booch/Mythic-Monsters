using UnityEngine;
using UnityEngine.UI;

public class SeeHandButtonUI : MonoBehaviour
{
    public static SeeHandButtonUI Instance { get; private set; }

    public Button seeHandButton;
    private BoardActionButtonStyle style;
    private int originalSiblingIndex = -1;
    private bool handOverlayMode;

    private void Awake()
    {
        Instance = this;

        if (seeHandButton == null)
            seeHandButton = GetComponent<Button>();

        style = BoardActionButtonStyle.Attach(seeHandButton, BoardActionButtonStyle.Variant.Primary);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (GameManager.Instance == null || HandUIManager.Instance == null)
            return;

        var state = GameManager.Instance.CurrentState;

        seeHandButton.interactable =
            state == GameState.PlayerTurn ||
            state == GameState.AwaitingDiceDecision ||
            state == GameState.Combat ||
            state == GameState.Pilfer ||
            state == GameState.CavePhase ||
            (state == GameState.ResolvingSpace &&
             GameManager.Instance.GetActivePlayer() != null &&
             GameManager.Instance.GetActivePlayer().currentSpace != null &&
             GameManager.Instance.GetActivePlayer().currentSpace.spaceType == SpaceType.Shop);

        if (handOverlayMode)
            transform.SetAsLastSibling();

    }

    public void SetHandOverlayMode(bool enabled)
    {
        if (enabled)
        {
            if (!handOverlayMode)
                originalSiblingIndex = transform.GetSiblingIndex();

            handOverlayMode = true;
            transform.SetAsLastSibling();
        }
        else
        {
            if (handOverlayMode && originalSiblingIndex >= 0 && transform.parent != null)
                transform.SetSiblingIndex(Mathf.Min(originalSiblingIndex, transform.parent.childCount - 1));

            originalSiblingIndex = -1;
            handOverlayMode = false;
        }

        if (style != null)
            style.ApplyNow();
    }

    public void OnSeeHandClicked()
    {
        var state = GameManager.Instance.CurrentState;

        if (state != GameState.PlayerTurn &&
            state != GameState.AwaitingDiceDecision &&
            state != GameState.Combat &&
            state != GameState.Pilfer &&
            state != GameState.CavePhase &&
            !(state == GameState.ResolvingSpace &&
              GameManager.Instance.GetActivePlayer() != null &&
              GameManager.Instance.GetActivePlayer().currentSpace != null &&
              GameManager.Instance.GetActivePlayer().currentSpace.spaceType == SpaceType.Shop))
            return;

        PlayerPawn player = GameManager.Instance.GetCurrentHandOwner();
        if (player == null)
            return;

        HandUIManager.Instance.ToggleHand(player);
    }
}
