import * as THREE from 'three';
import { GAME_CONFIG } from '../config';

export interface EnclosureData {
  center: { x: number; z: number };
  radiusX: number;
  radiusZ: number;
  gateAngles: number[];
}

// Prosedürel taş renk varyasyonu
function stoneColorVariation(baseColor: number, seed: number): THREE.Color {
  const base = new THREE.Color(baseColor);
  const variation = (Math.sin(seed * 127.1) * 0.5 + 0.5) * 0.08 - 0.04;
  base.r = Math.max(0, Math.min(1, base.r + variation));
  base.g = Math.max(0, Math.min(1, base.g + variation * 0.8));
  base.b = Math.max(0, Math.min(1, base.b + variation * 0.6));
  return base;
}

// Merlon position data for InstancedMesh
interface MerlonData {
  x: number; y: number; z: number;
  rotY: number;
  colorSeed: number;
}

// Shared merlon collector across all enclosures
const merlonCollector: MerlonData[] = [];

export function createWallEnclosures(scene: THREE.Scene): EnclosureData[] {
  const { wallEnclosure, colors, bases } = GAME_CONFIG;
  const { radiusX, radiusZ, wallHeight, wallThickness, segmentCount, gateWidth } = wallEnclosure;

  const woodMat = new THREE.MeshStandardMaterial({
    color: colors.woodGate,
    roughness: 0.85,
    metalness: 0.05,
  });

  const metalMat = new THREE.MeshStandardMaterial({
    color: 0x3a3a3a,
    roughness: 0.4,
    metalness: 0.7,
  });

  merlonCollector.length = 0;
  const enclosures: EnclosureData[] = [];

  for (const base of bases) {
    buildEnclosure(scene, base.center.x, base.center.z, base.gateAngles, woodMat, metalMat,
      radiusX, radiusZ, wallHeight, wallThickness, segmentCount, gateWidth, colors);
    enclosures.push({
      center: base.center,
      radiusX,
      radiusZ,
      gateAngles: base.gateAngles,
    });
  }

  // Create single InstancedMesh for all merlons
  if (merlonCollector.length > 0) {
    const merlonGeo = new THREE.BoxGeometry(0.4, 0.6, 0.55);
    const merlonMat = new THREE.MeshStandardMaterial({
      color: colors.stoneDark,
      roughness: 0.9,
      metalness: 0.03,
    });
    const merlons = new THREE.InstancedMesh(merlonGeo, merlonMat, merlonCollector.length);
    merlons.castShadow = true;

    const dummy = new THREE.Object3D();
    for (let i = 0; i < merlonCollector.length; i++) {
      const md = merlonCollector[i];
      dummy.position.set(md.x, md.y, md.z);
      dummy.rotation.set(0, md.rotY, 0);
      dummy.scale.set(1, 1, 1);
      dummy.updateMatrix();
      merlons.setMatrixAt(i, dummy.matrix);
      merlons.setColorAt(i, stoneColorVariation(colors.stoneDark, md.colorSeed));
    }
    merlons.instanceMatrix.needsUpdate = true;
    if (merlons.instanceColor) merlons.instanceColor.needsUpdate = true;
    scene.add(merlons);
  }

  return enclosures;
}

