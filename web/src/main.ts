/**
 * Bootstrap — GameBootstrap.cs equivalent. Builds the arena, spawns TCs,
 * villagers and resource nodes, then runs the fixed-step sim loop.
 * Pre-game screen lets the player pick civilization + map type before starting.
 */
import * as THREE from "three";
import { Config, TeamColors } from "./core/Config";
import { ResourceManager } from "./core/ResourceManager";
import { BuildingType, ResourceKind, UnitState, UnitType } from "./core/GameTypes";
import { CameraRig } from "./camera/CameraRig";
import { mulberry32 } from "./world/World";
import { buildTerrain as buildTerrainNew, type TerrainObjects } from "./render/TerrainRenderer";
import { buildLighting } from "./render/Lighting";
import { PostFx } from "./render/PostFx";
import {
  MapType, buildForest, getMapArchetype,
  spawnBaseResourcesForMap, spawnContestedMines,
  type TreeInstance,
} from "./world/MapGenerator";
import { navGrid } from "./sim/NavGrid";
import { PathQueue } from "./sim/PathQueue";
import { MovementSystem } from "./sim/MovementSystem";
import { initSimRng } from "./sim/SimRng";
import { Civilization } from "./core/CivilizationDefs";
import { setTeamCiv } from "./core/CivState";
import { Unit } from "./game/Unit";
import { Building, DEFS } from "./game/Building";
import { ResourceNode } from "./game/ResourceNode";
import { GatherSystem } from "./game/GatherSystem";
import { CombatSystem } from "./game/CombatSystem";
import { TrainingQueue } from "./game/TrainingQueue";
import { TradingSystem } from "./game/TradingSystem";
import { EnemyAI, Difficulty, Personality } from "./game/EnemyAI";
import { diplomacy, resetDiplomacy } from "./core/Diplomacy";
import { VictorySystem } from "./game/VictorySystem";
import { Selection } from "./game/Selection";
import { HUD } from "./ui/HUD";
import { Minimap } from "./ui/Minimap";
import { DamagePopup } from "./ui/DamagePopup";
import { PreGameScreen, type OpponentConfig } from "./ui/PreGameScreen";
import { FogOfWarSystem } from "./game/FogOfWarSystem";
import { AgeSystem } from "./game/AgeSystem";
import { ResearchSystem } from "./game/ResearchSystem";
import { MarketSystem } from "./game/MarketSystem";
import { ProjectileSystem } from "./game/ProjectileSystem";
import { GarrisonSystem } from "./game/GarrisonSystem";
import { ControlGroups } from "./game/ControlGroups";
import { ConversionSystem } from "./game/ConversionSystem";
import { BuildingPlacement } from "./game/BuildingPlacement";
import { RelicSystem } from "./game/RelicSystem";
import { VisualEffectSystem } from "./game/VisualEffectSystem";
import { play, setAmbientDuck, SoundId } from "./game/AudioManager";
import { PerfHud } from "./dev/PerfHud";
import { SettingsPanel } from "./ui/SettingsPanel";
import { buildSnapshot, saveToSlot } from "./game/SaveSystem";
import { GameMode, type GameModeType } from "./game/GameMode";

// ── Renderer (eager — shows while PreGameScreen is up) ───────────────────────
const app = document.getElementById("app")!;
const perfHud = new PerfHud(app);
const renderer = new THREE.WebGLRenderer({ antialias: true });
renderer.setSize(window.innerWidth, window.innerHeight);
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
app.appendChild(renderer.domElement);

const scene = new THREE.Scene();
scene.background = new THREE.Color(0x7ab0d8);

buildLighting(scene, renderer);
const terrain: TerrainObjects = buildTerrainNew(scene);
const rig = new CameraRig(app);
const postfx = new PostFx(renderer, scene, rig.camera);

window.addEventListener("resize", () => {
  renderer.setSize(window.innerWidth, window.innerHeight);
  postfx.onResize(window.innerWidth * Math.min(window.devicePixelRatio, 2), window.innerHeight * Math.min(window.devicePixelRatio, 2));
});

