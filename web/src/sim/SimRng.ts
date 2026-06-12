/**
 * SimRng — seeded deterministic RNG for the sim layer.
 * Replaces Math.random() calls inside sim/** (TrainingQueue spawn offset, etc.)
 * Zero three.js imports — pure sim layer.
 */

export class SimRng {
  private _s: number;

  constructor(seed: number) {
    this._s = seed >>> 0;
  }

  /** Returns float in [0, 1). Mulberry32 algorithm. */
  next(): number {
    this._s += 0x6D2B79F5;
    let z = this._s;
    z = (z ^ (z >>> 15)) * (z | 1);
    z ^= z + (z ^ (z >>> 7)) * (z | 61);
    return ((z ^ (z >>> 14)) >>> 0) / 4294967296;
  }

  /** Integer in [0, n). */
  nextInt(n: number): number { return (this.next() * n) | 0; }

  /** Float in [min, max). */
  range(min: number, max: number): number { return min + this.next() * (max - min); }

  get state(): number  { return this._s; }
  set state(s: number) { this._s = s >>> 0; }
}

/** Module-level singleton — call initSimRng(seed) once in main.ts before startGame. */
export let simRng: SimRng = new SimRng(42);

export function initSimRng(seed: number): void {
  simRng = new SimRng(seed);
}
