using Fusion;
using UnityEngine;

public class TetrisEngine : NetworkBehaviour
{
    public const int Width = 10;
    public const int Height = 20;
    public const int MaxPieceBlocks = 5; // The X attack piece has 5 cells
    private const int XPieceID = 9;
    private const int GarbageID = 8;
    private const int MaxLockResets = 15;      // Moves/rotations on the ground that can delay locking
    private const int MaxPendingGarbage = 20;
    private const int MaxGarbagePerLock = 8;   // Extra pending garbage waits for the next lock
    private static readonly Vector2Int Hidden = new Vector2Int(-1000, -1000);

    // --- NETWORKED LOGIC DATA ---
    [Networked, Capacity(200)] public NetworkArray<int> NetworkGrid { get; }
    [Networked, Capacity(MaxPieceBlocks)] public NetworkArray<Vector2Int> ActivePiecePositions { get; }
    [Networked, Capacity(MaxPieceBlocks)] public NetworkArray<Vector2Int> GhostPiecePositions { get; }

    [Networked] public int SkillPoints { get; set; }
    [Networked] public int Score { get; set; }        // Never decreases (SkillPoints get spent)
    [Networked] public int LinesCleared { get; set; }
    [Networked] public NetworkBool IsWinner { get; set; }
    [Networked] public int CurrentPieceID { get; set; }
    [Networked] public bool IsInitialized { get; set; }
    [Networked] public bool IsGameOver { get; set; }

    // --- 7-Bag & Hold Data ---
    [Networked, Capacity(7)] public NetworkArray<int> Bag { get; }
    [Networked] public int BagIndex { get; set; }
    [Networked] public int HoldPieceID { get; set; }
    [Networked] public NetworkBool CanHold { get; set; }

    // --- Pro Timers (DAS, ARR, Lock Delay) ---
    [Networked] public float LockTimer { get; set; }
    [Networked] public int LockResets { get; set; }
    [Networked] public int LowestPieceY { get; set; } // Reaching a new lowest row refills the lock resets

    // --- Versus Garbage ---
    [Networked] public int PendingGarbage { get; set; } // Lines queued to rise on this board
    [Networked] public float DasLeftTimer { get; set; }
    [Networked] public float DasRightTimer { get; set; }

    // Skill & Queue Data
    [Networked] public int ForcedNextPiece { get; set; }
    [Networked] public int NextPieceID { get; set; }

    // --- VISUAL DATA ---
    [Header("Visuals")]
    public GameObject blockPrefab;
    public GameObject ghostPrefab;
    [Tooltip("0=Empty, 1-7=Shapes, 8=Garbage, 9=Special X")]
    public Color[] blockColors = new Color[10];
    public SpriteRenderer boardBackground;
    public Color myBoardColor = Color.white;
    public Color opponentBoardColor = new Color(0.4f, 0.4f, 0.4f, 1f);

    private bool _isSpawned = false;

    [Header("Incoming Garbage Meter")]
    [Tooltip("X position (board units) of the meter bar; -0.8 sits just left of the board")]
    public float garbageMeterOffsetX = -0.8f;
    public float garbageMeterWidth = 0.35f;
    public Color garbageMeterColor = new Color(1f, 0.2f, 0.2f, 1f);
    private Transform[] garbageMeterBlocks = new Transform[MaxPendingGarbage];

    [Header("UI Anchors")]
    public Transform nextPieceAnchor;
    public Transform holdPieceAnchor;

    [Header("Skill UI Images")]
    public UnityEngine.UI.Image skill1Icon;
    public UnityEngine.UI.Image skill2Icon;
    public UnityEngine.UI.Image skill3Icon;

    private bool hasSavedScore = false;

    private Transform[,] visualGrid = new Transform[Width, Height];
    private Transform[] activeVisualBlocks = new Transform[MaxPieceBlocks];
    private Transform[] ghostVisualBlocks = new Transform[MaxPieceBlocks];
    private Transform[] nextVisualBlocks = new Transform[MaxPieceBlocks];
    private Transform[] holdVisualBlocks = new Transform[MaxPieceBlocks];

    private Tetromino currentPiece;

    [Header("Game Speeds")]
    public float baseFallSpeed = 0.8f;
    public float softDropSpeed = 0.05f;
    public float speedDecreasePerSecond = 0.003f;
    private float minimumFallSpeed;

