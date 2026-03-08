import * as THREE from 'three';
import { GAME_CONFIG } from '../config';

interface CameraControls {
  target: THREE.Vector3;
  moveTo: (worldX: number, worldZ: number) => void;
}

export class Minimap {
  private canvas: HTMLCanvasElement;
  private ctx: CanvasRenderingContext2D;
  private camera: THREE.OrthographicCamera;
  private controls: CameraControls;
  private mapW: number;
  private mapH: number;
  private frameCount = 0;
  private isDragging = false;
  private w: number;
  private h: number;
  // Diamond center & half-sizes
  private cx: number;
  private cy: number;
  private dhw: number;
  private dhh: number;

  // Cached static layer (drawn once)
  private staticCanvas: HTMLCanvasElement;
  private staticCtx: CanvasRenderingContext2D;
  private staticDrawn = false;

  // Reusable objects for viewport calculation
  private raycaster = new THREE.Raycaster();
  private groundPlane = new THREE.Plane(new THREE.Vector3(0, 1, 0), 0);
  private cornerNDCs = [
    new THREE.Vector2(-1, -1),
    new THREE.Vector2(1, -1),
    new THREE.Vector2(1, 1),
    new THREE.Vector2(-1, 1),
  ];
  private intersectTarget = new THREE.Vector3();

  constructor(camera: THREE.OrthographicCamera, controls: CameraControls, mapW: number, mapH: number) {
    this.camera = camera;
    this.controls = controls;
    this.mapW = mapW;
    this.mapH = mapH;

    const container = document.getElementById('minimap-container');
    if (!container) throw new Error('minimap-container not found');

    const containerRect = container.getBoundingClientRect();
    this.w = Math.floor(containerRect.width - 8);
    this.h = Math.floor(containerRect.height - 8);

    this.canvas = document.createElement('canvas');
    this.canvas.width = this.w;
    this.canvas.height = this.h;
    container.appendChild(this.canvas);

    const pad = 6;
    this.cx = this.w / 2;
    this.cy = this.h / 2;
    this.dhw = this.w / 2 - pad;
    this.dhh = this.h / 2 - pad;

    this.ctx = this.canvas.getContext('2d')!;

    // Create off-screen canvas for static content (drawn once)
    this.staticCanvas = document.createElement('canvas');
    this.staticCanvas.width = this.w;
    this.staticCanvas.height = this.h;
    this.staticCtx = this.staticCanvas.getContext('2d')!;

    this.canvas.addEventListener('pointerdown', (e) => {
      if (!this.isInsideDiamond(e)) return;
      this.isDragging = true;
      this.canvas.setPointerCapture(e.pointerId);
      this.moveCameraTo(e);
    });
    this.canvas.addEventListener('pointermove', (e) => {
      if (this.isDragging) this.moveCameraTo(e);
    });
    this.canvas.addEventListener('pointerup', () => { this.isDragging = false; });
    this.canvas.addEventListener('pointercancel', () => { this.isDragging = false; });

    this.drawStaticOnce();
  }

  private worldToMinimap(wx: number, wz: number): [number, number] {
    const nx = wx / this.mapW;
    const nz = wz / this.mapH;
    const isoX = (nx - nz) * 0.5;
    const isoY = (nx + nz) * 0.5;
    return [
      this.cx + isoX * this.dhw * 2,
      this.cy + (isoY - 0.5) * this.dhh * 2,
    ];
  }

  private minimapToWorld(mx: number, my: number): [number, number] {
    const isoX = (mx - this.cx) / (this.dhw * 2);
    const isoY = (my - this.cy) / (this.dhh * 2) + 0.5;
    const nx = isoX + isoY;
    const nz = isoY - isoX;
    return [nx * this.mapW, nz * this.mapH];
  }

  private isInsideDiamond(e: PointerEvent): boolean {
    const rect = this.canvas.getBoundingClientRect();
    const mx = (e.clientX - rect.left) * (this.w / rect.width);
    const my = (e.clientY - rect.top) * (this.h / rect.height);
    return Math.abs(mx - this.cx) / this.dhw + Math.abs(my - this.cy) / this.dhh <= 1;
  }

  private diamondPath(ctx: CanvasRenderingContext2D): void {
    ctx.beginPath();
    ctx.moveTo(this.cx, this.cy - this.dhh);
    ctx.lineTo(this.cx + this.dhw, this.cy);
    ctx.lineTo(this.cx, this.cy + this.dhh);
    ctx.lineTo(this.cx - this.dhw, this.cy);
    ctx.closePath();
  }

