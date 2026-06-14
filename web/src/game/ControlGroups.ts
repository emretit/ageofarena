/**
 * ControlGroups — Ctrl+1..9 assign, 1..9 recall, double-tap to focus camera.
 * Port of ControlGroupSystem.cs.
 */
import type { Unit } from './Unit';
import type { CameraRig } from '../camera/CameraRig';

export class ControlGroups {
  private readonly _groups = new Map<number, Unit[]>();
  private readonly _lastTap = new Map<number, number>(); // key → timestamp ms

  assign(key: number, units: Unit[]): void {
    if (key < 1 || key > 9) return;
    // Clear old group membership badge for any unit previously in this slot
    const prev = this._groups.get(key);
    if (prev) for (const u of prev) { if (u.controlGroupNum === key) u.controlGroupNum = 0; }
    const next = units.filter(u => u.alive).slice();
    this._groups.set(key, next);
    for (const u of next) u.controlGroupNum = key;
  }

  recall(key: number, out: Unit[]): Unit[] {
    if (key < 1 || key > 9) return out;
    const group = this._groups.get(key);
    if (!group) return out;
    // Prune dead units
    const alive = group.filter(u => u.alive);
    this._groups.set(key, alive);
    out.length = 0;
    out.push(...alive);
    return out;
  }

  /** Returns true if the tap was a double-tap (≤300ms since last tap for same key). */
  isDoubleTap(key: number): boolean {
    const now = performance.now();
    const last = this._lastTap.get(key) ?? 0;
    this._lastTap.set(key, now);
    return now - last < 300;
  }

  /** Focus camera on the centroid of a group. */
  focusCentroid(key: number, rig: CameraRig): void {
    const group = this._groups.get(key);
    if (!group || group.length === 0) return;
    const alive = group.filter(u => u.alive);
    if (alive.length === 0) return;
    const cx = alive.reduce((s, u) => s + u.x, 0) / alive.length;
    const cz = alive.reduce((s, u) => s + u.z, 0) / alive.length;
    rig.panTo(cx, cz);
  }
}