    [Networked] public float CurrentFallSpeed { get; set; }
    private float fallTimer;

    public override void Spawned()
    {
        _isSpawned = true;

        // Cap the maximum speed to exactly 2.5x the original speed
        minimumFallSpeed = baseFallSpeed / 2.5f;

        for (int i = 0; i < MaxPieceBlocks; i++)
        {
            activeVisualBlocks[i] = Instantiate(blockPrefab, transform).transform;
            ghostVisualBlocks[i] = Instantiate(ghostPrefab, transform).transform;
            nextVisualBlocks[i] = Instantiate(blockPrefab, transform).transform;
            holdVisualBlocks[i] = Instantiate(blockPrefab, transform).transform;

            activeVisualBlocks[i].position = new Vector3(-1000, -1000, 0);
            ghostVisualBlocks[i].position = new Vector3(-1000, -1000, 0);
            nextVisualBlocks[i].position = new Vector3(-1000, -1000, 0);
            holdVisualBlocks[i].position = new Vector3(-1000, -1000, 0);
        }

        for (int i = 0; i < MaxPendingGarbage; i++)
        {
            garbageMeterBlocks[i] = Instantiate(blockPrefab, transform).transform;
            garbageMeterBlocks[i].localScale = Vector3.Scale(garbageMeterBlocks[i].localScale, new Vector3(garbageMeterWidth, 1f, 1f));
            garbageMeterBlocks[i].position = new Vector3(-1000, -1000, 0);
        }

        if (boardBackground != null) boardBackground.color = HasInputAuthority ? myBoardColor : opponentBoardColor;
    }

    public override void FixedUpdateNetwork()
    {
        if (IsGameOver) return;

        if (HasStateAuthority && !IsInitialized)
        {
            IsInitialized = true;
            CanHold = true;
            CurrentFallSpeed = baseFallSpeed; // Set initial speed
            SpawnPiece();
        }

        // Missing input (e.g. a lagging client) must not freeze gravity, so it runs with nothing pressed
        if (GetInput(out TetrisInput input))
        {
            HandleDAS(input);

            if (input.SpacePressed) HardDrop();
            if (!IsGameOver && input.UpPressed) RotatePiece();
            if (!IsGameOver && input.HoldPressed) HoldCurrentPiece();
            if (input.Skill1Pressed) UseSkill(1);
            if (input.Skill2Pressed) UseSkill(2);
            if (input.Skill3Pressed) UseSkill(3);
        }

        if (HasStateAuthority && !IsGameOver && currentPiece != null)
        {
            // Safely decrease speed without dipping below the minimum
            if (CurrentFallSpeed > minimumFallSpeed)
            {
                CurrentFallSpeed = Mathf.Max(minimumFallSpeed, CurrentFallSpeed - (speedDecreasePerSecond * Runner.DeltaTime));
            }

            // Lock Delay (Coyote Time)
            bool isGrounded = !IsValidPosition(currentPiece.Position + new Vector2Int(0, -1), currentPiece.Shape);
            if (isGrounded)
            {
                LockTimer += Runner.DeltaTime;
                if (LockTimer >= 0.5f) LockPiece();
            }
            else
            {
                LockTimer = 0f;
                float currentSpeed = input.DownHeld ? softDropSpeed : CurrentFallSpeed;
                HandleGravity(currentSpeed);
            }
        }
    }

    // ==========================================
    // --- PRO CONTROLS (DAS & ARR) ---
    // ==========================================
    void HandleDAS(TetrisInput input)
    {
        float dasDelay = 0.15f;
        float arrSpeed = 0.05f;

        if (input.LeftHeld)
        {
            if (DasLeftTimer == 0f) TryMove(new Vector2Int(-1, 0));
            DasLeftTimer += Runner.DeltaTime;
            if (DasLeftTimer >= dasDelay)
            {
                TryMove(new Vector2Int(-1, 0));
                DasLeftTimer -= arrSpeed;
            }
        }
        else DasLeftTimer = 0f;

        if (input.RightHeld)
        {
            if (DasRightTimer == 0f) TryMove(new Vector2Int(1, 0));
            DasRightTimer += Runner.DeltaTime;
            if (DasRightTimer >= dasDelay)
            {
                TryMove(new Vector2Int(1, 0));
                DasRightTimer -= arrSpeed;
            }
        }
        else DasRightTimer = 0f;
    }