// ── FocusPause — pause sim when tab is hidden (FocusPause.cs port) ───────────
let _focusPaused = false;
document.addEventListener("visibilitychange", () => {
  _focusPaused = document.hidden;
});
// Dev helper: window.__setPaused(false) to force-resume in embedded preview
(window as unknown as Record<string, unknown>).__setPaused = (v: boolean) => { _focusPaused = v; };

// ── Pre-game screen ───────────────────────────────────────────────────────────
const preScreen = new PreGameScreen(app);
preScreen.onStart = (playerCiv: Civilization, opponents: OpponentConfig[], mapType: MapType) => {
  setTeamCiv(0, playerCiv);
  opponents.forEach((op, i) => setTeamCiv(i + 1, op.civ));
  initSimRng(1453);
  const trees = buildForest(scene, mapType, 1453);
  startGame(mapType, trees, opponents);
};

// Building footprint half-extents for NavGrid stamping
const BUILDING_HALF: Partial<Record<BuildingType, [number, number]>> = {
  [BuildingType.TownCenter]:   [2.5, 2.5],
  [BuildingType.House]:        [1.5, 1.5],
  [BuildingType.Barracks]:     [2.0, 2.0],
  [BuildingType.ArcheryRange]: [2.0, 2.0],
  [BuildingType.Stable]:       [2.0, 2.0],
  [BuildingType.Farm]:         [1.5, 1.5],
  [BuildingType.LumberCamp]:   [1.5, 1.5],
  [BuildingType.MiningCamp]:   [1.5, 1.5],
  [BuildingType.Mill]:         [1.5, 1.5],
  [BuildingType.Market]:       [2.0, 2.0],
  [BuildingType.Castle]:       [3.0, 3.0],
  [BuildingType.Wall]:         [0.5, 0.5],
  [BuildingType.Monastery]:    [2.0, 2.0],
  [BuildingType.University]:   [2.0, 2.0],
  [BuildingType.Blacksmith]:   [1.5, 1.5],
  [BuildingType.SiegeWorkshop]:[2.0, 2.0],
  [BuildingType.Dock]:         [2.0, 2.5],
  [BuildingType.WatchTower]:   [1.0, 1.0],
  [BuildingType.Wonder]:       [3.0, 3.0],
};

function stampBuilding(b: Building): void {
  const half = BUILDING_HALF[b.buildingType] ?? [1.5, 1.5];
  navGrid.stampWorldRect(b.pos.x, b.pos.z, half[0], half[1]);
}

