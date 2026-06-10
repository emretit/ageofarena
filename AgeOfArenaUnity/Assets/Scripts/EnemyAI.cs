using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AISC: unit-mix profile driven by personality. Weights are relative — the AI
/// samples them probabilistically so composition varies game-to-game while staying
/// thematically consistent (Rusher = heavy melee bias; Boomer = balanced with siege;
/// Balanced = slight archer lean for ranged harassment).
/// </summary>
public struct AIProfile
{
    public float meleeWeight;   // Militia / Spearman
    public float archerWeight;  // Archer
    public float cavalryWeight; // Cavalry
    public float siegeWeight;   // Trebuchet (Castle+)

    static readonly AIProfile _rusher   = new AIProfile { meleeWeight = 0.65f, archerWeight = 0.15f, cavalryWeight = 0.15f, siegeWeight = 0.05f };
    static readonly AIProfile _boomer   = new AIProfile { meleeWeight = 0.25f, archerWeight = 0.25f, cavalryWeight = 0.30f, siegeWeight = 0.20f };
    static readonly AIProfile _balanced = new AIProfile { meleeWeight = 0.35f, archerWeight = 0.35f, cavalryWeight = 0.20f, siegeWeight = 0.10f };

    public static AIProfile For(AIPersonality p) => p switch
    {
        AIPersonality.Rusher   => _rusher,
        AIPersonality.Boomer   => _boomer,
        _                      => _balanced,
    };
}

/// <summary>
/// Per-team enemy brain. Manages an economy (villagers gather resources) and a
/// coordinated military loop: instead of trickling units at the player one by
/// one, the army gathers at a rally point, marches on a target together, and
/// retreats to regroup once it bleeds past a loss threshold.
///
/// A <see cref="AIPersonality"/> tunes the thresholds so the three enemy teams
/// play differently — a Rusher commits early with a small force, a Boomer hoards
/// economy and attacks late with a big army, a Balanced team sits in between.
/// Each AI uses its own <see cref="ResourceManager"/> slot in <c>GameManager.teamRes</c>.
/// </summary>
public class EnemyAI : MonoBehaviour
{
    // Fixed cadences (tuning that doesn't vary by personality)
    const float AssessInterval      = 3f;
    const float GatherCheckInterval = 6f;
    const float TechInterval        = 8f;

    // Unit costs — these MUST mirror the real UnitTrainable costs the AI enqueues
    // (BuildingEntity), otherwise the affordability gate tests the wrong resource:
    // the AI would "pick" a unit it can't actually pay for, Enqueue rejects it, and the
    // spawn tick is wasted. (Old bug: Militia was gated on wood=60 though Militia now
    // costs food 60 / gold 20; Archer was gated on wood 35 / gold 25 though it costs wood 25 / gold 45.)
    const int MilitiaCostFood    = 60;   // Militia: food 60, gold 20
    const int MilitiaCostGold    = 20;
    const int ArcherCostWood     = 25;   // Archer: wood 25, gold 45
    const int ArcherCostGold     = 45;
    const int CavalryCostFood    = 80;   // Cavalry: food 80
    const int TrebuchetCostWood  = 200;  // Trebuchet: wood 200, gold 100
    const int TrebuchetCostGold  = 100;
    const int VillagerCostFood   = 50;   // Villager: food 50
    const int SpearmanCostFood   = 35;   // Spearman: food 35, wood 25
    const int SpearmanCostWood   = 25;
    const int MedicCostFood      = 60;   // Medic: food 60

    // ── Army coordination tuning ────────────────────────────────────────────
    const float RallyRadius        = 6f;    // arrival check radius at the rally point
    const float ArriveFraction     = 0.7f;  // fraction gathered before the army commits
    const int   RallyTimeoutTicks  = 5;     // ~15s: attack even if stragglers haven't arrived
    const int   RetreatTimeoutTicks = 6;    // ~18s: resume gathering even if not fully home

    /// <summary>Coordinated army phases. The whole force shares one stance.</summary>
    enum Stance { Gathering, Rallying, Attacking, Retreating }

    int _teamId;
    Color _teamColor;
    Vector3 _home;
    Transform _unitsRoot;
    MapType _mapType = MapType.Arena;

    // Personality-tuned parameters (set in ApplyPersonality)
    float _spawnInterval;
    int   _armyCap;
    int   _rushThreshold;
    int   _villagerTarget;
    float _retreatLoss;   // fraction of the assault force that may be lost before retreating

    float _spawnTimer;
    float _assessTimer = 2f;
    float _gatherTimer = 3f;
    float _techTimer;
    float _rebalanceTimer = 15f; // how often to rebalance ALL gatherers (not just idle)

    // ── Army state machine ──────────────────────────────────────────────────
    Stance _stance = Stance.Gathering;
    Vector3 _rallyPoint;
    IDamageable _target;
    int _attackForce;    // army size recorded when the current assault began
    int _stanceTicks;    // Assess ticks spent in the current stance

    ResourceManager _res;
    TechState _tech;

    AIPersonality _personality;
    AIProfile     _profile;

    public void Init(int teamId, Color teamColor, Vector3 home, Transform unitsRoot, AIPersonality personality, MapType mapType = MapType.Arena)
    {
        _teamId    = teamId;
        _teamColor = teamColor;
        _home      = home;
        _unitsRoot = unitsRoot;
        _mapType   = mapType;
        _personality = personality;
        _profile     = AIProfile.For(personality);  // AISC
        ApplyPersonality(personality);
        ApplyDifficulty();
        // AICH: publish eco multiplier so GatherSystem/ResearchSystem can apply it.
        var gm = GameManager.Instance;
        if (gm != null && teamId >= 0 && teamId < 4)
            gm.teamEcoMult[teamId] = _ecoMult;
    }

    /// <summary>Re-derive the tuning from personality + the current global difficulty.
    /// Called by the HUD when the player cycles the difficulty mid-game.</summary>
    public void SetDifficulty()
    {
        ApplyPersonality(_personality);
        ApplyDifficulty();
        var gm = GameManager.Instance;
        if (gm != null && _teamId >= 0 && _teamId < 4)
            gm.teamEcoMult[_teamId] = _ecoMult;
    }

