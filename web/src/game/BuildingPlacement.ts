/**
 * BuildingPlacement.cs port — translucent ghost follows cursor while placing
 * a building; green = valid, red = invalid. Left-click places; Escape/Right-click
 * cancels. Grid snaps to 1-unit steps.
 */
import * as THREE from "three";
import { BuildingType } from "../core/GameTypes";
import { DEFS } from "./Building";
import { navGrid } from "../sim/NavGrid";
import type { ResourceManager } from "../core/ResourceManager";

const GRID_SIZE = 1;

const _ghostMatValid   = new THREE.MeshLambertMaterial({ color: 0x44ff55, transparent: true, opacity: 0.45 });
const _ghostMatInvalid = new THREE.MeshLambertMaterial({ color: 0xff3322, transparent: true, opacity: 0.45 });
const _ground = new THREE.Plane(new THREE.Vector3(0, 1, 0), 0);

export class BuildingPlacement {
  private _ghost: THREE.Group | null = null;
  private _type: BuildingType = BuildingType.House;
  private _active = false;

  /** Callback fired when the user confirms placement. */
  onPlace: ((type: BuildingType, pos: THREE.Vector3) => boolean) | null = null;

  get active(): boolean { return this._active; }

  constructor(
    private readonly scene: THREE.Scene,
    private readonly camera: THREE.Camera,
    private readonly canvas: HTMLElement,
  ) {
    canvas.addEventListener("pointermove", e => this._onMove(e));
    canvas.addEventListener("pointerdown", e => this._onDown(e));
    window.addEventListener("keydown",     e => this._onKey(e));
  }

  begin(type: BuildingType): void {
    this.cancel();
    this._type = type;
    this._active = true;
    this._ghost = this._buildGhost(type);
    this.scene.add(this._ghost);
  }

  cancel(): void {
    if (this._ghost) {
      this.scene.remove(this._ghost);
      this._ghost = null;
    }
    this._active = false;
  }

  private _buildGhost(type: BuildingType): THREE.Group {
    const def = DEFS[type];
    const dims: [number, number, number] = this._dims(type);
    const [w, h, d] = dims;
    const geo = new THREE.BoxGeometry(w, h, d);
    const body = new THREE.Mesh(geo, _ghostMatValid.clone());
    body.position.y = h / 2;
    const g = new THREE.Group();
    g.add(body);
    return g;
  }

  private _dims(type: BuildingType): [number, number, number] {
    const dims: Partial<Record<BuildingType, [number, number, number]>> = {
      [BuildingType.TownCenter]: [5, 3.5, 5],
      [BuildingType.House]:      [3, 2.5, 3],
      [BuildingType.Barracks]:   [4, 2.5, 4],
      [BuildingType.ArcheryRange]:[4, 2.5, 4],
      [BuildingType.Stable]:     [4, 2.5, 4],
      [BuildingType.Market]:     [4, 3, 4],
      [BuildingType.Castle]:     [5, 4, 5],
      [BuildingType.Gate]:       [4, 3, 1],
    };
    return dims[type] ?? [3, 2.5, 3];
  }

  /** Set ghost color based on validity. */
  setValid(valid: boolean): void {
    if (!this._ghost) return;
    this._ghost.traverse(o => {
      if (o instanceof THREE.Mesh) {
        (o.material as THREE.MeshLambertMaterial).color.set(valid ? 0x44ff55 : 0xff3322);
      }
    });
  }

  /** Check affordability and NavGrid walkability at current ghost position. */
  isValid(rm: ResourceManager): boolean {
    const def = DEFS[this._type];
    if (!rm.canAfford(0, def.costWood, def.costGold, def.costStone)) return false;
    return this._ghost ? this._footprintWalkable(this._ghost.position) : true;
  }

  private _footprintWalkable(pos: THREE.Vector3): boolean {
    const [w, , d] = this._dims(this._type);
    const hw = w / 2 - 0.1;
    const hd = d / 2 - 0.1;
    // Fish Trap is a water building — its footprint must sit on open water, not land.
    const domain = this._type === BuildingType.FishTrap ? 'water' : 'land';
    for (const [ox, oz] of [[-hw, -hd], [hw, -hd], [-hw, hd], [hw, hd]] as [number, number][]) {
      if (!navGrid.isWalkableWorld(pos.x + ox, pos.z + oz, domain)) return false;
    }
    return true;
  }

  private _worldPos(e: PointerEvent): THREE.Vector3 | null {
    const rect = this.canvas.getBoundingClientRect();
    const ndc = new THREE.Vector2(
      ((e.clientX - rect.left) / rect.width)  * 2 - 1,
      -((e.clientY - rect.top)  / rect.height) * 2 + 1,
    );
    const ray = new THREE.Raycaster();
    ray.setFromCamera(ndc, this.camera);
    const pt = new THREE.Vector3();
    if (!ray.ray.intersectPlane(_ground, pt)) return null;
    // Snap to grid
    pt.x = Math.round(pt.x / GRID_SIZE) * GRID_SIZE;
    pt.z = Math.round(pt.z / GRID_SIZE) * GRID_SIZE;
    pt.y = 0;
    return pt;
  }

  private _onMove(e: PointerEvent): void {
    if (!this._active || !this._ghost) return;
    const pt = this._worldPos(e);
    if (!pt) return;
    this._ghost.position.copy(pt);
    this.setValid(this._footprintWalkable(pt));
  }

  private _onDown(e: PointerEvent): void {
    if (!this._active) return;
    if (e.button === 2) { this.cancel(); return; }
    if (e.button !== 0) return;
    const pt = this._worldPos(e);
    if (!pt) return;
    if (!this._footprintWalkable(pt)) return; // blocked cell — reject
    this.onPlace?.(this._type, pt);
    this.cancel();
  }

  private _onKey(e: KeyboardEvent): void {
    if (!this._active) return;
    if (e.key === "Escape" || e.key === "Backspace") this.cancel();
  }
}
