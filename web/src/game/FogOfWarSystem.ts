/**
 * FogOfWarSystem.ts — FogOfWarSystem.cs port.
 * 128×128 CPU grid covers the 120×120 world.
 * Three tiers: unexplored (black), shroud (dim), visible (clear).
 *
 * Ground fog: a large PlaneGeometry floating just above the terrain,
 * textured with a CanvasTexture updated from the fog grid — same approach
 * as Unity's Custom/FogOfWar shader on the ground mesh.
 *
 * Enemy entity hiding: enemy unit/building root.visible toggled each
 * VIS_INTERVAL seconds based on whether their grid cell is lit.
 */
import * as THREE from "three";
import { BuildingType, UnitType } from "../core/GameTypes";
import { diplomacy } from "../core/Diplomacy";
import type { Unit } from "./Unit";
import type { Building } from "./Building";

const TEX_SIZE    = 128;
const WORLD_HALF  = 90;  // covers BaseDistance=84 bases with margin
const WORLD_SIZE  = WORLD_HALF * 2;
const PIX_PER_UNIT = TEX_SIZE / WORLD_SIZE;

const REPAINT_INTERVAL = 0.2;
const VIS_INTERVAL     = 0.5;

const UNEXPLORED = 0;
const SHROUD     = 1;
const VISIBLE    = 2;

function unitSight(t: UnitType): number {
  switch (t) {
    case UnitType.Scout:     return 16;
    case UnitType.Cavalry:   return 10;
    case UnitType.Archer:    return 9;
    case UnitType.Militia:   return 8;
    case UnitType.Villager:  return 6;
    case UnitType.Trebuchet: return 5;
    default:                 return 6;
  }
}

function buildingSight(b: Building): number {
  let base: number;
  switch (b.buildingType) {
    case BuildingType.TownCenter:   base = 14; break;
    case BuildingType.Outpost:      base = 12; break;
    case BuildingType.Castle:       base = 10; break;
    case BuildingType.BombardTower:
    case BuildingType.WatchTower:
    case BuildingType.Barracks:
    case BuildingType.ArcheryRange:
    case BuildingType.Stable:       base = 8; break;
    default:                        base = 6; break;
  }
  return base + b.sightBonus;
}

function worldToPixel(w: number): number {
  return Math.round((w + WORLD_HALF) * PIX_PER_UNIT);
}

export class FogOfWarSystem {
  private readonly vis      = new Uint8Array(TEX_SIZE * TEX_SIZE);
  private readonly explored = new Uint8Array(TEX_SIZE * TEX_SIZE);

  private repaintTimer = 0;
  private visTimer     = 0;

  /** Canvas used as the Three.js fog texture source. */
  private readonly fogCanvas: HTMLCanvasElement;
  private readonly fogCtx: CanvasRenderingContext2D;
  private readonly imageData: ImageData;
  private readonly fogTexture: THREE.CanvasTexture;

  /** The fog plane mesh sitting just above the ground. */
  readonly mesh: THREE.Mesh;

  constructor(scene: THREE.Scene) {
    // ── Fog canvas texture ─────────────────────────────────────────────────
    this.fogCanvas = document.createElement("canvas");
    this.fogCanvas.width  = TEX_SIZE;
    this.fogCanvas.height = TEX_SIZE;
    this.fogCtx   = this.fogCanvas.getContext("2d")!;
    this.imageData = new ImageData(TEX_SIZE, TEX_SIZE);

    // Fill fully black initially
    const d = this.imageData.data;
    for (let i = 0; i < TEX_SIZE * TEX_SIZE; i++) {
      d[i * 4 + 3] = 255;
    }

    this.fogTexture = new THREE.CanvasTexture(this.fogCanvas);
    this.fogTexture.magFilter = THREE.LinearFilter;
    this.fogTexture.minFilter = THREE.LinearFilter;

    // ── Fog plane mesh ─────────────────────────────────────────────────────
    // Size matches the world bounds (180×180 = WORLD_HALF*2), placed at y=0.05.
    const geo = new THREE.PlaneGeometry(WORLD_SIZE, WORLD_SIZE);
    const mat = new THREE.MeshBasicMaterial({
      map: this.fogTexture,
      transparent: true,
      depthWrite: false,
      side: THREE.FrontSide,
    });
    this.mesh = new THREE.Mesh(geo, mat);
    this.mesh.rotation.x = -Math.PI / 2; // lay flat
    this.mesh.position.y = 0.05;
    this.mesh.renderOrder = 1;
    scene.add(this.mesh);
  }

