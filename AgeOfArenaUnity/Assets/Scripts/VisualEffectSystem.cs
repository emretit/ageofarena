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

    // Per-building smoke emitter: visible when hp < 50%.
    readonly System.Collections.Generic.Dictionary<BuildingEntity, ParticleSystem> _smoke = new();

    float _smokeCheck = 0f;

    void Update()
    {
        // Throttle — check every 1s.
        _smokeCheck -= Time.deltaTime;
        if (_smokeCheck > 0f) return;
        _smokeCheck = 1f;

        var gm = GameManager.Instance;
        if (gm == null) return;

        foreach (var b in gm.buildings)
        {
            if (b == null || b.hp <= 0f) continue;
            bool damaged = b.hp < b.maxHp * 0.5f;
            _smoke.TryGetValue(b, out var ps);

            if (damaged && ps == null)
            {
                ps = MakeSmokeEmitter(b.transform.position + Vector3.up * 2.2f);
                ps.transform.SetParent(b.transform, true);
                _smoke[b] = ps;
            }
            else if (!damaged && ps != null)
            {
                Destroy(ps.gameObject);
                _smoke.Remove(b);
            }
        }
    }

    static ParticleSystem MakeSmokeEmitter(Vector3 worldPos)
    {
        var go = new GameObject("DamageSmoke");
        go.transform.position = worldPos;
        var ps = go.AddComponent<ParticleSystem>();
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Prims.ParticleMat();
        var main = ps.main;
        main.startLifetime   = 2.5f;
        main.startSpeed      = 0.8f;
        main.startSize       = 0.45f;
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(0.35f, 0.35f, 0.35f, 0.7f),
            new Color(0.55f, 0.50f, 0.40f, 0.3f));
        main.maxParticles    = 20;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.08f; // rises slowly

        var emission = ps.emission;
        emission.rateOverTime = 4f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.3f;

        ps.Play();
        return ps;
    }

    void OnEnable()
    {
        GameEvents.OnUnitKilled        += OnUnitKilled;
        GameEvents.OnBuildingDestroyed += OnBuildingDestroyed;
        GameEvents.OnHitLanded         += OnHitLanded;
    }

    void OnDisable()
    {
        GameEvents.OnUnitKilled        -= OnUnitKilled;
        GameEvents.OnBuildingDestroyed -= OnBuildingDestroyed;
        GameEvents.OnHitLanded         -= OnHitLanded;
    }

    void OnUnitKilled(UnitEntity u, int team)
    {
        if (u == null) return;
        SpawnDeathParticle(u.transform.position + Vector3.up * 0.8f);
        AudioManager.Play(AudioManager.SoundId.UnitDie, 0.55f);
    }

    void OnBuildingDestroyed(BuildingEntity b, int team)
    {
        if (b == null) return;

        SpawnDeathParticle(b.transform.position + Vector3.up * 1.5f, large: true);
        AudioManager.Play(AudioManager.SoundId.Sword, 0.8f);
        _smoke.Remove(b); // clean up smoke emitter on destruction

        if (System.Array.IndexOf(ShakeBuildings, b.type) >= 0)
        {
            var rig = GameManager.Instance?.cameraRig;
            if (rig != null) rig.Shake(0.35f, 0.4f);
        }
    }

    // Pool of reusable death-FX particle systems. The old code did new GameObject +
    // AddComponent<ParticleSystem> + Destroy on EVERY death — heavy GC/instantiate churn
    // exactly when a big battle is busiest. Now we reuse from a pool and recycle on fade-out.
    static readonly System.Collections.Generic.Stack<ParticleSystem> _deathPool = new();
    static readonly ParticleSystem.Burst[] _burstSmall = { new ParticleSystem.Burst(0f, 12) };
    static readonly ParticleSystem.Burst[] _burstLarge = { new ParticleSystem.Burst(0f, 28) };

    static void SpawnDeathParticle(Vector3 pos, bool large = false)
    {
        ParticleSystem ps = null;
        while (_deathPool.Count > 0 && ps == null) ps = _deathPool.Pop(); // skip any destroyed by Restart
        if (ps == null) ps = CreateDeathPS();

        var go = ps.gameObject;
        go.transform.position = pos;
        if (!go.activeSelf) go.SetActive(true);

        var main = ps.main;
        main.startLifetime = large ? 0.8f : 0.5f;
        main.startSpeed    = large ? 5f   : 3f;
        main.startSize     = large ? 0.5f : 0.28f;
        main.maxParticles  = large ? 30   : 14;

        ps.emission.SetBursts(large ? _burstLarge : _burstSmall);

        var shape = ps.shape;
        shape.radius = large ? 0.6f : 0.25f;

        ps.Clear();
        ps.Play();

        // Return to the pool (not Destroy) once the burst has faded.
        var ret = go.GetComponent<FXPoolReturn>();
        if (ret == null) ret = go.AddComponent<FXPoolReturn>();
        ret.Arm(main.startLifetime.constant + 0.2f);
    }

    static ParticleSystem CreateDeathPS()
    {
        var go = new GameObject("DeathFX");
        var ps = go.AddComponent<ParticleSystem>();
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Prims.ParticleMat();
        var main = ps.main;
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.6f, 0.15f, 1f),
            new Color(0.9f, 0.2f, 0.05f, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake     = false;
        var emission = ps.emission; emission.rateOverTime = 0;
        var shape    = ps.shape;    shape.shapeType = ParticleSystemShapeType.Sphere;
        return ps;
    }

    // ── FEEL.vfx: per-hit impact bursts (cosmetic; driven by GameEvents.OnHitLanded) ──
    // Same pooled pattern as the death FX. Colors keyed by damage type:
    // melee = pale sparks, pierce = dusty puff, siege = big dust cloud.
    static readonly System.Collections.Generic.Stack<ParticleSystem> _impactPool = new();
    const int ImpactPoolMax = 32; // hard cap — big battles reuse, never grow unbounded
    static readonly ParticleSystem.Burst[] _burstHit   = { new ParticleSystem.Burst(0f, 6) };
    static readonly ParticleSystem.Burst[] _burstHeavy = { new ParticleSystem.Burst(0f, 10) };

    void OnHitLanded(Vector3 pos, DamageType type, bool heavy)
    {
        ParticleSystem ps = null;
        while (_impactPool.Count > 0 && ps == null) ps = _impactPool.Pop();
        if (ps == null) ps = CreateImpactPS();

        var go = ps.gameObject;
        go.transform.position = pos;
        if (!go.activeSelf) go.SetActive(true);

        var main = ps.main;
        main.startLifetime = heavy ? 0.35f : 0.22f;
        main.startSpeed    = heavy ? 3.5f  : 2.2f;
        main.startSize     = heavy ? 0.22f : 0.12f;
        main.maxParticles  = 12;
        main.startColor    = type switch
        {
            DamageType.Melee => new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.95f, 0.7f, 1f), new Color(1f, 0.8f, 0.3f, 1f)),
            DamageType.Siege => new ParticleSystem.MinMaxGradient(
                new Color(0.55f, 0.48f, 0.38f, 1f), new Color(0.4f, 0.35f, 0.28f, 1f)),
            _ => new ParticleSystem.MinMaxGradient(
                new Color(0.75f, 0.7f, 0.6f, 1f), new Color(0.6f, 0.55f, 0.45f, 1f)),
        };

        ps.emission.SetBursts(heavy ? _burstHeavy : _burstHit);
        ps.Clear();
        ps.Play();

        var ret = go.GetComponent<FXPoolReturn>();
        if (ret == null) ret = go.AddComponent<FXPoolReturn>();
        ret.Arm(main.startLifetime.constant + 0.15f, impact: true);
    }

    static ParticleSystem CreateImpactPS()
    {
        var go = new GameObject("ImpactFX");
        var ps = go.AddComponent<ParticleSystem>();
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Prims.ParticleMat();
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake     = false;
        var emission = ps.emission; emission.rateOverTime = 0;
        var shape    = ps.shape;    shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;
        return ps;
    }

    internal static void ReturnToPool(ParticleSystem ps, bool impact = false)
    {
        if (ps == null) return;
        if (impact)
        {
            if (_impactPool.Count < ImpactPoolMax) _impactPool.Push(ps);
            else Destroy(ps.gameObject);
        }
        else _deathPool.Push(ps);
    }
}

/// <summary>Recycles a pooled FX particle system back to its pool once its burst has
/// faded, instead of destroying the GameObject (avoids per-hit instantiate/GC churn).</summary>
class FXPoolReturn : MonoBehaviour
{
    float _t;
    bool  _impact;
    public void Arm(float seconds, bool impact = false) { _t = seconds; _impact = impact; }
    void Update()
    {
        if ((_t -= Time.deltaTime) > 0f) return;
        gameObject.SetActive(false);
        VisualEffectSystem.ReturnToPool(GetComponent<ParticleSystem>(), _impact);
    }
}
