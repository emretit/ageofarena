import * as THREE from 'three';
import { EnclosureData } from './Walls';

function seededRandom(seed: number): number {
  const x = Math.sin(seed * 127.1 + seed * 311.7) * 43758.5453;
  return x - Math.floor(x);
}

function isInsideOval(px: number, pz: number, enc: EnclosureData, margin: number): boolean {
  const dx = (px - enc.center.x) / (enc.radiusX + margin);
  const dz = (pz - enc.center.z) / (enc.radiusZ + margin);
  return dx * dx + dz * dz < 1;
}

export function createProps(
  scene: THREE.Scene,
  mapW: number, mapH: number,
  enclosures: EnclosureData[]
): void {
  const propGroup = new THREE.Group();

  scatterRocks(propGroup, mapW, mapH, enclosures);

  for (const enc of enclosures) {
    addBaseProps(propGroup, enc);
  }

  scene.add(propGroup);
}

// Shared materials (reused across all props)
const sharedMaterials = {
  rock: new THREE.MeshStandardMaterial({ color: 0x7a7a70, roughness: 0.9, metalness: 0.05 }),
  darkRock: new THREE.MeshStandardMaterial({ color: 0x5a5a50, roughness: 0.95, metalness: 0.03 }),
  barrel: new THREE.MeshStandardMaterial({ color: 0x6a4a2a, roughness: 0.75, metalness: 0.08 }),
  barrelBand: new THREE.MeshStandardMaterial({ color: 0x3a3a3a, roughness: 0.4, metalness: 0.5 }),
  crate: new THREE.MeshStandardMaterial({ color: 0x7a5a30, roughness: 0.8, metalness: 0.05 }),
  wood: new THREE.MeshStandardMaterial({ color: 0x5c3a1e, roughness: 0.88, metalness: 0.0 }),
  fence: new THREE.MeshStandardMaterial({ color: 0x6a4a28, roughness: 0.85, metalness: 0.03 }),
  pole: new THREE.MeshStandardMaterial({ color: 0x3a2a1a, roughness: 0.8, metalness: 0.1 }),
  torchHead: new THREE.MeshStandardMaterial({ color: 0x4a3a2a, roughness: 0.7, metalness: 0.15 }),
  flame: new THREE.MeshStandardMaterial({
    color: 0xff4400, emissive: 0xff6600, emissiveIntensity: 2.0,
    transparent: true, opacity: 0.8, roughness: 1, metalness: 0,
  }),
  stone: new THREE.MeshStandardMaterial({ color: 0x7a7060, roughness: 0.92, metalness: 0.02 }),
  hay: new THREE.MeshStandardMaterial({ color: 0xc8a848, roughness: 0.95, metalness: 0.0 }),
  straw: new THREE.MeshStandardMaterial({ color: 0xb8a050, roughness: 0.95, metalness: 0.0 }),
  shield: new THREE.MeshStandardMaterial({ color: 0x4a3a2a, roughness: 0.7, metalness: 0.15 }),
  rope: new THREE.MeshStandardMaterial({ color: 0x8a7a5a, roughness: 0.95, metalness: 0.0 }),
  cartSide: new THREE.MeshStandardMaterial({ color: 0x5a3a1e, roughness: 0.88, metalness: 0.02 }),
  wheel: new THREE.MeshStandardMaterial({ color: 0x3a2a1a, roughness: 0.8, metalness: 0.1 }),
};

