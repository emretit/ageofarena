/**
 * Unit entity — UnitEntity.cs port (minimal first slice).
 * Box-body visual + stats from UnitRegistry, movement, HP.
 */
import * as THREE from "three";
import { TeamColors } from "../core/Config";
import { AttackStance, DamageType, UnitState, UnitType } from "../core/GameTypes";
import { ArmorClassFlags } from "../core/GameTypes";
import { getUnitRow } from "../core/UnitRegistry";
import { getTeamBonus } from "../core/CivState";
import { allocId, type EntityId } from "../sim/EntityIds";
import { assetLoader } from "../render/AssetLoader";
import type { Domain } from "../sim/NavGrid";
import type { BakedModel } from "../render/ModelBake";
import type { ResourceNode } from "./ResourceNode";
import type { Building } from "./Building";

// Shared selection ring geometry — one allocation for all units.
const RING_GEO = new THREE.RingGeometry(0.5, 0.65, 24);
const RING_MAT = new THREE.MeshBasicMaterial({ color: 0x6aff6a, side: THREE.DoubleSide });
// Shared team-colour ground disc (AoE2-style player-colour readout under baked models).
const TEAM_DISC_GEO = new THREE.CircleGeometry(0.55, 20);

// HP bar planes (shared geometry, per-instance material)
const BAR_BG_GEO  = new THREE.PlaneGeometry(1.0, 0.12);
const BAR_FG_GEO  = new THREE.PlaneGeometry(1.0, 0.12);
const BAR_BG_MAT  = new THREE.MeshBasicMaterial({ color: 0x330000 });

// Module-shared materials — fading or disposing these per-unit would corrupt every other
// unit's selection ring / HP-bar background. Single source for both the fade and dispose skips.
const SHARED_MATS: ReadonlySet<THREE.Material> = new Set<THREE.Material>([RING_MAT, BAR_BG_MAT]);

export class Unit {
  readonly id: EntityId = allocId();
  readonly root: THREE.Group;
  readonly teamId: number;
  readonly unitType: UnitType;

  // Combat stats (from UnitRegistry)
  moveSpeed: number;
  hp: number;
  readonly maxHp: number;
  readonly baseAtk: number;
  readonly attackInterval: number;
  readonly attackRange: number;
  readonly aggroRadius: number;
  readonly armorMelee: number;
  readonly armorPierce: number;
  readonly armorClass: ArmorClassFlags;
  readonly damageKind: DamageType;
  readonly isRanged: boolean;
  readonly splashRadius: number;
  /** Ballistics researched → projectiles lead moving targets (else miss when target moves). */
  hasBallistics = false;
  readonly bonusVs: Array<{ cls: ArmorClassFlags; bonus: number }>;
  readonly gathers: boolean;
  /** Movement domain — 'land' (default) or 'water' (ships). NavGrid/PathQueue honour it. */
  readonly domain: Domain;

  // Sim-layer position (source of truth; root.position synced each tick)
  x = 0;
  z = 0;
  velX = 0;
  velZ = 0;
  facingAngle = 0;

  // Waypoint path (set by PathQueue; consumed by MovementSystem)
  waypoints: [number, number][] = [];
  waypointIdx = 0;
  // Shift-queued move goals: consumed sequentially on arrival at each destination
  pendingGoals: [number, number][] = [];

  // State
  state = UnitState.Idle;
  stance = AttackStance.Aggressive;
  // AttackMove: destination for the current attack-move order
  attackMoveGoalX = 0;
  attackMoveGoalZ = 0;
  // Patrol: ping-pong between two points
  patrolAX = 0; patrolAZ = 0;
  patrolBX = 0; patrolBZ = 0;
  patrolGoingToB = true; // which end of the route is the current destination
  // Defensive stance leash
  defensiveHomeX = 0;
  defensiveHomeZ = 0;
  private moveTarget: THREE.Vector3 | null = null;
  private ring: THREE.Mesh;
  // Death animation state
  private _deathTimer = 0;

  // Gather state (GatherSystem fills these)
  gatherTarget: ResourceNode | null = null;
  dropoffBuilding: Building | null = null;
  carryKind = 0;
  carryAmount = 0;

