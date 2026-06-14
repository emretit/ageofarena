/**
 * ScenarioEditor.ts — Runtime map editor (N12.edit port, simplified).
 * Toggle with 'E' key. Left palette → click terrain → entity placed.
 * Delete mode: toggle 🗑 → click entity → removed.
 * Save/load via localStorage (JSON).
 */
import * as THREE from "three";
import { BuildingType, ResourceKind, UnitType } from "../core/GameTypes";
import type { Unit } from "./Unit";
import type { Building } from "./Building";
import type { ResourceNode } from "./ResourceNode";

const SAVE_KEY = "AoA_Scenario_0";

type Placeable =
  | { kind: 'unit'; type: UnitType; label: string }
  | { kind: 'building'; type: BuildingType; label: string }
  | { kind: 'resource'; rtype: ResourceKind; amount: number; label: string };

const PALETTE: Placeable[] = [
  { kind: 'unit', type: UnitType.Villager,  label: 'Köylü' },
  { kind: 'unit', type: UnitType.Militia,   label: 'Nefer' },
  { kind: 'unit', type: UnitType.Archer,    label: 'Okçu' },
  { kind: 'unit', type: UnitType.Cavalry,   label: 'Süvari' },
  { kind: 'unit', type: UnitType.Trebuchet, label: 'Trebuchet' },
  { kind: 'building', type: BuildingType.TownCenter, label: 'Şehir M.' },
  { kind: 'building', type: BuildingType.Barracks,   label: 'Kışla' },
  { kind: 'building', type: BuildingType.WatchTower, label: 'Kule' },
  { kind: 'building', type: BuildingType.Castle,     label: 'Kale' },
  { kind: 'resource', rtype: ResourceKind.Gold,  amount: 800, label: 'Altın' },
  { kind: 'resource', rtype: ResourceKind.Wood,  amount: 300, label: 'Odun' },
  { kind: 'resource', rtype: ResourceKind.Stone, amount: 400, label: 'Taş' },
  { kind: 'resource', rtype: ResourceKind.Food,  amount: 250, label: 'Yiyecek' },
];

interface Callbacks {
  spawnUnit:     (type: UnitType, x: number, z: number, team: number) => Unit | null;
  placeBuilding: (type: BuildingType, x: number, z: number, team: number) => void;
  placeResource: (kind: ResourceKind, amount: number, x: number, z: number) => void;
  removeUnit:     (u: Unit) => void;
  removeBuilding: (b: Building) => void;
  removeResource: (n: ResourceNode) => void;
  getUnits:       () => Unit[];
  getBuildings:   () => Building[];
  getResources:   () => ResourceNode[];
}

export class ScenarioEditor {
  private _open       = false;
  private _deleteMode = false;
  private _selTeam    = 0;
  private _selItem    = -1;
  private readonly _panel: HTMLDivElement;
  private readonly _raycaster = new THREE.Raycaster();
  private readonly _ground    = new THREE.Plane(new THREE.Vector3(0, 1, 0), 0);
  private readonly _hitPt     = new THREE.Vector3();
  private _activeBtn: HTMLButtonElement | null = null;
  private readonly _callbacks: Callbacks;
  private readonly _camera: THREE.Camera;
  private readonly _canvas: HTMLCanvasElement;

  constructor(
    container: HTMLElement,
    camera: THREE.Camera,
    canvas: HTMLCanvasElement,
    callbacks: Callbacks,
  ) {
    this._camera    = camera;
    this._canvas    = canvas;
    this._callbacks = callbacks;

    this._panel = document.createElement("div");
    this._panel.style.cssText = `
      position:absolute; top:10px; left:10px; width:160px;
      background:rgba(6,10,20,0.92); border:1px solid #334; border-radius:8px;
      padding:10px 8px; font-family:monospace; font-size:12px; color:#c8c8e0;
      display:none; z-index:800; user-select:none;
    `;
    container.appendChild(this._panel);

    canvas.addEventListener("click", (e) => this._onClick(e));
  }

  isOpen(): boolean { return this._open; }

