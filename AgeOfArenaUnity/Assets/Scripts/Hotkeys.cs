using UnityEngine;

/// <summary>
/// HKEY: Persistent, rebindable hotkey map. Actions are identified by
/// <see cref="HotkeyAction"/>; each maps to a <see cref="KeyCode"/> stored in
/// <c>PlayerPrefs</c> so rebindings survive restarts. Call <see cref="Get"/> to
/// look up the current binding at any call-site; call <see cref="Set"/> to rebind.
/// </summary>
public enum HotkeyAction
{
    Stop,           // S — halt selection
    AttackMove,     // A
    Stance,         // Q — cycle attack stance
    Garrison,       // G
    Ungarrison,     // U
    Diplomacy,      // D — toggle diplomacy panel
    SelectIdle,     // .
    AgeAdvance,     // (none — opens TC research)
    BuildMenu,      // B
    Repair,         // H
}

public static class Hotkeys
{
    static readonly KeyCode[] _defaults =
    {
        KeyCode.S,        // Stop
        KeyCode.A,        // AttackMove
        KeyCode.Q,        // Stance
        KeyCode.G,        // Garrison
        KeyCode.U,        // Ungarrison
        KeyCode.D,        // Diplomacy
        KeyCode.Period,   // SelectIdle
        KeyCode.None,     // AgeAdvance (not a global hotkey)
        KeyCode.B,        // BuildMenu
        KeyCode.H,        // Repair
    };

    static readonly KeyCode[] _bindings;

    static Hotkeys()
    {
        _bindings = new KeyCode[_defaults.Length];
        for (int i = 0; i < _defaults.Length; i++)
        {
            string key = "Hotkey_" + (HotkeyAction)i;
            _bindings[i] = PlayerPrefs.HasKey(key)
                ? (KeyCode)PlayerPrefs.GetInt(key)
                : _defaults[i];
        }
    }

    /// <summary>Current binding for the given action.</summary>
    public static KeyCode Get(HotkeyAction action) => _bindings[(int)action];

    /// <summary>Rebind an action. Persists across restarts via PlayerPrefs.</summary>
    public static void Set(HotkeyAction action, KeyCode key)
    {
        _bindings[(int)action] = key;
        PlayerPrefs.SetInt("Hotkey_" + action, (int)key);
        PlayerPrefs.Save();
    }

    /// <summary>Reset a single action to its default binding.</summary>
    public static void Reset(HotkeyAction action)
    {
        Set(action, _defaults[(int)action]);
    }

    /// <summary>Reset all bindings to defaults.</summary>
    public static void ResetAll()
    {
        for (int i = 0; i < _defaults.Length; i++)
            Set((HotkeyAction)i, _defaults[i]);
    }

    /// <summary>True if the key for this action was pressed this frame.</summary>
    public static bool Down(HotkeyAction action)
    {
        var k = Get(action);
        return k != KeyCode.None && Input.GetKeyDown(k);
    }

    /// <summary>N9.hotkeys: which action currently holds this key (null if unbound).
    /// Used by the remap UI to detect conflicts before assigning.</summary>
    public static HotkeyAction? ActionFor(KeyCode key)
    {
        if (key == KeyCode.None) return null;
        for (int i = 0; i < _bindings.Length; i++)
            if (_bindings[i] == key) return (HotkeyAction)i;
        return null;
    }

    /// <summary>N9.hotkeys: rebind, evicting any other action that held the key to None so
    /// no two actions ever share a binding. Returns the evicted action (if any).</summary>
    public static HotkeyAction? Rebind(HotkeyAction action, KeyCode key)
    {
        HotkeyAction? evicted = null;
        if (key != KeyCode.None)
        {
            var holder = ActionFor(key);
            if (holder.HasValue && holder.Value != action)
            {
                evicted = holder;
                Set(holder.Value, KeyCode.None);
            }
        }
        Set(action, key);
        return evicted;
    }

    /// <summary>Number of actions (for iterating in the remap UI).</summary>
    public static int Count => _defaults.Length;
}
