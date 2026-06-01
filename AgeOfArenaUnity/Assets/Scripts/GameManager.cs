using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central runtime state + per-frame driver for the gameplay slice. Created by
/// <see cref="WorldRoot"/> after the static scene is built. Holds the unit list,
/// resource nodes, buildings (which double as gather drop-off points), the
/// player's <see cref="ResourceManager"/>, and references to the gather systems.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public readonly List<UnitEntity> units = new();
    public readonly List<ResourceNode> nodes = new();
    public readonly List<BuildingEntity> buildings = new();

    public ResourceManager[] teamRes = { new(), new(), new(), new() };
    public ResourceManager resources => teamRes[0];

    public SelectionSystem selection;
    public CommandSystem command;
    public GatherSystem gather;
    public CombatSystem combat;
    public BuildSystem build;
    public BuildingPlacement placement;
    public TrainingQueue trainingQueue;
    public HUD hud;
    public MinimapSystem minimap;
    public MatchSystem match;

    public BuildingEntity selectedBuilding;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void RegisterUnit(UnitEntity u)
    {
        if (u != null && !units.Contains(u)) units.Add(u);
    }

    public void RegisterNode(ResourceNode n)
    {
        if (n != null && !nodes.Contains(n)) nodes.Add(n);
    }

    public void RegisterBuilding(BuildingEntity b)
    {
        if (b != null && !buildings.Contains(b)) buildings.Add(b);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (gather != null)        gather.Tick(units, dt);
        if (combat != null)        combat.Tick(units, dt);
        if (build != null)         build.Tick(units, dt);
        if (trainingQueue != null) trainingQueue.Tick(dt);

        // Compact lists once per frame after all systems ticked, so destroyed
        // units/buildings (Unity fake-null) don't linger as null holes.
        units.RemoveAll(u => u == null);
        buildings.RemoveAll(b => b == null);
        nodes.RemoveAll(n => n == null);

        RecomputePop();
    }

    /// <summary>
    /// Player (team 0) population = live team-0 unit count; population cap = sum of
    /// <see cref="BuildingDefs"/> popProvided over completed team-0 buildings
    /// (Town Center + Houses), clamped to 200. Only pushes to the
    /// <see cref="ResourceManager"/> when a value actually changed, to avoid
    /// firing <see cref="ResourceManager.OnChanged"/> (HUD refresh) every frame.
    /// </summary>
    public void RecomputePop()
    {
        int pop = 0;
        for (int i = 0; i < units.Count; i++)
            if (units[i] != null && units[i].teamId == 0) pop++;

        int cap = 0;
        for (int i = 0; i < buildings.Count; i++)
        {
            var b = buildings[i];
            if (b == null || b.teamId != 0 || b.underConstruction) continue;
            cap += BuildingDefs.Get(b.type).popProvided;
        }
        cap = Mathf.Clamp(cap, 0, 200);

        if (pop != resources.pop || cap != resources.popCap)
            resources.SetPop(pop, cap);
    }
}
