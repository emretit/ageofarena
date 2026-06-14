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
import { CIVILIZATION_DEFS } from "../core/CivilizationDefs";
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
 * One AoE2-style command button: 60×60 tile with a large glyph + small sub-label.
 * Matches Unity HUD.cs BtnW=60/BtnH=60. Disabled = dark + not-allowed cursor.
 */
function iconButton(
  attr: string, val: string | number, icon: string, sub: string,
  o: { bg: string; fg: string; enabled: boolean; title: string },
): string {
  return (
    `<button ${attr}="${val}" title="${o.title}"${o.enabled ? "" : " disabled"} style="` +
    `width:60px;height:60px;display:flex;flex-direction:column;align-items:center;justify-content:center;` +
    `gap:2px;padding:3px;border-radius:3px;` +
    `border:1px solid rgba(255,255,255,0.1);outline:1px solid rgba(0,0,0,0.5);outline-offset:-2px;` +
    `cursor:${o.enabled ? "pointer" : "not-allowed"};background:${o.bg};color:${o.fg};font-family:monospace;` +
    `box-shadow:inset 0 1px 0 rgba(255,255,255,0.07),0 2px 4px rgba(0,0,0,0.6);">` +
    `<span style="font-size:24px;line-height:1">${icon}</span>` +
    `<span style="font-size:8px;line-height:1.1;opacity:0.9;text-align:center;word-break:break-word;max-height:16px;overflow:hidden">${sub}</span>` +
    `</button>`
  );
}

/** Empty command slot — always-visible dark frame matching AoE2's slot grid. */
const SLOT_EMPTY = `<div style="width:60px;height:60px;background:#0d0f18;border:1px solid #1e2230;border-radius:3px;box-shadow:inset 0 1px 3px rgba(0,0,0,0.7)"></div>`;

/**
 * AoE2-style 5-column slot grid. Pads to the next full row with dark empty-slot divs
 * so the grid always looks deliberate (Unity: Cols×Rows fixed slot frames).
 */
function cmdGrid(buttons: string, cols = 5): string {
  const count = (buttons.match(/<button /g) ?? []).length;
  const totalRows = Math.max(1, Math.ceil(count / cols));
  const empty = Array(Math.max(0, totalRows * cols - count)).fill(SLOT_EMPTY).join("");
  return `<div style="display:grid;grid-template-columns:repeat(${cols},60px);gap:4px;padding:8px 8px 4px">${buttons}${empty}</div>`;
}

/**
 * AoE2-style info card for the infoSlot: emoji portrait box + bold name + colour-coded
 * HP bar (green >66%, amber 33-66%, red <33%) + one-line sub-text.
 */