  tick(units: Unit[], buildings: Building[], dt: number) {
    this.repaintTimer -= dt;
    if (this.repaintTimer <= 0) {
      this.repaintTimer = REPAINT_INTERVAL;
      this._repaintGrid(units, buildings);
      this._uploadTexture();
    }

    this.visTimer -= dt;
    if (this.visTimer <= 0) {
      this.visTimer = VIS_INTERVAL;
      this._updateEnemyVisibility(units, buildings);
    }
  }

  private _repaintGrid(units: Unit[], buildings: Building[]) {
    for (let i = 0; i < this.vis.length; i++) {
      this.vis[i] = this.explored[i] > 0 ? SHROUD : UNEXPLORED;
    }

    for (const u of units) {
      if (!u.alive || (u.teamId !== 0 && !diplomacy.isAlly(u.teamId, 0))) continue;
      this._paintCircle(u.pos.x, u.pos.z, unitSight(u.unitType));
    }
    for (const b of buildings) {
      if (!b.alive || (b.teamId !== 0 && !diplomacy.isAlly(b.teamId, 0))) continue;
      this._paintCircle(b.pos.x, b.pos.z, buildingSight(b));
    }

    for (let i = 0; i < this.vis.length; i++) {
      if (this.vis[i] === VISIBLE) this.explored[i] = 1;
    }
  }

  private _paintCircle(wx: number, wz: number, radiusU: number) {
    const rp  = radiusU * PIX_PER_UNIT;
    const rp2 = rp * rp;
    const cx  = worldToPixel(wx);
    const cz  = worldToPixel(wz);
    const ext = Math.ceil(rp) + 1;

    const x0 = Math.max(0, cx - ext), x1 = Math.min(TEX_SIZE - 1, cx + ext);
    const z0 = Math.max(0, cz - ext), z1 = Math.min(TEX_SIZE - 1, cz + ext);

    for (let z = z0; z <= z1; z++) {
      const rowOff = z * TEX_SIZE;
      const dz = z - cz;
      for (let x = x0; x <= x1; x++) {
        const dx = x - cx;
        if (dx * dx + dz * dz <= rp2) this.vis[rowOff + x] = VISIBLE;
      }
    }
  }

  private _uploadTexture() {
    const d = this.imageData.data;
    for (let pz = 0; pz < TEX_SIZE; pz++) {
      // Three.js PlaneGeometry UV: V=0 at top (world Z = +60), V=1 at bottom (Z = -60).
      // Our grid row 0 = world Z=-60. So mirror vertically when writing to ImageData.
      const srcRow = TEX_SIZE - 1 - pz;
      for (let px = 0; px < TEX_SIZE; px++) {
        const tier = this.vis[srcRow * TEX_SIZE + px];
        const off  = (pz * TEX_SIZE + px) * 4;
        if (tier === VISIBLE) {
          d[off + 3] = 0;   // fully transparent
        } else if (tier === SHROUD) {
          d[off]     = 0;
          d[off + 1] = 0;
          d[off + 2] = 0;
          d[off + 3] = 110; // explored-but-not-visible: dark grey
        } else {
          d[off]     = 0;
          d[off + 1] = 0;
          d[off + 2] = 0;
          d[off + 3] = 205; // unexplored: near-black, faint terrain silhouette visible
        }
      }
    }

    this.fogCtx.putImageData(this.imageData, 0, 0);
    this.fogTexture.needsUpdate = true;
  }

  private _updateEnemyVisibility(units: Unit[], buildings: Building[]) {
    for (const u of units) {
      if (u.teamId === 0 || diplomacy.isAlly(u.teamId, 0)) continue;
      u.root.visible = this._isLit(u.pos.x, u.pos.z);
    }
    for (const b of buildings) {
      if (b.teamId === 0 || diplomacy.isAlly(b.teamId, 0)) continue;
      b.root.visible = this._isLit(b.pos.x, b.pos.z);
    }
  }

  private _isLit(wx: number, wz: number): boolean {
    const x = Math.max(0, Math.min(TEX_SIZE - 1, worldToPixel(wx)));
    const z = Math.max(0, Math.min(TEX_SIZE - 1, worldToPixel(wz)));
    return this.vis[z * TEX_SIZE + x] === VISIBLE;
  }

  isVisible(wx: number, wz: number): boolean {
    return this._isLit(wx, wz);
  }

  /** Cheat: mark entire map as explored + visible, reveal all enemies. */
  revealAll(): void {
    this.vis.fill(VISIBLE);
    this.explored.fill(VISIBLE);
    this._uploadTexture();
  }
}
