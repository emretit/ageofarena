#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Editor/batchmode gameplay gate for QA Wave 2. It creates temporary scenes and
/// validates the core SP flows that are easy to regress while iterating on the RTS:
/// building placement/construction, production/rally, unit commands, and pause UI.
/// </summary>
public static class GameplayQaWave2Validator
{
    sealed class Harness
    {
        public GameObject root;
        public Transform unitsRoot;
        public Transform buildingsRoot;
        public Transform nodesRoot;
        public GameManager gm;
        public Camera camera;
        public readonly List<string> report = new();
    }

    [MenuItem("Tools/Age of Arena/Run Gameplay QA Wave 2")]
    public static void RunFromMenu()
    {
        RunOrThrow();
        Debug.Log("[GameplayQaWave2Validator] Gameplay QA Wave 2 passed.");
    }

    public static void RunFromCommandLine()
    {
        try
        {
            RunOrThrow();
            Debug.Log("[GameplayQaWave2Validator] Gameplay QA Wave 2 passed.");
            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogError("[GameplayQaWave2Validator] Gameplay QA Wave 2 failed:\n" + ex);
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
            throw;
        }
    }

    static void RunOrThrow()
    {
        var report = new List<string>();
        RunScenario("Builder flow", report, ValidateBuilderFlow);
        RunScenario("Production and rally flow", report, ValidateProductionRallyFlow);
        RunScenario("Unit command flow", report, ValidateUnitCommandFlow);
        RunScenario("Pause and subscreen flow", report, ValidatePauseAndSubscreenFlow);
        RunScenario("Runtime lifecycle smoke", report, ValidateRuntimeLifecycleSmoke);

        string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "gameplay-qa-wave2-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
        File.WriteAllLines(path, report);
        Debug.Log("[GameplayQaWave2Validator] Wrote gameplay QA report: " + path);
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
            Time.timeScale = 1f;
            if (h.root != null) UnityEngine.Object.DestroyImmediate(h.root);
        }
    }

    static Harness CreateHarness(string name)
    {
        var h = new Harness();
        h.root = new GameObject("GameplayQaWave2_" + name.Replace(" ", ""));
        h.unitsRoot = new GameObject("Units").transform;
        h.unitsRoot.SetParent(h.root.transform, false);
        h.buildingsRoot = new GameObject("Buildings").transform;
        h.buildingsRoot.SetParent(h.root.transform, false);
        h.nodesRoot = new GameObject("Resources").transform;
        h.nodesRoot.SetParent(h.root.transform, false);

        var camGo = new GameObject("GameplayQaCamera");
        camGo.transform.SetParent(h.root.transform, false);
        camGo.tag = "MainCamera";
        h.camera = camGo.AddComponent<Camera>();
        h.camera.orthographic = true;
        h.camera.orthographicSize = 18f;
        h.camera.transform.position = new Vector3(0f, 28f, -22f);
        h.camera.transform.rotation = Quaternion.Euler(55f, 0f, 0f);

        var eventGo = new GameObject("EventSystem");
        eventGo.transform.SetParent(h.root.transform, false);
        eventGo.AddComponent<EventSystem>();
        eventGo.AddComponent<StandaloneInputModule>();

        h.gm = h.root.AddComponent<GameManager>();
        InvokePrivate(h.gm, "Awake");
        h.gm.TeamCount = 4;
        h.gm.selection = h.root.AddComponent<SelectionSystem>();
        h.gm.command = h.root.AddComponent<CommandSystem>();
        h.gm.gather = h.root.AddComponent<GatherSystem>();
        h.gm.build = h.root.AddComponent<BuildSystem>();
        h.gm.placement = h.root.AddComponent<BuildingPlacement>();
        h.gm.trainingQueue = h.root.AddComponent<TrainingQueue>();
        h.gm.research = h.root.AddComponent<ResearchSystem>();
        h.gm.match = h.root.AddComponent<MatchSystem>();
        h.gm.scenarioEditor = h.root.AddComponent<ScenarioEditor>();
        h.gm.campaignScreen = h.root.AddComponent<CampaignScreen>();
        h.gm.checksum = h.root.AddComponent<ChecksumSystem>();

        h.gm.teamRes[0] = new ResourceManager();
        h.gm.teamTech[0] = new TechState();
        h.gm.teamRes[0].food = 1000;
        h.gm.teamRes[0].wood = 1000;
        h.gm.teamRes[0].gold = 1000;
        h.gm.teamRes[0].stone = 1000;
        h.gm.playerCiv = Civilization.None;

        SetPrivateField(h.gm.selection, "_cam", h.camera);
        SetPrivateField(h.gm.command, "_cam", h.camera);
        return h;
    }

    static void ValidateBuilderFlow(Harness h)
    {
        var tc = CreateBuilding(h, BuildingType.TownCenter, new Vector3(-10f, 0f, -2f), finished: true);
        h.gm.RecomputePop();
        int initialCap = h.gm.resources.popCap;
        Require(initialCap >= 5, "Town Center did not provide base pop cap.");

        var builder = CreateUnit(h, UnitType.Villager, new Vector3(0f, 0f, 4f));
        h.gm.selection.Selected.Add(builder);

        h.gm.placement.Begin(BuildingType.House);
        Require(h.gm.placement.Active, "House placement did not activate.");
        Require((bool)InvokePrivate(h.gm.placement, "IsValid", new Vector3(0f, 0f, 0f)), "Free House placement was invalid.");
        Require(!(bool)InvokePrivate(h.gm.placement, "IsValid", tc.transform.position), "Overlap placement was incorrectly valid.");

        int woodBefore = h.gm.resources.wood;
        var house = (BuildingEntity)InvokePrivate(h.gm.placement, "PlaceAt", new Vector3(0f, 0f, 0f), new List<UnitEntity> { builder });
        Require(house != null && house.underConstruction, "House construction site was not created.");
        Require(h.gm.resources.wood == woodBefore - BuildingDefs.Get(BuildingType.House).wood, "House did not deduct wood.");
        Require(builder.constructTarget == house, "Builder was not assigned to the construction site.");

        builder.transform.position = house.transform.position + Vector3.forward;
        h.gm.build.Tick(h.gm.units, house.buildTime + 1f);
        Require(!house.underConstruction, "House did not finish construction.");
        Require(h.gm.resources.popCap > initialCap, "Finished House did not raise pop cap.");

        h.gm.placement.Begin(BuildingType.Farm);
        var farm = (BuildingEntity)InvokePrivate(h.gm.placement, "PlaceAt", new Vector3(5f, 0f, 0f), new List<UnitEntity> { builder });
        Require(farm != null, "Farm construction site was not created.");
        builder.transform.position = farm.transform.position + Vector3.forward;
        h.gm.build.Tick(h.gm.units, farm.buildTime + 1f);
        Require(!farm.underConstruction, "Farm did not finish construction.");
        Require(farm.GetComponent<ResourceNode>() != null && h.gm.nodes.Contains(farm.GetComponent<ResourceNode>()),
            "Finished Farm did not register a food node.");

        h.gm.resources.wood = 0;
        h.gm.placement.Begin(BuildingType.House);
        Require(!(bool)InvokePrivate(h.gm.placement, "IsValid", new Vector3(9f, 0f, 0f)), "Unaffordable House placement was valid.");
        h.gm.resources.wood = 1000;
        h.gm.placement.Cancel();

        int wallBefore = CountBuildings(h, BuildingType.Wall);
        int wallWoodBefore = h.gm.resources.wood;
        h.gm.placement.Begin(BuildingType.Wall);
        InvokePrivate(h.gm.placement, "PlaceLine", new Vector3(12f, 0f, 0f), new Vector3(16f, 0f, 0f));
        int wallsPlaced = CountBuildings(h, BuildingType.Wall) - wallBefore;
        Require(wallsPlaced >= 2, "Wall drag did not place multiple segments.");
        Require(h.gm.resources.wood <= wallWoodBefore - BuildingDefs.Get(BuildingType.Wall).wood * wallsPlaced,
            "Wall drag did not deduct per segment.");
        Require(h.gm.placement.Active, "Wall placement mode did not stay active after drag placement.");
        h.gm.placement.Cancel();
    }

    static void ValidateProductionRallyFlow(Harness h)
    {
        var tc = CreateBuilding(h, BuildingType.TownCenter, new Vector3(0f, 0f, 0f), finished: true);
        var tree = ResourceFactory.Tree(h.nodesRoot, new Vector3(7f, 0f, 0f));
        h.gm.RegisterNode(tree);
        h.gm.resources.food = 1000;
        h.gm.resources.wood = 1000;
        h.gm.resources.SetPop(0, 50);

        var villagerDef = tc.GetTrainables()[0];
        int foodBefore = h.gm.resources.food;
        Require(h.gm.trainingQueue.Enqueue(tc, villagerDef), "Villager enqueue failed.");
        Require(h.gm.trainingQueue.GetQueueCount(tc) == 1, "Queue count did not increase.");
        h.gm.trainingQueue.Cancel(tc, 0);
        Require(h.gm.resources.food == foodBefore, "Queue cancel did not refund food.");

        h.gm.resources.SetPop(0, 1);
        Require(h.gm.trainingQueue.Enqueue(tc, villagerDef), "First pop-reserved enqueue failed.");
        Require(!h.gm.trainingQueue.Enqueue(tc, villagerDef), "Pop reservation allowed an over-cap enqueue.");
        h.gm.trainingQueue.Cancel(tc, 0);
        h.gm.resources.SetPop(0, 100);

        for (int i = 0; i < 5; i++)
            Require(h.gm.trainingQueue.Enqueue(tc, villagerDef), "Queue fill failed at slot " + i);
        Require(!h.gm.trainingQueue.Enqueue(tc, villagerDef), "Queue limit allowed a sixth item.");
        while (h.gm.trainingQueue.GetQueueCount(tc) > 0)
            h.gm.trainingQueue.Cancel(tc, 0);

        int unitsBefore = h.gm.units.Count;
        tc.hasRally = true;
        tc.rallyPoint = tree.transform.position;
        tc.rallyNode = tree;
        Require(h.gm.trainingQueue.Enqueue(tc, villagerDef), "Rally villager enqueue failed.");
        h.gm.trainingQueue.Tick(30f);
        Require(h.gm.units.Count == unitsBefore + 1, "Queued villager did not spawn.");
        var spawned = h.gm.units[h.gm.units.Count - 1];
        Require(spawned.teamId == 0 && spawned.type == UnitType.Villager, "Spawned unit had wrong team or type.");
        Require(spawned.gatherTarget == tree, "Resource rally did not assign the spawned villager to gather.");

        tree.amount = 0;
        tc.rallyNode = tree;
        tc.rallyPoint = new Vector3(4f, 0f, 4f);
        unitsBefore = h.gm.units.Count;
        Require(h.gm.trainingQueue.Enqueue(tc, villagerDef), "Fallback rally enqueue failed.");
        h.gm.trainingQueue.Tick(30f);
        spawned = h.gm.units[h.gm.units.Count - 1];
        Require(spawned.gatherTarget == null && spawned.targetPos == tc.rallyPoint,
            "Depleted resource rally did not fall back to ground rally.");
    }

    static void ValidateUnitCommandFlow(Harness h)
    {
        var u1 = CreateUnit(h, UnitType.Villager, new Vector3(-1f, 0f, 0f));
        var u2 = CreateUnit(h, UnitType.Villager, new Vector3(1f, 0f, 0f));
        var soldier = CreateUnit(h, UnitType.Militia, new Vector3(3f, 0f, 0f));

        var offsets = CommandSystem.FormationOffsets(3, CommandSystem.FormationType.Line);
        Require(offsets.Length == 3 && Mathf.Abs(offsets[0].x + 1.5f) < 0.001f, "Formation offsets regressed.");

        u1.attackMove = true;
        u1.patrolActive = true;
        u1.moveQueue.Enqueue(new Vector3(2f, 0f, 2f));
        u1.Stop();
        Require(u1.state == UnitState.Idle && !u1.attackMove && !u1.patrolActive && u1.moveQueue.Count == 0,
            "Stop did not clear attack-move, patrol, and queued waypoints.");

        h.gm.selection.Selected.Add(soldier);
        h.gm.command.BeginAttackMove();
        Require(h.gm.command.AttackMovePending, "BeginAttackMove did not expose pending state.");
        h.gm.command.BeginPatrol();
        Require(h.gm.command.PatrolPending, "BeginPatrol did not expose pending state.");

        h.gm.selection.ClearSelection();
        h.gm.selection.Selected.Add(u1);
        InvokePrivate(h.gm.selection, "AssignGroup", 1);
        h.gm.selection.ClearSelection();
        InvokePrivate(h.gm.selection, "SelectGroup", 1);
        Require(h.gm.selection.Selected.Count == 1 && h.gm.selection.Selected[0] == u1, "Control-group recall failed.");

        h.gm.selection.ClearSelection();
        h.gm.selection.SelectNextIdleWorker();
        Require(h.gm.selection.Selected.Count == 1 && h.gm.selection.Selected[0].type == UnitType.Villager,
            "Idle worker selection failed.");

        if (Screen.width > 0 && Screen.height > 0)
        {
            h.gm.selection.ClearSelection();
            InvokePrivate(h.gm.selection, "BoxSelect", Vector2.zero, new Vector2(Screen.width, Screen.height));
            Require(h.gm.selection.Selected.Count >= 2, "Full-screen box select did not pick visible units.");
        }

        try
        {
            Hotkeys.Reset(HotkeyAction.Patrol);
            Hotkeys.Reset(HotkeyAction.Formation);
            Hotkeys.Reset(HotkeyAction.TownBell);
            Hotkeys.Reset(HotkeyAction.Repair);
            Require(Hotkeys.Get(HotkeyAction.Patrol) == KeyCode.P, "Patrol hotkey default changed.");
            Require(Hotkeys.Get(HotkeyAction.Formation) == KeyCode.F, "Formation hotkey default changed.");
            Require(Hotkeys.Get(HotkeyAction.TownBell) == KeyCode.H, "TownBell hotkey default changed.");
            Require(!Hotkeys.IsBindableAction(HotkeyAction.Repair), "Contextual repair joined global hotkey conflicts.");
        }
        finally
        {
            Hotkeys.ResetAll();
        }
    }

    static void ValidatePauseAndSubscreenFlow(Harness h)
    {
        h.gm.hud = h.root.AddComponent<HUD>();
        h.gm.hud.Init(h.gm.resources);
        if (!h.gm.hud.HasCanvasRoot)
            InvokePrivate(h.gm.hud, "BuildCanvas");
        EnsureHudCanvasForQa(h.gm.hud, h.root.transform);
        Require(GetPrivateField(h.gm.hud, "_canvasRoot") is Transform canvasRoot && canvasRoot != null,
            "HUD canvas was not initialized.");

        Time.timeScale = 2f;
        InvokePrivate(h.gm.hud, "OpenPauseMenu", h.gm);
        Require(Mathf.Approximately(Time.timeScale, 0f), "Pause menu did not pause time.");
        var pause = (GameObject)GetPrivateField(h.gm.hud, "_pauseMenu");
        Require(pause != null && pause.activeSelf, "Pause menu did not open.");
        RequireNoButtonOverlap(pause, "Pause menu");
        RequireTextFonts(pause, "Pause menu");
        InvokePrivate(h.gm.hud, "ClosePauseMenu");
        Require(Mathf.Approximately(Time.timeScale, 2f), "Pause close did not restore previous time scale.");

        Time.timeScale = 1.5f;
        InvokePrivate(h.gm.hud, "OpenHotkeyPanel");
        var hotkeys = (GameObject)GetPrivateField(h.gm.hud, "_hotkeyPanel");
        Require(hotkeys != null && hotkeys.activeSelf && Mathf.Approximately(Time.timeScale, 0f), "Hotkey panel did not open paused.");
        RequireTextFonts(hotkeys, "Hotkey panel");
        InvokePrivate(h.gm.hud, "ClosePauseMenu");
        Require(Mathf.Approximately(Time.timeScale, 1.5f), "Hotkey close did not restore previous time scale.");

        InvokePrivate(h.gm.hud, "OpenTechTreePanel", h.gm);
        var techTree = (GameObject)GetPrivateField(h.gm.hud, "_techTreePanel");
        Require(techTree != null && techTree.activeSelf, "Tech tree panel did not open.");
        RequireTextFonts(techTree, "Tech tree panel");

        h.gm.hud.ToggleDiplomacyPanel();
        var dipl = (GameObject)GetPrivateField(h.gm.hud, "_diplPanel");
        Require(dipl != null, "Diplomacy panel did not open.");
        RequireTextFonts(dipl, "Diplomacy panel");
    }

    static void ValidateRuntimeLifecycleSmoke(Harness h)
    {
        var previousGm = h.gm;
        var go = new GameObject("RuntimeLifecycleSmokeGM");
        go.transform.SetParent(h.root.transform, false);
        var gm = go.AddComponent<GameManager>();
        gm.TeamCount = 4;
        gm.gather = go.AddComponent<GatherSystem>();
        gm.trainingQueue = go.AddComponent<TrainingQueue>();
        gm.playerCiv = Civilization.None;

        try
        {
            SetStaticField(typeof(GameManager), "_instance", gm);
            Require(gm.resources != null, "GameManager resource slot 0 did not self-initialize.");
            Require(gm.tech != null, "GameManager tech slot 0 did not self-initialize.");

            var tcGo = BuildingFactory.Create(BuildingType.TownCenter, h.buildingsRoot, Vector3.zero, TeamPalette.For(0));
            var tc = tcGo.GetComponent<BuildingEntity>();
            tc.teamId = 0;
            tc.underConstruction = false;
            tc.hp = tc.maxHp = BuildingDefs.Get(BuildingType.TownCenter).maxHp;
            InvokePrivate(tc, "Start");
            gm.RegisterBuilding(tc);

            var tree = ResourceFactory.Tree(h.nodesRoot, new Vector3(6f, 0f, 0f));
            gm.RegisterNode(tree);
            gm.RecomputePop();
            Require(gm.resources.popCap >= BuildingDefs.Get(BuildingType.TownCenter).popProvided,
                "Runtime pop recompute did not initialize/populate team resources.");

            gm.resources.food = 1000;
            gm.resources.wood = 1000;
            gm.resources.SetPop(0, 100);
            tc.hasRally = true;
            tc.rallyNode = tree;
            tc.rallyPoint = tree.transform.position;
            int before = gm.units.Count;
            Require(gm.trainingQueue.Enqueue(tc, tc.GetTrainables()[0]), "Runtime lifecycle TC training enqueue failed.");
            gm.trainingQueue.Tick(30f);
            Require(gm.units.Count == before + 1, "Runtime lifecycle training queue did not spawn a villager.");
            Require(gm.units[gm.units.Count - 1].gatherTarget == tree,
                "Runtime lifecycle resource rally did not assign gather.");
        }
        finally
        {
            SetStaticField(typeof(GameManager), "_instance", previousGm);
        }
    }

    static BuildingEntity CreateBuilding(Harness h, BuildingType type, Vector3 pos, bool finished)
    {
        var go = BuildingFactory.Create(type, h.buildingsRoot, pos, TeamPalette.For(0));
        var b = go.GetComponent<BuildingEntity>();
        var def = BuildingDefs.Get(type);
        b.teamId = 0;
        b.maxHp = def.maxHp;
        b.hp = def.maxHp;
        b.buildTime = def.buildTime;
        b.underConstruction = !finished;
        b.buildProgress = finished ? 1f : 0f;
        InvokePrivate(b, "Start");
        h.gm.RegisterBuilding(b);
        return b;
    }

    static UnitEntity CreateUnit(Harness h, UnitType type, Vector3 pos)
    {
        var u = UnitFactory.Spawn(type, h.unitsRoot, pos, 0);
        EnsureUnitAwakeForEditMode(u);
        InvokePrivate(u, "Start");
        h.gm.RegisterUnit(u);
        return u;
    }

    static void EnsureHudCanvasForQa(HUD hud, Transform parent)
    {
        var existing = GetPrivateField(hud, "_canvasRoot") as Transform;
        if (existing != null) return;

        var canvasGo = new GameObject("HUDCanvasQaFallback");
        canvasGo.transform.SetParent(parent, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        SetPrivateField(hud, "_canvasRoot", canvasGo.transform);
        SetPrivateField(hud, "_font", Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
    }

    static int CountBuildings(Harness h, BuildingType type)
    {
        int count = 0;
        for (int i = 0; i < h.gm.buildings.Count; i++)
            if (h.gm.buildings[i] != null && h.gm.buildings[i].type == type) count++;
        return count;
    }

    static void EnsureUnitAwakeForEditMode(UnitEntity unit)
    {
        if (unit.GetComponent<NavMeshAgent>() != null) return;
        InvokePrivate(unit, "Awake");
    }

    static void RequireTextFonts(GameObject root, string label)
    {
        var texts = root.GetComponentsInChildren<Text>(true);
        Require(texts.Length > 0, label + " did not create any Text components.");
        for (int i = 0; i < texts.Length; i++)
            Require(texts[i].font != null, label + " has a null-font Text: " + texts[i].name);
    }

    static void RequireNoButtonOverlap(GameObject root, string label)
    {
        var buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            var a = buttons[i].GetComponent<RectTransform>();
            if (a == null) continue;
            Rect ar = AnchoredRect(a);
            for (int j = i + 1; j < buttons.Length; j++)
            {
                var b = buttons[j].GetComponent<RectTransform>();
                if (b == null) continue;
                Rect br = AnchoredRect(b);
                Require(!ar.Overlaps(br), label + " buttons overlap: " + buttons[i].name + " / " + buttons[j].name);
            }
        }
    }

    static Rect AnchoredRect(RectTransform rt)
    {
        Vector2 size = rt.sizeDelta;
        Vector2 pivot = rt.pivot;
        Vector2 pos = rt.anchoredPosition;
        return new Rect(pos.x - size.x * pivot.x, pos.y - size.y * pivot.y, size.x, size.y);
    }

    static object InvokePrivate(object target, string method, params object[] args)
    {
        var mi = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Require(mi != null, "Missing method " + target.GetType().Name + "." + method);
        return mi.Invoke(target, args);
    }

    static void SetPrivateField(object target, string field, object value)
    {
        var fi = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Require(fi != null, "Missing field " + target.GetType().Name + "." + field);
        fi.SetValue(target, value);
    }

    static object GetPrivateField(object target, string field)
    {
        var fi = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Require(fi != null, "Missing field " + target.GetType().Name + "." + field);
        return fi.GetValue(target);
    }

    static void SetStaticField(Type type, string field, object value)
    {
        var fi = type.GetField(field, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Require(fi != null, "Missing static field " + type.Name + "." + field);
        fi.SetValue(null, value);
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
