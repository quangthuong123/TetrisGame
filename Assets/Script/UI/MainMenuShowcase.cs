using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The middle of the main menu: animated title, player card (name, level, XP, records),
// rotating tips, and neon tetrominoes drifting down in the background.
// Built by Tools > Tetris > Build Main Menu Showcase.
public class MainMenuShowcase : MonoBehaviour
{
    [Header("Title")]
    public TMP_Text title;
    public TMP_Text tagline;
    public Color titleTop = new Color(0.35f, 0.95f, 1f);
    public Color titleBottom = new Color(0.75f, 0.35f, 1f);

    [Header("Player card")]
    public TMP_Text greetingText;
    public TMP_InputField nameInput;
    public TMP_Text levelText;
    public Image xpFill;
    public TMP_Text bestScoreText;
    public TMP_Text bestSprintText;
    public TMP_Text bestUltraText;

    [Header("Tips")]
    public TMP_Text tipText;
    public float tipSeconds = 6f;

    [Header("Falling blocks")]
    public RectTransform fallArea;
    public Sprite blockSprite;
    public Color[] blockColors = new Color[0];
    public float cellSize = 40f;
    public float spawnEvery = 1.1f;
    public Vector2 fallSpeed = new Vector2(55f, 110f);
    [Range(0f, 1f)] public float blockAlpha = 0.35f;

    private readonly List<Piece> _pieces = new List<Piece>();
    private readonly Stack<Image> _pool = new Stack<Image>();
    private float _spawnTimer;
    private float _tipTimer;
    private int _tipIndex;

    private class Piece
    {
        public RectTransform root;
        public List<Image> cells = new List<Image>();
        public float speed;
        public float spin;
    }

    void OnEnable()
    {
        Refresh();
        _tipIndex = Random.Range(0, 100);
        ShowNextTip();
        if (nameInput != null)
        {
            nameInput.text = PlayerPrefs.GetString("PlayerName", "");
            nameInput.onEndEdit.RemoveListener(SaveName);
            nameInput.onEndEdit.AddListener(SaveName);
        }
    }

    void SaveName(string newName)
    {
        PlayerPrefs.SetString("PlayerName", newName.Trim());
        PlayerPrefs.Save();
        Refresh();
    }

    // Name, level and records (called when the menu opens and when the name changes)
    public void Refresh()
    {
        PlayerProfile p = ProfileStore.Profile;
        string playerName = PlayerPrefs.GetString("PlayerName", "").Trim();
        if (greetingText)
        {
            greetingText.text = playerName.Length == 0 ? "WELCOME, NEW PLAYER!"
                : p.gamesPlayed == 0 ? $"WELCOME, <color=#FFD84D>{playerName.ToUpper()}</color>!"
                : $"WELCOME BACK, <color=#FFD84D>{playerName.ToUpper()}</color>";
        }
        if (levelText) levelText.text = $"LEVEL {p.level}  ·  {ProfileStore.RankTitle(p.level).ToUpper()}  <size=70%><color=#9FCBFF>{p.xp} / {ProfileStore.XpToNextLevel(p.level)} XP</color></size>";
        if (xpFill) xpFill.fillAmount = ProfileStore.LevelProgress;

        float sprint = GameModeSettings.BestSprintTime;
        if (bestScoreText) bestScoreText.text = Tile("BEST SCORE", GameOverManager.SavedHighScore > 0 ? GameOverManager.SavedHighScore.ToString("N0") : "—");
        if (bestSprintText) bestSprintText.text = Tile("SPRINT 40L", sprint > 0f ? GameModeSettings.FormatTime(sprint) : "—");
        if (bestUltraText) bestUltraText.text = Tile("ULTRA 2:00", GameModeSettings.BestUltraScore > 0 ? GameModeSettings.BestUltraScore.ToString("N0") : "—");
    }

    private static string Tile(string label, string value) => $"<size=55%><color=#9FCBFF>{label}</color></size>\n{value}";

    void Update()
    {
        AnimateTitle();

        _tipTimer += Time.unscaledDeltaTime;
        if (_tipTimer >= tipSeconds) ShowNextTip();

        UpdateFallingBlocks(Time.unscaledDeltaTime);
    }

    // ---------- Title ----------

    void AnimateTitle()
    {
        if (title == null) return;
        float t = (Mathf.Sin(Time.unscaledTime * 1.6f) + 1f) * 0.5f;
        Color top = Color.Lerp(titleTop, Color.white, t * 0.35f);
        title.colorGradient = new VertexGradient(top, top, titleBottom, titleBottom);
        title.transform.localScale = Vector3.one * (1f + t * 0.02f);
    }

    // ---------- Tips ----------

    void ShowNextTip()
    {
        _tipTimer = 0f;
        if (tipText == null) return;
        string[] tips = Tips();
        _tipIndex = (_tipIndex + 1) % tips.Length;
        tipText.text = "<color=#FFD84D>TIP</color>  " + tips[_tipIndex];
    }

