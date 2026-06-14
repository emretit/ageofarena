/**
 * HUD — HTML overlay port of Unity's HUD.cs.
 * Resource bar + selected entity info panel + training card.
 */
import { Age, BuildingType, ResourceKind, UnitType } from "../core/GameTypes";
import { play, SoundId } from "../game/AudioManager";
import { ResourceManager } from "../core/ResourceManager";
import { getUnitRow } from "../core/UnitRegistry";
import { TRAINABLE, trainableFor } from "../game/TrainingQueue";
import type { TrainingQueue } from "../game/TrainingQueue";
import type { Unit } from "../game/Unit";
import { Building, DEFS } from "../game/Building";
import { AGE_NAMES, AgeSystem } from "../game/AgeSystem";
import { BUILDING_TECHS, TECH_DEFS, type ResearchSystem, type TechId } from "../game/ResearchSystem";
import type { MarketSystem } from "../game/MarketSystem";
import type { GarrisonSystem } from "../game/GarrisonSystem";
import type { CommandIssuer } from "../sim/CommandBus";
import { BottomBar } from "./BottomBar";
import { versionString } from "../../../shared/Versions";

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
  [BuildingType.Gate]:         Age.Feudal,
  [BuildingType.Outpost]:      Age.Feudal,
  [BuildingType.BombardTower]: Age.Imperial,
  [BuildingType.FishTrap]:     Age.Feudal,
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
  BuildingType.Outpost,
  BuildingType.Gate,
  BuildingType.Monastery,
  BuildingType.University,
  BuildingType.SiegeWorkshop,
  BuildingType.BombardTower,
  BuildingType.Castle,
  BuildingType.FishTrap,
];

export type BuildCallback = (type: BuildingType) => void;

/**
 * One AoE2-style command button: a large glyph over a small sub-line (cost or label).
 * Disabled buttons render greyed and ignore clicks (the wiring's querySelectorAll still
 * matches them, but a disabled <button> fires no click event).
 */
function iconButton(
  attr: string, val: string | number, icon: string, sub: string,
  o: { bg: string; fg: string; enabled: boolean; title: string },
): string {
  return (
    `<button ${attr}="${val}" title="${o.title}"${o.enabled ? "" : " disabled"} style="` +
    `width:58px;height:56px;display:flex;flex-direction:column;align-items:center;justify-content:center;` +
    `gap:1px;padding:2px;border-radius:5px;border:1px solid rgba(0,0,0,0.35);` +
    `cursor:${o.enabled ? "pointer" : "not-allowed"};background:${o.bg};color:${o.fg};font-family:monospace;">` +
    `<span style="font-size:22px;line-height:1">${icon}</span>` +
    `<span style="font-size:8.5px;line-height:1.05;opacity:0.92;text-align:center;word-break:break-word;max-height:18px;overflow:hidden">${sub}</span>` +
    `</button>`
  );
}

/** Section header strip inside the command card. */
function cmdHeader(text: string): string {
  return `<div style="font-size:11px;color:#caa48a;letter-spacing:1px;margin:2px 0 5px">${text}</div>`;
}

/** Flex grid wrapper that lays icon buttons out in AoE2-style rows. */
function cmdGrid(buttons: string): string {
  return `<div style="display:flex;flex-wrap:wrap;gap:5px">${buttons}</div>`;
}

const ICONS: Record<ResourceKind, string> = {
  [ResourceKind.Food]:  "🌾",
  [ResourceKind.Wood]:  "🪵",
  [ResourceKind.Gold]:  "🪙",
  [ResourceKind.Stone]: "🪨",
};

/** Emoji glyphs for command buttons — mirrors Unity's CommandIconFactory silhouettes. */
const BUILDING_ICONS: Partial<Record<BuildingType, string>> = {
  [BuildingType.TownCenter]:   "🏛️",
  [BuildingType.House]:        "🏠",
  [BuildingType.Barracks]:     "⚔️",
  [BuildingType.ArcheryRange]: "🏹",
  [BuildingType.Stable]:       "🐎",
  [BuildingType.Farm]:         "🌽",
  [BuildingType.LumberCamp]:   "🪵",
  [BuildingType.MiningCamp]:   "⛏️",
  [BuildingType.Mill]:         "🌾",
  [BuildingType.Market]:       "💰",
  [BuildingType.Castle]:       "🏰",
  [BuildingType.Wall]:         "🧱",
  [BuildingType.Wonder]:       "🏯",
  [BuildingType.Blacksmith]:   "⚒️",
  [BuildingType.Monastery]:    "⛪",
  [BuildingType.University]:   "🎓",
  [BuildingType.SiegeWorkshop]:"🛠️",
  [BuildingType.Dock]:         "⚓",
  [BuildingType.WatchTower]:   "🗼",
  [BuildingType.Gate]:         "🚪",
  [BuildingType.Outpost]:      "🔭",
  [BuildingType.BombardTower]: "💣",
  [BuildingType.FishTrap]:     "🎏",
};

