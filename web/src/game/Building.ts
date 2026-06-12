/**
 * Building entity — BuildingEntity.cs + BuildingFactory.cs port (minimal).
 * Visual box mesh representing a building on the map.
 */
import * as THREE from "three";
import { ArmorClass, ArmorClassFlags, BuildingType } from "../core/GameTypes";
import { TeamColors } from "../core/Config";
import { getTeamBonus } from "../core/CivState";
import { allocId, type EntityId } from "../sim/EntityIds";

export interface BuildingDef {
  hp: number;
  display: string;
  isDropoff: boolean;
  dropoffMask: number;
  popProvided: number;
  armorMelee: number;
  armorPierce: number;
  /** Build cost (wood is most common; AoE2 values) */
  costWood: number;
  costStone: number;
  costGold: number;
}

export const DEFS: Record<BuildingType, BuildingDef> = {
  [BuildingType.TownCenter]:   { hp: 600,  display: "Town Center",    isDropoff: true,  dropoffMask: 0b1111, popProvided: 5,  armorMelee: 3, armorPierce: 5,  costWood: 0,   costStone: 0,   costGold: 0 },
  [BuildingType.House]:        { hp: 300,  display: "House",           isDropoff: false, dropoffMask: 0,      popProvided: 5,  armorMelee: 1, armorPierce: 3,  costWood: 25,  costStone: 0,   costGold: 0 },
  [BuildingType.Barracks]:     { hp: 400,  display: "Barracks",        isDropoff: false, dropoffMask: 0,      popProvided: 0,  armorMelee: 1, armorPierce: 3,  costWood: 175, costStone: 0,   costGold: 0 },
  [BuildingType.ArcheryRange]: { hp: 400,  display: "Archery Range",   isDropoff: false, dropoffMask: 0,      popProvided: 0,  armorMelee: 1, armorPierce: 3,  costWood: 175, costStone: 0,   costGold: 0 },
  [BuildingType.Stable]:       { hp: 400,  display: "Stable",          isDropoff: false, dropoffMask: 0,      popProvided: 0,  armorMelee: 1, armorPierce: 3,  costWood: 150, costStone: 0,   costGold: 0 },
  [BuildingType.Farm]:         { hp: 200,  display: "Farm",            isDropoff: false, dropoffMask: 0,      popProvided: 0,  armorMelee: 0, armorPierce: 0,  costWood: 60,  costStone: 0,   costGold: 0 },
  [BuildingType.LumberCamp]:   { hp: 150,  display: "Lumber Camp",     isDropoff: true,  dropoffMask: 0b0010, popProvided: 0,  armorMelee: 0, armorPierce: 0,  costWood: 100, costStone: 0,   costGold: 0 },
  [BuildingType.MiningCamp]:   { hp: 150,  display: "Mining Camp",     isDropoff: true,  dropoffMask: 0b1100, popProvided: 0,  armorMelee: 0, armorPierce: 0,  costWood: 100, costStone: 0,   costGold: 0 },
  [BuildingType.Mill]:         { hp: 150,  display: "Mill",            isDropoff: true,  dropoffMask: 0b0001, popProvided: 0,  armorMelee: 0, armorPierce: 0,  costWood: 100, costStone: 0,   costGold: 0 },
  [BuildingType.Market]:       { hp: 350,  display: "Market",          isDropoff: false, dropoffMask: 0,      popProvided: 0,  armorMelee: 1, armorPierce: 3,  costWood: 175, costStone: 0,   costGold: 0 },
  [BuildingType.Castle]:       { hp: 2000, display: "Castle",          isDropoff: false, dropoffMask: 0,      popProvided: 10, armorMelee: 8, armorPierce: 8,  costWood: 0,   costStone: 650, costGold: 0 },
  [BuildingType.Wall]:         { hp: 200,  display: "Wall",            isDropoff: false, dropoffMask: 0,      popProvided: 0,  armorMelee: 10,armorPierce: 10, costWood: 0,   costStone: 5,   costGold: 0 },
  [BuildingType.Wonder]:       { hp: 3000, display: "Wonder",          isDropoff: false, dropoffMask: 0,      popProvided: 0,  armorMelee: 5, armorPierce: 8,  costWood: 0,   costStone: 1000,costGold: 1000 },
  [BuildingType.Blacksmith]:   { hp: 350,  display: "Blacksmith",      isDropoff: false, dropoffMask: 0,      popProvided: 0,  armorMelee: 1, armorPierce: 3,  costWood: 150, costStone: 0,   costGold: 0 },
  [BuildingType.Monastery]:    { hp: 350,  display: "Monastery",       isDropoff: false, dropoffMask: 0,      popProvided: 0,  armorMelee: 1, armorPierce: 3,  costWood: 175, costStone: 0,   costGold: 0 },
  [BuildingType.University]:   { hp: 350,  display: "University",      isDropoff: false, dropoffMask: 0,      popProvided: 0,  armorMelee: 1, armorPierce: 3,  costWood: 200, costStone: 0,   costGold: 0 },
  [BuildingType.SiegeWorkshop]:{ hp: 400,  display: "Siege Workshop",  isDropoff: false, dropoffMask: 0,      popProvided: 0,  armorMelee: 1, armorPierce: 3,  costWood: 200, costStone: 0,   costGold: 0 },
  [BuildingType.Dock]:         { hp: 350,  display: "Dock",            isDropoff: false, dropoffMask: 0,      popProvided: 0,  armorMelee: 1, armorPierce: 3,  costWood: 150, costStone: 0,   costGold: 0 },
  [BuildingType.WatchTower]:   { hp: 500,  display: "Watch Tower",     isDropoff: false, dropoffMask: 0,      popProvided: 0,  armorMelee: 5, armorPierce: 8,  costWood: 0,   costStone: 125, costGold: 0 },
};

