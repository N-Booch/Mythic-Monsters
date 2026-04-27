using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CombatSceneDirector : MonoBehaviour
{
    public const string CombatSceneName = "CombatScene";

    public static CombatSceneDirector Instance { get; private set; }

    public bool IsCombatSceneLoaded { get; private set; }
    public bool IsTransitioning { get; private set; }

    private RollButtonUI cachedBoardRoll;
    private SeeHandButtonUI cachedSeeHand;
    private ConfirmRollUI cachedConfirmRoll;
    private DiceUI cachedBoardDie;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public static CombatSceneDirector EnsureExists()
    {
        if (Instance != null)
            return Instance;

        GameObject root = new GameObject("CombatSceneDirector");
        return root.AddComponent<CombatSceneDirector>();
    }

    public bool CanLoadCombatScene()
    {
        return Application.CanStreamedLevelBeLoaded(CombatSceneName);
    }

    public void BeginCombatScene(PlayerPawn player)
    {
        if (player == null || IsCombatSceneLoaded || IsTransitioning || !CanLoadCombatScene())
            return;

        StartCoroutine(LoadCombatSceneRoutine(player));
    }

    public void EndCombatScene(Action onComplete)
    {
        if (!IsCombatSceneLoaded || IsTransitioning)
        {
            onComplete?.Invoke();
            return;
        }

        StartCoroutine(UnloadCombatSceneRoutine(onComplete));
    }

    private IEnumerator LoadCombatSceneRoutine(PlayerPawn player)
    {
        IsTransitioning = true;
        SetBoardHudVisible(false);

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(CombatSceneName, LoadSceneMode.Additive);
        while (loadOperation != null && !loadOperation.isDone)
            yield return null;

        IsCombatSceneLoaded = SceneManager.GetSceneByName(CombatSceneName).isLoaded;
        IsTransitioning = false;

        HandUIManager.Instance?.EnterCombatPresentation(player);
        CombatSceneController.Instance?.RefreshFromState();
    }

    private IEnumerator UnloadCombatSceneRoutine(Action onComplete)
    {
        IsTransitioning = true;
        HandUIManager.Instance?.ExitCombatPresentation();

        AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(CombatSceneName);
        while (unloadOperation != null && !unloadOperation.isDone)
            yield return null;

        IsCombatSceneLoaded = false;
        IsTransitioning = false;
        SetBoardHudVisible(true);
        onComplete?.Invoke();
    }

    private void SetBoardHudVisible(bool visible)
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
            return;

        if (!visible)
        {
            cachedBoardRoll = FindFirstObjectByType<RollButtonUI>();
            cachedSeeHand = FindFirstObjectByType<SeeHandButtonUI>();
            cachedConfirmRoll = ConfirmRollUI.Instance;
            cachedBoardDie = DiceManager.Instance != null ? DiceManager.Instance.diceUI : null;

            if (gameManager.playerHUD != null)
                gameManager.playerHUD.gameObject.SetActive(false);
            if (gameManager.endTurnButtonUI != null)
                gameManager.endTurnButtonUI.gameObject.SetActive(false);
            if (cachedBoardRoll != null)
                cachedBoardRoll.gameObject.SetActive(false);
            if (cachedSeeHand != null)
                cachedSeeHand.gameObject.SetActive(false);
            if (cachedConfirmRoll != null)
                cachedConfirmRoll.gameObject.SetActive(false);
            if (cachedBoardDie != null)
                cachedBoardDie.gameObject.SetActive(false);

            return;
        }

        if (gameManager.playerHUD != null)
            gameManager.playerHUD.gameObject.SetActive(true);
        if (gameManager.endTurnButtonUI != null)
            gameManager.endTurnButtonUI.gameObject.SetActive(true);

        if (cachedBoardRoll != null)
            cachedBoardRoll.gameObject.SetActive(true);
        if (cachedSeeHand != null)
            cachedSeeHand.gameObject.SetActive(true);
        if (cachedConfirmRoll != null)
            cachedConfirmRoll.gameObject.SetActive(false);
        if (cachedBoardDie != null)
            cachedBoardDie.gameObject.SetActive(true);

        gameManager.RefreshActionAvailability();
    }
}