function buildEnclosure(
  scene: THREE.Scene,
  cx: number, cz: number, gateAngles: number[],
  woodMat: THREE.Material, metalMat: THREE.Material,
  radiusX: number, radiusZ: number,
  wallHeight: number, wallThickness: number,
  segmentCount: number, gateWidth: number,
  colors: typeof GAME_CONFIG.colors
): void {
  const group = new THREE.Group();
  const gateHalfAngle = gateWidth / ((radiusX + radiusZ) / 2) * 0.5;

  // Shared materials for all wall segments (instead of per-segment)
  const wallStoneMat = new THREE.MeshStandardMaterial({
    color: colors.stoneWall,
    roughness: 0.92,
    metalness: 0.02,
  });
  const walkwayStoneMat = new THREE.MeshStandardMaterial({
    color: colors.stoneDark,
    roughness: 0.9,
    metalness: 0.02,
  });
  const buttressStoneMat = new THREE.MeshStandardMaterial({
    color: colors.stoneDark,
    roughness: 0.85,
    metalness: 0.05,
  });

  for (let i = 0; i < segmentCount; i++) {
    const t0 = (i / segmentCount) * Math.PI * 2;
    const t1 = ((i + 1) / segmentCount) * Math.PI * 2;
    const tMid = (t0 + t1) / 2;

    let isGate = false;
    for (const ga of gateAngles) {
      let diff = tMid - ga;
      while (diff > Math.PI) diff -= Math.PI * 2;
      while (diff < -Math.PI) diff += Math.PI * 2;
      if (Math.abs(diff) < gateHalfAngle) {
        isGate = true;
        break;
      }
    }

    const x0 = cx + radiusX * Math.cos(t0);
    const z0 = cz + radiusZ * Math.sin(t0);
    const x1 = cx + radiusX * Math.cos(t1);
    const z1 = cz + radiusZ * Math.sin(t1);

    if (isGate) {
      buildGatePillars(group, x0, z0, x1, z1, tMid, woodMat, metalMat, wallHeight, colors);
      continue;
    }

    const mx = (x0 + x1) / 2;
    const mz = (z0 + z1) / 2;
    const segLen = Math.sqrt((x1 - x0) ** 2 + (z1 - z0) ** 2);
    const angle = Math.atan2(z1 - z0, x1 - x0);

    // Damage variation: 20% of segments have reduced height
    const damageRng = Math.sin(i * 31.7 + cx * 5.3) * 0.5 + 0.5;
    const segHeight = damageRng < 0.2 ? wallHeight * (0.85 + damageRng * 0.5) : wallHeight;

    // Wall segment - shared material
    const wallGeo = new THREE.BoxGeometry(segLen + 0.1, segHeight, wallThickness);
    const wall = new THREE.Mesh(wallGeo, wallStoneMat);
    wall.position.set(mx, segHeight / 2, mz);
    wall.rotation.y = -angle;
    wall.castShadow = true;
    wall.receiveShadow = true;
    group.add(wall);

    // Walkway - shared material
    const walkwayGeo = new THREE.BoxGeometry(segLen + 0.1, 0.12, wallThickness + 0.3);
    const walkway = new THREE.Mesh(walkwayGeo, walkwayStoneMat);
    walkway.position.set(mx, wallHeight, mz);
    walkway.rotation.y = -angle;
    walkway.receiveShadow = true;
    group.add(walkway);

    // Mazgallar (InstancedMesh'e toplanıyor)
    const merlonCount = Math.max(1, Math.floor(segLen / 1.0));
    for (let m = 0; m < merlonCount; m++) {
      if (m % 2 !== 0) continue;
      const mt = (m + 0.5) / merlonCount;
      merlonCollector.push({
        x: x0 + (x1 - x0) * mt,
        y: wallHeight + 0.36,
        z: z0 + (z1 - z0) * mt,
        rotY: -angle,
        colorSeed: m * 11.7 + i,
      });
    }

    // Her 4 segmentte meşale
    if (i % 4 === 2) {
      const normal = new THREE.Vector2(-(z1 - z0), x1 - x0).normalize();
      const torchX = mx - normal.x * (wallThickness / 2 + 0.15);
      const torchZ = mz - normal.y * (wallThickness / 2 + 0.15);
      addWallTorch(group, torchX, torchZ, wallHeight);
    }

    // Her 4 segmentte destek sütunu
    if (i % 4 === 0) {
      const normal = new THREE.Vector2(
        -(z1 - z0),
        x1 - x0
      ).normalize();

      const bGeo = new THREE.BoxGeometry(0.5, wallHeight * 0.8, 0.8);
      const buttress = new THREE.Mesh(bGeo, buttressStoneMat);
      buttress.position.set(
        mx + normal.x * (wallThickness / 2 + 0.3),
        wallHeight * 0.4,
        mz + normal.y * (wallThickness / 2 + 0.3)
      );
      buttress.rotation.y = -angle;
      buttress.castShadow = true;
      buttress.receiveShadow = true;
      group.add(buttress);
    }
  }

  // Köşe kuleleri
  for (let corner = 0; corner < 4; corner++) {
    const t = (corner / 4) * Math.PI * 2 + Math.PI / 4;
    const tx = cx + (radiusX + 0.3) * Math.cos(t);
    const tz = cz + (radiusZ + 0.3) * Math.sin(t);

    const towerGeo = new THREE.CylinderGeometry(0.8, 1.0, wallHeight + 1.5, 8);
    const tower = new THREE.Mesh(towerGeo, buttressStoneMat);
    tower.position.set(tx, (wallHeight + 1.5) / 2, tz);
    tower.castShadow = true;
    tower.receiveShadow = true;
    group.add(tower);

    const roofGeo = new THREE.ConeGeometry(1.1, 1.2, 8);
    const roofMat = new THREE.MeshStandardMaterial({
      color: 0x5a3a1e,
      roughness: 0.8,
      metalness: 0.05,
    });
    const roof = new THREE.Mesh(roofGeo, roofMat);
    roof.position.set(tx, wallHeight + 2.1, tz);
    roof.castShadow = true;
    group.add(roof);

    // Kule mazgalları (InstancedMesh'e toplanıyor)
    for (let m = 0; m < 8; m++) {
      const ma = (m / 8) * Math.PI * 2;
      if (m % 2 !== 0) continue;
      merlonCollector.push({
        x: tx + Math.cos(ma) * 0.85,
        y: wallHeight + 1.5 + 0.25,
        z: tz + Math.sin(ma) * 0.85,
        rotY: ma,
        colorSeed: corner * 17 + m,
      });
    }

    // Kule dibinde sandık/varil kümesi
    const outDir = new THREE.Vector2(tx - cx, tz - cz).normalize();
    addTowerBaseProps(group, tx + outDir.x * 1.5, tz + outDir.y * 1.5, corner);
  }

  scene.add(group);
}

