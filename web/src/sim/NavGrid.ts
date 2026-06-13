/**
 * NavGrid — walkability grid. GridPathfinder.cs grid port.
 * Zero three.js imports — pure sim layer.
 */

export const GRID_SIZE = 192;
export const GRID_HALF = 96;  // cells from center to edge
export const CELL_SIZE = 1.0; // world units per cell

// Cell flag bits
export const FLAG_WATER   = 0x01; // water domain (ships only)
export const FLAG_BLOCKED = 0x02; // solid obstacle
export const FLAG_GATE_T0 = 0x04; // gate owned by team 0 (passable only to team 0)
export const FLAG_GATE_T1 = 0x08;
export const FLAG_GATE_T2 = 0x10;
export const FLAG_GATE_T3 = 0x20;

const GATE_FLAGS = [FLAG_GATE_T0, FLAG_GATE_T1, FLAG_GATE_T2, FLAG_GATE_T3] as const;

export type Domain = 'land' | 'water';

export class NavGrid {
  readonly flags = new Uint8Array(GRID_SIZE * GRID_SIZE);
  private readonly _refs  = new Uint8Array(GRID_SIZE * GRID_SIZE); // BLOCKED ref count

  /** Clear all flags and block refs — call at the start of each new game. */
  reset(): void {
    this.flags.fill(0);
    this._refs.fill(0);
  }

  // ── Coordinate conversion ─────────────────────────────────────────────────

  worldToCell(wx: number, wz: number): [number, number] {
    return [
      Math.max(0, Math.min(GRID_SIZE - 1, Math.floor(wx + GRID_HALF))),
      Math.max(0, Math.min(GRID_SIZE - 1, Math.floor(wz + GRID_HALF))),
    ];
  }

  cellToWorld(cx: number, cz: number): [number, number] {
    return [cx - GRID_HALF + 0.5, cz - GRID_HALF + 0.5];
  }

  inBounds(cx: number, cz: number): boolean {
    return cx >= 0 && cx < GRID_SIZE && cz >= 0 && cz < GRID_SIZE;
  }

  // ── Walkability ───────────────────────────────────────────────────────────

  isWalkable(cx: number, cz: number, domain: Domain = 'land', teamId = -1): boolean {
    if (!this.inBounds(cx, cz)) return false;
    const f = this.flags[cz * GRID_SIZE + cx];
    if (f & FLAG_BLOCKED) return false;
    if (domain === 'land'  && (f & FLAG_WATER)) return false;
    if (domain === 'water' && !(f & FLAG_WATER)) return false;
    for (let t = 0; t < 4; t++) {
      if ((f & GATE_FLAGS[t]) && teamId !== t) return false;
    }
    return true;
  }

  /** Walkability from world coords without allocating a cell tuple (hot-path friendly). */
  isWalkableWorld(wx: number, wz: number, domain: Domain = 'land', teamId = -1): boolean {
    return this.isWalkable(Math.floor(wx + GRID_HALF), Math.floor(wz + GRID_HALF), domain, teamId);
  }

  // ── Stamp (cell coordinates) ──────────────────────────────────────────────

  stampRect(cx0: number, cz0: number, cx1: number, cz1: number): void {
    for (let cz = cz0; cz <= cz1; cz++) {
      for (let cx = cx0; cx <= cx1; cx++) {
        if (!this.inBounds(cx, cz)) continue;
        const i = cz * GRID_SIZE + cx;
        if (this._refs[i] < 255) this._refs[i]++;
        this.flags[i] |= FLAG_BLOCKED;
      }
    }
  }

  unstampRect(cx0: number, cz0: number, cx1: number, cz1: number): void {
    for (let cz = cz0; cz <= cz1; cz++) {
      for (let cx = cx0; cx <= cx1; cx++) {
        if (!this.inBounds(cx, cz)) continue;
        const i = cz * GRID_SIZE + cx;
        if (this._refs[i] > 0) this._refs[i]--;
        if (this._refs[i] === 0) this.flags[i] &= ~FLAG_BLOCKED;
      }
    }
  }

  // ── Stamp (world coordinates) ─────────────────────────────────────────────

