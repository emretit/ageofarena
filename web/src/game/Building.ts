/**
 * Building entity — BuildingEntity.cs + BuildingFactory.cs port (minimal).
 * Visual box mesh representing a building on the map.
 */
import * as THREE from "three";
import { ArmorClass, ArmorClassFlags, BuildingType } from "../core/GameTypes";
import { TeamColors } from "../core/Config";
import { getTeamBonus } from "../core/CivState";
import { allocId, type EntityId } from "../sim/EntityIds";
import { assetLoader } from "../render/AssetLoader";

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
  [BuildingType.Dock]:         { hp: 350,  display: "Dock",            isDropoff: true,  dropoffMask: 0b0001, popProvided: 0,  armorMelee: 1, armorPierce: 3,  costWood: 150, costStone: 0,   costGold: 0 },
  [BuildingType.WatchTower]:   { hp: 500,  display: "Watch Tower",     isDropoff: false, dropoffMask: 0,      popProvided: 0,  armorMelee: 5, armorPierce: 8,  costWood: 0,   costStone: 125, costGold: 0 },
  [BuildingType.Gate]:         { hp: 700,  display: "Gate",            isDropoff: false, dropoffMask: 0,      popProvided: 0,  armorMelee: 8, armorPierce: 8,  costWood: 0,   costStone: 30,  costGold: 0 },
  [BuildingType.Outpost]:      { hp: 500,  display: "Outpost",         isDropoff: false, dropoffMask: 0,      popProvided: 0,  armorMelee: 0, armorPierce: 3,  costWood: 25,  costStone: 5,   costGold: 0 },
  [BuildingType.BombardTower]: { hp: 1500, display: "Bombard Tower",   isDropoff: false, dropoffMask: 0,      popProvided: 0,  armorMelee: 5, armorPierce: 8,  costWood: 100, costStone: 125, costGold: 0 },
  [BuildingType.FishTrap]:     { hp: 50,   display: "Fish Trap",       isDropoff: true,  dropoffMask: 0b0001, popProvided: 0,  armorMelee: 0, armorPierce: 0,  costWood: 100, costStone: 0,   costGold: 0 },
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
  [BuildingType.Outpost]:      0x9a8a6a,
  [BuildingType.BombardTower]: 0x6a6a6a,
  [BuildingType.FishTrap]:     0x3a6a7a,
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
  [BuildingType.Gate]:         [4, 3, 1],
  // Large/landmark buildings — footprints match BUILDING_HALF (main.ts) so the baked
  // model fills its collision area instead of defaulting to the small 3×3 fallback.
  [BuildingType.Castle]:       [6, 5, 6],
  [BuildingType.Wonder]:       [6, 7, 6],
  [BuildingType.Monastery]:    [4, 3.5, 4],
  [BuildingType.Mill]:         [3, 2.5, 3],
  [BuildingType.Farm]:         [3, 1.5, 3],
  [BuildingType.Blacksmith]:   [3, 2.5, 3],
  [BuildingType.Outpost]:      [1.5, 4, 1.5],
  [BuildingType.BombardTower]: [2.5, 4.5, 2.5],
  [BuildingType.FishTrap]:     [2, 0.6, 2],
};

export const BUILDING_ARMORCLASS: ArmorClassFlags = ArmorClass.Building;