function buildGatePillars(
  group: THREE.Group,
  x0: number, z0: number,
  x1: number, z1: number,
  _angle: number,
  gateMat: THREE.Material,
  metalMat: THREE.Material,
  wallHeight: number,
  colors: typeof GAME_CONFIG.colors
): void {
  const pillarH = wallHeight + 0.5;
  const pillarMat = new THREE.MeshStandardMaterial({
    color: colors.stoneDark,
    roughness: 0.8,
    metalness: 0.08,
  });

  const p1Geo = new THREE.BoxGeometry(0.7, pillarH, 0.7);
  const p1 = new THREE.Mesh(p1Geo, pillarMat);
  p1.position.set(x0, pillarH / 2, z0);
  p1.castShadow = true;
  group.add(p1);

  const p2 = new THREE.Mesh(p1Geo, pillarMat);
  p2.position.set(x1, pillarH / 2, z1);
  p2.castShadow = true;
  group.add(p2);

  const capGeo = new THREE.BoxGeometry(0.9, 0.25, 0.9);
  for (const pos of [[x0, z0], [x1, z1]]) {
    const cap = new THREE.Mesh(capGeo, pillarMat);
    cap.position.set(pos[0], pillarH + 0.12, pos[1]);
    cap.castShadow = true;
    group.add(cap);
  }

  const mx = (x0 + x1) / 2;
  const mz = (z0 + z1) / 2;
  const dist = Math.sqrt((x1 - x0) ** 2 + (z1 - z0) ** 2);
  const rot = Math.atan2(z1 - z0, x1 - x0);

  const lintelGeo = new THREE.BoxGeometry(dist, 0.7, 0.8);
  const lintel = new THREE.Mesh(lintelGeo, pillarMat);
  lintel.position.set(mx, pillarH, mz);
  lintel.rotation.y = -rot;
  lintel.castShadow = true;
  group.add(lintel);

  const doorGeo = new THREE.BoxGeometry(dist * 0.8, wallHeight * 0.7, 0.2);
  const door = new THREE.Mesh(doorGeo, gateMat);
  door.position.set(mx, wallHeight * 0.35, mz);
  door.rotation.y = -rot;
  group.add(door);

  for (let b = 0; b < 3; b++) {
    const bandY = wallHeight * 0.1 + b * wallHeight * 0.25;
    const bandGeo = new THREE.BoxGeometry(dist * 0.78, 0.08, 0.22);
    const band = new THREE.Mesh(bandGeo, metalMat);
    band.position.set(mx, bandY, mz);
    band.rotation.y = -rot;
    group.add(band);
  }

  const ringGeo = new THREE.TorusGeometry(0.12, 0.03, 6, 8);
  const ring = new THREE.Mesh(ringGeo, metalMat);
  ring.position.set(mx, wallHeight * 0.35, mz);
  ring.rotation.y = -rot;
  group.add(ring);
}

