using System.Collections.Generic;
using System.Collections;
using UnityEngine;

/// <summary>
/// Singleton that manages the game board and provides access to individual spaces.
/// </summary>
public class BoardManager : MonoBehaviour
{
    private const string BoardViewRootName = "BoardViewRoot";

    public static BoardManager Instance;

    [Header("Board Spaces")]
    [Tooltip("Ordered list of spaces that form the main board path.")]
    public List<BoardSpace> boardPath = new List<BoardSpace>();

    [Tooltip("Special cave space players move to during the cave phase.")]
    public BoardSpace caveSpace;

    [Tooltip("Special peak space, if used for any game effects.")]
    public BoardSpace peakSpace;

    [Header("Board Environment")]
    [Tooltip("Builds the generated playmat/background around the board when the scene starts.")]
    public bool autoBuildEnvironment = true;

    [Tooltip("When enabled, the main playmat expands far enough to include the cave space. Disable this for a tighter frame around only the board path.")]
    public bool includeCaveInEnvironmentFrame = false;

    [Tooltip("Extra space added around the main board path before the playmat frame is calculated. Negative values are allowed for visual tightening.")]
    public Vector2 environmentBoardPadding = new Vector2(12.64f, -1f);

    [Tooltip("Extra space reserved around the cave when Include Cave In Environment Frame is enabled. Negative values are allowed for visual tightening.")]
    public Vector2 environmentCavePadding = new Vector2(0.75f, 0.75f);

    [Tooltip("Manual nudge for the generated playmat frame.")]
    public Vector2 environmentFrameOffset = new Vector2(0f, -1.1f);

    [Tooltip("Extra size added to the drop shadow behind the playmat.")]
    public Vector2 environmentShadowPadding = new Vector2(0.85f, 0.75f);

    [Tooltip("Extra size added to the outer decorative border.")]
    public Vector2 environmentOuterPadding = new Vector2(0.58f, 0.5f);

    [Tooltip("Extra size added to the inner decorative border.")]
    public Vector2 environmentInnerPadding = new Vector2(0.32f, 0.28f);

    [Tooltip("Extra size added to the main field behind the playable board.")]
    public Vector2 environmentFieldPadding = Vector2.zero;

    [Header("Board View Rotation")]
    [Tooltip("Rotates the board at turn changes so the active player's home biome is at the bottom of the screen. Disabled for now while the board visuals are stabilized.")]
    public bool rotateViewToActivePlayer = false;

    [Tooltip("Seconds used to rotate between player perspectives.")]
    public float viewRotationDuration = 0.45f;

    [Tooltip("When enabled, the active pawn's current position is placed at the bottom of the screen instead of using only the player's home side.")]
    public bool orientViewToCurrentPawnPosition = false;

    [Tooltip("Keeps pawn artwork readable while pawn positions rotate with the board view.")]
    public bool keepPawnSpritesUpright = true;

    private BoardEnvironment boardEnvironment;
    private EnvironmentSettingsSnapshot environmentSnapshot;
    private Coroutine viewRotationRoutine;
    private Transform boardViewRoot;
    private bool boardViewRootPositioned;
    private bool hasOrientedView;

    private void Awake()
    {
        // Implement singleton pattern
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        EnsureBoardViewRoot();

        if (autoBuildEnvironment)
            EnsureBoardEnvironment();

        ResetBoardViewToUpright();
    }

    private void Update()
    {
        if (!Application.isPlaying || !autoBuildEnvironment)
            return;

        EnvironmentSettingsSnapshot currentSnapshot = CaptureEnvironmentSettings();
        if (!currentSnapshot.Equals(environmentSnapshot))
            RebuildBoardEnvironment();
    }

    // =========================
    // BOARD INFORMATION
    // =========================

    /// <summary>
    /// Returns the index of the last space on the board path.
    /// </summary>
    public int GetLastIndex()
    {
        return boardPath.Count - 1;
    }

