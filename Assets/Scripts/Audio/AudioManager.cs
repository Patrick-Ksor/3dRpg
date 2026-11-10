using UnityEngine;
using UnityEngine.UIElements;

public enum Sfx
{
    UiHover, UiClick, UiConfirm, UiBack, UiDenied, Pause, Unpause,
    EncounterStart, Miss, Block, EnemyDeath, Heal, Buff, Debuff, ItemUse, Victory, Defeat, Footstep,
}

/// <summary>
/// Central audio: crossfading music, pooled sound effects, and saved Music / Effects volume.
/// All game sounds go through here so the volume sliders cover them. Clips come from Resources/AudioLibrary.
/// </summary>
public class AudioManager : MonoBehaviour
{
    private const string KeyMusic = "vol_music";
    private const string KeySfx = "vol_sfx";
    private const float MusicGain = 0.45f;     // music sits under the effects at equal slider positions
    private const float FadeSeconds = 1.2f;
    private const int PoolSize = 10;

    private static AudioManager s_Instance;

    private AudioLibrary _library;
    private AudioSource[] _pool;
    private int _poolIndex;
    private AudioSource _musicA, _musicB;
    private bool _aActive = true;
    private AudioClip _currentMusic;
    private float _musicVolume = 0.8f;
    private float _sfxVolume = 0.9f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot() => Get();

    private static AudioManager Get()
    {
        if (s_Instance != null) return s_Instance;

        var go = new GameObject("AudioManager");
        DontDestroyOnLoad(go);
        s_Instance = go.AddComponent<AudioManager>();
        s_Instance.Init();
        return s_Instance;
    }

    private void Init()
    {
        _library = Resources.Load<AudioLibrary>("AudioLibrary");
        if (_library == null) Debug.LogWarning("[AudioManager] Resources/AudioLibrary is missing; the game will be silent.");

        _musicVolume = PlayerPrefs.GetFloat(KeyMusic, 0.8f);
        _sfxVolume = PlayerPrefs.GetFloat(KeySfx, 0.9f);

        _pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            _pool[i] = gameObject.AddComponent<AudioSource>();
            _pool[i].playOnAwake = false;
        }

