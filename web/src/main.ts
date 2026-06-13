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
import { Selection, FORMATION_NAMES } from "./game/Selection";
import { orderAttackMove } from "./game/Orders";
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
import { CommandBus } from "./sim/CommandBus";
import { CommandExecutor } from "./sim/CommandExecutor";
import { resetIds } from "./sim/EntityIds";
import { type ReplaySetup, type AoaRep, REPLAY_MAGIC, REPLAY_VERSION, saveRepToSlot } from "./replay/ReplayFile";
import { LockstepClient, SP_OPTIONS } from "./net/LockstepClient";
import { LoopbackTransport } from "./net/LoopbackTransport";
import type { Transport } from "./net/Transport";
import { DesyncHandler } from "./net/DesyncHandler";
import { RoomScreen, type MPGameConfig } from "./ui/RoomScreen";
import { NetStatus } from "./ui/NetStatus";
import { initTelemetry } from "./net/Telemetry";
import { assetLoader } from "./render/AssetLoader";
import { LoadingScreen } from "./ui/LoadingScreen";

/** Multiplayer wiring passed to startGame; absent for single-player. */
interface NetConfig {
  transport: Transport;
  myTeam: number;
  seed: number;
}

// Optional Sentry (no-op unless VITE_SENTRY_DSN is set)
initTelemetry();

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

// ── Asset preload — bake CC0 models before any Unit is constructed ───────────
// A blocking overlay covers the pre-game screen until every manifest model is baked,
// so Unit() always finds its baked template (else it silently uses procedural geometry).
const loadingScreen = new LoadingScreen(app);
void assetLoader
  .preload((loaded, total) => loadingScreen.setProgress(loaded, total))
  .then(() => loadingScreen.done())
  .catch(() => loadingScreen.done()); // graceful: missing models fall back to procedural

// ── Pre-game screen ───────────────────────────────────────────────────────────
const preScreen = new PreGameScreen(app);
preScreen.onStart = (playerCiv: Civilization, opponents: OpponentConfig[], mapType: MapType) => {
  if (!assetLoader.isLoaded) return; // guard: never spawn units before models are baked
  setTeamCiv(0, playerCiv);
  opponents.forEach((op, i) => setTeamCiv(i + 1, op.civ));
  const simSeed = 1453;
  initSimRng(simSeed);
  const trees = buildForest(scene, mapType, simSeed);
  const replaySetup: ReplaySetup = {
    mapType, simSeed, playerCiv,
    opponents: opponents.map(op => ({ civ: op.civ, difficulty: op.difficulty, personality: op.personality })),
  };
  startGame(mapType, trees, opponents, replaySetup);
};

// ── Online multiplayer (RoomScreen → WsTransport → game_start) ──────────────────
const roomScreen = new RoomScreen(app);
preScreen.onOnline = () => roomScreen.show();
roomScreen.onGameStart = (cfg: MPGameConfig) => {
  if (!assetLoader.isLoaded) return; // guard: never spawn units before models are baked
  // MP MVP: every team starts on the default civ (lobby civ-select is a follow-up).
  const otherCount = cfg.players.filter(p => p.team !== 0).length;
  const opponents: OpponentConfig[] = Array.from({ length: otherCount }, () => ({
    civ: 0 as Civilization, difficulty: Difficulty.Normal, personality: Personality.Balanced,
  }));
  for (const p of cfg.players) setTeamCiv(p.team, 0 as Civilization);
  initSimRng(cfg.seed);                       // identical RNG seed across all clients
  const trees = buildForest(scene, cfg.mapType, cfg.seed);
  startGame(cfg.mapType, trees, opponents, undefined, {
    transport: cfg.transport, myTeam: cfg.myTeam, seed: cfg.seed,
  });
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
  [BuildingType.Gate]:         [2.0, 0.5],
};

function stampBuilding(b: Building): void {
  const half = BUILDING_HALF[b.buildingType] ?? [1.5, 1.5];
  // Gates are passable to their owner (team-masked cells); other buildings are solid.
  if (b.buildingType === BuildingType.Gate) {
    navGrid.stampGateWorld(b.pos.x, b.pos.z, half[0], half[1], b.teamId);
    return;
  }
  navGrid.stampWorldRect(b.pos.x, b.pos.z, half[0], half[1]);
}

