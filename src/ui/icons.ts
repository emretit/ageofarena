import { UnitId } from '../entities/types';

export function getBuildingIcon(id: string): string {
  const icons: Record<string, string> = {
    townCenter: '🏛️',
    house: '🏠',
    barracks: '⚔️',
    archeryRange: '🏹',
    stable: '🐴',
    blacksmith: '🔨',
    market: '💰',
    castle: '🏰',
  };
  return icons[id] || '🏗️';
}

export function getUnitIcon(id: UnitId): string {
  const icons: Record<string, string> = {
    villager: '👷',
    militia: '⚔️',
    spearman: '🗡️',
    archer: '🏹',
    skirmisher: '🎯',
    scoutCavalry: '🐎',
    knight: '🛡️',
    monk: '📿',
    tradeCart: '🛒',
  };
  return icons[id] || '👤';
}
