import { GameEntity, BuildingEntity, UnitEntity, UNIT_DEFS, UnitId } from '../entities/types';
import { QueueEntry } from '../systems/TrainingQueue';
import { getBuildingIcon, getUnitIcon } from './icons';

export class HUD {
  private infoHeader: HTMLElement;
  private infoContent: HTMLElement;
  private unitPanel: HTMLElement;
  private queueDisplay: HTMLElement;
  private currentBuilding: BuildingEntity | null = null;

  // Callback for queue cancellation
  public onCancelQueueClick: ((building: BuildingEntity, index: number) => void) | null = null;

  constructor() {
    this.infoHeader = document.getElementById('info-header')!;
    this.infoContent = document.getElementById('info-content')!;
    this.unitPanel = document.getElementById('unit-panel')!;
    this.queueDisplay = document.getElementById('queue-display')!;
  }

  showEntity(entity: GameEntity | null): void {
    if (!entity) {
      this.infoHeader.textContent = 'Age of Arena';
      this.infoContent.innerHTML = 'Bir bina veya birim seçin';
      this.clearPortrait();
      this.currentBuilding = null;
      this.queueDisplay.innerHTML = '';
      return;
    }

    if (entity.type === 'building') {
      this.showBuilding(entity);
    } else {
      this.showUnit(entity);
    }
  }

  showEntities(entities: GameEntity[]): void {
    if (entities.length === 0) {
      this.showEntity(null);
      return;
    }
    if (entities.length === 1) {
      this.showEntity(entities[0]);
      return;
    }

    // Multi-select: only units
    const units = entities.filter((e): e is UnitEntity => e.type === 'unit');
    if (units.length === 0) {
      this.showEntity(entities[0]);
      return;
    }

    this.currentBuilding = null;
    this.queueDisplay.innerHTML = '';

    // Count unit types
    const counts = new Map<UnitId, { count: number; totalHp: number; maxHp: number }>();
    for (const u of units) {
      const existing = counts.get(u.def.id);
      if (existing) {
        existing.count++;
        existing.totalHp += u.hp;
        existing.maxHp += u.maxHp;
      } else {
        counts.set(u.def.id, { count: 1, totalHp: u.hp, maxHp: u.maxHp });
      }
    }

    this.infoHeader.textContent = `${units.length} birim seçili`;

    // Summary info
    let infoHtml = '<div style="width:100%;color:#a0906a;font-size:11px;">';
    for (const [unitId, data] of counts) {
      const def = UNIT_DEFS[unitId];
      const avgHp = Math.round(data.totalHp / data.count);
      infoHtml += `<div>${getUnitIcon(unitId)} ${def.name} x${data.count} (HP: ${avgHp})</div>`;
    }
    infoHtml += '</div>';
    this.infoContent.innerHTML = infoHtml;

    // Portrait grid (max 12)
    const grid = this.unitPanel.querySelector('.unit-grid');
    if (!grid) return;
    grid.innerHTML = '';
    const maxPortraits = Math.min(units.length, 12);
    for (let i = 0; i < maxPortraits; i++) {
      const u = units[i];
      const hpPct = Math.round((u.hp / u.maxHp) * 100);
      const hpColor = hpPct > 60 ? '#4a4' : hpPct > 30 ? '#aa4' : '#a44';
      const div = document.createElement('div');
      div.className = 'unit-portrait';
      div.style.cssText = 'position:relative;cursor:pointer;font-size:16px;';
      div.innerHTML = `${getUnitIcon(u.def.id)}<div style="position:absolute;bottom:1px;left:2px;right:2px;height:2px;background:#2a1a0a;"><div style="width:${hpPct}%;height:100%;background:${hpColor};"></div></div>`;
      div.dataset.unitIndex = String(i);
      grid.appendChild(div);
    }
  }

  private showBuilding(b: BuildingEntity): void {
    this.currentBuilding = b;
    this.infoHeader.textContent = b.def.name;

    const hpPct = Math.round((b.hp / b.maxHp) * 100);
    const hpColor = hpPct > 60 ? '#4a4' : hpPct > 30 ? '#aa4' : '#a44';
    this.infoContent.innerHTML = `
      <div style="width:100%">
        <div style="display:flex;justify-content:space-between;margin-bottom:4px;">
          <span style="color:#c0b080">HP: ${b.hp}/${b.maxHp}</span>
          <span style="color:#8a7a5a">Player ${b.playerIndex + 1}</span>
        </div>
        <div style="width:100%;height:6px;background:#2a1a0a;border:1px solid #4a3a20;border-radius:2px;">
          <div style="width:${hpPct}%;height:100%;background:${hpColor};border-radius:1px;"></div>
        </div>
        ${b.def.popSpace ? `<div style="color:#a0906a;font-size:11px;margin-top:4px;">Population: +${b.def.popSpace}</div>` : ''}
      </div>
    `;

    this.showPortraitIcon(b.def.name, getBuildingIcon(b.def.id));
  }

