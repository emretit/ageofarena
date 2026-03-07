export const GAME_CONFIG = {
  // Harita boyutu (tile sayısı) - dikey arena
  mapTilesW: 20,
  mapTilesH: 40,
  tileSize: 2, // Her tile 2x2 world unit

  // Kamera
  cameraZoom: 10,
  cameraAngle: Math.PI / 6, // 30 derece (AoE2 izometrik açı)

  // Başlangıç kaynakları
  startingResources: {
    food: 200,
    wood: 200,
    gold: 100,
  },

  // Renkler
  colors: {
    grass: 0x4a7c3f,
    grassDark: 0x3d6b34,
    playerBase: 0x2a5a2a,
    enemyBase: 0x5a2a2a,
    gridLine: 0x3a6a30,
  },
} as const;

export type ResourceType = 'food' | 'wood' | 'gold';
