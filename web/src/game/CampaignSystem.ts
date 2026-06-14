/**
 * CampaignSystem.ts — Port of CampaignSystem.cs (N13.camp).
 * Ordered sequence of scenarios; progress persisted in localStorage.
 */
import { Age, ResourceKind } from "../core/GameTypes";
import type { ResourceManager } from "../core/ResourceManager";
import { makeTrigger, TriggerSystem } from "./TriggerSystem";

const SAVE_KEY = "AoA_Campaign_Progress";

export interface Mission {
  id:        number;
  name:      string;
  briefing:  string;   // shown on campaign screen
  objective: string;   // shown in-game subtitle at start
  startFood: number;
  startWood: number;
  startGold: number;
  castleAge: boolean;
}

export const MISSIONS: Mission[] = [
  {
    id:       0,
    name:     "İlk Savaş",
    briefing: "Küçük bir düşman kuvveti kapıya dayanıyor. Tüm düşman birimi ve binalarını yok et.",
    objective:"Hedef: Tüm düşmanları yok et.",
    startFood: 400, startWood: 300, startGold: 100, castleAge: false,
  },
  {
    id:       1,
    name:     "Kaynak Savaşı",
    briefing: "Orta haritadaki altın madenlerini ele geçir. Toplam 1500 altın topla ve Castle Çağı'na ulaş.",
    objective:"Hedef: 1500 altın topla ve Castle Çağı'na ulaş.",
    startFood: 600, startWood: 500, startGold: 200, castleAge: false,
  },
  {
    id:       2,
    name:     "İmparatorun Seferi",
    briefing: "İmparatorluk Çağı'na ulaş ve düşmanın kalesini yık. Bu sefer ne pahasına olursa olsun kazanmalısın.",
    objective:"Hedef: Imperial Çağ'a ulaş, ardından tüm düşmanları yok et.",
    startFood: 800, startWood: 600, startGold: 400, castleAge: true,
  },
];

/** Active mission ID; -1 = no campaign active. Module-level state like Unity's static class. */
export let activeMissionId = -1;
export function setActiveMission(id: number): void { activeMissionId = id; }
export function clearActiveMission(): void { activeMissionId = -1; }

// ── Progress ──────────────────────────────────────────────────────────────────

export function isMissionUnlocked(id: number): boolean {
  if (id === 0) return true;
  return localStorage.getItem(`${SAVE_KEY}_${id}_unlocked`) === '1';
}

export function isMissionComplete(id: number): boolean {
  return localStorage.getItem(`${SAVE_KEY}_${id}_done`) === '1';
}

export function unlockMission(id: number): void {
  localStorage.setItem(`${SAVE_KEY}_${id}_unlocked`, '1');
}

export function completeMission(id: number): void {
  localStorage.setItem(`${SAVE_KEY}_${id}_done`, '1');
  if (id + 1 < MISSIONS.length) unlockMission(id + 1);
}

export function resetProgress(): void {
  for (const m of MISSIONS) {
    localStorage.removeItem(`${SAVE_KEY}_${m.id}_unlocked`);
    localStorage.removeItem(`${SAVE_KEY}_${m.id}_done`);
  }
}

// ── Setup ─────────────────────────────────────────────────────────────────────

/**
 * Called inside startGame when activeMissionId >= 0.
 * Applies starting resources, sets Castle Age if needed, and injects win/fail triggers.
 */
export function setupCampaign(teamRes: ResourceManager[], triggerSys: TriggerSystem): string {
  if (activeMissionId < 0 || activeMissionId >= MISSIONS.length) return "";
  const m = MISSIONS[activeMissionId];

  const rm = teamRes[0];
  if (!rm) return "";
  rm.food  = Math.max(rm.food,  m.startFood);
  rm.wood  = Math.max(rm.wood,  m.startWood);
  rm.gold  = Math.max(rm.gold,  m.startGold);
  if (m.castleAge) rm.age = Age.Castle;

  triggerSys.clear();
  _buildTriggers(m.id, triggerSys);
  return m.objective;
}

function _buildTriggers(id: number, ts: TriggerSystem): void {
  switch (id) {
    case 0: // Eliminate all enemies; 10-minute time limit
      ts.add(makeTrigger({
        id: 0, conditionType: 'EnemyEliminated',
        effectType: 'YouWin', effectStr1: "Görev tamamlandı! Tüm düşmanlar yok edildi.",
      }));
      ts.add(makeTrigger({
        id: 1, conditionType: 'Timer', condFloat1: 600,
        effectType: 'YouLose', effectStr1: "Süre doldu — takviye yetişemedi!",
      }));
      break;

    case 1: // Gather 1500 gold AND reach Castle Age — two chained triggers
      ts.add(makeTrigger({
        id: 10, conditionType: 'ResourceGathered',
        condInt1: 0, condInt2: ResourceKind.Gold, condFloat1: 1500,
        effectType: 'ActivateTrigger', effectInt1: 12,
        effect2Type: 'ShowMessage', effect2Str1: "1500 altın toplandı! Castle Çağı'na geç.",
      }));
      ts.add(makeTrigger({
        id: 11, conditionType: 'AgeReached', condInt1: 0, condInt2: Age.Castle,
        effectType: 'ActivateTrigger', effectInt1: 14,
        effect2Type: 'ShowMessage', effect2Str1: "Castle Çağı! 1500 altın hedefini tamamla.",
      }));
      ts.add(makeTrigger({
        id: 12, enabled: false, conditionType: 'AgeReached', condInt1: 0, condInt2: Age.Castle,
        effectType: 'YouWin', effectStr1: "Görev tamamlandı! Ekonomi gücü kanıtlandı.",
      }));
      ts.add(makeTrigger({
        id: 14, enabled: false, conditionType: 'ResourceGathered',
        condInt1: 0, condInt2: ResourceKind.Gold, condFloat1: 1500,
        effectType: 'YouWin', effectStr1: "Görev tamamlandı! Ekonomi gücü kanıtlandı.",
      }));
      ts.add(makeTrigger({
        id: 13, conditionType: 'Timer', condFloat1: 900,
        effectType: 'YouLose', effectStr1: "Süre doldu!",
      }));
      break;

    case 2: // Reach Imperial Age then eliminate all enemies
      ts.add(makeTrigger({
        id: 20, conditionType: 'AgeReached', condInt1: 0, condInt2: Age.Imperial,
        effectType: 'ActivateTrigger', effectInt1: 21,
        effect2Type: 'ShowMessage', effect2Str1: "İmparatorluk Çağı! Şimdi düşmanı ez.",
      }));
      ts.add(makeTrigger({
        id: 21, enabled: false, conditionType: 'EnemyEliminated',
        effectType: 'YouWin', effectStr1: "Sefer başarıyla tamamlandı! İmparatorluk hüküm sürdü.",
      }));
      break;
  }
}

/** Call when player wins a campaign mission. */
export function onCampaignWin(): void {
  if (activeMissionId < 0) return;
  completeMission(activeMissionId);
  activeMissionId = -1;
}

/** Call when player loses or quits a campaign mission. */
export function abortCampaign(): void { activeMissionId = -1; }
