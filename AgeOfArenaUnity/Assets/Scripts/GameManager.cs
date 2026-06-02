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
    static GameManager _instance;

    /// <summary>
    /// Singleton accessor. Self-healing: a domain reload during Play (e.g. editing a
    /// script while play-testing) resets the static but does NOT re-run Awake on the
    /// surviving GameManager, which would otherwise leave every system that reads
    /// <c>Instance</c> silently no-opping. The lazy <see cref="Object.FindAnyObjectByType{T}"/>
    /// fallback re-binds to the live instance so play survives hot reloads.
    /// </summary>
    public static GameManager Instance =>
        _instance != null ? _instance : (_instance = FindAnyObjectByType<GameManager>());

    public readonly List<UnitEntity> units = new();
    public readonly List<ResourceNode> nodes = new();
    public readonly List<BuildingEntity> buildings = new();
    public readonly List<RelicEntity> relics = new();

    public ResourceManager[] teamRes = { new(), new(), new(), new() };
    public ResourceManager resources => teamRes[0];

    public TechState[] teamTech = { new(), new(), new(), new() };
    public TechState tech => teamTech[0];

    public SelectionSystem selection;
    public CommandSystem command;
    public GatherSystem gather;
    public CombatSystem combat;
    public BuildingCombatSystem buildingCombat;
    public GarrisonSystem garrison;
    public BuildSystem build;
    public BuildingPlacement placement;
    public TrainingQueue trainingQueue;
    public ResearchSystem research;
    public HUD hud;
    public MinimapSystem minimap;
    public MatchSystem match;
    public VisualEffectSystem vfx;
    public IsometricCameraRig cameraRig;
    public FogOfWarSystem fow;
    public RelicSystem relicSystem;
    public TradingSystem trading;

    public BuildingEntity selectedBuilding;

    /// <summary>Global AI difficulty (applied by every <see cref="EnemyAI"/>).</summary>
    public Difficulty difficulty = Difficulty.Normal;

    /// <summary>Per-team civilization. Index 0 = player; 1-3 = AI teams.</summary>
    public Civilization[] teamCivs = { Civilization.None, Civilization.None, Civilization.None, Civilization.None };

    /// <summary>Player civ (team 0). Backed by teamCivs[0] for HUD/system compatibility.</summary>
    public Civilization playerCiv { get => teamCivs[0]; set => teamCivs[0] = value; }

    /// <summary>Player civ bonus (team 0).</summary>
    public CivBonus CivBonus => CivilizationDefs.Get(playerCiv);

    /// <summary>Civ bonus for any team.</summary>
    public CivBonus TeamCivBonus(int teamId) => CivilizationDefs.Get(teamCivs[teamId]);

    void Awake()
    {
        _instance = this;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
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

    public void RegisterRelic(RelicEntity r)
    {
        if (r != null && !relics.Contains(r)) relics.Add(r);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (gather != null)        gather.Tick(units, dt);
        if (combat != null)        combat.Tick(units, dt);
        if (buildingCombat != null) buildingCombat.Tick(buildings, units, dt);
        if (garrison != null)      garrison.Tick(units, buildings, dt);
        if (build != null)         build.Tick(units, dt);
        if (trainingQueue != null) trainingQueue.Tick(dt);
        if (research != null)      research.Tick(dt);
        if (relicSystem != null)   relicSystem.Tick(units, relics, dt);
        MarketSystem.Tick(dt);
        if (trading != null) trading.Tick(units, buildings, dt);

        // Compact lists once per frame after all systems ticked, so destroyed
        // units/buildings (Unity fake-null) don't linger as null holes.
        units.RemoveAll(u => u == null);
        buildings.RemoveAll(b => b == null);
        nodes.RemoveAll(n => n == null);
        relics.RemoveAll(r => r == null);

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
