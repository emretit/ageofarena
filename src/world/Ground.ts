import * as THREE from 'three';
import { SimplexNoise } from 'three/examples/jsm/math/SimplexNoise.js';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';
import { GAME_CONFIG } from '../config';

const noise = new SimplexNoise();

function fbm(x: number, z: number, octaves: number, lacunarity: number, gain: number): number {
  let value = 0;
  let amplitude = 1;
  let frequency = 1;
  let maxValue = 0;
  for (let i = 0; i < octaves; i++) {
    value += amplitude * noise.noise(x * frequency, z * frequency);
    maxValue += amplitude;
    amplitude *= gain;
    frequency *= lacunarity;
  }
  return value / maxValue;
}

function hexToRgb(hex: number): [number, number, number] {
  return [(hex >> 16) / 255, ((hex >> 8) & 0xff) / 255, (hex & 0xff) / 255];
}

function lerpColor(
  r1: number, g1: number, b1: number,
  r2: number, g2: number, b2: number,
  t: number
): [number, number, number] {
  const ct = Math.max(0, Math.min(1, t));
  return [r1 + (r2 - r1) * ct, g1 + (g2 - g1) * ct, b1 + (b2 - b1) * ct];
}

export interface TerrainProps {
  rocks: THREE.InstancedMesh;
  tufts: THREE.InstancedMesh;
  bushes: THREE.InstancedMesh;
}

