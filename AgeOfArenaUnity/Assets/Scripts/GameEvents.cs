using System;

/// <summary>
/// Static event hub — systems fire, UI/VFX/audio subscribe without knowing the source.
/// Call <see cref="Reset"/> on session restart to clear stale Unity object references.
/// </summary>
public static class GameEvents
{
    public static event Action<UnitEntity, int>     OnUnitKilled;
    public static event Action<BuildingEntity, int> OnBuildingDestroyed;
    public static event Action<int, Age>            OnAgeAdvanced;
    public static event Action<int, TechType>       OnResearchCompleted;
    /// <summary>FEEL.vfx: a damage application landed at a world position. Purely
    /// cosmetic subscribers (impact particles); bool = heavy hit (charge/siege).</summary>
    public static event Action<UnityEngine.Vector3, DamageType, bool> OnHitLanded;

    public static void FireUnitKilled(UnitEntity u, int team)       => OnUnitKilled?.Invoke(u, team);
    public static void FireBuildingDestroyed(BuildingEntity b, int team) => OnBuildingDestroyed?.Invoke(b, team);
    public static void FireAgeAdvanced(int team, Age newAge)        => OnAgeAdvanced?.Invoke(team, newAge);
    public static void FireResearchCompleted(int team, TechType tech) => OnResearchCompleted?.Invoke(team, tech);
    public static void FireHitLanded(UnityEngine.Vector3 pos, DamageType type, bool heavy)
        => OnHitLanded?.Invoke(pos, type, heavy);

    /// <summary>Clears all subscribers to prevent stale closures after a restart.</summary>
    public static void Reset()
    {
        OnUnitKilled         = null;
        OnBuildingDestroyed  = null;
        OnAgeAdvanced        = null;
        OnResearchCompleted  = null;
        OnHitLanded          = null;
    }
}
