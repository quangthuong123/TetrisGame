using Fusion;
using Fusion.Sockets;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

public class FusionLauncher : MonoBehaviour, INetworkRunnerCallbacks
{
    private const int MenuSceneBuildIndex = 0;
    private const int MultiplayerSceneBuildIndex = 1;
    private const int SinglePlayerSceneBuildIndex = 2;
    private NetworkRunner _runner;

    // The launcher running the current session (it survives scene loads)
    public static FusionLauncher SessionOwner { get; private set; }
    private bool _returningToMenu = false;
    public NetworkPrefabRef playerBoardPrefab;

    [Header("Spawn Locations")]
    public Transform[] spawnPoints;

    [Header("UI Panels")]
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;
    public GameObject modeSelectPanel;
    public GameObject matchLobbyPanel;

    [Header("Menu Features")]
    public TMP_InputField nameInputField;
    public Slider volumeSlider;
    [Tooltip("Optional: shows the saved high score on the menu (e.g. Stats_Text)")]
    public TextMeshProUGUI menuHighScoreText;

    [Header("Lobby UI Elements")]
    public TextMeshProUGUI lobbyStatusText;
    public TextMeshProUGUI p1StatusText;  // Player 1's Ready Checkmark
    public TextMeshProUGUI p2StatusText;  // Player 2's Ready Checkmark
    public TextMeshProUGUI pingText;      // Ping indicator
    public TextMeshProUGUI countdownText; // The giant center countdown
    public GameObject readyButton;        // The Ready button
    [Tooltip("Your name in the lobby (found automatically as P1_Name_text if empty)")]
    public TextMeshProUGUI p1NameText;
    [Tooltip("Opponent's name in the lobby (found automatically as P2_Name_text if empty)")]
    public TextMeshProUGUI p2NameText;

    [Header("Private Rooms")]
    [Tooltip("Room code box on the mode select panel (found automatically as RoomCode_Input if empty)")]
    public TMP_InputField roomCodeInput;
    private const string PrivateRoomPrefix = "tetris-private-";
    private const string RoomCodeLetters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // No 0/O or 1/I mix-ups
    private const int RoomCodeLength = 5;
    private string _roomCode;

    // Lobby messages (first byte of the reliable data)
    private const byte MsgReady = 1;
    private const byte MsgStartCountdown = 2;
    private const byte MsgName = 3;
    private const byte MsgRematch = 4;
    private bool _localRematch;
    private bool _opponentRematch;
    private const int MaxNameLength = 16;
    private bool _sentName = false;

    private TetrisInput _localInput;
    private bool _isSinglePlayer = false;

    // --- Ready State Variables ---
    private bool _isLocalReady = false;
    private bool _isOpponentReady = false;
    private bool _countdownStarted = false;
    private Coroutine _countdownRoutine;

    void Start()
    {
        if (nameInputField != null) nameInputField.text = PlayerPrefs.GetString("PlayerName", "Player 1");
        if (p1NameText == null) p1NameText = FindLobbyText("P1_Name_text");
        if (p2NameText == null) p2NameText = FindLobbyText("P2_Name_text");
        if (roomCodeInput == null && modeSelectPanel != null)
        {
            foreach (TMP_InputField field in modeSelectPanel.GetComponentsInChildren<TMP_InputField>(true))
            {
                if (field.name == "RoomCode_Input") roomCodeInput = field;
            }
        }
        if (menuHighScoreText != null) menuHighScoreText.text = "HIGH SCORE: " + GameOverManager.SavedHighScore;

        if (volumeSlider != null) volumeSlider.value = GameSettings.MasterVolume;

        bool isGameplayScene = SceneManager.GetActiveScene().buildIndex == MultiplayerSceneBuildIndex
            || SceneManager.GetActiveScene().buildIndex == SinglePlayerSceneBuildIndex;

        if (mainMenuPanel == null && isGameplayScene && FindFirstObjectByType<NetworkRunner>() == null)
        {
            _isSinglePlayer = SceneManager.GetActiveScene().buildIndex == SinglePlayerSceneBuildIndex;
            StartNetwork(_isSinglePlayer ? 1 : 2);
            return;
        }

        int targetMenu = PlayerPrefs.GetInt("TargetMenu", 0);
        PlayerPrefs.SetInt("TargetMenu", 0);

        if (mainMenuPanel) mainMenuPanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (modeSelectPanel) modeSelectPanel.SetActive(false);
        if (matchLobbyPanel) matchLobbyPanel.SetActive(false);

        if (targetMenu == 0 && mainMenuPanel) mainMenuPanel.SetActive(true);
        else if (targetMenu == 1 && modeSelectPanel) modeSelectPanel.SetActive(true);
        else if (targetMenu == 2) Button_Multiplayer();
    }

