/**
 * PreGameScreen v2 — opponent count, difficulty, personality + civ select.
 * onStart fires with player civ, N opponent configs, and map type.
 */
import { Civilization, CIVILIZATION_DEFS, PLAYABLE_CIVS, type CivBonus } from "../core/CivilizationDefs";
import { MapType } from "../world/MapGenerator";
import { Difficulty, Personality } from "../game/EnemyAI";

export interface OpponentConfig {
  civ:         Civilization;
  difficulty:  Difficulty;
  personality: Personality;
}

export class PreGameScreen {
  private readonly _el: HTMLDivElement;

  onStart: ((player: Civilization, opponents: OpponentConfig[], map: MapType) => void) | null = null;
  /** Fired when the player chooses online multiplayer instead of a skirmish. */
  onOnline: (() => void) | null = null;

  constructor(container: HTMLElement) {
    this._el = document.createElement("div");
    this._el.style.cssText = `
      position:absolute; inset:0; background:rgba(6,8,16,0.96);
      display:flex; flex-direction:column; align-items:center; justify-content:center;
      font-family:monospace; color:#c8c8e0; z-index:9999; overflow-y:auto;
      padding:20px 0;
    `;
    this._build();
    container.appendChild(this._el);
  }

  private _civSel       = Civilization.None;
  private _mapSel       = MapType.Arabia;
  private _opponentCount = 1;
  private _difficulty    = Difficulty.Normal;
  private _personality   = Personality.Balanced;