function scatterRocks(
  group: THREE.Group, mapW: number, mapH: number,
  enclosures: EnclosureData[]
): void {
  // InstancedMesh instead of 200 individual meshes
  const rockGeo = new THREE.DodecahedronGeometry(1, 0); // unit size, scaled per instance
  const maxRocks = 200;
  const rocks = new THREE.InstancedMesh(rockGeo, sharedMaterials.rock, maxRocks);
  rocks.castShadow = true;
  rocks.receiveShadow = true;

  const dummy = new THREE.Object3D();
  const instanceColor = new THREE.Color();
  let idx = 0;

  for (let i = 0; i < maxRocks && idx < maxRocks; i++) {
    const seed = i * 317 + 42;
    const px = seededRandom(seed) * mapW;
    const pz = seededRandom(seed + 1) * mapH;

    let skip = false;
    for (const enc of enclosures) {
      if (isInsideOval(px, pz, enc, 0)) { skip = true; break; }
    }
    if (skip) continue;

    const scale = 0.1 + seededRandom(seed + 2) * 0.3;
    dummy.position.set(px, scale * 0.4, pz);
    dummy.rotation.set(
      seededRandom(seed + 4) * Math.PI,
      seededRandom(seed + 5) * Math.PI,
      seededRandom(seed + 6) * Math.PI
    );
    dummy.scale.set(
      scale * (1 + (seededRandom(seed + 7) - 0.5) * 0.5),
      scale * (0.5 + seededRandom(seed + 8) * 0.5),
      scale * (1 + (seededRandom(seed + 9) - 0.5) * 0.5)
    );
    dummy.updateMatrix();
    rocks.setMatrixAt(idx, dummy.matrix);

    // Alternate between light and dark rock colors
    const isDark = seededRandom(seed + 3) > 0.5;
    if (isDark) {
      instanceColor.setRGB(0.35, 0.35, 0.31);
    } else {
      instanceColor.setRGB(0.48, 0.48, 0.44);
    }
    rocks.setColorAt(idx, instanceColor);
    idx++;
  }

  rocks.count = idx;
  rocks.instanceMatrix.needsUpdate = true;
  if (rocks.instanceColor) rocks.instanceColor.needsUpdate = true;
  group.add(rocks);
}

function addBaseProps(group: THREE.Group, enc: EnclosureData): void {
  const cx = enc.center.x;
  const cz = enc.center.z;

  const barrelPositions = [
    { x: cx + 3, z: cz + 2 },
    { x: cx + 3.4, z: cz + 2.3 },
    { x: cx - 4, z: cz - 1 },
  ];

  for (const bp of barrelPositions) {
    addBarrel(group, bp.x, bp.z);
  }

  const cratePositions = [
    { x: cx - 3, z: cz + 3 },
    { x: cx - 3.5, z: cz + 3.2 },
  ];

  const crateGeo = new THREE.BoxGeometry(0.5, 0.5, 0.5);
  for (const cp of cratePositions) {
    const crate = new THREE.Mesh(crateGeo, sharedMaterials.crate);
    crate.position.set(cp.x, 0.25, cp.z);
    crate.rotation.y = seededRandom(cp.x * 100 + cp.z) * 0.5;
    crate.castShadow = true;
    group.add(crate);
  }

  for (const ga of enc.gateAngles) {
    const gx = enc.center.x + enc.radiusX * Math.cos(ga);
    const gz = enc.center.z + enc.radiusZ * Math.sin(ga);

    const perpX = -Math.sin(ga) * 1.5;
    const perpZ = Math.cos(ga) * 1.5;

    for (const side of [-1, 1]) {
      addTorch(group, gx + perpX * side, gz + perpZ * side);
    }
  }

  const logGeo = new THREE.CylinderGeometry(0.12, 0.12, 0.8, 5);
  const logPositions = [
    { x: cx + 5, z: cz - 3 },
    { x: cx + 5.3, z: cz - 2.7 },
  ];
  for (const lp of logPositions) {
    const log = new THREE.Mesh(logGeo, sharedMaterials.wood);
    log.position.set(lp.x, 0.12, lp.z);
    log.rotation.z = Math.PI / 2;
    log.rotation.y = seededRandom(lp.x * 50) * Math.PI;
    log.castShadow = true;
    group.add(log);
  }

  const postGeo = new THREE.BoxGeometry(0.06, 0.6, 0.06);
  const railGeo = new THREE.BoxGeometry(0.5, 0.04, 0.04);
  for (let i = 0; i < 4; i++) {
    const post = new THREE.Mesh(postGeo, sharedMaterials.fence);
    post.position.set(cx + 6 + i * 0.5, 0.3, cz);
    post.castShadow = true;
    group.add(post);

    if (i < 3) {
      const rail = new THREE.Mesh(railGeo, sharedMaterials.fence);
      rail.position.set(cx + 6.25 + i * 0.5, 0.45, cz);
      group.add(rail);
      const rail2 = new THREE.Mesh(railGeo, sharedMaterials.fence);
      rail2.position.set(cx + 6.25 + i * 0.5, 0.2, cz);
      group.add(rail2);
    }
  }

  addWell(group, cx - 2, cz - 2);
  addHayBales(group, cx + 4, cz + 4);
  addTrainingDummy(group, cx + 2, cz - 6);
  addCart(group, cx + 6, cz - 2);
}

