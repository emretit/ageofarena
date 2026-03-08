import {
  GameEntity, BuildingEntity, UnitEntity, BuildingId,
  BUILDING_DEFS, UNIT_DEFS, UnitId, VILLAGER_BUILDABLE, Stance,
} from '../entities/types';
import { PlayerResources } from '../systems/ResourceManager';
import { getBuildingIcon, getUnitIcon } from './icons';

interface StanceDef {
  id: Stance;
  name: string;
  icon: string;
  hotkey: string;
}

const STANCES: StanceDef[] = [
  { id: 'aggressive', name: 'Aggressive', icon: '⚔️', hotkey: 'Q' },
  { id: 'defensive', name: 'Defensive', icon: '🛡️', hotkey: 'W' },
  { id: 'standGround', name: 'Stand Ground', icon: '🚫', hotkey: 'E' },
  { id: 'noAttack', name: 'No Attack', icon: '🕊️', hotkey: 'R' },
];

const GRID_COLS = 5;
const GRID_ROWS = 3;
const GRID_SIZE = GRID_COLS * GRID_ROWS;

// AoE2-style hotkey layout: 3 rows mapped to keyboard rows
const HOTKEY_LABELS = [
  'Q', 'W', 'E', 'R', 'T',
  'A', 'S', 'D', 'F', 'G',
  'Z', 'X', 'C', 'V', 'B',
];

type CommandContext =
  | { type: 'empty' }
  | { type: 'villager'; villagers: UnitEntity[] }
  | { type: 'building'; building: BuildingEntity }
  | { type: 'military'; units: UnitEntity[] };

interface SlotData {
  kind: 'build' | 'train';
  id: string;
  cost: { food: number; wood: number; gold: number };
}

export class CommandPanel {
  private grid: HTMLElement;
  private tooltip: HTMLElement;
  private context: CommandContext = { type: 'empty' };
  private slotData: (SlotData | null)[] = [];

  // Callbacks
  public onTrainClick: ((building: BuildingEntity, unitId: UnitId) => void) | null = null;
  public onBuildClick: ((buildingId: BuildingId) => void) | null = null;
  public onStanceClick: ((units: UnitEntity[], stance: Stance) => void) | null = null;
  public onDeleteClick: (() => void) | null = null;

  constructor() {
    this.grid = document.getElementById('command-grid')!;
    this.tooltip = document.getElementById('command-tooltip')!;
    this.renderEmpty();
  }

  onSelectionChanged(entities: GameEntity[]): void {
    const ctx = this.determineContext(entities);

    if (this.isSameContext(ctx)) return;

    this.context = ctx;

    switch (ctx.type) {
      case 'villager':
        this.showVillagerCommands();
        break;
      case 'building':
        this.showBuildingCommands(ctx.building);
        break;
      case 'military':
        this.showMilitaryCommands(ctx.units);
        break;
      case 'empty':
      default:
        this.renderEmpty();
        break;
    }
  }

  updateButtonStates(resources: PlayerResources): void {
    const slots = this.grid.querySelectorAll('.command-slot');
    slots.forEach((slot, i) => {
      const data = this.slotData[i];
      if (!data) return;

      const canAfford =
        resources.food >= data.cost.food &&
        resources.wood >= data.cost.wood &&
        resources.gold >= data.cost.gold;
      const hasPop = data.kind === 'train' ? resources.pop < resources.popCap : true;

      if (!canAfford || !hasPop) {
        slot.classList.add('disabled');
      } else {
        slot.classList.remove('disabled');
      }
    });
  }

  private determineContext(entities: GameEntity[]): CommandContext {
    if (entities.length === 0) return { type: 'empty' };

    // Single building selected
    if (entities.length === 1 && entities[0].type === 'building') {
      const b = entities[0];
      if (b.playerIndex === 0 && b.def.trainable.length > 0) {
        return { type: 'building', building: b };
      }
      return { type: 'empty' };
    }

    // Units selected
    const units = entities.filter((e): e is UnitEntity => e.type === 'unit' && e.playerIndex === 0);
    if (units.length === 0) return { type: 'empty' };

    const villagers = units.filter(u => u.def.id === 'villager');
    if (villagers.length > 0) {
      return { type: 'villager', villagers };
    }

    return { type: 'military', units };
  }

  private isSameContext(ctx: CommandContext): boolean {
    if (ctx.type !== this.context.type) return false;
    if (ctx.type === 'building' && this.context.type === 'building') {
      return ctx.building === this.context.building;
    }
    return true;
  }

  private showVillagerCommands(): void {
    this.grid.innerHTML = '';
    this.slotData = [];

    for (let i = 0; i < GRID_SIZE; i++) {
      if (i < VILLAGER_BUILDABLE.length) {
        const buildingId = VILLAGER_BUILDABLE[i];
        const def = BUILDING_DEFS[buildingId];
        const slot = this.createSlot(getBuildingIcon(buildingId), true, i);

        this.slotData.push({ kind: 'build', id: buildingId, cost: def.cost });

        slot.addEventListener('click', () => {
          if (slot.classList.contains('disabled')) return;
          this.onBuildClick?.(buildingId);
        });

        this.addTooltipEvents(slot, def.name, def.cost, `Build time: ${def.buildTime}s | [${HOTKEY_LABELS[i]}]`);
        this.grid.appendChild(slot);
      } else {
        this.slotData.push(null);
        this.grid.appendChild(this.createSlot('', false, i));
      }
    }
    this.addDeleteButton();
  }