  toggle(): void {
    this._open = !this._open;
    this._panel.style.display = this._open ? "block" : "none";
    if (this._open) this._rebuild();
  }

  private _rebuild(): void {
    this._panel.innerHTML = "";

    const title = document.createElement("div");
    title.textContent = "SENARYO EDITÖRÜ";
    title.style.cssText = "font-size:10px;color:#f5d060;font-weight:bold;letter-spacing:1px;margin-bottom:8px;";
    this._panel.appendChild(title);

    // Team selector
    const teamRow = document.createElement("div");
    teamRow.style.cssText = "display:flex;gap:4px;margin-bottom:8px;";
    for (let t = 0; t < 4; t++) {
      const btn = document.createElement("button");
      btn.textContent = `T${t}`;
      btn.style.cssText = `
        flex:1; padding:3px; border:1px solid ${t === this._selTeam ? '#f5d060' : '#333'};
        border-radius:3px; background:${t === this._selTeam ? '#2a2000' : '#11192a'};
        color:${t === this._selTeam ? '#f5d060' : '#aaa'}; font-size:10px; cursor:pointer;
        font-family:monospace;
      `;
      btn.addEventListener("click", () => { this._selTeam = t; this._rebuild(); });
      teamRow.appendChild(btn);
    }
    this._panel.appendChild(teamRow);

    // Delete mode toggle
    const delBtn = document.createElement("button");
    delBtn.textContent = this._deleteMode ? "SİL MODU AÇIK" : "Sil Modu";
    delBtn.style.cssText = `
      width:100%; padding:4px; margin-bottom:8px; border:1px solid ${this._deleteMode ? '#cc4444' : '#333'};
      border-radius:3px; background:${this._deleteMode ? '#3a0a0a' : '#11192a'};
      color:${this._deleteMode ? '#ff6666' : '#aaa'}; font-size:11px; cursor:pointer;
      font-family:monospace;
    `;
    delBtn.addEventListener("click", () => { this._deleteMode = !this._deleteMode; this._selItem = -1; this._rebuild(); });
    this._panel.appendChild(delBtn);

    if (!this._deleteMode) {
      const sep = document.createElement("div");
      sep.textContent = "── PALET ──";
      sep.style.cssText = "font-size:10px;color:#556;margin-bottom:6px;text-align:center;";
      this._panel.appendChild(sep);

      // Palette buttons
      PALETTE.forEach((p, i) => {
        const btn = document.createElement("button");
        btn.textContent = p.label;
        const sel = i === this._selItem;
        btn.style.cssText = `
          width:100%; padding:4px 6px; margin-bottom:3px;
          border:1px solid ${sel ? '#6af' : '#333'}; border-radius:3px;
          background:${sel ? '#0a1a2a' : '#11192a'}; color:${sel ? '#6af' : '#aaa'};
          font-size:11px; cursor:pointer; text-align:left; font-family:monospace;
        `;
        btn.addEventListener("click", () => {
          this._selItem = i;
          this._rebuild();
        });
        if (sel) this._activeBtn = btn;
        this._panel.appendChild(btn);
      });
    }

    // Save/Load
    const ioRow = document.createElement("div");
    ioRow.style.cssText = "display:flex;gap:4px;margin-top:10px;";

    const saveBtn = document.createElement("button");
    saveBtn.textContent = "Kaydet";
    saveBtn.style.cssText = `
      flex:1; padding:4px; border:1px solid #336; border-radius:3px;
      background:#0a0e1e; color:#88aadd; font-size:10px; cursor:pointer; font-family:monospace;
    `;
    saveBtn.addEventListener("click", () => this._save());
    ioRow.appendChild(saveBtn);

    const loadBtn = document.createElement("button");
    loadBtn.textContent = "Yükle";
    loadBtn.style.cssText = `
      flex:1; padding:4px; border:1px solid #336; border-radius:3px;
      background:#0a0e1e; color:#88aadd; font-size:10px; cursor:pointer; font-family:monospace;
    `;
    loadBtn.addEventListener("click", () => this._load());
    ioRow.appendChild(loadBtn);

    this._panel.appendChild(ioRow);

    const hint = document.createElement("div");
    hint.textContent = "E: kapat";
    hint.style.cssText = "font-size:9px;color:#445;margin-top:8px;text-align:center;";
    this._panel.appendChild(hint);
  }

