/**
 * PreGameScreen — CivSelectScreen.cs port (HTML overlay).
 * Player picks a civilization and map type before the game starts.
 * Fires onStart(playerCiv, enemyCiv, mapType) when "OYNA" is clicked.
 */
import { Civilization, CIVILIZATION_DEFS, PLAYABLE_CIVS, CivBonus } from "../core/CivilizationDefs";
import { MapType } from "../world/MapGenerator";

export class PreGameScreen {
  private readonly _el: HTMLDivElement;

  onStart: ((player: Civilization, enemy: Civilization, map: MapType) => void) | null = null;

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

  private _civSel   = Civilization.None;
  private _mapSel   = MapType.Arabia;

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
        this._refreshCivButtons();
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
    mapRow.style.cssText = "display:flex;gap:8px;margin-bottom:28px;flex-wrap:wrap;justify-content:center;";

    const MAP_NAMES: Record<MapType, string> = {
      [MapType.Arabia]:    "Arabistan",
      [MapType.Arena]:     "Arena",
      [MapType.BlackForest]:"Kara Orman",
      [MapType.Islands]:   "Adalar",
      [MapType.Nomad]:     "Göçebe",
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
        this._refreshMapButtons();
      });
      mapRow.appendChild(btn);
    }
    this._el.appendChild(mapRow);

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
      const enemyCivs = PLAYABLE_CIVS.filter(c => c !== this._civSel);
      const enemy = enemyCivs[Math.floor(Math.random() * enemyCivs.length)];
      this._el.remove();
      this.onStart?.(this._civSel, enemy, this._mapSel);
    });
    this._el.appendChild(startBtn);

    this._refreshMapButtonsIn(mapRow);
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

  private _refreshCivButtons(): void {
    this._refreshCivButtonsIn(this._el);
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

  private _refreshMapButtons(): void {
    this._refreshMapButtonsIn(this._el);
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
