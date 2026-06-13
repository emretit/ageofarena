/**
 * LoadingScreen.ts — Full-screen overlay shown while AssetLoader bakes the CC0 models.
 * Blocks interaction with the pre-game screen behind it until preload completes, so no
 * Unit is ever constructed before its baked template exists.
 */
export class LoadingScreen {
  private readonly _root: HTMLDivElement;
  private readonly _bar: HTMLDivElement;
  private readonly _label: HTMLDivElement;

  constructor(parent: HTMLElement) {
    this._root = document.createElement("div");
    this._root.style.cssText = [
      "position:fixed", "inset:0", "z-index:9999",
      "display:flex", "flex-direction:column", "align-items:center", "justify-content:center",
      "gap:18px", "background:#0e1626", "color:#dfe8f5",
      "font-family:system-ui,sans-serif",
    ].join(";");

    const title = document.createElement("div");
    title.textContent = "Age of Arena";
    title.style.cssText = "font-size:34px;font-weight:700;letter-spacing:1px;color:#f0d68a";

    this._label = document.createElement("div");
    this._label.textContent = "Modeller yükleniyor…";
    this._label.style.cssText = "font-size:14px;opacity:0.8";

    const track = document.createElement("div");
    track.style.cssText = "width:320px;height:8px;border-radius:4px;background:#243049;overflow:hidden";
    this._bar = document.createElement("div");
    this._bar.style.cssText = "height:100%;width:0%;background:linear-gradient(90deg,#f0d68a,#e0a93c);transition:width .15s ease";
    track.appendChild(this._bar);

    this._root.append(title, track, this._label);
    parent.appendChild(this._root);
  }

  setProgress(loaded: number, total: number): void {
    const pct = total > 0 ? Math.round((loaded / total) * 100) : 100;
    this._bar.style.width = `${pct}%`;
    this._label.textContent = `Modeller yükleniyor… ${pct}%`;
  }

  done(): void {
    this._root.style.transition = "opacity .3s ease";
    this._root.style.opacity = "0";
    setTimeout(() => this._root.remove(), 320);
  }
}
