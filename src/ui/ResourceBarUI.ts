import { PlayerResources } from '../systems/ResourceManager';

export type AgeName = 'Dark Age' | 'Feudal Age' | 'Castle Age' | 'Imperial Age';

const AGE_ICONS: Record<AgeName, string> = {
  'Dark Age': '🏛️',
  'Feudal Age': '⛪',
  'Castle Age': '🏰',
  'Imperial Age': '👑',
};

export class ResourceBarUI {
  private foodEl: HTMLElement;
  private woodEl: HTMLElement;
  private goldEl: HTMLElement;
  private popEl: HTMLElement;
  private idleCountEl: HTMLElement;
  private militaryCountEl: HTMLElement;
  private ageNameEl: HTMLElement;
  private ageIconEl: HTMLElement;
  private gameTimeEl: HTMLElement;

  constructor() {
    this.foodEl = document.getElementById('food')!;
    this.woodEl = document.getElementById('wood')!;
    this.goldEl = document.getElementById('gold')!;
    this.popEl = document.getElementById('pop')!;
    this.idleCountEl = document.getElementById('idle-count')!;
    this.militaryCountEl = document.getElementById('military-count')!;
    this.ageNameEl = document.getElementById('age-name')!;
    this.ageIconEl = this.ageNameEl.previousElementSibling as HTMLElement;
    this.gameTimeEl = document.getElementById('game-time')!;
  }

  update(res: PlayerResources): void {
    this.foodEl.textContent = String(Math.floor(res.food));
    this.woodEl.textContent = String(Math.floor(res.wood));
    this.goldEl.textContent = String(Math.floor(res.gold));
    this.popEl.textContent = `${res.pop}/${res.popCap}`;
  }

  updateCounts(idleVillagers: number, militaryUnits: number): void {
    this.idleCountEl.textContent = String(idleVillagers);
    this.militaryCountEl.textContent = String(militaryUnits);
  }

  updateAge(age: AgeName): void {
    this.ageNameEl.textContent = age;
    this.ageIconEl.textContent = AGE_ICONS[age];
  }

  updateGameTime(totalSeconds: number): void {
    const mins = Math.floor(totalSeconds / 60);
    const secs = Math.floor(totalSeconds % 60);
    this.gameTimeEl.textContent = `${String(mins).padStart(2, '0')}:${String(secs).padStart(2, '0')}`;
  }
}