    /// <summary>
    /// Returns the BoardSpace at a given index, or null if index is invalid.
    /// </summary>
    public BoardSpace GetSpaceAt(int index)
    {
        if (index >= 0 && index < boardPath.Count)
            return boardPath[index];

        Debug.LogError($"BoardManager: Invalid board index {index}");
        return null;
    }

    /// <summary>
    /// Returns the special cave space, or logs an error if not assigned.
    /// </summary>
    public BoardSpace GetCaveSpace()
    {
        if (caveSpace != null)
            return caveSpace;

        Debug.LogError("BoardManager: Cave space not assigned!");
        return null;
    }

    /// <summary>
    /// Returns the special peak space, or logs an error if not assigned.
    /// </summary>
    public BoardSpace GetPeakSpace()
    {
        if (peakSpace != null)
            return peakSpace;

        Debug.LogError("BoardManager: Peak space not assigned!");
        return null;
    }

    public int GetBiomeSize()
    {
        int playerCount = GameManager.Instance != null && GameManager.Instance.players != null
            ? GameManager.Instance.players.Length
            : 4;

        if (playerCount <= 0 || boardPath.Count == 0)
            return 0;

        return Mathf.Max(1, boardPath.Count / playerCount);
    }

    public int GetBiomeIndex(int boardIndex)
    {
        BoardSpace space = GetSpaceAt(boardIndex);
        if (space != null && space.biome != BoardBiome.None)
            return (int)space.biome;

        int biomeSize = GetBiomeSize();
        if (biomeSize <= 0)
            return -1;

        return Mathf.Clamp(boardIndex / biomeSize, 0, Mathf.Max(0, (boardPath.Count / biomeSize) - 1));
    }

    public int GetCircularDistance(int firstIndex, int secondIndex)
    {
        if (boardPath == null || boardPath.Count == 0)
            return int.MaxValue;

        int maxIndex = boardPath.Count;
        int directDistance = Mathf.Abs(firstIndex - secondIndex);
        return Mathf.Min(directDistance, maxIndex - directDistance);
    }

    public void OrientViewToPlayer(PlayerPawn player, bool instant = false)
    {
        if (!rotateViewToActivePlayer)
        {
            ResetBoardViewToUpright();
            return;
        }

        if (player == null)
            return;

        ReparentBoardViewObjects();
        ResetCameraRotation();

        float targetAngle = GetViewAngleForPlayer(player);
        bool shouldSnap = instant || !Application.isPlaying || !hasOrientedView || viewRotationDuration <= 0f;
        hasOrientedView = true;

        if (viewRotationRoutine != null)
            StopCoroutine(viewRotationRoutine);

        if (shouldSnap)
        {
            SetBoardViewRotation(targetAngle, true);
            return;
        }

        viewRotationRoutine = StartCoroutine(AnimateViewRotation(targetAngle));
    }

    public Transform GetOrCreateBoardViewRoot()
    {
        EnsureBoardViewRoot();
        return boardViewRoot;
    }

    private void EnsureBoardViewRoot()
    {
        if (boardViewRoot != null)
            return;

        GameObject existingRoot = GameObject.Find(BoardViewRootName);
        boardViewRoot = existingRoot != null
            ? existingRoot.transform
            : new GameObject(BoardViewRootName).transform;

        boardViewRoot.localScale = Vector3.one;

        if (!boardViewRootPositioned)
        {
            boardViewRoot.position = GetBoardPathCenter();
            boardViewRootPositioned = true;
        }
    }

    private void ReparentBoardViewObjects()
    {
        EnsureBoardViewRoot();
        DetachFromBoardViewRoot(caveSpace != null ? caveSpace.transform : null);
        DetachFromBoardViewRoot(peakSpace != null ? peakSpace.transform : null);

        if (boardPath != null)
        {
            foreach (BoardSpace space in boardPath)
            {
                if (space != null)
                    ParentToBoardViewRoot(space.transform);
            }
        }

        if (GameManager.Instance == null || GameManager.Instance.players == null)
            return;

        foreach (PlayerPawn player in GameManager.Instance.players)
        {
            if (player == null)
                continue;

            if (player.inCavePhase)
                DetachFromBoardViewRoot(player.transform);
            else
                ParentToBoardViewRoot(player.transform);
        }
    }