  /** Draw static content once to off-screen canvas */
  private drawStaticOnce(): void {
    if (this.staticDrawn) return;
    this.staticDrawn = true;

    const ctx = this.staticCtx;
    const { wallEnclosure, bases } = GAME_CONFIG;

    ctx.clearRect(0, 0, this.w, this.h);

    // Dark background
    ctx.fillStyle = '#1a0e04';
    ctx.fillRect(0, 0, this.w, this.h);

    // Diamond clip
    ctx.save();
    this.diamondPath(ctx);
    ctx.fillStyle = '#2a1a0a';
    ctx.fill();
    ctx.clip();

    // Green ground
    ctx.fillStyle = '#4a7c3f';
    ctx.fillRect(0, 0, this.w, this.h);

    // Edge forests
    this.drawEdgeForests(ctx);

    // Team colors
    const teamColors = ['#cc4444', '#4444cc', '#44aa44', '#ccaa22'];

    for (let i = 0; i < bases.length; i++) {
      const base = bases[i];
      const color = teamColors[i];

      this.drawWallOval(ctx, base.center.x, base.center.z,
        wallEnclosure.radiusX, wallEnclosure.radiusZ, color);

      this.drawDot(ctx, base.center.x, base.center.z, '#ffcc00', 3);

      const bx = base.center.x;
      const bz = base.center.z;
      this.drawDot(ctx, bx - 5, bz - 4, color, 1.5);
      this.drawDot(ctx, bx + 5, bz - 4, color, 1.5);
      this.drawDot(ctx, bx - 5, bz + 4, color, 1.5);
      this.drawDot(ctx, bx + 5, bz + 4, color, 1.5);
      this.drawDot(ctx, bx, bz - 6, color, 2);
    }

    ctx.restore();

    // Outer frame
    ctx.strokeStyle = '#3a2a18';
    ctx.lineWidth = 4;
    this.diamondPath(ctx);
    ctx.stroke();

    ctx.strokeStyle = '#6a5a38';
    ctx.lineWidth = 2;
    this.diamondPath(ctx);
    ctx.stroke();

    ctx.strokeStyle = '#9a8a58';
    ctx.lineWidth = 1;
    const inset = 3;
    ctx.beginPath();
    ctx.moveTo(this.cx, this.cy - this.dhh + inset);
    ctx.lineTo(this.cx + this.dhw - inset * (this.dhw / this.dhh), this.cy);
    ctx.lineTo(this.cx, this.cy + this.dhh - inset);
    ctx.lineTo(this.cx - this.dhw + inset * (this.dhw / this.dhh), this.cy);
    ctx.closePath();
    ctx.stroke();
  }

  private drawEdgeForests(ctx: CanvasRenderingContext2D): void {
    const edgeDepth = GAME_CONFIG.forest.edgeForestDepth;
    const steps = 50;

    for (let i = 0; i <= steps; i++) {
      for (let j = 0; j <= steps; j++) {
        const wx = (i / steps) * this.mapW;
        const wz = (j / steps) * this.mapH;

        const distLeft = wx;
        const distRight = this.mapW - wx;
        const distTop = wz;
        const distBottom = this.mapH - wz;
        const minEdgeDist = Math.min(distLeft, distRight, distTop, distBottom);

        if (minEdgeDist < edgeDepth) {
          const [mx, my] = this.worldToMinimap(wx, wz);
          const opacity = (1 - minEdgeDist / edgeDepth) * 0.55;
          ctx.fillStyle = `rgba(26, 74, 14, ${opacity})`;
          ctx.fillRect(mx - 2, my - 1.5, 4, 3);
        }
      }
    }
  }

  private drawWallOval(ctx: CanvasRenderingContext2D, cx: number, cz: number,
    rx: number, rz: number, color: string): void {
    ctx.strokeStyle = color;
    ctx.lineWidth = 2;
    ctx.beginPath();

    const segments = 32;
    for (let i = 0; i <= segments; i++) {
      const t = (i / segments) * Math.PI * 2;
      const wx = cx + rx * Math.cos(t);
      const wz = cz + rz * Math.sin(t);
      const [mx, my] = this.worldToMinimap(wx, wz);
      if (i === 0) ctx.moveTo(mx, my);
      else ctx.lineTo(mx, my);
    }
    ctx.closePath();
    ctx.stroke();

    const r = parseInt(color.slice(1, 3), 16);
    const g = parseInt(color.slice(3, 5), 16);
    const b = parseInt(color.slice(5, 7), 16);
    ctx.fillStyle = `rgba(${r}, ${g}, ${b}, 0.1)`;
    ctx.fill();
  }

  private drawDot(ctx: CanvasRenderingContext2D, wx: number, wz: number,
    color: string, radius: number): void {
    const [mx, my] = this.worldToMinimap(wx, wz);
    ctx.fillStyle = color;
    ctx.beginPath();
    ctx.arc(mx, my, radius, 0, Math.PI * 2);
    ctx.fill();
  }

  update(): void {
    this.frameCount++;
    if (this.frameCount % 10 !== 0) return; // Reduced frequency: every 10 frames instead of 5

    // Blit cached static layer (no recalculation)
    this.ctx.drawImage(this.staticCanvas, 0, 0);
    this.drawCameraViewport();
  }

  private drawCameraViewport(): void {
    const cam = this.camera;
    const ctx = this.ctx;

    ctx.save();
    this.diamondPath(ctx);
    ctx.clip();

    const worldCorners: [number, number][] = [];
    for (const corner of this.cornerNDCs) {
      this.raycaster.setFromCamera(corner, cam);
      const hit = this.raycaster.ray.intersectPlane(this.groundPlane, this.intersectTarget);
      if (hit) {
        worldCorners.push(this.worldToMinimap(this.intersectTarget.x, this.intersectTarget.z));
      }
    }

    if (worldCorners.length === 4) {
      ctx.strokeStyle = 'rgba(255, 255, 255, 0.9)';
      ctx.lineWidth = 1.5;
      ctx.beginPath();
      ctx.moveTo(worldCorners[0][0], worldCorners[0][1]);
      for (let i = 1; i < 4; i++) {
        ctx.lineTo(worldCorners[i][0], worldCorners[i][1]);
      }
      ctx.closePath();
      ctx.stroke();

      ctx.fillStyle = 'rgba(255, 255, 255, 0.06)';
      ctx.fill();
    }

    ctx.restore();
  }

  private moveCameraTo(e: PointerEvent): void {
    const rect = this.canvas.getBoundingClientRect();
    const mx = (e.clientX - rect.left) * (this.w / rect.width);
    const my = (e.clientY - rect.top) * (this.h / rect.height);

    const [worldX, worldZ] = this.minimapToWorld(mx, my);

    this.controls.moveTo(
      Math.max(0, Math.min(this.mapW, worldX)),
      Math.max(0, Math.min(this.mapH, worldZ))
    );
  }
}
