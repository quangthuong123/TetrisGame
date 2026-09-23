using System;
using System.IO;
using UnityEngine;

// Everything needed to put one board back exactly as it was
[Serializable]
public class BoardSnapshot
{
    public int[] grid;          // Width * Height cells
    public int pieceId;         // 1-7, or 9 for the X piece
    public int pieceRotation;
    public int pieceX, pieceY;
    public int holdId;
    public bool canHold;
    public int nextId;
    public int forcedNext;
    public int[] bag;
    public int bagIndex;
    public int score, skillPoints, lines;
    public int combo;
    public bool backToBack;
    public int pendingGarbage;
    public float modeTime, elapsedTime, fallSpeed;
    public int pieces, quads, tSpins, tSpinMinis, allClears, bestCombo;
}

// A single player game saved mid-play (Sprint, Ultra or vs AI)
[Serializable]
public class ResumeData
{
    public MatchMode mode;
    public int aiDifficulty;
    public string savedAt;      // Local time, for the Continue button
    public BoardSnapshot player;
    public BoardSnapshot ai;    // Only for vs AI
}

// One resumable game at a time, in Application.persistentDataPath/resume.json
public static class ResumeStore
{
    // Set when CONTINUE is pressed; FusionLauncher applies it once the boards spawn
    public static ResumeData Pending;

    private static string FilePath => Path.Combine(Application.persistentDataPath, "resume.json");

    public static bool HasSave => File.Exists(FilePath);

    public static void Save(ResumeData data)
    {
        try
        {
            data.savedAt = DateTime.Now.ToString("MMM d, HH:mm");
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data));
            File.Copy(temp, FilePath, true);
            File.Delete(temp);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Couldn't save the game: " + e.Message);
        }
    }

    public static ResumeData Load()
    {
        try
        {
            return HasSave ? JsonUtility.FromJson<ResumeData>(File.ReadAllText(FilePath)) : null;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Couldn't read the saved game: " + e.Message);
            return null;
        }
    }

    public static void Delete()
    {
        try
        {
            if (HasSave) File.Delete(FilePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Couldn't delete the saved game: " + e.Message);
        }
    }

    public static string Describe(ResumeData data)
    {
        if (data == null) return "";
        string mode = data.mode == MatchMode.Versus ? "VS AI" : GameModeSettings.DisplayName(data.mode);
        return $"{mode}  ·  {data.savedAt}";
    }
}
