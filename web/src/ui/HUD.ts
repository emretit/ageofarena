/**
 * HUD — HTML overlay port of Unity's HUD.cs.
 * Resource bar + selected entity info panel + training card.
 */
import { Age, BuildingType, ResourceKind, UnitType } from "../core/GameTypes";
import { play, SoundId } from "../game/AudioManager";
import { ResourceManager } from "../core/ResourceManager";
import { getUnitRow } from "../core/UnitRegistry";
import { TRAINABLE } from "../game/TrainingQueue";
import type { TrainingQueue } from "../game/TrainingQueue";
import type { Unit } from "../game/Unit";
import { Building, DEFS } from "../game/Building";
import { AGE_NAMES, AgeSystem } from "../game/AgeSystem";
import { BUILDING_TECHS, TECH_DEFS, type ResearchSystem } from "../game/ResearchSystem";
import type { MarketSystem } from "../game/MarketSystem";
import type { GarrisonSystem } from "../game/GarrisonSystem";

/** Minimum age required to construct each building type. */
const BUILDING_MIN_AGE: Partial<Record<BuildingType, Age>> = {
  [BuildingType.Barracks]:     Age.Feudal,
  [BuildingType.ArcheryRange]: Age.Feudal,
  [BuildingType.Stable]:       Age.Feudal,
  [BuildingType.Market]:       Age.Feudal,
  [BuildingType.Blacksmith]:   Age.Feudal,
  [BuildingType.LumberCamp]:   Age.Feudal,
  [BuildingType.MiningCamp]:   Age.Feudal,
  [BuildingType.Mill]:         Age.Feudal,
  [BuildingType.Monastery]:    Age.Castle,
  [BuildingType.University]:   Age.Castle,
  [BuildingType.SiegeWorkshop]:Age.Castle,
  [BuildingType.WatchTower]:   Age.Feudal,
  [BuildingType.Castle]:       Age.Castle,
};

/** Buildings the player can construct (ordered by tech progression). */
export const BUILDABLE: BuildingType[] = [
  BuildingType.House,
  BuildingType.Barracks,
  BuildingType.ArcheryRange,
  BuildingType.Stable,
  BuildingType.LumberCamp,
  BuildingType.MiningCamp,
  BuildingType.Mill,
  BuildingType.Market,
  BuildingType.Blacksmith,
  BuildingType.WatchTower,
  BuildingType.Monastery,
  BuildingType.University,
  BuildingType.SiegeWorkshop,
  BuildingType.Castle,
];

export type BuildCallback = (type: BuildingType) => void;

const ICONS: Record<ResourceKind, string> = {
  [ResourceKind.Food]:  "🌾",
  [ResourceKind.Wood]:  "🪵",
  [ResourceKind.Gold]:  "🪙",
  [ResourceKind.Stone]: "🪨",
};

const UNIT_NAMES: Record<UnitType, string> = {
  [UnitType.Villager]:   "Villager",
  [UnitType.Militia]:    "Militia",
  [UnitType.Archer]:     "Archer",
  [UnitType.Cavalry]:    "Cavalry",
  [UnitType.Spearman]:   "Spearman",
  [UnitType.Scout]:      "Scout",
  [UnitType.Trebuchet]:  "Trebuchet",
  [UnitType.Longbowman]: "Longbowman",
  [UnitType.Skirmisher]: "Skirmisher",
  [UnitType.Mangonel]:   "Mangonel",
  [UnitType.Ram]:        "Ram",
  [UnitType.Monk]:       "Monk",
  [UnitType.TradeCart]:  "Trade Cart",
};

export class HUD {
  private readonly root: HTMLElement;
  private readonly resBar: HTMLElement;
  private readonly infoPanel: HTMLElement;
  private readonly counters: Record<ResourceKind, HTMLElement>;
  private readonly popCell: HTMLElement;