  private _build(): void {
    this._el.innerHTML = "";

    // Title
    const title = document.createElement("div");
    title.textContent = "MEDENİYETİNİ SEÇ";
    title.style.cssText = "font-size:28px;font-weight:bold;color:#f5d060;margin-bottom:6px;letter-spacing:2px;";
    this._el.appendChild(title);

    const hint = document.createElement("div");
    hint.textContent = "Bonuslar oyun başladığında hemen etkinleşir. 'Yok' ile dengeli başlayabilirsin.";
    hint.style.cssText = "font-size:12px;color:#888;margin-bottom:20px;";
    this._el.appendChild(hint);

    // Civ grid
    const grid = document.createElement("div");
    grid.style.cssText = "display:flex;flex-wrap:wrap;gap:8px;justify-content:center;max-width:860px;margin-bottom:24px;";

    const civChoices = [Civilization.None, ...PLAYABLE_CIVS];
    for (const civ of civChoices) {
      const def = CIVILIZATION_DEFS[civ];
      const btn = document.createElement("button");
      btn.style.cssText = `
        width:190px; padding:10px 8px; border:2px solid #333; border-radius:6px;
        background:#11192a; color:#c8c8e0; font-family:monospace; font-size:12px;
        cursor:pointer; text-align:left; transition:background 0.15s;
      `;
      btn.innerHTML = `<div style="font-size:18px;margin-bottom:3px">${def.flag} ${def.display}</div>${this._bonusSummary(def)}`;
      btn.dataset.civ = String(civ);
      btn.addEventListener("click", () => {
        this._civSel = civ;
        this._refreshCivButtonsIn(grid);
      });
      grid.appendChild(btn);
    }
    this._el.appendChild(grid);
    this._refreshCivButtonsIn(grid);

    // Map type picker
    const mapLabel = document.createElement("div");
    mapLabel.textContent = "HARİTA SEÇ";
    mapLabel.style.cssText = "font-size:14px;font-weight:bold;color:#f5d060;margin-bottom:10px;letter-spacing:1px;";
    this._el.appendChild(mapLabel);

    const mapRow = document.createElement("div");
    mapRow.style.cssText = "display:flex;gap:8px;margin-bottom:24px;flex-wrap:wrap;justify-content:center;";

    const MAP_NAMES: Record<MapType, string> = {
      [MapType.Arabia]:     "Arabistan",
      [MapType.Arena]:      "Arena",
      [MapType.BlackForest]:"Kara Orman",
      [MapType.Islands]:    "Adalar",
      [MapType.Nomad]:      "Göçebe",
    };
    for (const [k, name] of Object.entries(MAP_NAMES) as Array<[string, string]>) {
      const mt = Number(k) as MapType;
      const btn = document.createElement("button");
      btn.dataset.map = k;
      btn.textContent = name;
      btn.style.cssText = `
        padding:8px 18px; border:2px solid #333; border-radius:5px;
        background:#11192a; color:#aaa; font-family:monospace; font-size:13px;
        cursor:pointer;
      `;
      btn.addEventListener("click", () => {
        this._mapSel = mt;
        this._refreshMapButtonsIn(mapRow);
      });
      mapRow.appendChild(btn);
    }
    this._el.appendChild(mapRow);
    this._refreshMapButtonsIn(mapRow);

    // ── Opponent / difficulty / personality row ────────────────────────────
    const optLabel = document.createElement("div");
    optLabel.textContent = "OYUN AYARLARI";
    optLabel.style.cssText = "font-size:14px;font-weight:bold;color:#f5d060;margin-bottom:10px;letter-spacing:1px;";
    this._el.appendChild(optLabel);

    const optRow = document.createElement("div");
    optRow.style.cssText = "display:flex;gap:24px;margin-bottom:28px;flex-wrap:wrap;justify-content:center;align-items:center;";

    // Opponent count
    const oppGroup = this._buildOptionGroup("RAKİP", ["1", "2", "3"], String(this._opponentCount), v => {
      this._opponentCount = Number(v);
    });
    optRow.appendChild(oppGroup);

    // Difficulty
    const DIFF_LABELS = ["Çok Kolay", "Kolay", "Normal", "Zor", "Çok Zor", "Aşırı Zor"];
    const diffGroup = this._buildOptionGroup("ZORLUK", DIFF_LABELS, DIFF_LABELS[this._difficulty], (_, i) => {
      this._difficulty = i as Difficulty;
    });
    optRow.appendChild(diffGroup);

    // Personality
    const PERS_LABELS = ["Saldırgan", "Dengeli", "Geliştirici"];
    const persGroup = this._buildOptionGroup("KİŞİLİK", PERS_LABELS, PERS_LABELS[this._personality], (_, i) => {
      this._personality = i as Personality;
    });
    optRow.appendChild(persGroup);

    this._el.appendChild(optRow);

    // Start button
    const startBtn = document.createElement("button");
    startBtn.textContent = "OYNA";
    startBtn.style.cssText = `
      padding:14px 60px; font-size:20px; font-weight:bold; font-family:monospace;
      background:#1f5c22; color:#c8f5c0; border:2px solid #3a9a3a; border-radius:8px;
      cursor:pointer; letter-spacing:3px;
    `;
    startBtn.addEventListener("mouseenter", () => { startBtn.style.background = "#2a7a2e"; });
    startBtn.addEventListener("mouseleave", () => { startBtn.style.background = "#1f5c22"; });
    startBtn.addEventListener("click", () => {
      const opponents = this._buildOpponents();
      this._el.remove();
      this.onStart?.(this._civSel, opponents, this._mapSel);
    });
    this._el.appendChild(startBtn);

    // Online multiplayer button
    const onlineBtn = document.createElement("button");
    onlineBtn.textContent = "ÇOK OYUNCULU";
    onlineBtn.style.cssText = `
      padding:10px 40px; font-size:15px; font-weight:bold; font-family:monospace;
      background:#1f3c6c; color:#c0d8f5; border:2px solid #3a6a9a; border-radius:8px;
      cursor:pointer; letter-spacing:2px; margin-top:8px;
    `;
    onlineBtn.addEventListener("mouseenter", () => { onlineBtn.style.background = "#2a4f8c"; });
    onlineBtn.addEventListener("mouseleave", () => { onlineBtn.style.background = "#1f3c6c"; });
    onlineBtn.addEventListener("click", () => {
      this._el.remove();
      this.onOnline?.();
    });
    this._el.appendChild(onlineBtn);
  }

  private _buildOpponents(): OpponentConfig[] {
    const taken = new Set([this._civSel]);
    const remaining = PLAYABLE_CIVS.filter(c => !taken.has(c));
    // Shuffle remaining (non-deterministic but only in menu, not sim)
    for (let i = remaining.length - 1; i > 0; i--) {
      const j = Math.floor(Math.random() * (i + 1));
      [remaining[i], remaining[j]] = [remaining[j], remaining[i]];
    }

    const opponents: OpponentConfig[] = [];
    for (let i = 0; i < this._opponentCount; i++) {
      opponents.push({
        civ:         remaining[i % remaining.length],
        difficulty:  this._difficulty,
        personality: this._personality,
      });
    }
    return opponents;
  }