// Building mesh colors by type
const BODY_COLORS: Partial<Record<BuildingType, number>> = {
  [BuildingType.TownCenter]:   0xc8a86e,
  [BuildingType.House]:        0xd4a06a,
  [BuildingType.Barracks]:     0x8c6c4a,
  [BuildingType.ArcheryRange]: 0x7a6040,
  [BuildingType.Stable]:       0x9e8060,
  [BuildingType.LumberCamp]:   0x7a5c38,
  [BuildingType.MiningCamp]:   0x888090,
  [BuildingType.Market]:       0xc8a830,
  [BuildingType.University]:   0x4a6a9e,
  [BuildingType.SiegeWorkshop]:0x6a5040,
  [BuildingType.Dock]:         0x4a7a9e,
  [BuildingType.WatchTower]:   0x808070,
};

/** Dimensions per building type [width, height, depth] */
const DIMS: Partial<Record<BuildingType, [number, number, number]>> = {
  [BuildingType.TownCenter]:   [5, 3.5, 5],
  [BuildingType.House]:        [3, 2.5, 3],
  [BuildingType.Barracks]:     [4, 2.5, 4],
  [BuildingType.ArcheryRange]: [4, 2.5, 4],
  [BuildingType.Stable]:       [4, 2.5, 4],
  [BuildingType.LumberCamp]:   [3, 2, 3],
  [BuildingType.MiningCamp]:   [3, 2, 3],
  [BuildingType.Market]:       [4, 3, 4],
  [BuildingType.University]:   [4, 3, 4],
  [BuildingType.SiegeWorkshop]:[4, 2.5, 4],
  [BuildingType.Dock]:         [4, 2, 5],
  [BuildingType.WatchTower]:   [2, 4.5, 2],
};

export const BUILDING_ARMORCLASS: ArmorClassFlags = ArmorClass.Building;

// Shared HP bar geometry (one allocation per type, shared across all buildings)
const HP_BG_GEO  = new THREE.PlaneGeometry(1.2, 0.13);
const HP_FG_GEO  = new THREE.PlaneGeometry(1.2, 0.13);
const HP_BG_MAT  = new THREE.MeshBasicMaterial({ color: 0x220000 });

export class Building {
  readonly id: EntityId = allocId();
  readonly root: THREE.Group;
  readonly teamId: number;
  readonly buildingType: BuildingType;
  readonly def: BuildingDef;
  readonly armorClass: ArmorClassFlags = BUILDING_ARMORCLASS;
  hp: number;
  readonly maxHp: number;

  /** Rally point: trained units move here after spawning. Null = spawn in place. */
  rallyPoint: THREE.Vector3 | null = null;

  private readonly _hpBarGroup: THREE.Group;
  private readonly _hpFgMat: THREE.MeshBasicMaterial;
  private readonly _hpFg: THREE.Mesh;