  stampWorldRect(wx: number, wz: number, halfW: number, halfD: number): void {
    const [cx0, cz0] = this.worldToCell(wx - halfW, wz - halfD);
    const [cx1, cz1] = this.worldToCell(wx + halfW - 0.01, wz + halfD - 0.01);
    this.stampRect(cx0, cz0, cx1, cz1);
  }

  unstampWorldRect(wx: number, wz: number, halfW: number, halfD: number): void {
    const [cx0, cz0] = this.worldToCell(wx - halfW, wz - halfD);
    const [cx1, cz1] = this.worldToCell(wx + halfW - 0.01, wz + halfD - 0.01);
    this.unstampRect(cx0, cz0, cx1, cz1);
  }

  /** Mark a gate footprint passable only to `ownerTeam` (enemies are blocked). */
  stampGateWorld(wx: number, wz: number, halfW: number, halfD: number, ownerTeam: number): void {
    const flag = GATE_FLAGS[ownerTeam] ?? 0;
    if (!flag) return;
    const [cx0, cz0] = this.worldToCell(wx - halfW, wz - halfD);
    const [cx1, cz1] = this.worldToCell(wx + halfW - 0.01, wz + halfD - 0.01);
    for (let cz = cz0; cz <= cz1; cz++) {
      for (let cx = cx0; cx <= cx1; cx++) {
        if (this.inBounds(cx, cz)) this.flags[cz * GRID_SIZE + cx] |= flag;
      }
    }
  }

  /** Remove a gate footprint's team mask — the cells become passable to everyone. */
  unstampGateWorld(wx: number, wz: number, halfW: number, halfD: number, ownerTeam: number): void {
    const flag = GATE_FLAGS[ownerTeam] ?? 0;
    if (!flag) return;
    const [cx0, cz0] = this.worldToCell(wx - halfW, wz - halfD);
    const [cx1, cz1] = this.worldToCell(wx + halfW - 0.01, wz + halfD - 0.01);
    for (let cz = cz0; cz <= cz1; cz++) {
      for (let cx = cx0; cx <= cx1; cx++) {
        if (this.inBounds(cx, cz)) this.flags[cz * GRID_SIZE + cx] &= ~flag;
      }
    }
  }

  stampWorldCircle(wx: number, wz: number, worldRadius: number): void {
    const rc = Math.ceil(worldRadius / CELL_SIZE);
    const [ccx, ccz] = this.worldToCell(wx, wz);
    const r2 = worldRadius * worldRadius;
    for (let dz = -rc; dz <= rc; dz++) {
      for (let dx = -rc; dx <= rc; dx++) {
        if (dx * dx + dz * dz <= r2) {
          const cx = ccx + dx; const cz = ccz + dz;
          if (!this.inBounds(cx, cz)) continue;
          const i = cz * GRID_SIZE + cx;
          if (this._refs[i] < 255) this._refs[i]++;
          this.flags[i] |= FLAG_BLOCKED;
        }
      }
    }
  }

  /** Reverse a stampWorldCircle with the same (wx, wz, worldRadius). */
  unstampWorldCircle(wx: number, wz: number, worldRadius: number): void {
    const rc = Math.ceil(worldRadius / CELL_SIZE);
    const [ccx, ccz] = this.worldToCell(wx, wz);
    const r2 = worldRadius * worldRadius;
    for (let dz = -rc; dz <= rc; dz++) {
      for (let dx = -rc; dx <= rc; dx++) {
        if (dx * dx + dz * dz <= r2) {
          const cx = ccx + dx; const cz = ccz + dz;
          if (!this.inBounds(cx, cz)) continue;
          const i = cz * GRID_SIZE + cx;
          if (this._refs[i] > 0) this._refs[i]--;
          if (this._refs[i] === 0) this.flags[i] &= ~FLAG_BLOCKED;
        }
      }
    }
  }

  // ── Water marking ─────────────────────────────────────────────────────────

  markWaterBeyondRadius(worldRadius: number): void {
    const r2 = worldRadius * worldRadius;
    for (let cz = 0; cz < GRID_SIZE; cz++) {
      for (let cx = 0; cx < GRID_SIZE; cx++) {
        const dx = cx - GRID_HALF + 0.5;
        const dz = cz - GRID_HALF + 0.5;
        if (dx * dx + dz * dz > r2) {
          this.flags[cz * GRID_SIZE + cx] |= FLAG_WATER;
        }
      }
    }
  }

