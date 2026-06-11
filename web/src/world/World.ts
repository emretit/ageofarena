import * as THREE from "three";
import { Config } from "../core/Config";
import { ResourceKind } from "../core/GameTypes";
import { ResourceNode } from "../game/ResourceNode";

/** Terrain only — lighting, ocean, land disc (no forest). Called before startGame. */
export function buildTerrain(scene: THREE.Scene): void {
  const sun = new THREE.DirectionalLight(0xfff2dd, 2.6);
  sun.position.set(-60, 90, 40);
  sun.castShadow = true;
  sun.shadow.mapSize.set(2048, 2048);
  const s = 120;
  sun.shadow.camera.left = -s; sun.shadow.camera.right = s;
  sun.shadow.camera.top = s; sun.shadow.camera.bottom = -s;
  sun.shadow.camera.far = 400;
  scene.add(sun);
  scene.add(new THREE.AmbientLight(0xbfd4ff, 0.9));

  const ocean = new THREE.Mesh(
    new THREE.PlaneGeometry(Config.OceanHalf * 2, Config.OceanHalf * 2),
    new THREE.MeshLambertMaterial({ color: 0x3ba7e0 }),
  );
  ocean.rotation.x = -Math.PI / 2;
  ocean.position.y = -0.4;
  scene.add(ocean);

  const rim = new THREE.Mesh(
    new THREE.CircleGeometry(Config.LandRadius + 3, 96),
    new THREE.MeshLambertMaterial({ color: 0xd9c489 }),
  );
  rim.rotation.x = -Math.PI / 2;
  rim.position.y = -0.15;
  scene.add(rim);

  const land = new THREE.Mesh(
    new THREE.CircleGeometry(Config.LandRadius, 96),
    new THREE.MeshLambertMaterial({ color: 0xc8bd8a }),
  );
  land.rotation.x = -Math.PI / 2;
  land.receiveShadow = true;
  land.name = "Ground";
  scene.add(land);
}

/**
 * Full world build (terrain + default Arabia forest) — kept for reference.
 * Main.ts uses buildTerrain + MapGenerator.buildForest instead.
 */
export function buildWorld(scene: THREE.Scene): void {
  buildTerrain(scene);

  const pineGeo = new THREE.ConeGeometry(1.1, 3.2, 6);
  const trunkGeo = new THREE.CylinderGeometry(0.18, 0.22, 1.0, 5);
  const pineMat = new THREE.MeshLambertMaterial({ color: 0x3f9b4f });
  const trunkMat = new THREE.MeshLambertMaterial({ color: 0x6b4a2a });
  const trees = new THREE.Group();
  const rng = mulberry32(1453);

  for (let i = 0; i < 900; i++) {
    const ang = rng() * Math.PI * 2;
    const rad = Config.CoastInner + rng() * (Config.ForestOuter - Config.CoastInner);
    const x = Math.cos(ang) * rad, z = Math.sin(ang) * rad;

    const nearPocket = [[0, -1], [0, 1], [-1, 0], [1, 0]].some(([dx, dz]) => {
      const px = dx * Config.BaseDistance, pz = dz * Config.BaseDistance;
      return (x - px) * (x - px) + (z - pz) * (z - pz) < 18 * 18;
    });
    if (nearPocket) continue;

    const scale = 0.7 + rng() * 0.8;
    const trunk = new THREE.Mesh(trunkGeo, trunkMat);
    trunk.position.set(x, 0.5 * scale, z);
    trunk.scale.setScalar(scale);
    const crown = new THREE.Mesh(pineGeo, pineMat);
    crown.position.set(x, (1.0 + 1.6) * scale, z);
    crown.scale.setScalar(scale);
    crown.castShadow = true;
    trees.add(trunk, crown);
  }
  scene.add(trees);
}

/**
 * Spawn resource nodes for a base pocket centred at (bx, bz).
 * Returns the created nodes so they can be registered with the game.
 */
export function spawnBaseResources(
  scene: THREE.Scene,
  bx: number,
  bz: number,
  rng: () => number,
): ResourceNode[] {
  const nodes: ResourceNode[] = [];
  const offsets: Array<[number, number, ResourceKind, number]> = [
    [-8,  -6, ResourceKind.Gold,  400],
    [ 8,  -6, ResourceKind.Gold,  400],
    [-6,   8, ResourceKind.Food,  150],
    [ 6,   8, ResourceKind.Food,  150],
    [-12,  4, ResourceKind.Wood,  250],
    [ 12,  4, ResourceKind.Wood,  250],
    [  0, -12, ResourceKind.Stone, 200],
  ];
  for (const [ox, oz, kind, amt] of offsets) {
    const jx = (rng() - 0.5) * 2;
    const jz = (rng() - 0.5) * 2;
    nodes.push(new ResourceNode(
      scene,
      new THREE.Vector3(bx + ox + jx, 0, bz + oz + jz),
      kind,
      amt,
    ));
  }
  return nodes;
}

/** Small deterministic PRNG (sim-safe, like Unity's SimRandom). */
export function mulberry32(seed: number): () => number {
  let a = seed >>> 0;
  return () => {
    a |= 0; a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