// ── Game bootstrap ────────────────────────────────────────────────────────────
function startGame(mapType: MapType, trees: TreeInstance[], opponents: OpponentConfig[] = [{ civ: 0 as Civilization, difficulty: Difficulty.Normal, personality: Personality.Balanced }]): void {
  const arch = getMapArchetype(mapType);
  const rng  = mulberry32(42);

  // Reset diplomacy for new game
  resetDiplomacy();
  const teamCount = 1 + opponents.length;

  // Default: everyone is enemy of everyone (FFA)
  for (let a = 0; a < teamCount; a++) {
    for (let b = a + 1; b < teamCount; b++) {
      diplomacy.setStance(a, b, 'enemy');
    }
  }

  // ── NavGrid setup ────────────────────────────────────────────────────────
  navGrid.markWaterBeyondRadius(88); // ocean starts beyond land disc (Config.LandRadius ≈ 92)
  for (const t of trees) {
    navGrid.stampWorldCircle(t.x, t.z, t.scale * 0.7); // trunk radius
  }

  const basePositions = arch.basePositions;
  const [p1x, p1z] = basePositions[0];

  // Pan camera to player base
  rig.panTo(p1x, p1z);

  // ── Resource managers (one per team) ────────────────────────────────────
  const teamRes: ResourceManager[] = Array.from({ length: teamCount }, () => new ResourceManager());

  // ── Buildings ────────────────────────────────────────────────────────────
  const buildings: Building[] = [];

  const playerTC = new Building(scene, new THREE.Vector3(p1x, 0, p1z), 0, BuildingType.TownCenter);
  buildings.push(playerTC);
  stampBuilding(playerTC);

  const aiTCs: Building[] = [];
  for (let i = 0; i < opponents.length; i++) {
    const [bx, bz] = basePositions[(i + 1) % basePositions.length];
    const aiTC = new Building(scene, new THREE.Vector3(bx, 0, bz), i + 1, BuildingType.TownCenter);
    buildings.push(aiTC);
    stampBuilding(aiTC);
    aiTCs.push(aiTC);

    const aiBarracks = new Building(scene, new THREE.Vector3(bx - 8, 0, bz + 10), i + 1, BuildingType.Barracks);
    buildings.push(aiBarracks);
    stampBuilding(aiBarracks);
  }

  // ── Units ────────────────────────────────────────────────────────────────
  const units: Unit[] = [];

  for (let i = 0; i < 3; i++) {
    units.push(new Unit(scene, new THREE.Vector3(p1x - 2 + i * 2, 0, p1z + 6), 0, UnitType.Villager));
  }
  for (let ai = 0; ai < opponents.length; ai++) {
    const [bx, bz] = basePositions[(ai + 1) % basePositions.length];
    for (let i = 0; i < 3; i++) {
      units.push(new Unit(scene, new THREE.Vector3(bx - 2 + i * 2, 0, bz - 6), ai + 1, UnitType.Villager));
    }
  }

  // ── Resource nodes ───────────────────────────────────────────────────────
  const nodes: ResourceNode[] = [];

  nodes.push(...spawnBaseResourcesForMap(scene, p1x, p1z, arch, rng));
  for (let ai = 0; ai < opponents.length; ai++) {
    const [bx, bz] = basePositions[(ai + 1) % basePositions.length];
    nodes.push(...spawnBaseResourcesForMap(scene, bx, bz, arch, rng));
  }
  nodes.push(...spawnContestedMines(scene, arch, rng));

  // ── Systems ──────────────────────────────────────────────────────────────
  const gather      = new GatherSystem();
  const combat      = new CombatSystem();
  const training    = new TrainingQueue();
  const trading     = new TradingSystem();
  const ageSystem   = new AgeSystem();
  const ageSystems  = [ageSystem, ...opponents.map(() => new AgeSystem())];
  const research    = new ResearchSystem();
  const market      = new MarketSystem();
  const aiInstances = opponents.map((op, i) =>
    new EnemyAI(i + 1, teamRes[i + 1], ageSystems[i + 1], gather, training, research,
      op.difficulty, op.personality, (i * 7) % 30),
  );
  const victory     = new VictorySystem();
  const gameMode    = new GameMode('Conquest'); // default; PreGameScreen v3 can set mode
  const projectiles = new ProjectileSystem(scene);
  const garrison    = new GarrisonSystem();
  const placement   = new BuildingPlacement(scene, rig.camera, renderer.domElement);
  const pathQueue   = new PathQueue();
  const movement    = new MovementSystem();
  const relicSys    = new RelicSystem();
  const relics      = RelicSystem.spawnRelics(scene, 3);
  const vfx         = new VisualEffectSystem(app);
  const ctrlGroups  = new ControlGroups();
  const conversion  = new ConversionSystem();

  // ── HUD ──────────────────────────────────────────────────────────────────
  const hud = new HUD(app, teamRes[0]);
  const settings = new SettingsPanel(app, postfx);
  settings.onResume = () => { _focusPaused = false; };

  // ── Fog of War ───────────────────────────────────────────────────────────
  const fog = new FogOfWarSystem(scene);

  // ── Damage popups ─────────────────────────────────────────────────────────
  const damagePopup = new DamagePopup(app);
  combat.onHit = (pos, dmg) => { damagePopup.show(pos, dmg); play(SoundId.UnitAttack); };
  combat.onUnitKilled = (u) => { u.startDeathAnim(); play(SoundId.UnitDie); };
  combat.onBuildingDestroyed = () => { rig.shake(1.5, 0.4); play(SoundId.BuildingDie); };
  gather.onGatherTick = () => play(SoundId.GatherHit);

  // ── Audio hooks (cosmetic seam: sim → sound) ──────────────────────────────
  ageSystem.onAgeUp = () => play(SoundId.AgeUp);
  research.onComplete = (teamId) => { if (teamId === 0) play(SoundId.ResearchDone); };
  combat.onRangedFire = (from, to, splash) => projectiles.fire(from, to, splash);

  // ── Conversion callbacks ──────────────────────────────────────────────────
  conversion.onConverted = (u, newTeam) => {
    if (u.teamMat) {
      u.teamMat.color.setHex(TeamColors[newTeam % TeamColors.length]);
    }
    play(SoundId.Conversion);
  };

  // ── Minimap ───────────────────────────────────────────────────────────────
  const minimap = new Minimap(app);
  minimap.onNavigate = (x, z) => rig.panTo(x, z);

  // ── Selection ─────────────────────────────────────────────────────────────
  const selection = new Selection(
    renderer.domElement, rig.camera, scene,
    units, buildings, nodes, gather, combat, garrison, pathQueue,
  );

  function onBuild(type: BuildingType) {
    placement.begin(type);
  }

  placement.onPlace = (type, pos) => {
    const def = DEFS[type];
    const rm = teamRes[0];
    if (!rm.canAfford(0, def.costWood, def.costGold, def.costStone)) return false;
    rm.wood  = Math.max(0, rm.wood  - def.costWood);
    rm.stone = Math.max(0, rm.stone - def.costStone);
    rm.gold  = Math.max(0, rm.gold  - def.costGold);
    rm.onChange?.();
    const newBuilding = new Building(scene, pos, 0, type);
    buildings.push(newBuilding);
    stampBuilding(newBuilding); // register with NavGrid
    if (type === BuildingType.Farm) {
      const farmNode = new ResourceNode(scene, pos.clone(), ResourceKind.Food, 250);
      (farmNode as { destroyOnDeplete: boolean }).destroyOnDeplete = false;
      farmNode.decayPerSecond = 2;
      farmNode.ownerTeamId = 0;
      nodes.push(farmNode);
    }
    return true;
  };

  selection.onSelectUnit = (u) => {
    if (u) hud.showUnit(u, teamRes[0], onBuild);
    else if (!selection.selectedBuilding) hud.clearInfo();
  };
  selection.onSelectBuilding = (b) => {
    if (b) hud.showBuilding(b, training, teamRes[0], ageSystem, research, market, garrison);
    else hud.clearInfo();
  };

  // Auto-assign player villagers to different resources at start (gold, wood, food)
  const startKinds = [ResourceKind.Gold, ResourceKind.Wood, ResourceKind.Food];
  const playerVills = units.filter(u => u.teamId === 0);
  playerVills.forEach((u, i) => {
    const kind = startKinds[i % startKinds.length];
    const node = nodes.find(n => n.kind === kind && !n.depleted && n.hasRoom);
    if (node) gather.assignGather(u, node, buildings);
  });

  // ── Dev/debug handle ──────────────────────────────────────────────────────
  (window as unknown as Record<string, unknown>).__game = {
    units, buildings, nodes, selection, rig, teamRes, diplomacy, victory,
    gather, combat, training, trading, pathQueue, ctrlGroups, conversion,
    aiInstances,
  };

  // ── Hotkeys (HKEY port) ───────────────────────────────────────────────────
  window.addEventListener("keydown", e => {
    if (e.target instanceof HTMLInputElement) return;
    const key = e.key.toLowerCase();

    // Attack-move (A key)
    if (key === "a" && selection.selected.length > 0) {
      selection.attackMovePending = true;
      selection.patrolPending = false;
    }

    // Patrol (Z key)
    if (key === "z" && selection.selected.length > 0) {
      selection.patrolPending = true;
      selection.attackMovePending = false;
    }

    // Stop
    if (key === "s") {
      selection.attackMovePending = false;
      for (const u of selection.selected) {
        u.attackTarget = null;
        u.attackTargetBuilding = null;
        u.stopMoving();
      }
    }

    if (key === "g" && selection.selectedBuilding) {
      const b = selection.selectedBuilding;
      if (b.teamId === 0 && garrison.canGarrison(b)) {
        for (const u of selection.selected) {
          if (u.teamId === 0) garrison.orderGarrison(u, b);
        }
      }
    }

    if (key === "u" && selection.selectedBuilding) {
      garrison.ungarrisonAll(selection.selectedBuilding);
    }

    if (key === ".") {
      const idle = units.find(u => u.teamId === 0 && u.alive && u.gathers && !u.gatherTarget && !u.attackTarget);
      if (idle) {
        for (const u of selection.selected) u.selected = false;
        selection.selected.length = 0;
        selection.selectedBuilding = null;
        idle.selected = true;
        selection.selected.push(idle);
        selection.onSelectUnit?.(idle);
      }
    }

    if (key === "escape") {
      if (settings.isVisible) {
        settings.hide();
        _focusPaused = false;
      } else {
        selection.attackMovePending = false;
        selection.patrolPending = false;
        placement.cancel();
        settings.toggle();
        _focusPaused = true;
      }
    }

    // Quick save (F5) / quick load hint
    if (e.key === "F5") {
      e.preventDefault();
      const snap = buildSnapshot(gameElapsed, teamRes, units, buildings);
      saveToSlot(1, snap);
      // Save notification (HUD does not have showNotification yet — just console log)
      console.info("[Save] slot 1 kaydedildi");
    }

    // Control groups: Ctrl+1..9 assign, 1..9 recall (double-tap to focus)
    const digit = parseInt(e.key);
    if (digit >= 1 && digit <= 9) {
      if (e.ctrlKey || e.metaKey) {
        e.preventDefault();
        ctrlGroups.assign(digit, selection.selected);
      } else {
        const doubleTap = ctrlGroups.isDoubleTap(digit);
        for (const u of selection.selected) u.selected = false;
        selection.selected.length = 0;
        selection.selectedBuilding = null;
        ctrlGroups.recall(digit, selection.selected);
        for (const u of selection.selected) u.selected = true;
        if (selection.selected.length > 0) {
          selection.onSelectUnit?.(selection.selected[0]);
          if (doubleTap) ctrlGroups.focusCentroid(digit, rig);
        }
      }
    }
  });

  // ── Game state ────────────────────────────────────────────────────────────
  let gameOver    = false;
  let gameElapsed = 0; // seconds since game start (for save snapshot)

  // ── Fixed-step sim + render loop ──────────────────────────────────────────
  let last = performance.now();
  let acc  = 0;

  function frame(now: number) {
    const dt = Math.min((now - last) / 1000, 0.25);
    last = now;

    if (!gameOver && !_focusPaused) {
      acc += dt;
      while (!gameOver && acc >= Config.FixedStep) {
        const step = Config.FixedStep;
        gameElapsed += step;
        const simStart = performance.now();

        // Pathfinding + movement (before systems that read positions)
        pathQueue.tick(navGrid, step);
        movement.tick(units, navGrid, step);

        for (const u of units) u.tick(step);

        gather.tick(units, buildings, teamRes, scene, step);
        gather.tickFarms(nodes, teamRes, step);
        combat.tick(units, buildings, step);
        combat.tickBuildings(buildings, units, step, b => garrison.garrisonCount(b));
        training.tick(buildings, units, scene, research, step);
        trading.tick(units, buildings, teamRes, step);
        garrison.tick(units, buildings, step);
        relicSys.tick(units, relics, buildings, teamRes, step);
        research.tick(units, buildings, teamRes, step);
        market.tick(step);
        // Shift-queue: when unit arrives and has pending goals, issue next goal
        for (const u of units) {
          if (!u.alive || u.isGarrisoned || u.pendingGoals.length === 0) continue;
          if (u.waypoints.length > 0 || u.attackTarget || u.attackTargetBuilding) continue;
          const [gx, gz] = u.pendingGoals.shift()!;
          pathQueue.requestForced(u, gx, gz, 'land', u.teamId, 1);
        }

        // Patrol: flip destination when waypoints exhausted + no target
        for (const u of units) {
          if (u.state !== UnitState.Patrol) continue;
          if (u.attackTarget || u.attackTargetBuilding) continue;
          if (u.waypoints.length > 0) continue;
          u.patrolGoingToB = !u.patrolGoingToB;
          const destX = u.patrolGoingToB ? u.patrolBX : u.patrolAX;
          const destZ = u.patrolGoingToB ? u.patrolBZ : u.patrolAZ;
          pathQueue.requestForced(u, destX, destZ, 'land', u.teamId, 1);
        }

        for (const ai of aiInstances) ai.tick(units, buildings, nodes, scene, step, pathQueue);
        conversion.tick(units, step);
        fog.tick(units, buildings, step);
        ageSystem.tick(teamRes[0], step);
        // AI ageSystems ticked inside each EnemyAI.tick()
        minimap.tick(units, buildings, nodes, fog, step);

        gather.prune();
        pathQueue.prune();
        trading.prune(units);
        for (let i = units.length - 1; i >= 0; i--) {
          const u = units[i];
          if (!u.alive && !u.isDying) {
            scene.remove(u.root);
            units.splice(i, 1);
          }
        }

        for (const rm of teamRes) rm.pop = 0;
        for (const u of units) teamRes[u.teamId].pop++;
        for (const rm of teamRes) rm.popCap = 5;
        for (const b of buildings) {
          if (b.alive && b.def.popProvided > 0) teamRes[b.teamId].popCap += b.def.popProvided;
        }
        teamRes[0].onChange?.();

        if (!gameOver) {
          const allTeams = Array.from({ length: teamCount }, (_, i) => i);

          // Conquest: VictorySystem handles TC elimination
          const victoryWinner = victory.tick(buildings, allTeams);
          // Other modes: GameMode
          const modeResult = gameMode.tick(buildings, units, allTeams, step);
          const winner = victoryWinner >= 0 ? victoryWinner : modeResult.winner;

          if (winner >= 0) {
            gameOver = true;
            if (winner === 0) {
              hud.showVictory(0);
              play(SoundId.Victory);
            } else {
              hud.showVictory(winner);
              play(SoundId.Defeat);
            }
          }
        }

        perfHud.setSimMs(performance.now() - simStart);
        acc -= step;
      }
    }

    for (const u of units) u.tick(0, rig.camera);
    for (const b of buildings) b.refreshHpBarCamera(rig.camera);

    if (selection.selected.length > 1) hud.showMultiUnit(selection.selected);
    else if (selection.selected.length === 1) hud.showUnit(selection.selected[0], teamRes[0], onBuild);
    else if (selection.selectedBuilding) hud.showBuilding(selection.selectedBuilding, training, teamRes[0], ageSystem, research, market, garrison);

    // Combat intensity for audio ducking (fraction of player units actively attacking)
    const playerUnits = units.filter(u => u.teamId === 0 && u.alive);
    const attacking   = playerUnits.filter(u => u.attackTarget || u.attackTargetBuilding).length;
    setAmbientDuck(playerUnits.length > 0 ? attacking / playerUnits.length : 0);

    damagePopup.tick(rig.camera, dt);
    projectiles.tick(dt);
    vfx.tick(buildings, rig.camera, dt);

    terrain.tick(dt);
    rig.update(dt);
    postfx.render();

    perfHud.tickFrame(dt);
    perfHud.setDrawCalls(renderer.info.render.calls);
    perfHud.setPathQueue(pathQueue.pendingCount);
    perfHud.flush();
    requestAnimationFrame(frame);
  }
  requestAnimationFrame(frame);
}
