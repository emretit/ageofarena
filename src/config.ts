export interface BaseConfig {
  center: { x: number; z: number };
  teamColor: number;
  roofColor: number;
  gateAngles: number[];
  label: string;
}

export const GAME_CONFIG = {
  // Harita boyutu (tile sayısı) - büyük kare arena
  mapTilesW: 80,
  mapTilesH: 80,
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

  // Duvar çevreleme parametreleri
  wallEnclosure: {
    radiusX: 12,
    radiusZ: 10,
    wallHeight: 3,
    wallThickness: 0.5,
    segmentCount: 32,
    gateWidth: 3,
  },

  // 4 üs tanımları - Arena modu (baklava/diamond düzeni)
  bases: [
    {
      center: { x: 80, z: 130 },
      teamColor: 0xcc4444,
      roofColor: 0x8b2500,
      gateAngles: [-Math.PI / 2],         // kuzey (merkeze doğru)
      label: 'Player 1',
    },
    {
      center: { x: 80, z: 30 },
      teamColor: 0x4444cc,
      roofColor: 0x4444cc,
      gateAngles: [Math.PI / 2],          // güney (merkeze doğru)
      label: 'Player 2',
    },
    {
      center: { x: 30, z: 80 },
      teamColor: 0x44aa44,
      roofColor: 0x2a7a2a,
      gateAngles: [0],                    // doğu (merkeze doğru)
      label: 'Player 3',
    },
    {
      center: { x: 130, z: 80 },
      teamColor: 0xccaa22,
      roofColor: 0xaa8800,
      gateAngles: [Math.PI],             // batı (merkeze doğru)
      label: 'Player 4',
    },
  ] as BaseConfig[],

  // Orman
  forest: {
    density: 0.4,
    clearingWidth: 4,
    edgeForestDepth: 25, // harita kenarından içeri orman derinliği
    interBaseForestDensity: 0.2, // üsler arası orman yoğunluğu
  },

  // Terrain
  terrain: {
    subdivisions: 2,    // per tile subdivision (reduced from 4 for performance)
    noiseScale1: 0.07,  // large-scale variation
    noiseScale2: 0.18,  // medium detail
    noiseScale3: 0.4,   // fine detail
    heightScale: 0.12,  // subtle Y displacement
    pathWidth: 1.8,     // dirt path half-width
  },

  // Renkler
  colors: {
    grass: 0x4a7c3f,
    grassDark: 0x3d6b34,
    grassYellow: 0x6a8a3a,
    dirt: 0x7a6b4a,
    dirtDark: 0x5a4e38,
    path: 0x8a7a5e,
    playerBase: 0x2a5a2a,
    enemyBase: 0x5a2a2a,
    gridLine: 0x3a6a30,
    stoneWall: 0x8a8378,
    stoneDark: 0x6b6560,
    woodGate: 0x5c3a1e,
    roofRed: 0x8b2500,
    roofBlue: 0x4444cc,
    treeTrunk: 0x5c3a1e,
    treeLeafDark: 0x2d5a1e,
    treeLeafLight: 0x3d7a2e,
    treePine: 0x1a4a0e,
  },
} as const;

export type ResourceType = 'food' | 'wood' | 'gold';
