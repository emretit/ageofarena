#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor/batchmode validator for the AoE2 parity content set. This focuses on
/// the shared availability gate, the new tech/unit rows, and a small AI smoke
/// test so the added content is not just present in data but actually reachable.
/// </summary>
public static class ContentParityQaValidator
{
    sealed class Harness
    {
        public GameObject root;
        public GameManager gm;
        public Transform unitsRoot;
        public Transform buildingsRoot;
        public readonly List<string> report = new();
    }

    [MenuItem("Tools/Age of Arena/Run Content Parity QA")]
    public static void RunFromMenu()
    {
        RunOrThrow();
        Debug.Log("[ContentParityQaValidator] Content parity QA passed.");
    }

    public static void RunFromCommandLine()
    {
        try
        {
            RunOrThrow();
            Debug.Log("[ContentParityQaValidator] Content parity QA passed.");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogError("[ContentParityQaValidator] Content parity QA failed:\n" + ex);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            throw;
        }
    }

    static void RunOrThrow()
    {
        var report = new List<string>();
        RunScenario("Availability and costs", report, ValidateAvailabilityAndCosts);
        RunScenario("Tech stat effects", report, ValidateTechStatEffects);
        RunScenario("Economy and relic flow", report, ValidateEconomyFlow);
        RunScenario("AI smoke", report, ValidateAiSmoke);

        string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "content-parity-qa-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
        File.WriteAllLines(path, report);
        Debug.Log("[ContentParityQaValidator] Wrote report: " + path);
    }

