/**
 * CivilizationDefs.cs port — civ identities and per-civ bonuses.
 * Bonuses are data-only; systems read them and apply multipliers in their own tick.
 */

export const enum Civilization {
  None,
  Franks, Britons, Mongols, Japanese, Byzantines,
  Aztecs, Teutons, Persians, Vikings, Saracens,
  Celts, Chinese, Goths, Turks,
}

export interface CivBonus {
  civ:                  Civilization;
  display:              string;
  flag:                 string; // emoji flag for map-select UI

  // Economy
  gatherFoodMult:       number; // Franks: 1.2
  gatherWoodMult:       number;
  gatherGoldMult:       number;
  farmDecayMult:        number; // 1.0 = normal; Franks 0.5 = half decay

  // Military
  cavalryHpMult:        number; // Franks: 1.2
  archerRangeBonus:     number; // Britons: +1
  cavalrySpeedMult:     number; // Mongols: 1.25
  infantryAttackMult:   number; // Japanese: 1.1
  buildingHpMult:       number; // Byzantines: 1.1
  healRateMult:         number; // Byzantines: 1.5

  // New in M9
  archerAttackMult:     number; // Vikings/Saracens
  unitTrainTimeMult:    number; // Mongols/Aztecs: 0.8 = 20% faster

  // Shared team bonus
  teamGatherFoodBonus:  number; // additive fraction (0.05 = +5%)
}

function make(
  civ: Civilization,
  display: string,
  flag: string,
  overrides: Partial<CivBonus> = {},
): CivBonus {
  return {
    civ, display, flag,
    gatherFoodMult: 1, gatherWoodMult: 1, gatherGoldMult: 1, farmDecayMult: 1,
    cavalryHpMult: 1, archerRangeBonus: 0, cavalrySpeedMult: 1,
    infantryAttackMult: 1, buildingHpMult: 1, healRateMult: 1,
    archerAttackMult: 1, unitTrainTimeMult: 1, teamGatherFoodBonus: 0,
    ...overrides,
  };
}

export const CIVILIZATION_DEFS: Record<Civilization, CivBonus> = {
  [Civilization.None]:       make(Civilization.None,       "Belirsiz",   "⬜"),
  [Civilization.Franks]:     make(Civilization.Franks,     "Franklar",   "🇫🇷", { gatherFoodMult: 1.2, cavalryHpMult: 1.2, farmDecayMult: 0.5 }),
  [Civilization.Britons]:    make(Civilization.Britons,    "Britonyalılar","🇬🇧", { archerRangeBonus: 1 }),
  [Civilization.Mongols]:    make(Civilization.Mongols,    "Moğollar",   "🇲🇳", { cavalrySpeedMult: 1.25, unitTrainTimeMult: 0.8 }),
  [Civilization.Japanese]:   make(Civilization.Japanese,   "Japonlar",   "🇯🇵", { infantryAttackMult: 1.1 }),
  [Civilization.Byzantines]: make(Civilization.Byzantines, "Bizanslılar","🇬🇷", { buildingHpMult: 1.1, healRateMult: 1.5 }),
  [Civilization.Aztecs]:     make(Civilization.Aztecs,     "Aztekler",   "🇲🇽", { unitTrainTimeMult: 0.85, teamGatherFoodBonus: 0.05 }),
  [Civilization.Teutons]:    make(Civilization.Teutons,    "Tötonlar",   "🇩🇪", { buildingHpMult: 1.15, cavalryHpMult: 1.1 }),
  [Civilization.Persians]:   make(Civilization.Persians,   "Persler",    "🇮🇷", { gatherFoodMult: 1.05, gatherGoldMult: 1.05 }),
  [Civilization.Vikings]:    make(Civilization.Vikings,    "Vikingler",  "🇳🇴", { archerAttackMult: 1.1 }),
  [Civilization.Saracens]:   make(Civilization.Saracens,   "Sarazenler", "⚔️", { archerAttackMult: 1.05, gatherGoldMult: 1.1 }),
  [Civilization.Celts]:      make(Civilization.Celts,      "Keltler",    "🏴󠁧󠁢󠁳󠁣󠁴󠁿", { infantryAttackMult: 1.15, gatherWoodMult: 1.15 }),
  [Civilization.Chinese]:    make(Civilization.Chinese,    "Çinliler",   "🇨🇳", { gatherFoodMult: 1.1, teamGatherFoodBonus: 0.05 }),
  [Civilization.Goths]:      make(Civilization.Goths,      "Gotlar",     "⚔️", { infantryAttackMult: 1.2, unitTrainTimeMult: 0.75 }),
  [Civilization.Turks]:      make(Civilization.Turks,      "Türkler",    "🇹🇷", { gatherGoldMult: 1.2, unitTrainTimeMult: 0.9 }),
};

/** Civs available in the selection screen (original 5 + M9 expansion). */
export const PLAYABLE_CIVS: Civilization[] = [
  Civilization.Franks, Civilization.Britons, Civilization.Mongols,
  Civilization.Japanese, Civilization.Byzantines,
  Civilization.Aztecs, Civilization.Teutons, Civilization.Persians,
  Civilization.Vikings, Civilization.Saracens,
  Civilization.Celts, Civilization.Chinese, Civilization.Goths, Civilization.Turks,
];