    void Update()
    {
        // Keys come from KeyBindings (rebindable in Settings > Controls); ignore them while a key is being rebound
        if (!KeyRebindButton.IsListening)
        {
            // Continuous input for movement
            _localInput.LeftHeld = KeyBindings.Held(GameAction.MoveLeft);
            _localInput.RightHeld = KeyBindings.Held(GameAction.MoveRight);
            _localInput.DownHeld = KeyBindings.Held(GameAction.SoftDrop);

            // Single tap inputs for actions
            _localInput.UpPressed |= KeyBindings.Pressed(GameAction.RotateClockwise);
            _localInput.RotateCCWPressed |= KeyBindings.Pressed(GameAction.RotateCounterClockwise);
            _localInput.Rotate180Pressed |= KeyBindings.Pressed(GameAction.Rotate180);
            _localInput.SpacePressed |= KeyBindings.Pressed(GameAction.HardDrop);
            _localInput.HoldPressed |= KeyBindings.Pressed(GameAction.Hold);

            _localInput.Skill1Pressed |= KeyBindings.Pressed(GameAction.Skill1);
            _localInput.Skill2Pressed |= KeyBindings.Pressed(GameAction.Skill2);
            _localInput.Skill3Pressed |= KeyBindings.Pressed(GameAction.Skill3);
        }

        // --- PING IDENTIFICATION ---
        if (_runner != null && _runner.IsRunning && pingText != null)
        {
            double rtt = _runner.GetPlayerRtt(_runner.LocalPlayer);
            int pingMs = Mathf.RoundToInt((float)rtt * 1000f);

            pingText.text = $"Ping: {pingMs} ms";

            if (pingMs < 60) pingText.color = Color.green;
            else if (pingMs < 150) pingText.color = Color.yellow;
            else pingText.color = Color.red;
        }
    }

    public void SavePlayerName() { if (nameInputField != null) PlayerPrefs.SetString("PlayerName", nameInputField.text); }
    public void OnVolumeChanged()
    {
        if (volumeSlider != null) GameSettings.SetMasterVolume(volumeSlider.value);
    }

    public void Button_OpenModeSelect() { SavePlayerName(); mainMenuPanel.SetActive(false); modeSelectPanel.SetActive(true); }
    public void Button_OpenSettings() { mainMenuPanel.SetActive(false); settingsPanel.SetActive(true); }
    public void Button_CloseSettings() { settingsPanel.SetActive(false); mainMenuPanel.SetActive(true); }
    public void Button_BackToMain() { modeSelectPanel.SetActive(false); matchLobbyPanel.SetActive(false); mainMenuPanel.SetActive(true); }
    public void Button_QuitGame() { Application.Quit(); }

    // Hook up to a Dropdown's On Value Changed (0 = Easy, 1 = Normal, 2 = Hard, 3 = Insane)
    public void SetAIDifficulty(int level)
    {
        GameSettings.AIDifficulty = level;
    }

    public void Button_CancelMatchmaking()
    {
        if (_runner != null && _runner.IsRunning)
        {
            LeaveSession(1); // Reloads the menu on the mode select panel
            return;
        }
        if (matchLobbyPanel) matchLobbyPanel.SetActive(false);
        if (modeSelectPanel) modeSelectPanel.SetActive(true);
    }

    // Ends the session (if any) and goes back to the menu scene. targetMenu: 0 = main menu, 1 = mode select
    public void LeaveSession(int targetMenu = 0)
    {
        PlayerPrefs.SetInt("TargetMenu", targetMenu);
        if (_runner != null && _runner.IsRunning) _runner.Shutdown(); // OnShutdown returns to the menu
        else ReturnToMenu(0f);
    }

    private void ReturnToMenu(float delay)
    {
        if (_returningToMenu) return;
        _returningToMenu = true;
        if (SessionOwner == this) SessionOwner = null;

        // Runs on its own object: Shutdown() destroys this launcher (the runner's GameObject),
        // which used to kill the coroutine before the menu loaded
        MenuReturner.Load(MenuSceneBuildIndex, delay, gameObject);
    }

