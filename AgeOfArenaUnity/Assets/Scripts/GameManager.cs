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
    /// <summary>Maximum supported team count. Arrays are pre-allocated to this size; active
    /// teams are <see cref="TeamCount"/>. Raise to 8 for 8-player skirmish support.</summary>
    public const int MaxTeams = 8;

    public ResourceManager[] teamRes  = new ResourceManager[MaxTeams];
    public ResourceManager resources  => teamRes[0] ??= new ResourceManager();
    /// <summary>Number of active teams this match (set by WorldRoot.Build).</summary>
    public int TeamCount { get; set; } = 4;

    public TechState[] teamTech = new TechState[MaxTeams];
    public TechState tech => teamTech[0] ??= new TechState();

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
    public TutorialSystem tutorial;
    public TriggerSystem   triggers;       // N11.trig
    public ScenarioEditor  scenarioEditor; // N12.edit
    public CampaignScreen  campaignScreen; // N13.camp
    public CommandRecorder cmdRecorder;    // N3.cmdlog
    public ChecksumSystem  checksum;       // N15.checksum
    public LockstepSystem  lockstep;       // N16.lockstep
    public DesyncHandler   desync;         // N17.desync
    public TransportLayer  transport;      // N17.transport
    public LobbyScreen    lobbyScreen;    // MP-3.lobby

    /// <summary>N1: per-frame spatial index of all units for O(n) proximity queries
    /// (combat aggro / heal / convert / projectile splash). Rebuilt at the top of Update.</summary>
    public readonly SpatialGrid unitGrid = new SpatialGrid(8f);

    public BuildingEntity selectedBuilding;
    public ResourceNode selectedNode;

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
    public float[] teamEcoMult = new float[MaxTeams]; // all default to 0f; Awake fills 1f

    /// <summary>Per-team civilization. Index 0 = player; 1-3 = AI teams.</summary>
    public Civilization[] teamCivs = new Civilization[MaxTeams]; // defaults to Civilization.None (0)

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
        // Pre-populate every slot so teamRes[i] / teamTech[i] are never null.
        for (int i = 0; i < MaxTeams; i++)
        {
            teamRes[i]    ??= new ResourceManager();
            teamTech[i]   ??= new TechState();
            teamEcoMult[i]  = 1f; // default eco speed multiplier (AI may override later)
        }
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    public void RegisterUnit(UnitEntity u)
    {
        if (u != null && !units.Contains(u))
        {
            units.Add(u);
            // N1.hpbar: attach world-space billboard HP bar (replaces IMGUI).
            var bar = u.gameObject.AddComponent<WorldHpBar>();
            float yOff = u.IsKayKitModel ? 2.0f : 1.6f;
            bar.Init(yOff, u.teamId == 0);
            u.hpBar = bar;   // cache so CombatSystem.LateUpdate skips per-frame GetComponent
        }
    }

    public void RegisterNode(ResourceNode n)
    {
        if (n != null && !nodes.Contains(n)) nodes.Add(n);
    }

    public void RegisterBuilding(BuildingEntity b)
    {
        if (b != null && !buildings.Contains(b))
        {
            buildings.Add(b);
            // N1.hpbar: world-space HP bar for buildings.
            var bar = b.gameObject.AddComponent<WorldHpBar>();
            bar.Init(3.4f, b.teamId == 0);
            b.hpBar = bar;   // cache so CombatSystem.LateUpdate skips per-frame GetComponent
        }
    }

    public void RegisterRelic(RelicEntity r)
    {
        if (r != null && !relics.Contains(r)) relics.Add(r);
    }

    // N3.fixedstep: fixed-step sim tick (~30 Hz). When enabled, sim systems receive a
    // constant dt (FIXED_DT) regardless of render frame rate. Time.deltaTime is
    // accumulated and drained in whole steps so the simulation remains deterministic.
    public  bool  FixedStepEnabled = false;
    const   float FIXED_DT         = 1f / 30f;
    float         _stepAccumulator;

    void Update()
    {
        if (FixedStepEnabled)
        {
            // N3.fixedstep: drain accumulator in constant-dt increments (max 3 steps/frame
            // to prevent spiral-of-death on lag spikes).
            _stepAccumulator += Time.deltaTime;
            int steps = 0;
            while (_stepAccumulator >= FIXED_DT && steps < 3)
            {
                SimTick(FIXED_DT);
                _stepAccumulator -= FIXED_DT;
                steps++;
            }
        }
        else
        {
            SimTick(Time.deltaTime);
        }
    }

    void SimTick(float dt)
    {
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

        // Farm reseed/decay now ticks inside the sim (was a per-frame ResourceNode.Update on
        // Time.deltaTime, which mutated the resource ledger outside the deterministic tick stream).
        for (int i = 0; i < nodes.Count; i++)
        {
            var nd = nodes[i];
            if (nd != null) nd.SimStep(dt);
        }

        // Compact lists once per frame after all systems ticked, so destroyed
        // units/buildings (Unity fake-null) don't linger as null holes. The predicate
        // captures nothing, so Roslyn caches the delegate — these are allocation-free
        // O(n) scans (all sim systems also null-check, so this is hygiene, not correctness).
        units.RemoveAll(u => u == null);
        buildings.RemoveAll(b => b == null);
        nodes.RemoveAll(n => n == null);
        relics.RemoveAll(r => r == null);

        RecomputePop();
        lockstep?.OnSimTick(); // N16.lockstep: advance tick after all systems
    }

    /// <summary>
    /// Per-team population: unit count + building popProvided for each team,
    /// clamped to 200. Only pushes to <see cref="ResourceManager"/> when changed.
    /// Called after unit/building add/remove; AI uses the resulting popCap before training.
    /// </summary>
    int[] _popBuf, _capBuf;   // reused across calls — RecomputePop runs every SimTick

    public void RecomputePop()
    {
        int n = TeamCount;
        // Reuse buffers instead of allocating two int[] every tick (steady GC pressure).
        if (_popBuf == null || _popBuf.Length < n) { _popBuf = new int[n]; _capBuf = new int[n]; }
        var pop = _popBuf;
        var cap = _capBuf;
        System.Array.Clear(pop, 0, n);
        System.Array.Clear(cap, 0, n);

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
            var res = teamRes[t] ??= new ResourceManager();
            if (pop[t] != res.pop || cap[t] != res.popCap)
                res.SetPop(pop[t], cap[t]);
        }
    }
}