    /// <summary>Scale the personality baseline by the global <see cref="Difficulty"/>:
    /// easier AI trains slower with a smaller army and pushes later; harder AI trains
    /// faster, fields more, booms harder and commits sooner.</summary>
    // AICH: eco multiplier (applied to gather/research rates, not free resources).
    float _ecoMult = 1f;

    // BAL.ai: no offensive push before this match time on Easy/Moderate/Normal so the
    // player gets a real build-up phase; Hard+ keeps the gloves off. The gate lifts
    // early if the army cap fills (units are never parked forever).
    float _minFirstPushTime;
    float _matchClock;

    void ApplyDifficulty()
    {
        var gm = GameManager.Instance;
        var diff = gm != null ? gm.difficulty : Difficulty.Normal;

        // AIRD: use FloorToInt(x + 0.5f) for deterministic round-half-up (no banker's rounding).
        static int Round(float v) => Mathf.FloorToInt(v + 0.5f);

        _minFirstPushTime = 240f; // Normal baseline; overridden per level below
        switch (diff)
        {
            case Difficulty.Easy:
                _spawnInterval *= 2.0f; _armyCap = Mathf.Max(3, Round(_armyCap * 0.50f));
                _rushThreshold = Round(_rushThreshold * 1.5f);
                _villagerTarget = Mathf.Max(1, _villagerTarget - 2);
                _ecoMult = 0.65f;
                _minFirstPushTime = 420f;
                break;
            case Difficulty.Moderate:
                _spawnInterval *= 1.4f; _armyCap = Mathf.Max(4, Round(_armyCap * 0.75f));
                _rushThreshold = Round(_rushThreshold * 1.2f);
                _villagerTarget = Mathf.Max(1, _villagerTarget - 1);
                _ecoMult = 0.85f;
                _minFirstPushTime = 330f;
                break;
            // Normal: baseline (no change, _ecoMult stays 1).
            case Difficulty.Hard:
                _spawnInterval *= 0.80f; _armyCap = Round(_armyCap * 1.30f);
                _rushThreshold = Mathf.Max(3, Round(_rushThreshold * 0.85f));
                _villagerTarget += 2;
                _ecoMult = 1.15f;
                _minFirstPushTime = 0f;
                break;
            case Difficulty.Insane:
                _spawnInterval *= 0.60f; _armyCap = Round(_armyCap * 1.65f);
                _rushThreshold = Mathf.Max(3, Round(_rushThreshold * 0.72f));
                _villagerTarget += 4;
                _ecoMult = 1.35f;
                _minFirstPushTime = 0f;
                break;
            case Difficulty.Extreme:
                _spawnInterval *= 0.42f; _armyCap = Round(_armyCap * 2.10f);
                _rushThreshold = Mathf.Max(2, Round(_rushThreshold * 0.55f));
                _villagerTarget += 6;
                _ecoMult = 1.60f;
                _minFirstPushTime = 0f;
                break;
        }
    }

    /// <summary>Map a personality onto the army-size, push-timing and economy knobs.</summary>
    void ApplyPersonality(AIPersonality p)
    {
        switch (p)
        {
            case AIPersonality.Rusher: // early pressure, lean economy, committed pushes
                _spawnInterval = 11f; _armyCap = 10; _rushThreshold = 5;
                _villagerTarget = 4;  _retreatLoss = 0.6f;
                _spawnTimer = 8f;     _techTimer = 16f;
                break;
            case AIPersonality.Boomer: // economy first, big cautious army, upgrade-heavy
                _spawnInterval = 13f; _armyCap = 18; _rushThreshold = 12;
                _villagerTarget = 8;  _retreatLoss = 0.3f;
                _spawnTimer = 22f;    _techTimer = 8f;
                break;
            default: // Balanced — the original behaviour
                _spawnInterval = 15f; _armyCap = 12; _rushThreshold = 8;
                _villagerTarget = 5;  _retreatLoss = 0.4f;
                _spawnTimer = 15f;    _techTimer = 12f;
                break;
        }
    }

    /// <summary>Live registry of all active AI brains, so callers (e.g. the HUD difficulty
    /// cycler) don't have to FindObjectsByType (a full scene scan) to reach them.</summary>
    public static readonly List<EnemyAI> All = new();
    void OnEnable()  { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    /// <summary>Daily-challenge "Assault": drop the rush threshold and speed up production so
    /// the AI commits to attacking almost immediately (no early boom). Call after Init.</summary>
    public void MakeAggressive()
    {
        _rushThreshold = 1;
        if (_spawnInterval > 0f) _spawnInterval *= 0.7f;
        _minFirstPushTime = 0f; // BAL.ai: assault challenge ignores the first-push gate
        _assessTimer = 0f;   // re-assess (and likely push) on the next tick
    }

    void Start()
    {
        var gm = GameManager.Instance;
        if (gm != null) { _res = gm.teamRes[_teamId]; _tech = gm.teamTech[_teamId]; }
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || _res == null) return;
        float dt = Time.deltaTime;
        _matchClock += dt;

        if ((_spawnTimer  -= dt) <= 0f) { _spawnTimer  = _spawnInterval;       TrySpawn(gm); }
        if ((_assessTimer -= dt) <= 0f) { _assessTimer = AssessInterval;       Assess(gm); }
        if ((_gatherTimer -= dt) <= 0f) { _gatherTimer = GatherCheckInterval;  EconomyTick(gm); }
        if ((_techTimer   -= dt) <= 0f) { _techTimer   = TechInterval;         TryAdvanceTech(); }
    }

    // ── Tech / Age ─────────────────────────────────────────────────────────────

    /// <summary>Advance age when affordable and pick up a couple of military upgrades.
    /// Resources are deducted directly (AI doesn't queue) and the tech is applied via
    /// the shared <see cref="ResearchSystem.Apply"/> so live units get hp bumps too.</summary>
    void TryAdvanceTech()
    {
        if (_tech == null) return;

        TechType[] order = _tech.age switch
        {
            Age.Dark => new[] { TechType.FeudalAge },
            Age.Feudal => new[]
            {
                TechType.DoubleBitAxe, TechType.GoldMining, TechType.StoneMining, TechType.Wheelbarrow,
                TechType.Forging, TechType.Fletching, TechType.TownWatch,
                TechType.Supplies, TechType.ManAtArms, TechType.Squires,
                TechType.CastleAge
            },
            Age.Castle => CastleTechOrder(),
            _ => ImperialTechOrder()
        };

        for (int i = 0; i < order.Length; i++)
            if (TryResearch(order[i])) return;
    }

