/**
 * Click-select + right-click command + drag-box multi-select — port of
 * SelectionSystem / CommandSystem.cs.
 */
import * as THREE from "three";
import { UnitState } from "../core/GameTypes";
import { GatherSystem } from "./GatherSystem";
import { CombatSystem } from "./CombatSystem";
import type { GarrisonSystem } from "./GarrisonSystem";
import type { Unit } from "./Unit";
import type { Building } from "./Building";
import type { ResourceNode } from "./ResourceNode";

const DRAG_THRESHOLD = 6; // pixels before drag-box activates

export class Selection {
  private readonly ray = new THREE.Raycaster();
  private readonly ndc = new THREE.Vector2();
  readonly selected: Unit[] = [];
  selectedBuilding: Building | null = null;

  onSelectUnit: ((u: Unit | null) => void) | null = null;
  onSelectBuilding: ((b: Building | null) => void) | null = null;

  // ── Drag-box state ────────────────────────────────────────────────────────
  private dragStart: { x: number; y: number } | null = null;
  private dragging = false;
  private readonly boxEl: HTMLDivElement;

  constructor(
    private readonly dom: HTMLElement,
    private readonly camera: THREE.Camera,
    private readonly scene: THREE.Scene,
    private readonly units: Unit[],
    private readonly buildings: Building[],
    private readonly nodes: ResourceNode[],
    private readonly gather: GatherSystem,
    private readonly combat: CombatSystem,
    private readonly garrisonSys?: GarrisonSystem,
  ) {
    // Selection-box DOM element
    this.boxEl = document.createElement("div");
    Object.assign(this.boxEl.style, {
      position: "fixed", display: "none", pointerEvents: "none",
      border: "1px solid #4af", background: "rgba(50,150,255,0.08)",
      zIndex: "10",
    });
    document.body.appendChild(this.boxEl);

    dom.addEventListener("pointerdown", e => {
      if (e.button === 0) {
        this.dragStart = { x: e.clientX, y: e.clientY };
        this.dragging  = false;
      } else if (e.button === 2) {
        this.toNdc(e, dom);
        this.order();
      }
    });

    dom.addEventListener("pointermove", e => {
      if (!this.dragStart) return;
      const dx = e.clientX - this.dragStart.x;
      const dy = e.clientY - this.dragStart.y;
      if (!this.dragging && Math.hypot(dx, dy) > DRAG_THRESHOLD) {
        this.dragging = true;
      }
      if (this.dragging) this._updateBox(e.clientX, e.clientY);
    });

    dom.addEventListener("pointerup", e => {
      if (e.button !== 0) return;
      if (this.dragging) {
        this._finishDrag(e.clientX, e.clientY);
      } else if (this.dragStart) {
        this.toNdc(e, dom);
        this.pick();
      }
      this.dragStart = null;
      this.dragging  = false;
      this.boxEl.style.display = "none";
    });

    dom.addEventListener("contextmenu", e => e.preventDefault());
  }

  private toNdc(e: PointerEvent | MouseEvent, dom: HTMLElement) {
    const r = dom.getBoundingClientRect();
    this.ndc.set(
      ((e.clientX - r.left) / r.width)  * 2 - 1,
      -((e.clientY - r.top)  / r.height) * 2 + 1,
    );
  }

  private _updateBox(cx: number, cy: number) {
    const { x, y } = this.dragStart!;
    const l = Math.min(x, cx), t = Math.min(y, cy);
    const w = Math.abs(cx - x), h = Math.abs(cy - y);
    Object.assign(this.boxEl.style, {
      display: "block",
      left: `${l}px`, top: `${t}px`,
      width: `${w}px`, height: `${h}px`,
    });
  }