  /** Build a row of labeled option buttons. Callback receives (value, index). */
  private _buildOptionGroup(
    label: string,
    options: string[],
    initial: string,
    onChange: (value: string, index: number) => void,
  ): HTMLElement {
    const group = document.createElement("div");
    group.style.cssText = "display:flex;flex-direction:column;align-items:center;gap:6px;";

    const lbl = document.createElement("div");
    lbl.textContent = label;
    lbl.style.cssText = "font-size:11px;color:#888;letter-spacing:1px;";
    group.appendChild(lbl);

    const row = document.createElement("div");
    row.style.cssText = "display:flex;gap:4px;";

    let activeBtn: HTMLButtonElement | null = null;
    options.forEach((opt, i) => {
      const btn = document.createElement("button");
      btn.textContent = opt;
      btn.style.cssText = `
        padding:5px 10px; border:2px solid #333; border-radius:4px;
        background:#11192a; color:#aaa; font-family:monospace; font-size:11px;
        cursor:pointer; white-space:nowrap;
      `;
      const select = () => {
        if (activeBtn) {
          activeBtn.style.borderColor = "#333";
          activeBtn.style.background  = "#11192a";
          activeBtn.style.color       = "#aaa";
        }
        btn.style.borderColor = "#f5d060";
        btn.style.background  = "#2a2000";
        btn.style.color       = "#f5d060";
        activeBtn = btn;
        onChange(opt, i);
      };
      if (opt === initial) { select(); }
      btn.addEventListener("click", select);
      row.appendChild(btn);
    });
    group.appendChild(row);
    return group;
  }

  private _bonusSummary(def: CivBonus): string {
    const lines: string[] = [];
    if (def.gatherFoodMult !== 1) lines.push(`+yiyecek toplama ×${def.gatherFoodMult}`);
    if (def.gatherWoodMult !== 1) lines.push(`+odun toplama ×${def.gatherWoodMult}`);
    if (def.gatherGoldMult !== 1) lines.push(`+altın toplama ×${def.gatherGoldMult}`);
    if (def.cavalryHpMult  !== 1) lines.push(`süvari HP ×${def.cavalryHpMult}`);
    if (def.cavalrySpeedMult !== 1) lines.push(`süvari hız ×${def.cavalrySpeedMult}`);
    if (def.infantryAttackMult !== 1) lines.push(`piyade saldırı ×${def.infantryAttackMult}`);
    if (def.archerRangeBonus !== 0) lines.push(`okçu menzil +${def.archerRangeBonus}`);
    if (def.archerAttackMult !== 1) lines.push(`okçu saldırı ×${def.archerAttackMult}`);
    if (def.buildingHpMult !== 1) lines.push(`bina HP ×${def.buildingHpMult}`);
    if (def.healRateMult !== 1) lines.push(`iyileşme ×${def.healRateMult}`);
    if (def.unitTrainTimeMult !== 1) lines.push(`eğitim süresi ×${def.unitTrainTimeMult}`);
    if (def.teamGatherFoodBonus !== 0) lines.push(`takım yiyecek +${def.teamGatherFoodBonus * 100}%`);
    if (lines.length === 0) lines.push("dengeli başlangıç");
    return `<div style="font-size:10px;color:#888;margin-top:4px">${lines.slice(0, 3).join(" · ")}</div>`;
  }

  private _refreshCivButtonsIn(root: HTMLElement): void {
    const btns = root.querySelectorAll<HTMLButtonElement>("button[data-civ]");
    btns.forEach(btn => {
      const selected = Number(btn.dataset.civ) === this._civSel;
      btn.style.borderColor = selected ? "#f5d060" : "#333";
      btn.style.background  = selected ? "#2a2000" : "#11192a";
      btn.style.color       = selected ? "#f5d060" : "#c8c8e0";
    });
  }

  private _refreshMapButtonsIn(root: HTMLElement): void {
    const btns = root.querySelectorAll<HTMLButtonElement>("button[data-map]");
    btns.forEach(btn => {
      const selected = Number(btn.dataset.map) === this._mapSel;
      btn.style.borderColor = selected ? "#6af" : "#333";
      btn.style.background  = selected ? "#0a1a2a" : "#11192a";
      btn.style.color       = selected ? "#6af" : "#aaa";
    });
  }
}
