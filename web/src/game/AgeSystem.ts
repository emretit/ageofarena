/**
 * AgeSystem.ts — Age advancement port of TechDefs / ResearchSystem age-up logic.
 * Costs and timers from TechDefs.cs (Unity source of truth).
 *
 * Dark  → Feudal:   400 food /  0 gold / 25 s
 * Feudal → Castle:  600 food / 200 gold / 35 s
 * Castle → Imperial:1000 food/ 600 gold / 50 s
 */
import { Age } from "../core/GameTypes";
import { ResourceManager } from "../core/ResourceManager";

interface AgeUpDef {
  food: number;
  gold: number;
  time: number;
  label: string;
}

const AGE_DEFS: Record<Age, AgeUpDef | null> = {
  [Age.Dark]:     { food: 400, gold:   0, time: 25, label: "Feudal Age" },
  [Age.Feudal]:   { food: 600, gold: 200, time: 35, label: "Castle Age" },
  [Age.Castle]:   { food:1000, gold: 600, time: 50, label: "Imperial Age" },
  [Age.Imperial]: null,
};

export const AGE_NAMES: Record<Age, string> = {
  [Age.Dark]:     "Dark Age",
  [Age.Feudal]:   "Feudal Age",
  [Age.Castle]:   "Castle Age",
  [Age.Imperial]: "Imperial Age",
};

export class AgeSystem {
  /** -1 = not researching; >=0 = seconds remaining. */
  ageUpTimer = -1;
  private totalTime = 0;
  /** Called when age-up completes (cosmetic seam). */
  onAgeUp: (() => void) | null = null;

  /** Returns the definition for advancing FROM the current age, or null if at Imperial. */
  nextAgeDef(rm: ResourceManager): AgeUpDef | null {
    return AGE_DEFS[rm.age] ?? null;
  }

  /**
   * Start age-up research. Returns true on success (cost deducted).
   * Fails if already researching, at Imperial, or can't afford.
   */
  startAgeUp(rm: ResourceManager): boolean {
    if (this.ageUpTimer >= 0) return false; // already in progress
    const def = this.nextAgeDef(rm);
    if (!def) return false; // already Imperial
    if (!rm.canAfford(def.food, 0, def.gold)) return false;
    rm.deduct(def.food, 0, def.gold);
    this.ageUpTimer = def.time;
    this.totalTime  = def.time;
    return true;
  }

  /** Progress [0..1] of current age-up, or -1 if idle. */
  progress(): number {
    if (this.ageUpTimer < 0 || this.totalTime <= 0) return -1;
    return 1 - this.ageUpTimer / this.totalTime;
  }

  tick(rm: ResourceManager, dt: number) {
    if (this.ageUpTimer < 0) return;
    this.ageUpTimer -= dt;
    if (this.ageUpTimer <= 0) {
      this.ageUpTimer = -1;
      rm.age = Math.min(Age.Imperial, rm.age + 1) as Age;
      rm.onChange?.();
      this.onAgeUp?.();
    }
  }
}
