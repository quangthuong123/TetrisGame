using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Single-player opponent.
// PlaysOwnBoard: FusionLauncher spawns a second board next to yours and this AI plays real Tetris on it,
//                sending you garbage and using skills. First to top out loses.
// TimedAttacks:  the original mode - no board, random skill attacks on a timer that speeds up.
public class NPCAI : MonoBehaviour
{
    public enum Mode { PlaysOwnBoard, TimedAttacks }
    public enum Difficulty { Easy, Normal, Hard, Insane }

    public const string DifficultyPrefKey = "AIDifficulty";

    [Header("Mode")]
    public Mode mode = Mode.PlaysOwnBoard;
    public Difficulty difficulty = Difficulty.Normal;
    [Tooltip("Use the difficulty saved by the menu (FusionLauncher.SetAIDifficulty) when there is one")]
    public bool useSavedDifficulty = true;

    [Header("Board AI")]
    [Tooltip("The AI reacts this much faster each minute (0.1 = 10%)")]
    [Range(0f, 0.5f)] public float speedUpPerMinute = 0.1f;
    [Tooltip("Reaction times never drop below this fraction of the starting ones")]
    [Range(0.1f, 1f)] public float fastestSpeedFactor = 0.4f;

    [Header("Timed Attacks Mode")]
    public float firstAttackDelay = 20f;
    public float initialAttackInterval = 15f;
    public float minimumAttackInterval = 5f;
    public float difficultyIncrease = 0.5f;

    public bool PlaysOwnBoard => mode == Mode.PlaysOwnBoard;

    // Set from the difficulty
    private float _thinkTime;            // Pause after a new piece appears
    private float _actionInterval;       // Time between key presses
    private float _mistakeChance;        // Chance to pick a worse placement
    private float _skillChancePerSecond;
    private bool _useHardDrop;

    // Plan for the current piece
    private int _plannedSerial = -1;
    private bool _hasPlan;
    private bool _dropping;
    private int[,] _targetShape;
    private int _targetX;
    private int _rotatePresses;
    private int _movePresses;
    private float _actionTimer;
    private float _elapsed;

    private struct Placement
    {
        public int[,] Shape;
        public int X;
        public float Score;
    }

    void Awake()
    {
        if (useSavedDifficulty && PlayerPrefs.HasKey(DifficultyPrefKey))
        {
            difficulty = (Difficulty)Mathf.Clamp(PlayerPrefs.GetInt(DifficultyPrefKey), 0, 3);
        }
        ApplyDifficulty();
    }

    void Start()
    {
        // Sprint / Ultra are solo: no opponent, no attacks
        if (GameModeSettings.IsSolo) return;
        if (mode == Mode.TimedAttacks) StartCoroutine(AttackLoop());
    }

    void ApplyDifficulty()
    {
        switch (difficulty)
        {
            case Difficulty.Easy:
                _thinkTime = 0.9f; _actionInterval = 0.30f; _mistakeChance = 0.35f; _useHardDrop = false; _skillChancePerSecond = 0.03f;
                break;
            case Difficulty.Normal:
                _thinkTime = 0.45f; _actionInterval = 0.16f; _mistakeChance = 0.12f; _useHardDrop = true; _skillChancePerSecond = 0.06f;
                break;
            case Difficulty.Hard:
                _thinkTime = 0.2f; _actionInterval = 0.08f; _mistakeChance = 0.04f; _useHardDrop = true; _skillChancePerSecond = 0.12f;
                break;
            default: // Insane
                _thinkTime = 0.05f; _actionInterval = 0.035f; _mistakeChance = 0f; _useHardDrop = true; _skillChancePerSecond = 0.25f;
                break;
        }
    }

    float SpeedFactor => Mathf.Max(fastestSpeedFactor, 1f - speedUpPerMinute * (_elapsed / 60f));

