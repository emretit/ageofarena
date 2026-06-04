using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Runtime entry point. Fires automatically when you press Play in ANY scene
/// (including an empty one), so no .unity scene asset needs to be hand-authored.
/// Builds the whole arena via <see cref="WorldRoot"/>.
/// </summary>
public static class GameBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        Loc.LoadSaved();                 // N9.i18n
        AccessibilitySettings.Load();    // N9.a11y
        BuildIfNeeded();
    }

    static void BuildIfNeeded()
    {
        if (Object.FindAnyObjectByType<WorldRoot>() != null) return; // already built
        var root = new GameObject("AgeOfArena").AddComponent<WorldRoot>();
        root.gameObject.AddComponent<FocusPause>(); // N9: pause the sim when the window loses focus
        root.mapSeed = _nextSeed;
        root.Build();
    }

    /// <summary>
    /// Rebuilds the arena in place (used by the game-over restart). The project has no
    /// authored scene in the build settings, so <c>SceneManager.LoadScene</c> is unreliable
    /// and <see cref="RuntimeInitializeOnLoadMethod"/> doesn't re-fire on reload — instead we
    /// destroy the existing world root and rebuild on the next frame via <see cref="RebuildKick"/>.
    /// </summary>
    static int _nextSeed; // 0 = pick fresh in WorldRoot.Build

    /// <summary>CIVS: player's chosen civilization (team 0). Persists across restarts so the
    /// civ-select screen only needs to be answered once. None until the player picks.</summary>
    public static Civilization PlayerCiv = Civilization.None;

    /// <summary>GMODE-ENUM: game mode for the next (or current) match.</summary>
    public static GameMode NextGameMode = GameMode.Random;

    /// <summary>SAVF: if non-null, WorldRoot applies this snapshot instead of the default spawn.</summary>
    public static SaveSystem.SaveData PendingLoad;

    /// <summary>ARES: difficulty for the next match. Persists across restarts.</summary>
    public static Difficulty NextDifficulty = Difficulty.Normal;

    /// <summary>Restart with a fresh random seed so the next map looks different.</summary>
    public static void Restart(int seed = 0)
    {
        _nextSeed = seed; // 0 → WorldRoot picks one
        Time.timeScale = 1f;
        GameEvents.Reset();
        Prims.ClearMatCache();   // N1.mat: rebuild shared material cache for fresh scene

        var existing = Object.FindAnyObjectByType<WorldRoot>();
        if (existing != null) Object.Destroy(existing.gameObject);
        NavMesh.RemoveAllNavMeshData(); // avoid stacking nav data on the next bake

        // Root-level helper (NOT a child of the world root) so the Destroy above
        // doesn't take it down before it can rebuild next frame.
        new GameObject("RebuildKick").AddComponent<RebuildKick>();
    }

    /// <summary>One-shot: waits one frame for the old world to finish tearing down, rebuilds, self-destructs.</summary>
    class RebuildKick : MonoBehaviour
    {
        void Update()
        {
            BuildIfNeeded();
            Destroy(gameObject);
        }
    }
}