  // Combat state (CombatSystem fills these)
  attackTarget: Unit | null = null;
  attackTargetBuilding: Building | null = null;
  attackTimer = 0;

  // Garrison state (GarrisonSystem fills these)
  garrisonTarget: Building | null = null;
  isGarrisoned = false;

  // Team-colored material reference — updated by ConversionSystem on team change.
  // Null for un-tinted baked models (e.g. horses) so conversion won't repaint the body.
  teamMat: THREE.MeshLambertMaterial | THREE.MeshStandardMaterial | null = null;
  // Team-colour ground disc material (baked units) — re-coloured on conversion.
  private _teamDiscMat: THREE.MeshBasicMaterial | null = null;

  // HP bar
  private readonly hpFgMat: THREE.MeshBasicMaterial;
  private readonly hpFg: THREE.Mesh;

  constructor(scene: THREE.Scene, pos: THREE.Vector3, teamId: number, type = UnitType.Villager) {
    this.teamId = teamId;
    this.unitType = type;

    const row = getUnitRow(type);
    const civ = getTeamBonus(teamId);

    const isCav      = type === UnitType.Cavalry || type === UnitType.Scout;
    const isInfantry = type === UnitType.Militia || type === UnitType.Spearman;
    const isArcher   = type === UnitType.Archer || type === UnitType.Longbowman;

    this.moveSpeed    = row.moveSpeed * (isCav ? civ.cavalrySpeedMult : 1);
    this.hp           = Math.floor(row.hp * (isCav ? civ.cavalryHpMult : 1));
    this.maxHp        = this.hp;
    this.baseAtk      = Math.round(row.baseAtk * (isInfantry ? civ.infantryAttackMult : isArcher ? civ.archerAttackMult : 1));
    this.attackInterval = row.attackInterval;
    this.attackRange  = row.baseRange + (isArcher ? civ.archerRangeBonus : 0);
    this.aggroRadius  = row.aggroRadius;
    this.armorMelee   = row.armorMelee;
    this.armorPierce  = row.armorPierce;
    this.armorClass   = row.armorClass;
    this.damageKind   = row.damageKind;
    this.isRanged     = row.isRanged;
    this.splashRadius = row.splashRadius;
    this.bonusVs      = row.bonusVs;
    this.gathers      = row.gathers;
    this.domain       = row.domain ?? 'land';

    this.root = new THREE.Group();
    this.root.position.copy(pos);
    this.x = pos.x;
    this.z = pos.z;

    const team = new THREE.Color(TeamColors[teamId % TeamColors.length]);

    // Prefer baked CC0 model (KayKit/Quaternius); fall back to procedural primitives.
    const baked = assetLoader.getBakedUnit(type);
    if (baked) {
      this._buildFromBaked(baked, team, type);
    } else if (type === UnitType.FishingShip || type === UnitType.Galley) {
      this._buildShip(team, type);
    } else if (type === UnitType.Cavalry || type === UnitType.Scout) {
      this._buildMounted(team);
    } else if (type === UnitType.TradeCart) {
      this._buildCart(team);
    } else {
      this._buildHumanoid(team, type);
    }

    // Selection ring
    this.ring = new THREE.Mesh(RING_GEO, RING_MAT);
    this.ring.rotation.x = -Math.PI / 2;
    this.ring.position.y = 0.03;
    this.ring.visible = false;
    this.root.add(this.ring);

    // HP bar (world-space, always faces camera — updated each frame)
    const barBg = new THREE.Mesh(BAR_BG_GEO, BAR_BG_MAT);
    this.hpFgMat = new THREE.MeshBasicMaterial({ color: 0x44cc22 });
    this.hpFg = new THREE.Mesh(BAR_FG_GEO, this.hpFgMat);
    const hpGroup = new THREE.Group();
    hpGroup.add(barBg, this.hpFg);
    hpGroup.position.y = this._hpBarHeight(type);
    hpGroup.renderOrder = 1;
    this.root.add(hpGroup);
    this._hpBarGroup = hpGroup;

    this.root.userData.unit = this;
    scene.add(this.root);
  }

  private readonly _hpBarGroup: THREE.Group;

