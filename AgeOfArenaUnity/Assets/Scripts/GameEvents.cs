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

    public static void FireUnitKilled(UnitEntity u, int team)       => OnUnitKilled?.Invoke(u, team);
    public static void FireBuildingDestroyed(BuildingEntity b, int team) => OnBuildingDestroyed?.Invoke(b, team);
    public static void FireAgeAdvanced(int team, Age newAge)        => OnAgeAdvanced?.Invoke(team, newAge);
    public static void FireResearchCompleted(int team, TechType tech) => OnResearchCompleted?.Invoke(team, tech);

    /// <summary>Clears all subscribers to prevent stale closures after a restart.</summary>
    public static void Reset()
    {
        OnUnitKilled         = null;
        OnBuildingDestroyed  = null;
        OnAgeAdvanced        = null;
        OnResearchCompleted  = null;
    }
}
