using Fusion;
using UnityEngine;
using TMPro;

public class BoardUI : NetworkBehaviour
{
    [Header("References")]
    public TetrisEngine engine;
    public GameObject uiCanvasObject;

    [Header("Skill Icons & SP")]
    public TextMeshProUGUI spText;

    [Header("Score & High Score (all optional)")]
    public TextMeshProUGUI scoreText;
    [Tooltip("Shows the best score, counting up live once you pass it")]
    public TextMeshProUGUI highScoreText;
    public TextMeshProUGUI linesText;
    public Color newBestColor = Color.yellow;
    private int _savedHighScore;
    private Color _highScoreBaseColor;

    // Explicitly using UnityEngine.UI.Image to prevent ambiguity!
    public UnityEngine.UI.Image skill1Icon;
    public UnityEngine.UI.Image skill2Icon;
    public UnityEngine.UI.Image skill3Icon;

    [Header("Incoming Attack Warning")]
    public GameObject warningPanel;
    public TextMeshProUGUI warningText;

    [Header("Game Over UI")]
    public GameObject gameOverPanel;

    [Header("Colors")]
    public Color affordableColor = Color.white;
    public Color lockedColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);

    public override void Spawned()
    {
        if (engine == null) engine = GetComponentInParent<TetrisEngine>();
        if (uiCanvasObject == null) uiCanvasObject = gameObject;

        uiCanvasObject.SetActive(HasInputAuthority);

        _savedHighScore = GameOverManager.SavedHighScore;
        if (highScoreText != null) _highScoreBaseColor = highScoreText.color;
    }

    public override void Render()
    {
        if (engine == null) engine = GetComponentInParent<TetrisEngine>();
        if (!HasInputAuthority || engine == null) return;

        // 1. Update the SP Number
        if (spText != null) spText.text = "SP: " + engine.SkillPoints;

        // 1b. Score, lines and the live high score counter
        if (scoreText != null) scoreText.text = "SCORE: " + engine.Score;
        if (linesText != null) linesText.text = "LINES: " + engine.LinesCleared;
        if (highScoreText != null)
        {
            bool beatingBest = engine.Score > _savedHighScore;
            highScoreText.text = (beatingBest ? "NEW BEST: " : "BEST: ") + Mathf.Max(engine.Score, _savedHighScore);
            highScoreText.color = beatingBest ? newBestColor : _highScoreBaseColor;
        }

        // 2. Light up the Skill Icons based on the 3 Tiers
        if (skill1Icon != null) skill1Icon.color = engine.SkillPoints >= 200 ? affordableColor : lockedColor;
        if (skill2Icon != null) skill2Icon.color = engine.SkillPoints >= 600 ? affordableColor : lockedColor;
        if (skill3Icon != null) skill3Icon.color = engine.SkillPoints >= 1200 ? affordableColor : lockedColor;

        // 3. Show a warning if an attack is queued up!
        if (warningPanel != null)
        {
            if (engine.ForcedNextPiece > 0)
            {
                warningPanel.SetActive(true);

                if (warningText != null)
                {
                    if (engine.ForcedNextPiece == 99)
                        warningText.text = "DANGER: X-BLOCK INCOMING!";
                    else
                        warningText.text = "WARNING: FORCED PIECE!";
                }
            }
            else if (engine.PendingGarbage > 0)
            {
                warningPanel.SetActive(true);
                if (warningText != null) warningText.text = $"INCOMING: {engine.PendingGarbage} GARBAGE LINE{(engine.PendingGarbage > 1 ? "S" : "")}!";
            }
            else
            {
                warningPanel.SetActive(false);
            }
        }

        // 4. Toggle the Game Over Panel
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(engine.IsGameOver);
        }
    }
}