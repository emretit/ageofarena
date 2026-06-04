using UnityEngine;

/// <summary>
/// Singleton audio manager. Call <see cref="Play"/> from anywhere with a
/// <see cref="SoundId"/>. If no clip file exists in Resources/Audio/, a
/// procedural PCM tone is generated at runtime (no audio assets required).
/// </summary>
public class AudioManager : MonoBehaviour
{
    public enum SoundId
    {
        Sword, Arrow, BuildComplete, UnitTrained, UnitDie, ButtonClick,
        UnitSelect,   // SUBT: generic unit selection
        UnitMove,     // SUBT: unit move-order confirm
        UnitVillager, // SUBT: villager-specific select
        AgeUp,        // AGFX: age advance fanfare
        // N7.sfx additions
        Gather,       // villager hits a resource node
        Research,     // tech research complete
        Ping,         // minimap / attack ping
        Repair,       // villager repairing a building
    }

    const int PoolSize = 12;   // N7.sfx: extra pool slots for new sounds
    const int SampleRate = 22050;

    // N7.sfx: pitch variation range ±PitchJitter per shot — avoids "machine gun" sameness.
    const float PitchJitter = 0.10f;

    // N7.sfx: per-SoundId round-robin counter to cycle between variant takes (future: load _1, _2, _3).
    readonly int[] _rrIdx = new int[System.Enum.GetValues(typeof(SoundId)).Length];

    static AudioManager _instance;
    AudioSource[] _pool;
    int _poolIdx;

    readonly AudioClip[] _clips = new AudioClip[System.Enum.GetValues(typeof(SoundId)).Length];

    // Resources paths — tried first; procedural fallback if null.
    static readonly string[] Paths =
    {
        "Audio/sword",          // Sword
        "Audio/arrow",          // Arrow
        "Audio/build_complete", // BuildComplete
        "Audio/unit_trained",   // UnitTrained
        "Audio/unit_die",       // UnitDie
        "Audio/button_click",   // ButtonClick
        "Audio/unit_select",    // UnitSelect
        "Audio/unit_select",    // UnitMove
        "Audio/unit_select",    // UnitVillager
        "Audio/unit_trained",   // AgeUp
        "Audio/gather",         // Gather      (N7.sfx)
        "Audio/research",       // Research    (N7.sfx)
        "Audio/ping",           // Ping        (N7.sfx)
        "Audio/build_complete", // Repair      (N7.sfx — reuse build_complete pitch-shifted)
    };

    // N7.spatial: volume controls — Master (all) × SFX (shots/UI). Music handled separately.
    static float _masterVol  = 1f;
    static float _sfxVol     = 1f;
    static float _musicVol   = 0.55f;

    public static float MasterVolume { get => _masterVol; set { _masterVol = Mathf.Clamp01(value); PlayerPrefs.SetFloat("Audio.Master", _masterVol); PlayerPrefs.Save(); ApplyMusicVolume(); } }
    public static float SfxVolume    { get => _sfxVol;    set { _sfxVol    = Mathf.Clamp01(value); PlayerPrefs.SetFloat("Audio.SFX",    _sfxVol);    PlayerPrefs.Save(); } }
    public static float MusicVolume  { get => _musicVol;  set { _musicVol  = Mathf.Clamp01(value); PlayerPrefs.SetFloat("Audio.Music",  _musicVol);  PlayerPrefs.Save(); ApplyMusicVolume(); } }

    static void ApplyMusicVolume()
    {
        if (_instance != null && _instance._musicSrc != null)
            _instance._musicSrc.volume = _masterVol * _musicVol * _instance._duckFactor;
    }

    public static void LoadVolumes()
    {
        _masterVol = PlayerPrefs.GetFloat("Audio.Master", 1f);
        _sfxVol    = PlayerPrefs.GetFloat("Audio.SFX",    1f);
        _musicVol  = PlayerPrefs.GetFloat("Audio.Music",  0.55f);
    }

    // N7.music: dedicated looping music source + combat duck
    AudioSource _musicSrc;
    AudioClip   _musicClip;
    float       _duckFactor   = 1f;      // lowered during combat (duck)
    const float DuckTarget    = 0.35f;   // ducked volume fraction during combat
    const float DuckSpeed     = 2.0f;    // lerp speed for duck/unduck
    Age         _lastAge      = (Age)(-1);
    bool        _combatActive;

    /// <summary>N7.music: play the age-appropriate background track. Called by HUD on age advance.</summary>
    public static void PlayMusicForAge(Age age)
    {
        if (_instance == null) return;
        _instance.StartMusicTrack(age);
    }

