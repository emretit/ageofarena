/**
 * Minimap.ts — MinimapSystem.cs port (simplified: 2D canvas instead of secondary camera).
 * Bottom-right corner diamond-shaped minimap with fog overlay, unit/building blips.
 * Refresh ~10 Hz to match Unity's RefreshInterval = 0.1 s.
 */
import type { Unit } from "../game/Unit";
import type { Building } from "../game/Building";
import type { ResourceNode } from "../game/ResourceNode";
import type { FogOfWarSystem } from "../game/FogOfWarSystem";
import type { CameraRig } from "../camera/CameraRig";
import { ResourceKind } from "../core/GameTypes";

const MAP_WORLD  = 180;   // world units captured (covers BaseDistance=84 bases)
const SIDE       = 140;   // canvas pixels
const REFRESH    = 0.1;   // seconds between redraws

const TEAM_COLORS = ["#3af", "#f44"];
const RES_COLORS: Record<number, string> = {
  [ResourceKind.Food]:  "#5d4",
  [ResourceKind.Wood]:  "#873",
  [ResourceKind.Gold]:  "#fd3",
  [ResourceKind.Stone]: "#aaa",
};

function worldToMap(w: number): number {
  return ((w + MAP_WORLD / 2) / MAP_WORLD) * SIDE;
}

export class Minimap {
  private readonly canvas: HTMLCanvasElement;
  private readonly ctx: CanvasRenderingContext2D;
  private timer = 0;
  /** Cached offscreen canvas for fog ImageData blit — avoids per-draw allocation. */
  private readonly fogBlit: HTMLCanvasElement;
  private readonly fogBlitCtx: CanvasRenderingContext2D;
  /** Reused fog ImageData — avoids a SIDE×SIDE×4 allocation on every redraw. */
  private _fogImg?: ImageData;

  /** Called when user clicks the minimap — world X and Z coordinates. */
  onNavigate: ((x: number, z: number) => void) | null = null;

  constructor(container: HTMLElement) {
    this.canvas = document.createElement("canvas");
    this.canvas.width  = SIDE;
    this.canvas.height = SIDE;
    Object.assign(this.canvas.style, {
      position:   "absolute",
      bottom:     "10px",
      right:      "10px",
      width:      `${SIDE}px`,
      height:     `${SIDE}px`,
      border:     "2px solid #444",
      borderRadius: "2px",
      cursor:     "crosshair",
      pointerEvents: "auto",
    });

    this.ctx = this.canvas.getContext("2d")!;

    this.fogBlit = document.createElement("canvas");
    this.fogBlit.width = this.fogBlit.height = SIDE;
    this.fogBlitCtx = this.fogBlit.getContext("2d")!;

    container.appendChild(this.canvas);

    this.canvas.addEventListener("click", (e) => {
      const rect = this.canvas.getBoundingClientRect();
      const px = e.clientX - rect.left;
      const py = e.clientY - rect.top;
      // Convert minimap pixel → world coordinate
      const wx = (px / SIDE) * MAP_WORLD - MAP_WORLD / 2;
      const wz = (py / SIDE) * MAP_WORLD - MAP_WORLD / 2;
      this.onNavigate?.(wx, wz);
    });
  }

  tick(
    units: Unit[],
    buildings: Building[],
    nodes: ResourceNode[],
    fog: FogOfWarSystem,
    dt: number,
  ) {
    this.timer -= dt;
    if (this.timer > 0) return;
    this.timer = REFRESH;
    this._draw(units, buildings, nodes, fog);
  }

  private _draw(
    units: Unit[],
    buildings: Building[],
    nodes: ResourceNode[],
    fog: FogOfWarSystem,
  ) {
    const ctx = this.ctx;
    ctx.clearRect(0, 0, SIDE, SIDE);

    // ── Background (terrain color) ─────────────────────────────────────────
    ctx.fillStyle = "#4a7a35";
    ctx.fillRect(0, 0, SIDE, SIDE);

    // ── Fog overlay ────────────────────────────────────────────────────────
    // Sample fog grid to shade minimap pixels
    const fogGrid = (fog as unknown as { vis: Uint8Array; explored: Uint8Array });
    if (fogGrid.vis) {
      const TEX_SIZE  = 128;
      const FOG_WORLD = 180; // must match FogOfWarSystem WORLD_HALF*2
      const ratio     = SIDE / TEX_SIZE;
      const scaleX    = (TEX_SIZE / FOG_WORLD) * (MAP_WORLD / SIDE);
      const scaleZ    = (TEX_SIZE / FOG_WORLD) * (MAP_WORLD / SIDE);

      // Draw fog as semi-transparent overlay using image data (reused buffer)
      const imgData = this._fogImg ??= ctx.createImageData(SIDE, SIDE);
      for (let py = 0; py < SIDE; py++) {
        for (let px = 0; px < SIDE; px++) {
          // Map minimap pixel → fog grid cell
          const fogX = Math.floor((px / SIDE) * TEX_SIZE);
          const fogZ = Math.floor((py / SIDE) * TEX_SIZE);
          const fogZflipped = TEX_SIZE - 1 - fogZ; // fog grid row 0 = world Z=-90
          const clampedX = Math.max(0, Math.min(TEX_SIZE - 1, fogX));
          const clampedZ = Math.max(0, Math.min(TEX_SIZE - 1, fogZflipped));
          const tier = fogGrid.vis[clampedZ * TEX_SIZE + clampedX];
          const off  = (py * SIDE + px) * 4;
          if (tier === 2) {
            // visible — transparent
            imgData.data[off + 3] = 0;
          } else if (tier === 1) {
            // shroud — dark overlay
            imgData.data[off + 3] = 120;
          } else {
            // unexplored — black
            imgData.data[off + 3] = 220;
          }
        }
      }
      this.fogBlitCtx.putImageData(imgData, 0, 0);
      ctx.drawImage(this.fogBlit, 0, 0);
    }

    // ── Resource nodes ─────────────────────────────────────────────────────
    for (const n of nodes) {
      if (n.depleted) continue;
      const x = worldToMap(n.root.position.x);
      const z = worldToMap(n.root.position.z);
      if (!fog.isVisible(n.root.position.x, n.root.position.z)) continue;
      ctx.fillStyle = RES_COLORS[n.kind] ?? "#fff";
      ctx.fillRect(x - 1, z - 1, 3, 3);
    }

    // ── Buildings ──────────────────────────────────────────────────────────
    for (const b of buildings) {
      if (!b.alive) continue;
      if (b.teamId !== 0 && !fog.isVisible(b.pos.x, b.pos.z)) continue;
      const x = worldToMap(b.pos.x);
      const z = worldToMap(b.pos.z);
      ctx.fillStyle = TEAM_COLORS[b.teamId] ?? "#fff";
      ctx.fillRect(x - 3, z - 3, 6, 6);
    }

    // ── Units ──────────────────────────────────────────────────────────────
    for (const u of units) {
      if (!u.alive) continue;
      if (u.teamId !== 0 && !fog.isVisible(u.pos.x, u.pos.z)) continue;
      const x = worldToMap(u.pos.x);
      const z = worldToMap(u.pos.z);
      ctx.fillStyle = TEAM_COLORS[u.teamId] ?? "#fff";
      ctx.beginPath();
      ctx.arc(x, z, 2.5, 0, Math.PI * 2);
      ctx.fill();
    }

    // ── Camera viewport indicator ──────────────────────────────────────────
    // (skipped — camera position approximation only)
  }
}