export function createGround(scene: THREE.Scene, mapW: number, mapH: number): TerrainProps {
  const { colors, terrain, bases, wallEnclosure, mapTilesW, mapTilesH } = GAME_CONFIG;
  const { radiusX: rX, radiusZ: rZ } = wallEnclosure;

  const segW = mapTilesW * terrain.subdivisions;
  const segH = mapTilesH * terrain.subdivisions;

  const geo = new THREE.PlaneGeometry(mapW, mapH, segW, segH);
  geo.rotateX(-Math.PI / 2);

  const positions = geo.attributes.position;
  const count = positions.count;

  const colorsAttr = new Float32Array(count * 3);

  const grassRGB = hexToRgb(colors.grass);
  const grassDarkRGB = hexToRgb(colors.grassDark);
  const dirtRGB = hexToRgb(colors.dirt);
  const dirtDarkRGB = hexToRgb(colors.dirtDark);

  // All base centers
  const baseCenters = bases.map(b => b.center);

  // Building positions for all bases
  const buildingPositions: { x: number; z: number }[] = [];
  for (const base of bases) {
    const cx = base.center.x;
    const cz = base.center.z;
    buildingPositions.push(
      { x: cx, z: cz }, // town center
      { x: cx - 5, z: cz - 4 },
      { x: cx + 5, z: cz - 4 },
      { x: cx - 5, z: cz + 4 },
      { x: cx + 5, z: cz + 4 },
      { x: cx, z: cz - 6 },
    );
  }

  // Pre-compute inverse radii to avoid division in loop
  const invRX = 1 / rX;
  const invRZ = 1 / rZ;
  const halfMapW = mapW / 2;
  const halfMapH = mapH / 2;

  for (let i = 0; i < count; i++) {
    const wx = positions.getX(i) + halfMapW;
    const wz = positions.getZ(i) + halfMapH;

    // Reduced octaves: 2 instead of 3 for primary noise
    const n1 = fbm(wx * terrain.noiseScale1, wz * terrain.noiseScale1, 2, 2.0, 0.5);
    const n3 = noise.noise(wx * terrain.noiseScale3 + 50, wz * terrain.noiseScale3 + 50);

    const grassBlend = (n1 + 1) * 0.5;
    let [r, g, b] = lerpColor(
      ...grassDarkRGB, ...grassRGB, grassBlend
    );

    // Merged yellow blend into detail noise (skip n2 entirely)
    const detail = n3 * 0.04;
    r += detail;
    g += detail * 0.8;
    b += detail * 0.5;

    // Proximity to wall enclosures: worn dirt (all 4 bases)
    for (const center of baseCenters) {
      const dx = (wx - center.x) * invRX;
      const dz = (wz - center.z) * invRZ;
      const ellipseDistSq = dx * dx + dz * dz;

      // Use squared distance to avoid sqrt where possible
      if (ellipseDistSq > 0.64 && ellipseDistSq < 1.44) {
        const ellipseDist = Math.sqrt(ellipseDistSq);
        if (ellipseDist > 0.8 && ellipseDist < 1.2) {
          const wallProximity = 1 - Math.abs(ellipseDist - 1.0) / 0.2;
          const wp = Math.max(0, wallProximity) * 0.7;
          [r, g, b] = lerpColor(r, g, b, ...dirtDarkRGB, wp);
        }
        if (ellipseDist < 0.75) {
          const innerWear = (1 - ellipseDist / 0.75) * 0.2;
          [r, g, b] = lerpColor(r, g, b, ...dirtRGB, innerWear);
        }
      } else if (ellipseDistSq < 0.5625) {
        const ellipseDist = Math.sqrt(ellipseDistSq);
        const innerWear = (1 - ellipseDist / 0.75) * 0.2;
        [r, g, b] = lerpColor(r, g, b, ...dirtRGB, innerWear);
      }
    }

    // Building proximity worn patches - use squared distance
    for (const bp of buildingPositions) {
      const bdx = wx - bp.x;
      const bdz = wz - bp.z;
      const bDistSq = bdx * bdx + bdz * bdz;
      if (bDistSq < 12.25) { // 3.5^2
        const bDist = Math.sqrt(bDistSq);
        const bWear = (1 - bDist / 3.5);
        const bw = bWear * bWear * 0.35;
        [r, g, b] = lerpColor(r, g, b, ...dirtRGB, bw);
      }
    }

    r = Math.max(0, Math.min(1, r));
    g = Math.max(0, Math.min(1, g));
    b = Math.max(0, Math.min(1, b));

    colorsAttr[i * 3] = r;
    colorsAttr[i * 3 + 1] = g;
    colorsAttr[i * 3 + 2] = b;

    // Height displacement - single noise call instead of fbm
    let yDisp = noise.noise(wx * 0.06 + 300, wz * 0.06 + 300) * terrain.heightScale;

    for (const bp of buildingPositions) {
      const bdx = wx - bp.x;
      const bdz = wz - bp.z;
      const bDistSq = bdx * bdx + bdz * bdz;
      if (bDistSq < 6.25) { // 2.5^2
        yDisp *= Math.min(1, Math.sqrt(bDistSq) / 2.5);
      }
    }

    for (const center of baseCenters) {
      const dx = (wx - center.x) * invRX;
      const dz = (wz - center.z) * invRZ;
      const ellipseDistSq = dx * dx + dz * dz;
      if (ellipseDistSq > 0.7225 && ellipseDistSq < 1.3225) { // 0.85^2 and 1.15^2
        const ellipseDist = Math.sqrt(ellipseDistSq);
        if (Math.abs(ellipseDist - 1.0) < 0.15) {
          yDisp *= 0.1;
        }
      }
    }

    positions.setY(i, yDisp);
  }

  geo.setAttribute('color', new THREE.BufferAttribute(colorsAttr, 3));
  geo.computeVertexNormals();

  const mat = new THREE.MeshStandardMaterial({
    vertexColors: true,
    roughness: 0.95,
    metalness: 0.0,
  });

  const ground = new THREE.Mesh(geo, mat);
  ground.position.set(mapW / 2, 0, mapH / 2);
  ground.receiveShadow = true;
  scene.add(ground);

  const terrainProps = addTerrainProps(scene, mapW, mapH);
  return terrainProps;
}