    /// <summary>N7.music: signal combat start/end so music ducks during battles.</summary>
    public static void SetCombatActive(bool active)
    {
        if (_instance == null) return;
        _instance._combatActive = active;
    }

    void StartMusicTrack(Age age)
    {
        if (age == _lastAge) return;
        _lastAge = age;

        // Try to load a file from Resources/Audio/music_<age>; fall back to procedural.
        string path = "Audio/music_" + age.ToString().ToLower();
        var clip = Resources.Load<AudioClip>(path) ?? MakeMusicClip(age);

        if (_musicSrc == null)
        {
            _musicSrc       = gameObject.AddComponent<AudioSource>();
            _musicSrc.loop  = true;
        }
        _musicSrc.clip   = clip;
        _musicSrc.volume = _masterVol * _musicVol;
        _musicSrc.Play();
    }

    void Update()
    {
        if (_musicSrc == null) return;
        float target = _combatActive ? DuckTarget : 1f;
        _duckFactor = Mathf.Lerp(_duckFactor, target, Time.unscaledDeltaTime * DuckSpeed);
        _musicSrc.volume = _masterVol * _musicVol * _duckFactor;
    }

    public static void Play(SoundId id, float volumeScale = 1f)
    {
        if (_instance == null) return;
        _instance.PlaySound(id, volumeScale);
    }

    public static void Init()
    {
        if (_instance != null) return;
        LoadVolumes();
        var go = new GameObject("AudioManager");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<AudioManager>();
        _instance.Bootstrap();
    }

    void Bootstrap()
    {
        _pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
            _pool[i] = gameObject.AddComponent<AudioSource>();

        for (int i = 0; i < Paths.Length; i++)
            _clips[i] = Resources.Load<AudioClip>(Paths[i]) ?? MakeClip((SoundId)i);

        // N7.music: start Dark Age ambient immediately
        StartMusicTrack(Age.Dark);
    }

