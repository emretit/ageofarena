/**
 * Pathfinder — A* on NavGrid with octile heuristic, string-pulling, goal relaxation.
 * Zero three.js imports — pure sim layer.
 */

import { NavGrid, Domain, GRID_SIZE } from './NavGrid';

export interface PathResult {
  waypoints: [number, number][]; // world x, z pairs
  partial: boolean;              // true if goal unreachable; returned nearest reachable
}

// Integer cost scale (×10 avoids float comparisons)
const COST_CARD = 10; // cardinal step cost
const COST_DIAG = 14; // diagonal step cost (~10√2)

interface ANode {
  cx: number;
  cz: number;
  g:  number;
  f:  number;
  parentKey: number; // cz*GRID_SIZE+cx of parent, -1 = none
}

// ── Binary min-heap keyed on (f*10000 - g) for stable tie-breaking ──────────

class MinHeap {
  private _data: ANode[] = [];

  get size(): number { return this._data.length; }

  push(n: ANode): void {
    this._data.push(n);
    this._up(this._data.length - 1);
  }

  pop(): ANode {
    const top = this._data[0];
    const last = this._data.pop()!;
    if (this._data.length > 0) { this._data[0] = last; this._down(0); }
    return top;
  }

  private _key(n: ANode): number { return n.f * 10000 - n.g; }

  private _up(i: number): void {
    while (i > 0) {
      const p = (i - 1) >> 1;
      if (this._key(this._data[i]) < this._key(this._data[p])) {
        [this._data[i], this._data[p]] = [this._data[p], this._data[i]];
        i = p;
      } else break;
    }
  }

  private _down(i: number): void {
    const n = this._data.length;
    while (true) {
      let m = i;
      const l = 2 * i + 1; const r = l + 1;
      if (l < n && this._key(this._data[l]) < this._key(this._data[m])) m = l;
      if (r < n && this._key(this._data[r]) < this._key(this._data[m])) m = r;
      if (m === i) break;
      [this._data[i], this._data[m]] = [this._data[m], this._data[i]];
      i = m;
    }
  }
}

// ── A* internals ─────────────────────────────────────────────────────────────

function heuristic(dx: number, dz: number): number {
  const a = Math.abs(dx); const b = Math.abs(dz);
  return COST_CARD * Math.max(a, b) + (COST_DIAG - COST_CARD) * Math.min(a, b);
}

const DIRS: [number, number, number][] = [
  [ 1, 0, COST_CARD], [-1,  0, COST_CARD], [ 0,  1, COST_CARD], [0, -1, COST_CARD],
  [ 1, 1, COST_DIAG], [ 1, -1, COST_DIAG], [-1,  1, COST_DIAG], [-1, -1, COST_DIAG],
];

// ── Public API ────────────────────────────────────────────────────────────────

export function findPath(
  startWX: number, startWZ: number,
  goalWX:  number, goalWZ:  number,
  nav: NavGrid,
  domain:   Domain = 'land',
  teamId    = -1,
  maxNodes  = 4000,
): PathResult {
  let [scx, scz] = nav.worldToCell(startWX, startWZ);
  let [gcx, gcz] = nav.worldToCell(goalWX,  goalWZ);

  // Start walkability — snap to nearest free if needed
  if (!nav.isWalkable(scx, scz, domain, teamId)) {
    [scx, scz] = nav.nearestFreeCell(scx, scz, domain, teamId);
  }
  // Goal relaxation
  if (!nav.isWalkable(gcx, gcz, domain, teamId)) {
    [gcx, gcz] = nav.nearestFreeCell(gcx, gcz, domain, teamId);
  }

  if (scx === gcx && scz === gcz) return { waypoints: [], partial: false };

  const open    = new MinHeap();
  const closed  = new Map<number, ANode>();  // key → node
  const openG   = new Map<number, number>(); // key → best g seen

  const startH  = heuristic(gcx - scx, gcz - scz);
  const startN: ANode = { cx: scx, cz: scz, g: 0, f: startH, parentKey: -1 };
  open.push(startN);
  openG.set(scz * GRID_SIZE + scx, 0);

  let bestNode  = startN;
  let bestH     = startH;
  let expanded  = 0;

  while (open.size > 0 && expanded < maxNodes) {
    const cur    = open.pop();
    const curKey = cur.cz * GRID_SIZE + cur.cx;
    if (closed.has(curKey)) continue;
    closed.set(curKey, cur);
    expanded++;

    const h = heuristic(gcx - cur.cx, gcz - cur.cz);
    if (h < bestH) { bestH = h; bestNode = cur; }

    if (cur.cx === gcx && cur.cz === gcz) {
      return { waypoints: _pullPath(cur, closed, nav, domain, teamId), partial: false };
    }

    for (const [dx, dz, cost] of DIRS) {
      const nx = cur.cx + dx;
      const nz = cur.cz + dz;
      if (!nav.isWalkable(nx, nz, domain, teamId)) continue;
      // No corner-cutting on diagonals
      if (dx !== 0 && dz !== 0) {
        if (!nav.isWalkable(cur.cx + dx, cur.cz, domain, teamId)) continue;
        if (!nav.isWalkable(cur.cx, cur.cz + dz, domain, teamId)) continue;
      }
      const ng   = cur.g + cost;
      const nKey = nz * GRID_SIZE + nx;
      if (closed.has(nKey)) continue;
      const prev = openG.get(nKey);
      if (prev !== undefined && prev <= ng) continue;
      openG.set(nKey, ng);
      open.push({ cx: nx, cz: nz, g: ng, f: ng + heuristic(gcx - nx, gcz - nz), parentKey: curKey });
    }
  }

  // Partial path — return path to best reachable node
  if (bestNode === startN) return { waypoints: [], partial: true };
  return { waypoints: _pullPath(bestNode, closed, nav, domain, teamId), partial: true };
}

// ── String-pulling ────────────────────────────────────────────────────────────

function _pullPath(
  end:    ANode,
  closed: Map<number, ANode>,
  nav:    NavGrid,
  domain: Domain,
  teamId: number,
): [number, number][] {
  // Reconstruct raw cell list
  const cells: [number, number][] = [];
  let cur: ANode | undefined = end;
  while (cur) {
    cells.push([cur.cx, cur.cz]);
    if (cur.parentKey < 0) break;
    cur = closed.get(cur.parentKey);
  }
  cells.reverse();

  if (cells.length <= 2) {
    return cells.map(([cx, cz]) => nav.cellToWorld(cx, cz));
  }

  // Greedy string-pull: from cells[i], find furthest j with direct LOS
  const pulled: [number, number][] = [cells[0]];
  let i = 0;
  while (i < cells.length - 1) {
    let j = cells.length - 1;
    while (j > i + 1 && !nav.lineWalkableCells(cells[i][0], cells[i][1], cells[j][0], cells[j][1], domain, teamId)) {
      j--;
    }
    pulled.push(cells[j]);
    i = j;
  }

  return pulled.map(([cx, cz]) => nav.cellToWorld(cx, cz));
}
