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

    /// <summary>N1: per-frame spatial index of all units for O(n) proximity queries
    /// (combat aggro / heal / convert / projectile splash). Rebuilt at the top of Update.</summary>
    public readonly SpatialGrid unitGrid = new SpatialGrid(8f);

    public BuildingEntity selectedBuilding;

    /// <summary>Global AI difficulty (applied by every <see cref="EnemyAI"/>).</summary>
    public Difficulty difficulty = Difficulty.Normal;

    /// <summary>Active game mode — set by WorldRoot before gameplay begins.</summary>
    public GameMode gameMode = GameMode.Random;

    /// <summary>
    /// VDIPL: 4×4 diplomacy matrix. diplomacy[a,b] = team a's stance toward team b.
    /// Default: all teams are enemies; self-to-self is Allied.
    /// </summary>
    public DiplomacyState[,] diplomacy = InitDiplomacy();

    static DiplomacyState[,] InitDiplomacy()
    {
        var d = new DiplomacyState[4, 4];
        for (int a = 0; a < 4; a++)
            for (int b = 0; b < 4; b++)
                d[a, b] = (a == b) ? DiplomacyState.Allied : DiplomacyState.Enemy;
        return d;
    }

    /// <summary>True if team a considers team b an enemy (i.e. will attack on sight).</summary>
    public bool IsEnemy(int a, int b) =>
        a != b && a >= 0 && a < 4 && b >= 0 && b < 4 && diplomacy[a, b] == DiplomacyState.Enemy;

    /// <summary>N0.2: true if team b is team a itself or an ally — used for shared victory
    /// (a wonder/relic/score win by an ally is a win for the whole alliance, not a loss).</summary>
    public bool IsAllied(int a, int b) =>
        a >= 0 && a < 4 && b >= 0 && b < 4 && (a == b || diplomacy[a, b] == DiplomacyState.Allied);

    /// <summary>AICH: per-team economy speed multiplier set by EnemyAI per difficulty.
    /// Applied to gather deposits and research time. Player (team 0) stays at 1×.</summary>
    public float[] teamEcoMult = { 1f, 1f, 1f, 1f };

    /// <summary>Per-team civilization. Index 0 = player; 1-3 = AI teams.</summary>
    public Civilization[] teamCivs = { Civilization.None, Civilization.None, Civilization.None, Civilization.None };

    /// <summary>Player civ (team 0). Backed by teamCivs[0] for HUD/system compatibility.</summary>
    public Civilization playerCiv { get => teamCivs[0]; set => teamCivs[0] = value; }

    /// <summary>Player civ bonus (team 0).</summary>
    public CivBonus CivBonus => CivilizationDefs.Get(playerCiv);

    /// <summary>Civ bonus for any team.</summary>
    public CivBonus TeamCivBonus(int teamId) => CivilizationDefs.Get(teamCivs[teamId]);

    /// <summary>CIVM/N0.6: aggregated team (shared) bonus for a team — the team's own civ
    /// team bonus PLUS every allied team's team bonus (AoE2 shared team bonuses). Was a stub
    /// that returned only the team's own bonus. Consumed by gameplay systems (e.g. GatherSystem
    /// food deposit). When <see cref="TeamBonus"/> gains fields, sum each one here.</summary>
    public TeamBonus TeamSharedBonus(int teamId)
    {
        if (teamId < 0 || teamId >= teamCivs.Length) return default;
        var sum = new TeamBonus();
        for (int t = 0; t < teamCivs.Length; t++)
        {
            if (!IsAllied(teamId, t)) continue;   // IsAllied is true for self and allies
            sum.gatherFoodBonus += CivilizationDefs.Get(teamCivs[t]).teamBonus.gatherFoodBonus;
        }
        return sum;
    }

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
        unitGrid.Rebuild(units);   // N1: refresh spatial index before any proximity query
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