const UNIT_ICONS: Partial<Record<UnitType, string>> = {
  [UnitType.Villager]:      "🧑‍🌾",
  [UnitType.Militia]:       "⚔️",
  [UnitType.Archer]:        "🏹",
  [UnitType.Cavalry]:       "🐎",
  [UnitType.Spearman]:      "🔱",
  [UnitType.Scout]:         "🐴",
  [UnitType.Trebuchet]:     "🏗️",
  [UnitType.Longbowman]:    "🏹",
  [UnitType.Skirmisher]:    "🗡️",
  [UnitType.Mangonel]:      "🪨",
  [UnitType.Ram]:           "🐏",
  [UnitType.Monk]:          "✝️",
  [UnitType.TradeCart]:     "🛒",
  [UnitType.FishingShip]:   "🎣",
  [UnitType.Galley]:        "⛵",
  [UnitType.Camel]:         "🐫",
  [UnitType.CavalryArcher]: "🏹",
  [UnitType.Medic]:         "⚕️",
  [UnitType.Scorpion]:      "🦂",
  [UnitType.FireShip]:      "🔥",
  [UnitType.DemoShip]:      "💥",
  [UnitType.King]:          "👑",
  [UnitType.TeutonicKnight]:"🛡️",
  [UnitType.WarElephant]:   "🐘",
  [UnitType.Mangudai]:      "🏹",
  [UnitType.Samurai]:       "🗡️",
  [UnitType.ThrowingAxeman]:"🪓",
  [UnitType.Cataphract]:    "🐎",
  [UnitType.Berserk]:       "⚔️",
  [UnitType.Mameluke]:      "🐫",
  [UnitType.WoadRaider]:    "🏃",
  [UnitType.ChuKoNu]:       "🎯",
  [UnitType.Huskarl]:       "🛡️",
  [UnitType.Janissary]:     "🔫",
  [UnitType.Eagle]:         "🦅",
  [UnitType.EliteEagle]:    "🦅",
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
  [UnitType.FishingShip]:"Fishing Ship",
  [UnitType.Galley]:     "Galley",
  [UnitType.Camel]:         "Camel",
  [UnitType.CavalryArcher]: "Cavalry Archer",
  [UnitType.Medic]:         "Medic",
  [UnitType.Scorpion]:      "Scorpion",
  [UnitType.FireShip]:      "Fire Ship",
  [UnitType.DemoShip]:      "Demolition Ship",
  [UnitType.King]:          "King",
  [UnitType.TeutonicKnight]:"Teutonic Knight",
  [UnitType.WarElephant]:   "War Elephant",
  [UnitType.Mangudai]:      "Mangudai",
  [UnitType.Samurai]:       "Samurai",
  [UnitType.ThrowingAxeman]:"Throwing Axeman",
  [UnitType.Cataphract]:    "Cataphract",
  [UnitType.Berserk]:       "Berserk",
  [UnitType.Mameluke]:      "Mameluke",
  [UnitType.WoadRaider]:    "Woad Raider",
  [UnitType.ChuKoNu]:       "Chu Ko Nu",
  [UnitType.Huskarl]:       "Huskarl",
  [UnitType.Janissary]:     "Janissary",
  [UnitType.Eagle]:         "Eagle Warrior",
  [UnitType.EliteEagle]:    "Elite Eagle Warrior",
};

export class HUD {
  private readonly root: HTMLElement;
  private readonly resBar: HTMLElement;
  /** Floating panel — only created when no BottomBar is provided (legacy / standalone). */
  private readonly infoPanel: HTMLElement | null;
  /** Selected-entity stats go here (BottomBar.infoSlot when docked, else infoPanel). */
  private readonly infoTarget: HTMLElement;
  /** Action buttons go here (BottomBar.commandSlot when docked, else infoPanel). */
  private readonly cmdTarget: HTMLElement;
  private readonly _docked: boolean;
  private readonly counters: Record<ResourceKind, HTMLElement>;
  private readonly popCell: HTMLElement;
  private _formationBadge!: HTMLDivElement;
  private _bus?: CommandIssuer;
  /** Local player's team — panels/victory key off this (MP: = myTeam). */
  localTeam = 0;

