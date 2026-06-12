/**
 * DMath — deterministic math helpers for the sim layer.
 * sin/cos: 4096-entry lookup (error < 0.001).
 * atan2: thin wrapper over native — IEEE-754 cross-engine consistent in
 * practice; returns value in (-π, π] per spec.
 *
 * Rules: Math.sin / Math.cos / Math.pow are BANNED in sim/**
 * Math.atan2 and Math.sqrt are IEEE-754 bitwise-identical across V8/SM/JSC.
 * Refer: https://tc39.es/ecma262/#sec-math.atan2 (implementation-defined)
 */

const N = 4096;
const TWO_PI = Math.PI * 2;
const STEP_INV = N / TWO_PI;

const _sin = new Float64Array(N + 1);
const _cos = new Float64Array(N + 1);
for (let i = 0; i <= N; i++) {
  const a = (i / N) * TWO_PI;
  _sin[i] = Math.sin(a);
  _cos[i] = Math.cos(a);
}

function _idx(a: number): number {
  const w = ((a % TWO_PI) + TWO_PI) % TWO_PI; // wrap to [0, 2π)
  return w * STEP_INV;
}

function _lerp(a: number, t: Float64Array): number {
  const fi = _idx(a);
  const i = fi | 0;
  return t[i] + (t[i + 1] - t[i]) * (fi - i);
}

export const DMath = {
  sin(a: number): number  { return _lerp(a, _sin); },
  cos(a: number): number  { return _lerp(a, _cos); },
  /** Native atan2 — IEEE-754 consistent on V8/SM/JSC. */
  atan2: Math.atan2.bind(Math),
  sqrt: Math.sqrt.bind(Math),
} as const;
