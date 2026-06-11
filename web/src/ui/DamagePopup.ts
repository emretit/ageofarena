/**
 * DamagePopup.ts — port of DamagePopup.cs.
 * Floating damage numbers that rise and fade over ~0.9s.
 * Uses a pool of DOM elements projected from world to screen each frame.
 */
import * as THREE from "three";

interface PopupEntry {
  el: HTMLDivElement;
  worldPos: THREE.Vector3;
  /** Seconds since spawn */
  age: number;
}

const LIFETIME = 0.9;
const RISE_SPEED = 28; // px/s

const POOL_SIZE = 24;

export class DamagePopup {
  private readonly container: HTMLElement;
  private readonly pool: HTMLDivElement[] = [];
  private readonly active: PopupEntry[] = [];
  private readonly _ndc = new THREE.Vector3();

  constructor(container: HTMLElement) {
    this.container = container;
    for (let i = 0; i < POOL_SIZE; i++) {
      const el = document.createElement("div");
      Object.assign(el.style, {
        position:  "absolute",
        pointerEvents: "none",
        fontFamily: "monospace",
        fontWeight: "bold",
        fontSize:  "15px",
        textShadow: "1px 1px 2px #000",
        display:   "none",
        userSelect: "none",
      });
      container.appendChild(el);
      this.pool.push(el);
    }
  }

  /** Show a damage number at the given world position. */
  show(worldPos: THREE.Vector3, dmg: number) {
    const el = this.pool.pop();
    if (!el) return; // pool exhausted
    const color = dmg >= 20 ? "#ff6040" : dmg >= 10 ? "#ffcc44" : "#fff";
    el.textContent = `-${dmg}`;
    el.style.color = color;
    el.style.display = "block";
    el.style.opacity = "1";
    this.active.push({ el, worldPos: worldPos.clone(), age: 0 });
  }

  tick(camera: THREE.Camera, dt: number) {
    const w = this.container.clientWidth;
    const h = this.container.clientHeight;

    for (let i = this.active.length - 1; i >= 0; i--) {
      const p = this.active[i];
      p.age += dt;

      if (p.age >= LIFETIME) {
        p.el.style.display = "none";
        this.pool.push(p.el);
        this.active.splice(i, 1);
        continue;
      }

      // Project world → NDC → screen
      this._ndc.copy(p.worldPos).project(camera);
      const sx = (this._ndc.x + 1) / 2 * w;
      const sy = (-this._ndc.y + 1) / 2 * h;

      const rise = p.age * RISE_SPEED;
      const alpha = 1 - p.age / LIFETIME;

      p.el.style.left    = `${sx - 12}px`;
      p.el.style.top     = `${sy - 24 - rise}px`;
      p.el.style.opacity = `${alpha.toFixed(2)}`;
    }
  }
}
