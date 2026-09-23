using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class GameOverManager : MonoBehaviour
{
    // The Singleton instance that any script can find globally
    public static GameOverManager Instance;

    // The local player's latest score, kept so the result screen can still show it if the connection drops
    public static int LastKnownScore;
    public static MatchSummary LastKnownSummary;
    // XP earned by the match that just ended (set by TetrisEngine right before the result screen)
    public static XpResult? LastXp;

    [Header("UI Elements")]
    public GameObject gameOverPanel;
    [Tooltip("Optional title: YOU WIN! / YOU LOSE (empty outside versus matches)")]
    public TextMeshProUGUI resultText;
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI highScoreText;
    [Tooltip("Extra line: rematch status, 'Opponent left the match', etc. (found as NoteText if empty)")]
    public TextMeshProUGUI noteText;
    [Tooltip("Found as RematchButton inside the panel if empty")]
    public Button rematchButton;
    [Tooltip("World top 10 for Sprint / Ultra (found as LeaderboardText if empty)")]
    public TextMeshProUGUI leaderboardText;
    [Tooltip("XP earned and level progress (found as XpText if empty)")]
    public TextMeshProUGUI xpText;

    private bool _opponentLeft;
    private bool _rematchRequested;

    void Awake()
    {
        // Each gameplay scene load brings a fresh manager; the previous one was destroyed with its scene
        Instance = this;

        // The panel lives in a prefab, so find its pieces by name if they weren't dragged in
        if (gameOverPanel != null)
        {
            if (resultText == null) resultText = FindText("ResultText");
            if (finalScoreText == null) finalScoreText = FindText("FinalScoreText");
            if (highScoreText == null) highScoreText = FindText("HighScoreText");
            if (noteText == null) noteText = FindText("NoteText");
            if (leaderboardText == null) leaderboardText = FindText("LeaderboardText");
            if (xpText == null) xpText = FindText("XpText");
            if (leaderboardText != null) leaderboardText.transform.parent.gameObject.SetActive(GameModeSettings.IsSolo);
            if (rematchButton == null)
            {
                foreach (Button b in gameOverPanel.GetComponentsInChildren<Button>(true))
                {
                    if (b.name == "RematchButton") rematchButton = b;
                }
            }
        }
        if (rematchButton != null) rematchButton.onClick.AddListener(Rematch);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        // Auto-disable the panel when the game starts
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    public void TriggerGameOver(int finalScore, bool isVersusMatch = false, bool isWinner = false)
    {
        // 1. Show the panel
        if (gameOverPanel != null) gameOverPanel.SetActive(true);

        if (resultText != null)
        {
            if (!isVersusMatch) resultText.text = ""; // The background art already says GAME OVER
            else if (isWinner) resultText.text = "<color=green>YOU WIN!</color>";
            else resultText.text = "<color=red>YOU LOSE</color>";
        }

        // 2. Calculate High Score
        int savedHighScore = SavedHighScore;
        bool isNewHighScore = finalScore > savedHighScore;
        if (isNewHighScore)
        {
            savedHighScore = finalScore;
            PlayerPrefs.SetInt(HighScoreKey, savedHighScore);
            PlayerPrefs.Save();
        }

        // 3. Update the Text
        if (finalScoreText != null) finalScoreText.text = "YOUR SCORE: " + finalScore;
        if (highScoreText != null)
        {
            highScoreText.text = isNewHighScore
                ? "<color=yellow>NEW HIGH SCORE! " + savedHighScore + "</color>"
                : "HIGH SCORE: " + savedHighScore;
        }

        // 4. Rematch / opponent status
        if (_opponentLeft) ShowOpponentLeftNote();
        else
        {
            SetNote(_rematchRequested ? "Waiting for your opponent..." : "");
            SetRematchAvailable(!_rematchRequested);
        }

        ShowXp();

        // 5. Sound
        if (isVersusMatch) AudioManager.Play(isWinner ? Sfx.Win : Sfx.Lose);
        else AudioManager.Play(Sfx.GameOver);
        if (isNewHighScore) AudioManager.Play(Sfx.NewHighScore);
    }

    // ---------- Sprint / Ultra results ----------

    public void TriggerModeResult(MatchMode mode, bool completed, float time, int score, int lines)
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        SetRematchAvailable(true); // "Play again" in solo modes restarts immediately
        TMP_Text rematchLabel = rematchButton != null ? rematchButton.GetComponentInChildren<TMP_Text>(true) : null;
        if (rematchLabel != null) rematchLabel.text = "PLAY AGAIN";
        SetNote("");

        bool newBest = false;
        if (mode == MatchMode.Sprint)
        {
            if (completed)
            {
                newBest = GameModeSettings.RecordSprint(time);
                SetTexts("<color=green>SPRINT CLEAR!</color>",
                         "TIME: " + GameModeSettings.FormatTime(time),
                         newBest ? "<color=yellow>NEW PERSONAL BEST!</color>" : "BEST: " + GameModeSettings.FormatTime(GameModeSettings.BestSprintTime));
                SubmitToLeaderboard(OnlineLeaderboard.SprintBoard, time * 1000.0);
            }
            else
            {
                float best = GameModeSettings.BestSprintTime;
                SetTexts("<color=red>SPRINT FAILED</color>",
                         $"LINES: {lines} / {GameModeSettings.SprintLines}",
                         best > 0f ? "BEST: " + GameModeSettings.FormatTime(best) : "Clear 40 lines to set a time");
            }
        }
        else
        {
            newBest = GameModeSettings.RecordUltra(score);
            SetTexts(completed ? "<color=green>TIME UP!</color>" : "<color=red>TOPPED OUT</color>",
                     "SCORE: " + score,
                     newBest ? "<color=yellow>NEW PERSONAL BEST!</color>" : "BEST: " + GameModeSettings.BestUltraScore);
            SubmitToLeaderboard(OnlineLeaderboard.UltraBoard, score);
        }

        ShowXp();

        if (completed || mode == MatchMode.Ultra) AudioManager.Play(Sfx.Win);
        else AudioManager.Play(Sfx.GameOver);
        if (newBest) AudioManager.Play(Sfx.NewHighScore);
    }

    // "+85 XP  ·  LEVEL 7 ROOKIE (65%)" or "LEVEL UP!"
    private void ShowXp()
    {
        if (LastXp == null) return;
        XpResult xp = LastXp.Value;
        LastXp = null;

        PlayerProfile p = ProfileStore.Profile;
        string line = xp.LeveledUp
            ? $"<color=yellow>+{xp.gained} XP  ·  LEVEL UP! LEVEL {p.level} {ProfileStore.RankTitle(p.level).ToUpper()}</color>"
            : $"<color=#9FCBFF>+{xp.gained} XP  ·  LEVEL {p.level} {ProfileStore.RankTitle(p.level).ToUpper()} ({Mathf.RoundToInt(ProfileStore.LevelProgress * 100)}%)</color>";

        if (xpText != null) xpText.text = line;
        else if (highScoreText != null) highScoreText.text += "\n<size=70%>" + line + "</size>";

        if (xp.LeveledUp) AudioManager.Play(Sfx.NewHighScore);
    }

    private void SetTexts(string result, string score, string best)
    {
        if (resultText != null) resultText.text = result;
        if (finalScoreText != null) finalScoreText.text = score;
        if (highScoreText != null) highScoreText.text = best;
    }

    // Posts the run, then shows the world rank and the top 10 (if the leaderboard service is set up)
    private async void SubmitToLeaderboard(string boardId, double value)
    {
        SetNote("<color=#9FCBFF>Submitting to the world leaderboard...</color>");
        string rank = await OnlineLeaderboard.SubmitAsync(boardId, value);
        if (this == null) return; // Scene changed while waiting
        SetNote(rank);

        if (leaderboardText == null) return;
        leaderboardText.text = "<b>WORLD TOP 10</b>\n<size=70%>loading...</size>";
        string top = await OnlineLeaderboard.TopTextAsync(boardId, 10);
        if (this != null && leaderboardText != null) leaderboardText.text = "<b>WORLD TOP 10</b>\n" + top;
    }

    public const string HighScoreKey = "MyHighScore";
    public static int SavedHighScore => PlayerPrefs.GetInt(HighScoreKey, 0);

    // ---------- Rematch ----------

    public void Rematch()
    {
        if (FusionLauncher.SessionOwner == null || _opponentLeft) return;
        _rematchRequested = true;
        SetRematchAvailable(false);
        SetNote("Waiting for your opponent...");
        FusionLauncher.SessionOwner.RequestRematch();
    }

    // Called by FusionLauncher when the other player asks for a rematch
    public void OnOpponentWantsRematch()
    {
        if (_rematchRequested || _opponentLeft) return;
        SetNote("<color=yellow>Your opponent wants a rematch!</color>");
    }

    // ---------- Disconnects ----------

    // Host side: the other player left. The match already declared us the winner.
    public void OnOpponentLeft()
    {
        _opponentLeft = true;
        if (IsShowing) ShowOpponentLeftNote();
    }

    // Client side: the session ended under us (host quit or the connection dropped)
    public void OnConnectionLost()
    {
        _opponentLeft = true;
        if (!IsShowing)
        {
            // The match never reached a result: still count what was played
            MatchSummary summary = LastKnownSummary;
            summary.outcome = MatchOutcome.Ended;
            LastXp = ProfileStore.RecordMatch(summary);
            TriggerGameOver(LastKnownScore, false, false);
            if (resultText != null) resultText.text = "<color=yellow>MATCH ENDED</color>";
        }
        SetNote("<color=#FF8080>Connection to your opponent was lost</color>");
        SetRematchAvailable(false);
    }

    private void ShowOpponentLeftNote()
    {
        SetNote("<color=#FF8080>Your opponent left the match</color>");
        SetRematchAvailable(false);
    }

    private void SetNote(string text)
    {
        if (noteText != null) noteText.text = text;
    }

    private void SetRematchAvailable(bool available)
    {
        if (rematchButton == null) return;
        bool sessionRunning = FusionLauncher.SessionOwner != null;
        rematchButton.gameObject.SetActive(sessionRunning || available);
        rematchButton.interactable = available && sessionRunning;
    }

    private TextMeshProUGUI FindText(string objectName)
    {
        foreach (TextMeshProUGUI text in gameOverPanel.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text.name == objectName) return text;
        }
        return null;
    }

    public void ReturnToMainMenu()
    {
        // Hide the panel instantly to avoid double-clicks or visual lingering
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        if (FusionLauncher.SessionOwner != null)
        {
            // Shuts the runner down, reloads the menu and cleans up the persistent launcher
            FusionLauncher.SessionOwner.LeaveSession();
        }
        else
        {
            SceneManager.LoadScene(0);
        }
    }

    public bool IsShowing => gameOverPanel != null && gameOverPanel.activeSelf;
}
