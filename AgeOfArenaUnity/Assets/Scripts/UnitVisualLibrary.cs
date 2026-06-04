using UnityEngine;

/// <summary>
/// Maps unit types to animated visual prefabs (KayKit). Stored as a single small
/// <c>.asset</c> in <c>Resources/</c> so <see cref="UnitFactory"/> can
/// <c>Resources.Load</c> it once. The heavy character FBX themselves live OUTSIDE
/// <c>Resources/</c> (under <c>Assets/Art/KayKit</c>): only the prefabs referenced here
/// (plus their meshes and the animation clips their controllers use) are pulled into the
/// WebGL build, so unused characters and the other ~70 clips per character are excluded.
///
/// Returns <c>null</c> for any unit type without an assigned prefab, so callers fall back
/// to the procedural <see cref="Prims"/> primitive build path.
/// </summary>
[CreateAssetMenu(fileName = "UnitVisualLibrary", menuName = "AgeOfArena/Unit Visual Library")]
public class UnitVisualLibrary : ScriptableObject
{
    [Tooltip("Militia → KayKit Knight (animated). Null = primitive fallback.")]
    public GameObject militiaVisual;

        [Tooltip("Spearman → KayKit Knight (same model as Militia). Null = primitive fallback.")]
    public GameObject spearmanVisual;

    [Tooltip("Archer/Skirmisher → KayKit Rogue. Null = primitive fallback.")]
    public GameObject archerVisual;

    [Tooltip("Scout/Longbowman → KayKit RogueHooded. Null = primitive fallback.")]
    public GameObject scoutVisual;

    [Tooltip("Monk/Medic → KayKit Mage. Null = primitive fallback.")]
    public GameObject mageVisual;

    [Tooltip("Villager → KayKit Barbarian. Null = primitive fallback.")]
    public GameObject barbarianVisual;

    [Header("Enemy Skeletons (teamId > 0)")]
    [Tooltip("Heavy infantry (Militia/Spearman) for enemy teams.")]
    public GameObject skeletonWarriorVisual;
    [Tooltip("Light infantry (Scout) for enemy teams.")]
    public GameObject skeletonMinionVisual;
    [Tooltip("Ranged (Archer/Skirmisher/Longbowman) for enemy teams.")]
    public GameObject skeletonRogueVisual;
    [Tooltip("Caster (Monk/Medic) for enemy teams.")]
    public GameObject skeletonMageVisual;

    /// <summary>
    /// Animated visual prefab for a unit type. teamId 0 = player (Adventurers),
    /// teamId 1-3 = AI enemies (Skeletons). Returns null → primitive fallback.
    /// </summary>
    public GameObject VisualFor(UnitType type, int teamId = 0)
    {
        bool enemy = teamId > 0;
        return type switch
        {
            UnitType.Militia    => enemy ? skeletonWarriorVisual : militiaVisual,
            UnitType.Spearman   => enemy ? skeletonWarriorVisual : spearmanVisual,
            UnitType.Archer     => enemy ? skeletonRogueVisual   : archerVisual,
            UnitType.Skirmisher => enemy ? skeletonRogueVisual   : archerVisual,
            UnitType.Longbowman => enemy ? skeletonRogueVisual   : scoutVisual,
            UnitType.Scout      => enemy ? skeletonMinionVisual  : scoutVisual,
            UnitType.Monk       => enemy ? skeletonMageVisual    : mageVisual,
            UnitType.Medic      => enemy ? skeletonMageVisual    : mageVisual,
            UnitType.Villager   => barbarianVisual,
            _                   => null,
        };
    }
}
