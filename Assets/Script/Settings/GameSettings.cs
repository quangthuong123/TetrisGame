using UnityEngine;

// Every player setting in one place: saved in PlayerPrefs, cached for fast reads during play,
// and applied to the game at startup (before the first scene loads).
public static class GameSettings
{
    // Keys (MasterVolume and AIDifficulty match the keys the game already used)
    private const string MasterVolumeKey = "MasterVolume";
    private const string MusicVolumeKey = "MusicVolume";
    private const string SfxVolumeKey = "SfxVolume";
    private const string FullscreenKey = "Fullscreen";
    private const string ResolutionWidthKey = "ResolutionWidth";
    private const string ResolutionHeightKey = "ResolutionHeight";
    private const string VSyncKey = "VSync";
    private const string FrameRateKey = "FrameRateLimit";
    private const string DasKey = "DasMs";
    private const string ArrKey = "ArrMs";
    private const string GhostKey = "ShowGhost";

    // Defaults
    public const float DefaultMasterVolume = 1f;
    public const float DefaultMusicVolume = 0.5f;
    public const float DefaultSfxVolume = 0.8f;
    public const int DefaultDasMs = 150;  // Delay before a held left/right starts repeating
    public const int DefaultArrMs = 50;   // Time between repeats (0 = slide instantly to the wall)
    public const int MinDasMs = 50, MaxDasMs = 300;
    public const int MinArrMs = 0, MaxArrMs = 100;

    // Frame rate limit choices; 0 = unlimited
    public static readonly int[] FrameRateOptions = { 30, 60, 120, 144, 0 };

    public static float MasterVolume { get; private set; }
    public static float MusicVolume { get; private set; }
    public static float SfxVolume { get; private set; }
    public static bool Fullscreen { get; private set; }
    public static int ResolutionWidth { get; private set; }
    public static int ResolutionHeight { get; private set; }
    public static bool VSync { get; private set; }
    public static int FrameRateLimit { get; private set; }
    public static int DasMs { get; private set; }
    public static int ArrMs { get; private set; }
    public static bool ShowGhost { get; private set; }

    public static int AIDifficulty
    {
        get => PlayerPrefs.GetInt(NPCAI.DifficultyPrefKey, (int)NPCAI.Difficulty.Normal);
        set { PlayerPrefs.SetInt(NPCAI.DifficultyPrefKey, Mathf.Clamp(value, 0, 3)); PlayerPrefs.Save(); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadAndApply()
    {
        MasterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume);
        MusicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume);
        SfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, DefaultSfxVolume);
        Fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        ResolutionWidth = PlayerPrefs.GetInt(ResolutionWidthKey, 0);
        ResolutionHeight = PlayerPrefs.GetInt(ResolutionHeightKey, 0);
        VSync = PlayerPrefs.GetInt(VSyncKey, 1) == 1;
        FrameRateLimit = PlayerPrefs.GetInt(FrameRateKey, 0);
        DasMs = PlayerPrefs.GetInt(DasKey, DefaultDasMs);
        ArrMs = PlayerPrefs.GetInt(ArrKey, DefaultArrMs);
        ShowGhost = PlayerPrefs.GetInt(GhostKey, 1) == 1;

        ApplyAudio();
        ApplyDisplay();
    }

    // --- Audio ---
    public static void SetMasterVolume(float value) { MasterVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(MasterVolumeKey, MasterVolume); ApplyAudio(); }
    public static void SetMusicVolume(float value) { MusicVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume); ApplyAudio(); }
    public static void SetSfxVolume(float value) { SfxVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume); ApplyAudio(); }

    public static void ApplyAudio()
    {
        AudioListener.volume = MasterVolume;
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.musicVolume = MusicVolume;
            AudioManager.Instance.sfxVolume = SfxVolume;
        }
    }

    // --- Display ---
    public static void SetFullscreen(bool value) { Fullscreen = value; PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0); ApplyDisplay(); }
    public static void SetResolution(int width, int height)
    {
        ResolutionWidth = width;
        ResolutionHeight = height;
        PlayerPrefs.SetInt(ResolutionWidthKey, width);
        PlayerPrefs.SetInt(ResolutionHeightKey, height);
        ApplyDisplay();
    }
    public static void SetVSync(bool value) { VSync = value; PlayerPrefs.SetInt(VSyncKey, value ? 1 : 0); ApplyDisplay(); }
    public static void SetFrameRateLimit(int fps) { FrameRateLimit = fps; PlayerPrefs.SetInt(FrameRateKey, fps); ApplyDisplay(); }

    public static void ApplyDisplay()
    {
        FullScreenMode mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        if (ResolutionWidth > 0 && ResolutionHeight > 0) Screen.SetResolution(ResolutionWidth, ResolutionHeight, mode);
        else Screen.fullScreenMode = mode;

        QualitySettings.vSyncCount = VSync ? 1 : 0;
        // With VSync on, the monitor sets the pace and the limit is ignored
        Application.targetFrameRate = FrameRateLimit > 0 ? FrameRateLimit : -1;
    }

    // --- Gameplay ---
    public static void SetDasMs(int ms) { DasMs = Mathf.Clamp(ms, MinDasMs, MaxDasMs); PlayerPrefs.SetInt(DasKey, DasMs); }
    public static void SetArrMs(int ms) { ArrMs = Mathf.Clamp(ms, MinArrMs, MaxArrMs); PlayerPrefs.SetInt(ArrKey, ArrMs); }
    public static void SetShowGhost(bool value) { ShowGhost = value; PlayerPrefs.SetInt(GhostKey, value ? 1 : 0); }

    public static void ResetToDefaults()
    {
        SetMasterVolume(DefaultMasterVolume);
        SetMusicVolume(DefaultMusicVolume);
        SetSfxVolume(DefaultSfxVolume);
        SetVSync(true);
        SetFrameRateLimit(0);
        SetDasMs(DefaultDasMs);
        SetArrMs(DefaultArrMs);
        SetShowGhost(true);
        AIDifficulty = (int)NPCAI.Difficulty.Normal;
        PlayerPrefs.Save();
        // Fullscreen and resolution are left alone: resetting them would suddenly resize the window
    }

    public static void Save() => PlayerPrefs.Save();
}