        _musicA = gameObject.AddComponent<AudioSource>();
        _musicB = gameObject.AddComponent<AudioSource>();
        foreach (var m in new[] { _musicA, _musicB })
        {
            m.playOnAwake = false;
            m.loop = true;
            m.volume = 0f;
        }
    }

    // ── Volumes ─────────────────────────────────────────────────────────────────

    public static float MusicVolume
    {
        get => Get()._musicVolume;
        set { var m = Get(); m._musicVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KeyMusic, m._musicVolume); }
    }

    public static float SfxVolume
    {
        get => Get()._sfxVolume;
        set { var m = Get(); m._sfxVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KeySfx, m._sfxVolume); }
    }

    // ── Music ───────────────────────────────────────────────────────────────────

    public static void PlayMenuMusic() => Get().PlayMusic(Get()._library != null ? Get()._library.menuMusic : null);

    public static void PlayBattleMusic(int encounter, bool boss)
    {
        var m = Get();
        if (m._library == null) return;

        AudioClip clip;
        if (boss && m._library.bossMusic != null) clip = m._library.bossMusic;
        else
        {
            var list = m._library.battleMusic;
            clip = list != null && list.Length > 0 ? list[(RunManager.GetEncounterTier(encounter) - 1) % list.Length] : null;
        }
        m.PlayMusic(clip);
    }

    public static void StopMusic() => Get().PlayMusic(null);

    private void PlayMusic(AudioClip clip)
    {
        if (clip == _currentMusic) return;
        _currentMusic = clip;

        _aActive = !_aActive;                         // the other source takes over and fades in
        AudioSource incoming = _aActive ? _musicA : _musicB;
        if (clip != null)
        {
            incoming.clip = clip;
            incoming.volume = 0f;
            incoming.Play();
        }
    }

    private void Update()
    {
        // Fade the active source to the target level and the other one out; unscaled so it works while paused.
        float target = _musicVolume * MusicGain;
        AudioSource active = _aActive ? _musicA : _musicB;
        AudioSource other = _aActive ? _musicB : _musicA;
        float step = (MusicGain / FadeSeconds) * Time.unscaledDeltaTime;

        if (_currentMusic != null) active.volume = Mathf.MoveTowards(active.volume, target, Mathf.Max(step, Time.unscaledDeltaTime));   // fade in, but follow the slider quickly
        else active.volume = Mathf.MoveTowards(active.volume, 0f, step);

        other.volume = Mathf.MoveTowards(other.volume, 0f, step);
        if (other.volume <= 0f && other.isPlaying) other.Stop();
        if (_currentMusic == null && active.volume <= 0f && active.isPlaying) active.Stop();
    }

    // ── Sound effects ───────────────────────────────────────────────────────────

    public static void Play(Sfx sfx, float volume = 1f, float pitchJitter = 0.04f)
    {
        var m = Get();
        PlayClip(m.Clip(sfx), volume, pitchJitter);
    }

    /// <summary>Plays any clip as a 2D effect at the Effects volume (used by attack and unit sounds).</summary>
    public static void PlayClip(AudioClip clip, float volume = 1f, float pitchJitter = 0f)
    {
        if (clip == null) return;
        var m = Get();
        AudioSource src = m._pool[m._poolIndex];
        m._poolIndex = (m._poolIndex + 1) % PoolSize;
        src.clip = clip;
        src.volume = Mathf.Clamp01(volume * m._sfxVolume);
        src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        src.Play();
    }

    private AudioClip Clip(Sfx sfx)
    {
        if (_library == null) return null;
        switch (sfx)
        {
            case Sfx.UiHover: return _library.uiHover;
            case Sfx.UiClick: return _library.uiClick;
            case Sfx.UiConfirm: return _library.uiConfirm;
            case Sfx.UiBack: return _library.uiBack;
            case Sfx.UiDenied: return _library.uiDenied;
            case Sfx.Pause: return _library.pause;
            case Sfx.Unpause: return _library.unpause;
            case Sfx.EncounterStart: return _library.encounterStart;
            case Sfx.Miss: return _library.miss;
            case Sfx.Block: return _library.block;
            case Sfx.EnemyDeath: return _library.enemyDeath;
            case Sfx.Heal: return _library.heal;
            case Sfx.Buff: return _library.buff;
            case Sfx.Debuff: return _library.debuff;
            case Sfx.ItemUse: return _library.itemUse;
            case Sfx.Victory: return _library.victory;
            case Sfx.Defeat: return _library.defeat;
            case Sfx.Footstep: return _library.footstep;
            default: return null;
        }
    }

    // ── UI sounds for any UI Toolkit tree ───────────────────────────────────────

    private static VisualElement s_LastHover;

    /// <summary>
    /// Hover and click sounds for buttons, reward / path cards, hero entries and enemy plates under
    /// <paramref name="root"/>. Safe to call once per root.
    /// </summary>
    public static void AttachUiSounds(VisualElement root)
    {
        if (root == null || root.ClassListContains("ui-sounds")) return;
        root.AddToClassList("ui-sounds");

        root.RegisterCallback<PointerOverEvent>(evt =>
        {
            VisualElement el = FindInteractive(evt.target as VisualElement);
            if (el == null || el == s_LastHover) return;
            s_LastHover = el;
            if (el.enabledInHierarchy) Play(Sfx.UiHover, 0.35f, 0.02f);
        }, TrickleDown.TrickleDown);

        root.RegisterCallback<PointerOutEvent>(evt =>
        {
            if (FindInteractive(evt.target as VisualElement) == s_LastHover) s_LastHover = null;
        }, TrickleDown.TrickleDown);

        root.RegisterCallback<ClickEvent>(evt =>
        {
            VisualElement el = FindInteractive(evt.target as VisualElement);
            if (el == null || !el.enabledInHierarchy) return;

            var btn = el as Button;
            if (btn != null && (btn.text == "Back" || btn.ClassListContains("quit-btn") || btn.ClassListContains("pause-resume")))
                Play(Sfx.UiBack, 0.7f, 0f);
            else if (el.ClassListContains("reward-button") || el.ClassListContains("claim-btn") || el.ClassListContains("start-btn") || el.ClassListContains("loot-take"))
                Play(Sfx.UiConfirm, 0.8f, 0f);
            else
                Play(Sfx.UiClick, 0.7f, 0.02f);
        }, TrickleDown.TrickleDown);
    }

    private static VisualElement FindInteractive(VisualElement e)
    {
        for (; e != null; e = e.parent)
        {
            if (e is Button) return e;
            if (e.ClassListContains("reward-card--pick") || e.ClassListContains("hero-entry") || e.ClassListContains("np--enemy")) return e;
        }
        return null;
    }

    // ── Volume sliders (main menu and pause screen) ─────────────────────────────

    /// <summary>A small "Music / Effects" panel bound to the saved volumes.</summary>
    public static VisualElement BuildVolumePanel()
    {
        var panel = new VisualElement();
        panel.AddToClassList("vol-panel");
        var sheet = Resources.Load<StyleSheet>("UI/VolumePanel");
        if (sheet != null) panel.styleSheets.Add(sheet);

        panel.Add(MakeRow("MUSIC", MusicVolume, v => MusicVolume = v, false));
        panel.Add(MakeRow("EFFECTS", SfxVolume, v => { SfxVolume = v; }, true));
        return panel;
    }

    private static VisualElement MakeRow(string label, float value, System.Action<float> onChange, bool tick)
    {
        var row = new VisualElement();
        row.AddToClassList("vol-row");

        var l = new Label(label);
        l.AddToClassList("vol-label");
        row.Add(l);

        var slider = new Slider(0f, 1f) { value = value };
        slider.AddToClassList("vol-slider");
        float lastTick = 0f;
        slider.RegisterValueChangedCallback(evt =>
        {
            onChange(evt.newValue);
            // Let the player hear the new effects level while dragging (not more than ~8 times a second).
            if (tick && Time.unscaledTime - lastTick > 0.12f)
            {
                lastTick = Time.unscaledTime;
                Play(Sfx.UiHover, 1f, 0.05f);
            }
        });
        row.Add(slider);
        return row;
    }
}
