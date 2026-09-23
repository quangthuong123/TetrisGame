using System;
using System.IO;
using UnityEngine;

// Everything a player earns over time. Saved as JSON in Application.persistentDataPath/profile.json.
[Serializable]
public class PlayerProfile
{
    public int version = 1;

    // Level & XP
    public int level = 1;
    public int xp;          // XP inside the current level
    public long totalXp;

    // Matches
    public int gamesPlayed;
    public int onlineWins, onlineLosses;
    public int aiWins, aiLosses;
    public int sprintRuns, sprintClears;
    public int ultraRuns;

    // Play
    public long linesCleared;
    public long piecesPlaced;
    public int quads;
    public int tSpins;
    public int tSpinMinis;
    public int allClears;
    public int bestCombo;
    public long totalScore;
    public double secondsPlayed;
}

public enum MatchOutcome { Win, Loss, Completed, Failed, Ended }

// One finished match, as reported by the local player's board
public struct MatchSummary
{
    public MatchMode mode;
    public bool vsAI;           // Versus against the single player AI (not online)
    public MatchOutcome outcome;
    public int score;
    public int lines;
    public int pieces;
    public int quads;
    public int tSpins;
    public int tSpinMinis;
    public int allClears;
    public int bestCombo;
    public float seconds;
}

// What a match was worth, for the result screen
public struct XpResult
{
    public int gained;
    public int levelBefore;
    public int levelAfter;
    public bool LeveledUp => levelAfter > levelBefore;
}

public static class ProfileStore
{
    private static PlayerProfile _profile;
    private static string FilePath => Path.Combine(Application.persistentDataPath, "profile.json");
    private static string BackupPath => FilePath + ".bak";

    public static PlayerProfile Profile
    {
        get
        {
            if (_profile == null) _profile = Load();
            return _profile;
        }
    }

    // ---------- Levels ----------

    public static int XpToNextLevel(int level) => 100 + 50 * (level - 1);

    public static string RankTitle(int level)
    {
        if (level >= 50) return "Legend";
        if (level >= 40) return "Grandmaster";
        if (level >= 30) return "Master";
        if (level >= 20) return "Expert";
        if (level >= 15) return "Veteran";
        if (level >= 10) return "Stacker";
        if (level >= 5) return "Apprentice";
        return "Rookie";
    }

    public static float LevelProgress => (float)Profile.xp / XpToNextLevel(Profile.level);

    // ---------- Recording a match ----------

    public static XpResult RecordMatch(MatchSummary m)
    {
        PlayerProfile p = Profile;
        p.gamesPlayed++;
        p.linesCleared += m.lines;
        p.piecesPlaced += m.pieces;
        p.quads += m.quads;
        p.tSpins += m.tSpins;
        p.tSpinMinis += m.tSpinMinis;
        p.allClears += m.allClears;
        p.bestCombo = Mathf.Max(p.bestCombo, m.bestCombo);
        p.totalScore += m.score;
        p.secondsPlayed += m.seconds;

        switch (m.mode)
        {
            case MatchMode.Sprint:
                p.sprintRuns++;
                if (m.outcome == MatchOutcome.Completed) p.sprintClears++;
                break;
            case MatchMode.Ultra:
                p.ultraRuns++;
                break;
            default:
                if (m.outcome == MatchOutcome.Win) { if (m.vsAI) p.aiWins++; else p.onlineWins++; }
                else if (m.outcome == MatchOutcome.Loss) { if (m.vsAI) p.aiLosses++; else p.onlineLosses++; }
                break;
        }

        var result = new XpResult { levelBefore = p.level };
        result.gained = XpFor(m);
        AddXp(result.gained);
        result.levelAfter = p.level;
        Save();
        return result;
    }

    // Everything counts a little; skilful clears and wins count a lot
    private static int XpFor(MatchSummary m)
    {
        int xp = 10 + m.lines * 2 + m.quads * 10 + m.tSpins * 15 + m.tSpinMinis * 5 + m.allClears * 50 + Mathf.Max(0, m.bestCombo) * 3;
        if (m.outcome == MatchOutcome.Win) xp += m.vsAI ? 30 : 60;
        if (m.mode == MatchMode.Sprint && m.outcome == MatchOutcome.Completed) xp += 40;
        if (m.mode == MatchMode.Ultra) xp += m.score / 250;
        return xp;
    }

    private static void AddXp(int amount)
    {
        PlayerProfile p = Profile;
        p.totalXp += amount;
        p.xp += amount;
        while (p.xp >= XpToNextLevel(p.level))
        {
            p.xp -= XpToNextLevel(p.level);
            p.level++;
        }
    }

    // ---------- Save / load ----------

    // Writes to a temp file first, so a crash mid-save can't leave a half-written profile
    public static void Save()
    {
        try
        {
            string json = JsonUtility.ToJson(Profile, true);
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, json);
            if (File.Exists(FilePath)) File.Copy(FilePath, BackupPath, true);
            File.Copy(temp, FilePath, true);
            File.Delete(temp);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Couldn't save the player profile: " + e.Message);
        }
    }

    private static PlayerProfile Load()
    {
        foreach (string path in new[] { FilePath, BackupPath })
        {
            try
            {
                if (!File.Exists(path)) continue;
                PlayerProfile loaded = JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(path));
                if (loaded != null) return loaded;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Couldn't read {Path.GetFileName(path)}: {e.Message}");
            }
        }
        return new PlayerProfile();
    }

    // For a "reset progress" button, if you ever add one
    public static void ResetProfile()
    {
        _profile = new PlayerProfile();
        Save();
    }
}