    // ==========================================
    // --- SERVER LOGIC ---
    // ==========================================
    void RefillBag()
    {
        int[] tempArr = { 1, 2, 3, 4, 5, 6, 7 };
        for (int i = 0; i < 7; i++)
        {
            int r = Random.Range(i, 7);
            int temp = tempArr[r];
            tempArr[r] = tempArr[i];
            tempArr[i] = temp;
            Bag.Set(i, tempArr[i]);
        }
        BagIndex = 0;
    }

    int GetNextPieceFromBag()
    {
        if (Bag[0] == 0 || BagIndex >= 7) RefillBag();
        int piece = Bag[BagIndex];
        BagIndex++;
        return piece;
    }

    void SpawnPiece()
    {
        if (!HasStateAuthority) return;

        if (NextPieceID == 0) NextPieceID = GetNextPieceFromBag();

        int[,] shape = null;

        if (ForcedNextPiece != 0)
        {
            if (ForcedNextPiece == 99)
            {
                shape = TetrominoShapes.X_Shape;
                CurrentPieceID = XPieceID;
            }
            else
            {
                shape = TetrominoShapes.AllShapes[ForcedNextPiece - 1];
                CurrentPieceID = ForcedNextPiece;
            }
            ForcedNextPiece = 0;
        }
        else
        {
            CurrentPieceID = NextPieceID;
            shape = TetrominoShapes.AllShapes[CurrentPieceID - 1];
            NextPieceID = GetNextPieceFromBag();
        }

        Vector2Int startPos = new Vector2Int(Width / 2 - shape.GetLength(0) / 2, Height - shape.GetLength(1));
        currentPiece = new Tetromino(shape, startPos);
        ResetLockDelay();

        if (!IsValidPosition(currentPiece.Position, currentPiece.Shape))
        {
            TopOut();
            return;
        }

        UpdateNetworkPiecePositions();
        UpdateGhostPositions();
    }

    // This board lost. In a versus match, every other board still playing wins.
    void TopOut()
    {
        if (!HasStateAuthority || IsGameOver) return;
        IsGameOver = true;

        foreach (TetrisEngine board in FindObjectsByType<TetrisEngine>(FindObjectsSortMode.None))
        {
            if (board != this && board.Object != null && !board.IsGameOver) board.DeclareWinner();
        }
    }

    // Also called by FusionLauncher when the opponent disconnects mid-match
    public void DeclareWinner()
    {
        if (!HasStateAuthority || IsGameOver) return;
        IsWinner = true;
        IsGameOver = true;
    }

    void HoldCurrentPiece()
    {
        // The X attack piece can't be held: it isn't in AllShapes, and holding would dodge the attack
        if (!HasStateAuthority || !CanHold || CurrentPieceID == XPieceID) return;

        CanHold = false;
        LockTimer = 0f;
        fallTimer = 0f;
        int temp = CurrentPieceID;

        if (HoldPieceID == 0)
        {
            HoldPieceID = temp;
            SpawnPiece();
        }
        else
        {
            CurrentPieceID = HoldPieceID;
            HoldPieceID = temp;
            int[,] shape = TetrominoShapes.AllShapes[CurrentPieceID - 1];
            Vector2Int startPos = new Vector2Int(Width / 2 - shape.GetLength(0) / 2, Height - shape.GetLength(1));
            currentPiece = new Tetromino(shape, startPos);
            ResetLockDelay();

            if (!IsValidPosition(currentPiece.Position, currentPiece.Shape))
            {
                TopOut();
                return;
            }

            UpdateNetworkPiecePositions();
            UpdateGhostPositions();
        }
    }