    TechType[] CastleTechOrder()
    {
        var gm = GameManager.Instance;
        bool monkReady = gm != null
            && (OwnsProduction(gm, BuildingType.Monastery) || CountType(gm, UnitType.Monk) > 0);
        bool navyReady = gm != null && _mapType == MapType.Islands && OwnsProduction(gm, BuildingType.Dock);

        if (navyReady)
        {
            return new[]
            {
                TechType.WarGalley,
                TechType.Sanctity, TechType.BlockPrinting, TechType.Redemption,
                TechType.ScaleMail, TechType.Bloodlines, TechType.Bodkin, TechType.Pikeman,
                TechType.LightCavalry, TechType.Caravan, TechType.Coinage,
                TechType.ThumbRing, TechType.CappedRam, TechType.Onager,
                TechType.TwoManSaw, TechType.GoldShaftMining, TechType.StoneShaftMining,
                TechType.HandCart, TechType.ImperialAge
            };
        }

        if (monkReady)
        {
            return new[]
            {
                TechType.Sanctity, TechType.BlockPrinting, TechType.Redemption,
                TechType.ScaleMail, TechType.Bloodlines, TechType.Bodkin, TechType.Pikeman,
                TechType.LightCavalry, TechType.WarGalley, TechType.Caravan, TechType.Coinage,
                TechType.ThumbRing, TechType.CappedRam, TechType.Onager,
                TechType.TwoManSaw, TechType.GoldShaftMining, TechType.StoneShaftMining,
                TechType.HandCart, TechType.ImperialAge
            };
        }

        return new[]
        {
            TechType.ScaleMail, TechType.Bloodlines, TechType.Bodkin, TechType.Pikeman,
            TechType.LightCavalry, TechType.WarGalley, TechType.Caravan, TechType.Coinage,
            TechType.Sanctity, TechType.BlockPrinting, TechType.Redemption,
            TechType.ThumbRing, TechType.CappedRam, TechType.Onager,
            TechType.TwoManSaw, TechType.GoldShaftMining, TechType.StoneShaftMining,
            TechType.HandCart, TechType.ImperialAge
        };
    }

    TechType[] ImperialTechOrder()
    {
        var gm = GameManager.Instance;
        bool navyReady = gm != null && _mapType == MapType.Islands && OwnsProduction(gm, BuildingType.Dock);
        bool marketReady = gm != null && OwnsProduction(gm, BuildingType.Market);
        bool siegeReady = gm != null && OwnsProduction(gm, BuildingType.SiegeWorkshop);

        if (navyReady && marketReady && siegeReady)
        {
            return new[]
            {
                TechType.Galleon,
                TechType.Guilds, TechType.Banking,
                TechType.CappedRam, TechType.SiegeRam, TechType.Onager, TechType.SiegeOnager, TechType.HeavyScorpion,
                TechType.BlastFurnace, TechType.ChainMail, TechType.PlateMail, TechType.Bracer,
                TechType.Arbalest, TechType.Paladin, TechType.Hussar, TechType.HeavyCamel,
                TechType.Theocracy
            };
        }

        if (marketReady && siegeReady)
        {
            return new[]
            {
                TechType.Guilds, TechType.Banking,
                TechType.CappedRam, TechType.SiegeRam, TechType.Onager, TechType.SiegeOnager, TechType.HeavyScorpion,
                TechType.BlastFurnace, TechType.ChainMail, TechType.PlateMail, TechType.Bracer,
                TechType.Arbalest, TechType.Paladin, TechType.Hussar, TechType.HeavyCamel,
                TechType.Theocracy
            };
        }

        if (marketReady)
        {
            return new[]
            {
                TechType.Guilds, TechType.Banking,
                TechType.BlastFurnace, TechType.ChainMail, TechType.PlateMail, TechType.Bracer,
                TechType.Arbalest, TechType.Paladin, TechType.Hussar, TechType.HeavyCamel,
                TechType.Theocracy, TechType.SiegeRam, TechType.SiegeOnager, TechType.HeavyScorpion
            };
        }

        if (siegeReady)
        {
            return new[]
            {
                TechType.CappedRam, TechType.SiegeRam, TechType.Onager, TechType.SiegeOnager, TechType.HeavyScorpion,
                TechType.BlastFurnace, TechType.ChainMail, TechType.PlateMail, TechType.Bracer,
                TechType.Arbalest, TechType.Paladin, TechType.Hussar, TechType.HeavyCamel,
                TechType.Guilds, TechType.Banking, TechType.Theocracy
            };
        }

        return new[]
        {
            TechType.BlastFurnace, TechType.ChainMail, TechType.PlateMail, TechType.Bracer,
            TechType.Arbalest, TechType.Paladin, TechType.Hussar, TechType.HeavyCamel,
            TechType.Guilds, TechType.Banking, TechType.Theocracy,
            TechType.SiegeRam, TechType.SiegeOnager, TechType.HeavyScorpion
        };
    }

    bool TryResearch(TechType type)
    {
        var d = TechDefs.Get(type);
        var gm = GameManager.Instance;
        if (_tech == null || gm == null) return false;
        var building = FindResearchBuilding(d.building);
        if (building == null) return false;
        var avail = TechDefs.Check(building, d, _tech, gm.teamCivs[_teamId], gm);
        if (!avail.canResearch) return false;
        if (!_res.CanAfford(d.food, d.wood, d.gold, d.stone)) return false;
        _res.Deduct(d.food, d.wood, d.gold, d.stone);
        ResearchSystem.Apply(type, _teamId);
        return true;
    }