  constructor(container: HTMLElement, rm: ResourceManager) {
    this.root = document.createElement("div");
    Object.assign(this.root.style, {
      position: "absolute", top: "0", left: "0",
      width: "100%", height: "100%",
      pointerEvents: "none",
      fontFamily: "monospace", userSelect: "none",
    });

    // ── Resource bar ────────────────────────────────────────────────────────
    this.resBar = document.createElement("div");
    Object.assign(this.resBar.style, {
      position: "absolute", top: "8px", left: "50%",
      transform: "translateX(-50%)",
      display: "flex", gap: "16px",
      background: "rgba(0,0,0,0.6)",
      padding: "6px 14px", borderRadius: "6px",
      color: "#fff", fontSize: "15px",
    });

    this.counters = {} as Record<ResourceKind, HTMLElement>;
    for (const kind of [ResourceKind.Food, ResourceKind.Wood, ResourceKind.Gold, ResourceKind.Stone]) {
      const cell = document.createElement("span");
      this.resBar.appendChild(cell);
      this.counters[kind] = cell;
    }

    this.popCell = document.createElement("span");
    Object.assign(this.popCell.style, { color: "#adf" });
    this.resBar.appendChild(this.popCell);

    // ── Info panel ──────────────────────────────────────────────────────────
    this.infoPanel = document.createElement("div");
    Object.assign(this.infoPanel.style, {
      position: "absolute", bottom: "10px", left: "50%",
      transform: "translateX(-50%)",
      minWidth: "260px",
      background: "rgba(0,0,0,0.75)",
      color: "#eee", fontSize: "13px",
      padding: "10px 16px", borderRadius: "6px",
      display: "none",
      pointerEvents: "auto",
    });

    this.root.appendChild(this.resBar);
    this.root.appendChild(this.infoPanel);
    container.appendChild(this.root);

    rm.onChange = () => this._updateRes(rm);
    this._updateRes(rm);
  }

  private _updateRes(rm: ResourceManager) {
    for (const kind of [ResourceKind.Food, ResourceKind.Wood, ResourceKind.Gold, ResourceKind.Stone]) {
      this.counters[kind].textContent = `${ICONS[kind]} ${rm.get(kind)}`;
    }
    this.popCell.textContent = `Pop ${rm.pop}/${rm.popCap}`;
  }

  showUnit(u: Unit, rm?: ResourceManager, onBuild?: BuildCallback) {
    const typeName = UNIT_NAMES[u.unitType] ?? "Unit";
    let html =
      `<b>${typeName}</b> (Team ${u.teamId + 1})<br>` +
      `HP: ${u.hp}/${u.maxHp}&nbsp;&nbsp;` +
      `Atk: ${u.baseAtk}&nbsp;&nbsp;` +
      (u.gathers && u.carryAmount > 0
        ? `Carry: ${u.carryAmount}/10`
        : `Spd: ${u.moveSpeed.toFixed(1)}`);

    // Build panel for player villagers
    if (u.teamId === 0 && u.gathers && onBuild && rm) {
      html += `<div style="margin-top:8px;border-top:1px solid #555;padding-top:6px;font-size:11px;color:#aaa">BUILD</div>`;
      html += `<div style="display:flex;flex-wrap:wrap;gap:4px;margin-top:4px">`;
      for (const type of BUILDABLE) {
        const def = DEFS[type];
        const minAge = BUILDING_MIN_AGE[type] ?? Age.Dark;
        const ageOk = rm.age >= minAge;
        const cost =
          (def.costWood  > 0 ? `\u{1FAB5}${def.costWood} ` : "") +
          (def.costStone > 0 ? `\u{1FAA8}${def.costStone} ` : "") +
          (def.costGold  > 0 ? `\u{1FA99}${def.costGold}` : "");
        const canAfford = ageOk && rm.wood >= def.costWood && rm.stone >= def.costStone && rm.gold >= def.costGold;
        const bgColor = canAfford ? "#35527a" : "#2a2a2a";
        const fgColor = !ageOk ? "#444" : canAfford ? "#dde" : "#555";
        const cursor  = canAfford ? "pointer" : "not-allowed";
        const label   = !ageOk ? `${def.display}<br><span style="font-size:10px;opacity:0.6">${AGE_NAMES[minAge]}</span>` : `${def.display}<br><span style="font-size:10px;opacity:0.75">${cost.trim() || "free"}</span>`;
        html += `<button data-build="${type}" title="${cost.trim()}" style="cursor:${cursor};padding:3px 7px;border-radius:4px;border:none;font-family:monospace;font-size:11px;background:${bgColor};color:${fgColor}">`;
        html += label;
        html += `</button>`;
      }
      html += `</div>`;
    }

    this.infoPanel.innerHTML = html;
    this.infoPanel.style.display = "block";

    if (onBuild) {
      this.infoPanel.querySelectorAll<HTMLButtonElement>("[data-build]").forEach(btn => {
        btn.addEventListener("click", () => {
          onBuild(parseInt(btn.dataset.build ?? "0") as BuildingType);
        });
      });
    }
  }

