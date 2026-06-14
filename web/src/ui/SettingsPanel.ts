/**
 * SettingsPanel.ts — ESC in-game menu: audio, quality tier, edge-scroll,
 * UI scale, rebindable hotkeys, and cheat codes.
 * All settings persisted to localStorage.
 */
import { getMasterVol, getMusicVol, getSfxVol, setMasterVol, setMusicVol, setSfxVol } from "../game/AudioManager";
import type { PostFx, QualityTier } from "../render/PostFx";
import {
  ALL_ACTIONS, ACTION_LABELS, getKey, setKey, resetHotkeys, type HotkeyAction
} from "../game/Hotkeys";

export class SettingsPanel {
  private readonly _el: HTMLDivElement;
  private _visible = false;
  private _hotkeyCleanup: (() => void) | null = null;
  onResume: (() => void) | null = null;
  onUiScale: ((scale: number) => void) | null = null;
  onCheat: ((code: string) => void) | null = null;

  constructor(container: HTMLElement, private readonly _postfx: PostFx) {
    this._el = document.createElement("div");
    this._el.style.cssText = `
      position:absolute; inset:0; background:rgba(0,0,0,0.75); display:none;
      align-items:flex-start; justify-content:center; z-index:8888; font-family:monospace;
      color:#c8c8e0; overflow-y:auto; padding:24px 0;
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
      padding:32px 40px; min-width:360px; max-width:440px; width:100%;
      display:flex; flex-direction:column; gap:16px;
    `;

    const title = document.createElement("div");
    title.textContent = "AYARLAR";
    title.style.cssText = "font-size:20px;font-weight:bold;color:#f5d060;letter-spacing:2px;text-align:center;margin-bottom:8px;";
    panel.appendChild(title);

    // ── Audio ───────────────────────────────────────────────────────────────
    panel.appendChild(this._buildSection("SES"));
    panel.appendChild(this._buildSlider("MASTER SES", getMasterVol(), v => { setMasterVol(v); }));
    panel.appendChild(this._buildSlider("MÜZİK", getMusicVol(), v => { setMusicVol(v); }));
    panel.appendChild(this._buildSlider("SFX", getSfxVol(), v => { setSfxVol(v); }));

    // ── Accessibility ───────────────────────────────────────────────────────
    panel.appendChild(this._buildSection("ERİŞİLEBİLİRLİK"));
    const savedScale = parseFloat(localStorage.getItem("uiScale") ?? "1");
    const scaleRow = this._buildSliderRaw("ARAYÜZ BOYUTU", 0.7, 1.5, 0.05, savedScale, v => {
      this.onUiScale?.(v);
    }, v => `${Math.round(v * 100)}%`);
    panel.appendChild(scaleRow);

    // ── Quality & edge scroll ───────────────────────────────────────────────
    panel.appendChild(this._buildSection("GÖRSEL"));
    panel.appendChild(this._buildTierSelect());
    const edgeKey = "edgeScroll";
    panel.appendChild(this._buildToggle(
      "KENAR KAYDIRMA",
      localStorage.getItem(edgeKey) !== "0",
      v => localStorage.setItem(edgeKey, v ? "1" : "0"),
    ));

    // ── Hotkeys ────────────────────────────────────────────────────────────
    panel.appendChild(this._buildSection("KISAYOLLAR"));
    panel.appendChild(this._buildHotkeySection());

    // ── Cheat codes ────────────────────────────────────────────────────────
    panel.appendChild(this._buildSection("HILE KODLARI"));
    panel.appendChild(this._buildCheatInput());

    // ── Divider ────────────────────────────────────────────────────────────
    const sep = document.createElement("div");
    sep.style.cssText = "border-top:1px solid #333;margin:8px 0;";
    panel.appendChild(sep);

    // ── Resume button ───────────────────────────────────────────────────────
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

  private _buildSection(label: string): HTMLElement {
    const el = document.createElement("div");
    el.style.cssText = "font-size:10px;color:#666;letter-spacing:2px;border-bottom:1px solid #222;padding-bottom:4px;margin-top:4px;";
    el.textContent = label;
    return el;
  }

  private _buildSlider(label: string, initial: number, onChange: (v: number) => void): HTMLElement {
    return this._buildSliderRaw(label, 0, 1, 0.05, initial, onChange);
  }

  private _buildSliderRaw(
    label: string, min: number, max: number, step: number,
    initial: number, onChange: (v: number) => void,
    displayFn?: (v: number) => string,
  ): HTMLElement {
    const fmt = displayFn ?? ((v: number) => `${Math.round(((v - min) / (max - min)) * 100)}%`);
    const row = document.createElement("div");
    row.style.cssText = "display:flex;flex-direction:column;gap:4px;";

    const top = document.createElement("div");
    top.style.cssText = "display:flex;justify-content:space-between;font-size:11px;color:#888;";
    const lbl = document.createElement("span"); lbl.textContent = label;
    const val = document.createElement("span"); val.textContent = fmt(initial);
    top.appendChild(lbl); top.appendChild(val);
    row.appendChild(top);

    const slider = document.createElement("input");
    slider.type  = "range";
    slider.min   = String(min);
    slider.max   = String(max);
    slider.step  = String(step);
    slider.value = String(initial);
    slider.style.cssText = "width:100%;accent-color:#f5d060;";
    slider.addEventListener("input", () => {
      const v = parseFloat(slider.value);
      val.textContent = fmt(v);
      onChange(v);
    });
    row.appendChild(slider);
    return row;
  }

  private _buildTierSelect(): HTMLElement {
    const row = document.createElement("div");
    row.style.cssText = "display:flex;flex-direction:column;gap:6px;";

    const lbl = document.createElement("div");
    lbl.textContent = "KALİTE";
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

  private _buildHotkeySection(): HTMLElement {
    const grid = document.createElement("div");
    grid.style.cssText = "display:flex;flex-direction:column;gap:6px;";

    let listening: { action: HotkeyAction; btn: HTMLButtonElement } | null = null;

    const onKey = (e: KeyboardEvent) => {
      if (!listening) return;
      e.stopPropagation();
      e.preventDefault();
      const k = e.key === " " ? "space" : e.key.toLowerCase();
      if (k === "escape") {
        listening.btn.textContent = `[${getKey(listening.action).toUpperCase()}]`;
        listening.btn.style.borderColor = "#444";
        listening = null;
        return;
      }
      setKey(listening.action, k);
      listening.btn.textContent = `[${k.toUpperCase()}]`;
      listening.btn.style.borderColor = "#444";
      listening = null;
    };
    // Remove previous listener before adding new one to prevent accumulation on rebuild
    this._hotkeyCleanup?.();
    document.addEventListener("keydown", onKey, true);
    this._hotkeyCleanup = () => document.removeEventListener("keydown", onKey, true);

    for (const action of ALL_ACTIONS) {
      const row = document.createElement("div");
      row.style.cssText = "display:flex;justify-content:space-between;align-items:center;";

      const lbl = document.createElement("span");
      lbl.textContent = ACTION_LABELS[action];
      lbl.style.cssText = "font-size:11px;color:#888;";
      row.appendChild(lbl);

      const btn = document.createElement("button");
      btn.textContent = `[${getKey(action).toUpperCase()}]`;
      btn.style.cssText = `
        min-width:48px; padding:3px 8px; border:2px solid #444; border-radius:4px;
        background:#11192a; color:#f5d060; font-family:monospace; font-size:11px; cursor:pointer;
      `;
      btn.addEventListener("click", () => {
        if (listening) {
          listening.btn.textContent = `[${getKey(listening.action).toUpperCase()}]`;
          listening.btn.style.borderColor = "#444";
        }
        listening = { action, btn };
        btn.textContent = "...";
        btn.style.borderColor = "#f5d060";
      });
      row.appendChild(btn);
      grid.appendChild(row);
    }

    const resetRow = document.createElement("div");
    resetRow.style.cssText = "display:flex;justify-content:flex-end;margin-top:4px;";
    const resetBtn = document.createElement("button");
    resetBtn.textContent = "Varsayılanları Yükle";
    resetBtn.style.cssText = `
      padding:3px 10px; border:1px solid #555; border-radius:4px;
      background:transparent; color:#888; font-family:monospace; font-size:10px; cursor:pointer;
    `;
    resetBtn.addEventListener("click", () => {
      resetHotkeys();
      // Rebuild the panel to refresh all key labels
      this._el.innerHTML = "";
      this._build();
    });
    resetRow.appendChild(resetBtn);
    grid.appendChild(resetRow);

    return grid;
  }

  private _buildCheatInput(): HTMLElement {
    const wrap = document.createElement("div");
    wrap.style.cssText = "display:flex;flex-direction:column;gap:6px;";

    const hint = document.createElement("div");
    hint.style.cssText = "font-size:10px;color:#555;";
    hint.textContent = "POLO · LUMBERJACK · CHEESE STEAK JIMMYS · ROBIN HOOD · ROCK ON · AEGIS (toggle)";
    wrap.appendChild(hint);

    const row = document.createElement("div");
    row.style.cssText = "display:flex;gap:6px;";

    const input = document.createElement("input");
    input.type = "text";
    input.placeholder = "Hile kodu girin...";
    input.style.cssText = `
      flex:1; padding:5px 8px; background:#0a0f1a; border:1px solid #444; border-radius:4px;
      color:#f5d060; font-family:monospace; font-size:11px; outline:none;
    `;
    // Prevent space from hiding the panel (ESC/keydown handlers in main.ts)
    input.addEventListener("keydown", e => e.stopPropagation());

    const submitBtn = document.createElement("button");
    submitBtn.textContent = "GÖNDEr";
    submitBtn.style.cssText = `
      padding:5px 12px; background:#1a1a2a; border:1px solid #555; border-radius:4px;
      color:#aaa; font-family:monospace; font-size:11px; cursor:pointer;
    `;

    const fire = () => {
      const code = input.value.trim().toLowerCase();
      if (code) this.onCheat?.(code);
      input.value = "";
    };
    submitBtn.addEventListener("click", fire);
    input.addEventListener("keydown", e => { if (e.key === "Enter") fire(); });

    row.appendChild(input);
    row.appendChild(submitBtn);
    wrap.appendChild(row);
    return wrap;
  }
}
