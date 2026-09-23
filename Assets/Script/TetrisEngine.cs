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
    [Networked] public TickTimer StartDelay { get; set; } // Both boards wait on this so everyone starts together

    // --- 7-Bag & Hold Data ---
    [Networked, Capacity(7)] public NetworkArray<int> Bag { get; }
    [Networked] public int BagIndex { get; set; }
    [Networked] public int HoldPieceID { get; set; }
    [Networked] public NetworkBool CanHold { get; set; }

    // --- Pro Timers (DAS, ARR, Lock Delay) ---
    [Networked] public float LockTimer { get; set; }
    [Networked] public int LockResets { get; set; }
    [Networked] public int LowestPieceY { get; set; } // Reaching a new lowest row refills the lock resets
    [Networked] public float DasLeftTimer { get; set; }
    [Networked] public float DasRightTimer { get; set; }

    // --- Versus Garbage ---
    [Networked] public int PendingGarbage { get; set; } // Lines queued to rise on this board

    // --- Sound Events ---
    // The host bumps a counter when something happens; the owning player's Render plays a sound
    // whenever a counter changes, so clients hear their own board too.
    [Networked] public int SfxMoves { get; set; }
    [Networked] public int SfxRotates { get; set; }
    [Networked] public int SfxHardDrops { get; set; }
    [Networked] public int SfxLocks { get; set; }
    [Networked] public int SfxHolds { get; set; }
    [Networked] public int SfxSkillsUsed { get; set; }
    [Networked] public int SfxAttacksReceived { get; set; }
    [Networked] public int SfxGarbageRisen { get; set; }

    // --- Scoring extras (combos, back-to-back, T-spins) ---
    [Networked] public int Combo { get; set; }             // -1 = no combo running; 0 = first clear, 1+ = combo
    [Networked] public NetworkBool BackToBack { get; set; } // Last clear was "difficult" (Quad or T-spin)
    [Networked] public int ClearEventCount { get; set; }    // Bumps on each notable clear (for the callout text)
    [Networked] public int ClearEventCode { get; set; }     // Packed details of that clear (see PackClear)

    // --- Solo modes (Sprint / Ultra) ---
    [Networked] public float ModeTime { get; set; }         // Seconds since the pieces started falling
    [Networked] public NetworkBool ModeCompleted { get; set; } // Reached the goal (40 lines / the 2 minutes ran out)

    // Host only: T-spin bookkeeping for the current piece
    private bool _lastActionWasRotation;
    private int _lastKickIndex;

    // --- Skill Alerts ---
    [Networked] public int SkillHitCount { get; set; } // Bumps each time an opponent's skill hits this board
    [Networked] public int LastSkillHit { get; set; }  // Tier (1-3) of that skill

    // --- AI ---
    // Set on the host for a board NPCAI plays instead of a person (single player opponent)
    [HideInInspector] public NPCAI aiController;
    public int PieceSerial { get; private set; } // Host only: bumps whenever a new piece becomes active
    public int[,] CurrentShape => currentPiece?.Shape;
    public Vector2Int CurrentPosition => currentPiece != null ? currentPiece.Position : Vector2Int.zero;

    // Skill & Queue Data
    [Networked] public int ForcedNextPiece { get; set; }
    [Networked] public int NextPieceID { get; set; }

    // --- VISUAL DATA ---
    [Header("Visuals")]
    public GameObject blockPrefab;
    public GameObject ghostPrefab;
    [Range(0f, 1f)] public float ghostAlpha = 0.6f;
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


    private bool hasSavedScore = false;

    private Transform[,] visualGrid = new Transform[Width, Height];
    private Transform[] activeVisualBlocks = new Transform[MaxPieceBlocks];
    private Transform[] ghostVisualBlocks = new Transform[MaxPieceBlocks];
    private Transform[] nextVisualBlocks = new Transform[MaxPieceBlocks];
    private Transform[] holdVisualBlocks = new Transform[MaxPieceBlocks];

    private Tetromino currentPiece;

    [Header("Match Start")]
    [Tooltip("Countdown before pieces start falling, so a client still loading the scene isn't behind")]
    public float startDelaySeconds = 3f;

    // Seconds left before the match starts (0 once it has started)
    public float StartCountdown => IsInitialized ? 0f : (StartDelay.RemainingTime(Runner) ?? 0f);

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

        PlaceOnThisScreen();

        if (HasStateAuthority && !IsInitialized)
        {
            StartDelay = TickTimer.CreateFromSeconds(Runner, startDelaySeconds);
            Combo = -1;
            // Nothing to draw until the first piece spawns (default (0,0) would show blocks in the corner)
            for (int i = 0; i < MaxPieceBlocks; i++)
            {
                ActivePiecePositions.Set(i, Hidden);
                GhostPiecePositions.Set(i, Hidden);
            }
        }
    }

    // Spawn positions aren't networked (the board has no NetworkTransform), so each screen places
    // the boards itself: your own board in the player spot, the other one in the opponent spot.
    void PlaceOnThisScreen()
    {
        Transform spot = FindSpawnPoint(HasInputAuthority);
        if (spot != null) transform.position = spot.position;
    }

    // Spawn points are only position markers, so inactive ones count too
    // (GameObject.Find skips inactive objects, which left the AI board on top of yours)
    public static Transform FindSpawnPoint(bool ownBoard)
    {
        string[] names = ownBoard
            ? new[] { "SpawnPoint Player", "SpawnPoint_P1" }
            : new[] { "SpawnPoint Opp", "SpawnPoint_P2" };

        GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (string spotName in names)
        {
            foreach (GameObject root in roots)
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == spotName) return t;
                }
            }
        }
        return null;
    }

    // A player who disconnects just stops; the remaining board is declared the winner separately
    public void Forfeit()
    {
        if (!HasStateAuthority || IsGameOver) return;
        IsGameOver = true;
    }

    public override void FixedUpdateNetwork()
    {
        if (IsGameOver) return;
        if (!IsInitialized && !StartDelay.ExpiredOrNotRunning(Runner)) return;

        if (HasStateAuthority && !IsInitialized)
        {
            IsInitialized = true;
            CanHold = true;
            CurrentFallSpeed = baseFallSpeed; // Set initial speed
            SpawnPiece();
        }

        // Missing input (e.g. a lagging client) must not freeze gravity, so it runs with nothing pressed
        bool hasInput = GetInput(out TetrisInput input);
        if (!hasInput && aiController != null && HasStateAuthority)
        {
            input = aiController.GetInput(this, Runner.DeltaTime);
            hasInput = true;
        }

        if (hasInput)
        {
            HandleDAS(input);

            if (input.SpacePressed) HardDrop();
            if (!IsGameOver && input.UpPressed) RotatePiece(1);
            if (!IsGameOver && input.RotateCCWPressed) RotatePiece(-1);
            if (!IsGameOver && input.Rotate180Pressed) RotatePiece(2);
            if (!IsGameOver && input.HoldPressed) HoldCurrentPiece();
            if (input.Skill1Pressed) UseSkill(1);
            if (input.Skill2Pressed) UseSkill(2);
            if (input.Skill3Pressed) UseSkill(3);
        }

        // Sprint / Ultra clock (Ultra ends when it runs out)
        if (HasStateAuthority && !IsGameOver && GameModeSettings.IsSolo)
        {
            ModeTime += Runner.DeltaTime;
            if (GameModeSettings.Current == MatchMode.Ultra && ModeTime >= GameModeSettings.UltraSeconds)
            {
                ModeTime = GameModeSettings.UltraSeconds;
                FinishMode();
            }
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
        // Each player's own handling from their Settings (the AI and old clients use the defaults)
        float dasDelay = input.HasHandling ? input.DasMs / 1000f : GameSettings.DefaultDasMs / 1000f;
        float arrSpeed = input.HasHandling ? input.ArrMs / 1000f : GameSettings.DefaultArrMs / 1000f;

        if (input.LeftHeld) DasLeftTimer = HandleHeldDirection(DasLeftTimer, new Vector2Int(-1, 0), dasDelay, arrSpeed);
        else DasLeftTimer = 0f;

        if (input.RightHeld) DasRightTimer = HandleHeldDirection(DasRightTimer, new Vector2Int(1, 0), dasDelay, arrSpeed);
        else DasRightTimer = 0f;
    }

    // Returns the updated DAS timer for one held direction
    float HandleHeldDirection(float timer, Vector2Int direction, float dasDelay, float arrSpeed)
    {
        if (timer == 0f) PlayerMove(direction);
        timer += Runner.DeltaTime;
        if (timer >= dasDelay)
        {
            if (arrSpeed <= 0f)
            {
                // ARR 0: slide straight to the wall
                bool moved = false;
                while (TryMove(direction)) moved = true;
                if (moved && HasStateAuthority) SfxMoves++;
            }
            else
            {
                PlayerMove(direction);
                timer -= arrSpeed;
            }
        }
        return timer;
    }

    // Sideways moves made by the player (gravity also uses TryMove, but shouldn't click)
    void PlayerMove(Vector2Int direction)
    {
        if (TryMove(direction) && HasStateAuthority) SfxMoves++;
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

        Vector2Int startPos = SpawnPosition(shape);
        currentPiece = new Tetromino(shape, startPos);
        OnNewActivePiece();

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
        SfxHolds++;
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
            Vector2Int startPos = SpawnPosition(shape);
            currentPiece = new Tetromino(shape, startPos);
            OnNewActivePiece();

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
        SfxHardDrops++;
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

    void OnNewActivePiece()
    {
        PieceSerial++; // Tells the AI to plan a new placement
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
            _lastActionWasRotation = false; // A T-spin needs the rotation to be the last move
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

    // SRS rotation. turn: 1 = clockwise, -1 = counter-clockwise, 2 = 180.
    // Tries the SRS wall-kick offsets in order and keeps the first position that fits.
    void RotatePiece(int turn)
    {
        if (currentPiece == null) return;
        int size = currentPiece.Shape.GetLength(0);
        if (size < 3) return; // The O piece doesn't rotate

        int from = currentPiece.Rotation;
        int to = from + turn;
        int[,] rotated = turn == -1
            ? Tetromino.RotatedCounterClockwise(currentPiece.Shape)
            : Tetromino.RotatedClockwise(currentPiece.Shape);
        if (turn == 2) rotated = Tetromino.RotatedClockwise(rotated);

        Vector2Int[] kicks = SrsKicks.For(size == 4, from, to);
        for (int i = 0; i < kicks.Length; i++)
        {
            Vector2Int target = currentPiece.Position + kicks[i];
            if (!IsValidPosition(target, rotated)) continue;

            currentPiece.SetState(rotated, to);
            currentPiece.Position = target;
            _lastActionWasRotation = true;
            _lastKickIndex = i;
            if (HasStateAuthority)
            {
                SfxRotates++;
                OnPieceMoved();
                UpdateNetworkPiecePositions();
                UpdateGhostPositions();
            }
            return;
        }
    }

    // ---------- T-spin detection (3-corner rule) ----------
    // Returns 0 = none, 1 = mini, 2 = full T-spin. Checked just before the piece locks.
    int DetectTSpin()
    {
        if (CurrentPieceID != TetrominoShapes.T_ID || !_lastActionWasRotation || currentPiece == null) return 0;

        Vector2Int p = currentPiece.Position; // Bottom-left of the T's 3x3 box (y up)
        bool topLeft = CellBlocked(p.x, p.y + 2), topRight = CellBlocked(p.x + 2, p.y + 2);
        bool bottomLeft = CellBlocked(p.x, p.y), bottomRight = CellBlocked(p.x + 2, p.y);
        int corners = (topLeft ? 1 : 0) + (topRight ? 1 : 0) + (bottomLeft ? 1 : 0) + (bottomRight ? 1 : 0);
        if (corners < 3) return 0;

        // The two corners on the side the T points to
        bool frontA, frontB;
        switch (currentPiece.Rotation)
        {
            case 0: frontA = topLeft; frontB = topRight; break;       // Pointing up
            case 1: frontA = topRight; frontB = bottomRight; break;   // Pointing right
            case 2: frontA = bottomLeft; frontB = bottomRight; break; // Pointing down
            default: frontA = topLeft; frontB = bottomLeft; break;    // Pointing left
        }
        if (frontA && frontB) return 2;
        return _lastKickIndex == 4 ? 2 : 1; // The last SRS kick (the "TST" kick) always counts as a full T-spin
    }

    // Walls and floor count as blocked; above the board counts as open
    bool CellBlocked(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0) return true;
        if (y >= Height) return false;
        return NetworkGrid[y * Width + x] != 0;
    }

    // SRS spawn columns: 3-wide pieces at 3-5, I at 3-6, O at 4-5, in the top rows
    static Vector2Int SpawnPosition(int[,] shape)
    {
        int size = shape.GetLength(0);
        return new Vector2Int((Width - size) / 2, Height - size);
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
        int tSpin = DetectTSpin(); // Before the piece is written into the grid
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
        SfxLocks++;
        int linesCleared = CheckForLines();
        int attack = ScoreClear(linesCleared, tSpin);

        // Lock out: part of the piece locked above the visible board
        if (lockedAboveBoard)
        {
            TopOut();
            return;
        }

        // Sprint: done at 40 lines
        if (GameModeSettings.Current == MatchMode.Sprint && LinesCleared >= GameModeSettings.SprintLines)
        {
            FinishMode();
            return;
        }

        // Clearing lines attacks; placing a piece without clearing lets queued garbage rise
        if (linesCleared > 0) SendGarbage(attack);
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
        LinesCleared += linesCleared;
        return linesCleared;
    }

    // ==========================================
    // --- SCORING: T-SPINS, COMBOS, BACK-TO-BACK, ALL CLEAR ---
    // ==========================================
    // Points (and the same amount of SP) for each kind of clear, by lines 0-4
    private static readonly int[] NormalPoints = { 0, 100, 300, 600, 1200 };
    private static readonly int[] TSpinPoints = { 400, 800, 1200, 1600, 1600 };
    private static readonly int[] TSpinMiniPoints = { 100, 200, 400, 400, 400 };
    private const int ComboPointsPerStep = 50;
    private const int AllClearPoints = 2000;

    // Garbage lines sent, by lines 0-4
    private static readonly int[] NormalAttack = { 0, 0, 1, 2, 4 };
    private static readonly int[] TSpinAttack = { 0, 2, 4, 6, 6 };
    private static readonly int[] TSpinMiniAttack = { 0, 0, 1, 1, 1 };
    private static readonly int[] ComboAttack = { 0, 0, 1, 1, 1, 2, 2, 3, 3, 4, 4, 4, 5 }; // By combo count
    private const int AllClearAttack = 10;

    // Updates combo / back-to-back, awards points and returns the garbage to send
    int ScoreClear(int lines, int tSpin)
    {
        bool isTSpin = tSpin > 0;
        if (lines == 0 && !isTSpin)
        {
            Combo = -1; // Placing a piece without clearing ends the combo
            return 0;
        }

        bool allClear = lines > 0 && BoardIsEmpty();
        bool difficult = lines == 4 || (isTSpin && lines > 0);
        bool backToBackBonus = false;
        if (lines > 0)
        {
            backToBackBonus = difficult && BackToBack;
            BackToBack = difficult;
            Combo++;
        }
        // (A T-spin with no lines keeps both the combo and back-to-back as they were)

        int[] pointTable = tSpin == 2 ? TSpinPoints : tSpin == 1 ? TSpinMiniPoints : NormalPoints;
        int points = pointTable[Mathf.Min(lines, 4)];
        if (backToBackBonus) points = points * 3 / 2;
        if (lines > 0 && Combo > 0) points += ComboPointsPerStep * Combo;
        if (allClear) points += AllClearPoints;
        SkillPoints += points;
        Score += points;

        int attack = 0;
        if (lines > 0)
        {
            int[] attackTable = tSpin == 2 ? TSpinAttack : tSpin == 1 ? TSpinMiniAttack : NormalAttack;
            attack = attackTable[Mathf.Min(lines, 4)];
            if (backToBackBonus) attack += 1;
            attack += ComboAttack[Mathf.Clamp(Combo, 0, ComboAttack.Length - 1)];
            if (allClear) attack += AllClearAttack;
        }

        // Callout text on the board ("T-SPIN DOUBLE", "BACK-TO-BACK", "3 COMBO", "ALL CLEAR!")
        ClearEventCode = PackClear(lines, tSpin, backToBackBonus, lines > 0 ? Combo : -1, allClear);
        ClearEventCount++;
        return attack;
    }

    bool BoardIsEmpty()
    {
        for (int i = 0; i < Width * Height; i++) if (NetworkGrid[i] != 0) return false;
        return true;
    }

    // bits 0-2 lines, 3-4 T-spin, 5 back-to-back, 6 all clear, 8+ combo (+1 so -1 packs as 0)
    static int PackClear(int lines, int tSpin, bool b2b, int combo, bool allClear) =>
        lines | (tSpin << 3) | ((b2b ? 1 : 0) << 5) | ((allClear ? 1 : 0) << 6) | ((combo + 1) << 8);

    public static string DescribeClear(int code, out string subLine)
    {
        int lines = code & 7, tSpin = (code >> 3) & 3, combo = (code >> 8) - 1;
        bool b2b = ((code >> 5) & 1) == 1, allClear = ((code >> 6) & 1) == 1;

        string[] lineNames = { "", "SINGLE", "DOUBLE", "TRIPLE", "QUAD" };
        string main = tSpin == 2 ? "T-SPIN " + lineNames[lines] : tSpin == 1 ? "T-SPIN MINI " + lineNames[lines] : lines >= 3 ? lineNames[lines] : "";
        if (allClear) main = "ALL CLEAR!";

        var extras = new System.Collections.Generic.List<string>();
        if (b2b) extras.Add("BACK-TO-BACK");
        if (combo >= 1) extras.Add(combo + " COMBO");
        subLine = string.Join("  ", extras);
        return main.Trim();
    }

    // Sprint reached 40 lines, or Ultra's 2 minutes ran out: the run ends as a success
    void FinishMode()
    {
        if (!HasStateAuthority || IsGameOver) return;
        ModeCompleted = true;
        IsWinner = true;
        IsGameOver = true;
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
        SfxAttacksReceived++;
    }

    // Pushes the stack up and fills the bottom with garbage rows sharing one random hole
    void ApplyPendingGarbage()
    {
        int rows = Mathf.Min(PendingGarbage, MaxGarbagePerLock);
        if (rows <= 0) return;
        PendingGarbage -= rows;
        SfxGarbageRisen++;

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
        int cost = SkillCost(tier);

        // No opponent (Sprint / Ultra): don't burn SP on a skill that hits nobody
        if (SkillPoints >= cost && FindOpponent() != null)
        {
            SkillPoints -= cost;
            SfxSkillsUsed++;
            AttackOpponent(tier);
        }
    }

    public static int SkillCost(int tier) => tier == 1 ? 200 : (tier == 2 ? 600 : 1200);

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
        SfxAttacksReceived++;
        RecordSkillHit(1);
    }

    public void ReceiveForcedPiece(int pieceID)
    {
        if (!HasStateAuthority) return;
        ForcedNextPiece = pieceID;
        SfxAttacksReceived++;
        RecordSkillHit(SkillInfo.TierForForcedPiece(pieceID));
    }

    void RecordSkillHit(int tier)
    {
        LastSkillHit = tier;
        SkillHitCount++;
    }

    // ==========================================
    // --- CLIENT SOUNDS ---
    // ==========================================
    private bool _soundsPrimed;
    private int _heardMoves, _heardRotates, _heardHardDrops, _heardLocks, _heardHolds;
    private int _heardSkills, _heardAttacks, _heardGarbage, _heardLines;

    void PlayBoardSounds()
    {
        if (_soundsPrimed)
        {
            if (SfxMoves != _heardMoves) AudioManager.Play(Sfx.Move);
            if (SfxRotates != _heardRotates) AudioManager.Play(Sfx.Rotate);
            if (SfxHolds != _heardHolds) AudioManager.Play(Sfx.Hold);

            // A hard drop also locks: play just the heavier sound
            if (SfxHardDrops != _heardHardDrops) AudioManager.Play(Sfx.HardDrop);
            else if (SfxLocks != _heardLocks) AudioManager.Play(Sfx.Lock);

            int newLines = LinesCleared - _heardLines;
            if (newLines >= 4) AudioManager.Play(Sfx.Tetris);
            else if (newLines > 0) AudioManager.Play(Sfx.LineClear);

            if (SfxSkillsUsed != _heardSkills) AudioManager.Play(Sfx.SkillUsed);
            if (SfxAttacksReceived != _heardAttacks) AudioManager.Play(Sfx.AttackReceived);
            if (SfxGarbageRisen != _heardGarbage) AudioManager.Play(Sfx.GarbageRise);
        }

        _soundsPrimed = true;
        _heardMoves = SfxMoves;
        _heardRotates = SfxRotates;
        _heardHardDrops = SfxHardDrops;
        _heardLocks = SfxLocks;
        _heardHolds = SfxHolds;
        _heardLines = LinesCleared;
        _heardSkills = SfxSkillsUsed;
        _heardAttacks = SfxAttacksReceived;
        _heardGarbage = SfxGarbageRisen;
    }

    // ==========================================
    // --- CLIENT VISUALS ---
    // ==========================================
    public override void Render()
    {
        // SAFETY GUARD: Prevent InvalidOperationException before Fusion finishes spawning
        if (!_isSpawned) return;

        if (HasInputAuthority)
        {
            PlayBoardSounds();
            GameOverManager.LastKnownScore = Score; // Still shown if the connection drops mid-match
        }

        UpdateClearCallout();
        UpdateModeHud();

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

        // 3. Draw Ghost Piece (can be turned off in Settings)
        if (HasInputAuthority && GameSettings.ShowGhost)
        {
            Color ghostColor = currentPieceColor;
            ghostColor.a = ghostAlpha;

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

        // 6. Skill icons are lit/dimmed by BoardUI

        // 7. --- GAME OVER & HIGH SCORE RESOLUTION ---
        if (HasInputAuthority && IsGameOver)
        {
            if (!hasSavedScore)
            {
                hasSavedScore = true;

                // Call the Scene's UI Manager via the Singleton!
                if (GameOverManager.Instance != null)
                {
                    if (GameModeSettings.IsSolo)
                    {
                        GameOverManager.Instance.TriggerModeResult(GameModeSettings.Current, ModeCompleted, ModeTime, Score, LinesCleared);
                    }
                    else
                    {
                        bool isVersusMatch = FindObjectsByType<TetrisEngine>(FindObjectsSortMode.None).Length > 1;
                        GameOverManager.Instance.TriggerGameOver(Score, isVersusMatch, IsWinner);
                    }
                }
            }
        }
    }

    // ==========================================
    // --- CALLOUTS & MODE HUD (world-space text, created on demand) ---
    // ==========================================
    [Header("Callout Text")]
    public float calloutSeconds = 1.6f;
    public Color calloutColor = new Color(1f, 0.85f, 0.3f, 1f);

    private TMPro.TextMeshPro _callout;
    private TMPro.TextMeshPro _modeHud;
    private int _seenClearEvents = -1;
    private float _calloutTimeLeft;

    void UpdateClearCallout()
    {
        if (_seenClearEvents < 0) { _seenClearEvents = ClearEventCount; return; }

        if (ClearEventCount != _seenClearEvents)
        {
            _seenClearEvents = ClearEventCount;
            string main = DescribeClear(ClearEventCode, out string sub);
            if (main.Length > 0 || sub.Length > 0)
            {
                if (_callout == null) _callout = CreateWorldText("ClearCallout", new Vector3(4.5f, 21.9f, 0f), new Vector2(11f, 2.6f), TMPro.TextAlignmentOptions.Bottom);
                _callout.text = (main.Length > 0 ? $"<b>{main}</b>" : "") + (sub.Length > 0 ? $"\n<size=70%>{sub}</size>" : "");
                _calloutTimeLeft = calloutSeconds;
            }
        }

        if (_callout == null) return;
        _calloutTimeLeft -= Time.deltaTime;
        Color c = calloutColor;
        c.a = Mathf.Clamp01(_calloutTimeLeft / 0.4f); // Fades out over the last 0.4 s
        _callout.color = c;
    }

    void UpdateModeHud()
    {
        if (!HasInputAuthority || !GameModeSettings.IsSolo) return;
        if (_modeHud == null)
        {
            _modeHud = CreateWorldText("ModeHud", new Vector3(-3.6f, 15f, 0f), new Vector2(5.5f, 5f), TMPro.TextAlignmentOptions.TopRight);
            _modeHud.color = Color.white;
        }

        string title = GameModeSettings.DisplayName(GameModeSettings.Current);
        if (GameModeSettings.Current == MatchMode.Sprint)
        {
            int left = Mathf.Max(0, GameModeSettings.SprintLines - LinesCleared);
            _modeHud.text = $"<b>{title}</b>\n<size=80%>LINES LEFT</size>\n<b>{left}</b>\n<size=80%>TIME</size>\n<b>{GameModeSettings.FormatTime(ModeTime)}</b>";
        }
        else
        {
            float remaining = Mathf.Max(0f, GameModeSettings.UltraSeconds - ModeTime);
            _modeHud.text = $"<b>{title}</b>\n<size=80%>TIME LEFT</size>\n<b>{GameModeSettings.FormatTime(remaining)}</b>\n<size=80%>SCORE</size>\n<b>{Score}</b>";
        }
    }

    TMPro.TextMeshPro CreateWorldText(string objectName, Vector3 localPosition, Vector2 size, TMPro.TextAlignmentOptions alignment)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPosition;
        TMPro.TextMeshPro text = go.AddComponent<TMPro.TextMeshPro>();
        text.rectTransform.sizeDelta = size;
        text.alignment = alignment;
        text.enableAutoSizing = true;
        text.fontSizeMin = 3f;
        text.fontSizeMax = 9f;
        text.color = calloutColor;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(20, 10, 50, 255);
        text.sortingOrder = 20; // In front of the board and blocks
        return text;
    }
}