    static string[] Tips()
    {
        string K(GameAction a) => "<b>" + KeyBindings.KeyName(KeyBindings.Get(a)) + "</b>";
        return new[]
        {
            $"{K(GameAction.RotateClockwise)} / {K(GameAction.RotateCounterClockwise)} rotate both ways, {K(GameAction.Rotate180)} flips 180°.",
            $"Press {K(GameAction.Hold)} to hold a piece for later.",
            "T-spins send double garbage. Twist the T into tight gaps!",
            "Clearing lines cancels garbage coming at you first.",
            "Back-to-back Quads and T-spins send +1 extra garbage.",
            "Keep clearing on every piece to build a combo.",
            "Empty the whole board for an ALL CLEAR: +10 garbage!",
            "Press Esc in single player to pause, or SAVE & QUIT to continue later.",
            "Share a room code to play with a friend.",
            "Lower DAS and ARR in Settings for faster movement.",
            $"Skills: {K(GameAction.Skill1)} Block Breaker, {K(GameAction.Skill2)} Forced Z, {K(GameAction.Skill3)} X-Bomb.",
        };
    }

    // ---------- Falling blocks ----------

    static readonly Vector2Int[][] Shapes =
    {
        new[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(2,0), new Vector2Int(1,1) }, // T
        new[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(0,1), new Vector2Int(1,1) }, // O
        new[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(2,0), new Vector2Int(3,0) }, // I
        new[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(1,1), new Vector2Int(2,1) }, // S
        new[] { new Vector2Int(0,1), new Vector2Int(1,1), new Vector2Int(1,0), new Vector2Int(2,0) }, // Z
        new[] { new Vector2Int(0,0), new Vector2Int(0,1), new Vector2Int(1,0), new Vector2Int(2,0) }, // J
        new[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(2,0), new Vector2Int(2,1) }, // L
    };

    void UpdateFallingBlocks(float dt)
    {
        if (fallArea == null || blockSprite == null) return;
        Rect area = fallArea.rect;

        _spawnTimer -= dt;
        if (_spawnTimer <= 0f)
        {
            _spawnTimer = spawnEvery * Random.Range(0.6f, 1.4f);
            SpawnPiece(area);
        }

        for (int i = _pieces.Count - 1; i >= 0; i--)
        {
            Piece piece = _pieces[i];
            piece.root.anchoredPosition += Vector2.down * piece.speed * dt;
            piece.root.localRotation *= Quaternion.Euler(0f, 0f, piece.spin * dt);
            if (piece.root.anchoredPosition.y < area.yMin - cellSize * 4f)
            {
                foreach (Image cell in piece.cells)
                {
                    cell.gameObject.SetActive(false);
                    cell.transform.SetParent(fallArea, false);
                    _pool.Push(cell);
                }
                Destroy(piece.root.gameObject);
                _pieces.RemoveAt(i);
            }
        }
    }

    void SpawnPiece(Rect area)
    {
        var rootGo = new GameObject("Piece", typeof(RectTransform));
        var root = (RectTransform)rootGo.transform;
        root.SetParent(fallArea, false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = new Vector2(Random.Range(area.xMin + cellSize * 2f, area.xMax - cellSize * 2f), area.yMax + cellSize * 3f);
        float scale = Random.Range(0.7f, 1.3f);
        root.localScale = Vector3.one * scale;

        var piece = new Piece
        {
            root = root,
            speed = Random.Range(fallSpeed.x, fallSpeed.y) * scale, // Bigger pieces look closer, so they fall faster
            spin = Random.Range(-12f, 12f),
        };

        int shape = Random.Range(0, Shapes.Length);
        Color color = blockColors != null && blockColors.Length > 0 ? blockColors[Random.Range(0, blockColors.Length)] : Color.cyan;
        color.a = blockAlpha * Mathf.Lerp(0.6f, 1f, (scale - 0.7f) / 0.6f);
        foreach (Vector2Int c in Shapes[shape])
        {
            Image cell = _pool.Count > 0 ? _pool.Pop() : NewCell();
            cell.transform.SetParent(root, false);
            cell.rectTransform.anchoredPosition = new Vector2((c.x - 1f) * cellSize, (c.y - 0.5f) * cellSize);
            cell.color = color;
            cell.gameObject.SetActive(true);
            piece.cells.Add(cell);
        }
        _pieces.Add(piece);
    }

    Image NewCell()
    {
        var go = new GameObject("Cell", typeof(RectTransform), typeof(Image));
        Image image = go.GetComponent<Image>();
        image.sprite = blockSprite;
        image.raycastTarget = false;
        image.rectTransform.sizeDelta = new Vector2(cellSize, cellSize);
        return image;
    }
}