  /**
   * Islands map: flood the whole grid with water, then carve a land disc at each island
   * centre. Cells outside every island stay water (ships cross, land units are confined to
   * their island). Keep the terrain visual (TerrainRenderer) in sync with the same centres/radius.
   */
  markIslands(centers: ReadonlyArray<readonly [number, number]>, islandRadius: number): void {
    for (let i = 0; i < this.flags.length; i++) this.flags[i] |= FLAG_WATER;
    const r2 = islandRadius * islandRadius;
    for (const [cxw, czw] of centers) {
      for (let cz = 0; cz < GRID_SIZE; cz++) {
        for (let cx = 0; cx < GRID_SIZE; cx++) {
          const dx = cx - GRID_HALF + 0.5 - cxw;
          const dz = cz - GRID_HALF + 0.5 - czw;
          if (dx * dx + dz * dz <= r2) this.flags[cz * GRID_SIZE + cx] &= ~FLAG_WATER;
        }
      }
    }
  }

  // ── Nearest free cell (deterministic square-spiral BFS) ──────────────────

  nearestFreeCell(cx: number, cz: number, domain: Domain = 'land', teamId = -1): [number, number] {
    if (this.isWalkable(cx, cz, domain, teamId)) return [cx, cz];
    for (let r = 1; r < 48; r++) {
      // top row left→right
      for (let dx = -r; dx <= r; dx++) {
        if (this.isWalkable(cx + dx, cz - r, domain, teamId)) return [cx + dx, cz - r];
      }
      // right col top→bottom (skip top-right corner)
      for (let dz = -r + 1; dz <= r; dz++) {
        if (this.isWalkable(cx + r, cz + dz, domain, teamId)) return [cx + r, cz + dz];
      }
      // bottom row right→left (skip bottom-right corner)
      for (let dx = r - 1; dx >= -r; dx--) {
        if (this.isWalkable(cx + dx, cz + r, domain, teamId)) return [cx + dx, cz + r];
      }
      // left col bottom→top (skip both corners)
      for (let dz = r - 1; dz >= -r + 1; dz--) {
        if (this.isWalkable(cx - r, cz + dz, domain, teamId)) return [cx - r, cz + dz];
      }
    }
    return [cx, cz];
  }

  nearestFreeCellWorld(wx: number, wz: number, domain: Domain = 'land', teamId = -1): [number, number] {
    const [cx, cz] = this.worldToCell(wx, wz);
    const [fx, fz] = this.nearestFreeCell(cx, cz, domain, teamId);
    return this.cellToWorld(fx, fz);
  }

  // ── Supercover line-of-sight (Bresenham-based) ────────────────────────────

  lineWalkable(ax: number, az: number, bx: number, bz: number, domain: Domain = 'land', teamId = -1): boolean {
    const [cax, caz] = this.worldToCell(ax, az);
    const [cbx, cbz] = this.worldToCell(bx, bz);
    return this._lineCells(cax, caz, cbx, cbz, domain, teamId);
  }

  lineWalkableCells(cax: number, caz: number, cbx: number, cbz: number, domain: Domain = 'land', teamId = -1): boolean {
    return this._lineCells(cax, caz, cbx, cbz, domain, teamId);
  }

  private _lineCells(cax: number, caz: number, cbx: number, cbz: number, domain: Domain, teamId: number): boolean {
    let x = cax; let z = caz;
    const adx = Math.abs(cbx - cax);
    const adz = Math.abs(cbz - caz);
    const sx = cbx > cax ? 1 : -1;
    const sz = cbz > caz ? 1 : -1;
    let err = adx - adz;

    if (!this.isWalkable(x, z, domain, teamId)) return false;

    for (let steps = 0; steps <= adx + adz; steps++) {
      if (x === cbx && z === cbz) return true;
      const e2 = 2 * err;
      if (e2 > -adz) { err -= adz; x += sx; }
      if (e2 < adx)  { err += adx; z += sz; }
      if (!this.isWalkable(x, z, domain, teamId)) return false;
    }
    return true;
  }
}

/** Singleton — initialized once in main.ts, shared across all systems. */
export const navGrid = new NavGrid();
