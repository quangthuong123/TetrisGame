using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Drives the Settings panel: shows the saved values when opened and saves every change right away.
// Build the panel with Tools > Tetris > Build Settings Panel (it fills in these references).
public class SettingsMenu : MonoBehaviour
{
    [Header("Audio")]
    public Slider masterSlider;
    public TMP_Text masterValue;
    public Slider musicSlider;
    public TMP_Text musicValue;
    public Slider sfxSlider;
    public TMP_Text sfxValue;

    [Header("Display")]
    public Toggle fullscreenToggle;
    public TMP_Dropdown resolutionDropdown;
    public Toggle vsyncToggle;
    public TMP_Dropdown frameRateDropdown;

    [Header("Gameplay")]
    public Slider dasSlider;
    public TMP_Text dasValue;
    public Slider arrSlider;
    public TMP_Text arrValue;
    public Toggle ghostToggle;
    public TMP_Dropdown aiDifficultyDropdown;

    [Header("Buttons")]
    public Button resetButton;
    public Button backButton;
    [Tooltip("Used to go back to the main menu; if empty the panel just closes")]
    public FusionLauncher launcher;

    private readonly List<Vector2Int> _resolutions = new List<Vector2Int>();

    void Awake()
    {
        Listen(masterSlider, v => { GameSettings.SetMasterVolume(v); ShowPercent(masterValue, v); });
        Listen(musicSlider, v => { GameSettings.SetMusicVolume(v); ShowPercent(musicValue, v); });
        Listen(sfxSlider, v =>
        {
            GameSettings.SetSfxVolume(v);
            ShowPercent(sfxValue, v);
            AudioManager.Play(Sfx.Move); // Preview the new level
        });

        if (fullscreenToggle) fullscreenToggle.onValueChanged.AddListener(GameSettings.SetFullscreen);
        if (vsyncToggle) vsyncToggle.onValueChanged.AddListener(v => { GameSettings.SetVSync(v); RefreshFrameRateInteractable(); });
        if (resolutionDropdown) resolutionDropdown.onValueChanged.AddListener(i =>
        {
            if (i >= 0 && i < _resolutions.Count) GameSettings.SetResolution(_resolutions[i].x, _resolutions[i].y);
        });
        if (frameRateDropdown) frameRateDropdown.onValueChanged.AddListener(i =>
            GameSettings.SetFrameRateLimit(GameSettings.FrameRateOptions[Mathf.Clamp(i, 0, GameSettings.FrameRateOptions.Length - 1)]));

        Listen(dasSlider, v => { GameSettings.SetDasMs(Mathf.RoundToInt(v)); ShowMs(dasValue, v); });
        Listen(arrSlider, v => { GameSettings.SetArrMs(Mathf.RoundToInt(v)); ShowMs(arrValue, v, "instant"); });
        if (ghostToggle) ghostToggle.onValueChanged.AddListener(GameSettings.SetShowGhost);
        if (aiDifficultyDropdown) aiDifficultyDropdown.onValueChanged.AddListener(i => GameSettings.AIDifficulty = i);

        if (resetButton) resetButton.onClick.AddListener(() => { GameSettings.ResetToDefaults(); Refresh(); });
        if (backButton) backButton.onClick.AddListener(Close);
    }

    void OnEnable() => Refresh();

    void OnDisable() => GameSettings.Save();

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    public void Close()
    {
        GameSettings.Save();
        if (launcher != null) launcher.Button_CloseSettings();
        else gameObject.SetActive(false);
    }

    // Puts the saved values into the controls without firing their change events
    public void Refresh()
    {
        SetSlider(masterSlider, 0f, 1f, false, GameSettings.MasterVolume);
        ShowPercent(masterValue, GameSettings.MasterVolume);
        SetSlider(musicSlider, 0f, 1f, false, GameSettings.MusicVolume);
        ShowPercent(musicValue, GameSettings.MusicVolume);
        SetSlider(sfxSlider, 0f, 1f, false, GameSettings.SfxVolume);
        ShowPercent(sfxValue, GameSettings.SfxVolume);

        if (fullscreenToggle) fullscreenToggle.SetIsOnWithoutNotify(GameSettings.Fullscreen);
        if (vsyncToggle) vsyncToggle.SetIsOnWithoutNotify(GameSettings.VSync);
        FillResolutions();
        FillFrameRates();
        RefreshFrameRateInteractable();

        SetSlider(dasSlider, GameSettings.MinDasMs, GameSettings.MaxDasMs, true, GameSettings.DasMs);
        ShowMs(dasValue, GameSettings.DasMs);
        SetSlider(arrSlider, GameSettings.MinArrMs, GameSettings.MaxArrMs, true, GameSettings.ArrMs);
        ShowMs(arrValue, GameSettings.ArrMs, "instant");
        if (ghostToggle) ghostToggle.SetIsOnWithoutNotify(GameSettings.ShowGhost);

        if (aiDifficultyDropdown)
        {
            aiDifficultyDropdown.ClearOptions();
            aiDifficultyDropdown.AddOptions(new List<string> { "Easy", "Normal", "Hard", "Insane" });
            aiDifficultyDropdown.SetValueWithoutNotify(GameSettings.AIDifficulty);
        }
    }

    void FillResolutions()
    {
        if (!resolutionDropdown) return;

        _resolutions.Clear();
        _resolutions.AddRange(Screen.resolutions
            .Select(r => new Vector2Int(r.width, r.height))
            .Distinct()
            .OrderByDescending(r => r.x * r.y));
        if (_resolutions.Count == 0) _resolutions.Add(new Vector2Int(Screen.width, Screen.height));

        int currentW = GameSettings.ResolutionWidth > 0 ? GameSettings.ResolutionWidth : Screen.width;
        int currentH = GameSettings.ResolutionHeight > 0 ? GameSettings.ResolutionHeight : Screen.height;
        int current = _resolutions.FindIndex(r => r.x == currentW && r.y == currentH);
        if (current < 0)
        {
            _resolutions.Insert(0, new Vector2Int(currentW, currentH));
            current = 0;
        }

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(_resolutions.Select(r => $"{r.x} x {r.y}").ToList());
        resolutionDropdown.SetValueWithoutNotify(current);
    }

    void FillFrameRates()
    {
        if (!frameRateDropdown) return;
        frameRateDropdown.ClearOptions();
        frameRateDropdown.AddOptions(GameSettings.FrameRateOptions.Select(f => f > 0 ? f + " FPS" : "Unlimited").ToList());
        int index = System.Array.IndexOf(GameSettings.FrameRateOptions, GameSettings.FrameRateLimit);
        frameRateDropdown.SetValueWithoutNotify(index >= 0 ? index : GameSettings.FrameRateOptions.Length - 1);
    }

    // VSync overrides the frame rate limit, so grey it out while VSync is on
    void RefreshFrameRateInteractable()
    {
        if (frameRateDropdown) frameRateDropdown.interactable = !GameSettings.VSync;
    }

    static void Listen(Slider slider, UnityEngine.Events.UnityAction<float> action)
    {
        if (slider) slider.onValueChanged.AddListener(action);
    }

    static void SetSlider(Slider slider, float min, float max, bool whole, float value)
    {
        if (!slider) return;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = whole;
        slider.SetValueWithoutNotify(value);
    }

    static void ShowPercent(TMP_Text text, float value)
    {
        if (text) text.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    static void ShowMs(TMP_Text text, float ms, string zeroLabel = null)
    {
        if (!text) return;
        int rounded = Mathf.RoundToInt(ms);
        text.text = rounded == 0 && zeroLabel != null ? zeroLabel : rounded + " ms";
    }
}
