/**
 * Projectile.cs port — pooled visual arrows/stones for ranged units.
 * Damage is applied instantly by CombatSystem; this is purely visual.
 * Speed = 22 u/s, slight arc (max 1.2u at mid-flight), pool size 64.
 *
 * Each pool slot holds one pre-built arrow group AND one pre-built stone group.
 * On fire(), the correct child is made visible; no allocations happen at runtime.
 */
import * as THREE from "three";

const SPEED      = 22;
const ARC_HEIGHT = 1.2;
const POOL_SIZE  = 64;

interface Proj {
  mesh:   THREE.Group;
  arrow:  THREE.Group; // pre-built arrow child
  stone:  THREE.Group; // pre-built stone child
  from:   THREE.Vector3;
  to:     THREE.Vector3;
  t:      number;
  total:  number;
  splash: boolean;
}

// Shared geometries — one allocation for all instances.
const _arrowGeo  = new THREE.BoxGeometry(0.06, 0.06, 0.5);
const _headGeo   = new THREE.BoxGeometry(0.09, 0.09, 0.10);
const _fletchGeo = new THREE.BoxGeometry(0.16, 0.02, 0.12);
const _stoneGeo  = new THREE.SphereGeometry(0.14, 6, 4);
const _arrowMat  = new THREE.MeshLambertMaterial({ color: 0x4a3018 });
const _headMat   = new THREE.MeshLambertMaterial({ color: 0x2b2b30 });
const _fletchMat = new THREE.MeshLambertMaterial({ color: 0xd8d2c0 });
const _stoneMat  = new THREE.MeshLambertMaterial({ color: 0x888080 });

function makeArrowGroup(): THREE.Group {
  const g = new THREE.Group();
  const shaft  = new THREE.Mesh(_arrowGeo,  _arrowMat);
  const head   = new THREE.Mesh(_headGeo,   _headMat);
  const fletch = new THREE.Mesh(_fletchGeo, _fletchMat);
  head.position.z   =  0.30;
  fletch.position.z = -0.24;
  g.add(shaft, head, fletch);
  return g;
}

function makeStoneGroup(): THREE.Group {
  const g = new THREE.Group();
  g.add(new THREE.Mesh(_stoneGeo, _stoneMat));
  return g;
}

export class ProjectileSystem {
  private readonly _scene: THREE.Scene;
  private readonly _pool: Proj[] = [];
  private readonly _active: Proj[] = [];

  constructor(scene: THREE.Scene) {
    this._scene = scene;
    for (let i = 0; i < POOL_SIZE; i++) {
      const mesh  = new THREE.Group();
      const arrow = makeArrowGroup();
      const stone = makeStoneGroup();
      stone.visible = false;
      mesh.add(arrow, stone);
      mesh.visible = false;
      scene.add(mesh);
      this._pool.push({
        mesh, arrow, stone,
        from: new THREE.Vector3(), to: new THREE.Vector3(),
        t: 0, total: 1, splash: false,
      });
    }
  }

  /** Fire a visual projectile from world-pos `from` toward `to`. */
  fire(from: THREE.Vector3, to: THREE.Vector3, splash = false): void {
    const p = this._pool.pop();
    if (!p) return;
    const dist = from.distanceTo(to);
    p.from.copy(from);
    p.to.copy(to);
    p.t = 0;
    p.total = Math.max(dist / SPEED, 0.05); // clamp to avoid /0
    p.splash = splash;

    // Toggle which child is visible — no new allocations.
    p.arrow.visible = !splash;
    p.stone.visible =  splash;

    p.mesh.position.copy(from);
    p.mesh.visible = true;
    this._active.push(p);
  }

  tick(dt: number): void {
    for (let i = this._active.length - 1; i >= 0; i--) {
      const p = this._active[i];
      p.t = Math.min(1, p.t + dt / p.total);

      const x   = p.from.x + (p.to.x - p.from.x) * p.t;
      const z   = p.from.z + (p.to.z - p.from.z) * p.t;
      const arc = ARC_HEIGHT * 4 * p.t * (1 - p.t);
      const y   = p.from.y + (p.to.y - p.from.y) * p.t + arc;
      p.mesh.position.set(x, y, z);

      // Orient arrow toward next travel step
      if (!p.splash && p.t < 0.98) {
        const nt   = Math.min(1, p.t + 0.02);
        const nx   = p.from.x + (p.to.x - p.from.x) * nt;
        const nz   = p.from.z + (p.to.z - p.from.z) * nt;
        const narc = ARC_HEIGHT * 4 * nt * (1 - nt);
        const ny   = p.from.y + (p.to.y - p.from.y) * nt + narc;
        p.mesh.lookAt(nx, ny, nz);
      }

      if (p.t >= 1) {
        p.mesh.visible = false;
        p.arrow.visible = false;
        p.stone.visible = false;
        this._active.splice(i, 1);
        this._pool.push(p);
      }
    }
  }
}
