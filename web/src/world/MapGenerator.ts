/**
 * MapGenerator.cs port — map archetypes and seed-deterministic placement.
 * Archetypes define: coast belt, interior groves, base positions, mine counts.
 */
import * as THREE from "three";
import { Config } from "../core/Config";
import { ResourceKind } from "../core/GameTypes";
import { ResourceNode } from "../game/ResourceNode";
import { mulberry32 } from "./World";

export const enum MapType { Arena, Arabia, BlackForest, Islands, Nomad }

interface Archetype {
  displayName:       string;
  coastInner:        number;
  coastOuter:        number;
  coastClusters:     number;
  basePositions:     [number, number][]; // [x, z] pairs
  groveCenters:      [number, number][];
  groveRadius:       number;
  extraGoldPerBase:  boolean;
  extraStonePerBase: boolean;
  contestedGold:     number;
  contestedStone:    number;
  forceNomad:        boolean;
}

const ARCHETYPES: Record<MapType, Archetype> = {
  [MapType.Arena]: {
    displayName: "Arena", coastInner: 76, coastOuter: 91, coastClusters: 104,
    basePositions: [[0, -58], [0, 58], [-58, 0], [58, 0]],
    groveCenters: [[0, 34], [0, -34], [34, 0], [-34, 0]], groveRadius: 4,
    extraGoldPerBase: false, extraStonePerBase: false, contestedGold: 2, contestedStone: 2, forceNomad: false,
  },
  [MapType.Arabia]: {
    displayName: "Arabistan", coastInner: 85, coastOuter: 91, coastClusters: 48,
    basePositions: [[0, -52], [0, 52], [-52, 0], [52, 0]],
    groveCenters: [[20, 20], [-20, -20]], groveRadius: 5,
    extraGoldPerBase: false, extraStonePerBase: false, contestedGold: 4, contestedStone: 1, forceNomad: false,
  },
  [MapType.BlackForest]: {
    displayName: "Kara Orman", coastInner: 72, coastOuter: 91, coastClusters: 136,
    basePositions: [[-52, -52], [52, 52], [-52, 52], [52, -52]],
    groveCenters: [[0, 0], [30, 0], [-30, 0], [0, 30], [0, -30], [20, 20], [-20, -20]], groveRadius: 9,
    extraGoldPerBase: true, extraStonePerBase: true, contestedGold: 1, contestedStone: 1, forceNomad: false,
  },
  [MapType.Islands]: {
    // coastClusters 0: no ring of coast trees (they'd land in open ocean on the multi-island
    // layout). Trees come from the centre-island grove instead.
    displayName: "Adalar", coastInner: 87, coastOuter: 91, coastClusters: 0,
    basePositions: [[-60, -60], [60, 60], [-60, 60], [60, -60]],
    groveCenters: [[0, 0]], groveRadius: 8,
    extraGoldPerBase: false, extraStonePerBase: false, contestedGold: 2, contestedStone: 2, forceNomad: false,
  },
  [MapType.Nomad]: {
    displayName: "Göçebe", coastInner: 84, coastOuter: 91, coastClusters: 56,
    basePositions: [[-38, -38], [38, 38], [-38, 38], [38, -38]],
    groveCenters: [[0, 0], [25, 0], [-25, 0], [0, 25]], groveRadius: 5,
    extraGoldPerBase: false, extraStonePerBase: false, contestedGold: 4, contestedStone: 2, forceNomad: true,
  },
};

export function getMapArchetype(type: MapType): Archetype {
  return ARCHETYPES[type];
}

export interface TreeInstance { x: number; z: number; scale: number; }

