using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SessionEntryMode
{
    None,
    LocalPrototype,
    Host,
    Join
}

public enum SessionFlowState
{
    Boot,
    Home,
    Lobby,
    Match
}

/// <summary>
/// Persistent session/bootstrap seam for future networking flow.
/// For now it provides a clean home/lobby/match entry path without
/// changing board gameplay.
/// </summary>
public class GameSessionBootstrap : MonoBehaviour
{
    public const string HomeSceneName = "HomeScene";
    public const string BoardSceneName = "BoardScene";

    public static GameSessionBootstrap Instance { get; private set; }

    public SessionEntryMode EntryMode { get; private set; } = SessionEntryMode.None;
    public SessionFlowState FlowState { get; private set; } = SessionFlowState.Boot;
    public string PendingJoinCode { get; private set; } = string.Empty;

    public event Action<SessionFlowState> FlowStateChanged;

    [SerializeField] private bool createHomeFirstFlow = false;
    [SerializeField] private bool verboseLogging = true;

    public static GameSessionBootstrap EnsureExists()
    {
        if (Instance != null)
            return Instance;

        GameObject bootstrapObject = new GameObject("GameSessionBootstrap");
        return bootstrapObject.AddComponent<GameSessionBootstrap>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (createHomeFirstFlow && SceneManager.GetActiveScene().name != HomeSceneName)
            OpenHome();
        else
            UpdateFlowStateFromScene(SceneManager.GetActiveScene().name);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    public void BeginLocalPrototypeFlow()
    {
        EntryMode = SessionEntryMode.LocalPrototype;
        FlowState = SessionFlowState.Match;
        NotifyFlowChanged();
        LoadSceneIfAvailable(BoardSceneName);
    }

    public void BeginHostFlow()
    {
        EntryMode = SessionEntryMode.Host;
        FlowState = SessionFlowState.Lobby;
        NotifyFlowChanged();
        DebugLog("Host flow selected. Lobby scaffold will be the next networking step.");
    }

    public void BeginJoinFlow(string joinCode = "")
    {
        EntryMode = SessionEntryMode.Join;
        PendingJoinCode = joinCode ?? string.Empty;
        FlowState = SessionFlowState.Lobby;
        NotifyFlowChanged();
        DebugLog($"Join flow selected{(string.IsNullOrWhiteSpace(PendingJoinCode) ? "." : $" with code '{PendingJoinCode}'.")}");
    }

    public void OpenHome()
    {
        EntryMode = SessionEntryMode.None;
        PendingJoinCode = string.Empty;
        FlowState = SessionFlowState.Home;
        NotifyFlowChanged();
        LoadSceneIfAvailable(HomeSceneName);
    }

    public void StartMatchFromLobby()
    {
        if (FlowState != SessionFlowState.Lobby)
            DebugLog("StartMatchFromLobby called outside of lobby flow.");

        FlowState = SessionFlowState.Match;
        NotifyFlowChanged();
        LoadSceneIfAvailable(BoardSceneName);
    }

    public bool IsHomeSceneAvailable()
    {
        return Application.CanStreamedLevelBeLoaded(HomeSceneName);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateFlowStateFromScene(scene.name);
    }

    private void UpdateFlowStateFromScene(string sceneName)
    {
        if (sceneName == HomeSceneName)
        {
            FlowState = SessionFlowState.Home;
        }
        else if (sceneName == BoardSceneName)
        {
            if (EntryMode == SessionEntryMode.Host || EntryMode == SessionEntryMode.Join)
                FlowState = SessionFlowState.Match;
            else if (FlowState == SessionFlowState.Boot || FlowState == SessionFlowState.Home)
                FlowState = SessionFlowState.Match;
        }

        NotifyFlowChanged();
    }

    private void LoadSceneIfAvailable(string sceneName)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"Scene '{sceneName}' is not in Build Settings yet.");
            return;
        }

        if (SceneManager.GetActiveScene().name == sceneName)
        {
            UpdateFlowStateFromScene(sceneName);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private void NotifyFlowChanged()
    {
        FlowStateChanged?.Invoke(FlowState);
    }

    private void DebugLog(string message)
    {
        if (verboseLogging)
            Debug.Log($"[SessionBootstrap] {message}");
    }
}