function addTerrainProps(scene: THREE.Scene, mapW: number, mapH: number): TerrainProps {
  const { bases, wallEnclosure } = GAME_CONFIG;
  const rX = wallEnclosure.radiusX;
  const rZ = wallEnclosure.radiusZ;
  const baseCenters = bases.map(b => b.center);

  // Scattered small rocks (InstancedMesh)
  const rockGeo = new THREE.DodecahedronGeometry(0.12, 0);
  const rockMat = new THREE.MeshStandardMaterial({
    color: 0x8a8070, roughness: 0.95, metalness: 0.02,
  });
  // Scale prop counts with map area (base counts were for 80x80 map)
  const mapArea = mapW * mapH;
  const areaScale = mapArea / (80 * 80);

  const rockCount = Math.floor(300 * areaScale);
  const rocks = new THREE.InstancedMesh(rockGeo, rockMat, rockCount);
  rocks.castShadow = true;
  rocks.receiveShadow = true;

  const dummy = new THREE.Object3D();
  const instanceColor = new THREE.Color();
  let rockIdx = 0;
  const rng = seedRandom(42);

  for (let i = 0; i < rockCount * 2 && rockIdx < rockCount; i++) {
    const x = rng() * mapW;
    const z = rng() * mapH;

    let skip = false;
    for (const c of baseCenters) {
      if (isInsideEnclosure(x, z, c, rX, rZ, 0.85)) { skip = true; break; }
    }
    if (skip) continue;

    const scale = 0.5 + rng() * 1.0;
    dummy.position.set(x, scale * 0.06, z);
    dummy.rotation.set(rng() * Math.PI, rng() * Math.PI, rng() * Math.PI);
    dummy.scale.set(scale, scale * (0.5 + rng() * 0.5), scale);
    dummy.updateMatrix();
    rocks.setMatrixAt(rockIdx, dummy.matrix);

    const cv = 0.08 + rng() * 0.12;
    instanceColor.setRGB(0.5 + cv, 0.47 + cv, 0.4 + cv);
    rocks.setColorAt(rockIdx, instanceColor);
    rockIdx++;
  }
  rocks.count = rockIdx;
  rocks.instanceMatrix.needsUpdate = true;
  if (rocks.instanceColor) rocks.instanceColor.needsUpdate = true;
  scene.add(rocks);

  // Grass tufts
  const tuftGeo = new THREE.ConeGeometry(0.08, 0.25, 4);
  const tuftMat = new THREE.MeshStandardMaterial({
    color: 0x4a7a35, roughness: 0.9, metalness: 0.0,
    side: THREE.DoubleSide,
  });
  const tuftCount = Math.floor(500 * areaScale);
  const tufts = new THREE.InstancedMesh(tuftGeo, tuftMat, tuftCount);
  tufts.receiveShadow = true;

  let tuftIdx = 0;
  for (let i = 0; i < tuftCount * 2 && tuftIdx < tuftCount; i++) {
    const x = rng() * mapW;
    const z = rng() * mapH;

    let skip = false;
    for (const c of baseCenters) {
      if (isInsideEnclosure(x, z, c, rX, rZ, 0.7)) { skip = true; break; }
    }
    if (skip) continue;

    for (let c = 0; c < 2 + Math.floor(rng() * 2) && tuftIdx < tuftCount; c++) {
      const ox = (rng() - 0.5) * 0.15;
      const oz = (rng() - 0.5) * 0.15;
      const s = 0.6 + rng() * 0.8;

      dummy.position.set(x + ox, s * 0.125, z + oz);
      dummy.rotation.set((rng() - 0.5) * 0.3, rng() * Math.PI * 2, (rng() - 0.5) * 0.3);
      dummy.scale.set(s, s, s);
      dummy.updateMatrix();
      tufts.setMatrixAt(tuftIdx, dummy.matrix);

      const gv = rng() * 0.1;
      instanceColor.setRGB(0.25 + gv, 0.45 + gv, 0.18 + gv);
      tufts.setColorAt(tuftIdx, instanceColor);
      tuftIdx++;
    }
  }
  tufts.count = tuftIdx;
  tufts.instanceMatrix.needsUpdate = true;
  if (tufts.instanceColor) tufts.instanceColor.needsUpdate = true;
  scene.add(tufts);

  // Small bushes
  const bushGeo = new THREE.SphereGeometry(0.2, 6, 4);
  const bushMat = new THREE.MeshStandardMaterial({
    color: 0x3a6a28, roughness: 0.9, metalness: 0.0,
  });
  const bushCount = Math.floor(120 * areaScale);
  const bushes = new THREE.InstancedMesh(bushGeo, bushMat, bushCount);
  bushes.castShadow = true;
  bushes.receiveShadow = true;

  let bushIdx = 0;
  for (let i = 0; i < bushCount * 3 && bushIdx < bushCount; i++) {
    const x = rng() * mapW;
    const z = rng() * mapH;

    let skip = false;
    for (const c of baseCenters) {
      if (isInsideEnclosure(x, z, c, rX, rZ, 0.9)) { skip = true; break; }
    }
    if (skip) continue;

    const s = 0.6 + rng() * 1.0;
    dummy.position.set(x, s * 0.15, z);
    dummy.scale.set(s, s * (0.6 + rng() * 0.4), s);
    dummy.rotation.y = rng() * Math.PI * 2;
    dummy.updateMatrix();
    bushes.setMatrixAt(bushIdx, dummy.matrix);

    const bv = rng() * 0.08;
    instanceColor.setRGB(0.2 + bv, 0.38 + bv, 0.14 + bv);
    bushes.setColorAt(bushIdx, instanceColor);
    bushIdx++;
  }
  bushes.count = bushIdx;
  bushes.instanceMatrix.needsUpdate = true;
  if (bushes.instanceColor) bushes.instanceColor.needsUpdate = true;
  scene.add(bushes);

  // Forest edge props: fallen logs and mushrooms (near map edges)
  addForestEdgeProps(scene, mapW, mapH, baseCenters, rX, rZ, rng, dummy, instanceColor);

  // Dirt paths from gates to map center
  addDirtPaths(scene, mapW, mapH);

  return { rocks, tufts, bushes };
}

