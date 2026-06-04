using UnityEngine;

/// <summary>
/// Singleton audio manager. Call <see cref="Play"/> from anywhere with a
/// <see cref="SoundId"/> — sounds load from <c>Resources/Audio/</c> on first use
/// and are played via a small <see cref="AudioSource"/> pool so simultaneous hits
/// don't cut each other off.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public enum SoundId
    {
        Sword, Arrow, BuildComplete, UnitTrained, UnitDie, ButtonClick,
        UnitSelect,   // SUBT: generic unit selection
        UnitMove,     // SUBT: unit move-order confirm
        UnitVillager, // SUBT: villager-specific select
        AgeUp         // AGFX: age advance fanfare
    }

    const int PoolSize = 10;

    static AudioManager _instance;
    AudioSource[] _pool;
    int _poolIdx;

    readonly AudioClip[] _clips = new AudioClip[System.Enum.GetValues(typeof(SoundId)).Length];

    static readonly string[] Paths =
    {
        "Audio/sword",         // Sword
        "Audio/arrow",         // Arrow
        "Audio/build_complete",// BuildComplete
        "Audio/unit_trained",  // UnitTrained
        "Audio/unit_die",      // UnitDie
        "Audio/button_click",  // ButtonClick
        "Audio/unit_select",   // UnitSelect
        "Audio/unit_select",   // UnitMove (reuse — swap when unique clip available)
        "Audio/unit_select",   // UnitVillager (reuse until villager clip exists)
        "Audio/unit_trained",  // AgeUp (reuse unit_trained as placeholder)
    };

    public static void Play(SoundId id, float volumeScale = 1f)
    {
        if (_instance == null) return;
        _instance.PlaySound(id, volumeScale);
    }

    public static void Init()
    {
        if (_instance != null) return;
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

        // Preload all clips.
        for (int i = 0; i < Paths.Length; i++)
            _clips[i] = Resources.Load<AudioClip>(Paths[i]);
    }

    void PlaySound(SoundId id, float vol)
    {
        var clip = _clips[(int)id];
        if (clip == null) return;

        var src = _pool[_poolIdx];
        _poolIdx = (_poolIdx + 1) % PoolSize;
        src.PlayOneShot(clip, vol * 0.7f);   // 0.7 global attenuation — not too loud for RTS
    }
}
