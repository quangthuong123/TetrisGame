using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public enum Sfx
{
    Move, Rotate, HardDrop, Lock, Hold,
    LineClear, Tetris, SkillUsed, AttackReceived, GarbageRise,
    CountdownTick, CountdownGo, Win, Lose, GameOver, NewHighScore
}

// Plays all music and sound effects. Lives across scenes and creates itself at startup from
// Assets/Resources/AudioManager.prefab, so drop your clips into that prefab's slots.
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private const int MenuSceneBuildIndex = 0;

    [Header("Background Music")]
    public AudioClip menuMusic;
    public AudioClip gameMusic;
    [Tooltip("Optional: used in single player instead of Game Music")]
    public AudioClip singlePlayerMusic;
    [Range(0f, 1f)] public float musicVolume = 0.5f;

    [Header("Gameplay Sounds")]
    public AudioClip moveSound;
    public AudioClip rotateSound;
    public AudioClip hardDropSound;
    public AudioClip lockSound;
    public AudioClip holdSound;
    public AudioClip lineClearSound;
    public AudioClip tetrisSound;

    [Header("Versus Sounds")]
    public AudioClip skillUsedSound;
    public AudioClip attackReceivedSound;
    public AudioClip garbageRiseSound;

    [Header("Match Sounds")]
    public AudioClip countdownTickSound;
    public AudioClip countdownGoSound;
    public AudioClip winSound;
    public AudioClip loseSound;
    public AudioClip gameOverSound;
    public AudioClip newHighScoreSound;

    [Header("Mixing")]
    [Range(0f, 1f)] public float sfxVolume = 0.8f;
    [Tooltip("Small random pitch change so repeated sounds don't feel robotic")]
    [Range(0f, 0.2f)] public float pitchVariation = 0.05f;
    [Tooltip("Optional: route through your Audio Mixer groups")]
    public AudioMixerGroup musicMixerGroup;
    public AudioMixerGroup sfxMixerGroup;

    private const int SfxVoices = 8; // Rotating sources so per-sound pitch doesn't bend sounds already playing
    private AudioSource _musicSource;
    private readonly AudioSource[] _sfxSources = new AudioSource[SfxVoices];
    private int _nextSfxSource;
    private AudioClip _lastClip;
    private int _lastClipFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        AudioManager prefab = Resources.Load<AudioManager>("AudioManager");
        if (prefab != null) Instantiate(prefab).name = "AudioManager";
        else new GameObject("AudioManager").AddComponent<AudioManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.loop = true;
        _musicSource.playOnAwake = false;
        _musicSource.outputAudioMixerGroup = musicMixerGroup;

        for (int i = 0; i < SfxVoices; i++)
        {
            _sfxSources[i] = gameObject.AddComponent<AudioSource>();
            _sfxSources[i].playOnAwake = false;
            _sfxSources[i].outputAudioMixerGroup = sfxMixerGroup;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        PlayMusicForScene(SceneManager.GetActiveScene());
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    void Update()
    {
        // Lets the volume slider in the inspector work live
        _musicSource.volume = musicVolume;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => PlayMusicForScene(scene);

    private void PlayMusicForScene(Scene scene)
    {
        AudioClip clip;
        if (scene.buildIndex == MenuSceneBuildIndex) clip = menuMusic;
        else if (singlePlayerMusic != null && FindFirstObjectByType<NPCAI>() != null) clip = singlePlayerMusic;
        else clip = gameMusic;
        PlayMusic(clip);
    }

    public void PlayMusic(AudioClip clip)
    {
        if (_musicSource.clip == clip && _musicSource.isPlaying) return; // Keep playing across same-music scenes
        _musicSource.clip = clip;
        _musicSource.volume = musicVolume;
        if (clip != null) _musicSource.Play();
        else _musicSource.Stop();
    }

    public static void Play(Sfx sfx)
    {
        if (Instance != null) Instance.PlayClip(Instance.ClipFor(sfx));
    }

    public void PlayClip(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null) return;
        // The same sound twice in one frame just gets louder, so skip the duplicate
        if (clip == _lastClip && Time.frameCount == _lastClipFrame) return;
        _lastClip = clip;
        _lastClipFrame = Time.frameCount;

        AudioSource source = _sfxSources[_nextSfxSource];
        _nextSfxSource = (_nextSfxSource + 1) % SfxVoices;
        source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        source.PlayOneShot(clip, sfxVolume * volumeScale);
    }

    private AudioClip ClipFor(Sfx sfx)
    {
        switch (sfx)
        {
            case Sfx.Move: return moveSound;
            case Sfx.Rotate: return rotateSound;
            case Sfx.HardDrop: return hardDropSound;
            case Sfx.Lock: return lockSound;
            case Sfx.Hold: return holdSound;
            case Sfx.LineClear: return lineClearSound;
            case Sfx.Tetris: return tetrisSound != null ? tetrisSound : lineClearSound;
            case Sfx.SkillUsed: return skillUsedSound;
            case Sfx.AttackReceived: return attackReceivedSound;
            case Sfx.GarbageRise: return garbageRiseSound;
            case Sfx.CountdownTick: return countdownTickSound;
            case Sfx.CountdownGo: return countdownGoSound;
            case Sfx.Win: return winSound;
            case Sfx.Lose: return loseSound;
            case Sfx.GameOver: return gameOverSound;
            case Sfx.NewHighScore: return newHighScoreSound;
            default: return null;
        }
    }
}