    // ==========================================
    // --- BOARD AI: called by TetrisEngine every tick on the host ---
    // ==========================================
    public TetrisInput GetInput(TetrisEngine board, float deltaTime)
    {
        TetrisInput input = default;
        if (board.IsGameOver || board.CurrentShape == null) return input;
        _elapsed += deltaTime;

        if (board.PieceSerial != _plannedSerial)
        {
            _plannedSerial = board.PieceSerial;
            _hasPlan = PlanPlacement(board);
            _dropping = false;
            _rotatePresses = 0;
            _movePresses = 0;
            _actionTimer = _thinkTime * SpeedFactor;
        }

        MaybeUseSkill(board, deltaTime, ref input);

        if (_dropping)
        {
            input.DownHeld = true; // Easy AI soft drops instead of hard dropping
            return input;
        }
        if (!_hasPlan) return input;

        _actionTimer -= deltaTime;
        if (_actionTimer > 0f) return input;
        // At least one empty tick between presses, so each press is a fresh tap (no DAS auto-repeat)
        _actionTimer = Mathf.Max(_actionInterval * SpeedFactor, deltaTime * 1.5f);

        if (!SameShape(board.CurrentShape, _targetShape) && _rotatePresses < 4)
        {
            input.UpPressed = true;
            _rotatePresses++;
            return input;
        }

        int x = board.CurrentPosition.x;
        if (x != _targetX && _movePresses < TetrisEngine.Width + 4)
        {
            if (x < _targetX) input.RightHeld = true;
            else input.LeftHeld = true;
            _movePresses++;
            return input;
        }

        // In position (or stuck trying): drop it
        if (_useHardDrop) input.SpacePressed = true;
        else _dropping = true;
        _hasPlan = false;
        return input;
    }

    void MaybeUseSkill(TetrisEngine board, float deltaTime, ref TetrisInput input)
    {
        if (Random.value > _skillChancePerSecond * deltaTime) return;

        if (board.SkillPoints >= TetrisEngine.SkillCost(3)) input.Skill3Pressed = true;
        else if (board.SkillPoints >= TetrisEngine.SkillCost(2)) input.Skill2Pressed = true;
        else if (board.SkillPoints >= TetrisEngine.SkillCost(1)) input.Skill1Pressed = true;
    }

    // Tries every rotation and column, scores the resulting board, and picks the best
    bool PlanPlacement(TetrisEngine board)
    {
        int width = TetrisEngine.Width;
        int[] grid = new int[width * TetrisEngine.Height];
        for (int i = 0; i < grid.Length; i++) grid[i] = board.NetworkGrid[i];

        Vector2Int start = board.CurrentPosition;
        var options = new List<Placement>();
        var piece = new Tetromino(board.CurrentShape, start);

        for (int rotation = 0; rotation < 4; rotation++)
        {
            if (rotation > 0) piece.Rotate();
            int[,] shape = piece.Shape;
            int size = shape.GetLength(0);

            for (int x = -size; x <= width; x++)
            {
                if (!Fits(grid, shape, x, start.y) || !PathClear(grid, shape, start.x, x, start.y)) continue;

                int y = start.y;
                while (Fits(grid, shape, x, y - 1)) y--;
                options.Add(new Placement { Shape = shape, X = x, Score = Evaluate(grid, shape, x, y) });
            }
        }

        if (options.Count == 0) return false;
        options.Sort((a, b) => b.Score.CompareTo(a.Score));

        int pick = 0;
        if (options.Count > 1 && Random.value < _mistakeChance) pick = Random.Range(1, Mathf.Min(options.Count, 6));

        _targetShape = options[pick].Shape;
        _targetX = options[pick].X;
        return true;
    }

    // Same rules as TetrisEngine.IsValidPosition, on a copy of the grid
    static bool Fits(int[] grid, int[,] shape, int posX, int posY)
    {
        int size = shape.GetLength(0);
        for (int sx = 0; sx < size; sx++)
        {
            for (int sy = 0; sy < size; sy++)
            {
                if (shape[sx, sy] == 0) continue;
                int boardX = posX + sx;
                int boardY = posY + (size - 1 - sy);
                if (boardX < 0 || boardX >= TetrisEngine.Width || boardY < 0) return false;
                if (boardY < TetrisEngine.Height && grid[boardY * TetrisEngine.Width + boardX] != 0) return false;
            }
        }
        return true;
    }

