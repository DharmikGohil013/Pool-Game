using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Master game manager for the pool game.
/// Handles: UI panels, turn logic, pocket detection, dynamic ball-type assignment,
/// scoring, win/lose conditions, and programmatic HUD creation.
/// </summary>
public class PoolGameManager : MonoBehaviour
{
    public static PoolGameManager Instance { get; private set; }

    public enum GameState { Home, Playing, Paused, GameOver }

    [Header("Game State")]
    public GameState currentState = GameState.Home;
    public int activePlayer = 1; // Player 1 or 2

    [Header("UI Panels")]
    public GameObject homeScreenPanel;
    public GameObject pausePanel;
    public GameObject completePanel;
    public GameObject hudPanel;

    [Header("HUD Text References")]
    public TextMeshProUGUI turnText;
    public TextMeshProUGUI player1ScoreText;
    public TextMeshProUGUI player2ScoreText;
    public TextMeshProUGUI statusText;

    [Header("Pocket Detection Settings")]
    [Tooltip("If Y coordinate goes below this offset from table top, ball is pocketed.")]
    public float pocketYThreshold = 0.3f;

    // ─── INTERNAL STATE ───────────────────────────────────────

    private PoolGameSetup setupHelper;
    private bool referencesInitialized = false;

    // Ball type assignment (dynamic — decided on first pocket)
    private bool ballTypesAssigned = false;
    private int player1BallGroup = 0; // 0=unassigned, 1=solids (no suffix), 2=stripes (with "(1)")
    private int player2BallGroup = 0;

    // Ball lists (populated after type assignment)
    private List<GameObject> player1Balls = new List<GameObject>();
    private List<GameObject> player2Balls = new List<GameObject>();

    // Per-turn tracking
    private bool isShotInProgress = false;
    public bool IsShotInProgress => isShotInProgress;
    private List<GameObject> ballsPocketedThisTurn = new List<GameObject>();
    private bool cueBallPocketedThisTurn = false;
    private bool strikerPocketedThisTurn = false;