    void UpdateNetworkPiecePositions()
    {
        if (!HasStateAuthority || currentPiece == null) return;
        int blockIndex = 0;
        int size = currentPiece.Shape.GetLength(0);

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                if (currentPiece.Shape[x, y] != 0 && blockIndex < MaxPieceBlocks)
                {
                    int boardX = currentPiece.Position.x + x;
                    int boardY = currentPiece.Position.y + (size - 1 - y);
                    ActivePiecePositions.Set(blockIndex, new Vector2Int(boardX, boardY));
                    blockIndex++;
                }
            }
        }
        for (; blockIndex < MaxPieceBlocks; blockIndex++) ActivePiecePositions.Set(blockIndex, Hidden);
    }

    void UpdateGhostPositions()
    {
        if (!HasStateAuthority || currentPiece == null) return;
        Vector2Int simulatedPos = currentPiece.Position;

        while (IsValidPosition(simulatedPos + new Vector2Int(0, -1), currentPiece.Shape))
        {
            simulatedPos.y -= 1;
        }

        int blockIndex = 0;
        int size = currentPiece.Shape.GetLength(0);
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                if (currentPiece.Shape[x, y] != 0 && blockIndex < MaxPieceBlocks)
                {
                    int boardX = simulatedPos.x + x;
                    int boardY = simulatedPos.y + (size - 1 - y);
                    GhostPiecePositions.Set(blockIndex, new Vector2Int(boardX, boardY));
                    blockIndex++;
                }
            }
        }
        for (; blockIndex < MaxPieceBlocks; blockIndex++) GhostPiecePositions.Set(blockIndex, Hidden);
    }

    void HandleGravity(float currentSpeed)
    {
        fallTimer += Runner.DeltaTime;
        if (fallTimer >= currentSpeed)
        {
            fallTimer = 0f;
            TryMove(new Vector2Int(0, -1));
        }
    }

    void HardDrop()
    {
        if (!HasStateAuthority || currentPiece == null) return;
        while (TryMove(new Vector2Int(0, -1))) { }
        LockPiece();
    }

    // Lock delay "move reset": moving/rotating on the ground restarts the lock timer,
    // but only MaxLockResets times per row, so a piece can't be stalled forever
    void OnPieceMoved()
    {
        if (currentPiece.Position.y < LowestPieceY)
        {
            LowestPieceY = currentPiece.Position.y;
            LockResets = 0;
        }

        if (LockTimer > 0f && LockResets < MaxLockResets)
        {
            LockTimer = 0f;
            LockResets++;
        }
    }

    void ResetLockDelay()
    {
        LockTimer = 0f;
        LockResets = 0;
        LowestPieceY = currentPiece != null ? currentPiece.Position.y : Height;
    }

    bool TryMove(Vector2Int direction)
    {
        if (currentPiece == null) return false;
        Vector2Int newPos = currentPiece.Position + direction;
        if (IsValidPosition(newPos, currentPiece.Shape))
        {
            currentPiece.Position = newPos;
            if (HasStateAuthority)
            {
                OnPieceMoved();
                UpdateNetworkPiecePositions();
                UpdateGhostPositions();
            }
            return true;
        }
        return false;
    }

    // Offsets tried in order when a rotation is blocked by a wall, the floor or the stack
    private static readonly Vector2Int[] WallKicks =
    {
        new Vector2Int(0, 0), new Vector2Int(-1, 0), new Vector2Int(1, 0),
        new Vector2Int(0, 1), new Vector2Int(-2, 0), new Vector2Int(2, 0)
    };

    void RotatePiece()
    {
        if (currentPiece == null) return;
        int[,] oldShape = (int[,])currentPiece.Shape.Clone();
        Vector2Int oldPos = currentPiece.Position;
        currentPiece.Rotate();

        foreach (Vector2Int kick in WallKicks)
        {
            if (IsValidPosition(oldPos + kick, currentPiece.Shape))
            {
                currentPiece.Position = oldPos + kick;
                if (HasStateAuthority)
                {
                    OnPieceMoved();
                    UpdateNetworkPiecePositions();
                    UpdateGhostPositions();
                }
                return;
            }
        }

        currentPiece = new Tetromino(oldShape, oldPos);
    }

    bool IsValidPosition(Vector2Int targetPos, int[,] shape)
    {
        int size = shape.GetLength(0);
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                if (shape[x, y] == 0) continue;
                int boardX = targetPos.x + x;
                int boardY = targetPos.y + (size - 1 - y);

                if (boardX < 0 || boardX >= Width || boardY < 0) return false;
                if (boardY < Height && NetworkGrid[boardY * Width + boardX] != 0) return false;
            }
        }
        return true;
    }

    void LockPiece()
    {
        if (!HasStateAuthority || currentPiece == null || IsGameOver) return;
        int size = currentPiece.Shape.GetLength(0);
        bool lockedAboveBoard = false;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                if (currentPiece.Shape[x, y] != 0)
                {
                    int boardY = currentPiece.Position.y + (size - 1 - y);
                    if (boardY < Height)
                    {
                        int boardX = currentPiece.Position.x + x;
                        NetworkGrid.Set(boardY * Width + boardX, CurrentPieceID);
                    }
                    else lockedAboveBoard = true;
                }
            }
        }

        CanHold = true;
        LockTimer = 0f;
        int linesCleared = CheckForLines();

        // Lock out: part of the piece locked above the visible board
        if (lockedAboveBoard)
        {
            TopOut();
            return;
        }

        // Clearing lines attacks; placing a piece without clearing lets queued garbage rise
        if (linesCleared > 0) SendGarbage(GarbageForLines(linesCleared));
        else ApplyPendingGarbage();

        if (IsGameOver) return;
        SpawnPiece();
    }

    int CheckForLines()
    {
        int linesCleared = 0;
        for (int y = 0; y < Height; y++)
        {
            if (IsLineFull(y))
            {
                DeleteLineLogic(y);
                linesCleared++;
                y--; // Re-check the same row index since blocks dropped down
            }
        }

        if (linesCleared > 0)
        {
            int spReward = 0;
            if (linesCleared == 1) spReward = 100;
            else if (linesCleared == 2) spReward = 300;
            else if (linesCleared == 3) spReward = 600;
            else if (linesCleared == 4) spReward = 1200;

            SkillPoints += spReward;
            Score += spReward;
            LinesCleared += linesCleared;
        }
        return linesCleared;
    }

    bool IsLineFull(int y)
    {
        for (int x = 0; x < Width; x++)
        {
            if (NetworkGrid[y * Width + x] == 0) return false;
        }
        return true;
    }

    void DeleteLineLogic(int yTarget)
    {
        for (int y = yTarget; y < Height - 1; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                NetworkGrid.Set(y * Width + x, NetworkGrid[(y + 1) * Width + x]);
            }
        }
        for (int x = 0; x < Width; x++)
        {
            NetworkGrid.Set((Height - 1) * Width + x, 0);
        }
    }

    // ==========================================
    // --- VERSUS GARBAGE ---
    // ==========================================
    static int GarbageForLines(int lines)
    {
        if (lines == 2) return 1;
        if (lines == 3) return 2;
        if (lines >= 4) return 4; // Tetris
        return 0;
    }

    void SendGarbage(int lines)
    {
        // Your own clears cancel garbage queued against you before any is sent
        int cancelled = Mathf.Min(lines, PendingGarbage);
        PendingGarbage -= cancelled;
        lines -= cancelled;

        if (lines <= 0) return;
        TetrisEngine opponent = FindOpponent();
        if (opponent != null) opponent.ReceiveGarbage(lines);
    }

    public void ReceiveGarbage(int lines)
    {
        if (!HasStateAuthority || IsGameOver) return;
        PendingGarbage = Mathf.Min(MaxPendingGarbage, PendingGarbage + lines);
    }

    // Pushes the stack up and fills the bottom with garbage rows sharing one random hole
    void ApplyPendingGarbage()
    {
        int rows = Mathf.Min(PendingGarbage, MaxGarbagePerLock);
        if (rows <= 0) return;
        PendingGarbage -= rows;

        // Anything in the top rows gets pushed off the board
        bool overflow = false;
        for (int y = Height - rows; y < Height && !overflow; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (NetworkGrid[y * Width + x] != 0) { overflow = true; break; }
            }
        }

        for (int y = Height - 1; y >= rows; y--)
        {
            for (int x = 0; x < Width; x++)
            {
                NetworkGrid.Set(y * Width + x, NetworkGrid[(y - rows) * Width + x]);
            }
        }

        int hole = Random.Range(0, Width);
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                NetworkGrid.Set(y * Width + x, x == hole ? 0 : GarbageID);
            }
        }

        if (overflow) TopOut();
    }

    TetrisEngine FindOpponent()
    {
        foreach (TetrisEngine board in FindObjectsByType<TetrisEngine>(FindObjectsSortMode.None))
        {
            if (board != this && board.Object != null && board.Object.InputAuthority != Object.InputAuthority) return board;
        }
        return null;
    }

    // ==========================================
    // --- SKILL LOGIC ---
    // ==========================================
    void UseSkill(int tier)
    {
        if (!HasStateAuthority) return;
        int cost = tier == 1 ? 200 : (tier == 2 ? 600 : 1200);

        if (SkillPoints >= cost)
        {
            SkillPoints -= cost;
            AttackOpponent(tier);
        }
    }

    void AttackOpponent(int tier)
    {
        TetrisEngine board = FindOpponent();
        if (board == null) return;

        if (tier == 1) board.ReceiveBlockDelete();
        if (tier == 2) board.ReceiveForcedPiece(5);
        if (tier == 3) board.ReceiveForcedPiece(99);
    }

    public void ReceiveBlockDelete()
    {
        if (!HasStateAuthority) return;
        int[] filledIndices = new int[200];
        int count = 0;

        for (int i = 0; i < Width * Height; i++)
        {
            if (NetworkGrid[i] != 0)
            {
                filledIndices[count] = i;
                count++;
            }
        }

        if (count > 0)
        {
            int randomIndex = Random.Range(0, count);
            NetworkGrid.Set(filledIndices[randomIndex], 0);
        }
    }

    public void ReceiveForcedPiece(int pieceID)
    {
        if (!HasStateAuthority) return;
        ForcedNextPiece = pieceID;
    }

    // ==========================================
    // --- CLIENT VISUALS ---
    // ==========================================
    public override void Render()
    {
        // SAFETY GUARD: Prevent InvalidOperationException before Fusion finishes spawning
        if (!_isSpawned) return;

        Color currentPieceColor = Color.white;
        if (CurrentPieceID > 0 && CurrentPieceID < blockColors.Length)
        {
            currentPieceColor = blockColors[CurrentPieceID];
        }

        // 1. Draw Active Falling Piece
        for (int i = 0; i < MaxPieceBlocks; i++)
        {
            if (activeVisualBlocks[i] != null)
            {
                Vector2Int pos = ActivePiecePositions[i];
                activeVisualBlocks[i].position = transform.position + new Vector3(pos.x, pos.y, 0);
                activeVisualBlocks[i].GetComponent<SpriteRenderer>().color = currentPieceColor;
            }
        }

        // 2. Draw Locked Grid
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int blockID = NetworkGrid[y * Width + x];
                bool isLocked = blockID != 0;

                if (isLocked && visualGrid[x, y] == null)
                {
                    visualGrid[x, y] = Instantiate(blockPrefab, transform).transform;
                    visualGrid[x, y].position = transform.position + new Vector3(x, y, 0);
                    visualGrid[x, y].GetComponent<SpriteRenderer>().color = blockColors[blockID];
                }
                else if (!isLocked && visualGrid[x, y] != null)
                {
                    Destroy(visualGrid[x, y].gameObject);
                    visualGrid[x, y] = null;
                }
                else if (isLocked && visualGrid[x, y] != null)
                {
                    visualGrid[x, y].position = transform.position + new Vector3(x, y, 0);
                    visualGrid[x, y].GetComponent<SpriteRenderer>().color = blockColors[blockID];
                }
            }
        }

        // 3. Draw Ghost Piece
        if (HasInputAuthority)
        {
            Color ghostColor = currentPieceColor;
            ghostColor.a = 0.4f;

            for (int i = 0; i < MaxPieceBlocks; i++)
            {
                if (ghostVisualBlocks[i] != null)
                {
                    Vector2Int pos = GhostPiecePositions[i];
                    ghostVisualBlocks[i].position = transform.position + new Vector3(pos.x, pos.y, 0);
                    ghostVisualBlocks[i].GetComponent<SpriteRenderer>().color = ghostColor;
                }
            }
        }
        else
        {
            for (int i = 0; i < MaxPieceBlocks; i++) if (ghostVisualBlocks[i] != null) ghostVisualBlocks[i].position = new Vector3(-1000, -1000, 0);
        }

        // 3b. Incoming garbage meter: a red bar beside the board, one cell per queued line
        for (int i = 0; i < MaxPendingGarbage; i++)
        {
            if (garbageMeterBlocks[i] == null) continue;
            if (i < PendingGarbage)
            {
                garbageMeterBlocks[i].position = transform.position + new Vector3(garbageMeterOffsetX, i, 0);
                garbageMeterBlocks[i].GetComponent<SpriteRenderer>().color = garbageMeterColor;
            }
            else garbageMeterBlocks[i].position = new Vector3(-1000, -1000, 0);
        }

        // 4. Draw NEXT PIECE Shape
        int targetPieceToDisplay = ForcedNextPiece > 0 ? (ForcedNextPiece == 99 ? XPieceID : ForcedNextPiece) : NextPieceID;

        if (HasInputAuthority && nextPieceAnchor != null && targetPieceToDisplay > 0)
        {
            int[,] nextShape = (targetPieceToDisplay == XPieceID) ? TetrominoShapes.X_Shape : TetrominoShapes.AllShapes[targetPieceToDisplay - 1];
            Color nextColor = blockColors[targetPieceToDisplay];
            int size = nextShape.GetLength(0);
            int blockIndex = 0;

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    if (nextShape[x, y] != 0 && blockIndex < MaxPieceBlocks)
                    {
                        float offsetX = x - (size / 2f) + 0.5f;
                        float offsetY = (size - 1 - y) - (size / 2f) + 0.5f;
                        if (nextVisualBlocks[blockIndex] != null)
                        {
                            nextVisualBlocks[blockIndex].position = nextPieceAnchor.position + new Vector3(offsetX, offsetY, 0);
                            nextVisualBlocks[blockIndex].GetComponent<SpriteRenderer>().color = nextColor;
                        }
                        blockIndex++;
                    }
                }
            }
            // A 4-cell piece after a 5-cell X leaves one stale preview block
            for (; blockIndex < MaxPieceBlocks; blockIndex++) if (nextVisualBlocks[blockIndex] != null) nextVisualBlocks[blockIndex].position = new Vector3(-1000, -1000, 0);
        }
        else
        {
            for (int i = 0; i < MaxPieceBlocks; i++) if (nextVisualBlocks[i] != null) nextVisualBlocks[i].position = new Vector3(-1000, -1000, 0);
        }

        // 5. Draw HOLD PIECE Shape 
        if (HasInputAuthority && holdPieceAnchor != null && HoldPieceID > 0)
        {
            int[,] holdShape = TetrominoShapes.AllShapes[HoldPieceID - 1];
            Color holdColor = CanHold ? blockColors[HoldPieceID] : new Color(0.4f, 0.4f, 0.4f, 1f);
            int size = holdShape.GetLength(0);
            int blockIndex = 0;

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    if (holdShape[x, y] != 0 && blockIndex < MaxPieceBlocks)
                    {
                        float offsetX = x - (size / 2f) + 0.5f;
                        float offsetY = (size - 1 - y) - (size / 2f) + 0.5f;
                        if (holdVisualBlocks[blockIndex] != null)
                        {
                            holdVisualBlocks[blockIndex].position = holdPieceAnchor.position + new Vector3(offsetX, offsetY, 0);
                            holdVisualBlocks[blockIndex].GetComponent<SpriteRenderer>().color = holdColor;
                        }
                        blockIndex++;
                    }
                }
            }
        }
        else
        {
            for (int i = 0; i < MaxPieceBlocks; i++) if (holdVisualBlocks[i] != null) holdVisualBlocks[i].position = new Vector3(-1000, -1000, 0);
        }

        // 6. --- SKILL BUTTON FADING VISUALS ---
        if (HasInputAuthority)
        {
            if (skill1Icon != null)
            {
                Color c1 = skill1Icon.color;
                c1.a = SkillPoints >= 200 ? 1f : 0.3f;
                skill1Icon.color = c1;
            }
            if (skill2Icon != null)
            {
                Color c2 = skill2Icon.color;
                c2.a = SkillPoints >= 600 ? 1f : 0.3f;
                skill2Icon.color = c2;
            }
            if (skill3Icon != null)
            {
                Color c3 = skill3Icon.color;
                c3.a = SkillPoints >= 1200 ? 1f : 0.3f;
                skill3Icon.color = c3;
            }
        }

        // 7. --- GAME OVER & HIGH SCORE RESOLUTION ---
        if (HasInputAuthority && IsGameOver)
        {
            if (!hasSavedScore)
            {
                hasSavedScore = true;

                // Call the Scene's UI Manager via the Singleton!
                if (GameOverManager.Instance != null)
                {
                    bool isVersusMatch = FindObjectsByType<TetrisEngine>(FindObjectsSortMode.None).Length > 1;
                    GameOverManager.Instance.TriggerGameOver(Score, isVersusMatch, IsWinner);
                }
            }
        }
    }
}