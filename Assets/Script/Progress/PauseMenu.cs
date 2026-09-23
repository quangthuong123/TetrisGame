using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Esc during a match. Single player: pauses the game, and SAVE & QUIT keeps the board for CONTINUE later.
// Online: the match can't pause, so it only offers LEAVE MATCH (the opponent wins).
// Closing the game window mid-game in single player also saves automatically.
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    public GameObject panel;
    public Button resumeButton;
    public Button saveQuitButton;
    public Button quitButton;
    public TMP_Text quitLabel;
    public TMP_Text hintText;

    private static bool SinglePlayer => FusionLauncher.SessionOwner != null && FusionLauncher.SessionOwner.IsSinglePlayer;

    void Awake()
    {
        if (resumeButton) resumeButton.onClick.AddListener(Close);
        if (saveQuitButton) saveQuitButton.onClick.AddListener(SaveAndQuit);
        if (quitButton) quitButton.onClick.AddListener(Quit);
        if (panel) panel.SetActive(false);
        IsPaused = false;
    }

    void OnDestroy() => IsPaused = false;

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (panel != null && panel.activeSelf) Close();
        else if (CanOpen()) Open();
    }

    // Not during the result screen, and not before a board exists
    private static bool CanOpen()
    {
        if (GameOverManager.Instance != null && GameOverManager.Instance.IsShowing) return false;
        return FindLocalBoard() != null;
    }

    public void Open()
    {
        if (panel == null) return;
        panel.SetActive(true);
        bool single = SinglePlayer;
        IsPaused = single; // Online matches keep running underneath
        if (saveQuitButton) saveQuitButton.gameObject.SetActive(single);
        if (quitLabel) quitLabel.text = single ? "QUIT (NO SAVE)" : "LEAVE MATCH";
        if (hintText) hintText.text = single ? "Game paused  ·  Esc to resume" : "The match is still running!  ·  Esc to go back";
    }

    public void Close()
    {
        if (panel) panel.SetActive(false);
        IsPaused = false;
    }

    public void SaveAndQuit()
    {
        SaveNow();
        LeaveToMenu();
    }

    public void Quit()
    {
        LeaveToMenu();
    }

    private void LeaveToMenu()
    {
        IsPaused = false;
        // Back to the Single Player screen (where CONTINUE is) or, online, the Multiplayer screen
        int menu = SinglePlayer ? FusionLauncher.MenuSinglePlayer : FusionLauncher.MenuMultiplayer;
        if (FusionLauncher.SessionOwner != null) FusionLauncher.SessionOwner.LeaveSession(menu);
        else UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

    void OnApplicationQuit()
    {
        // Closing the window mid-game keeps the game for CONTINUE
        if (SinglePlayer) SaveNow();
    }

    private static void SaveNow()
    {
        TetrisEngine player = FindLocalBoard();
        if (player == null || !SinglePlayer || player.IsGameOver || !player.IsInitialized) return;

        TetrisEngine ai = null;
        foreach (TetrisEngine board in FindObjectsByType<TetrisEngine>(FindObjectsSortMode.None))
        {
            if (board.aiController != null) ai = board;
        }
        if (ai != null && ai.IsGameOver) return; // Already decided

        ResumeStore.Save(new ResumeData
        {
            mode = GameModeSettings.Current,
            aiDifficulty = GameSettings.AIDifficulty,
            player = player.CaptureSnapshot(),
            ai = ai != null ? ai.CaptureSnapshot() : null,
        });
    }

    private static TetrisEngine FindLocalBoard()
    {
        foreach (TetrisEngine board in FindObjectsByType<TetrisEngine>(FindObjectsSortMode.None))
        {
            if (board.Object != null && board.Object.IsValid && board.Object.HasInputAuthority) return board;
        }
        return null;
    }
}
