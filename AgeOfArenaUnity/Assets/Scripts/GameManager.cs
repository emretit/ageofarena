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

    /// <summary>N5: maximum simultaneous teams. All team-indexed arrays are sized to this.
    /// Raising it to 8 enables 8-player skirmish without touching any < 4 guard — just
    /// rebuild the world with more teams and resize these arrays accordingly.</summary>
    public const int MaxTeams = 4;

    public ResourceManager[] teamRes = { new(), new(), new(), new() };
    public ResourceManager resources => teamRes[0];
    /// <summary>Number of active teams (≤ MaxTeams). Currently fixed at MaxTeams; N5 raises this.</summary>
    public int TeamCount => teamRes.Length;

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

    // ── N14/MODES: rule-toggle flags (set by WorldRoot.SetupGameplay) ─────────
    /// <summary>KingOfTheHill: control the centre TC to accumulate victory points.</summary>
    public bool kothActive;
    /// <summary>SuddenDeath: losing your TC causes immediate elimination.</summary>
    public bool suddenDeath;
    /// <summary>Treaty: attacks are blocked until this simulation-time (seconds) elapses.</summary>
    public float treatyEndTime;
    /// <summary>Turbo: multiplier on all villager gather yields (default 1 = no boost).</summary>
    public float turboGatherMult = 1f;

    /// <summary>
    /// VDIPL: 4×4 diplomacy matrix. diplomacy[a,b] = team a's stance toward team b.
    /// Default: all teams are enemies; self-to-self is Allied.
    /// </summary>
    public DiplomacyState[,] diplomacy = InitDiplomacy();

    static DiplomacyState[,] InitDiplomacy()
    {
        var d = new DiplomacyState[MaxTeams, MaxTeams];
        for (int a = 0; a < MaxTeams; a++)
            for (int b = 0; b < MaxTeams; b++)
                d[a, b] = (a == b) ? DiplomacyState.Allied : DiplomacyState.Enemy;
        return d;
    }

    /// <summary>True if team a considers team b an enemy (i.e. will attack on sight).</summary>
    public bool IsEnemy(int a, int b) =>
        a != b && a >= 0 && a < MaxTeams && b >= 0 && b < MaxTeams && diplomacy[a, b] == DiplomacyState.Enemy;

    /// <summary>N0.2: true if team b is team a itself or an ally — used for shared victory
    /// (a wonder/relic/score win by an ally is a win for the whole alliance, not a loss).</summary>
    public bool IsAllied(int a, int b) =>
        a >= 0 && a < MaxTeams && b >= 0 && b < MaxTeams && (a == b || diplomacy[a, b] == DiplomacyState.Allied);

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
    /// Per-team population: unit count + building popProvided for each team,
    /// clamped to 200. Only pushes to <see cref="ResourceManager"/> when changed.
    /// Called after unit/building add/remove; AI uses the resulting popCap before training.
    /// </summary>
    public void RecomputePop()
    {
        int n = TeamCount;
        var pop = new int[n];
        var cap = new int[n];

        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u != null && u.teamId >= 0 && u.teamId < n) pop[u.teamId]++;
        }
        for (int i = 0; i < buildings.Count; i++)
        {
            var b = buildings[i];
            if (b == null || b.underConstruction || b.teamId < 0 || b.teamId >= n) continue;
            cap[b.teamId] += BuildingDefs.Get(b.type).popProvided;
        }
        for (int t = 0; t < n; t++)
        {
            cap[t] = Mathf.Clamp(cap[t], 0, 200);
            var res = teamRes[t];
            if (pop[t] != res.pop || cap[t] != res.popCap)
                res.SetPop(pop[t], cap[t]);
        }
    }
}