  private _onClick(e: MouseEvent): void {
    if (!this._open) return;
    // Ignore clicks inside the editor panel itself
    if (this._panel.contains(e.target as Node)) return;

    const rect = this._canvas.getBoundingClientRect();
    const ndc = new THREE.Vector2(
      ((e.clientX - rect.left) / rect.width) * 2 - 1,
      -((e.clientY - rect.top)  / rect.height) * 2 + 1,
    );
    this._raycaster.setFromCamera(ndc, this._camera);
    if (!this._raycaster.ray.intersectPlane(this._ground, this._hitPt)) return;

    const x = this._hitPt.x;
    const z = this._hitPt.z;

    if (this._deleteMode) {
      this._tryDelete(x, z);
      return;
    }

    if (this._selItem < 0 || this._selItem >= PALETTE.length) return;
    const p = PALETTE[this._selItem];

    if (p.kind === 'unit') {
      this._callbacks.spawnUnit(p.type, x, z, this._selTeam);
    } else if (p.kind === 'building') {
      this._callbacks.placeBuilding(p.type, x, z, this._selTeam);
    } else if (p.kind === 'resource') {
      this._callbacks.placeResource(p.rtype, p.amount, x, z);
    }
  }

  private _tryDelete(x: number, z: number): void {
    const RADIUS = 3;
    const r2 = RADIUS * RADIUS;

    for (const u of this._callbacks.getUnits()) {
      if (!u.alive) continue;
      const dx = u.x - x; const dz = u.z - z;
      if (dx * dx + dz * dz <= r2) { this._callbacks.removeUnit(u); return; }
    }
    for (const b of this._callbacks.getBuildings()) {
      if (!b.alive) continue;
      const dx = b.pos.x - x; const dz = b.pos.z - z;
      if (dx * dx + dz * dz <= r2) { this._callbacks.removeBuilding(b); return; }
    }
    for (const n of this._callbacks.getResources()) {
      if (n.depleted) continue;
      const dx = n.root.position.x - x; const dz = n.root.position.z - z;
      if (dx * dx + dz * dz <= r2) { this._callbacks.removeResource(n); return; }
    }
  }

  private _save(): void {
    const data = {
      units:     this._callbacks.getUnits().filter(u => u.alive).map(u => ({ type: u.unitType, x: u.x, z: u.z, team: u.teamId })),
      buildings: this._callbacks.getBuildings().filter(b => b.alive).map(b => ({ type: b.buildingType, x: b.pos.x, z: b.pos.z, team: b.teamId })),
      resources: this._callbacks.getResources().filter(n => !n.depleted).map(n => ({ rtype: n.kind, amount: n.amount, x: n.root.position.x, z: n.root.position.z })),
    };
    localStorage.setItem(SAVE_KEY, JSON.stringify(data));
    console.info("[ScenarioEditor] kaydedildi");
  }

  private _load(): void {
    const raw = localStorage.getItem(SAVE_KEY);
    if (!raw) { console.warn("[ScenarioEditor] kayıtlı senaryo yok"); return; }
    try {
      const data = JSON.parse(raw) as {
        units:     Array<{type: UnitType; x: number; z: number; team: number}>;
        buildings: Array<{type: BuildingType; x: number; z: number; team: number}>;
        resources: Array<{rtype: ResourceKind; amount: number; x: number; z: number}>;
      };
      for (const u of data.units ?? []) this._callbacks.spawnUnit(u.type, u.x, u.z, u.team);
      for (const b of data.buildings ?? []) this._callbacks.placeBuilding(b.type, b.x, b.z, b.team);
      for (const r of data.resources ?? []) this._callbacks.placeResource(r.rtype, r.amount, r.x, r.z);
      console.info("[ScenarioEditor] yüklendi");
    } catch { console.error("[ScenarioEditor] JSON parse hatası"); }
  }
}