function isInsideEnclosure(
  x: number, z: number,
  center: { x: number; z: number },
  rX: number, rZ: number,
  threshold: number
): boolean {
  const dx = (x - center.x) / rX;
  const dz = (z - center.z) / rZ;
  return Math.sqrt(dx * dx + dz * dz) < threshold;
}

function addForestEdgeProps(
  scene: THREE.Scene, mapW: number, mapH: number,
  baseCenters: { x: number; z: number }[],
  rX: number, rZ: number,
  rng: () => number, dummy: THREE.Object3D, instanceColor: THREE.Color
): void {
  const edgeDepth = GAME_CONFIG.forest.edgeForestDepth;

  // Fallen logs
  const logGeo = new THREE.CylinderGeometry(0.1, 0.08, 1.2, 5);
  const logMat = new THREE.MeshStandardMaterial({
    color: 0x5c3a1e, roughness: 0.9, metalness: 0.0,
  });
  const logCount = 80;
  const logs = new THREE.InstancedMesh(logGeo, logMat, logCount);
  logs.castShadow = true;
  logs.receiveShadow = true;

  let logIdx = 0;
  for (let i = 0; i < logCount * 3 && logIdx < logCount; i++) {
    const x = rng() * mapW;
    const z = rng() * mapH;

    // Only near edges
    const distEdge = Math.min(x, mapW - x, z, mapH - z);
    if (distEdge > edgeDepth) continue;

    let skip = false;
    for (const c of baseCenters) {
      if (isInsideEnclosure(x, z, c, rX, rZ, 1.1)) { skip = true; break; }
    }
    if (skip) continue;

    const s = 0.6 + rng() * 0.8;
    dummy.position.set(x, 0.06, z);
    dummy.rotation.set(Math.PI / 2, rng() * Math.PI * 2, (rng() - 0.5) * 0.3);
    dummy.scale.set(s, s, s);
    dummy.updateMatrix();
    logs.setMatrixAt(logIdx, dummy.matrix);

    const lv = rng() * 0.06;
    instanceColor.setRGB(0.32 + lv, 0.2 + lv, 0.1 + lv);
    logs.setColorAt(logIdx, instanceColor);
    logIdx++;
  }
  logs.count = logIdx;
  logs.instanceMatrix.needsUpdate = true;
  if (logs.instanceColor) logs.instanceColor.needsUpdate = true;
  scene.add(logs);

  // Mushroom clusters
  const mushGeo = new THREE.CylinderGeometry(0.06, 0.02, 0.08, 5);
  const mushMat = new THREE.MeshStandardMaterial({
    color: 0xc8a070, roughness: 0.8, metalness: 0.0,
  });
  const mushCount = 100;
  const mushrooms = new THREE.InstancedMesh(mushGeo, mushMat, mushCount);

  let mushIdx = 0;
  for (let i = 0; i < mushCount * 3 && mushIdx < mushCount; i++) {
    const x = rng() * mapW;
    const z = rng() * mapH;

    const distEdge = Math.min(x, mapW - x, z, mapH - z);
    if (distEdge > edgeDepth * 0.8) continue;

    let skip = false;
    for (const c of baseCenters) {
      if (isInsideEnclosure(x, z, c, rX, rZ, 1.0)) { skip = true; break; }
    }
    if (skip) continue;

    // Small cluster of 2-3 mushrooms
    for (let m = 0; m < 2 + Math.floor(rng() * 2) && mushIdx < mushCount; m++) {
      const ox = (rng() - 0.5) * 0.2;
      const oz = (rng() - 0.5) * 0.2;
      const s = 0.5 + rng() * 1.0;
      dummy.position.set(x + ox, s * 0.04, z + oz);
      dummy.rotation.set(0, rng() * Math.PI * 2, 0);
      dummy.scale.set(s, s, s);
      dummy.updateMatrix();
      mushrooms.setMatrixAt(mushIdx, dummy.matrix);

      const mv = rng() * 0.1;
      const isRed = rng() > 0.7;
      if (isRed) {
        instanceColor.setRGB(0.7 + mv, 0.2, 0.15);
      } else {
        instanceColor.setRGB(0.7 + mv, 0.58 + mv, 0.4 + mv);
      }
      mushrooms.setColorAt(mushIdx, instanceColor);
      mushIdx++;
    }
  }
  mushrooms.count = mushIdx;
  mushrooms.instanceMatrix.needsUpdate = true;
  if (mushrooms.instanceColor) mushrooms.instanceColor.needsUpdate = true;
  scene.add(mushrooms);
}

