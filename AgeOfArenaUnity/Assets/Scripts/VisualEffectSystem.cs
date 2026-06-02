using System.Collections;
using UnityEngine;

/// <summary>
/// Subscribes to <see cref="GameEvents"/> and drives visual feedback:
/// particle bursts on unit death, camera shake on large building destruction.
/// All effects are purely cosmetic — no gameplay state is changed here.
/// </summary>
public class VisualEffectSystem : MonoBehaviour
{
    // Buildings whose destruction warrants a camera shake.
    static readonly BuildingType[] ShakeBuildings =
    {
        BuildingType.TownCenter, BuildingType.Castle,
        BuildingType.Barracks,   BuildingType.ArcheryRange, BuildingType.Stable,
    };

    void OnEnable()
    {
        GameEvents.OnUnitKilled        += OnUnitKilled;
        GameEvents.OnBuildingDestroyed += OnBuildingDestroyed;
    }

    void OnDisable()
    {
        GameEvents.OnUnitKilled        -= OnUnitKilled;
        GameEvents.OnBuildingDestroyed -= OnBuildingDestroyed;
    }

    void OnUnitKilled(UnitEntity u, int team)
    {
        if (u == null) return;
        SpawnDeathParticle(u.transform.position + Vector3.up * 0.8f);
    }

    void OnBuildingDestroyed(BuildingEntity b, int team)
    {
        if (b == null) return;

        SpawnDeathParticle(b.transform.position + Vector3.up * 1.5f, large: true);

        if (System.Array.IndexOf(ShakeBuildings, b.type) >= 0)
        {
            var rig = GameManager.Instance?.cameraRig;
            if (rig != null) rig.Shake(0.35f, 0.4f);
        }
    }

    static void SpawnDeathParticle(Vector3 pos, bool large = false)
    {
        var go = new GameObject("DeathFX");
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.startLifetime  = large ? 0.8f : 0.5f;
        main.startSpeed     = large ? 5f   : 3f;
        main.startSize      = large ? 0.5f : 0.28f;
        main.startColor     = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.6f, 0.15f, 1f),
            new Color(0.9f, 0.2f, 0.05f, 1f));
        main.maxParticles   = large ? 30 : 14;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        var burst = new ParticleSystem.Burst(0f, large ? 28 : 12);
        emission.SetBursts(new[] { burst });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius    = large ? 0.6f : 0.25f;

        ps.Play();
        // Destroy after particles have faded out.
        Destroy(go, main.startLifetime.constant + 0.2f);
    }
}
