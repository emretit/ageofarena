/**
 * SettingsPanel.ts — ESC in-game menu: audio, quality tier, edge-scroll.
 * All settings persisted to localStorage.
 */
import { getMasterVol, getMusicVol, getSfxVol, setMasterVol, setMusicVol, setSfxVol } from "../game/AudioManager";
import type { PostFx, QualityTier } from "../render/PostFx";

export class SettingsPanel {
  private readonly _el: HTMLDivElement;
  private _visible = false;
  onResume: (() => void) | null = null;

  constructor(container: HTMLElement, private readonly _postfx: PostFx) {
    this._el = document.createElement("div");
    this._el.style.cssText = `
      position:absolute; inset:0; background:rgba(0,0,0,0.75); display:none;
      align-items:center; justify-content:center; z-index:8888; font-family:monospace;
      color:#c8c8e0;
    `;
    this._build();
    container.appendChild(this._el);
  }

  toggle(): void {
    this._visible = !this._visible;
    this._el.style.display = this._visible ? "flex" : "none";
  }

  hide(): void {
    this._visible = false;
    this._el.style.display = "none";
  }

  get isVisible(): boolean { return this._visible; }

  private _build(): void {
    const panel = document.createElement("div");
    panel.style.cssText = `
      background:#0d1422; border:2px solid #444; border-radius:8px;
      padding:32px 40px; min-width:320px; display:flex; flex-direction:column; gap:16px;
    `;

    const title = document.createElement("div");
    title.textContent = "AYARLAR";
    title.style.cssText = "font-size:20px;font-weight:bold;color:#f5d060;letter-spacing:2px;text-align:center;margin-bottom:8px;";
    panel.appendChild(title);

    // Audio sliders
    panel.appendChild(this._buildSlider("MASTER SES", getMasterVol(), v => {
      setMasterVol(v);
    }));
    panel.appendChild(this._buildSlider("MÜZİK", getMusicVol(), v => {
      setMusicVol(v);
    }));
    panel.appendChild(this._buildSlider("SFX", getSfxVol(), v => {
      setSfxVol(v);
    }));

    // Quality tier
    panel.appendChild(this._buildTierSelect());

    // Edge scroll
    const edgeKey = "edgeScroll";
    const edgeRow = this._buildToggle(
      "KENAR KAYDIRMA",
      localStorage.getItem(edgeKey) !== "0",
      v => localStorage.setItem(edgeKey, v ? "1" : "0"),
    );
    panel.appendChild(edgeRow);

    // Divider
    const sep = document.createElement("div");
    sep.style.cssText = "border-top:1px solid #333;margin:8px 0;";
    panel.appendChild(sep);

    // Resume button
    const resumeBtn = document.createElement("button");
    resumeBtn.textContent = "DEVAM ET";
    resumeBtn.style.cssText = `
      padding:10px 24px; font-size:14px; font-weight:bold; font-family:monospace;
      background:#1f5c22; color:#c8f5c0; border:2px solid #3a9a3a; border-radius:6px;
      cursor:pointer; letter-spacing:2px;
    `;
    resumeBtn.addEventListener("click", () => {
      this.hide();
      this.onResume?.();
    });
    panel.appendChild(resumeBtn);

    this._el.appendChild(panel);
  }

  private _buildSlider(label: string, initial: number, onChange: (v: number) => void): HTMLElement {
    const row = document.createElement("div");
    row.style.cssText = "display:flex;flex-direction:column;gap:4px;";

    const top = document.createElement("div");
    top.style.cssText = "display:flex;justify-content:space-between;font-size:11px;color:#888;";
    const lbl = document.createElement("span"); lbl.textContent = label;
    const val = document.createElement("span"); val.textContent = Math.round(initial * 100) + "%";
    top.appendChild(lbl); top.appendChild(val);
    row.appendChild(top);

    const slider = document.createElement("input");
    slider.type = "range";
    slider.min  = "0";
    slider.max  = "1";
    slider.step = "0.05";
    slider.value = String(initial);
    slider.style.cssText = "width:100%;accent-color:#f5d060;";
    slider.addEventListener("input", () => {
      const v = parseFloat(slider.value);
      val.textContent = Math.round(v * 100) + "%";
      onChange(v);
    });
    row.appendChild(slider);
    return row;
  }

  private _buildTierSelect(): HTMLElement {
    const row = document.createElement("div");
    row.style.cssText = "display:flex;flex-direction:column;gap:6px;";

    const lbl = document.createElement("div");
    lbl.textContent = "GÖRSEL KALİTE";
    lbl.style.cssText = "font-size:11px;color:#888;";
    row.appendChild(lbl);

    const btnRow = document.createElement("div");
    btnRow.style.cssText = "display:flex;gap:6px;";
    const tiers: QualityTier[] = ['High', 'Medium', 'Low'];
    const names = ["Yüksek", "Orta", "Düşük"];
    let activeTier = this._postfx.tier;

    const buttons: HTMLButtonElement[] = [];
    tiers.forEach((t, i) => {
      const btn = document.createElement("button");
      btn.textContent = names[i];
      btn.style.cssText = `
        flex:1; padding:6px; border:2px solid #333; border-radius:4px;
        background:#11192a; color:#aaa; font-family:monospace; font-size:11px; cursor:pointer;
      `;
      const select = () => {
        buttons.forEach(b => { b.style.borderColor = "#333"; b.style.color = "#aaa"; });
        btn.style.borderColor = "#f5d060";
        btn.style.color = "#f5d060";
        activeTier = t;
        this._postfx.setTier(t);
        localStorage.setItem("qualityTier", t);
      };
      if (t === activeTier) select();
      btn.addEventListener("click", select);
      buttons.push(btn);
      btnRow.appendChild(btn);
    });
    row.appendChild(btnRow);
    return row;
  }

  private _buildToggle(label: string, initial: boolean, onChange: (v: boolean) => void): HTMLElement {
    const row = document.createElement("div");
    row.style.cssText = "display:flex;justify-content:space-between;align-items:center;";

    const lbl = document.createElement("div");
    lbl.textContent = label;
    lbl.style.cssText = "font-size:11px;color:#888;";
    row.appendChild(lbl);

    let current = initial;
    const btn = document.createElement("button");
    const refresh = () => {
      btn.textContent = current ? "AÇIK" : "KAPALI";
      btn.style.background  = current ? "#1f5c22" : "#2a1515";
      btn.style.borderColor = current ? "#3a9a3a" : "#9a3a3a";
      btn.style.color       = current ? "#c8f5c0" : "#f5c0c0";
    };
    btn.style.cssText = "padding:4px 12px;border:2px solid;border-radius:4px;font-family:monospace;font-size:11px;cursor:pointer;";
    refresh();
    btn.addEventListener("click", () => {
      current = !current;
      refresh();
      onChange(current);
    });
    row.appendChild(btn);
    return row;
  }
}
