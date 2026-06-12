/**
 * SpatialHash — uniform-grid spatial index for fast radius queries.
 * Zero three.js imports — pure sim layer.
 * Replaces O(n²) aggro scan in CombatSystem.
 */

const CELL = 4; // world units per hash cell

export interface SpatialEntry {
  x: number;
  z: number;
}

export class SpatialHash<T extends SpatialEntry> {
  private readonly _cells = new Map<number, T[]>();

  private _key(cx: number, cz: number): number {
    // Integer pair hash (assumes ±500 world range → ±125 cell range → safe)
    return (cx + 200) * 1000 + (cz + 200);
  }

  private _cell(x: number, z: number): [number, number] {
    return [Math.floor(x / CELL), Math.floor(z / CELL)];
  }

  insert(e: T): void {
    const [cx, cz] = this._cell(e.x, e.z);
    const key = this._key(cx, cz);
    let arr = this._cells.get(key);
    if (!arr) { arr = []; this._cells.set(key, arr); }
    arr.push(e);
  }

  remove(e: T): void {
    const [cx, cz] = this._cell(e.x, e.z);
    const arr = this._cells.get(this._key(cx, cz));
    if (!arr) return;
    const i = arr.indexOf(e);
    if (i >= 0) arr.splice(i, 1);
  }

  /** Query all entries within `radius` of (x, z). Appends to `out`. */
  query(x: number, z: number, radius: number, out: T[] = []): T[] {
    const [cx0, cz0] = this._cell(x - radius, z - radius);
    const [cx1, cz1] = this._cell(x + radius, z + radius);
    const r2 = radius * radius;
    for (let cz = cz0; cz <= cz1; cz++) {
      for (let cx = cx0; cx <= cx1; cx++) {
        const arr = this._cells.get(this._key(cx, cz));
        if (!arr) continue;
        for (const e of arr) {
          const dx = e.x - x; const dz = e.z - z;
          if (dx * dx + dz * dz <= r2) out.push(e);
        }
      }
    }
    return out;
  }

  /** Rebuild from scratch — call after bulk position updates. */
  rebuild(entities: T[]): void {
    this._cells.clear();
    for (const e of entities) this.insert(e);
  }

  clear(): void { this._cells.clear(); }
}