    BuildingEntity FindResearchBuilding(BuildingType type)
    {
        var gm = GameManager.Instance;
        if (gm == null) return null;
        for (int i = 0; i < gm.buildings.Count; i++)
        {
            var b = gm.buildings[i];
            if (b != null && b.teamId == _teamId && b.type == type && !b.underConstruction && b.hp > 0f)
                return b;
        }
        return null;
    }

    // ── Military: reinforcement ────────────────────────────────────────────────

    // N14.aieco: route unit type to the building it trains from.
    static BuildingType BuildingFor(UnitType t) => t switch
    {
        UnitType.Archer or UnitType.Skirmisher or UnitType.CavalryArcher or
        UnitType.Longbowman or UnitType.ChuKoNu or UnitType.Janissary => BuildingType.ArcheryRange,

        UnitType.Cavalry or UnitType.Camel or
        UnitType.Cataphract or UnitType.Mameluke or UnitType.WarElephant => BuildingType.Stable,

        UnitType.Trebuchet or UnitType.Mangonel or UnitType.Ram or UnitType.Scorpion => BuildingType.SiegeWorkshop,

        UnitType.Medic => BuildingType.Castle,
        UnitType.FishingShip or UnitType.Galley or UnitType.FireShip or UnitType.DemoShip => BuildingType.Dock,

        UnitType.Villager => BuildingType.TownCenter,

        _ => BuildingType.Barracks,  // Militia, Spearman, Halberdier, WoadRaider, etc.
    };

    void TrySpawn(GameManager gm)
    {
        if (CountArmy(gm) >= _armyCap) return;

        // N5.pop: AI respects its own pop-cap (computed by GameManager.RecomputePop)
        if (_res.pop >= _res.popCap) return;

        var pick = ChooseUnit(gm);
        if (pick == null) return;

        // N14.aieco: find an idle team building of the right type and use TrainingQueue.
        var tq = gm.trainingQueue;
        if (tq == null) return;

        BuildingType needed = BuildingFor(pick.Value);
        BuildingEntity prod = null;
        for (int i = 0; i < gm.buildings.Count; i++)
        {
            var b = gm.buildings[i];
            if (b != null && b.teamId == _teamId && b.type == needed
                && !b.underConstruction && b.hp > 0f
                && tq.GetQueueCount(b) < 3)
            { prod = b; break; }
        }
        if (prod == null) return;   // no matching production building ready

        // Find the matching UnitTrainable from the building's list and enqueue it.
        var trainables = prod.GetTrainables();
        for (int i = 0; i < trainables.Length; i++)
        {
            if (trainables[i].unitType == pick.Value)
            {
                tq.Enqueue(prod, trainables[i]);
                return;
            }
        }
    }

    /// <summary>Pick the next unit to reinforce with: keep a thin siege line for
    /// cracking buildings, otherwise rotate frontline/support as ages unlock. Returns
    /// null when nothing affordable/unlocked fits this tick.</summary>
    UnitType? ChooseUnit(GameManager gm)
    {
        Age age = _tech != null ? _tech.age : Age.Dark;

        if (_mapType == MapType.Islands && OwnsProduction(gm, BuildingType.Dock))
        {
            int fishers = CountType(gm, UnitType.FishingShip);
            if (fishers < 2 && _res.CanAfford(0, 75, 0, 0))
                return UnitType.FishingShip;

            int enemyShips = CountEnemyShips(gm);
            int demos = CountType(gm, UnitType.DemoShip);
            if (age >= Age.Castle && enemyShips >= 3 && demos < 1 + enemyShips / 4
                && _res.CanAfford(0, 70, 50, 0))
                return UnitType.DemoShip;

            int fire = CountType(gm, UnitType.FireShip);
            if (age >= Age.Feudal && fire < Mathf.Max(1, enemyShips / 2 + CountArmy(gm) / 10)
                && _res.CanAfford(0, 100, 45, 0))
                return UnitType.FireShip;

            int galleys = CountType(gm, UnitType.Galley);
            if (age >= Age.Feudal && galleys < 2 + CountArmy(gm) / 6 && _res.CanAfford(0, 120, 60, 0))
                return UnitType.Galley;
        }

        // Siege: aim for ~1 Trebuchet per 6 army to demolish structures. GATED on the AI
        // actually owning a Siege Workshop — otherwise BuildingFor(Trebuchet) finds no
        // production building, TrySpawn returns without training, and since `treb` can never
        // grow this branch fires every tick and the AI stops reinforcing entirely (the
        // Castle-age production deadlock). Same for the Medic/Castle pair below.
        if (age >= Age.Castle && OwnsProduction(gm, BuildingType.SiegeWorkshop))
        {
            int treb = CountType(gm, UnitType.Trebuchet);
            if (treb < 1 + CountArmy(gm) / 6 && _res.CanAfford(0, TrebuchetCostWood, TrebuchetCostGold, 0))
                return UnitType.Trebuchet;
            int scorp = CountType(gm, UnitType.Scorpion);
            if (scorp < 1 + CountArmy(gm) / 8 && _res.CanAfford(0, 120, 80, 0))
                return UnitType.Scorpion;
        }

        // Counter-awareness: if the enemy army has many Cavalry, add Spearmen.
        int enemyCav = CountEnemyCavalry(gm);
        int ownSpear = CountType(gm, UnitType.Spearman);
        if (age >= Age.Feudal && enemyCav > 0 && ownSpear < enemyCav / 2 + 1
            && _res.CanAfford(SpearmanCostFood, SpearmanCostWood, 0, 0))
            return UnitType.Spearman;

        // Support: one Medic per 6 army for survivability. Gated on owning a Castle.
        int medics = CountType(gm, UnitType.Medic);
        if (age >= Age.Castle && OwnsProduction(gm, BuildingType.Castle)
            && medics < 1 + CountArmy(gm) / 6
            && _res.CanAfford(MedicCostFood, 0, 0, 0))
            return UnitType.Medic;

        // AISC: profile-weighted frontline selection.
        // Build a weighted candidate list of affordable/unlocked unit types.
        var candidates = new System.Collections.Generic.List<(UnitType type, float w)>(3);
        if (_res.CanAfford(MilitiaCostFood, 0, MilitiaCostGold, 0))
            candidates.Add((UnitType.Militia, _profile.meleeWeight));
        if (age >= Age.Feudal && _res.CanAfford(0, ArcherCostWood, ArcherCostGold, 0))
            candidates.Add((UnitType.Archer, _profile.archerWeight));
        if (age >= Age.Castle && _res.CanAfford(CavalryCostFood, 0, 0, 0))
            candidates.Add((UnitType.Cavalry, _profile.cavalryWeight));
        if (age >= Age.Castle && OwnsProduction(gm, BuildingType.SiegeWorkshop)
            && _res.CanAfford(0, 120, 80, 0))
            candidates.Add((UnitType.Scorpion, _profile.siegeWeight));

        if (candidates.Count == 0) return null;

        float total = 0f;
        foreach (var c in candidates) total += c.w;
        float roll = SimRandom.Value * total; // N3: sim RNG
        float acc  = 0f;
        foreach (var c in candidates)
        {
            acc += c.w;
            if (roll <= acc) return c.type;
        }
        return candidates[candidates.Count - 1].type;
    }