  private showBuildingCommands(building: BuildingEntity): void {
    this.grid.innerHTML = '';
    this.slotData = [];

    for (let i = 0; i < GRID_SIZE; i++) {
      if (i < building.def.trainable.length) {
        const unitId = building.def.trainable[i] as UnitId;
        const def = UNIT_DEFS[unitId];
        if (!def) {
          this.slotData.push(null);
          this.grid.appendChild(this.createSlot('', false, i));
          continue;
        }

        const slot = this.createSlot(getUnitIcon(unitId), true, i);
        this.slotData.push({ kind: 'train', id: unitId, cost: def.cost });

        slot.addEventListener('click', () => {
          if (slot.classList.contains('disabled')) return;
          this.onTrainClick?.(building, unitId);
        });

        this.addTooltipEvents(slot, def.name, def.cost, `Train time: ${def.trainTime}s | ATK: ${def.attack} HP: ${def.hp} | [${HOTKEY_LABELS[i]}]`);
        this.grid.appendChild(slot);
      } else {
        this.slotData.push(null);
        this.grid.appendChild(this.createSlot('', false, i));
      }
    }
    this.addDeleteButton();
  }

  private showMilitaryCommands(units: UnitEntity[]): void {
    this.grid.innerHTML = '';
    this.slotData = [];

    // Current stance of first unit (for highlighting)
    const currentStance = units.length > 0 ? units[0].stance : 'aggressive';

    for (let i = 0; i < GRID_SIZE; i++) {
      if (i < STANCES.length) {
        const stanceDef = STANCES[i];
        const slot = this.createSlot(stanceDef.icon, true, i);
        this.slotData.push(null); // No cost for stance buttons

        // Highlight active stance
        if (stanceDef.id === currentStance) {
          slot.style.borderColor = '#c0a848';
          slot.style.background = 'linear-gradient(180deg, #4a3a1e 0%, #3a2a14 100%)';
        }

        slot.addEventListener('click', () => {
          this.onStanceClick?.(units, stanceDef.id);
        });

        // Tooltip
        slot.addEventListener('mouseenter', () => {
          this.tooltip.innerHTML = `
            <div class="tooltip-name">${stanceDef.name}</div>
            <div style="color:#8a7a5a;font-size:10px;margin-top:3px;">Hotkey: [${stanceDef.hotkey}]</div>
          `;
          this.tooltip.style.display = 'block';
        });
        slot.addEventListener('mouseleave', () => {
          this.tooltip.style.display = 'none';
        });

        this.grid.appendChild(slot);
      } else {
        this.slotData.push(null);
        this.grid.appendChild(this.createSlot('', false, i));
      }
    }
    this.addDeleteButton();
  }

  private renderEmpty(): void {
    this.grid.innerHTML = '';
    this.slotData = [];
    for (let i = 0; i < GRID_SIZE; i++) {
      this.slotData.push(null);
      this.grid.appendChild(this.createSlot('', false, i));
    }
  }

  private createSlot(icon: string, active: boolean, slotIndex: number): HTMLElement {
    const div = document.createElement('div');
    div.className = 'command-slot' + (active ? ' active' : '');
    if (icon) {
      div.textContent = icon;
    }
    // Add hotkey label
    if (active && slotIndex < HOTKEY_LABELS.length) {
      const label = document.createElement('span');
      label.className = 'hotkey-label';
      label.textContent = HOTKEY_LABELS[slotIndex];
      div.appendChild(label);
    }
    return div;
  }

  /** Add delete button at the last grid slot (index 14) */
  private addDeleteButton(): void {
    const lastSlot = this.grid.children[GRID_SIZE - 1] as HTMLElement;
    if (!lastSlot) return;

    lastSlot.className = 'command-slot active';
    lastSlot.textContent = '💀';
    lastSlot.style.borderColor = '#6a2020';

    // Add hotkey label
    const label = document.createElement('span');
    label.className = 'hotkey-label';
    label.textContent = 'DEL';
    label.style.fontSize = '7px';
    lastSlot.appendChild(label);

    lastSlot.addEventListener('click', () => {
      this.onDeleteClick?.();
    });

    lastSlot.addEventListener('mouseenter', () => {
      this.tooltip.innerHTML = `
        <div class="tooltip-name" style="color:#cc4444;">Delete</div>
        <div style="color:#8a7a5a;font-size:10px;margin-top:3px;">Seçili birimleri/binaları sil [DEL]</div>
      `;
      this.tooltip.style.display = 'block';
    });
    lastSlot.addEventListener('mouseleave', () => {
      this.tooltip.style.display = 'none';
    });
  }

  private addTooltipEvents(
    slot: HTMLElement,
    name: string,
    cost: { food: number; wood: number; gold: number },
    extra: string,
  ): void {
    slot.addEventListener('mouseenter', () => {
      const costParts: string[] = [];
      if (cost.food > 0) costParts.push(`🍞 ${cost.food}`);
      if (cost.wood > 0) costParts.push(`🪵 ${cost.wood}`);
      if (cost.gold > 0) costParts.push(`🪙 ${cost.gold}`);

      this.tooltip.innerHTML = `
        <div class="tooltip-name">${name}</div>
        <div class="tooltip-cost">${costParts.join(' &nbsp; ')}</div>
        <div style="color:#8a7a5a;font-size:10px;margin-top:3px;">${extra}</div>
      `;
      this.tooltip.style.display = 'block';
    });

    slot.addEventListener('mouseleave', () => {
      this.tooltip.style.display = 'none';
    });
  }
}