function addWallTorch(group: THREE.Group, x: number, z: number, wallHeight: number): void {
  const bracketMat = new THREE.MeshStandardMaterial({
    color: 0x3a3a3a, roughness: 0.4, metalness: 0.7,
  });

  // Duvar bağlantı braketi
  const bracketGeo = new THREE.BoxGeometry(0.06, 0.06, 0.25);
  const bracket = new THREE.Mesh(bracketGeo, bracketMat);
  bracket.position.set(x, wallHeight * 0.75, z);
  group.add(bracket);

  // Meşale direği
  const poleMat = new THREE.MeshStandardMaterial({
    color: 0x3a2a1a, roughness: 0.8, metalness: 0.1,
  });
  const poleGeo = new THREE.CylinderGeometry(0.03, 0.04, 0.6, 4);
  const pole = new THREE.Mesh(poleGeo, poleMat);
  pole.position.set(x, wallHeight * 0.75 + 0.3, z);
  pole.castShadow = true;
  group.add(pole);

  // Emissive flame only - no PointLight for performance
  const flameMat = new THREE.MeshStandardMaterial({
    color: 0xff4400,
    emissive: 0xff6600,
    emissiveIntensity: 2.0,
    transparent: true,
    opacity: 0.8,
  });
  const flameGeo = new THREE.SphereGeometry(0.05, 4, 3);
  const flame = new THREE.Mesh(flameGeo, flameMat);
  flame.position.set(x, wallHeight * 0.75 + 0.65, z);
  group.add(flame);
}

function addTowerBaseProps(group: THREE.Group, x: number, z: number, seed: number): void {
  const crateMat = new THREE.MeshStandardMaterial({
    color: 0x7a5a30, roughness: 0.8, metalness: 0.05,
  });
  const barrelMat = new THREE.MeshStandardMaterial({
    color: 0x6a4a2a, roughness: 0.75, metalness: 0.08,
  });

  // Sandık
  const crateGeo = new THREE.BoxGeometry(0.4, 0.4, 0.4);
  const crate = new THREE.Mesh(crateGeo, crateMat);
  crate.position.set(x, 0.2, z);
  crate.rotation.y = seed * 0.5;
  crate.castShadow = true;
  crate.receiveShadow = true;
  group.add(crate);

  // Varil
  const barrelGeo = new THREE.CylinderGeometry(0.18, 0.16, 0.4, 7);
  const barrel = new THREE.Mesh(barrelGeo, barrelMat);
  barrel.position.set(x + 0.35, 0.2, z + 0.15);
  barrel.castShadow = true;
  barrel.receiveShadow = true;
  group.add(barrel);
}