    int CountEnemyCavalry(GameManager gm)
    {
        int n = 0;
        for (int i = 0; i < gm.units.Count; i++)
        {
            var u = gm.units[i];
            if (u != null && u.teamId != _teamId && u.type == UnitType.Cavalry) n++;
        }
        return n;
    }

    int CountEnemyShips(GameManager gm)
    {
        int n = 0;
        for (int i = 0; i < gm.units.Count; i++)
        {
            var u = gm.units[i];
            if (u == null || u.teamId == _teamId) continue;
            if (!(GameManager.Instance?.IsEnemy(_teamId, u.teamId) ?? true)) continue;
            if (u.type == UnitType.Galley || u.type == UnitType.FireShip
                || u.type == UnitType.DemoShip || u.type == UnitType.FishingShip)
                n++;
        }
        return n;
    }

    /// <summary>True if this AI team owns a finished, alive production building of the
    /// given type. Used to gate unit picks so the AI never selects a unit it has no
    /// building to train (which would silently waste the spawn tick).</summary>
    bool OwnsProduction(GameManager gm, BuildingType type)
    {
        for (int i = 0; i < gm.buildings.Count; i++)
        {
            var b = gm.buildings[i];
            if (b != null && b.teamId == _teamId && b.type == type
                && !b.underConstruction && b.hp > 0f)
                return true;
        }
        return false;
    }

    // ── Military: coordinated army state machine ───────────────────────────────

    /// <summary>Drives the whole army through Gather → Rally → Attack → Retreat as
    /// one body. Runs every <see cref="AssessInterval"/> seconds.</summary>
    void Assess(GameManager gm)
    {
        var army = CollectArmy(gm);
        _stanceTicks++;

        CheckGarrison(gm);
        switch (_stance)
        {
            case Stance.Gathering:  TickGathering(gm, army);  break;
            case Stance.Rallying:   TickRallying(gm, army);   break;
            case Stance.Attacking:  TickAttacking(gm, army);  break;
            case Stance.Retreating: TickRetreating(gm, army); break;
        }
    }

    /// <summary>Garrison villagers inside the home TC when enemy military is nearby.
    /// They un-garrison naturally once the threat clears (BuildingCombatSystem auto-fires
    /// while garrisoned). This is a simple heuristic — no per-unit micro needed.</summary>
    void CheckGarrison(GameManager gm)
    {
        if (gm.garrison == null) return;
        BuildingEntity homeTc = null;
        for (int i = 0; i < gm.buildings.Count; i++)
        {
            var b = gm.buildings[i];
            if (b != null && b.teamId == _teamId && b.type == BuildingType.TownCenter) { homeTc = b; break; }
        }
        if (homeTc == null) return;

        float threatSq = 28f * 28f;
        bool threatened = false;
        for (int i = 0; i < gm.units.Count; i++)
        {
            var u = gm.units[i];
            if (u == null || u.teamId == _teamId) continue;
            // AIDP: skip allies/neutrals — only enemies threaten our base.
            if (!(GameManager.Instance?.IsEnemy(_teamId, u.teamId) ?? true)) continue;
            Vector3 d = u.transform.position - homeTc.transform.position; d.y = 0;
            if (d.sqrMagnitude <= threatSq) { threatened = true; break; }
        }

        if (threatened)
        {
            for (int i = 0; i < gm.units.Count; i++)
            {
                var u = gm.units[i];
                if (u == null || u.teamId != _teamId || u.type != UnitType.Villager) continue;
                if (!u.isGarrisoned && homeTc.GarrisonCount < homeTc.GarrisonCapacity)
                    u.GarrisonOrder(homeTc);
            }
        }
        else
        {
            if (homeTc.GarrisonCount > 0) gm.garrison.UngarrisonAll(homeTc);
        }
    }

    /// <summary>Hold until the army reaches its threshold, then pick a target and
    /// send everyone to the rally point.</summary>
    void TickGathering(GameManager gm, List<UnitEntity> army)
    {
        if (army.Count < _rushThreshold) return;
        // BAL.ai: hold the first push until the difficulty's grace window passes,
        // unless the army cap is already full (then attack anyway).
        if (_matchClock < _minFirstPushTime && army.Count < _armyCap) return;

        var target = FindBestTarget(gm, army);
        if (target == null) return;

        _target     = target;
        _rallyPoint = ComputeRally(target.Transform.position);
        SetStance(Stance.Rallying);
        for (int i = 0; i < army.Count; i++)
            army[i].MoveTo(RallyPosFor(army[i], i));
    }

    /// <summary>Wait for the force to mass at the rally point (or time out), then
    /// commit to the attack together.</summary>
    void TickRallying(GameManager gm, List<UnitEntity> army)
    {
        if (army.Count == 0) { SetStance(Stance.Gathering); return; }

        if (_target == null || !_target.IsAlive)
        {
            // AIWN: if an enemy Wonder or Relic countdown is ticking, prioritize it.
            var urgentTarget = FindWinConditionTarget(gm);
            var t = urgentTarget ?? FindBestTarget(gm, army);
            if (t == null) { SetStance(Stance.Gathering); return; }
            _target = t; _rallyPoint = ComputeRally(t.Transform.position);
        }

        bool massed   = FractionNear(army, _rallyPoint, RallyRadius) >= ArriveFraction;
        bool timedOut = _stanceTicks >= RallyTimeoutTicks;
        if (massed || timedOut)
        {
            _attackForce = army.Count;
            SetStance(Stance.Attacking);
            CommandAttack(gm, army);
        }
    }

