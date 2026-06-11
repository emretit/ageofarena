/**
 * Bootstrap — GameBootstrap.cs equivalent. Builds the arena, spawns TCs,
 * villagers and resource nodes, then runs the fixed-step sim loop.
 * Pre-game screen lets the player pick civilization + map type before starting.
 */
import * as THREE from "three";
import { Config } from "./core/Config";
import { ResourceManager } from "./core/ResourceManager";
import { BuildingType, ResourceKind, UnitType } from "./core/GameTypes";
import { CameraRig } from "./camera/CameraRig";
import { buildTerrain, mulberry32 } from "./world/World";
import {
  MapType, buildForest, getMapArchetype,
  spawnBaseResourcesForMap, spawnContestedMines,
} from "./world/MapGenerator";
import { Civilization } from "./core/CivilizationDefs";
import { setTeamCiv } from "./core/CivState";
import { Unit } from "./game/Unit";
import { Building, DEFS } from "./game/Building";
import { ResourceNode } from "./game/ResourceNode";
import { GatherSystem } from "./game/GatherSystem";
import { CombatSystem } from "./game/CombatSystem";
import { TrainingQueue } from "./game/TrainingQueue";
import { TradingSystem } from "./game/TradingSystem";
import { EnemyAI } from "./game/EnemyAI";
import { Selection } from "./game/Selection";
import { HUD } from "./ui/HUD";
import { Minimap } from "./ui/Minimap";
import { DamagePopup } from "./ui/DamagePopup";
import { PreGameScreen } from "./ui/PreGameScreen";
import { FogOfWarSystem } from "./game/FogOfWarSystem";
import { AgeSystem } from "./game/AgeSystem";
import { ResearchSystem } from "./game/ResearchSystem";
import { MarketSystem } from "./game/MarketSystem";
import { ProjectileSystem } from "./game/ProjectileSystem";
import { GarrisonSystem } from "./game/GarrisonSystem";
import { BuildingPlacement } from "./game/BuildingPlacement";
import { RelicSystem } from "./game/RelicSystem";
import { VisualEffectSystem } from "./game/VisualEffectSystem";
import { play, SoundId } from "./game/AudioManager";

// ── Renderer (eager — shows while PreGameScreen is up) ───────────────────────
const app = document.getElementById("app")!;
const renderer = new THREE.WebGLRenderer({ antialias: true });
renderer.setSize(window.innerWidth, window.innerHeight);
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
app.appendChild(renderer.domElement);

const scene = new THREE.Scene();
scene.background = new THREE.Color(0x87b9e8);

buildTerrain(scene); // lights + ocean + land disc (no forest yet)
const rig = new CameraRig(app);

window.addEventListener("resize", () => {
  renderer.setSize(window.innerWidth, window.innerHeight);
});

// ── FocusPause — pause sim when tab is hidden (FocusPause.cs port) ───────────
let _focusPaused = false;
document.addEventListener("visibilitychange", () => {
  _focusPaused = document.hidden;
});

// ── Pre-game screen ───────────────────────────────────────────────────────────
const preScreen = new PreGameScreen(app);
preScreen.onStart = (playerCiv: Civilization, enemyCiv: Civilization, mapType: MapType) => {
  setTeamCiv(0, playerCiv);
  setTeamCiv(1, enemyCiv);
  buildForest(scene, mapType, 1453);
  startGame(mapType);
};

