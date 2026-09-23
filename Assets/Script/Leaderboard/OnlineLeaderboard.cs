using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
#if UGS_LEADERBOARDS
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
#endif

// World leaderboards for Sprint (fastest time) and Ultra (highest score), on Unity Gaming Services.
// Players sign in anonymously; their lobby name (PlayerPrefs "PlayerName") is shown on the board.
// Setup: link the project in Project Settings > Services, then deploy Assets/Leaderboards/*.lb from
// Services > Deployment. Until then every call returns a friendly "not set up" message.
public static class OnlineLeaderboard
{
    public const string SprintBoard = "sprint_40l"; // Score = milliseconds, lower is better
    public const string UltraBoard = "ultra_2min";  // Score = points, higher is better

    private const string NotSetUp = "<color=#FFB080>World leaderboard not set up yet</color>";
    private const string Offline = "<color=#FFB080>World leaderboard offline</color>";

    // Returns a one-line result such as "WORLD RANK #12"
    public static async Task<string> SubmitAsync(string boardId, double score)
    {
#if UGS_LEADERBOARDS
        try
        {
            await EnsureSignedInAsync();
            var entry = await LeaderboardsService.Instance.AddPlayerScoreAsync(boardId, score);
            return $"<color=#9FCBFF>WORLD RANK #{entry.Rank + 1}</color>";
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Leaderboard submit failed ({boardId}): {e.Message}");
            return Offline;
        }
#else
        await Task.Yield();
        return NotSetUp;
#endif
    }

    // Multi-line "1. Name  1:02.33" list for the result screen
    public static async Task<string> TopTextAsync(string boardId, int count)
    {
#if UGS_LEADERBOARDS
        try
        {
            await EnsureSignedInAsync();
            var page = await LeaderboardsService.Instance.GetScoresAsync(boardId, new GetScoresOptions { Limit = count });
            if (page.Results == null || page.Results.Count == 0) return "<size=70%>No scores yet - be the first!</size>";

            string myId = AuthenticationService.Instance.PlayerId;
            var text = new StringBuilder();
            foreach (var entry in page.Results)
            {
                string name = StripDiscriminator(entry.PlayerName);
                string value = boardId == SprintBoard ? FormatTime(entry.Score / 1000.0) : ((long)entry.Score).ToString();
                string line = $"{entry.Rank + 1}. {name}  {value}";
                text.AppendLine(entry.PlayerId == myId ? $"<color=yellow>{line}</color>" : line);
            }
            return "<size=70%>" + text.ToString().TrimEnd() + "</size>";
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Leaderboard read failed ({boardId}): {e.Message}");
            return "<size=70%>" + Offline + "</size>";
        }
#else
        await Task.Yield();
        return "<size=70%>" + NotSetUp + "</size>";
#endif
    }

#if UGS_LEADERBOARDS
    private static Task _signIn;

    // UnityServices.InitializeAsync -> anonymous sign-in -> player name (done once per session)
    private static Task EnsureSignedInAsync()
    {
        if (_signIn == null || _signIn.IsFaulted) _signIn = SignInAsync();
        return _signIn;
    }

    private static async Task SignInAsync()
    {
        if (UnityServices.State == ServicesInitializationState.Uninitialized) await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();

        string wanted = CleanName(PlayerPrefs.GetString("PlayerName", ""));
        if (StripDiscriminator(AuthenticationService.Instance.PlayerName) != wanted)
        {
            await AuthenticationService.Instance.UpdatePlayerNameAsync(wanted);
        }
    }
#endif

    // UGS names can't contain spaces and get a "#1234" suffix added
    private static string CleanName(string name)
    {
        var clean = new StringBuilder();
        foreach (char c in (name ?? "").Trim())
        {
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-') clean.Append(c);
            else if (c == ' ') clean.Append('_');
            if (clean.Length >= 20) break;
        }
        return clean.Length > 0 ? clean.ToString() : "Player";
    }

    private static string StripDiscriminator(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Player";
        int hash = name.LastIndexOf('#');
        return hash > 0 ? name.Substring(0, hash) : name;
    }

    private static string FormatTime(double seconds)
    {
        int minutes = (int)(seconds / 60.0);
        return $"{minutes}:{seconds - minutes * 60.0:00.00}";
    }
}
