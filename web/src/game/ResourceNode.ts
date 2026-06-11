/**
 * Harvestable resource node — ResourceNode.cs port.
 * Visual mesh + state; does NOT use MonoBehaviour lifecycle.
 */
import * as THREE from "three";
import { ResourceKind } from "../core/GameTypes";

const GEO_GOLD  = new THREE.CylinderGeometry(0.6, 0.8, 1.0, 6);
const GEO_STONE = new THREE.DodecahedronGeometry(0.8, 0);
const GEO_FOOD  = new THREE.SphereGeometry(0.65, 8, 6);
const GEO_WOOD  = new THREE.CylinderGeometry(0.25, 0.32, 2.5, 6);
const GEO_CROWN = new THREE.ConeGeometry(1.0, 2.8, 6);

const MAT_GOLD   = new THREE.MeshLambertMaterial({ color: 0xf5c842 });
const MAT_STONE  = new THREE.MeshLambertMaterial({ color: 0x9e9e9e });
const MAT_FOOD   = new THREE.MeshLambertMaterial({ color: 0x7fbf2e });
const MAT_TRUNK  = new THREE.MeshLambertMaterial({ color: 0x6b4a2a });
const MAT_PINE   = new THREE.MeshLambertMaterial({ color: 0x3f9b4f });

export class ResourceNode {
  readonly root: THREE.Group;
  readonly kind: ResourceKind;
  amount: number;
  readonly maxAmount: number;
  readonly gathererCap = 6;
  currentGatherers = 0;
  readonly destroyOnDeplete: boolean;
  /** Idle decay rate (food/sec). >0 only for farm nodes. */
  decayPerSecond = 0;
  /** Fractional decay accumulator (sub-1 amounts are accumulated). */
  decayAccum = 0;
  /** Team that owns this node (relevant for per-civ farmDecayMult). */
  ownerTeamId = -1;

  get depleted(): boolean { return this.amount <= 0; }
  get hasRoom(): boolean { return this.currentGatherers < this.gathererCap; }

  constructor(scene: THREE.Scene, pos: THREE.Vector3, kind: ResourceKind, amount: number) {
    this.kind = kind;
    this.amount = amount;
    this.maxAmount = amount;
    this.destroyOnDeplete = true;

    this.root = new THREE.Group();
    this.root.position.copy(pos);
    this.root.userData.resourceNode = this;

    switch (kind) {
      case ResourceKind.Gold: {
        const mesh = new THREE.Mesh(GEO_GOLD, MAT_GOLD);
        mesh.position.y = 0.5;
        mesh.castShadow = true;
        this.root.add(mesh);
        break;
      }
      case ResourceKind.Stone: {
        const mesh = new THREE.Mesh(GEO_STONE, MAT_STONE);
        mesh.position.y = 0.6;
        mesh.castShadow = true;
        this.root.add(mesh);
        break;
      }
      case ResourceKind.Food: {
        const bush = new THREE.Mesh(GEO_FOOD, MAT_FOOD);
        bush.position.y = 0.65;
        bush.castShadow = true;
        this.root.add(bush);
        break;
      }
      case ResourceKind.Wood: {
        const trunk = new THREE.Mesh(GEO_WOOD, MAT_TRUNK);
        trunk.position.y = 1.25;
        const crown = new THREE.Mesh(GEO_CROWN, MAT_PINE);
        crown.position.y = 3.2;
        crown.castShadow = true;
        this.root.add(trunk, crown);
        break;
      }
    }

    scene.add(this.root);
  }

  /** Returns the amount actually taken (may be less than n if node is nearly empty). */
  take(n: number): number {
    const taken = Math.min(n, this.amount);
    this.amount -= taken;
    return taken;
  }

  remove(scene: THREE.Scene) {
    scene.remove(this.root);
  }
}
