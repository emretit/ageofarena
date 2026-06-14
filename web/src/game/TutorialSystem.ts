/**
 * TutorialSystem.ts — Port of TutorialSystem.cs (N13.tut).
 * Step-based overlay with coach-mark hints. Auto-progresses when conditions are met.
 * Skipped if localStorage "tutorial_done"=1.
 */
import { BuildingType, UnitType } from "../core/GameTypes";
import type { Unit } from "./Unit";
import type { Building } from "./Building";
import type { ResourceManager } from "../core/ResourceManager";

const DONE_KEY = "tutorial_done";

interface TutStep {
  text:    string;
  /** Returns true when this step's goal has been achieved (null = requires click). */
  done:    ((units: Unit[], buildings: Building[], rm: ResourceManager) => boolean) | null;
}

const STEPS: TutStep[] = [
  {
    text: "Hoş geldin! Age of Arena'ya hoş geldin.\nBu kısa rehber temel mekanikleri adım adım öğretecek.",
    done: null,
  },
  {
    text: "Sol tıkla ya da sürükleyerek bir KÖYLÜ seç.\nKöylüler yiyecek, odun, altın ve taş toplar.",
    done: (units) => units.some(u => u.teamId === 0 && u.unitType === UnitType.Villager && u.selected),
  },
  {
    text: "Köylüyü yiyecek kaynağına SAĞ-TIKLA — toplama emri verir.\nYiyecek inşaat ve eğitim için gerekli!",
    done: (_u, _b, rm) => rm.food > 220, // started with 200; +20 means gather happened
  },
  {
    text: "Bir köylü seç → B tuşuna bas → Ev (House) yap.\nEv nüfus limitini 5 artırır.",
    done: (_u, buildings) => buildings.some(b => b.teamId === 0 && b.buildingType === BuildingType.House && b.alive),
  },
  {
    text: "Şimdi bir KIŞLA (Barracks) inşa et — B → Kışla.\nKışla'dan piyade birimi eğitebilirsin.",
    done: (_u, buildings) => buildings.some(b => b.teamId === 0 && b.buildingType === BuildingType.Barracks && b.alive),
  },
  {
    text: "Kışla'yı seç → komut kartından NEFER (Militia) eğit.\nEğitim birkaç saniye sürer.",
    done: (units) => units.some(u => u.teamId === 0 && u.unitType === UnitType.Militia && u.alive),
  },
  {
    text: "TC'yi (Şehir Merkezini) seç → 'Feodal Çağ'a Geç' butonuna tıkla.\nYeterli kaynak gerekir: 500 yiyecek.",
    done: (_u, _b, rm) => rm.age >= 1, // Age.Feudal = 1
  },
  {
    text: "Feodal Çağ'a girdin! Artık Okçu Aralığı ve Süvari Ahırı inşa edebilirsin.\nBir saldırı ordusu kur — 3 farklı birim türü eğit.",
    done: (units) => {
      const types = new Set(units.filter(u => u.teamId === 0 && !u.gathers && u.alive).map(u => u.unitType));
      return types.size >= 2; // at least 2 military unit types
    },
  },
  {
    text: "Ordu hazır! Birimleri sürükle-seç, ardından düşman bölgesine SAĞ-TIKLA.\nZafer için düşman TC'sini yok et. İyi şanslar!",
    done: null,
  },
];

export class TutorialSystem {
  private readonly _overlay: HTMLDivElement;
  private readonly _textEl:  HTMLDivElement;
  private readonly _stepEl:  HTMLDivElement;
  private readonly _nextBtn: HTMLButtonElement;
  private _step    = 0;
  private _active  = false;
  private _waitForClick = false;

  constructor(container: HTMLElement) {
    this._overlay = document.createElement("div");
    this._overlay.style.cssText = `
      position:absolute; left:50%; bottom:255px; transform:translateX(-50%);
      width:680px; background:rgba(4,8,16,0.93); border:2px solid #c8a13a;
      border-radius:8px; padding:16px 18px 12px; font-family:monospace; color:#c8c8e0;
      pointer-events:auto; display:none; z-index:900;
    `;

    const header = document.createElement("div");
    header.style.cssText = "display:flex;justify-content:space-between;align-items:center;margin-bottom:8px;";

    this._stepEl = document.createElement("div");
    this._stepEl.style.cssText = "font-size:11px;color:#c8a13a;font-weight:bold;";
    header.appendChild(this._stepEl);

    const skipBtn = document.createElement("button");
    skipBtn.textContent = "Atla";
    skipBtn.style.cssText = `
      padding:3px 12px; font-size:11px; font-family:monospace;
      background:#3a0a0a; color:#cc6666; border:1px solid #6a2a2a; border-radius:3px; cursor:pointer;
    `;
    skipBtn.addEventListener("click", () => this._complete());
    header.appendChild(skipBtn);

    this._overlay.appendChild(header);

    this._textEl = document.createElement("div");
    this._textEl.style.cssText = "font-size:15px;line-height:1.6;white-space:pre-line;color:#d8d8f0;";
    this._overlay.appendChild(this._textEl);

    this._nextBtn = document.createElement("button");
    this._nextBtn.textContent = "İleri →";
    this._nextBtn.style.cssText = `
      display:none; margin-top:10px; padding:5px 22px; font-size:13px; font-family:monospace;
      background:#1f5c22; color:#c8f5c0; border:1px solid #3a9a3a; border-radius:4px; cursor:pointer;
    `;
    this._nextBtn.addEventListener("click", () => {
      this._step++;
      this._showStep();
    });
    this._overlay.appendChild(this._nextBtn);

    container.appendChild(this._overlay);
  }

  /** Call once at game start (after units/buildings are ready). Skips if already done. */
  init(): void {
    if (localStorage.getItem(DONE_KEY) === '1') return;
    this._step   = 0;
    this._active = true;
    this._overlay.style.display = "block";
    this._showStep();
  }

  /** Call each fixed step. */
  tick(units: Unit[], buildings: Building[], rm: ResourceManager): void {
    if (!this._active || this._waitForClick) return;
    if (this._step >= STEPS.length) return;

    const s = STEPS[this._step];
    if (s.done && s.done(units, buildings, rm)) {
      this._step++;
      this._showStep();
    }
  }

  private _showStep(): void {
    if (this._step >= STEPS.length) { this._complete(); return; }
    const s = STEPS[this._step];
    this._textEl.textContent = s.text;
    this._stepEl.textContent = `Adım ${this._step + 1} / ${STEPS.length}`;
    this._waitForClick = s.done === null;
    this._nextBtn.style.display = this._waitForClick ? "inline-block" : "none";
  }

  private _complete(): void {
    localStorage.setItem(DONE_KEY, '1');
    this._active = false;
    this._overlay.style.display = "none";
  }
}