const barrelGeo = new THREE.CylinderGeometry(0.22, 0.2, 0.5, 8);
const bandGeo = new THREE.TorusGeometry(0.215, 0.015, 4, 8);

function addBarrel(group: THREE.Group, x: number, z: number): void {
  const barrel = new THREE.Mesh(barrelGeo, sharedMaterials.barrel);
  barrel.position.set(x, 0.25, z);
  barrel.castShadow = true;
  group.add(barrel);

  for (const y of [0.12, 0.38]) {
    const band = new THREE.Mesh(bandGeo, sharedMaterials.barrelBand);
    band.position.set(x, y, z);
    band.rotation.x = Math.PI / 2;
    group.add(band);
  }
}

const torchPoleGeo = new THREE.CylinderGeometry(0.04, 0.06, 1.8, 4);
const torchHeadGeo = new THREE.CylinderGeometry(0.08, 0.05, 0.2, 5);
const flameGeo = new THREE.SphereGeometry(0.06, 4, 3);

function addTorch(group: THREE.Group, x: number, z: number): void {
  const pole = new THREE.Mesh(torchPoleGeo, sharedMaterials.pole);
  pole.position.set(x, 0.9, z);
  pole.castShadow = true;
  group.add(pole);

  const head = new THREE.Mesh(torchHeadGeo, sharedMaterials.torchHead);
  head.position.set(x, 1.85, z);
  group.add(head);

  // Emissive flame only - NO PointLight (major perf savings)
  const flame = new THREE.Mesh(flameGeo, sharedMaterials.flame);
  flame.position.set(x, 1.98, z);
  group.add(flame);
}

const wellWallGeo = new THREE.CylinderGeometry(0.45, 0.5, 0.6, 8);
const wellRimGeo = new THREE.TorusGeometry(0.48, 0.05, 4, 8);
const wellPostGeo = new THREE.CylinderGeometry(0.04, 0.05, 1.2, 4);
const wellBeamGeo = new THREE.BoxGeometry(0.9, 0.06, 0.06);
const ropeGeo = new THREE.CylinderGeometry(0.01, 0.01, 0.8, 3);
const bucketGeo = new THREE.CylinderGeometry(0.06, 0.05, 0.1, 5);

function addWell(group: THREE.Group, x: number, z: number): void {
  const wall = new THREE.Mesh(wellWallGeo, sharedMaterials.stone);
  wall.position.set(x, 0.3, z);
  wall.castShadow = true;
  group.add(wall);

  const rim = new THREE.Mesh(wellRimGeo, sharedMaterials.stone);
  rim.position.set(x, 0.62, z);
  rim.rotation.x = Math.PI / 2;
  group.add(rim);

  for (const side of [-1, 1]) {
    const post = new THREE.Mesh(wellPostGeo, sharedMaterials.wood);
    post.position.set(x + side * 0.35, 1.2, z);
    post.castShadow = true;
    group.add(post);
  }

  const beam = new THREE.Mesh(wellBeamGeo, sharedMaterials.wood);
  beam.position.set(x, 1.8, z);
  group.add(beam);

  const rope = new THREE.Mesh(ropeGeo, sharedMaterials.rope);
  rope.position.set(x, 1.4, z);
  group.add(rope);

  const bucket = new THREE.Mesh(bucketGeo, sharedMaterials.torchHead);
  bucket.position.set(x, 0.95, z);
  group.add(bucket);
}

const hayGeo = new THREE.CylinderGeometry(0.25, 0.25, 0.45, 8);