    public void Button_Sprint() => StartSolo(MatchMode.Sprint);
    public void Button_Ultra() => StartSolo(MatchMode.Ultra);

    // Single player against the AI
    public void Button_SinglePlayer() => StartSolo(MatchMode.Versus);

    private void StartSolo(MatchMode mode)
    {
        GameModeSettings.Current = mode;
        _isSinglePlayer = true;
        modeSelectPanel.SetActive(false);
        StartNetwork(1);
    }

    public void Button_Multiplayer() => BeginMultiplayer(null);

    // Private room: type a friend's code to join them, or leave it empty to create a room and get a code
    public void Button_PrivateRoom()
    {
        string code = CleanRoomCode(roomCodeInput != null ? roomCodeInput.text : "");
        if (code.Length == 0) code = NewRoomCode();
        if (roomCodeInput != null) roomCodeInput.text = code;
        BeginMultiplayer(code);
    }

    private void BeginMultiplayer(string privateCode)
    {
        GameModeSettings.Current = MatchMode.Versus;
        _isSinglePlayer = false;
        _roomCode = privateCode;

        // Reset Lobby States
        _isLocalReady = false;
        _isOpponentReady = false;
        _countdownStarted = false;
        _sentName = false;

        if (p1NameText) p1NameText.text = LocalPlayerName();
        if (p2NameText) p2NameText.text = "<color=grey>Searching...</color>";
        if (countdownText) countdownText.text = "";
        if (p1StatusText) p1StatusText.text = "<color=grey>Connecting...</color>";
        if (p2StatusText) p2StatusText.text = "<color=grey>Waiting...</color>";
        if (lobbyStatusText) lobbyStatusText.text = _roomCode != null ? "OPENING ROOM " + _roomCode + "..." : "CONNECTING TO MATCHMAKING SERVER...";
        if (readyButton) readyButton.SetActive(false); // Hidden by default

        modeSelectPanel.SetActive(false);
        matchLobbyPanel.SetActive(true);
        StartNetwork(2, _roomCode);
    }

