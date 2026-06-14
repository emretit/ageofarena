/**
 * CampaignSystem.ts — Port of CampaignSystem.cs (N13.camp).
 * Ordered sequence of scenarios; progress persisted in localStorage.
 */
import { Age, BuildingType, ResourceKind } from "../core/GameTypes";
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
  {
    id:       3,
    name:     "Deniz Savaşı",
    briefing: "Adalarda düşman bir donanma üssü kuruldu. Filo gönder, düşman Dock ve tüm deniz kuvvetlerini yok et.",
    objective:"Hedef: Tüm düşman gemileri ve Dock'u yok et.",
    startFood: 600, startWood: 800, startGold: 300, castleAge: false,
  },
  {
    id:       4,
    name:     "İmparator'un Kalkını",
    briefing: "Bir Wonder inşa et ve 300 saniye boyunca düşman saldırılarına dayanarak zafer kazan.",
    objective:"Hedef: Wonder inşa et ve 300 saniye boyunca koru.",
    startFood: 1000, startWood: 1000, startGold: 800, castleAge: true,
  },
  {
    id:       5,
    name:     "[AoW] Okçuluk",
    briefing: "Savaş Sanatı — Okçuluk: 10 Okçu ile düşman piyade kuvvetini 3 dakika içinde yok et. Birim kaybını minimumda tut.",
    objective:"Hedef: Tüm düşman birimi yok et (3 dk limit). Kayıp sayısını azalt.",
    startFood: 800, startWood: 500, startGold: 400, castleAge: false,
  },
  {
    id:       6,
    name:     "[AoW] Süvari Akını",
    briefing: "Savaş Sanatı — Süvari Akını: 6 Süvari ile düşmanın eko binalarını (Değirmen, Odun Kampı) yak. 5 bina yık.",
    objective:"Hedef: 5 düşman eko binasını yık (10 dk limit).",
    startFood: 1200, startWood: 0, startGold: 600, castleAge: true,
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

    case 3: // Naval: destroy all enemy ships and their Dock (EnemyEliminated covers all units+buildings)
      ts.add(makeTrigger({
        id: 30, conditionType: 'Timer', condFloat1: 30,
        effectType: 'ShowMessage', effect2Str1: "",
        effectStr1: "Deniz Savaşı başlıyor! Dock inşa et, Galley eğit ve düşman donanmasını yok et.",
      }));
      ts.add(makeTrigger({
        id: 31, conditionType: 'EnemyEliminated',
        effectType: 'YouWin', effectStr1: "Deniz zafer! Düşman donanması yok edildi.",
      }));
      ts.add(makeTrigger({
        id: 32, conditionType: 'Timer', condFloat1: 720,
        effectType: 'YouLose', effectStr1: "Süre doldu — düşman donanması hâlâ denizde!",
      }));
      break;

    case 4: {
      // Wonder: build Wonder, then defend until 1200s game time (20 min with Castle Age start)
      // Chain: Wonder built → activate win-timer trigger; absolute 1500s timeout = fail
      ts.add(makeTrigger({
        id: 40, conditionType: 'Timer', condFloat1: 10,
        effectType: 'ShowMessage', effectStr1: "Wonder inşa et ve 300 saniye boyunca koru — düşman saldıracak!",
      }));
      ts.add(makeTrigger({
        // Wonder built (condInt2 = BuildingType.Wonder = 12, condFloat1 = 1 = min count)
        id: 41, conditionType: 'OwnBuildings', condInt1: 0,
        condInt2: BuildingType.Wonder, condFloat1: 1,
        effectType: 'ActivateTrigger', effectInt1: 43,
        effect2Type: 'ShowMessage', effect2Str1: "Wonder tamamlandı! Şimdi 300 saniye boyunca savun.",
      }));
      ts.add(makeTrigger({
        // Win if Wonder still standing AND enough time has passed (fires when timer hits 1200s)
        id: 43, enabled: false, conditionType: 'Timer', condFloat1: 1200,
        effectType: 'YouWin', effectStr1: "Wonder savunuldu! İmparatorluk zaferi kazanıldı.",
      }));
      ts.add(makeTrigger({
        id: 44, conditionType: 'Timer', condFloat1: 1500,
        effectType: 'YouLose', effectStr1: "Süre doldu — Wonder tamamlanamadı!",
      }));
      break;
    }

    case 5: // Art of War — Archery: eliminate enemies in 3 minutes
      ts.add(makeTrigger({
        id: 50, conditionType: 'Timer', condFloat1: 5,
        effectType: 'ShowMessage', effectStr1: "10 Okçunla düşman piyade kuvvetini yok et! Hız ve kayıp sayısı önemli.",
      }));
      ts.add(makeTrigger({
        id: 51, conditionType: 'EnemyEliminated',
        effectType: 'YouWin', effectStr1: "Mükemmel! Okçuluk taktigi ustalandi.",
      }));
      ts.add(makeTrigger({
        id: 52, conditionType: 'Timer', condFloat1: 180,
        effectType: 'YouLose', effectStr1: "3 dakika doldu! Düşman hâlâ sahada.",
      }));
      break;

    case 6: // Art of War — Cavalry Raid: destroy 5 eco buildings
      ts.add(makeTrigger({
        id: 60, conditionType: 'Timer', condFloat1: 5,
        effectType: 'ShowMessage', effectStr1: "Süvarilerle düşmanın eko binalarına sal! Değirmen ve Odun Kampları hedef.",
      }));
      ts.add(makeTrigger({
        id: 61, conditionType: 'EnemyEliminated',
        effectType: 'YouWin', effectStr1: "Hasar verildi! Düşman ekonomisi çöktü.",
      }));
      ts.add(makeTrigger({
        id: 62, conditionType: 'Timer', condFloat1: 600,
        effectType: 'YouLose', effectStr1: "Süre doldu! Süvari akını yetersiz kaldı.",
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