  private _hpBarHeight(t: UnitType): number {
    return t === UnitType.Cavalry || t === UnitType.Scout ? 2.4 : 1.6;
  }

  /** Clone a baked CC0 model into this unit's root, tinted toward the team colour. */
  private _buildFromBaked(baked: BakedModel, team: THREE.Color, type: UnitType) {
    const def = assetLoader.getUnitDef(type)!;
    const tintStrength = def.tint ?? 0.4;

    const group = new THREE.Group();
    group.scale.setScalar(def.scale);
    group.rotation.y = def.yawOffset;
    // Clone the material per unit (so team tint + death-fade are per-instance and never
    // mutate the shared baked template). teamMat is only set when the model is tinted —
    // un-tinted models (animals, tint:0) keep teamMat null so conversion won't repaint them.
    let firstMat: THREE.MeshStandardMaterial | null = null;
    for (const g of baked.groups) {
      const mat = g.material.clone() as THREE.MeshStandardMaterial;
      if (tintStrength > 0 && mat.color) {
        mat.color.lerp(team, tintStrength);
        if (!firstMat) firstMat = mat;
      }
      const mesh = new THREE.Mesh(g.geometry, mat); // geometry shared across units
      mesh.castShadow = true;
      group.add(mesh);
    }
    this.teamMat = firstMat;
    this.root.add(group);

    // Always-on team-colour ground disc (player-colour readout for textured models).
    this._teamDiscMat = new THREE.MeshBasicMaterial({ color: team, transparent: true, opacity: 0.5, side: THREE.DoubleSide });
    const disc = new THREE.Mesh(TEAM_DISC_GEO, this._teamDiscMat);
    disc.rotation.x = -Math.PI / 2;
    disc.position.y = 0.02;
    this.root.add(disc);
  }

  /** Re-colour team indicators after a conversion (ConversionSystem). Safe for all unit types. */
  setTeamColor(hex: number): void {
    this.teamMat?.color.setHex(hex);
    this._teamDiscMat?.color.setHex(hex);
  }

  /** Procedural ship (no CC0 model yet): wooden hull + prow + team-coloured sail/flag. */
  private _buildShip(team: THREE.Color, type: UnitType) {
    const woodMat = new THREE.MeshLambertMaterial({ color: 0x6b4423 });
    const hull = new THREE.Mesh(new THREE.BoxGeometry(1.7, 0.5, 0.8), woodMat);
    hull.position.y = 0.3;
    hull.castShadow = true;
    this.root.add(hull);

    const prow = new THREE.Mesh(new THREE.ConeGeometry(0.4, 0.8, 4), woodMat);
    prow.rotation.set(0, Math.PI / 4, -Math.PI / 2);
    prow.position.set(1.05, 0.3, 0);
    this.root.add(prow);

    const teamMat = new THREE.MeshLambertMaterial({ color: team, side: THREE.DoubleSide });
    const mastMat = new THREE.MeshLambertMaterial({ color: 0x3a2a1a });
    if (type === UnitType.Galley) {
      const mast = new THREE.Mesh(new THREE.CylinderGeometry(0.06, 0.06, 1.4, 6), mastMat);
      mast.position.set(0, 1.0, 0);
      const sail = new THREE.Mesh(new THREE.PlaneGeometry(1.0, 0.9), teamMat);
      sail.position.set(0, 1.1, 0);
      this.root.add(mast, sail);
    } else {
      const pole = new THREE.Mesh(new THREE.CylinderGeometry(0.04, 0.04, 0.8, 5), mastMat);
      pole.position.set(-0.8, 0.6, 0);
      const flag = new THREE.Mesh(new THREE.PlaneGeometry(0.55, 0.4), teamMat);
      flag.position.set(-0.55, 0.8, 0);
      this.root.add(pole, flag);
    }
    // teamMat is per-instance → conversion recolours it; death-fade fades it (not shared).
    this.teamMat = teamMat;
  }