    // ─── LIFECYCLE ────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        InitializeReferences();
        GoToHome();
    }

    private void Update()
    {
        if (currentState != GameState.Playing) return;

        // Safety fallback: Check if any ball fell off the table
        CheckBallFellOffTable();

        // Monitor if balls are moving after a shot
        if (isShotInProgress)
        {
            if (AreAllBallsStopped())
            {
                EndShot();
            }
        }
        else
        {
            // Safety check: if cue ball is inactive while shot is NOT in progress, respawn it!
            if (setupHelper != null && setupHelper.cueBall != null && !setupHelper.cueBall.activeSelf)
            {
                Debug.LogWarning("Cue ball inactive while shot is not in progress. Respawning cue ball.");
                ResetCueBall();
                SwapTurn("Scratch! Turn \u2192 Player " + (activePlayer == 1 ? 2 : 1));
                ResetTurnState();
            }
        }
    }

    private void CheckBallFellOffTable()
    {
        if (setupHelper == null || setupHelper.table == null) return;

        float tableTopY = setupHelper.table.transform.position.y + (setupHelper.table.transform.localScale.y / 2f);
        float thresholdY = tableTopY - pocketYThreshold;

        // 1. Check Cue Ball
        if (setupHelper.cueBall != null && setupHelper.cueBall.activeSelf)
        {
            if (setupHelper.cueBall.transform.position.y < thresholdY)
            {
                BallEnteredPocket(setupHelper.cueBall);
            }
        }

        // 2. Check Striker (8-ball)
        if (setupHelper.striker != null && setupHelper.striker.activeSelf)
        {
            if (setupHelper.striker.transform.position.y < thresholdY)
            {
                BallEnteredPocket(setupHelper.striker);
            }
        }

        // 3. Check Object Balls
        foreach (GameObject ball in setupHelper.objectBalls)
        {
            if (ball != null && ball.activeSelf)
            {
                if (ball.transform.position.y < thresholdY)
                {
                    BallEnteredPocket(ball);
                }
            }
        }
    }

    // ─── INITIALIZATION ───────────────────────────────────────

    public void InitializeReferences()
    {
        if (referencesInitialized) return;

        setupHelper = FindFirstObjectByType<PoolGameSetup>();
        if (setupHelper == null)
        {
            Debug.LogError("PoolGameSetup script not found in scene!");
            return;
        }

        // Auto-detect UI panels
        if (homeScreenPanel == null) homeScreenPanel = GameObject.Find("Home Screen");
        if (pausePanel == null) pausePanel = GameObject.Find("pause");
        if (completePanel == null)
        {
            completePanel = GameObject.Find("Complaet game");
            if (completePanel == null) completePanel = GameObject.Find("Complete game");
        }
        if (hudPanel == null) hudPanel = GameObject.Find("HUD Game Screen");

        // Bind UI Buttons
        BindAllButtons();

        // Auto-detect HUD Texts or create them if missing
        if (hudPanel != null)
        {
            FindHUDTexts();
            Button pauseBtn = hudPanel.GetComponentInChildren<Button>(true);
            if (turnText == null || player1ScoreText == null || player2ScoreText == null || statusText == null || pauseBtn == null)
            {
                SetupHUDUI();
            }
        }

        // Setup pocket triggers on all capsules under Table > Hall
        SetupPockets();

        referencesInitialized = true;
    }

    private void FindHUDTexts()
    {
        if (hudPanel == null) return;
        TextMeshProUGUI[] tmps = hudPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var tmp in tmps)
        {
            string name = tmp.gameObject.name.ToLower();
            if (name.Contains("turn")) turnText = tmp;
            else if (name.Contains("p1") || name.Contains("player1") || name.Contains("player 1")) player1ScoreText = tmp;
            else if (name.Contains("p2") || name.Contains("player2") || name.Contains("player 2")) player2ScoreText = tmp;
            else if (name.Contains("status")) statusText = tmp;
        }
    }

    private void BindAllButtons()
    {
        // 1. Home Screen Buttons
        if (homeScreenPanel != null)
        {
            Button[] buttons = homeScreenPanel.GetComponentsInChildren<Button>(true);
            if (buttons.Length >= 1) buttons[0].onClick.AddListener(StartNewGame);
            if (buttons.Length >= 2) buttons[1].onClick.AddListener(QuitGame);
        }

        // 2. Pause Panel Buttons
        if (pausePanel != null)
        {
            Button[] buttons = pausePanel.GetComponentsInChildren<Button>(true);
            if (buttons.Length >= 1) buttons[0].onClick.AddListener(ResumeGame);
            if (buttons.Length >= 2) buttons[1].onClick.AddListener(RestartGame);
            if (buttons.Length >= 3) buttons[2].onClick.AddListener(GoToHome);
        }

        // 3. Complete Panel Buttons
        if (completePanel != null)
        {
            Button[] buttons = completePanel.GetComponentsInChildren<Button>(true);
            if (buttons.Length >= 1) buttons[0].onClick.AddListener(RestartGame);
            if (buttons.Length >= 2) buttons[1].onClick.AddListener(GoToHome);
        }

        // 4. HUD Pause Button
        if (hudPanel != null)
        {
            Button pauseBtn = hudPanel.GetComponentInChildren<Button>(true);
            if (pauseBtn != null) pauseBtn.onClick.AddListener(PauseGame);
        }
    }

    /// <summary>
    /// Finds all capsule pockets under Table > Hall and attaches PocketTrigger scripts.
    /// </summary>
    private void SetupPockets()
    {
        // Try to find Hall under Table first
        GameObject hallObj = null;

        if (setupHelper != null && setupHelper.table != null)
        {
            Transform hallTransform = setupHelper.table.transform.Find("Hall");
            if (hallTransform != null) hallObj = hallTransform.gameObject;
        }

        // Fallback: search in entire scene
        if (hallObj == null)
        {
            hallObj = GameObject.Find("Hall");
        }

        if (hallObj == null)
        {
            Debug.LogWarning("Hall object not found! Pocket trigger detection will not work.");
            return;
        }

        int pocketCount = 0;
        for (int i = 0; i < hallObj.transform.childCount; i++)
        {
            GameObject pocket = hallObj.transform.GetChild(i).gameObject;

            // Add PocketTrigger if not already present
            if (pocket.GetComponent<PocketTrigger>() == null)
            {
                pocket.AddComponent<PocketTrigger>();
            }

            // Ensure collider is set as trigger
            Collider col = pocket.GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            pocketCount++;
        }

        Debug.Log("Setup " + pocketCount + " pocket triggers under Hall.");
    }

    // ─── GAME FLOW ────────────────────────────────────────────

    public void StartNewGame()
    {
        InitializeReferences();

        // Reset physics setup and ball positions
        if (setupHelper != null)
        {
            setupHelper.SetupGame();
        }

        // Reset all game state
        player1Balls.Clear();
        player2Balls.Clear();
        ballTypesAssigned = false;
        player1BallGroup = 0;
        player2BallGroup = 0;
        activePlayer = 1;
        isShotInProgress = false;
        ResetTurnState();

        currentState = GameState.Playing;
        Time.timeScale = 1f;

        // Show/Hide Panels
        if (homeScreenPanel != null) homeScreenPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (completePanel != null) completePanel.SetActive(false);
        if (hudPanel != null) hudPanel.SetActive(true);

        UpdateHUD();
        if (statusText != null) statusText.text = "Player 1 Breaks! Table is Open.";
    }

    public void ResumeGame()
    {
        currentState = GameState.Playing;
        Time.timeScale = 1f;
        if (pausePanel != null) pausePanel.SetActive(false);
        if (hudPanel != null) hudPanel.SetActive(true);

        if (!isShotInProgress && setupHelper != null && setupHelper.cueStick != null)
        {
            setupHelper.cueStick.SetActive(true);
        }
    }

    public void PauseGame()
    {
        currentState = GameState.Paused;
        Time.timeScale = 0f;
        if (pausePanel != null) pausePanel.SetActive(true);

        if (setupHelper != null && setupHelper.cueStick != null)
        {
            setupHelper.cueStick.SetActive(false);
        }
    }

    public void RestartGame()
    {
        StartNewGame();
    }

    public void GoToHome()
    {
        currentState = GameState.Home;
        Time.timeScale = 1f;
        if (homeScreenPanel != null) homeScreenPanel.SetActive(true);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (completePanel != null) completePanel.SetActive(false);
        if (hudPanel != null) hudPanel.SetActive(false);

        // Hide cue stick in home screen
        if (setupHelper != null && setupHelper.cueStick != null)
        {
            setupHelper.cueStick.SetActive(false);
        }
    }

    public void QuitGame()
    {
        Debug.Log("Quitting game...");
        Application.Quit();
    }

    // ─── SHOT & POCKET DETECTION ──────────────────────────────

    /// <summary>
    /// Called by PoolCueController when the cue ball is struck.
    /// </summary>
    public void NotifyShotStarted()
    {
        isShotInProgress = true;

        // Hide the stick while balls are in motion
        if (setupHelper != null && setupHelper.cueStick != null)
        {
            setupHelper.cueStick.SetActive(false);
        }

        if (statusText != null) statusText.text = "Balls in motion...";
    }

    /// <summary>
    /// Called by PocketTrigger when any ball enters a pocket hole.
    /// </summary>
    public void BallEnteredPocket(GameObject ball)
    {
        if (currentState != GameState.Playing) return;
        if (ball == null || setupHelper == null) return;

        // Prevent double-detection (ball already deactivated)
        if (!ball.activeSelf) return;

        // ── CUE BALL ──
        if (ball == setupHelper.cueBall)
        {
            cueBallPocketedThisTurn = true;
            StopAndDeactivateBall(ball);
            if (statusText != null) statusText.text = "Scratch! Cue ball pocketed.";

            // If shot is not in progress, immediately reset cue ball & swap turn so game never soft-locks
            if (!isShotInProgress)
            {
                ResetCueBall();
                SwapTurn("Scratch! Turn \u2192 Player " + (activePlayer == 1 ? 2 : 1));
                ResetTurnState();
            }
            return;
        }

        // ── STRIKER (8-BALL / BLACK BALL) ──
        if (ball == setupHelper.striker)
        {
            strikerPocketedThisTurn = true;
            StopAndDeactivateBall(ball);

            // If shot is not in progress, immediately end game
            if (!isShotInProgress)
            {
                EndGameOnStrikerPocketed();
                ResetTurnState();
            }
            return;
        }

        // ── OBJECT BALL ──
        if (setupHelper.objectBalls.Contains(ball))
        {
            if (!ballsPocketedThisTurn.Contains(ball))
            {
                ballsPocketedThisTurn.Add(ball);
            }
            StopAndDeactivateBall(ball);

            // Dynamic ball-type assignment on first pocket ever
            if (!ballTypesAssigned)
            {
                AssignBallTypes(ball);
            }

            UpdateHUD();
        }
    }

    private void StopAndDeactivateBall(GameObject ball)
    {
        Rigidbody rb = ball.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        ball.SetActive(false);
    }

    // ─── BALL TYPE ASSIGNMENT ─────────────────────────────────

    /// <summary>
    /// Dynamically assigns ball types on the first pocketed object ball.
    /// Balls without "(1)" in name = Solids (group 1).
    /// Balls with "(1)" in name = Stripes (group 2).
    /// The active player who pocketed gets that ball's group.
    /// </summary>
    private bool IsSolidBall(GameObject ball)
    {
        if (ball == null) return true;
        string name = ball.name.ToLower();

        // 1. Explicit (1) in name -> Stripe
        if (name.Contains("(1)")) return false;

        // 2. Extract digits from name (e.g. billiard_ball008 -> 8, billiard_ball014 -> 14)
        string digits = System.Text.RegularExpressions.Regex.Match(name, @"\d+").Value;
        if (int.TryParse(digits, out int ballNum))
        {
            if (ballNum >= 9 && ballNum <= 15) return false; // Stripes
            if (ballNum >= 1 && ballNum <= 7) return true;   // Solids
        }

        // 3. Fallback: position in setupHelper.objectBalls list (first half = Solids, second half = Stripes)
        if (setupHelper != null && setupHelper.objectBalls != null)
        {
            int index = setupHelper.objectBalls.IndexOf(ball);
            int half = setupHelper.objectBalls.Count / 2;
            if (index >= half) return false;
        }

        return true;
    }

    private void AssignBallTypes(GameObject firstPocketedBall)
    {
        bool pocketedSolid = IsSolidBall(firstPocketedBall);

        if (pocketedSolid)
        {
            // Active player gets Solids
            player1BallGroup = (activePlayer == 1) ? 1 : 2;
            player2BallGroup = (activePlayer == 1) ? 2 : 1;
        }
        else
        {
            // Active player gets Stripes
            player1BallGroup = (activePlayer == 1) ? 2 : 1;
            player2BallGroup = (activePlayer == 1) ? 1 : 2;
        }

        ballTypesAssigned = true;
        CategorizeBalls();

        string p1Type = player1BallGroup == 1 ? "Solids" : "Stripes";
        string p2Type = player2BallGroup == 1 ? "Solids" : "Stripes";
        if (statusText != null) statusText.text = "P1: " + p1Type + " | P2: " + p2Type;

        UpdateHUD();
    }

    /// <summary>
    /// Sorts all object balls into player1Balls and player2Balls based on assigned groups.
    /// </summary>
    private void CategorizeBalls()
    {
        player1Balls.Clear();
        player2Balls.Clear();
        if (setupHelper == null) return;

        foreach (GameObject ball in setupHelper.objectBalls)
        {
            if (ball == null) continue;
            bool isSolid = IsSolidBall(ball);

            if (player1BallGroup == 1) // P1 = Solids
            {
                if (isSolid) player1Balls.Add(ball);
                else player2Balls.Add(ball);
            }
            else // P1 = Stripes
            {
                if (isSolid) player2Balls.Add(ball);
                else player1Balls.Add(ball);
            }
        }
    }

    // ─── TURN LOGIC ───────────────────────────────────────────

    private bool AreAllBallsStopped()
    {
        if (setupHelper == null) return true;

        List<GameObject> allBalls = new List<GameObject>();
        if (setupHelper.cueBall != null && setupHelper.cueBall.activeSelf) allBalls.Add(setupHelper.cueBall);
        if (setupHelper.striker != null && setupHelper.striker.activeSelf) allBalls.Add(setupHelper.striker);
        foreach (GameObject ball in setupHelper.objectBalls)
        {
            if (ball != null && ball.activeSelf) allBalls.Add(ball);
        }

        bool allStopped = true;
        foreach (GameObject ball in allBalls)
        {
            Rigidbody rb = ball.GetComponent<Rigidbody>();
            if (rb != null)
            {
                float speed = rb.linearVelocity.magnitude;
                float angularSpeed = rb.angularVelocity.magnitude;

                // Force slowly moving balls to stop completely to speed up transition
                if (speed < 0.2f && angularSpeed < 0.2f)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                else
                {
                    allStopped = false;
                }
            }
        }

        return allStopped;
    }

    private void EndShot()
    {
        isShotInProgress = false;

        // ── 1. STRIKER (8-BALL) POCKETED ──
        if (strikerPocketedThisTurn)
        {
            EndGameOnStrikerPocketed();
            ResetTurnState();
            return;
        }

        // ── 2. CUE BALL SCRATCH ──
        if (cueBallPocketedThisTurn)
        {
            ResetCueBall();
            SwapTurn("Scratch! Turn \u2192 Player " + (activePlayer == 1 ? 2 : 1));
            ResetTurnState();
            return;
        }

        // ── 3. DETERMINE WHAT WAS POCKETED ──
        bool pocketedOwnBall = false;
        bool pocketedOpponentBall = false;

        foreach (var ball in ballsPocketedThisTurn)
        {
            if (ballTypesAssigned)
            {
                List<GameObject> ownBalls = activePlayer == 1 ? player1Balls : player2Balls;
                List<GameObject> oppBalls = activePlayer == 1 ? player2Balls : player1Balls;

                if (ownBalls.Contains(ball)) pocketedOwnBall = true;
                if (oppBalls.Contains(ball)) pocketedOpponentBall = true;
            }
            else
            {
                // Before type assignment, any pocket counts as "own" (keeps turn)
                pocketedOwnBall = true;
            }
        }

        // ── 4. APPLY TURN RULES ──
        if (pocketedOwnBall)
        {
            // Pocketed at least one own ball → keep turn
            int remaining = GetRemainingBallsCount(activePlayer);
            if (remaining == 0 && ballTypesAssigned)
            {
                if (statusText != null) statusText.text = "All clear! Player " + activePlayer + " \u2192 Pocket the 8-Ball!";
            }
            else
            {
                if (statusText != null) statusText.text = "Nice! Player " + activePlayer + " shoots again.";
            }
            ResetStickPosition();
        }
        else if (pocketedOpponentBall)
        {
            // Pocketed ONLY opponent's ball(s) → swap turn
            SwapTurn("Foul! Opponent's ball. Turn \u2192 Player " + (activePlayer == 1 ? 2 : 1));
        }
        else
        {
            // Nothing pocketed → swap turn
            SwapTurn("Turn \u2192 Player " + (activePlayer == 1 ? 2 : 1));
        }

        ResetTurnState();
    }

    private void SwapTurn(string statusMsg)
    {
        activePlayer = activePlayer == 1 ? 2 : 1;
        if (statusText != null) statusText.text = statusMsg;
        UpdateHUD();
        ResetStickPosition();
    }

    private void ResetStickPosition()
    {
        if (setupHelper != null)
        {
            setupHelper.PositionStick();
        }
    }

    /// <summary>
    /// Resets only the cue ball back to the head spot (used after a scratch).
    /// Does NOT disturb other balls.
    /// </summary>
    private void ResetCueBall()
    {
        if (setupHelper == null || setupHelper.cueBall == null) return;

        setupHelper.cueBall.SetActive(true);

        // Position at the head spot
        if (setupHelper.table != null)
        {
            float tableTopY = setupHelper.table.transform.position.y + (setupHelper.table.transform.localScale.y / 2f);
            float ballRadius = 0.5f * setupHelper.cueBall.transform.localScale.y;
            setupHelper.cueBall.transform.position = new Vector3(
                setupHelper.table.transform.position.x,
                tableTopY + ballRadius,
                setupHelper.table.transform.position.z + setupHelper.cueBallZOffset
            );
        }

        Rigidbody rb = setupHelper.cueBall.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void ResetTurnState()
    {
        ballsPocketedThisTurn.Clear();
        cueBallPocketedThisTurn = false;
        strikerPocketedThisTurn = false;
    }

    // ─── GAME OVER ────────────────────────────────────────────

    private void EndGameOnStrikerPocketed()
    {
        currentState = GameState.GameOver;

        int winner;
        string reason;

        if (!ballTypesAssigned)
        {
            // 8-ball pocketed before types were assigned = automatic loss
            winner = activePlayer == 1 ? 2 : 1;
            reason = "Player " + activePlayer + " pocketed the 8-Ball too early! Loses!";
        }
        else
        {
            int remaining = GetRemainingBallsCount(activePlayer);

            if (remaining == 0 && !cueBallPocketedThisTurn)
            {
                // Active player cleared all their balls and legally pocketed the 8-ball
                winner = activePlayer;
                reason = "Player " + activePlayer + " pocketed the 8-Ball to WIN!";
            }
            else if (cueBallPocketedThisTurn)
            {
                // Scratched while pocketing the 8-ball = loss
                winner = activePlayer == 1 ? 2 : 1;
                reason = "Player " + activePlayer + " scratched on the 8-Ball! Loses!";
            }
            else
            {
                // Still had remaining balls = pocketed 8-ball too early
                winner = activePlayer == 1 ? 2 : 1;
                reason = "Player " + activePlayer + " pocketed the 8-Ball too early! Loses!";
            }
        }

        ShowGameOverScreen(winner, reason);
    }

    private void ShowGameOverScreen(int winner, string reason)
    {
        if (completePanel != null)
        {
            completePanel.SetActive(true);
            TextMeshProUGUI congratsText = completePanel.GetComponentInChildren<TextMeshProUGUI>(true);
            if (congratsText != null)
            {
                congratsText.text = "Player " + winner + " Wins!\n<size=20>" + reason + "</size>";
            }
        }

        if (hudPanel != null) hudPanel.SetActive(false);
        if (setupHelper != null && setupHelper.cueStick != null)
        {
            setupHelper.cueStick.SetActive(false);
        }
    }

    // ─── HUD ──────────────────────────────────────────────────

    private void UpdateHUD()
    {
        if (turnText != null)
        {
            turnText.text = "Turn: Player " + activePlayer;
        }

        if (ballTypesAssigned)
        {
            string p1Type = player1BallGroup == 1 ? "Solids" : "Stripes";
            string p2Type = player2BallGroup == 1 ? "Solids" : "Stripes";
            int p1Total = player1Balls.Count;
            int p2Total = player2Balls.Count;
            int p1Pocketed = p1Total - GetRemainingBallsCount(1);
            int p2Pocketed = p2Total - GetRemainingBallsCount(2);

            if (player1ScoreText != null)
                player1ScoreText.text = "P1 [" + p1Type + "]: " + p1Pocketed + "/" + p1Total;
            if (player2ScoreText != null)
                player2ScoreText.text = "P2 [" + p2Type + "]: " + p2Pocketed + "/" + p2Total;
        }
        else
        {
            if (player1ScoreText != null)
                player1ScoreText.text = "P1: Table Open";
            if (player2ScoreText != null)
                player2ScoreText.text = "P2: Table Open";
        }
    }

    private int GetRemainingBallsCount(int playerNum)
    {
        int count = 0;
        List<GameObject> balls = playerNum == 1 ? player1Balls : player2Balls;
        foreach (var ball in balls)
        {
            if (ball != null && ball.activeSelf)
            {
                count++;
            }
        }
        return count;
    }

    // ─── HUD UI SETUP (Programmatic) ─────────────────────────

    [ContextMenu("Setup HUD UI")]
    public void SetupHUDUI()
    {
        // Find panel if null
        if (hudPanel == null)
        {
            hudPanel = GameObject.Find("HUD Game Screen");
            if (hudPanel == null)
            {
                Debug.LogError("HUD Panel not found! Cannot setup HUD UI.");
                return;
            }
        }

        // Clear all existing children under hudPanel to start clean
        List<GameObject> childrenToDestroy = new List<GameObject>();
        for (int i = 0; i < hudPanel.transform.childCount; i++)
        {
            childrenToDestroy.Add(hudPanel.transform.GetChild(i).gameObject);
        }
        foreach (GameObject child in childrenToDestroy)
        {
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }

        // Create UI elements
        // 1. Turn Text (centered at top)
        turnText = CreateText("TurnText", "Turn: Player 1",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector3(0f, -20f, 0f), new Vector2(400f, 60f), TextAlignmentOptions.Center, 32);

        // 2. Player 1 Score (top left)
        player1ScoreText = CreateText("Player1ScoreText", "P1: Table Open",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector3(40f, -20f, 0f), new Vector2(350f, 60f), TextAlignmentOptions.Left, 28);

        // 3. Player 2 Score (top right)
        player2ScoreText = CreateText("Player2ScoreText", "P2: Table Open",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector3(-160f, -20f, 0f), new Vector2(350f, 60f), TextAlignmentOptions.Right, 28);

        // 4. Status/Foul Text (bottom center)
        statusText = CreateText("StatusText", "Player 1 Breaks! Table is Open.",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector3(0f, 60f, 0f), new Vector2(800f, 80f), TextAlignmentOptions.Center, 26);

        // 5. Pause Button (far top right)
        Button pauseButton = CreateButton("pause", "||",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector3(-20f, -20f, 0f), new Vector2(50f, 50f));

        // Bind Pause Button
        pauseButton.onClick.RemoveAllListeners();
        pauseButton.onClick.AddListener(PauseGame);

        Debug.Log("HUD UI setup programmatically successfully!");
    }

    private TextMeshProUGUI CreateText(string name, string initText, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, TextAlignmentOptions alignment, float fontSize)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(hudPanel.transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();

        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = initText;
        tmp.alignment = alignment;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;

        tmp.textWrappingMode = TextWrappingModes.NoWrap;

        return tmp;
    }

    private Button CreateButton(string name, string labelText, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject buttonGo = new GameObject(name);
        buttonGo.transform.SetParent(hudPanel.transform, false);

        RectTransform rt = buttonGo.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;

        Image img = buttonGo.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        Button btn = buttonGo.AddComponent<Button>();
        btn.targetGraphic = img;

        // Button text
        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(buttonGo.transform, false);

        RectTransform textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = labelText;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 18;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;

        return btn;
    }
}
