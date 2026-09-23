using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The PROFILE screen on the main menu: level, XP bar, records and lifetime stats.
public class ProfileMenu : MonoBehaviour
{
    public TMP_Text nameText;
    public TMP_Text levelText;
    public Image xpFill;          // Filled image (Horizontal)
    public TMP_Text xpText;
    public TMP_Text matchesText;
    public TMP_Text recordsText;
    public Button backButton;
    public FusionLauncher launcher;

    void Awake()
    {
        if (backButton) backButton.onClick.AddListener(Close);
    }

    void OnEnable() => Refresh();

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    public void Close()
    {
        if (launcher != null) launcher.Button_CloseProfile();
        else gameObject.SetActive(false);
    }

    public void Refresh()
    {
        PlayerProfile p = ProfileStore.Profile;
        int needed = ProfileStore.XpToNextLevel(p.level);

        if (nameText) nameText.text = PlayerPrefs.GetString("PlayerName", "Player");
        if (levelText) levelText.text = $"LEVEL {p.level}  ·  <color=yellow>{ProfileStore.RankTitle(p.level).ToUpper()}</color>";
        if (xpFill) xpFill.fillAmount = ProfileStore.LevelProgress;
        if (xpText) xpText.text = $"{p.xp} / {needed} XP  ·  {p.totalXp} XP total";

        int onlineGames = p.onlineWins + p.onlineLosses;
        int aiGames = p.aiWins + p.aiLosses;
        if (matchesText)
        {
            matchesText.text =
                "<b><color=#9FCBFF>MATCHES</color></b>\n" +
                $"Games played  <b>{p.gamesPlayed}</b>\n" +
                $"Online  <b>{p.onlineWins}W - {p.onlineLosses}L</b>{Percent(p.onlineWins, onlineGames)}\n" +
                $"vs AI  <b>{p.aiWins}W - {p.aiLosses}L</b>{Percent(p.aiWins, aiGames)}\n" +
                $"Sprint  <b>{p.sprintClears} / {p.sprintRuns}</b> cleared\n" +
                $"Ultra runs  <b>{p.ultraRuns}</b>\n" +
                $"Time played  <b>{FormatDuration(p.secondsPlayed)}</b>";
        }

        if (recordsText)
        {
            float sprint = GameModeSettings.BestSprintTime;
            recordsText.text =
                "<b><color=#9FCBFF>RECORDS</color></b>\n" +
                $"High score  <b>{GameOverManager.SavedHighScore}</b>\n" +
                $"Best Sprint  <b>{(sprint > 0f ? GameModeSettings.FormatTime(sprint) : "-")}</b>\n" +
                $"Best Ultra  <b>{GameModeSettings.BestUltraScore}</b>\n" +
                $"Best combo  <b>{p.bestCombo}</b>\n\n" +
                "<b><color=#9FCBFF>LIFETIME</color></b>\n" +
                $"Lines  <b>{p.linesCleared}</b>   Pieces  <b>{p.piecesPlaced}</b>\n" +
                $"Quads  <b>{p.quads}</b>   T-spins  <b>{p.tSpins}</b> (+{p.tSpinMinis} mini)\n" +
                $"All clears  <b>{p.allClears}</b>";
        }
    }

    private static string Percent(int wins, int games) =>
        games > 0 ? $"  <size=80%>({Mathf.RoundToInt(100f * wins / games)}%)</size>" : "";

    private static string FormatDuration(double seconds)
    {
        int total = (int)seconds;
        return total >= 3600 ? $"{total / 3600}h {total % 3600 / 60}m" : $"{total / 60}m {total % 60}s";
    }
}