    private async void StartNetwork(int maxPlayers, string privateCode = null)
    {
        // 1. Prevent Component Leaks
        if (_runner != null)
        {
            Destroy(_runner);
        }

        // Only the launcher that owns the session persists across scene loads
        DontDestroyOnLoad(gameObject);
        SessionOwner = this;

        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.AddCallbacks(this);
        _runner.ProvideInput = true;

        var startGameArgs = new StartGameArgs()
        {
            GameMode = _isSinglePlayer ? GameMode.Single : GameMode.AutoHostOrClient,
            // Multiplayer: no session name = random matchmaking (join any open match, or host a new one),
            // so many matches can run at once instead of everyone fighting over a single room
            // A private room uses the code as its name and is hidden from random matchmaking
            SessionName = _isSinglePlayer ? Guid.NewGuid().ToString() : (privateCode != null ? PrivateRoomPrefix + privateCode : null),
            IsVisible = privateCode == null,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(),
            PlayerCount = maxPlayers
        };

        if (_isSinglePlayer)
        {
            startGameArgs.Scene = SceneRef.FromIndex(SinglePlayerSceneBuildIndex);
        }

        // 2. Handle Connection Failures gracefully
        var result = await _runner.StartGame(startGameArgs);

        if (!result.Ok)
        {
            Debug.LogError($"Matchmaking Failed: {result.ShutdownReason}");
            if (lobbyStatusText) lobbyStatusText.text = $"ERROR: {result.ShutdownReason}";
            PlayerPrefs.SetInt("TargetMenu", 1);
            ReturnToMenu(3f); // Kick them back to the menu after 3 seconds
        }
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (_isSinglePlayer)
        {
            // StartGame already loads the single player scene (startGameArgs.Scene); loading it
            // again here reloaded the scene and could spawn the boards twice
            if (matchLobbyPanel) matchLobbyPanel.SetActive(false);
            return;
        }

        if (!_isSinglePlayer && runner.IsServer && SceneManager.GetActiveScene().buildIndex == MultiplayerSceneBuildIndex)
        {
            runner.LoadScene(SceneRef.FromIndex(MultiplayerSceneBuildIndex));
            return;
        }

        int currentPlayers = 0;
        foreach (var p in runner.ActivePlayers) currentPlayers++;

        // If the local player just established a connection
        if (player == runner.LocalPlayer)
        {
            if (p1StatusText) p1StatusText.text = "<color=red>Not Ready</color>";
            if (lobbyStatusText) lobbyStatusText.text = currentPlayers == 1 ? WaitingForOpponentText() : "OPPONENT FOUND!";
        }

        // When BOTH players are fully connected and in the room
        if (currentPlayers == 2)
        {
            if (lobbyStatusText) lobbyStatusText.text = "OPPONENT CONNECTED! CLICK READY!";
            if (p2StatusText) p2StatusText.text = "<color=red>Not Ready</color>";
            if (readyButton && !_isLocalReady) readyButton.SetActive(true);
            SendMyName();
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        // Opponent quit mid-match: whoever is still here wins
        if (!_isSinglePlayer && runner.IsServer && SceneManager.GetActiveScene().buildIndex == MultiplayerSceneBuildIndex)
        {
            foreach (TetrisEngine board in FindObjectsByType<TetrisEngine>(FindObjectsSortMode.None))
            {
                if (board.Object == null) continue;
                if (board.Object.InputAuthority != player) board.DeclareWinner();
                else board.Forfeit(); // Stop the leaver's board instead of letting it fall on its own
            }
            _opponentRematch = false;
            if (GameOverManager.Instance != null) GameOverManager.Instance.OnOpponentLeft();
        }

        // Instantly reset the lobby if the opponent disconnects
        if (!_isSinglePlayer && matchLobbyPanel != null && matchLobbyPanel.activeInHierarchy)
        {
            _isOpponentReady = false;
            _sentName = false; // Introduce ourselves again to the next opponent
            if (p2NameText) p2NameText.text = "<color=grey>Searching...</color>";
            if (p2StatusText) p2StatusText.text = "<color=grey>Waiting...</color>";
            if (lobbyStatusText) lobbyStatusText.text = "OPPONENT DISCONNECTED. WAITING...";
            if (readyButton) readyButton.SetActive(false);

            // Kill the countdown if they rage-quit before it hits 0
            if (_countdownStarted)
            {
                _countdownStarted = false;

                // 3. Stop only the countdown, not everything else
                if (_countdownRoutine != null) StopCoroutine(_countdownRoutine);

                if (countdownText) countdownText.text = "MATCH CANCELLED";
            }
        }
    }

    // ==========================================
    // --- LOBBY READY & COUNTDOWN LOGIC ---
    // ==========================================

    public void Button_Ready()
    {
        _isLocalReady = true;

        // Update our local visual proxy
        if (p1StatusText) p1StatusText.text = "<color=green>READY ✓</color>";
        if (readyButton) readyButton.SetActive(false);

        if (_runner == null || !_runner.IsRunning) return;

        // Send a network message to the opponent saying we are ready (Signal '1').
        // The host sends to the client; a client always talks to the host through the server.
        if (_runner.IsServer)
        {
            foreach (var p in _runner.ActivePlayers)
            {
                if (p != _runner.LocalPlayer)
                {
                    _runner.SendReliableDataToPlayer(p, ReliableKey.FromInts(1), new byte[] { 1 });
                }
            }
            CheckBothReady();
        }
        else
        {
            _runner.SendReliableDataToServer(ReliableKey.FromInts(1), new byte[] { 1 });
        }
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
        if (data.Count > 0)
        {
            // If we receive a '1', the opponent clicked ready!
            if (data[0] == 1)
            {
                _isOpponentReady = true;

                // Update the opponent's visual proxy on our screen
                if (p2StatusText) p2StatusText.text = "<color=green>READY ✓</color>";

                if (runner.IsServer) CheckBothReady();
            }
            // If we receive a '2', the host is starting the countdown!
            else if (data[0] == MsgStartCountdown)
            {
                _countdownRoutine = StartCoroutine(LobbyCountdown());
            }
            // A '3' carries the opponent's name
            else if (data[0] == MsgName)
            {
                string opponentName = CleanName(System.Text.Encoding.UTF8.GetString(data.Array, data.Offset + 1, data.Count - 1));
                if (p2NameText) p2NameText.text = opponentName;
                SendMyName(); // Answer with ours if we haven't yet
            }
            // A '4' means the other player pressed REMATCH
            else if (data[0] == MsgRematch)
            {
                _opponentRematch = true;
                if (GameOverManager.Instance != null) GameOverManager.Instance.OnOpponentWantsRematch();
                if (runner.IsServer) CheckRematch();
            }
        }
    }

    // ==========================================
    // --- REMATCH ---
    // ==========================================

    // Called by the result screen's REMATCH button. Both players must press it; single player restarts at once.
    public void RequestRematch()
    {
        if (_runner == null || !_runner.IsRunning) return;
        _localRematch = true;

        if (_isSinglePlayer)
        {
            StartRematch();
            return;
        }

        byte[] message = { MsgRematch };
        if (_runner.IsServer)
        {
            foreach (var p in _runner.ActivePlayers)
            {
                if (p != _runner.LocalPlayer) _runner.SendReliableDataToPlayer(p, ReliableKey.FromInts(3), message);
            }
            CheckRematch();
        }
        else
        {
            _runner.SendReliableDataToServer(ReliableKey.FromInts(3), message);
        }
    }

    private void CheckRematch()
    {
        if (_localRematch && _opponentRematch) StartRematch();
    }

    // Host only: clear the old boards and reload the match scene; OnSceneLoadDone spawns fresh boards
    private void StartRematch()
    {
        if (!_runner.IsServer) return;
        _localRematch = _opponentRematch = false;

        foreach (TetrisEngine board in FindObjectsByType<TetrisEngine>(FindObjectsSortMode.None))
        {
            if (board.Object != null && board.Object.IsValid) _runner.Despawn(board.Object);
        }
        _runner.LoadScene(SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex));
    }