    static void RunScenario(string label, List<string> report, Action<Harness> validate)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var h = CreateHarness(label);
        try
        {
            validate(h);
            report.Add("[PASS] " + label);
        }
        finally
        {
            if (h.root != null) UnityEngine.Object.DestroyImmediate(h.root);
        }
    }

    static Harness CreateHarness(string name)
    {
        var h = new Harness();
        h.root = new GameObject("ContentParity_" + name.Replace(" ", ""));
        h.unitsRoot = new GameObject("Units").transform;
        h.unitsRoot.SetParent(h.root.transform, false);
        h.buildingsRoot = new GameObject("Buildings").transform;
        h.buildingsRoot.SetParent(h.root.transform, false);

        h.gm = h.root.AddComponent<GameManager>();
        InvokePrivate(h.gm, "Awake");
        h.gm.TeamCount = 4;
        h.gm.teamRes[0] = new ResourceManager { food = 5000, wood = 5000, gold = 5000, stone = 5000 };
        h.gm.teamRes[1] = new ResourceManager { food = 5000, wood = 5000, gold = 5000, stone = 5000 };
        h.gm.teamTech[0] = new TechState { age = Age.Feudal };
        h.gm.teamTech[1] = new TechState { age = Age.Castle };
        h.gm.teamCivs[0] = Civilization.None;
        h.gm.teamCivs[1] = Civilization.None;
        return h;
    }

    static void ValidateAvailabilityAndCosts(Harness h)
    {
        var tc = CreateBuilding(h, BuildingType.TownCenter, 0);
        h.gm.teamTech[0].age = Age.Dark;

        var feudal = TechDefs.Get(TechType.FeudalAge);
        var check1 = TechDefs.Check(tc, feudal, h.gm.teamTech[0], h.gm.teamCivs[0], h.gm);
        Require(!check1.canResearch, "Age advance unlocked with only one substantial building.");

        CreateBuilding(h, BuildingType.Barracks, 0);
        CreateBuilding(h, BuildingType.Market, 0);
        var check3 = TechDefs.Check(tc, feudal, h.gm.teamTech[0], h.gm.teamCivs[0], h.gm);
        Require(check3.canResearch, "Age advance did not unlock after two substantial buildings.");

        h.gm.teamTech[0].age = Age.Imperial;
        h.gm.teamCivs[0] = Civilization.Franks;
        var barracks = CreateBuilding(h, BuildingType.Barracks, 0);
        var eliteEagle = TechDefs.Get(TechType.EliteEagle);
        var deny = TechDefs.Check(barracks, eliteEagle, h.gm.teamTech[0], h.gm.teamCivs[0], h.gm);
        Require(!deny.canResearch, "Civ-denied tech unexpectedly became researchable.");

        h.gm.teamTech[0].Mark(TechType.Supplies);
        h.gm.teamTech[0].age = Age.Feudal;
        var barracksTrainables = barracks.GetTrainables();
        bool foundMilitia = false;
        foreach (var t in barracksTrainables)
        {
            if (t.unitType != UnitType.Militia) continue;
            foundMilitia = true;
            Require(t.food == 45, "Supplies did not reduce Militia food cost.");
        }
        Require(foundMilitia, "Barracks no longer exposes Militia.");

        var siege = CreateBuilding(h, BuildingType.SiegeWorkshop, 0);
        h.gm.teamTech[0].age = Age.Castle;
        var siegeTrainables = siege.GetTrainables();
        bool foundScorpion = false;
        foreach (var t in siegeTrainables)
            if (t.unitType == UnitType.Scorpion) foundScorpion = true;
        Require(foundScorpion, "Siege Workshop does not expose Scorpion.");
    }

    static void ValidateTechStatEffects(Harness h)
    {
        var tech = new TechState();
        Require(Mathf.Approximately(tech.CarryCapacityMult, 1f), "Base carry capacity changed.");

        tech.Mark(TechType.Wheelbarrow);
        Require(Mathf.Approximately(tech.CarryCapacityMult, 1.25f), "Wheelbarrow carry multiplier is wrong.");
        Require(Mathf.Approximately(tech.MoveSpeedMult(UnitType.Villager), 1.1f), "Wheelbarrow villager speed is wrong.");

        tech.Mark(TechType.HandCart);
        Require(Mathf.Approximately(tech.CarryCapacityMult, 1.5f), "Hand Cart carry multiplier is wrong.");
        Require(Mathf.Approximately(tech.MoveSpeedMult(UnitType.Villager), 1.2f), "Hand Cart villager speed is wrong.");

        tech = new TechState();
        tech.Mark(TechType.Squires);
        tech.Mark(TechType.Gambesons);
        tech.Mark(TechType.ThumbRing);
        Require(tech.MoveSpeedMult(UnitType.Militia) > 1f, "Squires did not speed up infantry.");
        Require(tech.PierceArmorBonus(UnitType.Militia) > 0f, "Gambesons did not add pierce armor.");
        Require(tech.AttackIntervalMult(UnitType.Archer) < 1f, "Thumb Ring did not speed up archers.");

        tech = new TechState();
        float baseGalleyAttack = tech.AttackBonus(UnitType.Galley);
        float baseGalleyHp = tech.HpBonus(UnitType.Galley);
        tech.Mark(TechType.WarGalley);
        Require(tech.AttackBonus(UnitType.Galley) > baseGalleyAttack, "War Galley did not raise Galley attack.");
        Require(tech.HpBonus(UnitType.Galley) > baseGalleyHp, "War Galley did not raise Galley HP.");
        float warAttack = tech.AttackBonus(UnitType.Galley);
        float warHp = tech.HpBonus(UnitType.Galley);
        tech.Mark(TechType.Galleon);
        Require(tech.AttackBonus(UnitType.Galley) > warAttack, "Galleon did not further raise Galley attack.");
        Require(tech.HpBonus(UnitType.Galley) > warHp, "Galleon did not further raise Galley HP.");
    }

    static void ValidateEconomyFlow(Harness h)
    {
        // Tribute tax tiers.
        h.gm.teamRes[0].wood = 1000;
        h.gm.teamRes[1].wood = 0;
        h.gm.teamTech[0] = new TechState();
        Require(TributeSystem.Tribute(0, 1, ResourceKind.Wood, 100), "Base tribute failed.");
        Require(h.gm.teamRes[0].wood == 900, "Tribute sender did not pay full amount.");
        Require(h.gm.teamRes[1].wood == 70, "Base tribute tax is wrong.");

        h.gm.teamRes[0].wood = 1000;
        h.gm.teamRes[1].wood = 0;
        h.gm.teamTech[0].Mark(TechType.Coinage);
        Require(TributeSystem.Tribute(0, 1, ResourceKind.Wood, 100), "Coinage tribute failed.");
        Require(h.gm.teamRes[1].wood == 80, "Coinage tribute tax is wrong.");

        h.gm.teamRes[0].wood = 1000;
        h.gm.teamRes[1].wood = 0;
        h.gm.teamTech[0].Mark(TechType.Banking);
        Require(TributeSystem.Tribute(0, 1, ResourceKind.Wood, 100), "Banking tribute failed.");
        Require(h.gm.teamRes[1].wood == 100, "Banking did not remove tribute tax.");

        // Market spread with Guilds.
        MarketSystem.Reset();
        var baseSell = MarketSystem.SellGold(ResourceKind.Wood);
        var baseBuy = MarketSystem.BuyCost(ResourceKind.Wood);
        h.gm.teamTech[0] = new TechState();
        h.gm.teamTech[0].Mark(TechType.Guilds);
        var guildSell = MarketSystem.SellGold(ResourceKind.Wood);
        var guildBuy = MarketSystem.BuyCost(ResourceKind.Wood);
        Require(guildSell > baseSell && guildBuy < baseBuy, "Guilds did not narrow the market spread.");

        // Farm reseed / capacity.
        var farmGo = new GameObject("FarmNode");
        try
        {
            var farm = farmGo.AddComponent<ResourceNode>();
            farm.kind = ResourceKind.Food;
            farm.ownerTeamId = 0;
            farm.renewable = true;
            farm.destroyOnDeplete = false;
            farm.reseedWoodCost = 60;
            farm.amount = 0;
            farm.maxAmount = 100;
            h.gm.teamRes[0].wood = 1000;
            h.gm.teamTech[0] = new TechState();
            h.gm.teamTech[0].Mark(TechType.HorseCollar);
            farm.SimStep(1f);
            Require(farm.amount == 175, "Farm reseed capacity bonus is wrong.");
            Require(h.gm.teamRes[0].wood == 940, "Farm reseed did not deduct wood.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(farmGo);
        }

        // Relic deposit at a Monastery.
        var monastery = CreateBuilding(h, BuildingType.Monastery, 0);
        monastery.transform.position = Vector3.zero;

        var monkGo = new GameObject("Monk");
        var relicGo = new GameObject("Relic");
        try
        {
            var monk = monkGo.AddComponent<UnitEntity>();
            monk.type = UnitType.Monk;
            monk.teamId = 0;
            monk.transform.position = Vector3.zero;
            monk.isCarryingRelic = true;

            var relic = relicGo.AddComponent<RelicEntity>();
            relic.carrier = monk;
            relic.heldInMonastery = false;
            relic.transform.position = Vector3.zero;

            var rs = h.root.AddComponent<RelicSystem>();
            rs.Tick(new List<UnitEntity> { monk }, new List<RelicEntity> { relic }, 0.1f);

            Require(relic.heldInMonastery, "Relic was not deposited in the Monastery.");
            Require(relic.controllingTeam == 0, "Relic deposit did not lock ownership.");
            Require(!monk.isCarryingRelic, "Monk still thinks it is carrying the relic.");
            Require(relic.carrier == null, "Relic carrier was not cleared after deposit.");

            int beforeGold = h.gm.teamRes[0].gold;
            for (int i = 0; i < 30; i++)
                rs.Tick(new List<UnitEntity> { monk }, new List<RelicEntity> { relic }, 0.2f);
            Require(h.gm.teamRes[0].gold > beforeGold, "Deposited relic did not trickle Monastery gold.");
        }
        finally
        {
            if (h.root.GetComponent<RelicSystem>() != null)
                UnityEngine.Object.DestroyImmediate(h.root.GetComponent<RelicSystem>());
            UnityEngine.Object.DestroyImmediate(monkGo);
            UnityEngine.Object.DestroyImmediate(relicGo);
        }

        // Fishing Ship loop against a natural fish pond.
        CreateBuilding(h, BuildingType.Dock, 0);
        var pond = ResourceFactory.FishPond(h.root.transform, new Vector3(1f, 0f, 0f));
        h.gm.nodes.Add(pond);
        ValidateFishingDeposit(h, pond, "FishPond");

        // Fish Trap registers its own co-located pond node from BuildingEntity.Start().
        int beforeNodes = h.gm.nodes.Count;
        var trap = CreateBuilding(h, BuildingType.FishTrap, 0);
        trap.transform.position = new Vector3(1.5f, 0f, 0f);
        InvokePrivate(trap, "Start");
        Require(h.gm.nodes.Count > beforeNodes, "FishTrap did not register a fish node.");
        ValidateFishingDeposit(h, h.gm.nodes[h.gm.nodes.Count - 1], "FishTrap");
    }

    static void ValidateAiSmoke(Harness h)
    {
        h.gm.teamTech[1].age = Age.Castle;
        h.gm.teamRes[1].food = 5000;
        h.gm.teamRes[1].wood = 5000;
        h.gm.teamRes[1].gold = 5000;
        h.gm.teamRes[1].stone = 5000;

        CreateBuilding(h, BuildingType.TownCenter, 1);
        CreateBuilding(h, BuildingType.Blacksmith, 1);
        CreateBuilding(h, BuildingType.Dock, 1);
        CreateBuilding(h, BuildingType.Market, 1);
        CreateBuilding(h, BuildingType.Monastery, 1);
        CreateBuilding(h, BuildingType.SiegeWorkshop, 1);

        var aiGo = new GameObject("AI");
        aiGo.transform.SetParent(h.root.transform, false);
        var ai = aiGo.AddComponent<EnemyAI>();
        ai.Init(1, Color.red, Vector3.zero, h.unitsRoot, AIPersonality.Boomer, MapType.Islands);
        InvokePrivate(ai, "Start");
        InvokePrivate(ai, "TryAdvanceTech");
        Require(h.gm.teamTech[1].Has(TechType.WarGalley), "AI did not prefer a Castle-age naval tech on Islands.");
        InvokePrivate(ai, "TryAdvanceTech");
        Require(h.gm.teamTech[1].Has(TechType.Sanctity), "AI did not prefer a Castle-age Monk tech.");

        h.gm.teamTech[1].age = Age.Imperial;
        InvokePrivate(ai, "TryAdvanceTech");
        Require(h.gm.teamTech[1].Has(TechType.Galleon), "AI did not prefer an Imperial-age naval tech on Islands.");
        InvokePrivate(ai, "TryAdvanceTech");
        Require(h.gm.teamTech[1].Has(TechType.Guilds), "AI did not prefer an Imperial-age Market tech.");
        InvokePrivate(ai, "TryAdvanceTech");
        Require(h.gm.teamTech[1].Has(TechType.CappedRam), "AI did not prioritize an Imperial siege tech chain.");

        object pick = InvokePrivate(ai, "ChooseUnit", h.gm);
        Require(pick != null, "AI did not choose any unit on Islands.");
        Require((UnitType)pick == UnitType.FishingShip, "AI Islands path did not pick a naval eco unit.");

        CreateUnit(h, UnitType.FishingShip, 1);
        CreateUnit(h, UnitType.FishingShip, 1);
        CreateUnit(h, UnitType.Galley, 0);
        CreateUnit(h, UnitType.Galley, 0);
        CreateUnit(h, UnitType.Galley, 0);
        CreateUnit(h, UnitType.FireShip, 0);
        pick = InvokePrivate(ai, "ChooseUnit", h.gm);
        Require(pick != null && (UnitType)pick == UnitType.DemoShip,
            "AI Islands path did not counter a dense enemy fleet with Demo Ship.");
    }

    static BuildingEntity CreateBuilding(Harness h, BuildingType type, int teamId)
    {
        var go = new GameObject(type.ToString() + "_T" + teamId);
        go.transform.SetParent(h.buildingsRoot, false);
        var b = go.AddComponent<BuildingEntity>();
        b.type = type;
        b.teamId = teamId;
        b.underConstruction = false;
        b.hp = 100f;
        b.maxHp = 100f;
        h.gm.buildings.Add(b);
        return b;
    }

    static UnitEntity CreateUnit(Harness h, UnitType type, int teamId)
    {
        var go = new GameObject(type.ToString() + "_T" + teamId);
        go.transform.SetParent(h.unitsRoot, false);
        var u = go.AddComponent<UnitEntity>();
        u.type = type;
        u.teamId = teamId;
        u.isNaval = type == UnitType.FishingShip || type == UnitType.Galley
            || type == UnitType.FireShip || type == UnitType.DemoShip;
        u.hp = 50f;
        u.maxHp = 50f;
        h.gm.units.Add(u);
        return u;
    }

    static void ValidateFishingDeposit(Harness h, ResourceNode node, string label)
    {
        var gather = h.root.GetComponent<GatherSystem>() ?? h.root.AddComponent<GatherSystem>();
        h.gm.gather = gather;

        int beforeFood = h.gm.teamRes[0].food;
        int beforeNodeAmount = node.amount;
        var ship = CreateUnit(h, UnitType.FishingShip, 0);
        ship.transform.position = node.transform.position + Vector3.forward;

        gather.AssignGather(ship, node);
        ship.state = UnitState.Gathering;
        for (int i = 0; i < 140; i++)
            gather.Tick(h.gm.units, 0.1f);

        ship.transform.position = Vector3.zero;
        for (int i = 0; i < 20; i++)
            gather.Tick(h.gm.units, 0.1f);

        Require(h.gm.teamRes[0].food > beforeFood, label + " fishing did not deposit food.");
        Require(node.amount < beforeNodeAmount, label + " fishing did not consume node food.");
    }

    static object InvokePrivate(object obj, string method, params object[] args)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var mi = obj.GetType().GetMethod(method, flags);
        if (mi == null) throw new MissingMethodException(obj.GetType().Name, method);
        return mi.Invoke(obj, args);
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