  showBuilding(b: Building, training?: TrainingQueue, rm?: ResourceManager, ageSystem?: AgeSystem, research?: ResearchSystem, market?: MarketSystem, garrison?: GarrisonSystem) {
    // Header
    const ageName = rm ? ` — ${AGE_NAMES[rm.age]}` : "";
    const header =
      `<b>${b.def.display}</b> (Team ${b.teamId + 1})${b.buildingType === BuildingType.TownCenter ? ageName : ""}<br>` +
      `HP: ${Math.ceil(b.hp)}/${b.maxHp}`;

    // Age-up card — only for player TC
    let ageCard = "";
    if (b.teamId === 0 && b.buildingType === BuildingType.TownCenter && rm && ageSystem) {
      const def = ageSystem.nextAgeDef(rm);
      const prog = ageSystem.progress();
      if (def) {
        const isResearching = prog >= 0;
        const canAfford = rm.canAfford(def.food, 0, def.gold);
        const cost = `🌾${def.food}${def.gold > 0 ? ` 🪙${def.gold}` : ""}`;
        ageCard += `<div style="margin-top:8px;border-top:1px solid #555;padding-top:6px">`;
        if (isResearching) {
          const pct = Math.round(prog * 100);
          ageCard += `<div style="font-size:12px;color:#fda">Advancing to ${def.label}... ${pct}%</div>`;
          ageCard += `<div style="background:#333;border-radius:3px;height:5px;margin:4px 0">`;
          ageCard += `<div style="background:#f90;height:100%;width:${pct}%;border-radius:3px"></div></div>`;
        } else {
          const btnStyle = canAfford
            ? "cursor:pointer;background:#5a3a0a;color:#fda;border:none;border-radius:4px;padding:4px 10px;font-family:monospace;font-size:12px;"
            : "cursor:not-allowed;background:#2a2a2a;color:#555;border:none;border-radius:4px;padding:4px 10px;font-family:monospace;font-size:12px;";
          ageCard += `<button id="age-up-btn" style="${btnStyle}">`;
          ageCard += `Advance to ${def.label}<br><span style="font-size:10px;opacity:0.8">${cost} / ${def.time}s</span>`;
          ageCard += `</button>`;
        }
        ageCard += `</div>`;
      } else {
        ageCard += `<div style="margin-top:6px;font-size:11px;color:#fa0">Imperial Age</div>`;
      }
    }

    // Training card — only for player-owned buildings with trainable units
    let card = "";
    if (b.teamId === 0 && training && rm) {
      const trainable = TRAINABLE[b.buildingType];
      if (trainable?.length) {
        const qLen = training.queueLength(b);
        const prog = training.progress(b);
        card += `<div style="margin-top:8px;border-top:1px solid #555;padding-top:8px">`;
        if (qLen > 0) {
          const pct = Math.round(prog * 100);
          card += `<div style="margin-bottom:6px;font-size:12px;color:#cca">`;
          card += `Training... ${pct}%&nbsp;[${qLen}/5]</div>`;
          card += `<div style="background:#333;border-radius:3px;height:6px;margin-bottom:8px">`;
          card += `<div style="background:#4c4;height:100%;width:${pct}%;border-radius:3px"></div></div>`;
        }
        card += `<div style="display:flex;flex-wrap:wrap;gap:5px">`;
        for (const type of trainable) {
          const row = getUnitRow(type);
          const canAfford = rm.canAfford(row.trainFood, row.trainWood, row.trainGold);
          const cost =
            (row.trainFood  > 0 ? `🌾${row.trainFood} ` : "") +
            (row.trainWood  > 0 ? `🪵${row.trainWood} ` : "") +
            (row.trainGold  > 0 ? `🪙${row.trainGold}` : "");
          const btnStyle = [
            "cursor:pointer;padding:4px 8px;border-radius:4px;border:none;",
            "font-family:monospace;font-size:12px;",
            canAfford
              ? "background:#2a5;color:#fff;"
              : "background:#444;color:#888;cursor:not-allowed;",
          ].join("");
          card += `<button data-train="${type}" style="${btnStyle}" title="${cost.trim()}">`;
          card += `${UNIT_NAMES[type]}<br><span style="font-size:10px;opacity:0.8">${cost.trim()}</span>`;
          card += `</button>`;
        }
        card += `</div></div>`;
      }
    }

    // ── Research card ──────────────────────────────────────────────────────
    let resCard = "";
    if (b.teamId === 0 && research && rm) {
      const avail = research.available(b, rm);
      const active = research.active(b);
      if (avail.length > 0 || active) {
        resCard += `<div style="margin-top:8px;border-top:1px solid #555;padding-top:6px;font-size:11px;color:#aaa">RESEARCH</div>`;
        if (active) {
          const pct = Math.round((1 - active.timer / active.total) * 100);
          const def = TECH_DEFS[active.tech];
          resCard += `<div style="font-size:12px;color:#acf">${def.label}... ${pct}%</div>`;
          resCard += `<div style="background:#333;border-radius:3px;height:5px;margin:4px 0"><div style="background:#4af;height:100%;width:${pct}%;border-radius:3px"></div></div>`;
        }
        resCard += `<div style="display:flex;flex-wrap:wrap;gap:4px;margin-top:4px">`;
        for (const tech of avail) {
          const def = TECH_DEFS[tech];
          const costStr =
            (def.food > 0 ? `🌾${def.food} ` : "") +
            (def.wood > 0 ? `🪵${def.wood} ` : "") +
            (def.gold > 0 ? `🪙${def.gold}` : "");
          const canAfford = rm.canAfford(def.food, def.wood, def.gold);
          const bg  = canAfford ? "#2a4a6a" : "#2a2a2a";
          const col = canAfford ? "#adf" : "#555";
          const cur = canAfford ? "pointer" : "not-allowed";
          resCard += `<button data-research="${tech}" style="cursor:${cur};padding:3px 7px;border-radius:4px;border:none;font-family:monospace;font-size:11px;background:${bg};color:${col}">`;
          resCard += `${def.label}<br><span style="font-size:10px;opacity:0.75">${costStr.trim() || "free"}</span>`;
          resCard += `</button>`;
        }
        resCard += `</div>`;
      }
    }

    // ── Market card ───────────────────────────────────────────────────────
    let mktCard = "";
    if (b.teamId === 0 && b.buildingType === BuildingType.Market && market && rm) {
      mktCard += `<div style="margin-top:8px;border-top:1px solid #555;padding-top:6px;font-size:11px;color:#aaa">TRADE (batch 100)</div>`;
      mktCard += `<div style="display:flex;flex-wrap:wrap;gap:4px;margin-top:4px">`;
      for (const kind of [ResourceKind.Food, ResourceKind.Wood, ResourceKind.Stone]) {
        const icon   = ICONS[kind];
        const sell   = market.sellGold(kind);
        const cost   = market.buyCost(kind);
        const hasSell= rm.get(kind) >= 100;
        const hasBuy = rm.gold >= cost;
        const sellBg = hasSell ? "#5a3a0a" : "#2a2a2a";
        const buyBg  = hasBuy  ? "#1a4a1a" : "#2a2a2a";
        const sellC  = hasSell ? "#fda" : "#555";
        const buyC   = hasBuy  ? "#adf" : "#555";
        mktCard += `<button data-sell="${kind}" style="cursor:${hasSell?"pointer":"not-allowed"};padding:3px 7px;border-radius:4px;border:none;font-family:monospace;font-size:11px;background:${sellBg};color:${sellC}">`;
        mktCard += `Sell ${icon}<br><span style="font-size:10px">+${sell}🪙</span></button>`;
        mktCard += `<button data-buy="${kind}" style="cursor:${hasBuy?"pointer":"not-allowed"};padding:3px 7px;border-radius:4px;border:none;font-family:monospace;font-size:11px;background:${buyBg};color:${buyC}">`;
        mktCard += `Buy ${icon}<br><span style="font-size:10px">-${cost}🪙</span></button>`;
      }
      mktCard += `</div>`;
    }

    // Garrison card — TC and Castle show garrison count + Ungarrison button
    let garrisonCard = "";
    if (b.teamId === 0 && garrison && (garrison.canGarrison(b) || garrison.garrisonCount(b) > 0)) {
      const count = garrison.garrisonCount(b);
      garrisonCard += `<div style="margin-top:8px;border-top:1px solid #555;padding-top:6px;font-size:11px;color:#aaa">`;
      garrisonCard += `Garrison: ${count} unit${count !== 1 ? "s" : ""}`;
      if (count > 0) {
        garrisonCard += ` <button id="ungarrison-btn" style="margin-left:8px;cursor:pointer;padding:2px 6px;border-radius:4px;border:none;font-family:monospace;font-size:11px;background:#4a2a0a;color:#fca">Eject All</button>`;
        garrisonCard += ` <button id="ungarrison-one-btn" style="margin-left:4px;cursor:pointer;padding:2px 6px;border-radius:4px;border:none;font-family:monospace;font-size:11px;background:#2a3a4a;color:#acf">Eject 1</button>`;
      }
      garrisonCard += `</div>`;
    }

    // Rally point hint
    const rallyHint = b.teamId === 0 && (TRAINABLE[b.buildingType]?.length || BUILDING_TECHS[b.buildingType]?.length)
      ? `<div style="font-size:10px;color:#666;margin-top:6px">Right-click to set rally point${b.rallyPoint ? " (set)" : ""}</div>`
      : "";

    this.infoPanel.innerHTML = header + ageCard + card + resCard + mktCard + garrisonCard + rallyHint;
    this.infoPanel.style.display = "block";

    // Wire up age-up button
    if (ageSystem && rm) {
      const ageBtn = this.infoPanel.querySelector<HTMLButtonElement>("#age-up-btn");
      ageBtn?.addEventListener("click", () => ageSystem.startAgeUp(rm));
    }

    // Wire up training buttons
    if (training && rm) {
      this.infoPanel.querySelectorAll<HTMLButtonElement>("[data-train]").forEach(btn => {
        btn.addEventListener("click", () => {
          const type = parseInt(btn.dataset.train ?? "0") as UnitType;
          if (training.train(b, type, rm)) play(SoundId.TrainStart);
        });
      });
    }

    // Wire up research buttons
    if (research && rm) {
      this.infoPanel.querySelectorAll<HTMLButtonElement>("[data-research]").forEach(btn => {
        btn.addEventListener("click", () => {
          const tech = btn.dataset.research!;
          if (research.start(b, tech as import("../game/ResearchSystem").TechId, rm)) play(SoundId.ButtonClick);
        });
      });
    }

    // Wire up market buttons
    if (market && rm) {
      this.infoPanel.querySelectorAll<HTMLButtonElement>("[data-sell]").forEach(btn => {
        btn.addEventListener("click", () => {
          market.sell(rm, parseInt(btn.dataset.sell!) as ResourceKind);
        });
      });
      this.infoPanel.querySelectorAll<HTMLButtonElement>("[data-buy]").forEach(btn => {
        btn.addEventListener("click", () => {
          market.buy(rm, parseInt(btn.dataset.buy!) as ResourceKind);
        });
      });
    }

    // Wire up garrison buttons
    if (garrison) {
      this.infoPanel.querySelector<HTMLButtonElement>("#ungarrison-btn")
        ?.addEventListener("click", () => garrison.ungarrisonAll(b));
      this.infoPanel.querySelector<HTMLButtonElement>("#ungarrison-one-btn")
        ?.addEventListener("click", () => garrison.ungarrisonOne(b));
    }
  }