    /// <summary>Press the target. Retreat once losses cross the personality's
    /// threshold; re-target (don't disband) if the current objective falls.</summary>
    void TickAttacking(GameManager gm, List<UnitEntity> army)
    {
        if (army.Count == 0) { SetStance(Stance.Gathering); return; }

        // Bled too much — pull back to home to regroup.
        if (army.Count <= _attackForce * (1f - _retreatLoss))
        {
            SetStance(Stance.Retreating);
            for (int i = 0; i < army.Count; i++)
                army[i].MoveTo(Scatter(_home, i));
            return;
        }

        if (_target == null || !_target.IsAlive)
        {
            // AIWN: re-check win-condition threat on target loss.
            var t = FindWinConditionTarget(gm) ?? FindBestTarget(gm, army);
            if (t == null) { SetStance(Stance.Gathering); return; }
            _target = t;
        }
        CommandAttack(gm, army);
    }

    /// <summary>Fall back home and regroup, then start gathering for the next push.</summary>
    void TickRetreating(GameManager gm, List<UnitEntity> army)
    {
        if (army.Count == 0) { SetStance(Stance.Gathering); return; }

        bool home     = FractionNear(army, _home, RallyRadius * 1.5f) >= 0.6f;
        bool timedOut = _stanceTicks >= RetreatTimeoutTicks;
        if (home || timedOut) SetStance(Stance.Gathering);
    }

    /// <summary>Order idle units onto their role's objective: siege (Trebuchet)
    /// hangs back and demolishes the nearest structure (3× anti-structure), the rest
    /// press the shared strategic target. Units that auto-acquired a closer foe
    /// (CombatSystem aggro) keep their fight.</summary>
    void CommandAttack(GameManager gm, List<UnitEntity> army)
    {
        for (int i = 0; i < army.Count; i++)
        {
            var u = army[i];
            if (u.attackTarget != null && u.attackTarget.IsAlive) continue;

            // Medic: stay behind army and heal most-wounded ally (handled by CombatSystem);
            // just keep them near the army center so they're in range.
            if (u.type == UnitType.Medic)
            {
                if (u.state == UnitState.Idle && army.Count > 1)
                {
                    var center = Vector3.zero; int c = 0;
                    for (int j = 0; j < army.Count; j++)
                        if (army[j] != u && army[j] != null) { center += army[j].transform.position; c++; }
                    if (c > 0) u.MoveTo(center / c);
                }
                continue;
            }

            // Scout: patrol toward the player base independently rather than joining the army.
            if (u.type == UnitType.Scout)
            {
                if (u.state == UnitState.Idle)
                    u.MoveTo(gm.units.Count > 0
                        ? gm.units[SimRandom.Range(0, gm.units.Count)].transform.position // N3: sim RNG
                        : Vector3.zero);
                continue;
            }

            if (u.type == UnitType.Trebuchet)
            {
                var b = NearestEnemyBuilding(gm, u.transform.position);
                u.AttackOrder((IDamageable)b ?? _target);
            }
            else
            {
                u.AttackOrder(_target);
            }
        }
    }

    void SetStance(Stance s) { _stance = s; _stanceTicks = 0; }

    // ── Economy ───────────────────────────────────────────────────────────────

    void EconomyTick(GameManager gm)
    {
        _rebalanceTimer -= GatherCheckInterval;
        bool fullRebalance = _rebalanceTimer <= 0f;
        if (fullRebalance) _rebalanceTimer = 60f; // full rebalance every ~60s
        AssignVillagersToGather(gm, fullRebalance);
        TryTrainVillager(gm);
        TryRepairBuildings(gm);     // N14.aieco: idle villagers repair damaged buildings
        CheckTcRecovery(gm);        // N14.aieco: retreat if TC is critically damaged
    }

    // N14.aieco: send idle villagers to repair any friendly building below 60% HP.
    void TryRepairBuildings(GameManager gm)
    {
        for (int i = 0; i < gm.buildings.Count; i++)
        {
            var b = gm.buildings[i];
            if (b == null || b.teamId != _teamId || b.hp <= 0f) continue;
            if (b.hp >= b.maxHp * 0.6f) continue; // only repair below 60%
            // Find the nearest idle villager.
            UnitEntity repairer = null;
            float bestDist = float.MaxValue;
            for (int j = 0; j < gm.units.Count; j++)
            {
                var u = gm.units[j];
                if (u == null || u.teamId != _teamId || u.type != UnitType.Villager) continue;
                if (u.state != UnitState.Idle) continue;
                float d = (u.transform.position - b.transform.position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; repairer = u; }
            }
            if (repairer != null) repairer.constructTarget = b; // BuildSystem.Tick picks this up
        }
    }

    // N14.aieco: if TC HP < 30%, switch to Retreating stance so the army defends home.
    void CheckTcRecovery(GameManager gm)
    {
        for (int i = 0; i < gm.buildings.Count; i++)
        {
            var b = gm.buildings[i];
            if (b == null || b.teamId != _teamId || b.type != BuildingType.TownCenter) continue;
            if (b.hp < b.maxHp * 0.3f && _stance != Stance.Retreating)
                SetStance(Stance.Retreating);
            break;
        }
    }

    // Desired stock targets per resource — gatherers reassign away from oversupplied resources.
    const int TargetFood  = 400;
    const int TargetWood  = 300;
    const int TargetGold  = 200;
    const int TargetStone = 150;

