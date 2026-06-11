/**
 * Unit entity — UnitEntity.cs port (minimal first slice).
 * Box-body visual + stats from UnitRegistry, movement, HP.
 */
import * as THREE from "three";
import { TeamColors } from "../core/Config";
import { DamageType, UnitState, UnitType } from "../core/GameTypes";
import { ArmorClassFlags } from "../core/GameTypes";
import { getUnitRow } from "../core/UnitRegistry";
import { getTeamBonus } from "../core/CivState";
import type { ResourceNode } from "./ResourceNode";
import type { Building } from "./Building";

// Shared selection ring geometry — one allocation for all units.
const RING_GEO = new THREE.RingGeometry(0.5, 0.65, 24);
const RING_MAT = new THREE.MeshBasicMaterial({ color: 0x6aff6a, side: THREE.DoubleSide });

// HP bar planes (shared geometry, per-instance material)
const BAR_BG_GEO  = new THREE.PlaneGeometry(1.0, 0.12);
const BAR_FG_GEO  = new THREE.PlaneGeometry(1.0, 0.12);
const BAR_BG_MAT  = new THREE.MeshBasicMaterial({ color: 0x330000 });

export class Unit {
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
  readonly bonusVs: Array<{ cls: ArmorClassFlags; bonus: number }>;
  readonly gathers: boolean;

  // State
  state = UnitState.Idle;
  private moveTarget: THREE.Vector3 | null = null;
  private ring: THREE.Mesh;

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

    this.root = new THREE.Group();
    this.root.position.copy(pos);

    const team = new THREE.Color(TeamColors[teamId % TeamColors.length]);

    // Visual body differs by unit type
    if (type === UnitType.Cavalry || type === UnitType.Scout) {
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

  private _buildHumanoid(team: THREE.Color, type: UnitType) {
    const skinColor = 0xd9b88a;
    const bodyH = type === UnitType.Militia || type === UnitType.Spearman ? 0.95 : 0.9;

    const body = new THREE.Mesh(
      new THREE.BoxGeometry(0.55, bodyH, 0.4),
      new THREE.MeshLambertMaterial({ color: skinColor }),
    );
    body.position.y = bodyH / 2 + 0.05;
    body.castShadow = true;

    const tunic = new THREE.Mesh(
      new THREE.BoxGeometry(0.58, 0.38, 0.43),
      new THREE.MeshLambertMaterial({ color: team }),
    );
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

    const canopy = new THREE.Mesh(
      new THREE.BoxGeometry(1.1, 0.25, 0.75),
      new THREE.MeshLambertMaterial({ color: team }),
    );
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

    const rider = new THREE.Mesh(
      new THREE.BoxGeometry(0.45, 0.55, 0.35),
      new THREE.MeshLambertMaterial({ color: team }),
    );
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

  tick(dt: number, camera?: THREE.Camera) {
    if (!this.alive || this.isGarrisoned) return;

    // Movement
    if (this.moveTarget) {
      const to = this.moveTarget.clone().sub(this.root.position); to.y = 0;
      const dist = to.length();
      if (dist < 0.1) {
        this.moveTarget = null;
      } else {
        const step = Math.min(dist, this.moveSpeed * dt);
        this.root.position.add(to.normalize().multiplyScalar(step));
        this.root.rotation.y = Math.atan2(to.x, to.z);
      }
    }

    // HP bar faces camera
    if (camera) {
      this._hpBarGroup.lookAt(camera.position);
    }
  }
}