// ── Game bootstrap ────────────────────────────────────────────────────────────
function startGame(mapType: MapType): void {
  const arch = getMapArchetype(mapType);
  const rng  = mulberry32(42);

  // 2-player game: player at basePositions[0], enemy at basePositions[1]
  const [p1x, p1z] = arch.basePositions[0];
  const [p2x, p2z] = arch.basePositions[1];

  // Pan camera to player base
  rig.panTo(p1x, p1z);

  // ── Resource managers (one per team) ────────────────────────────────────
  const teamRes: ResourceManager[] = [new ResourceManager(), new ResourceManager()];

  // ── Buildings ────────────────────────────────────────────────────────────
  const buildings: Building[] = [];

  const playerTC = new Building(scene, new THREE.Vector3(p1x, 0, p1z), 0, BuildingType.TownCenter);
  buildings.push(playerTC);

  const enemyTC = new Building(scene, new THREE.Vector3(p2x, 0, p2z), 1, BuildingType.TownCenter);
  buildings.push(enemyTC);

  const enemyBarracks = new Building(scene, new THREE.Vector3(p2x - 8, 0, p2z + 10), 1, BuildingType.Barracks);
  buildings.push(enemyBarracks);

  // ── Units ────────────────────────────────────────────────────────────────
  const units: Unit[] = [];

  for (let i = 0; i < 3; i++) {
    units.push(new Unit(scene, new THREE.Vector3(p1x - 2 + i * 2, 0, p1z + 6), 0, UnitType.Villager));
  }
  for (let i = 0; i < 3; i++) {
    units.push(new Unit(scene, new THREE.Vector3(p2x - 2 + i * 2, 0, p2z - 6), 1, UnitType.Villager));
  }

  // ── Resource nodes ───────────────────────────────────────────────────────
  const nodes: ResourceNode[] = [];

  nodes.push(...spawnBaseResourcesForMap(scene, p1x, p1z, arch, rng));
  nodes.push(...spawnBaseResourcesForMap(scene, p2x, p2z, arch, rng));
  nodes.push(...spawnContestedMines(scene, arch, rng));

  // ── Systems ──────────────────────────────────────────────────────────────
  const gather      = new GatherSystem();
  const combat      = new CombatSystem();
  const training    = new TrainingQueue();
  const trading     = new TradingSystem();
  const ageSystem   = new AgeSystem();
  const ageSystemEnemy = new AgeSystem();
  const research    = new ResearchSystem();
  const market      = new MarketSystem();
  const enemyAI     = new EnemyAI(1, teamRes[1], ageSystemEnemy, gather, training, research);
  const projectiles = new ProjectileSystem(scene);
  const garrison    = new GarrisonSystem();
  const placement   = new BuildingPlacement(scene, rig.camera, renderer.domElement);
  const relicSys    = new RelicSystem();
  const relics      = RelicSystem.spawnRelics(scene, 3);
  const vfx         = new VisualEffectSystem(app);

  // ── HUD ──────────────────────────────────────────────────────────────────
  const hud = new HUD(app, teamRes[0]);

  // ── Fog of War ───────────────────────────────────────────────────────────
  const fog = new FogOfWarSystem(scene);

  // ── Damage popups ─────────────────────────────────────────────────────────
  const damagePopup = new DamagePopup(app);
  combat.onHit = (pos, dmg) => { damagePopup.show(pos, dmg); play(SoundId.UnitAttack); };
  combat.onUnitKilled = () => play(SoundId.UnitDie);
  combat.onBuildingDestroyed = () => play(SoundId.BuildingDie);

  // ── Audio hooks (cosmetic seam: sim → sound) ──────────────────────────────
  ageSystem.onAgeUp = () => play(SoundId.AgeUp);
  research.onComplete = (teamId) => { if (teamId === 0) play(SoundId.ResearchDone); };
  combat.onRangedFire = (from, to, splash) => projectiles.fire(from, to, splash);

  // ── Minimap ───────────────────────────────────────────────────────────────
  const minimap = new Minimap(app);
  minimap.onNavigate = (x, z) => rig.panTo(x, z);

  // ── Selection ─────────────────────────────────────────────────────────────
  const selection = new Selection(
    renderer.domElement, rig.camera, scene,
    units, buildings, nodes, gather, combat, garrison,
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
    buildings.push(new Building(scene, pos, 0, type));
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
    units, buildings, nodes, selection, rig, teamRes,
    gather, combat, training, trading,
  };

  // ── Hotkeys (HKEY port) ───────────────────────────────────────────────────
  window.addEventListener("keydown", e => {
    if (e.target instanceof HTMLInputElement) return;
    const key = e.key.toLowerCase();

    if (key === "s") {
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
      placement.cancel();
    }
  });

  // ── Game state ────────────────────────────────────────────────────────────
  let gameOver = false;

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

        for (const u of units) u.tick(step);

        gather.tick(units, buildings, teamRes, scene, step);
        combat.tick(units, buildings, step);
        combat.tickBuildings(buildings, units, step);
        training.tick(buildings, units, scene, research, step);
        trading.tick(units, buildings, teamRes, step);
        garrison.tick(units, buildings, step);
        relicSys.tick(units, relics, buildings, teamRes, step);
        research.tick(units, buildings, teamRes, step);
        market.tick(step);
        enemyAI.tick(units, buildings, nodes, scene, step);
        fog.tick(units, buildings, step);
        ageSystem.tick(teamRes[0], step);
        // ageSystemEnemy is ticked inside EnemyAI.tick() — no separate call needed
        minimap.tick(units, buildings, nodes, fog, step);

        gather.prune();
        trading.prune(units);
        for (let i = units.length - 1; i >= 0; i--) {
          if (!units[i].alive) {
            units[i].root.visible = false;
            scene.remove(units[i].root);
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

        if (!enemyTC.alive && !gameOver) {
          gameOver = true;
          hud.showVictory(0);
          play(SoundId.Victory);
        }
        if (!playerTC.alive && !gameOver) {
          gameOver = true;
          hud.showVictory(1);
          play(SoundId.Defeat);
        }

        acc -= step;
      }
    }

    for (const u of units) u.tick(0, rig.camera);
    for (const b of buildings) b.refreshHpBarCamera(rig.camera);

    if (selection.selected.length > 1) hud.showMultiUnit(selection.selected);
    else if (selection.selected.length === 1) hud.showUnit(selection.selected[0], teamRes[0], onBuild);
    else if (selection.selectedBuilding) hud.showBuilding(selection.selectedBuilding, training, teamRes[0], ageSystem, research, market, garrison);

    damagePopup.tick(rig.camera, dt);
    projectiles.tick(dt);
    vfx.tick(buildings, rig.camera, dt);

    rig.update(dt);
    renderer.render(scene, rig.camera);
    requestAnimationFrame(frame);
  }
  requestAnimationFrame(frame);
}
