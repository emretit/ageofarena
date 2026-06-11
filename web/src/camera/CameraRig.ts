import * as THREE from "three";
import { Config } from "../core/Config";

/**
 * Isometric RTS camera — port of Unity's IsometricCameraRig.
 * Orthographic camera looking at a ground focus point from a fixed isometric
 * angle; WASD/arrow + screen-edge panning, wheel zoom, focus clamped to bounds.
 */
export class CameraRig {
  readonly camera: THREE.OrthographicCamera;
  private focus = new THREE.Vector3(0, 0, Config.BaseSpawnZ); // start over the player base
  private size: number = Config.DefaultZoom;
  private readonly keys = new Set<string>();
  private mouseX = -1;
  private mouseY = -1;
  private dirty = true; // re-project only when focus/zoom/viewport change

  // Scratch vectors reused every frame — update() runs at 60fps and per-frame
  // `new Vector3` churn triggers avoidable GC pauses.
  private readonly _right = new THREE.Vector3();
  private readonly _fwd = new THREE.Vector3();
  private readonly _offset = new THREE.Vector3();

  constructor(private readonly dom: HTMLElement) {
    const aspect = dom.clientWidth / dom.clientHeight;
    this.camera = new THREE.OrthographicCamera(
      -this.size * aspect, this.size * aspect, this.size, -this.size, 0.1, 600);

    window.addEventListener("keydown", e => this.keys.add(e.code));
    window.addEventListener("keyup", e => this.keys.delete(e.code));
    window.addEventListener("mousemove", e => { this.mouseX = e.clientX; this.mouseY = e.clientY; });
    // Cursor leaving the window must stop edge-scroll, or the last edge
    // position keeps panning the camera forever.
    document.addEventListener("mouseleave", () => { this.mouseX = -1; this.mouseY = -1; });
    window.addEventListener("blur", () => { this.mouseX = -1; this.mouseY = -1; });
    window.addEventListener("wheel", e => {
      this.size = THREE.MathUtils.clamp(
        this.size - Math.sign(-e.deltaY) * Config.ZoomSpeed, Config.MinZoom, Config.MaxZoom);
      this.dirty = true;
    }, { passive: true });
    window.addEventListener("resize", () => { this.dirty = true; });
    this.apply();
  }

  focusOn(p: THREE.Vector3) {
    this.focus.x = THREE.MathUtils.clamp(p.x, -Config.CameraBounds, Config.CameraBounds);
    this.focus.z = THREE.MathUtils.clamp(p.z, -Config.CameraBounds, Config.CameraBounds);
    this.dirty = true;
  }

  /** Pan camera focus to given world X/Z coordinates (minimap click navigation). */
  panTo(x: number, z: number) {
    this.focus.x = THREE.MathUtils.clamp(x, -Config.CameraBounds, Config.CameraBounds);
    this.focus.z = THREE.MathUtils.clamp(z, -Config.CameraBounds, Config.CameraBounds);
    this.dirty = true;
  }

  update(dt: number) {
    let mx = 0, mz = 0;
    if (this.keys.has("KeyW") || this.keys.has("ArrowUp")) mz -= 1;
    if (this.keys.has("KeyS") || this.keys.has("ArrowDown")) mz += 1;
    if (this.keys.has("KeyA") || this.keys.has("ArrowLeft")) mx -= 1;
    if (this.keys.has("KeyD") || this.keys.has("ArrowRight")) mx += 1;

    // Screen-edge scroll (skip when the cursor is outside the window).
    if (this.mouseX >= 0) {
      const w = this.dom.clientWidth, h = this.dom.clientHeight, m = Config.EdgeMargin;
      if (this.mouseX < m) mx -= 1; else if (this.mouseX > w - m) mx += 1;
      if (this.mouseY < m) mz -= 1; else if (this.mouseY > h - m) mz += 1;
    }

    if (mx !== 0 || mz !== 0) {
      // Camera-relative pan on the ground plane (shared isometric yaw).
      const yaw = Config.CameraYaw;
      this._right.set(Math.cos(yaw), 0, -Math.sin(yaw)).multiplyScalar(mx);
      this._fwd.set(-Math.sin(yaw), 0, -Math.cos(yaw)).multiplyScalar(-mz);
      this._right.add(this._fwd).normalize()
        .multiplyScalar(Config.PanSpeed * dt * (this.size / Config.DefaultZoom));
      this.focus.add(this._right);
      this.focus.x = THREE.MathUtils.clamp(this.focus.x, -Config.CameraBounds, Config.CameraBounds);
      this.focus.z = THREE.MathUtils.clamp(this.focus.z, -Config.CameraBounds, Config.CameraBounds);
      this.dirty = true;
    }
    if (this.dirty) this.apply();
  }

  private apply() {
    const aspect = this.dom.clientWidth / this.dom.clientHeight;
    this.camera.left = -this.size * aspect;
    this.camera.right = this.size * aspect;
    this.camera.top = this.size;
    this.camera.bottom = -this.size;

    // Classic isometric: 35.26° pitch, shared yaw, far enough to clear the island.
    const dist = 180;
    const pitch = THREE.MathUtils.degToRad(35.264);
    const yaw = Config.CameraYaw;
    this._offset.set(
      Math.sin(yaw) * Math.cos(pitch), Math.sin(pitch), Math.cos(yaw) * Math.cos(pitch),
    ).multiplyScalar(dist);
    this.camera.position.copy(this.focus).add(this._offset);
    this.camera.lookAt(this.focus);
    this.camera.updateProjectionMatrix();
    this.dirty = false;
  }
}