    // Can the piece slide sideways at the top from its spawn column to the target column?
    static bool PathClear(int[] grid, int[,] shape, int fromX, int toX, int y)
    {
        int step = toX > fromX ? 1 : -1;
        for (int x = fromX; x != toX; x += step)
        {
            if (!Fits(grid, shape, x, y)) return false;
        }
        return true;
    }

    // Classic weighted heuristic: fewer holes, lower and flatter stacks, more cleared lines
    static float Evaluate(int[] grid, int[,] shape, int posX, int posY)
    {
        int width = TetrisEngine.Width;
        int height = TetrisEngine.Height;
        int[] board = (int[])grid.Clone();

        int size = shape.GetLength(0);
        for (int sx = 0; sx < size; sx++)
        {
            for (int sy = 0; sy < size; sy++)
            {
                if (shape[sx, sy] == 0) continue;
                int boardY = posY + (size - 1 - sy);
                if (boardY >= height) return -1000f; // Would lock out and lose
                board[boardY * width + posX + sx] = 1;
            }
        }

        // Remove full rows
        int lines = 0;
        int[] packed = new int[width * height];
        int row = 0;
        for (int y = 0; y < height; y++)
        {
            bool full = true;
            for (int x = 0; x < width; x++) if (board[y * width + x] == 0) { full = false; break; }
            if (full) { lines++; continue; }
            System.Array.Copy(board, y * width, packed, row * width, width);
            row++;
        }

        int aggregateHeight = 0, holes = 0, bumpiness = 0, previousHeight = -1;
        for (int x = 0; x < width; x++)
        {
            int columnHeight = 0;
            for (int y = height - 1; y >= 0; y--)
            {
                if (packed[y * width + x] != 0) { columnHeight = y + 1; break; }
            }
            for (int y = 0; y < columnHeight; y++)
            {
                if (packed[y * width + x] == 0) holes++;
            }

            aggregateHeight += columnHeight;
            if (previousHeight >= 0) bumpiness += Mathf.Abs(columnHeight - previousHeight);
            previousHeight = columnHeight;
        }

        return -0.510066f * aggregateHeight + 0.760666f * lines - 0.35663f * holes - 0.184483f * bumpiness;
    }

    static bool SameShape(int[,] a, int[,] b)
    {
        if (a == null || b == null || a.GetLength(0) != b.GetLength(0)) return false;
        int size = a.GetLength(0);
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                if (a[x, y] != b[x, y]) return false;
        return true;
    }

    // ==========================================
    // --- TIMED ATTACKS MODE (original behaviour) ---
    // ==========================================
    IEnumerator AttackLoop()
    {
        float currentInterval = initialAttackInterval;

        // Wait a few seconds for the board to spawn and the match to start
        yield return new WaitForSeconds(firstAttackDelay);

        while (true)
        {
            // Wait for the timer
            yield return new WaitForSeconds(currentInterval);

            // Find the player's board
            TetrisEngine playerBoard = Object.FindAnyObjectByType<TetrisEngine>();

            if (playerBoard != null && !playerBoard.IsGameOver)
            {
                // Pick a random attack (1 = Delete Block, 2 = Z-Piece, 3 = X-Piece)
                int randomAttack = Random.Range(1, 4);

                if (randomAttack == 1) playerBoard.ReceiveBlockDelete();
                else if (randomAttack == 2) playerBoard.ReceiveForcedPiece(5);
                else if (randomAttack == 3) playerBoard.ReceiveForcedPiece(99);

                Debug.Log("NPC attacked with tier: " + randomAttack);

                // Make the NPC attack faster next time (Survival Mode!)
                if (currentInterval > minimumAttackInterval)
                {
                    currentInterval -= difficultyIncrease;
                }
            }
        }
    }
}
