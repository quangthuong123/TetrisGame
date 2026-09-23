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
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI highScoreText;

    void Awake()
    {
        // Initialize the Singleton when the scene starts
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        // Auto-disable the panel when the game starts
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    public void TriggerGameOver(int finalScore)
    {
        // 1. Show the panel
        if (gameOverPanel != null) gameOverPanel.SetActive(true);

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

        NetworkRunner runner = FindAnyObjectByType<NetworkRunner>();
        if (runner != null)
        {
            runner.Shutdown();
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        }
    }
}