  /** Provide a CommandIssuer (CommandBus or LockstepClient) after construction. */
  setBus(bus: CommandIssuer): void { this._bus = bus; }

  /** Update the formation badge text. Pass null to hide it. */
  setFormation(name: string | null): void {
    if (!name) { this._formationBadge.style.display = "none"; return; }
    this._formationBadge.textContent = `Formasyon: ${name}  [F]`;
    this._formationBadge.style.display = "block";
  }

  constructor(container: HTMLElement, rm: ResourceManager, bar?: BottomBar) {
    this._docked = !!bar;
    this.root = document.createElement("div");
    Object.assign(this.root.style, {
      position: "absolute", top: "0", left: "0",
      width: "100%", height: "100%",
      pointerEvents: "none",
      fontFamily: "monospace", userSelect: "none",
    });

    // ── Version badge (bottom-right corner) ────────────────────────────────
    const versionBadge = document.createElement("div");
    Object.assign(versionBadge.style, {
      position: "absolute", bottom: "4px", right: "6px",
      color: "rgba(255,255,255,0.35)", fontSize: "10px", pointerEvents: "none",
    });
    versionBadge.textContent = `v${versionString()}`;
    this.root.appendChild(versionBadge);

    // ── Formation badge (just above the bottom bar, shown while units selected) ──
    this._formationBadge = document.createElement("div");
    Object.assign(this._formationBadge.style, {
      position: "absolute", bottom: this._docked ? `${BottomBar.HEIGHT + 6}px` : "6px",
      left: "50%", transform: "translateX(-50%)",
      color: "rgba(255,255,255,0.7)", fontSize: "11px", pointerEvents: "none",
      background: "rgba(0,0,0,0.35)", padding: "2px 8px", borderRadius: "3px",
      display: "none",
    });
    this.root.appendChild(this._formationBadge);

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
    // Docked: stats → bar.infoSlot, buttons → bar.commandSlot (AoE2-style bottom bar).
    // Standalone: a single floating panel holds both (legacy fallback).
    if (bar) {
      this.infoPanel  = null;
      this.infoTarget = bar.infoSlot;
      this.cmdTarget  = bar.commandSlot;
    } else {
      const panel = document.createElement("div");
      Object.assign(panel.style, {
        position: "absolute", bottom: "10px", left: "50%",
        transform: "translateX(-50%)",
        minWidth: "260px",
        background: "rgba(0,0,0,0.75)",
        color: "#eee", fontSize: "13px",
        padding: "10px 16px", borderRadius: "6px",
        display: "none",
        pointerEvents: "auto",
      });
      this.infoPanel  = panel;
      this.infoTarget = panel;
      this.cmdTarget  = panel;
    }

    this.root.appendChild(this.resBar);
    if (this.infoPanel) this.root.appendChild(this.infoPanel);
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

  /** Render stats into the info zone and buttons into the command zone (or both in one panel). */
  private _render(stats: string, cmds: string): void {
    if (this._docked) {
      this.infoTarget.innerHTML = stats;
      this.cmdTarget.innerHTML  = cmds;
    } else {
      this.infoTarget.innerHTML = stats + cmds;
      this.infoPanel!.style.display = "block";
    }
  }

  showUnit(u: Unit, rm?: ResourceManager, onBuild?: BuildCallback) {
    const typeName = UNIT_NAMES[u.unitType] ?? "Unit";
    const stats =
      `<b>${typeName}</b> (Team ${u.teamId + 1})<br>` +
      `HP: ${u.hp}/${u.maxHp}&nbsp;&nbsp;` +
      `Atk: ${u.baseAtk}&nbsp;&nbsp;` +
      (u.gathers && u.carryAmount > 0
        ? `Carry: ${u.carryAmount}/10`
        : `Spd: ${u.moveSpeed.toFixed(1)}`);

    // Build panel for player villagers — all building types always shown (locked ones
    // greyed with their unlock age), AoE2-style icon buttons.
    let cmds = "";
    if (u.teamId === this.localTeam && u.gathers && onBuild && rm) {
      let btns = "";
      for (const type of BUILDABLE) {
        const def = DEFS[type];
        const minAge = BUILDING_MIN_AGE[type] ?? Age.Dark;
        const ageOk = rm.age >= minAge;
        const cost =
          (def.costWood  > 0 ? `\u{1FAB5}${def.costWood} ` : "") +
          (def.costStone > 0 ? `\u{1FAA8}${def.costStone} ` : "") +
          (def.costGold  > 0 ? `\u{1FA99}${def.costGold}` : "");
        const canAfford = ageOk && rm.wood >= def.costWood && rm.stone >= def.costStone && rm.gold >= def.costGold;
        const icon = BUILDING_ICONS[type] ?? "🏚️";
        const bg = !ageOk ? "#1b1710" : canAfford ? "#35527a" : "#2a2a2a";
        const fg = !ageOk ? "#6a6048" : canAfford ? "#dde" : "#888";
        const sub = !ageOk ? AGE_NAMES[minAge] : (cost.trim() || "—");
        btns += iconButton("data-build", type, icon, sub, {
          bg, fg, enabled: canAfford,
          title: `${def.display}${cost.trim() ? ` — ${cost.trim()}` : " — free"}${ageOk ? "" : ` (${AGE_NAMES[minAge]})`}`,
        });
      }
      cmds += cmdHeader("İNŞA ET") + cmdGrid(btns);
    }

    this._render(stats, cmds);

    if (onBuild) {
      this.cmdTarget.querySelectorAll<HTMLButtonElement>("[data-build]").forEach(btn => {
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
    if (b.teamId === this.localTeam && b.buildingType === BuildingType.TownCenter && rm && ageSystem) {
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
    if (b.teamId === this.localTeam && training && rm) {
      const trainable = trainableFor(b.buildingType, b.teamId);
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
        let btns = "";
        for (const type of trainable) {
          const row = getUnitRow(type);
          const canAfford = rm.canAfford(row.trainFood, row.trainWood, row.trainGold);
          const cost =
            (row.trainFood  > 0 ? `🌾${row.trainFood} ` : "") +
            (row.trainWood  > 0 ? `🪵${row.trainWood} ` : "") +
            (row.trainGold  > 0 ? `🪙${row.trainGold}` : "");
          const icon = UNIT_ICONS[type] ?? "🧍";
          btns += iconButton("data-train", type, icon, cost.trim() || "—", {
            bg: canAfford ? "#2a5a2a" : "#2a2a2a",
            fg: canAfford ? "#dfd" : "#888",
            enabled: canAfford,
            title: `${UNIT_NAMES[type]}${cost.trim() ? ` — ${cost.trim()}` : ""}`,
          });
        }
        card += cmdGrid(btns) + `</div>`;
      }
    }

    // ── Research card ──────────────────────────────────────────────────────
    let resCard = "";
    if (b.teamId === this.localTeam && research && rm) {
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
        let btns = "";
        for (const tech of avail) {
          const def = TECH_DEFS[tech];
          const costStr =
            (def.food > 0 ? `🌾${def.food} ` : "") +
            (def.wood > 0 ? `🪵${def.wood} ` : "") +
            (def.gold > 0 ? `🪙${def.gold}` : "");
          const canAfford = rm.canAfford(def.food, def.wood, def.gold);
          btns += iconButton("data-research", tech, "📜", def.label, {
            bg: canAfford ? "#2a4a6a" : "#2a2a2a",
            fg: canAfford ? "#adf" : "#888",
            enabled: canAfford,
            title: `${def.label}${costStr.trim() ? ` — ${costStr.trim()}` : ""}`,
          });
        }
        resCard += cmdGrid(btns);
      }
    }

    // ── Market card ───────────────────────────────────────────────────────
    let mktCard = "";
    if (b.teamId === this.localTeam && b.buildingType === BuildingType.Market && market && rm) {
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
    if (b.teamId === this.localTeam && garrison && (garrison.canGarrison(b) || garrison.garrisonCount(b) > 0)) {
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
    const rallyHint = b.teamId === this.localTeam && (TRAINABLE[b.buildingType]?.length || BUILDING_TECHS[b.buildingType]?.length)
      ? `<div style="font-size:10px;color:#666;margin-top:6px">Right-click to set rally point${b.rallyPoint ? " (set)" : ""}</div>`
      : "";

    this._render(header, ageCard + card + resCard + mktCard + garrisonCard + rallyHint);

    // Wire up age-up button
    if (ageSystem && rm) {
      const ageBtn = this.cmdTarget.querySelector<HTMLButtonElement>("#age-up-btn");
      ageBtn?.addEventListener("click", () => {
        // Replicated command (MP-safe); falls back to direct call if no bus is wired.
        if (this._bus) this._bus.issue({ kind: 'ageUp', teamId: b.teamId, ai: false });
        else ageSystem.startAgeUp(rm);
      });
    }

    // Wire up training buttons
    if (training && rm) {
      this.cmdTarget.querySelectorAll<HTMLButtonElement>("[data-train]").forEach(btn => {
        btn.addEventListener("click", () => {
          const type = parseInt(btn.dataset.train ?? "0") as UnitType;
          if (this._bus) {
            this._bus.issue({ kind: 'train', teamId: b.teamId, ai: false, buildingId: b.id, unitType: type });
            play(SoundId.TrainStart);
          } else if (training.train(b, type, rm)) {
            play(SoundId.TrainStart);
          }
        });
      });
    }

    // Wire up research buttons
    if (research && rm) {
      this.cmdTarget.querySelectorAll<HTMLButtonElement>("[data-research]").forEach(btn => {
        btn.addEventListener("click", () => {
          const tech = btn.dataset.research! as TechId;
          if (this._bus) {
            this._bus.issue({ kind: 'research', teamId: b.teamId, ai: false, buildingId: b.id, techId: tech });
            play(SoundId.ButtonClick);
          } else if (research.start(b, tech, rm)) {
            play(SoundId.ButtonClick);
          }
        });
      });
    }

    // Wire up market buttons
    if (market && rm) {
      this.cmdTarget.querySelectorAll<HTMLButtonElement>("[data-sell]").forEach(btn => {
        btn.addEventListener("click", () => {
          const kind = parseInt(btn.dataset.sell!) as ResourceKind;
          if (this._bus) {
            this._bus.issue({ kind: 'marketSell', teamId: b.teamId, ai: false, resource: kind });
          } else {
            market.sell(rm, kind);
          }
        });
      });
      this.cmdTarget.querySelectorAll<HTMLButtonElement>("[data-buy]").forEach(btn => {
        btn.addEventListener("click", () => {
          const kind = parseInt(btn.dataset.buy!) as ResourceKind;
          if (this._bus) {
            this._bus.issue({ kind: 'marketBuy', teamId: b.teamId, ai: false, resource: kind });
          } else {
            market.buy(rm, kind);
          }
        });
      });
    }

    // Wire up garrison buttons
    if (garrison) {
      this.cmdTarget.querySelector<HTMLButtonElement>("#ungarrison-btn")
        ?.addEventListener("click", () => {
          if (this._bus) {
            this._bus.issue({ kind: 'ungarrison', teamId: b.teamId, ai: false, buildingId: b.id });
          } else {
            garrison.ungarrisonAll(b);
          }
        });
      this.cmdTarget.querySelector<HTMLButtonElement>("#ungarrison-one-btn")
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

    this._render(
      `<b>${units.length} units selected</b><br>` +
      `<span style="font-size:11px;color:#aaa">${breakdown}</span><br>` +
      `<span style="font-size:11px">HP: <span style="color:${hpColor}">${hpPct}%</span></span>`,
      "",
    );
  }

  clearInfo() {
    this.infoTarget.innerHTML = "";
    if (this._docked) this.cmdTarget.innerHTML = "";
    else this.infoPanel!.style.display = "none";
  }

  showVictory(winner: number) {
    const banner = document.createElement("div");
    Object.assign(banner.style, {
      position: "absolute", top: "40%", left: "50%",
      transform: "translate(-50%,-50%)",
      background: winner === this.localTeam ? "rgba(0,100,0,0.85)" : "rgba(120,0,0,0.85)",
      color: "#fff", fontSize: "32px", padding: "24px 40px",
      borderRadius: "10px", textAlign: "center", pointerEvents: "none",
    });
    banner.textContent = winner === this.localTeam ? "Victory!" : "Defeat";
    this.root.appendChild(banner);
  }

  /** Show a temporary message overlay (TriggerSystem / Tutorial seam). */
  showSubtitle(text: string, duration: number): void {
    const el = document.createElement("div");
    Object.assign(el.style, {
      position: "absolute", top: "18%", left: "50%",
      transform: "translateX(-50%)",
      background: "rgba(4,8,18,0.88)", color: "#f5d060",
      fontSize: "18px", padding: "12px 28px", borderRadius: "7px",
      border: "1px solid #886600", textAlign: "center",
      pointerEvents: "none", maxWidth: "640px", whiteSpace: "pre-wrap",
    });
    el.textContent = text;
    this.root.appendChild(el);
    window.setTimeout(() => el.remove(), duration * 1000);
  }
}