function unstampBuilding(b: Building): void {
  const half = BUILDING_HALF[b.buildingType] ?? [1.5, 1.5];
  if (b.buildingType === BuildingType.Gate) {
    navGrid.unstampGateWorld(b.pos.x, b.pos.z, half[0], half[1], b.teamId);
    return;
  }
  navGrid.unstampWorldRect(b.pos.x, b.pos.z, half[0], half[1]);
}

// ── Game bootstrap ────────────────────────────────────────────────────────────
function startGame(mapType: MapType, trees: TreeInstance[], opponents: OpponentConfig[] = [{ civ: 0 as Civilization, difficulty: Difficulty.Normal, personality: Personality.Balanced }], _replaySetup?: ReplaySetup, net?: NetConfig): void {
  const arch = getMapArchetype(mapType);
  const rng  = mulberry32(42);
  const isMP = !!net;
  const PLAYER_TEAM = net?.myTeam ?? 0; // local player's team (MP: assigned by server)

  // Reset entity IDs + NavGrid for new game (avoid stamp carry-over across rematches)
  resetIds();
  navGrid.reset();

  // Command bus — all player/AI actions flow through this
  const commandBus = new CommandBus();

  // Lockstep client: MP uses the WS transport (turn=4, delay=2); SP loops back at zero delay.
  const lockstepClient = isMP
    ? new LockstepClient(net!.transport, { ticksPerTurn: 4, inputDelay: 2, myTeamId: PLAYER_TEAM })
    : new LockstepClient(new LoopbackTransport(['player-0']), { ...SP_OPTIONS, myTeamId: 0 });

  // Desync detection (MP only — sends periodic checksums, banners on mismatch)
  const desyncHandler = isMP ? new DesyncHandler(commandBus, net!.transport) : null;

  // Net status overlay
  const netStatus = new NetStatus(app);

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

  // Pan camera to the LOCAL player's base (spawns below are deterministic & team-indexed).
  const [camX, camZ] = basePositions[PLAYER_TEAM % basePositions.length];
  rig.panTo(camX, camZ);

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
  const localAge    = ageSystems[PLAYER_TEAM] ?? ageSystem; // local player's age (UI + SP tick)
  const research    = new ResearchSystem();
  const market      = new MarketSystem();
  // MP: all teams are human (commands arrive over the wire) — no local AI.
  const aiInstances = isMP ? [] : opponents.map((op, i) =>
    new EnemyAI(i + 1, teamRes[i + 1], ageSystems[i + 1], gather, training, research,
      op.difficulty, op.personality, (i * 7) % 30, commandBus),
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
  const hud = new HUD(app, teamRes[PLAYER_TEAM]);
  hud.localTeam = PLAYER_TEAM;
  hud.setBus(lockstepClient);
  const settings = new SettingsPanel(app, postfx);
  settings.onResume = () => { _focusPaused = false; };

  // ── Fog of War ───────────────────────────────────────────────────────────
  const fog = new FogOfWarSystem(scene);

  // ── Damage popups ─────────────────────────────────────────────────────────
  const damagePopup = new DamagePopup(app);
  combat.onHit = (pos, dmg) => { damagePopup.show(pos, dmg); play(SoundId.UnitAttack); };
  combat.onUnitKilled = (u) => { u.startDeathAnim(); play(SoundId.UnitDie); };
  combat.onBuildingDestroyed = (b) => { unstampBuilding(b); rig.shake(1.5, 0.4); play(SoundId.BuildingDie); };
  gather.onGatherTick = () => play(SoundId.GatherHit);

  // ── Audio hooks (cosmetic seam: sim → sound) ──────────────────────────────
  localAge.onAgeUp = () => play(SoundId.AgeUp);
  research.onComplete = (teamId) => { if (teamId === PLAYER_TEAM) play(SoundId.ResearchDone); };
  combat.onRangedFire = (from, to, splash) => projectiles.fire(from, to, splash);

  // ── Conversion callbacks ──────────────────────────────────────────────────
  conversion.onConverted = (u, newTeam) => {
    u.setTeamColor(TeamColors[newTeam % TeamColors.length]); // re-colours model tint + ground disc
    play(SoundId.Conversion);
  };

  // ── Minimap ───────────────────────────────────────────────────────────────
  const minimap = new Minimap(app);
  minimap.localTeam = PLAYER_TEAM;
  minimap.onNavigate = (x, z) => rig.panTo(x, z);

  // ── Selection ─────────────────────────────────────────────────────────────
  const selection = new Selection(
    renderer.domElement, rig.camera, scene,
    units, buildings, nodes, gather, combat, garrison, pathQueue, lockstepClient,
  );
  selection.localTeam = PLAYER_TEAM;

  function onBuild(type: BuildingType) {
    placement.begin(type);
  }

  placement.onPlace = (type, pos) => {
    const def = DEFS[type];
    const rm = teamRes[PLAYER_TEAM];
    if (!rm.canAfford(0, def.costWood, def.costGold, def.costStone)) return false;
    rm.wood  = Math.max(0, rm.wood  - def.costWood);
    rm.stone = Math.max(0, rm.stone - def.costStone);
    rm.gold  = Math.max(0, rm.gold  - def.costGold);
    rm.onChange?.();
    const newBuilding = new Building(scene, pos, PLAYER_TEAM, type);
    buildings.push(newBuilding);
    stampBuilding(newBuilding); // register with NavGrid
    if (type === BuildingType.Farm) {
      const farmNode = new ResourceNode(scene, pos.clone(), ResourceKind.Food, 250);
      (farmNode as { destroyOnDeplete: boolean }).destroyOnDeplete = false;
      farmNode.decayPerSecond = 2;
      farmNode.ownerTeamId = PLAYER_TEAM;
      nodes.push(farmNode);
    }
    return true;
  };

  selection.onSelectUnit = (u) => {
    if (u) hud.showUnit(u, teamRes[PLAYER_TEAM], onBuild);
    else if (!selection.selectedBuilding) hud.clearInfo();
    hud.setFormation(selection.selected.length > 0 ? FORMATION_NAMES[selection.formationType] : null);
  };
  selection.onSelectBuilding = (b) => {
    if (b) hud.showBuilding(b, training, teamRes[PLAYER_TEAM], localAge, research, market, garrison);
    else hud.clearInfo();
  };
  selection.onFormationChange = (type) => {
    if (selection.selected.length > 0) hud.setFormation(FORMATION_NAMES[type]);
  };

  // Auto-assign player villagers to different resources at start (gold, wood, food)
  const startKinds = [ResourceKind.Gold, ResourceKind.Wood, ResourceKind.Food];
  const playerVills = units.filter(u => u.teamId === PLAYER_TEAM);
  playerVills.forEach((u, i) => {
    const kind = startKinds[i % startKinds.length];
    const node = nodes.find(n => n.kind === kind && !n.depleted && n.hasRoom);
    if (node) gather.assignGather(u, node, buildings);
  });

  // ── CommandExecutor — single dispatch for all Command objects ─────────────
  const commandExecutor = new CommandExecutor(
    units, buildings, nodes, gather, combat, training, research, market,
    garrison, pathQueue, teamRes,
  );

  // ── Dev/debug handle ──────────────────────────────────────────────────────
  (window as unknown as Record<string, unknown>).__game = {
    units, buildings, nodes, selection, rig, teamRes, diplomacy, victory,
    gather, combat, training, trading, pathQueue, ctrlGroups, conversion,
    aiInstances, commandBus,
  };

  // ── Stress-test spawn (dev) ───────────────────────────────────────────────
  function spawnStressArmy(count: number, teamId: number, baseX: number, targetX: number): void {
    const cols = Math.ceil(Math.sqrt(count));
    const added: Unit[] = [];
    for (let i = 0; i < count; i++) {
      const col = i % cols, row = Math.floor(i / cols);
      const x = baseX + (col - cols / 2) * 1.3;
      const z = (row - cols / 2) * 1.3;
      const type = i % 3 === 0 ? UnitType.Archer : UnitType.Militia;
      const u = new Unit(scene, new THREE.Vector3(x, 0, z), teamId, type);
      units.push(u);
      added.push(u);
    }
    orderAttackMove(added, targetX, 0, pathQueue);
  }

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

    // Cycle formation (F key)
    if (key === "f" && selection.selected.length > 0) {
      selection.cycleFormation();
    }

    // Stress test (P = 250v250, Shift+P = 500v500): dev-only mass battle.
    // Disabled in MP — direct spawns bypass the command bus and would desync.
    if (key === "p" && !isMP) {
      const perSide = e.shiftKey ? 500 : 250;
      spawnStressArmy(perSide, 0, -55, 55);   // team 0 on the left, attacks right
      spawnStressArmy(perSide, 1,  55, -55);  // team 1 on the right, attacks left
      if (!perfHud.visible) perfHud.toggle();
      console.log(`[Stress] spawned ${perSide}v${perSide} (${units.length} units total)`);
    }

    if (key === "g" && selection.selectedBuilding) {
      const b = selection.selectedBuilding;
      if (b.teamId === PLAYER_TEAM && garrison.canGarrison(b)) {
        for (const u of selection.selected) {
          if (u.teamId === PLAYER_TEAM) garrison.orderGarrison(u, b);
        }
      }
    }

    if (key === "u" && selection.selectedBuilding) {
      garrison.ungarrisonAll(selection.selectedBuilding);
    }

    if (key === ".") {
      const idle = units.find(u => u.teamId === PLAYER_TEAM && u.alive && u.gathers && !u.gatherTarget && !u.attackTarget);
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

    // Quick save (F5): snapshot + replay command log
    if (e.key === "F5") {
      e.preventDefault();
      const snap = buildSnapshot(gameElapsed, teamRes, units, buildings);
      saveToSlot(1, snap);
      if (_replaySetup) {
        const rep: AoaRep = {
          magic: REPLAY_MAGIC,
          version: REPLAY_VERSION,
          setup: _replaySetup,
          commands: [...commandBus.getLog()],
          checksums: [],
          durationTicks: commandBus.currentTick,
        };
        saveRepToSlot(1, rep);
      }
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

        // Lockstep tick: collect confirmed turn commands, gate on server echo
        const { stalling, commands: turnCmds } = lockstepClient.tick();
        netStatus.setStalling(stalling);
        if (stalling) { acc -= step; break; } // pause sim until server confirms

        // Advance command bus tick + execute confirmed player commands + AI commands
        commandBus.advanceTick();
        for (const cmd of turnCmds) commandBus.issue(cmd);
        commandExecutor.execute(commandBus.drain());

        // MP: emit periodic checksum for desync detection
        desyncHandler?.tick(commandBus.currentTick, app);

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
        if (isMP) {
          // No local AI in MP — tick every team's age system so opponents can advance.
          for (let i = 0; i < ageSystems.length; i++) ageSystems[i].tick(teamRes[i], step);
        } else {
          localAge.tick(teamRes[PLAYER_TEAM], step);
          // AI ageSystems ticked inside each EnemyAI.tick()
        }
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
        teamRes[PLAYER_TEAM].onChange?.();

        if (!gameOver) {
          const allTeams = Array.from({ length: teamCount }, (_, i) => i);

          // Conquest: VictorySystem handles TC elimination
          const victoryWinner = victory.tick(buildings, allTeams);
          // Other modes: GameMode
          const modeResult = gameMode.tick(buildings, units, allTeams, step);
          const winner = victoryWinner >= 0 ? victoryWinner : modeResult.winner;

          if (winner >= 0) {
            gameOver = true;
            if (winner === PLAYER_TEAM) {
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
    else if (selection.selected.length === 1) hud.showUnit(selection.selected[0], teamRes[PLAYER_TEAM], onBuild);
    else if (selection.selectedBuilding) hud.showBuilding(selection.selectedBuilding, training, teamRes[PLAYER_TEAM], localAge, research, market, garrison);
    hud.setFormation(selection.selected.length > 0 ? FORMATION_NAMES[selection.formationType] : null);

    // Combat intensity for audio ducking (fraction of player units actively attacking)
    const playerUnits = units.filter(u => u.teamId === PLAYER_TEAM && u.alive);
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
    perfHud.setUnitCount(units.length);
    perfHud.flush();
    requestAnimationFrame(frame);
  }
  requestAnimationFrame(frame);
}