function addHayBales(group: THREE.Group, x: number, z: number): void {
  const positions = [
    { dx: 0, dz: 0, r: 0 },
    { dx: 0.5, dz: 0.15, r: 0.3 },
    { dx: 0.15, dz: 0.45, r: -0.2 },
  ];

  for (const p of positions) {
    const bale = new THREE.Mesh(hayGeo, sharedMaterials.hay);
    bale.position.set(x + p.dx, 0.22, z + p.dz);
    bale.rotation.z = Math.PI / 2;
    bale.rotation.y = p.r;
    bale.castShadow = true;
    group.add(bale);
  }
}

const dummyPoleGeo = new THREE.CylinderGeometry(0.04, 0.06, 1.6, 4);
const dummyArmGeo = new THREE.BoxGeometry(0.9, 0.06, 0.06);
const dummyBodyGeo = new THREE.CylinderGeometry(0.15, 0.18, 0.6, 6);
const dummyHeadGeo = new THREE.SphereGeometry(0.12, 5, 4);
const shieldGeo = new THREE.CircleGeometry(0.15, 6);

function addTrainingDummy(group: THREE.Group, x: number, z: number): void {
  const pole = new THREE.Mesh(dummyPoleGeo, sharedMaterials.wood);
  pole.position.set(x, 0.8, z);
  pole.castShadow = true;
  group.add(pole);

  const arm = new THREE.Mesh(dummyArmGeo, sharedMaterials.wood);
  arm.position.set(x, 1.2, z);
  group.add(arm);

  const body = new THREE.Mesh(dummyBodyGeo, sharedMaterials.straw);
  body.position.set(x, 1.0, z);
  body.castShadow = true;
  group.add(body);

  const head = new THREE.Mesh(dummyHeadGeo, sharedMaterials.straw);
  head.position.set(x, 1.45, z);
  head.castShadow = true;
  group.add(head);

  const shield = new THREE.Mesh(shieldGeo, sharedMaterials.shield);
  shield.position.set(x - 0.42, 1.0, z);
  shield.rotation.y = Math.PI / 2;
  group.add(shield);
}

const cartBedGeo = new THREE.BoxGeometry(1.2, 0.08, 0.7);
const cartSideGeo = new THREE.BoxGeometry(1.2, 0.25, 0.04);
const cartBackGeo = new THREE.BoxGeometry(0.04, 0.25, 0.7);
const wheelGeo = new THREE.TorusGeometry(0.2, 0.03, 4, 8);
const spokeGeo = new THREE.BoxGeometry(0.38, 0.02, 0.02);
const shaftGeo = new THREE.BoxGeometry(0.8, 0.04, 0.04);

function addCart(group: THREE.Group, x: number, z: number): void {
  const bed = new THREE.Mesh(cartBedGeo, sharedMaterials.fence);
  bed.position.set(x, 0.35, z);
  bed.castShadow = true;
  group.add(bed);

  for (const sz of [-0.35, 0.35]) {
    const side = new THREE.Mesh(cartSideGeo, sharedMaterials.cartSide);
    side.position.set(x, 0.52, z + sz);
    side.castShadow = true;
    group.add(side);
  }

  const back = new THREE.Mesh(cartBackGeo, sharedMaterials.cartSide);
  back.position.set(x - 0.6, 0.52, z);
  group.add(back);

  for (const sz of [-0.42, 0.42]) {
    const wheel = new THREE.Mesh(wheelGeo, sharedMaterials.wheel);
    wheel.position.set(x + 0.2, 0.2, z + sz);
    wheel.rotation.y = Math.PI / 2;
    group.add(wheel);

    for (let s = 0; s < 4; s++) {
      const spoke = new THREE.Mesh(spokeGeo, sharedMaterials.wheel);
      spoke.position.set(x + 0.2, 0.2, z + sz);
      spoke.rotation.y = Math.PI / 2;
      spoke.rotation.z = (s / 4) * Math.PI;
      group.add(spoke);
    }
  }

  const shaft = new THREE.Mesh(shaftGeo, sharedMaterials.fence);
  shaft.position.set(x + 0.95, 0.3, z);
  shaft.rotation.z = -0.15;
  group.add(shaft);
}
