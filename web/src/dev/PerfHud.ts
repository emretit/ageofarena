/**
 * PerfHud.ts — F3 overlay: FPS p95, sim ms, draw call, path queue.
 * Port of Unity's dev overlay pattern.
 */

const SAMPLE_WINDOW = 120; // frames

export class PerfHud {
  private readonly _el: HTMLDivElement;
  private _visible = false;
  private readonly _frameTimes: number[] = [];
  private _simMs     = 0;
  private _drawCalls = 0;
  private _pathQueue = 0;
  private _unitCount = 0;

  get visible(): boolean { return this._visible; }

  constructor(container: HTMLElement) {
    this._el = document.createElement("div");
    this._el.style.cssText = `
      position:absolute; top:8px; left:50%; transform:translateX(-50%);
      background:rgba(0,0,0,0.7); color:#0f0; font-family:monospace; font-size:11px;
      padding:6px 12px; border-radius:4px; pointer-events:none; z-index:999;
      display:none; min-width:220px; line-height:1.6;
    `;
    container.appendChild(this._el);

    window.addEventListener("keydown", e => {
      if (e.key === "F3") { e.preventDefault(); this.toggle(); }
    });
  }

  toggle(): void {
    this._visible = !this._visible;
    this._el.style.display = this._visible ? "block" : "none";
  }

  /** Call once per rendered frame with dt in seconds. */
  tickFrame(dt: number): void {
    if (!this._visible) return;
    this._frameTimes.push(dt * 1000);
    if (this._frameTimes.length > SAMPLE_WINDOW) this._frameTimes.shift();
  }

  /** Set sim tick duration in ms. */
  setSimMs(ms: number): void { this._simMs = ms; }

  /** Set drawcall count from renderer.info. */
  setDrawCalls(n: number): void { this._drawCalls = n; }

  /** Set path queue depth. */
  setPathQueue(n: number): void { this._pathQueue = n; }

  /** Set live unit count (stress-test visibility). */
  setUnitCount(n: number): void { this._unitCount = n; }

  /** Call after tickFrame to refresh the display. */
  flush(): void {
    if (!this._visible || this._frameTimes.length === 0) return;

    const sorted = [...this._frameTimes].sort((a, b) => b - a);
    const p95    = sorted[Math.floor(sorted.length * 0.05)] ?? 0;
    const fps    = this._frameTimes.length > 0
      ? Math.round(1000 / (this._frameTimes.reduce((a, b) => a + b, 0) / this._frameTimes.length))
      : 0;

    this._el.innerHTML = [
      `FPS: ${fps}   p95 frame: ${p95.toFixed(1)}ms`,
      `Sim tick: ${this._simMs.toFixed(2)}ms`,
      `Draw calls: ${this._drawCalls}`,
      `Path queue: ${this._pathQueue}`,
      `Units: ${this._unitCount}`,
    ].join("<br>");
  }
}