    void AssignVillagersToGather(GameManager gm, bool rebalanceAll = false)
    {
        var gather = gm.gather;
        if (gather == null) return;

        // On full-rebalance ticks, redirect gatherers sitting on a massively oversupplied
        // resource to the most-needed resource.
        if (rebalanceAll)
        {
            for (int i = 0; i < gm.units.Count; i++)
            {
                var u = gm.units[i];
                if (u == null || u.teamId != _teamId || u.type != UnitType.Villager) continue;
                if (u.gatherTarget == null) continue;
                int stock  = StockFor(u.gatherTarget.kind);
                int target = TargetFor(u.gatherTarget.kind);
                // Redirect if this resource is >2× target AND some other resource is <50% target.
                if (stock > target * 2 && HasUndersupplied(target / 2))
                {
                    ResourceKind needed = MostNeededResource();
                    if (needed != u.gatherTarget.kind)
                    {
                        var newNode = FindNearestNode(gm, u.transform.position, needed);
                        if (newNode != null) gather.AssignGather(u, newNode);
                    }
                }
            }
        }

        // Assign idle villagers to whichever resource is most undersupplied.
        for (int i = 0; i < gm.units.Count; i++)
        {
            var u = gm.units[i];
            if (u == null || u.teamId != _teamId || u.type != UnitType.Villager) continue;
            if (u.state != UnitState.Idle) continue;

            ResourceKind best = MostNeededResource();
            var node = FindNearestNode(gm, u.transform.position, best)
                    ?? FindNearestNode(gm, u.transform.position, ResourceKind.Food)
                    ?? FindNearestNode(gm, u.transform.position, ResourceKind.Wood)
                    ?? FindNearestNode(gm, u.transform.position, ResourceKind.Gold);
            if (node != null) gather.AssignGather(u, node);
        }
    }

    int StockFor(ResourceKind kind) => kind switch
    {
        ResourceKind.Food  => _res.food,
        ResourceKind.Wood  => _res.wood,
        ResourceKind.Gold  => _res.gold,
        ResourceKind.Stone => _res.stone,
        _                  => 0,
    };

    static int TargetFor(ResourceKind kind) => kind switch
    {
        ResourceKind.Food  => TargetFood,
        ResourceKind.Wood  => TargetWood,
        ResourceKind.Gold  => TargetGold,
        ResourceKind.Stone => TargetStone,
        _                  => TargetWood,
    };

    bool HasUndersupplied(int threshold)
        => _res.food < threshold || _res.wood < threshold || _res.gold < threshold;

    // Pick the resource with the highest (target - stock) ratio — most in need.
    // Food has a hard priority floor: if stock < 80 it always beats gold/stone.
    ResourceKind MostNeededResource()
    {
        if (_res.food < 80) return ResourceKind.Food; // emergency floor

        float foodNeed  = Mathf.Max(0, TargetFood  - _res.food)  / (float)TargetFood;
        float woodNeed  = Mathf.Max(0, TargetWood  - _res.wood)  / (float)TargetWood;
        float goldNeed  = Mathf.Max(0, TargetGold  - _res.gold)  / (float)TargetGold;

        if (foodNeed >= woodNeed && foodNeed >= goldNeed) return ResourceKind.Food;
        if (woodNeed >= goldNeed)                         return ResourceKind.Wood;
        return ResourceKind.Gold;
    }

    void TryTrainVillager(GameManager gm)
    {
        if (!_res.CanAfford(VillagerCostFood, 0, 0, 0)) return;

        // N5.pop: villager training must respect the pop-cap just like military
        // (TrySpawn). Without this the AI could stack villagers past popCap and then
        // permanently deadlock military production (which IS pop-gated).
        if (_res.pop >= _res.popCap) return;

        // Require a live Town Center. No TC (Nomad start, or TC destroyed) means no
        // free villagers — the old code magically spawned them at _home regardless.
        BuildingEntity tc = null;
        for (int i = 0; i < gm.buildings.Count; i++)
        {
            var b = gm.buildings[i];
            if (b != null && b.teamId == _teamId && b.type == BuildingType.TownCenter
                && !b.underConstruction && b.hp > 0f)
            { tc = b; break; }
        }
        if (tc == null) return;

        int count = 0;
        for (int i = 0; i < gm.units.Count; i++)
        {
            var u = gm.units[i];
            if (u != null && u.teamId == _teamId && u.type == UnitType.Villager) count++;
        }
        if (count >= _villagerTarget) return;

        _res.Deduct(VillagerCostFood, 0, 0, 0);
        Vector3 home = tc.transform.position;
        Vector3 fwd = home.sqrMagnitude > 0.01f ? (-home).normalized : Vector3.forward;
        Vector3 pos = home - fwd * 2f + Vector3.right * SimRandom.Range(-1.5f, 1.5f); // N3: sim RNG
        var v = UnitFactory.Villager(_unitsRoot, pos, _teamColor);
        v.teamId = _teamId;
        gm.RegisterUnit(v);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Rally midway toward the target but biased home (40%, capped at 18u)
    /// so the army masses before committing to enemy ground.</summary>
    Vector3 ComputeRally(Vector3 targetPos)
    {
        Vector3 to   = targetPos - _home;
        float   dist = to.magnitude;
        Vector3 dir  = dist > 0.01f ? to / dist : Vector3.forward;
        return _home + dir * Mathf.Min(dist * 0.4f, 18f);
    }

    /// <summary>Deterministic golden-angle spread so massed units don't all path to
    /// the exact same point and jitter against each other.</summary>
    static Vector3 Scatter(Vector3 center, int i)
    {
        float a = i * 2.39996323f;            // golden angle (radians)
        float r = 1.5f + (i % 4) * 1.2f;      // 1.5–5.1u rings, within RallyRadius
        return center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
    }

    /// <summary>Lite formation: melee holds the rally line; ranged sits a little
    /// behind it and siege further back (toward home) so the front screens them.</summary>
    Vector3 RallyPosFor(UnitEntity u, int i)
    {
        Vector3 back = _home - _rallyPoint; back.y = 0f;
        back = back.sqrMagnitude > 0.01f ? back.normalized : Vector3.zero;
        float depth = u.type switch
        {
            UnitType.Trebuchet => 5f,
            UnitType.Archer    => 3f,
            _                  => 0f,
        };
        return Scatter(_rallyPoint, i) + back * depth;
    }

    float FractionNear(List<UnitEntity> army, Vector3 point, float radius)
    {
        if (army.Count == 0) return 0f;
        float r2 = radius * radius;
        int near = 0;
        for (int i = 0; i < army.Count; i++)
        {
            Vector3 d = army[i].transform.position - point; d.y = 0f;
            if (d.sqrMagnitude <= r2) near++;
        }
        return (float)near / army.Count;
    }

    List<UnitEntity> CollectArmy(GameManager gm)
    {
        var list = new List<UnitEntity>();
        for (int i = 0; i < gm.units.Count; i++)
        {
            var u = gm.units[i];
            if (u != null && u.teamId == _teamId && IsMilitary(u)) list.Add(u);
        }
        return list;
    }

    ResourceNode FindNearestNode(GameManager gm, Vector3 pos, ResourceKind kind)
    {
        ResourceNode best   = null;
        float        bestSq = float.MaxValue;
        var          nodes  = gm.nodes;
        for (int i = 0; i < nodes.Count; i++)
        {
            var n = nodes[i];
            if (n == null || n.Depleted || n.kind != kind || !n.HasRoom) continue;
            float dx = n.transform.position.x - pos.x;
            float dz = n.transform.position.z - pos.z;
            float sq = dx * dx + dz * dz;
            if (sq < bestSq) { bestSq = sq; best = n; }
        }
        return best;
    }

    int CountArmy(GameManager gm)
    {
        int n = 0;
        for (int i = 0; i < gm.units.Count; i++)
        {
            var u = gm.units[i];
            if (u != null && u.teamId == _teamId && IsMilitary(u)) n++;
        }
        return n;
    }

    int CountType(GameManager gm, UnitType type)
    {
        int n = 0;
        for (int i = 0; i < gm.units.Count; i++)
        {
            var u = gm.units[i];
            if (u != null && u.teamId == _teamId && u.type == type) n++;
        }
        return n;
    }

    Vector3 ArmyCentroid(List<UnitEntity> army)
    {
        if (army == null || army.Count == 0) return _home;
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < army.Count; i++) sum += army[i].transform.position;
        return sum / army.Count;
    }

