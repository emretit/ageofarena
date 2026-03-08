import * as THREE from 'three';

// === Building Types ===

export type BuildingId =
  | 'townCenter'
  | 'house'
  | 'barracks'
  | 'archeryRange'
  | 'stable'
  | 'blacksmith'
  | 'market'
  | 'castle';

export interface BuildingDef {
  id: BuildingId;
  name: string;
  hp: number;
  cost: { food: number; wood: number; gold: number };
  trainable: UnitId[];
  size: { w: number; h: number }; // footprint in world units
  buildTime: number; // seconds
  popSpace?: number; // houses give population space
}

export const BUILDING_DEFS: Record<BuildingId, BuildingDef> = {
  townCenter: {
    id: 'townCenter',
    name: 'Town Center',
    hp: 2400,
    cost: { food: 0, wood: 275, gold: 0 },
    trainable: ['villager'],
    size: { w: 3, h: 3 },
    buildTime: 150,
    popSpace: 5,
  },
  house: {
    id: 'house',
    name: 'House',
    hp: 550,
    cost: { food: 0, wood: 25, gold: 0 },
    trainable: [],
    size: { w: 1.5, h: 1.5 },
    buildTime: 25,
    popSpace: 5,
  },
  barracks: {
    id: 'barracks',
    name: 'Barracks',
    hp: 1200,
    cost: { food: 0, wood: 175, gold: 0 },
    trainable: ['militia', 'spearman'],
    size: { w: 2.5, h: 2 },
    buildTime: 50,
  },
  archeryRange: {
    id: 'archeryRange',
    name: 'Archery Range',
    hp: 1200,
    cost: { food: 0, wood: 175, gold: 0 },
    trainable: ['archer', 'skirmisher'],
    size: { w: 2.5, h: 2 },
    buildTime: 50,
  },
  stable: {
    id: 'stable',
    name: 'Stable',
    hp: 1200,
    cost: { food: 0, wood: 175, gold: 0 },
    trainable: ['scoutCavalry', 'knight'],
    size: { w: 2.5, h: 2 },
    buildTime: 50,
  },
  blacksmith: {
    id: 'blacksmith',
    name: 'Blacksmith',
    hp: 1200,
    cost: { food: 0, wood: 150, gold: 0 },
    trainable: [],
    size: { w: 2, h: 2 },
    buildTime: 40,
  },
  market: {
    id: 'market',
    name: 'Market',
    hp: 1200,
    cost: { food: 0, wood: 175, gold: 0 },
    trainable: ['tradeCart'],
    size: { w: 2.5, h: 2 },
    buildTime: 60,
  },
  castle: {
    id: 'castle',
    name: 'Castle',
    hp: 4800,
    cost: { food: 0, wood: 0, gold: 650 },
    trainable: ['knight'],
    size: { w: 4, h: 4 },
    buildTime: 200,
  },
};

// === Unit Types ===

export type UnitId =
  | 'villager'
  | 'militia'
  | 'spearman'
  | 'archer'
  | 'skirmisher'
  | 'scoutCavalry'
  | 'knight'
  | 'monk'
  | 'tradeCart';

export interface UnitDef {
  id: UnitId;
  name: string;
  hp: number;
  attack: number;
  armor: number;
  speed: number; // world units per second
  range: number; // 0 = melee
  cost: { food: number; wood: number; gold: number };
  trainTime: number; // seconds
  isMounted?: boolean;
}

export const UNIT_DEFS: Record<UnitId, UnitDef> = {
  villager: {
    id: 'villager',
    name: 'Villager',
    hp: 25,
    attack: 3,
    armor: 0,
    speed: 3.5,
    range: 0,
    cost: { food: 50, wood: 0, gold: 0 },
    trainTime: 25,
  },
  militia: {
    id: 'militia',
    name: 'Militia',
    hp: 40,
    attack: 4,
    armor: 1,
    speed: 3.5,
    range: 0,
    cost: { food: 60, wood: 0, gold: 20 },
    trainTime: 21,
  },
  spearman: {
    id: 'spearman',
    name: 'Spearman',
    hp: 45,
    attack: 3,
    armor: 0,
    speed: 3.5,
    range: 0,
    cost: { food: 35, wood: 25, gold: 0 },
    trainTime: 22,
  },
  archer: {
    id: 'archer',
    name: 'Archer',
    hp: 30,
    attack: 4,
    armor: 0,
    speed: 3.5,
    range: 8,
    cost: { food: 0, wood: 25, gold: 45 },
    trainTime: 35,
  },
  skirmisher: {
    id: 'skirmisher',
    name: 'Skirmisher',
    hp: 30,
    attack: 2,
    armor: 0,
    speed: 3.5,
    range: 6,
    cost: { food: 25, wood: 35, gold: 0 },
    trainTime: 22,
  },
  scoutCavalry: {
    id: 'scoutCavalry',
    name: 'Scout Cavalry',
    hp: 60,
    attack: 5,
    armor: 0,
    speed: 5.5,
    range: 0,
    cost: { food: 80, wood: 0, gold: 0 },
    trainTime: 30,
    isMounted: true,
  },
  knight: {
    id: 'knight',
    name: 'Knight',
    hp: 100,
    attack: 10,
    armor: 2,
    speed: 5.0,
    range: 0,
    cost: { food: 60, wood: 0, gold: 75 },
    trainTime: 30,
    isMounted: true,
  },
  monk: {
    id: 'monk',
    name: 'Monk',
    hp: 25,
    attack: 0,
    armor: 0,
    speed: 2.8,
    range: 9,
    cost: { food: 0, wood: 0, gold: 100 },
    trainTime: 51,
  },
  tradeCart: {
    id: 'tradeCart',
    name: 'Trade Cart',
    hp: 70,
    attack: 0,
    armor: 0,
    speed: 4.0,
    range: 0,
    cost: { food: 0, wood: 100, gold: 50 },
    trainTime: 51,
    isMounted: true,
  },
};

// === Villager buildable buildings ===
export const VILLAGER_BUILDABLE: BuildingId[] = [
  'house', 'barracks', 'archeryRange', 'stable', 'blacksmith', 'market', 'castle',
];

// === Entity instances ===

export interface BuildingEntity {
  type: 'building';
  def: BuildingDef;
  mesh: THREE.Group;
  hp: number;
  maxHp: number;
  playerIndex: number; // 0-3
  position: THREE.Vector3;
  rallyPoint: THREE.Vector3 | null;
}

export type Stance = 'aggressive' | 'defensive' | 'standGround' | 'noAttack';

export interface UnitEntity {
  type: 'unit';
  def: UnitDef;
  mesh: THREE.Group;
  hp: number;
  maxHp: number;
  playerIndex: number;
  position: THREE.Vector3;
  targetPos: THREE.Vector3 | null;
  state: 'idle' | 'moving' | 'attacking';
  stance: Stance;
}

export type GameEntity = BuildingEntity | UnitEntity;
