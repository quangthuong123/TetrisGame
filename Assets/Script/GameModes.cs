using UnityEngine;

public enum MatchMode
{
    Versus, // Online match, or single player against the AI
    Sprint, // Clear 40 lines as fast as possible
    Ultra,  // Highest score in 2 minutes
}

// The mode picked on the mode select screen. Single player sessions run on this machine, so a static is enough.
public static class GameModeSettings
{
    public const int SprintLines = 40;
    public const float UltraSeconds = 120f;

    public static MatchMode Current = MatchMode.Versus;

    public static bool IsSolo => Current != MatchMode.Versus;

    public static string DisplayName(MatchMode mode)
    {
        switch (mode)
        {
            case MatchMode.Sprint: return "SPRINT " + SprintLines + "L";
            case MatchMode.Ultra: return "ULTRA 2:00";
            default: return "VERSUS";
        }
    }

    // Personal bests (the online leaderboard keeps the world ones)
    private const string BestSprintKey = "BestSprintTime";
    private const string BestUltraKey = "BestUltraScore";

    public static float BestSprintTime => PlayerPrefs.GetFloat(BestSprintKey, 0f); // 0 = none yet
    public static int BestUltraScore => PlayerPrefs.GetInt(BestUltraKey, 0);

    // Returns true when this is a new personal best
    public static bool RecordSprint(float seconds)
    {
        float best = BestSprintTime;
        if (best > 0f && seconds >= best) return false;
        PlayerPrefs.SetFloat(BestSprintKey, seconds);
        PlayerPrefs.Save();
        return true;
    }

    public static bool RecordUltra(int score)
    {
        if (score <= BestUltraScore) return false;
        PlayerPrefs.SetInt(BestUltraKey, score);
        PlayerPrefs.Save();
        return true;
    }

    public static string FormatTime(float seconds)
    {
        if (seconds < 0f) seconds = 0f;
        int minutes = (int)(seconds / 60f);
        return $"{minutes}:{seconds - minutes * 60f:00.00}";
    }
}