  get pos(): THREE.Vector3 { return this.root.position; }
  get alive(): boolean { return this.hp > 0; }

  constructor(scene: THREE.Scene, pos: THREE.Vector3, teamId: number, type: BuildingType) {
    this.teamId = teamId;
    this.buildingType = type;
    this.def = DEFS[type];
    const bldHp = Math.floor(this.def.hp * getTeamBonus(teamId).buildingHpMult);
    this.hp = bldHp;
    this.maxHp = bldHp;

    this.root = new THREE.Group();
    this.root.position.copy(pos);
    this.root.userData.building = this;

    const [w, h, d] = DIMS[type] ?? [3, 2.5, 3];
    const bodyColor = BODY_COLORS[type] ?? 0xc8a86e;
    const teamColor = new THREE.Color(TeamColors[teamId % TeamColors.length]);

    // Main building body
    const body = new THREE.Mesh(
      new THREE.BoxGeometry(w, h, d),
      new THREE.MeshLambertMaterial({ color: bodyColor }),
    );
    body.position.y = h / 2;
    body.castShadow = true;
    body.receiveShadow = true;

    // Team-colored banner/roof trim
    const roof = new THREE.Mesh(
      new THREE.BoxGeometry(w + 0.2, 0.3, d + 0.2),
      new THREE.MeshLambertMaterial({ color: teamColor }),
    );
    roof.position.y = h + 0.15;

    // TC gets a larger flag pole
    if (type === BuildingType.TownCenter) {
      const pole = new THREE.Mesh(
        new THREE.CylinderGeometry(0.08, 0.08, 4, 6),
        new THREE.MeshLambertMaterial({ color: 0x5a3a1a }),
      );
      pole.position.set(w / 2 - 0.3, h + 2.15, d / 2 - 0.3);
      const flag = new THREE.Mesh(
        new THREE.PlaneGeometry(1.2, 0.7),
        new THREE.MeshLambertMaterial({ color: teamColor, side: THREE.DoubleSide }),
      );
      flag.position.set(w / 2 - 0.3 + 0.6, h + 3.9, d / 2 - 0.3);
      this.root.add(pole, flag);
    }

    // WatchTower gets a narrow tower shape
    if (type === BuildingType.WatchTower) {
      const parapet = new THREE.Mesh(
        new THREE.BoxGeometry(w + 0.4, 0.5, d + 0.4),
        new THREE.MeshLambertMaterial({ color: 0x999988 }),
      );
      parapet.position.y = h + 0.25;
      this.root.add(parapet);
    }

    this.root.add(body, roof);
    scene.add(this.root);

    // HP bar (world-space billboard, updated each frame via refreshHpBarCamera)
    const barBg = new THREE.Mesh(HP_BG_GEO, HP_BG_MAT);
    this._hpFgMat = new THREE.MeshBasicMaterial({ color: teamId === 0 ? 0x44cc22 : 0xcc2222 });
    this._hpFg = new THREE.Mesh(HP_FG_GEO, this._hpFgMat);
    this._hpFg.position.z = 0.001;
    barBg.add(this._hpFg);
    this._hpBarGroup = new THREE.Group();
    this._hpBarGroup.add(barBg);
    this._hpBarGroup.position.y = h + 0.8;
    this._hpBarGroup.renderOrder = 1;
    this.root.add(this._hpBarGroup);
  }

  takeDamage(dmg: number) {
    this.hp = Math.max(0, this.hp - dmg);
    this._refreshHpBar();
  }

  /** Call once per render frame with current camera so the bar billboards. */
  refreshHpBarCamera(camera: THREE.Camera) {
    this._hpBarGroup.lookAt(
      camera.position.x,
      this.root.position.y + this._hpBarGroup.position.y,
      camera.position.z,
    );
  }

  private _refreshHpBar() {
    const frac = this.hp / this.maxHp;
    this._hpFg.scale.x = frac;
    this._hpFg.position.x = (frac - 1) * 0.6; // left-align (half of bar width 1.2)
    const r = frac < 0.5 ? 1 : (1 - frac) * 2;
    const g = frac < 0.5 ? frac * 2 : 1;
    this._hpFgMat.color.setRGB(r, g, 0);
  }

  remove(scene: THREE.Scene) {
    scene.remove(this.root);
  }
}