function infoCard(portrait: string, name: string, hp: number, maxHp: number, sub: string): string {
  const pct  = maxHp > 0 ? Math.round((hp / maxHp) * 100) : 100;
  const hpC  = pct > 66 ? "#4caf50" : pct > 33 ? "#f90" : "#e44";
  return (
    `<div style="display:flex;gap:8px;align-items:flex-start;padding:4px 2px 6px">` +
    `<div style="width:64px;height:64px;flex-shrink:0;background:#0c0e14;border:2px solid #2a2c3a;` +
    `border-radius:4px;display:flex;align-items:center;justify-content:center;font-size:30px">${portrait}</div>` +
    `<div style="flex:1;min-width:0">` +
    `<div style="font-size:14px;font-weight:bold;color:#e8d4a0;margin-bottom:4px;` +
    `white-space:nowrap;overflow:hidden;text-overflow:ellipsis">${name}</div>` +
    `<div style="background:#14161e;border-radius:2px;height:8px;margin-bottom:3px">` +
    `<div style="background:${hpC};height:100%;width:${pct}%;border-radius:2px"></div></div>` +
    `<div style="font-size:11px;color:${hpC};margin-bottom:3px">${Math.ceil(hp)} / ${maxHp} HP</div>` +
    `<div style="font-size:11px;color:#9a9caa">${sub}</div>` +
    `</div></div>`
  );
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
    // Inject pop-flash CSS keyframe once
    if (!document.getElementById("aoa-hud-styles")) {
      const style = document.createElement("style");
      style.id = "aoa-hud-styles";
      style.textContent = `@keyframes pop-flash { from { opacity: 1; } to { opacity: 0.4; } }`;
      document.head.appendChild(style);
    }
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
    const rateKinds = [ResourceKind.Food, ResourceKind.Wood, ResourceKind.Gold, ResourceKind.Stone];
    const rates = [rm.rateFood, rm.rateWood, rm.rateGold, rm.rateStone];
    const hasIncome = rates.some(r => r > 0.1);
    for (let i = 0; i < rateKinds.length; i++) {
      const kind = rateKinds[i];
      const base = `${ICONS[kind]} ${rm.get(kind)}`;
      if (hasIncome && rates[i] > 0.1) {
        this.counters[kind].innerHTML = base + ` <span style="font-size:10px;color:#8a8;opacity:0.85">+${rates[i].toFixed(1)}/s</span>`;
      } else {
        this.counters[kind].textContent = base;
      }
    }
    // Pop cap warning: red flash at cap, orange near cap
    const popFull = rm.pop >= rm.popCap;
    const popNear = !popFull && rm.pop >= rm.popCap - 2;
    this.popCell.textContent = `Pop ${rm.pop}/${rm.popCap}`;
    this.popCell.style.color = popFull ? "#ff4444" : popNear ? "#ffaa22" : "#adf";
    this.popCell.style.fontWeight = popFull ? "bold" : "normal";
    this.popCell.style.animation = popFull ? "pop-flash 0.6s infinite alternate" : "";
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
    const portrait = UNIT_ICONS[u.unitType] ?? "🧍";
    const grpBadge = u.controlGroupNum > 0
      ? ` <span style="display:inline-block;background:#333;color:#f5d060;border:1px solid #666;border-radius:3px;padding:0 4px;font-size:10px;vertical-align:middle;">[${u.controlGroupNum}]</span>`
      : "";
    const waypointNote = u.pendingGoals.length > 0
      ? ` · ${u.pendingGoals.length} waypoint` + (u.pendingGoals.length > 1 ? "s" : "")
      : "";
    const subLine  = `Atk: ${u.baseAtk}  ` +
      (u.gathers && u.carryAmount > 0 ? `Carry: ${u.carryAmount}/10` : `Spd: ${u.moveSpeed.toFixed(1)}`) +
      waypointNote;
    const stats = infoCard(portrait, typeName + grpBadge, u.hp, u.maxHp, subLine);

    // Build panel for player villagers — all building types always shown (locked ones
    // greyed with their unlock age), AoE2-style 5-column slot grid.
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
        const bg = !ageOk ? "#1b1710" : canAfford ? "#2a4a2a" : "#1e2030";
        const fg = !ageOk ? "#5a5040" : canAfford ? "#cde" : "#666";
        const sub = !ageOk ? AGE_NAMES[minAge] : (cost.trim() || "free");
        btns += iconButton("data-build", type, icon, sub, {
          bg, fg, enabled: canAfford,
          title: `${def.display}${cost.trim() ? ` — ${cost.trim()}` : " — free"}${ageOk ? "" : ` (${AGE_NAMES[minAge]})`}`,
        });
      }
      cmds += cmdGrid(btns);
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
    const portrait = BUILDING_ICONS[b.buildingType] ?? "🏚️";
    const ageName  = rm && b.buildingType === BuildingType.TownCenter ? AGE_NAMES[rm.age] : "";
    // UI-6: building condition indicator
    const hpPct = Math.round((b.hp / b.maxHp) * 100);
    const condition = hpPct >= 100 ? "" : hpPct >= 66 ? " [OK]" : hpPct >= 33 ? " [Dam]" : " [Crit]";
    const condColor = hpPct >= 66 ? "#8a8" : hpPct >= 33 ? "#fa0" : "#f44";
    const condBadge = hpPct < 100 ? ` <span style="color:${condColor};font-size:10px">${condition}</span>` : "";
    const subLine  = ageName || (hpPct < 100 ? `<span style="color:${condColor}">${hpPct}% kondisyon</span>` : "");
    const header   = infoCard(portrait, b.def.display + condBadge, Math.ceil(b.hp), b.maxHp, subLine);

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
        if (qLen > 0) {
          const pct = Math.round(prog * 100);
          card += `<div style="font-size:11px;color:#cca;padding:4px 8px">Training ${pct}%&nbsp;[${qLen}/5]</div>`;
          card += `<div style="background:#1a1a22;border-radius:2px;height:5px;margin:0 8px 4px"><div style="background:#4c4;height:100%;width:${pct}%;border-radius:2px"></div></div>`;
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
          btns += iconButton("data-train", type, icon, cost.trim() || "free", {
            bg: canAfford ? "#1a4a1a" : "#1e2030",
            fg: canAfford ? "#cde" : "#555",
            enabled: canAfford,
            title: `${UNIT_NAMES[type]}${cost.trim() ? ` — ${cost.trim()}` : ""}`,
          });
        }
        card += cmdGrid(btns);
      }
    }

    // ── Research card ──────────────────────────────────────────────────────
    let resCard = "";
    if (b.teamId === this.localTeam && research && rm) {
      const avail  = research.available(b, rm);
      const locked = research.locked(b, rm);
      const active = research.active(b);
      if (avail.length > 0 || active || locked.length > 0) {
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
            title: `${def.label}${def.civGate !== undefined ? ` [${CIVILIZATION_DEFS[def.civGate].display}]` : ""}${costStr.trim() ? ` — ${costStr.trim()}` : ""}`,
          });
        }
        // Locked/blocked techs: shown greyed with lock reason
        for (const { tech, reason } of locked) {
          const def = TECH_DEFS[tech];
          const reasonLabel = reason === 'age' ? `(${AGE_NAMES[def.minAge]})` : reason === 'prereq' ? '(prereq)' : reason === 'done' ? '(done)' : '';
          btns += iconButton("data-research-locked", tech, reason === 'done' ? "✓" : "🔒", reasonLabel || def.label, {
            bg: reason === 'done' ? "#1a2a1a" : "#1a1a1a",
            fg: reason === 'done' ? "#4a6a4a" : "#444",
            enabled: false,
            title: `${def.label} — ${reason === 'done' ? 'researched' : reason === 'age' ? `requires ${AGE_NAMES[def.minAge]}` : reason === 'prereq' ? 'prerequisite missing' : 'locked'}`,
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

  showVictory(winner: number, stats?: { kills: number; buildingsDestroyed: number; resourcesGathered: number }) {
    const banner = document.createElement("div");
    Object.assign(banner.style, {
      position: "absolute", top: "40%", left: "50%",
      transform: "translate(-50%,-50%)",
      background: winner === this.localTeam ? "rgba(0,100,0,0.85)" : "rgba(120,0,0,0.85)",
      color: "#fff", fontSize: "32px", padding: "24px 40px",
      borderRadius: "10px", textAlign: "center", pointerEvents: "none",
    });
    const title = winner === this.localTeam ? "Victory!" : "Defeat";
    if (stats) {
      banner.innerHTML =
        `<div>${title}</div>` +
        `<div style="font-size:14px;margin-top:12px;color:rgba(255,255,255,0.85);line-height:1.8">` +
        `Kills: ${stats.kills} &nbsp;|&nbsp; Buildings: ${stats.buildingsDestroyed} &nbsp;|&nbsp; Resources: ${stats.resourcesGathered}` +
        `</div>`;
    } else {
      banner.textContent = title;
    }
    this.root.appendChild(banner);
  }

  /** Scale the HUD overlay (accessibility setting). 1.0 = normal. */
  setUiScale(scale: number): void {
    this.root.style.transform = `scale(${scale})`;
    this.root.style.transformOrigin = "bottom left";
    localStorage.setItem("uiScale", String(scale));
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