    /// <summary>Pick the army's strategic objective by value, not raw distance:
    /// enemy villagers (economy) rank highest, then the Town Center, then military
    /// and other structures — each discounted by distance from the army's centre.
    /// This makes the AI hunt economy and press the win condition instead of poking
    /// whatever happens to be nearest.</summary>
    IDamageable FindBestTarget(GameManager gm, List<UnitEntity> army)
    {
        Vector3 origin = ArmyCentroid(army);
        IDamageable best = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < gm.units.Count; i++)
        {
            var u = gm.units[i];
            // AIDP: skip self and non-enemies (allied/neutral).
            if (u == null || !gm.IsEnemy(_teamId, u.teamId)) continue;
            float s = UnitValue(u) - DistPenalty(u.transform.position, origin);
            if (s > bestScore) { bestScore = s; best = u; }
        }
        for (int i = 0; i < gm.buildings.Count; i++)
        {
            var b = gm.buildings[i];
            if (b == null || !gm.IsEnemy(_teamId, b.teamId)) continue;
            float s = BuildingValue(b) - DistPenalty(b.transform.position, origin);
            if (s > bestScore) { bestScore = s; best = b; }
        }
        return best;
    }

    // AIWN: return a Wonder/Relic-carrier that is actively counting down to victory,
    // so the AI prioritizes destroying it over general economy harassment.
    IDamageable FindWinConditionTarget(GameManager gm)
    {
        var match = gm.match;
        if (match == null || match.TimeRemaining == float.MaxValue) return null;

        // If VictoryStatus mentions a countdown, find an enemy Wonder or Relic holder.
        string status = match.VictoryStatus;
        if (string.IsNullOrEmpty(status)) return null;

        // Search for enemy Wonder buildings first (highest urgency).
        for (int i = 0; i < gm.buildings.Count; i++)
        {
            var b = gm.buildings[i];
            if (b == null || b.type != BuildingType.Wonder) continue;
            if (gm.IsEnemy(_teamId, b.teamId)) return b;
        }
        return null;
    }

    static float UnitValue(UnitEntity u) =>
        u.type == UnitType.Villager ? 65f : 35f;   // economy harassment over trading blows

    static float BuildingValue(BuildingEntity b) => b.type switch
    {
        BuildingType.TownCenter                                              => 60f, // win condition
        BuildingType.Barracks or BuildingType.ArcheryRange
            or BuildingType.Stable or BuildingType.Castle                    => 45f, // cut production
        BuildingType.Farm or BuildingType.LumberCamp or BuildingType.MiningCamp
            or BuildingType.Mill or BuildingType.Market                      => 40f, // cut economy
        _                                                                    => 25f, // houses, etc.
    };

    static float DistPenalty(Vector3 worldPos, Vector3 origin)
    {
        float dx = worldPos.x - origin.x, dz = worldPos.z - origin.z;
        return Mathf.Sqrt(dx * dx + dz * dz) / 8f;
    }

    BuildingEntity NearestEnemyBuilding(GameManager gm, Vector3 pos)
    {
        BuildingEntity best = null;
        float bestSq = float.MaxValue;
        for (int i = 0; i < gm.buildings.Count; i++)
        {
            var b = gm.buildings[i];
            if (b == null || b.teamId == _teamId) continue;
            float dx = b.transform.position.x - pos.x;
            float dz = b.transform.position.z - pos.z;
            float sq = dx * dx + dz * dz;
            if (sq < bestSq) { bestSq = sq; best = b; }
        }
        return best;
    }

    // Denylist: everything that is NOT a frontline combat unit. Counting via a
    // denylist means Spearman, Skirmisher, Camel, Mangonel, Cavalry Archer, every
    // unique/tier-promoted unit, Eagle, ships etc. all count toward the army —
    // fixing the old allowlist that silently ignored the counter units the AI itself
    // trains (Spearman vs cavalry) and broke army-cap / rush-threshold / retreat logic.
    static bool IsMilitary(UnitEntity u) =>
        u.type != UnitType.Villager   && u.type != UnitType.Scout &&
        u.type != UnitType.Medic      && u.type != UnitType.TradeCart &&
        u.type != UnitType.King       && u.type != UnitType.FishingShip &&
        u.type != UnitType.Monk;
}
