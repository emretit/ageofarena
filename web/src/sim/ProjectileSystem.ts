/**
 * ProjectileSystem — sim-side projectile tracking.
 * Ranged attacks fire a projectile; damage is applied on arrival.
 * Zero three.js imports — pure sim layer.
 */

export interface SimProjectile {
  /** Current world position */
  x: number;
  z: number;
  y: number; // arc height (view only, not used for hit detection)
  /** Destination */
  goalX: number;
  goalZ: number;
  /** Attacker stats frozen at fire time */
  damage: number;
  splash: number;        // world-unit splash radius; 0 = no splash
  friendlyFire: boolean;
  attackerTeam: number;
  /** Target (null if it died mid-flight) */
  targetId: number;      // entity id; -1 for ground-targeted splash
  /** Travel */
  speed: number;         // world units / second
  alive: boolean;
}

export interface ProjTarget {
  id: number;
  x: number;
  z: number;
  alive: boolean;
  teamId: number;
  takeDamage(amount: number): void;
}

/** Callback for landing notification (view layer hooks here for SFX/VFX). */
export type OnLand = (p: SimProjectile, hit: ProjTarget | null) => void;

export class ProjectileSystem {
  private readonly _projs: SimProjectile[] = [];
  onLand: OnLand | null = null;

  fire(
    fromX: number, fromZ: number,
    goalX: number, goalZ: number,
    damage: number, splash: number, friendlyFire: boolean,
    attackerTeam: number, targetId: number,
    speed = 20,
  ): void {
    this._projs.push({
      x: fromX, z: fromZ, y: 0,
      goalX, goalZ, damage, splash, friendlyFire,
      attackerTeam, targetId, speed, alive: true,
    });
  }

  tick(targets: ProjTarget[], dt: number): void {
    for (const p of this._projs) {
      if (!p.alive) continue;

      const dx = p.goalX - p.x;
      const dz = p.goalZ - p.z;
      const dist2 = dx * dx + dz * dz;

      if (dist2 <= (p.speed * dt) * (p.speed * dt)) {
        // Arrived
        p.alive = false;
        const tgt = targets.find(t => t.id === p.targetId && t.alive) ?? null;

        if (p.splash > 0) {
          // Splash at landing point — hit all targets in radius
          const r2 = p.splash * p.splash;
          for (const t of targets) {
            if (!t.alive) continue;
            if (!p.friendlyFire && t.teamId === p.attackerTeam) continue;
            const sdx = t.x - p.goalX; const sdz = t.z - p.goalZ;
            const sd2 = sdx * sdx + sdz * sdz;
            if (sd2 <= r2) {
              const falloff = 1 - Math.sqrt(sd2) / p.splash;
              t.takeDamage(Math.max(1, Math.round(p.damage * falloff)));
            }
          }
        } else if (tgt) {
          tgt.takeDamage(p.damage);
        }

        this.onLand?.(p, tgt);
      } else {
        const dist = Math.sqrt(dist2);
        const step = p.speed * dt;
        p.x += (dx / dist) * step;
        p.z += (dz / dist) * step;
        // Parabolic arc for view layer (peak at midpoint)
        const t = 1 - dist2 / ((p.goalX - p.x + dx) ** 2 + (p.goalZ - p.z + dz) ** 2 + 0.001);
        p.y = 4 * t * (1 - t) * 3; // max 3 world-units high
      }
    }

    // Compact dead projectiles
    for (let i = this._projs.length - 1; i >= 0; i--) {
      if (!this._projs[i].alive) this._projs.splice(i, 1);
    }
  }

  get projectiles(): readonly SimProjectile[] { return this._projs; }
}
