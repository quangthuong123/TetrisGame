using UnityEngine;
using TMPro;
using Fusion;
using UnityEngine.SceneManagement;

public class GameOverManager : MonoBehaviour
{
    // The Singleton instance that any script can find globally
    public static GameOverManager Instance;

    [Header("UI Elements")]
    public GameObject gameOverPanel;
    [Tooltip("Optional title: GAME OVER / YOU WIN! / YOU LOSE")]
    public TextMeshProUGUI resultText;
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI highScoreText;

    void Awake()
    {
        // Each gameplay scene load brings a fresh manager; the previous one was destroyed with its scene
        Instance = this;
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
            if (!isVersusMatch) resultText.text = "GAME OVER";
            else if (isWinner) resultText.text = "<color=green>YOU WIN!</color>";
            else resultText.text = "<color=red>YOU LOSE</color>";
        }

        // 2. Calculate High Score
        int savedHighScore = PlayerPrefs.GetInt("MyHighScore", 0);
        if (finalScore > savedHighScore)
        {
            savedHighScore = finalScore;
            PlayerPrefs.SetInt("MyHighScore", savedHighScore);
            PlayerPrefs.Save();
        }

        // 3. Update the Text
        if (finalScoreText != null) finalScoreText.text = "YOUR SCORE: " + finalScore;
        if (highScoreText != null) highScoreText.text = "HIGH SCORE: " + savedHighScore;
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
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        }
    }

    public bool IsShowing => gameOverPanel != null && gameOverPanel.activeSelf;
}