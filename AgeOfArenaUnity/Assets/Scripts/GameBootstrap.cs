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
    static void Boot() => BuildIfNeeded();

    static void BuildIfNeeded()
    {
        if (Object.FindAnyObjectByType<WorldRoot>() != null) return; // already built
        var root = new GameObject("AgeOfArena").AddComponent<WorldRoot>();
        root.Build();
    }

    /// <summary>
    /// Rebuilds the arena in place (used by the game-over restart). The project has no
    /// authored scene in the build settings, so <c>SceneManager.LoadScene</c> is unreliable
    /// and <see cref="RuntimeInitializeOnLoadMethod"/> doesn't re-fire on reload — instead we
    /// destroy the existing world root and rebuild on the next frame via <see cref="RebuildKick"/>.
    /// </summary>
    public static void Restart()
    {
        Time.timeScale = 1f;
        GameEvents.Reset();

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