/** Build the tree geometry for a given map archetype. Returns tree instance data for NavGrid stamping. */
export function buildForest(scene: THREE.Scene, type: MapType, seed = 1453): TreeInstance[] {
  const arch = ARCHETYPES[type];
  const rng = mulberry32(seed);
  const instances: TreeInstance[] = [];

  const pineGeo  = new THREE.ConeGeometry(1.1, 3.2, 6);
  const trunkGeo = new THREE.CylinderGeometry(0.18, 0.22, 1.0, 5);
  const pineMat  = new THREE.MeshLambertMaterial({ color: 0x3f9b4f });
  const trunkMat = new THREE.MeshLambertMaterial({ color: 0x6b4a2a });
  const trees = new THREE.Group();

  const treeCount = Math.round(arch.coastClusters * 6.5);
  for (let i = 0; i < treeCount; i++) {
    const ang = rng() * Math.PI * 2;
    const rad = arch.coastInner + rng() * (arch.coastOuter - arch.coastInner);
    const x = Math.cos(ang) * rad;
    const z = Math.sin(ang) * rad;

    // Skip near base pockets
    const nearPocket = arch.basePositions.some(([bx, bz]) =>
      (x - bx) ** 2 + (z - bz) ** 2 < 18 * 18,
    );
    if (nearPocket) continue;

    const scale = 0.7 + rng() * 0.8;
    const trunk = new THREE.Mesh(trunkGeo, trunkMat);
    trunk.position.set(x, 0.5 * scale, z);
    trunk.scale.setScalar(scale);
    const crown = new THREE.Mesh(pineGeo, pineMat);
    crown.position.set(x, (1.0 + 1.6) * scale, z);
    crown.scale.setScalar(scale);
    crown.castShadow = true;
    trees.add(trunk, crown);
    instances.push({ x, z, scale });
  }

  // Interior groves
  for (const [gx, gz] of arch.groveCenters) {
    const count = Math.round((arch.groveRadius * arch.groveRadius) * 1.4);
    for (let i = 0; i < count; i++) {
      const angle = rng() * Math.PI * 2;
      const r = rng() * arch.groveRadius;
      const x = gx + Math.cos(angle) * r;
      const z = gz + Math.sin(angle) * r;
      const scale = 0.6 + rng() * 0.7;
      const trunk = new THREE.Mesh(trunkGeo, trunkMat);
      trunk.position.set(x, 0.5 * scale, z);
      trunk.scale.setScalar(scale);
      const crown = new THREE.Mesh(pineGeo, pineMat);
      crown.position.set(x, (1.0 + 1.6) * scale, z);
      crown.scale.setScalar(scale);
      crown.castShadow = true;
      trees.add(trunk, crown);
      instances.push({ x, z, scale });
    }
  }

  scene.add(trees);
  return instances;
}

/** Spawn resource nodes for a base pocket. Mirrors WorldRoot.SpawnBaseResources. */
export function spawnBaseResourcesForMap(
  scene: THREE.Scene,
  bx: number, bz: number,
  arch: Archetype,
  rng: () => number,
): ResourceNode[] {
  const nodes: ResourceNode[] = [];
  const base: Array<[number, number, ResourceKind, number]> = [
    [-8, -6, ResourceKind.Gold,  400],
    [ 8, -6, ResourceKind.Gold,  400],
    [-6,  8, ResourceKind.Food,  150],
    [ 6,  8, ResourceKind.Food,  150],
    [-12, 4, ResourceKind.Wood,  250],
    [ 12, 4, ResourceKind.Wood,  250],
    [  0,-12, ResourceKind.Stone, 200],
  ];
  if (arch.extraGoldPerBase)  base.push([-14, -10, ResourceKind.Gold, 300]);
  if (arch.extraStonePerBase) base.push([  14, -10, ResourceKind.Stone, 150]);

  for (const [ox, oz, kind, amt] of base) {
    const jx = (rng() - 0.5) * 2;
    const jz = (rng() - 0.5) * 2;
    nodes.push(new ResourceNode(scene, new THREE.Vector3(bx + ox + jx, 0, bz + oz + jz), kind, amt));
  }
  return nodes;
}

/** Spawn contested centre mines. */
export function spawnContestedMines(
  scene: THREE.Scene,
  arch: Archetype,
  rng: () => number,
): ResourceNode[] {
  const nodes: ResourceNode[] = [];
  for (let i = 0; i < arch.contestedGold; i++) {
    const ang = (i / arch.contestedGold) * Math.PI * 2;
    nodes.push(new ResourceNode(scene, new THREE.Vector3(Math.cos(ang) * 30, 0, Math.sin(ang) * 30), ResourceKind.Gold, 600));
  }
  for (let i = 0; i < arch.contestedStone; i++) {
    const ang = (i / Math.max(1, arch.contestedStone)) * Math.PI * 2 + 0.5;
    nodes.push(new ResourceNode(scene, new THREE.Vector3(Math.cos(ang) * 22, 0, Math.sin(ang) * 22), ResourceKind.Stone, 300));
  }
  return nodes;
}

export function getBasePositions(type: MapType): [number, number][] {
  return ARCHETYPES[type].basePositions;
}

export function isNomad(type: MapType): boolean {
  return ARCHETYPES[type].forceNomad;
}

export function mapDisplayName(type: MapType): string {
  return ARCHETYPES[type].displayName;
}