// Shared HP bar geometry (one allocation per type, shared across all buildings)
const HP_BG_GEO  = new THREE.PlaneGeometry(1.2, 0.13);
const HP_FG_GEO  = new THREE.PlaneGeometry(1.2, 0.13);
const HP_BG_MAT  = new THREE.MeshBasicMaterial({ color: 0x220000 });
// Shared flag-pole material for the team banner added to baked building models.
const POLE_MAT   = new THREE.MeshLambertMaterial({ color: 0x5a3a1a });

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
    const teamColor = new THREE.Color(TeamColors[teamId % TeamColors.length]);

    // Prefer the baked CC0 model; fall back to procedural box geometry.
    const model = assetLoader.getBuilding(type);
    const topY = model ? this._buildFromModel(model, w, teamColor) : this._buildProcedural(type, w, h, d, teamColor);

    scene.add(this.root);

    // HP bar (world-space billboard, updated each frame via refreshHpBarCamera)
    const barBg = new THREE.Mesh(HP_BG_GEO, HP_BG_MAT);
    this._hpFgMat = new THREE.MeshBasicMaterial({ color: teamId === 0 ? 0x44cc22 : 0xcc2222 });
    this._hpFg = new THREE.Mesh(HP_FG_GEO, this._hpFgMat);
    this._hpFg.position.z = 0.001;
    barBg.add(this._hpFg);
    this._hpBarGroup = new THREE.Group();
    this._hpBarGroup.add(barBg);
    this._hpBarGroup.position.y = topY + 0.8;
    this._hpBarGroup.renderOrder = 1;
    this.root.add(this._hpBarGroup);
  }

  /** Normalise a baked model to the footprint width, ground it, add a team banner.
   *  Returns the building's top Y (for HP-bar placement). */
  private _buildFromModel(model: THREE.Group, targetW: number, teamColor: THREE.Color): number {
    model.updateMatrixWorld(true);
    const bbox = new THREE.Box3().setFromObject(model);
    const size = new THREE.Vector3(); bbox.getSize(size);
    const footprint = Math.max(size.x, size.z) || 1;
    const scale = targetW / footprint;
    model.scale.setScalar(scale);
    model.position.set(
      -((bbox.min.x + bbox.max.x) / 2) * scale,
      -bbox.min.y * scale,
      -((bbox.min.z + bbox.max.z) / 2) * scale,
    );
    this.root.add(model);

    const topY = size.y * scale;
    // Team-colour banner on a pole — player-colour readout for un-tinted models.
    const poleH = Math.max(1.5, topY * 0.6);
    const pole = new THREE.Mesh(new THREE.CylinderGeometry(0.07, 0.07, poleH, 6), POLE_MAT);
    pole.position.set(targetW * 0.42, topY + poleH / 2, targetW * 0.42);
    const flag = new THREE.Mesh(
      new THREE.PlaneGeometry(1.1, 0.65),
      new THREE.MeshLambertMaterial({ color: teamColor, side: THREE.DoubleSide }),
    );
    flag.position.set(targetW * 0.42 + 0.55, topY + poleH - 0.4, targetW * 0.42);
    this.root.add(pole, flag);
    return topY;
  }

  /** Procedural box building (fallback when no model is available). Returns top Y. */
  private _buildProcedural(type: BuildingType, w: number, h: number, d: number, teamColor: THREE.Color): number {
    const bodyColor = BODY_COLORS[type] ?? 0xc8a86e;

    const body = new THREE.Mesh(
      new THREE.BoxGeometry(w, h, d),
      new THREE.MeshLambertMaterial({ color: bodyColor }),
    );
    body.position.y = h / 2;
    body.castShadow = true;
    body.receiveShadow = true;

    const roof = new THREE.Mesh(
      new THREE.BoxGeometry(w + 0.2, 0.3, d + 0.2),
      new THREE.MeshLambertMaterial({ color: teamColor }),
    );
    roof.position.y = h + 0.15;

    if (type === BuildingType.TownCenter) {
      const pole = new THREE.Mesh(new THREE.CylinderGeometry(0.08, 0.08, 4, 6), POLE_MAT);
      pole.position.set(w / 2 - 0.3, h + 2.15, d / 2 - 0.3);
      const flag = new THREE.Mesh(
        new THREE.PlaneGeometry(1.2, 0.7),
        new THREE.MeshLambertMaterial({ color: teamColor, side: THREE.DoubleSide }),
      );
      flag.position.set(w / 2 - 0.3 + 0.6, h + 3.9, d / 2 - 0.3);
      this.root.add(pole, flag);
    }

    if (type === BuildingType.WatchTower) {
      const parapet = new THREE.Mesh(
        new THREE.BoxGeometry(w + 0.4, 0.5, d + 0.4),
        new THREE.MeshLambertMaterial({ color: 0x999988 }),
      );
      parapet.position.y = h + 0.25;
      this.root.add(parapet);
    }

    this.root.add(body, roof);
    return h;
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
