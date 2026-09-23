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
        // Continuous input for movement
        _localInput.LeftHeld = Input.GetKey(KeyCode.LeftArrow);
        _localInput.RightHeld = Input.GetKey(KeyCode.RightArrow);
        _localInput.DownHeld = Input.GetKey(KeyCode.DownArrow);

        // Single tap inputs for actions
        _localInput.UpPressed |= Input.GetKeyDown(KeyCode.UpArrow);
        _localInput.SpacePressed |= Input.GetKeyDown(KeyCode.Space);
        _localInput.HoldPressed |= Input.GetKeyDown(KeyCode.C);

        _localInput.Skill1Pressed |= Input.GetKeyDown(KeyCode.Alpha1);
        _localInput.Skill2Pressed |= Input.GetKeyDown(KeyCode.Alpha2);
        _localInput.Skill3Pressed |= Input.GetKeyDown(KeyCode.Alpha3);

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
        else StartCoroutine(ReturnToMenu(0f));
    }

    private IEnumerator ReturnToMenu(float delay)
    {
        if (_returningToMenu) yield break;
        _returningToMenu = true;

        // Never tear down from inside a Fusion callback
        yield return null;
        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (SessionOwner == this) SessionOwner = null;
        SceneManager.LoadScene(MenuSceneBuildIndex);
        // The fresh menu scene brings its own launcher with its UI references
        Destroy(gameObject);
    }

    public void Button_SinglePlayer()
    {
        _isSinglePlayer = true;
        modeSelectPanel.SetActive(false);
        StartNetwork(1);
    }

    public void Button_Multiplayer()
    {
        _isSinglePlayer = false;

        // Reset Lobby States
        _isLocalReady = false;
        _isOpponentReady = false;
        _countdownStarted = false;

        if (countdownText) countdownText.text = "";
        if (p1StatusText) p1StatusText.text = "<color=grey>Connecting...</color>";
        if (p2StatusText) p2StatusText.text = "<color=grey>Waiting...</color>";
        if (lobbyStatusText) lobbyStatusText.text = "CONNECTING TO MATCHMAKING SERVER...";
        if (readyButton) readyButton.SetActive(false); // Hidden by default

        modeSelectPanel.SetActive(false);
        matchLobbyPanel.SetActive(true);
        StartNetwork(2);
    }

    private async void StartNetwork(int maxPlayers)
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
            SessionName = _isSinglePlayer ? Guid.NewGuid().ToString() : null,
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
            StartCoroutine(ReturnToMenu(3f)); // Kick them back to the menu after 3 seconds
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
            if (lobbyStatusText) lobbyStatusText.text = currentPlayers == 1 ? "WAITING FOR OPPONENT TO JOIN..." : "OPPONENT FOUND!";
        }

        // When BOTH players are fully connected and in the room
        if (currentPlayers == 2)
        {
            if (lobbyStatusText) lobbyStatusText.text = "OPPONENT CONNECTED! CLICK READY!";
            if (p2StatusText) p2StatusText.text = "<color=red>Not Ready</color>";
            if (readyButton && !_isLocalReady) readyButton.SetActive(true);
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
        }

        // Instantly reset the lobby if the opponent disconnects
        if (!_isSinglePlayer && matchLobbyPanel != null && matchLobbyPanel.activeInHierarchy)
        {
            _isOpponentReady = false;
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
            else if (data[0] == 2)
            {
                _countdownRoutine = StartCoroutine(LobbyCountdown());
            }
        }
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
        NPCAI npc = _isSinglePlayer ? FindFirstObjectByType<NPCAI>() : null;
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
        // We lost the connection (e.g. the host left) while our result screen is up:
        // stay on it and let the player press Return, which calls LeaveSession
        bool resultScreenShowing = GameOverManager.Instance != null && GameOverManager.Instance.IsShowing;
        if (reason != ShutdownReason.Ok && resultScreenShowing) return;

        // A failed connection in the lobby is shown for a moment before leaving
        bool inLobby = SceneManager.GetActiveScene().buildIndex == MenuSceneBuildIndex;
        StartCoroutine(ReturnToMenu(inLobby && reason != ShutdownReason.Ok ? 3f : 0f));
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