    private void ParentToBoardViewRoot(Transform target)
    {
        if (target == null || target == boardViewRoot)
            return;

        if (!target.IsChildOf(boardViewRoot))
            target.SetParent(boardViewRoot, true);

        // Spaces should behave like printed board pieces: positions and icon orientation
        // both follow the rotating board root rather than preserving world rotation.
        if (target.GetComponent<BoardSpace>() != null)
            target.localRotation = Quaternion.identity;
    }

    private void DetachFromBoardViewRoot(Transform target)
    {
        if (target == null || boardViewRoot == null || !target.IsChildOf(boardViewRoot))
            return;

        target.SetParent(null, true);
    }

    private IEnumerator AnimateViewRotation(float targetAngle)
    {
        if (boardViewRoot == null)
            yield break;

        float startAngle = NormalizeAngle(boardViewRoot.eulerAngles.z);
        float delta = Mathf.DeltaAngle(startAngle, targetAngle);
        float elapsed = 0f;

        while (elapsed < viewRotationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / viewRotationDuration);
            t = t * t * (3f - 2f * t);
            SetBoardViewRotation(startAngle + delta * t);
            yield return null;
        }

        SetBoardViewRotation(targetAngle, true);
        viewRotationRoutine = null;
    }

    private float GetViewAngleForPlayer(PlayerPawn player)
    {
        EnsureBoardViewRoot();

        if (!orientViewToCurrentPawnPosition)
            return GetHomeSideViewAngle(player);

        Vector2 direction = GetLocalDirectionFromBoardCenter(player.transform);

        if (direction.sqrMagnitude < 0.001f)
            return 0f;

        float sideAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        return NormalizeAngle(-90f - sideAngle);
    }

    private float GetHomeSideViewAngle(PlayerPawn player)
    {
        int biomeSize = GetBiomeSize();
        if (player == null || biomeSize <= 0)
            return 0f;

        // The board path starts on the left side, then travels bottom, right, and top.
        // Keep view changes to cardinal 90-degree turns so the board sides stay parallel to the screen.
        int sideIndex = Mathf.FloorToInt(player.startingIndex / (float)biomeSize) % 4;
        return NormalizeAngle(90f - sideIndex * 90f);
    }

    private Vector2 GetLocalDirectionFromBoardCenter(Transform target)
    {
        if (target == null || boardViewRoot == null)
            return Vector2.zero;

        return boardViewRoot.InverseTransformPoint(target.position);
    }

    private Vector2 GetHomeBiomeLocalDirection(PlayerPawn player)
    {
        BoardBiome homeBiome = (BoardBiome)player.GetHomeBiomeIndex();
        if (TryGetBiomeCenter(homeBiome, out Vector2 sideCenter))
        {
            Vector2 localSideCenter = boardViewRoot != null
                ? (Vector2)boardViewRoot.InverseTransformPoint(sideCenter)
                : sideCenter - GetBoardPathCenter();

            float dominantX = Mathf.Abs(localSideCenter.x);
            float dominantY = Mathf.Abs(localSideCenter.y);
            if (dominantX > dominantY)
                return new Vector2(Mathf.Sign(localSideCenter.x), 0f);

            return new Vector2(0f, Mathf.Sign(localSideCenter.y));
        }

        BoardSpace startingSpace = GetSpaceAt(player.startingIndex);
        return GetLocalDirectionFromBoardCenter(startingSpace != null ? startingSpace.transform : player.transform);
    }

    private bool TryGetBiomeCenter(BoardBiome biome, out Vector2 center)
    {
        center = Vector2.zero;
        if (biome == BoardBiome.None || boardPath == null || boardPath.Count == 0)
            return false;

        int count = 0;
        foreach (BoardSpace space in boardPath)
        {
            if (space == null || space.biome != biome)
                continue;

            center += (Vector2)space.transform.position;
            count++;
        }

        if (count <= 0)
            return false;

        center /= count;
        return true;
    }

    private Vector2 GetBoardPathCenter()
    {
        if (boardPath == null || boardPath.Count == 0 || boardPath[0] == null)
            return Vector2.zero;

        Bounds bounds = new Bounds(boardPath[0].transform.position, Vector3.zero);
        foreach (BoardSpace space in boardPath)
        {
            if (space != null)
                bounds.Encapsulate(space.transform.position);
        }

        return bounds.center;
    }

    private void SetBoardViewRotation(float zAngle, bool updateFixedDecor = false)
    {
        EnsureBoardViewRoot();
        if (boardViewRoot == null)
            return;

        Vector3 eulerAngles = boardViewRoot.eulerAngles;
        eulerAngles.z = NormalizeAngle(zAngle);
        boardViewRoot.eulerAngles = eulerAngles;
        SyncBoardSpaceIconRotation(eulerAngles.z);
        SyncPawnViewRotation(eulerAngles.z);

        if (updateFixedDecor)
            boardEnvironment?.UpdateGateToPeakPaths(this);
    }

    private void ResetBoardViewToUpright()
    {
        if (viewRotationRoutine != null)
        {
            StopCoroutine(viewRotationRoutine);
            viewRotationRoutine = null;
        }

        EnsureBoardViewRoot();
        if (boardViewRoot != null)
        {
            Vector3 eulerAngles = boardViewRoot.eulerAngles;
            eulerAngles.z = 0f;
            boardViewRoot.eulerAngles = eulerAngles;
        }

        ResetCameraRotation();
        SyncBoardSpaceIconRotation(0f);
        SyncPawnViewRotation(0f);
    }

    private void ResetCameraRotation()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        Vector3 eulerAngles = mainCamera.transform.eulerAngles;
        eulerAngles.z = 0f;
        mainCamera.transform.eulerAngles = eulerAngles;
    }

    private void SyncPawnViewRotation(float viewAngle)
    {
        if (GameManager.Instance == null || GameManager.Instance.players == null)
            return;

        foreach (PlayerPawn player in GameManager.Instance.players)
        {
            if (player != null)
                player.SetBoardViewRotation(viewAngle, keepPawnSpritesUpright);
        }
    }

    private void SyncBoardSpaceIconRotation(float viewAngle)
    {
        if (boardPath == null)
            return;

        foreach (BoardSpace space in boardPath)
        {
            if (space != null)
                space.SyncIconViewRotation(viewAngle);
        }
    }

    private float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle < 0f)
            angle += 360f;
        return angle;
    }

    private void EnsureBoardEnvironment()
    {
        boardEnvironment = GetComponent<BoardEnvironment>();
        if (boardEnvironment == null)
            boardEnvironment = gameObject.AddComponent<BoardEnvironment>();

        RebuildBoardEnvironment();
    }

    private void RebuildBoardEnvironment()
    {
        if (boardEnvironment == null)
            boardEnvironment = GetComponent<BoardEnvironment>();

        if (boardEnvironment == null)
            return;

        boardEnvironment.BuildEnvironment(this);
        environmentSnapshot = CaptureEnvironmentSettings();
    }

    private EnvironmentSettingsSnapshot CaptureEnvironmentSettings()
    {
        return new EnvironmentSettingsSnapshot
        {
            includeCaveInEnvironmentFrame = includeCaveInEnvironmentFrame,
            boardPadding = environmentBoardPadding,
            cavePadding = environmentCavePadding,
            frameOffset = environmentFrameOffset,
            shadowPadding = environmentShadowPadding,
            outerPadding = environmentOuterPadding,
            innerPadding = environmentInnerPadding,
            fieldPadding = environmentFieldPadding
        };
    }

    private struct EnvironmentSettingsSnapshot
    {
        public bool includeCaveInEnvironmentFrame;
        public Vector2 boardPadding;
        public Vector2 cavePadding;
        public Vector2 frameOffset;
        public Vector2 shadowPadding;
        public Vector2 outerPadding;
        public Vector2 innerPadding;
        public Vector2 fieldPadding;
    }
}