  /** Show summary panel when multiple units are selected (drag-box). */
  showMultiUnit(units: Unit[]) {
    if (units.length === 0) { this.clearInfo(); return; }

    // Group by type
    const counts = new Map<UnitType, number>();
    let totalHp = 0, totalMaxHp = 0;
    for (const u of units) {
      counts.set(u.unitType, (counts.get(u.unitType) ?? 0) + 1);
      totalHp    += u.hp;
      totalMaxHp += u.maxHp;
    }

    const breakdown = Array.from(counts.entries())
      .sort((a, b) => b[1] - a[1])
      .map(([type, n]) => `${n}× ${UNIT_NAMES[type] ?? "Unit"}`)
      .join(", ");

    const hpPct = totalMaxHp > 0 ? Math.round((totalHp / totalMaxHp) * 100) : 0;
    const hpColor = hpPct > 60 ? "#4c4" : hpPct > 30 ? "#fa0" : "#e44";

    this.infoPanel.innerHTML =
      `<b>${units.length} units selected</b><br>` +
      `<span style="font-size:11px;color:#aaa">${breakdown}</span><br>` +
      `<span style="font-size:11px">HP: <span style="color:${hpColor}">${hpPct}%</span></span>`;
    this.infoPanel.style.display = "block";
  }

  clearInfo() {
    this.infoPanel.style.display = "none";
    this.infoPanel.innerHTML = "";
  }

  showVictory(winner: number) {
    const banner = document.createElement("div");
    Object.assign(banner.style, {
      position: "absolute", top: "40%", left: "50%",
      transform: "translate(-50%,-50%)",
      background: winner === 0 ? "rgba(0,100,0,0.85)" : "rgba(120,0,0,0.85)",
      color: "#fff", fontSize: "32px", padding: "24px 40px",
      borderRadius: "10px", textAlign: "center", pointerEvents: "none",
    });
    banner.textContent = winner === 0 ? "Victory!" : "Defeat";
    this.root.appendChild(banner);
  }
}