function addDirtPaths(scene: THREE.Scene, mapW: number, mapH: number): void {
  const { bases, wallEnclosure, terrain, colors } = GAME_CONFIG;
  const mapCenterX = mapW / 2;
  const mapCenterZ = mapH / 2;
  const pathHalfW = terrain.pathWidth * 0.5;

  const pathMat = new THREE.MeshStandardMaterial({
    color: colors.path,
    roughness: 0.95,
    metalness: 0.0,
  });

  // Collect all path segment geometries and merge into one
  const geometries: THREE.BufferGeometry[] = [];

  for (const base of bases) {
    for (const ga of base.gateAngles) {
      const gx = base.center.x + wallEnclosure.radiusX * Math.cos(ga);
      const gz = base.center.z + wallEnclosure.radiusZ * Math.sin(ga);

      const dx = mapCenterX - gx;
      const dz = mapCenterZ - gz;
      const dist = Math.sqrt(dx * dx + dz * dz);
      const angle = Math.atan2(dz, dx);

      const segmentLen = 2.0;
      const segCount = Math.floor(dist / segmentLen);

      for (let i = 0; i < segCount; i++) {
        const t = (i + 0.5) / segCount;
        const sx = gx + dx * t;
        const sz = gz + dz * t;

        const fadeT = Math.min(1, t * 1.5);
        const width = pathHalfW * 2 * (1 - fadeT * 0.3);

        const pathGeo = new THREE.PlaneGeometry(segmentLen + 0.2, width);
        pathGeo.rotateX(-Math.PI / 2);
        pathGeo.rotateY(-angle);
        pathGeo.translate(sx, 0.01, sz);
        geometries.push(pathGeo);
      }
    }
  }

  if (geometries.length > 0) {
    const mergedGeo = mergeGeometries(geometries);
    if (mergedGeo) {
      const pathMesh = new THREE.Mesh(mergedGeo, pathMat);
      pathMesh.receiveShadow = true;
      scene.add(pathMesh);
      // Dispose individual geometries
      for (const g of geometries) g.dispose();
    }
  }
}

function seedRandom(seed: number): () => number {
  let s = seed;
  return () => {
    s = (s * 16807 + 0) % 2147483647;
    return (s - 1) / 2147483646;
  };
}
