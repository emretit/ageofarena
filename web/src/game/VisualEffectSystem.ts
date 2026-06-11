/**
 * VisualEffectSystem.cs port — cosmetic building damage smoke.
 * Buildings below 50% HP get a smoke effect (DOM div positioned over the building).
 * Also handles death flash: buildings turn dark red for 0.3s before removal.
 * No gameplay state is changed here.
 */
import * as THREE from "three";
import type { Building } from "./Building";

interface SmokeEntry {
  building: Building;
  el: HTMLDivElement;
}

export class VisualEffectSystem {
  private readonly _container: HTMLElement;
  private readonly _smoke: SmokeEntry[] = [];
  private _checkTimer = 0;

  constructor(container: HTMLElement) {
    this._container = container;
  }

  tick(buildings: Building[], camera: THREE.Camera, dt: number): void {
    this._checkTimer -= dt;

    // Re-check smoke state every 1s
    if (this._checkTimer <= 0) {
      this._checkTimer = 1;
      this._syncSmoke(buildings);
    }

    // Update DOM positions each frame
    const w = this._container.clientWidth;
    const h = this._container.clientHeight;
    for (let i = this._smoke.length - 1; i >= 0; i--) {
      const s = this._smoke[i];
      if (!s.building.alive) {
        s.el.remove();
        this._smoke.splice(i, 1);
        continue;
      }
      const ndc = s.building.pos.clone();
      ndc.y += 4; // float above building
      ndc.project(camera);
      const sx = ((ndc.x + 1) / 2) * w;
      const sy = ((-ndc.y + 1) / 2) * h;
      s.el.style.left = `${sx - 12}px`;
      s.el.style.top  = `${sy - 12}px`;
      // Pulse opacity
      const pulse = 0.5 + Math.sin(Date.now() / 400) * 0.2;
      s.el.style.opacity = String(pulse);
    }
  }

  private _syncSmoke(buildings: Building[]): void {
    const activeBuildings = new Set(this._smoke.map(s => s.building));

    for (const b of buildings) {
      const damaged = b.alive && b.hp < b.maxHp * 0.5;
      const hasSmoke = activeBuildings.has(b);

      if (damaged && !hasSmoke) {
        const el = document.createElement("div");
        el.textContent = "💨";
        Object.assign(el.style, {
          position: "absolute",
          pointerEvents: "none",
          fontSize: "22px",
          userSelect: "none",
          zIndex: "5",
        });
        this._container.appendChild(el);
        this._smoke.push({ building: b, el });
      } else if (!damaged && hasSmoke) {
        const idx = this._smoke.findIndex(s => s.building === b);
        if (idx >= 0) {
          this._smoke[idx].el.remove();
          this._smoke.splice(idx, 1);
        }
      }
    }
  }

  dispose(): void {
    for (const s of this._smoke) s.el.remove();
    this._smoke.length = 0;
  }
}