  private _buildHumanoid(team: THREE.Color, type: UnitType) {
    const skinColor = 0xd9b88a;
    const bodyH = type === UnitType.Militia || type === UnitType.Spearman ? 0.95 : 0.9;

    const body = new THREE.Mesh(
      new THREE.BoxGeometry(0.55, bodyH, 0.4),
      new THREE.MeshLambertMaterial({ color: skinColor }),
    );
    body.position.y = bodyH / 2 + 0.05;
    body.castShadow = true;

    const tunicMat = new THREE.MeshLambertMaterial({ color: team });
    this.teamMat = tunicMat;
    const tunic = new THREE.Mesh(new THREE.BoxGeometry(0.58, 0.38, 0.43), tunicMat);
    tunic.position.y = 0.30;

    const head = new THREE.Mesh(
      new THREE.BoxGeometry(0.32, 0.3, 0.3),
      new THREE.MeshLambertMaterial({ color: 0xe8c9a0 }),
    );
    head.position.y = bodyH + 0.1;
    head.castShadow = true;

    // Archer gets a bow
    if (type === UnitType.Archer || type === UnitType.Longbowman || type === UnitType.Skirmisher) {
      const bow = new THREE.Mesh(
        new THREE.BoxGeometry(0.06, 0.7, 0.06),
        new THREE.MeshLambertMaterial({ color: 0x5c3a1a }),
      );
      bow.position.set(0.35, 0.6, 0.1);
      this.root.add(bow);
    }

    // Monk gets brown robes and a staff
    if (type === UnitType.Monk) {
      const robe = new THREE.Mesh(
        new THREE.BoxGeometry(0.6, 0.7, 0.45),
        new THREE.MeshLambertMaterial({ color: 0x8b6320 }),
      );
      robe.position.y = 0.4;
      const staff = new THREE.Mesh(
        new THREE.BoxGeometry(0.06, 1.0, 0.06),
        new THREE.MeshLambertMaterial({ color: 0x5c3a1a }),
      );
      staff.position.set(-0.4, 0.55, 0);
      this.root.add(robe, staff);
    }

    this.root.add(body, tunic, head);
  }

  private _buildCart(team: THREE.Color) {
    const body = new THREE.Mesh(
      new THREE.BoxGeometry(1.2, 0.65, 0.8),
      new THREE.MeshLambertMaterial({ color: 0xc49a3c }),
    );
    body.position.y = 0.8;
    body.castShadow = true;

    const canopyMat = new THREE.MeshLambertMaterial({ color: team });
    this.teamMat = canopyMat;
    const canopy = new THREE.Mesh(new THREE.BoxGeometry(1.1, 0.25, 0.75), canopyMat);
    canopy.position.y = 1.3;

    const wheelGeo = new THREE.CylinderGeometry(0.22, 0.22, 0.1, 8);
    const wheelMat = new THREE.MeshLambertMaterial({ color: 0x4a3018 });
    for (const sx of [-1, 1]) {
      for (const sz of [-0.28, 0.28]) {
        const w = new THREE.Mesh(wheelGeo, wheelMat);
        w.rotation.z = Math.PI / 2;
        w.position.set(sx * 0.67, 0.25, sz);
        this.root.add(w);
      }
    }
    this.root.add(body, canopy);
  }

  private _buildMounted(team: THREE.Color) {
    const horse = new THREE.Mesh(
      new THREE.BoxGeometry(0.8, 0.7, 1.4),
      new THREE.MeshLambertMaterial({ color: 0x8b6340 }),
    );
    horse.position.y = 0.7;
    horse.castShadow = true;

    const riderMat = new THREE.MeshLambertMaterial({ color: team });
    this.teamMat = riderMat;
    const rider = new THREE.Mesh(new THREE.BoxGeometry(0.45, 0.55, 0.35), riderMat);
    rider.position.y = 1.55;

    const head = new THREE.Mesh(
      new THREE.BoxGeometry(0.28, 0.26, 0.26),
      new THREE.MeshLambertMaterial({ color: 0xe8c9a0 }),
    );
    head.position.y = 2.0;

    this.root.add(horse, rider, head);
  }

  // ── Selection ──────────────────────────────────────────────────────────────

  set selected(v: boolean) { this.ring.visible = v; }
  get selected(): boolean  { return this.ring.visible; }

  // ── Position helpers ───────────────────────────────────────────────────────

  get pos(): THREE.Vector3 { return this.root.position; }
  get alive(): boolean     { return this.hp > 0; }