    // ==========================================
    // --- PRIVATE ROOM CODES ---
    // ==========================================

    private static string NewRoomCode()
    {
        var code = new System.Text.StringBuilder();
        for (int i = 0; i < RoomCodeLength; i++) code.Append(RoomCodeLetters[UnityEngine.Random.Range(0, RoomCodeLetters.Length)]);
        return code.ToString();
    }

    // Upper-case letters and digits only, so "abc12 " and "ABC12" reach the same room
    private static string CleanRoomCode(string code)
    {
        var clean = new System.Text.StringBuilder();
        foreach (char c in (code ?? "").ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(c) && clean.Length < 12) clean.Append(c);
        }
        return clean.ToString();
    }

    private string WaitingForOpponentText()
    {
        return _roomCode != null
            ? $"ROOM CODE: <color=yellow>{_roomCode}</color>\nSHARE IT WITH A FRIEND..."
            : "WAITING FOR OPPONENT TO JOIN...";
    }

    // ==========================================
    // --- PLAYER NAMES ---
    // ==========================================

    private string LocalPlayerName() => CleanName(PlayerPrefs.GetString("PlayerName", ""));

    // Short, and no rich-text tags (a name like "<size=500>" would break the lobby layout)
    private static string CleanName(string name)
    {
        name = (name ?? "").Replace("<", "").Replace(">", "").Trim();
        if (name.Length > MaxNameLength) name = name.Substring(0, MaxNameLength);
        return name.Length > 0 ? name : "Player";
    }

    // Tells the opponent our name once per opponent (host -> client directly, client -> host via the server)
    private void SendMyName()
    {
        if (_sentName || _runner == null || !_runner.IsRunning || _isSinglePlayer) return;

        byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(LocalPlayerName());
        byte[] payload = new byte[nameBytes.Length + 1];
        payload[0] = MsgName;
        Buffer.BlockCopy(nameBytes, 0, payload, 1, nameBytes.Length);

        bool sent = false;
        if (_runner.IsServer)
        {
            foreach (var p in _runner.ActivePlayers)
            {
                if (p == _runner.LocalPlayer) continue;
                _runner.SendReliableDataToPlayer(p, ReliableKey.FromInts(2), payload);
                sent = true;
            }
        }
        else
        {
            _runner.SendReliableDataToServer(ReliableKey.FromInts(2), payload);
            sent = true;
        }
        _sentName = sent;
    }

    private TextMeshProUGUI FindLobbyText(string objectName)
    {
        if (matchLobbyPanel == null) return null;
        foreach (TextMeshProUGUI text in matchLobbyPanel.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text.name == objectName) return text;
        }
        return null;
    }

    private void CheckBothReady()
    {
        if (_isLocalReady && _isOpponentReady && !_countdownStarted)
        {
            _countdownStarted = true;

            // Tell the client to start their countdown (Signal '2')
            foreach (var p in _runner.ActivePlayers)
            {
                if (p != _runner.LocalPlayer)
                {
                    _runner.SendReliableDataToPlayer(p, ReliableKey.FromInts(1), new byte[] { 2 });
                }
            }

            // 3. Track the specific coroutine
            _countdownRoutine = StartCoroutine(LobbyCountdown());
        }
    }

    private IEnumerator LobbyCountdown()
    {
        if (countdownText)
        {
            countdownText.color = Color.yellow;
            for (int i = 3; i >= 1; i--)
            {
                countdownText.text = i.ToString();
                AudioManager.Play(Sfx.CountdownTick);
                yield return new WaitForSeconds(1f);
            }

            countdownText.color = Color.green;
            countdownText.text = "START!";
            AudioManager.Play(Sfx.CountdownGo);
            yield return new WaitForSeconds(0.5f);
        }

        if (_runner.IsServer)
        {
            _runner.SessionInfo.IsOpen = false;
            _runner.LoadScene(SceneRef.FromIndex(MultiplayerSceneBuildIndex)); // Loads GameScene
        }
    }

    // ==========================================
    // --- SCENE LOADING & SPAWNING ---
    // ==========================================

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        _localRematch = _opponentRematch = false; // A new match (or rematch) starts clean
        if (!runner.IsServer) return;

        // FindSpawnPoint also finds inactive markers (SinglePlayerScene's SpawnPoint Opp is inactive)
        Transform p1Spawn = spawnPoints != null && spawnPoints.Length > 0 && spawnPoints[0] != null
            ? spawnPoints[0]
            : TetrisEngine.FindSpawnPoint(true);
        Transform p2Spawn = spawnPoints != null && spawnPoints.Length > 1 && spawnPoints[1] != null
            ? spawnPoints[1]
            : TetrisEngine.FindSpawnPoint(false);

        int playersSpawned = 0;
        foreach (var player in runner.ActivePlayers)
        {
            Vector3 spawnPos = playersSpawned == 0 && p1Spawn != null
                ? p1Spawn.position
                : playersSpawned == 1 && p2Spawn != null
                    ? p2Spawn.position
                    : playersSpawned == 0
                        ? new Vector3(-15, 0, 0)
                        : new Vector3(5, 0, 0);

            runner.Spawn(playerBoardPrefab, spawnPos, Quaternion.identity, player);
            playersSpawned++;
        }

        // Single player: the NPC gets its own board in the opponent's spot
        // (Sprint / Ultra are played alone: no AI board)
        NPCAI npc = _isSinglePlayer && !GameModeSettings.IsSolo ? FindFirstObjectByType<NPCAI>() : null;
        if (npc != null && npc.PlaysOwnBoard && !AIBoardExists())
        {
            Vector3 aiPos = p2Spawn != null ? p2Spawn.position : new Vector3(5, 0, 0);
            NetworkObject aiBoard = runner.Spawn(playerBoardPrefab, aiPos, Quaternion.identity, PlayerRef.None);
            aiBoard.GetComponent<TetrisEngine>().aiController = npc;
        }

        Debug.Log($"FusionLauncher spawned {playersSpawned} player board(s).");
        if (matchLobbyPanel) matchLobbyPanel.SetActive(false);
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason reason)
    {
        // The session ended under us in a match (the host left or the connection dropped):
        // say so on the result screen and let the player press Return, which calls LeaveSession
        if (reason != ShutdownReason.Ok && GameOverManager.Instance != null)
        {
            GameOverManager.Instance.OnConnectionLost();
            return;
        }

        // A failed connection in the lobby is shown for a moment before leaving
        bool inLobby = SceneManager.GetActiveScene().buildIndex == MenuSceneBuildIndex;
        if (inLobby && reason != ShutdownReason.Ok && lobbyStatusText)
        {
            lobbyStatusText.text = _countdownStarted || _isOpponentReady || _sentName
                ? "<color=#FF8080>THE HOST LEFT THE LOBBY</color>"
                : $"<color=#FF8080>CONNECTION LOST ({reason})</color>";
        }
        ReturnToMenu(inLobby && reason != ShutdownReason.Ok ? 3f : 0f);
    }

    private static bool AIBoardExists()
    {
        foreach (TetrisEngine board in FindObjectsByType<TetrisEngine>(FindObjectsSortMode.None))
        {
            if (board.aiController != null) return true;
        }
        return false;
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        _localInput.HasHandling = true;
        _localInput.DasMs = (short)GameSettings.DasMs;
        _localInput.ArrMs = (short)GameSettings.ArrMs;
        input.Set(_localInput);
        _localInput = default;
    }

    #region Unused Fusion Callbacks

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    #endregion
}