  updateQueueDisplay(entries: QueueEntry[]): void {
    if (!this.currentBuilding) {
      this.queueDisplay.innerHTML = '';
      return;
    }

    this.queueDisplay.innerHTML = '';
    for (let i = 0; i < entries.length; i++) {
      const entry = entries[i];
      const slot = document.createElement('div');
      slot.className = 'queue-slot';
      slot.title = `${UNIT_DEFS[entry.unitId].name} - Sağ tık iptal`;
      slot.innerHTML = `${getUnitIcon(entry.unitId)}`;

      // Progress bar for first entry
      if (i === 0) {
        const pct = Math.round((1 - entry.remainingTime / entry.totalTime) * 100);
        const bar = document.createElement('div');
        bar.className = 'queue-progress';
        bar.style.width = `${pct}%`;
        slot.appendChild(bar);
      }

      // Right-click to cancel
      slot.addEventListener('contextmenu', (e) => {
        e.preventDefault();
        e.stopPropagation();
        if (this.onCancelQueueClick && this.currentBuilding) {
          this.onCancelQueueClick(this.currentBuilding, i);
        }
      });

      this.queueDisplay.appendChild(slot);
    }
  }

  getCurrentBuilding(): BuildingEntity | null {
    return this.currentBuilding;
  }

  private showUnit(u: GameEntity & { type: 'unit' }): void {
    this.currentBuilding = null;
    this.queueDisplay.innerHTML = '';
    this.infoHeader.textContent = u.def.name;

    const hpPct = Math.round((u.hp / u.maxHp) * 100);
    const hpColor = hpPct > 60 ? '#4a4' : hpPct > 30 ? '#aa4' : '#a44';
    const stateColor = u.state === 'idle' ? '#888' : u.state === 'moving' ? '#4a4' : '#a44';
    const stateLabel = u.state === 'idle' ? 'Idle' : u.state === 'moving' ? 'Moving' : 'Attacking';

    this.infoContent.innerHTML = `
      <div style="width:100%">
        <div style="display:flex;justify-content:space-between;margin-bottom:4px;">
          <span style="color:#c0b080">HP: ${u.hp}/${u.maxHp}</span>
          <span style="color:#8a7a5a">Player ${u.playerIndex + 1}</span>
        </div>
        <div style="width:100%;height:6px;background:#2a1a0a;border:1px solid #4a3a20;border-radius:2px;margin-bottom:6px;">
          <div style="width:${hpPct}%;height:100%;background:${hpColor};border-radius:1px;"></div>
        </div>
        <div style="display:grid;grid-template-columns:1fr 1fr;gap:2px 16px;color:#a0906a;font-size:11px;margin-bottom:4px;">
          <span>ATK: ${u.def.attack}</span>
          <span>ARM: ${u.def.armor}</span>
          <span>SPD: ${u.def.speed}</span>
          <span>${u.def.range > 0 ? `RNG: ${u.def.range}` : ''}</span>
        </div>
        <div style="color:${stateColor};font-size:10px;">
          <span style="display:inline-block;width:6px;height:6px;border-radius:50%;background:${stateColor};margin-right:4px;vertical-align:middle;"></span>${stateLabel}
        </div>
      </div>
    `;

    this.showPortraitIcon(u.def.name, getUnitIcon(u.def.id));
  }

  private showPortraitIcon(name: string, icon: string): void {
    const grid = this.unitPanel.querySelector('.unit-grid');
    if (!grid) return;
    grid.innerHTML = `
      <div class="unit-portrait" style="width:64px;height:64px;grid-column:1/4;justify-self:center;font-size:28px;border-color:#9a8a58;">
        ${icon}
      </div>
      <div style="grid-column:1/4;text-align:center;color:#c0b080;font-size:11px;margin-top:2px;">
        ${name}
      </div>
    `;
  }

  private clearPortrait(): void {
    const grid = this.unitPanel.querySelector('.unit-grid');
    if (!grid) return;
    grid.innerHTML = '';
    for (let i = 0; i < 6; i++) {
      const div = document.createElement('div');
      div.className = 'unit-portrait';
      grid.appendChild(div);
    }
  }
}