  // ── Movement ───────────────────────────────────────────────────────────────

  moveTo(p: THREE.Vector3) {
    this.moveTarget = p.clone();
    // State is managed by the calling system, not here.
  }

  stopMoving() {
    this.moveTarget = null;
  }

  isAtTarget(tolerance = 0.15): boolean {
    if (!this.moveTarget) return true;
    const d = this.moveTarget.clone().sub(this.root.position); d.y = 0;
    return d.length() < tolerance;
  }

  // ── Combat ─────────────────────────────────────────────────────────────────

  takeDamage(amount: number) {
    this.hp = Math.max(0, this.hp - amount);
    this._refreshHpBar();
  }

  private _refreshHpBar() {
    const frac = this.hp / this.maxHp;
    this.hpFg.scale.x = frac;
    this.hpFg.position.x = (frac - 1) * 0.5; // left-align
    // Color: green → yellow → red
    const r = frac < 0.5 ? 1 : (1 - frac) * 2;
    const g = frac < 0.5 ? frac * 2 : 1;
    this.hpFgMat.color.setRGB(r, g, 0);
  }

  // ── Sim tick ───────────────────────────────────────────────────────────────

  /** Starts 0.8s sink + fade death animation. Call instead of removing immediately. */
  startDeathAnim(): void {
    this._deathTimer = 0.8;
  }

  /** True while death animation plays (unit already dead but still visible). */
  get isDying(): boolean { return this._deathTimer > 0; }

  /** Free per-instance GPU materials when the unit is pruned. Geometry is SHARED (baked
   *  templates + module-level ring/HP/disc geometry) and must never be disposed here.
   *  Module-shared materials (RING_MAT, BAR_BG_MAT) are skipped; everything else — cloned
   *  baked materials, teamMat, ground disc, HP-bar foreground, procedural — is per-instance. */
  dispose(): void {
    this.root.traverse(o => {
      const mat = (o as THREE.Mesh).material;
      if (!mat) return;
      const mats = Array.isArray(mat) ? mat : [mat];
      for (const m of mats) {
        if (!SHARED_MATS.has(m)) m.dispose();
      }
    });
  }

  tick(dt: number, camera?: THREE.Camera) {
    // Death animation: sink into ground + fade out
    if (this._deathTimer > 0) {
      this._deathTimer -= dt;
      const t = 1 - this._deathTimer / 0.8; // 0→1
      this.root.position.y = -t * 1.2;
      this.root.traverse(o => {
        if (o instanceof THREE.Mesh) {
          const mat = o.material as THREE.Material;
          // Skip module-shared materials — fading them would corrupt every other unit's
          // selection ring / HP-bar background permanently.
          if (SHARED_MATS.has(mat) || mat === this._teamDiscMat || mat === this.hpFgMat) return;
          const m = mat as THREE.MeshStandardMaterial;
          if (!m.transparent) m.transparent = true;
          m.opacity = 1 - t;
        }
      });
      if (this._deathTimer <= 0) this.root.visible = false;
      return;
    }

    if (!this.alive || this.isGarrisoned) return;

    if (this.waypoints.length > 0) {
      // MovementSystem owns movement — just sync root.position from sim coords
      this.root.position.x = this.x;
      this.root.position.z = this.z;
      if (this.velX !== 0 || this.velZ !== 0) {
        this.root.rotation.y = this.facingAngle;
      }
    } else if (this.moveTarget) {
      // Legacy direct-move (GatherSystem approach, combat chase, rally point)
      const to = this.moveTarget.clone().sub(this.root.position); to.y = 0;
      const dist = to.length();
      if (dist < 0.1) {
        this.moveTarget = null;
      } else {
        const step = Math.min(dist, this.moveSpeed * dt);
        this.root.position.add(to.normalize().multiplyScalar(step));
        this.root.rotation.y = Math.atan2(to.x, to.z);
      }
      // Keep sim coords in sync with root.position for SpatialHash etc.
      this.x = this.root.position.x;
      this.z = this.root.position.z;
    }

    // HP bar faces camera
    if (camera) {
      this._hpBarGroup.lookAt(camera.position);
    }
  }
}