  private _finishDrag(cx: number, cy: number) {
    const { x: sx, y: sy } = this.dragStart!;
    const minX = Math.min(sx, cx), maxX = Math.max(sx, cx);
    const minY = Math.min(sy, cy), maxY = Math.max(sy, cy);

    // Deselect previous
    for (const u of this.selected) u.selected = false;
    this.selected.length = 0;
    this.selectedBuilding = null;

    const screenPos = new THREE.Vector3();
    const r = this.dom.getBoundingClientRect();

    for (const u of this.units) {
      if (!u.alive || u.teamId !== 0) continue;
      // Project world position to screen space
      screenPos.copy(u.pos).project(this.camera);
      const sx2 = (screenPos.x + 1) / 2 * r.width  + r.left;
      const sy2 = (-screenPos.y + 1) / 2 * r.height + r.top;
      if (sx2 >= minX && sx2 <= maxX && sy2 >= minY && sy2 <= maxY) {
        u.selected = true;
        this.selected.push(u);
      }
    }

    if (this.selected.length > 0) {
      this.onSelectUnit?.(this.selected[0]);
    } else {
      this.onSelectUnit?.(null);
      this.onSelectBuilding?.(null);
    }
  }

  private pick() {
    this.ray.setFromCamera(this.ndc, this.camera);

    for (const u of this.selected) u.selected = false;
    this.selected.length = 0;
    this.selectedBuilding = null;

    const hits = this.ray.intersectObjects(this.scene.children, true);
    for (const h of hits) {
      let o: THREE.Object3D | null = h.object;
      while (o) {
        if (o.userData.unit) {
          const u = o.userData.unit as Unit;
          if (u.alive) {
            u.selected = true;
            this.selected.push(u);
            this.onSelectUnit?.(u);
            return;
          }
        }
        if (o.userData.building) {
          const b = o.userData.building as Building;
          if (b.alive) {
            this.selectedBuilding = b;
            this.onSelectBuilding?.(b);
            return;
          }
        }
        o = o.parent;
      }
    }

    this.onSelectUnit?.(null);
    this.onSelectBuilding?.(null);
  }

  private order() {
    this.ray.setFromCamera(this.ndc, this.camera);

    // Rally point: no units selected, own building selected → right-click ground sets rally.
    if (this.selected.length === 0) {
      if (this.selectedBuilding?.teamId === 0) {
        const ground = this.scene.getObjectByName("Ground");
        if (ground) {
          const hit = this.ray.intersectObject(ground, false)[0];
          if (hit) { this.selectedBuilding.rallyPoint = hit.point.clone(); }
        }
      }
      return;
    }

    const nodeHits = this.ray.intersectObjects(
      this.nodes.map(n => n.root), true,
    );
    for (const h of nodeHits) {
      let o: THREE.Object3D | null = h.object;
      while (o) {
        if (o.userData.resourceNode) {
          const node = o.userData.resourceNode as ResourceNode;
          for (const u of this.selected) {
            if (u.teamId === 0 && u.gathers) {
              this.gather.assignGather(u, node, this.buildings);
            }
          }
          return;
        }
        o = o.parent;
      }
    }

    const bldHits = this.ray.intersectObjects(
      this.buildings.map(b => b.root), true,
    );
    for (const h of bldHits) {
      let o: THREE.Object3D | null = h.object;
      while (o) {
        if (o.userData.building) {
          const b = o.userData.building as Building;
          if (b.teamId !== 0) {
            for (const u of this.selected) {
              this.combat.attackBuilding(u, b);
            }
            return;
          }
          // Right-click own building with selected units → garrison
          if (b.teamId === 0 && this.garrisonSys && this.garrisonSys.canGarrison(b)) {
            for (const u of this.selected) {
              if (u.teamId === 0) this.garrisonSys.orderGarrison(u, b);
            }
            return;
          }
        }
        o = o.parent;
      }
    }

    const unitHits = this.ray.intersectObjects(
      this.units.map(u => u.root), true,
    );
    for (const h of unitHits) {
      let o: THREE.Object3D | null = h.object;
      while (o) {
        if (o.userData.unit) {
          const t = o.userData.unit as Unit;
          if (t.teamId !== 0 && t.alive) {
            for (const u of this.selected) {
              this.combat.attackUnit(u, t);
            }
            return;
          }
        }
        o = o.parent;
      }
    }

    const ground = this.scene.getObjectByName("Ground");
    if (!ground) return;
    const hit = this.ray.intersectObject(ground, false)[0];
    if (!hit) return;

    this.selected.forEach((u, i) => {
      const off = new THREE.Vector3((i % 3 - 1) * 1.2, 0, Math.floor(i / 3) * 1.2);
      u.attackTarget = null;
      u.attackTargetBuilding = null;
      u.state = UnitState.Moving;
      u.moveTo(hit.point.clone().add(off));
    });
  }
}