    // N7.music: procedural ambient loop per age (simple harmonic drone, 4 bars × 2s).
    static AudioClip MakeMusicClip(Age age)
    {
        // Base drones that evolve per age (lower = darker, higher = grander).
        float[] freqs = age switch
        {
            Age.Dark     => new[] { 110f, 138f, 165f },     // A2-C#3-E3 minor
            Age.Feudal   => new[] { 146f, 185f, 220f },     // D3-F#3-A3
            Age.Castle   => new[] { 196f, 247f, 294f },     // G3-B3-D4
            Age.Imperial => new[] { 220f, 277f, 330f },     // A3-C#4-E4 major
            _            => new[] { 110f, 138f, 165f },
        };

        float dur = 8f; // 8-second loop
        int total = Mathf.RoundToInt(SampleRate * dur);
        float[] data = new float[total];

        for (int n = 0; n < total; n++)
        {
            float t   = (float)n / SampleRate;
            float env = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / dur); // slow swell
            float s   = 0f;
            foreach (float f in freqs)
            {
                s += Mathf.Sin(2f * Mathf.PI * f * t) * 0.25f;
                s += Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.06f; // 2nd harmonic
            }
            data[n] = s * env * 0.18f;
        }
        return FromData("music_" + age, data);
    }

    void PlaySound(SoundId id, float vol)
    {
        var clip = _clips[(int)id];
        if (clip == null) return;
        var src = _pool[_poolIdx];
        _poolIdx = (_poolIdx + 1) % PoolSize;
        // N7.sfx: pitch variation ±PitchJitter to avoid "machine gun" identical repeats.
        src.pitch = 1f + Random.Range(-PitchJitter, PitchJitter);
        src.PlayOneShot(clip, vol * 0.65f * _masterVol * _sfxVol);
        // Advance round-robin counter for this SoundId (used if variant clips are loaded later).
        int idx = (int)id;
        _rrIdx[idx] = (_rrIdx[idx] + 1) % 3;
    }

    // ── Procedural clip generation ────────────────────────────────────────────

    static AudioClip MakeClip(SoundId id)
    {
        switch (id)
        {
            case SoundId.Sword:        return Tone(180f, 0.12f, 0.0f, 0.12f, wave: Wave.Square, freqMult: 0.6f);
            case SoundId.Arrow:        return Sweep(800f, 200f, 0.18f);
            case SoundId.BuildComplete:return Ding(660f, 0.35f);
            case SoundId.UnitTrained:  return Ding(440f, 0.25f);
            case SoundId.UnitDie:      return Sweep(400f, 80f, 0.30f);
            case SoundId.ButtonClick:  return Tone(1200f, 0.04f, 0f, 0.04f, wave: Wave.Sine);
            case SoundId.UnitSelect:   return Tone(560f,  0.07f, 0f, 0.07f, wave: Wave.Sine);
            case SoundId.UnitMove:     return Tone(480f,  0.07f, 0f, 0.07f, wave: Wave.Sine);
            case SoundId.UnitVillager: return Tone(520f,  0.07f, 0f, 0.07f, wave: Wave.Sine);
            case SoundId.AgeUp:        return Fanfare();
            // N7.sfx new sounds
            case SoundId.Gather:       return Tone(320f, 0.01f, 0f, 0.06f, wave: Wave.Square, freqMult: 0.8f);
            case SoundId.Research:     return Ding(550f, 0.40f);
            case SoundId.Ping:         return Tone(880f, 0.01f, 0f, 0.18f, wave: Wave.Sine);
            case SoundId.Repair:       return Tone(260f, 0.02f, 0f, 0.10f, wave: Wave.Triangle);
            default:                   return Tone(440f, 0.1f, 0f, 0.1f, wave: Wave.Sine);
        }
    }

    enum Wave { Sine, Square, Triangle }

    // Simple pitched tone with linear attack + decay envelope.
    static AudioClip Tone(float freq, float attackSec, float sustainSec, float decaySec,
                          Wave wave = Wave.Sine, float freqMult = 1f)
    {
        int total = Mathf.RoundToInt(SampleRate * (attackSec + sustainSec + decaySec));
        int atkSamples  = Mathf.RoundToInt(SampleRate * attackSec);
        int susSamples  = Mathf.RoundToInt(SampleRate * sustainSec);
        float[] data = new float[total];
        for (int n = 0; n < total; n++)
        {
            float t = (float)n / SampleRate;
            float env;
            if (n < atkSamples)          env = (float)n / Mathf.Max(1, atkSamples);
            else if (n < atkSamples + susSamples) env = 1f;
            else                         env = 1f - (float)(n - atkSamples - susSamples) / Mathf.Max(1, total - atkSamples - susSamples);
            float phase = 2f * Mathf.PI * freq * freqMult * t;
            float s = wave == Wave.Square   ? (Mathf.Sin(phase) >= 0 ? 1f : -1f)
                    : wave == Wave.Triangle ? (2f / Mathf.PI) * Mathf.Asin(Mathf.Sin(phase))
                    :                         Mathf.Sin(phase);
            data[n] = s * env * 0.4f;
        }
        return FromData("proc_" + wave, data);
    }

    // Frequency sweep from startHz down to endHz.
    static AudioClip Sweep(float startHz, float endHz, float dur)
    {
        int total = Mathf.RoundToInt(SampleRate * dur);
        float[] data = new float[total];
        float phase = 0f;
        for (int n = 0; n < total; n++)
        {
            float frac = (float)n / total;
            float freq = Mathf.Lerp(startHz, endHz, frac);
            phase += 2f * Mathf.PI * freq / SampleRate;
            float env = 1f - frac; // linear decay
            data[n] = Mathf.Sin(phase) * env * 0.4f;
        }
        return FromData("proc_sweep", data);
    }

    // Bell-like tone with fast attack + long decay.
    static AudioClip Ding(float freq, float dur)
    {
        int total = Mathf.RoundToInt(SampleRate * dur);
        float[] data = new float[total];
        for (int n = 0; n < total; n++)
        {
            float t = (float)n / SampleRate;
            float env = Mathf.Exp(-t * 6f);
            float harm = Mathf.Sin(2f * Mathf.PI * freq * t)
                       + 0.3f * Mathf.Sin(2f * Mathf.PI * freq * 2f * t);
            data[n] = harm * env * 0.35f;
        }
        return FromData("proc_ding", data);
    }

    // AgeUp: four ascending notes played sequentially.
    static AudioClip Fanfare()
    {
        float[] notes = { 440f, 550f, 660f, 880f };
        float noteLen = 0.18f;
        int perNote = Mathf.RoundToInt(SampleRate * noteLen);
        int total   = perNote * notes.Length;
        float[] data = new float[total];
        for (int i = 0; i < notes.Length; i++)
        {
            float freq = notes[i];
            for (int n = 0; n < perNote; n++)
            {
                float t = (float)n / SampleRate;
                float env = n < perNote * 0.15f
                    ? (float)n / (perNote * 0.15f)
                    : Mathf.Exp(-(t - noteLen * 0.15f) * 4f);
                data[i * perNote + n] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.4f;
            }
        }
        return FromData("proc_fanfare", data);
    }

    static AudioClip FromData(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
