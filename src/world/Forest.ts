import * as THREE from 'three';
import { GAME_CONFIG } from '../config';
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

function isOnClearingPath(px: number, pz: number, enc: EnclosureData, mapCenterX: number, mapCenterZ: number): boolean {
  const halfW = GAME_CONFIG.forest.clearingWidth;
  for (const ga of enc.gateAngles) {
    const gx = enc.center.x + enc.radiusX * Math.cos(ga);
    const gz = enc.center.z + enc.radiusZ * Math.sin(ga);

    // Dikey kapılar (kuzey/güney): z yönünde yol aç
    if (Math.abs(ga + Math.PI / 2) < 0.3 || Math.abs(ga - Math.PI / 2) < 0.3) {
      if (Math.abs(px - gx) < halfW) {
        const minZ = Math.min(gz, mapCenterZ);
        const maxZ = Math.max(gz, mapCenterZ);
        if (pz >= minZ - 1 && pz <= maxZ + 1) return true;
      }
    }
    // Yatay kapılar (doğu/batı): x yönünde yol aç
    if (Math.abs(ga) < 0.3 || Math.abs(ga - Math.PI) < 0.3) {
      if (Math.abs(pz - gz) < halfW) {
        const minX = Math.min(gx, mapCenterX);
        const maxX = Math.max(gx, mapCenterX);
        if (px >= minX - 1 && px <= maxX + 1) return true;
      }
    }
  }
  return false;
}

// Renk varyasyonu
function varyColor(base: number, seed: number, amount: number = 0.08): THREE.Color {
  const c = new THREE.Color(base);
  const v = (seededRandom(seed) - 0.5) * amount * 2;
  c.r = Math.max(0, Math.min(1, c.r + v));
  c.g = Math.max(0, Math.min(1, c.g + v * 0.7));
  c.b = Math.max(0, Math.min(1, c.b + v * 0.5));
  return c;
}

// Arena modunda orman yoğunluğu: kenarlar yoğun, merkez seyrek
function getForestDensity(worldX: number, worldZ: number, mapW: number, mapH: number): number {
  const { forest } = GAME_CONFIG;
  const edgeDepth = forest.edgeForestDepth;

  const distLeft = worldX;
  const distRight = mapW - worldX;
  const distTop = worldZ;
  const distBottom = mapH - worldZ;
  const minEdgeDist = Math.min(distLeft, distRight, distTop, distBottom);

  if (minEdgeDist < edgeDepth) {
    const edgeFactor = 1 - minEdgeDist / edgeDepth;
    return forest.density * (0.4 + edgeFactor * 0.6);
  }

  const cx = mapW / 2;
  const cz = mapH / 2;
  const distCenter = Math.sqrt((worldX - cx) ** 2 + (worldZ - cz) ** 2);
  const maxDist = Math.sqrt(cx * cx + cz * cz);
  const centerFactor = distCenter / maxDist;

  return forest.interBaseForestDensity * centerFactor;
}

// === Tree position data ===
interface TreeData {
  x: number;
  z: number;
  scale: number;
  rotY: number;
  seed: number;
  type: 'pine' | 'tallpine' | 'oak' | 'birch' | 'bush';
}

function collectTreePositions(
  mapW: number, mapH: number, enclosures: EnclosureData[]
): TreeData[] {
  const { tileSize, mapTilesW, mapTilesH } = GAME_CONFIG;
  const mapCenterX = mapW / 2;
  const mapCenterZ = mapH / 2;
  const trees: TreeData[] = [];

  for (let tx = 0; tx < mapTilesW; tx++) {
    for (let tz = 0; tz < mapTilesH; tz++) {
      const seed = tx * 1000 + tz;
      const r = seededRandom(seed);

      const worldX = tx * tileSize + tileSize / 2;
      const worldZ = tz * tileSize + tileSize / 2;

      const density = getForestDensity(worldX, worldZ, mapW, mapH);
      if (r > density) continue;

      let insideBase = false;
      for (const enc of enclosures) {
        if (isInsideOval(worldX, worldZ, enc, 2)) { insideBase = true; break; }
      }
      if (insideBase) continue;

      let onPath = false;
      for (const enc of enclosures) {
        if (isOnClearingPath(worldX, worldZ, enc, mapCenterX, mapCenterZ)) { onPath = true; break; }
      }
      if (onPath) continue;

      const ox = (seededRandom(seed + 1) - 0.5) * tileSize * 0.7;
      const oz = (seededRandom(seed + 2) - 0.5) * tileSize * 0.7;
      const px = worldX + ox;
      const pz = worldZ + oz;

      if (px < 0.5 || px > mapW - 0.5 || pz < 0.5 || pz > mapH - 0.5) continue;

      const scale = 0.7 + seededRandom(seed + 3) * 0.7;
      const rotY = seededRandom(seed + 5) * Math.PI * 2;
      const treeType = seededRandom(seed + 4);

      let type: TreeData['type'];
      if (treeType < 0.35) type = 'pine';
      else if (treeType < 0.55) type = 'tallpine';
      else if (treeType < 0.78) type = 'oak';
      else if (treeType < 0.88) type = 'birch';
      else type = 'bush';

      trees.push({ x: px, z: pz, scale, rotY, seed, type });
    }
  }

  return trees;
}

// === InstancedMesh-based forest rendering ===

export function createForest(
  scene: THREE.Scene,
  mapW: number, mapH: number,
  enclosures: EnclosureData[]
): void {
  const { colors } = GAME_CONFIG;
  const trees = collectTreePositions(mapW, mapH, enclosures);

  // Count instances per geometry type
  let pineTrunkCount = 0, pineConeCount = 0;
  let tallPineTrunkCount = 0, tallPineConeCount = 0;
  let oakTrunkCount = 0, oakCanopyCount = 0, oakCanopy2Count = 0;
  let birchTrunkCount = 0, birchCanopyCount = 0;
  let bushSphereCount = 0;
  let shadowCount = 0;

  for (const t of trees) {
    switch (t.type) {
      case 'pine': pineTrunkCount++; pineConeCount += 3; break;
      case 'tallpine': tallPineTrunkCount++; tallPineConeCount += 4; break;
      case 'oak': oakTrunkCount++; oakCanopyCount++; oakCanopy2Count++; break;
      case 'birch': birchTrunkCount++; birchCanopyCount++; break;
      case 'bush': bushSphereCount += (1 + 1); break; // main + possible second
    }
    if (t.type !== 'bush') shadowCount++;
  }

  const dummy = new THREE.Object3D();
  const instanceColor = new THREE.Color();

  // --- Pine trees ---
  const pineTrunkGeo = new THREE.CylinderGeometry(0.08, 0.15, 1.5, 5);
  const pineTrunkMat = new THREE.MeshStandardMaterial({
    color: colors.treeTrunk, roughness: 0.9, metalness: 0.0,
  });
  const pineTrunks = new THREE.InstancedMesh(pineTrunkGeo, pineTrunkMat, pineTrunkCount);
  pineTrunks.castShadow = true;

  const pineConeGeo = new THREE.ConeGeometry(0.9, 1.2, 6);
  const pineConeMat = new THREE.MeshStandardMaterial({
    color: colors.treePine, roughness: 0.8, metalness: 0.0,
  });
  const pineCones = new THREE.InstancedMesh(pineConeGeo, pineConeMat, pineConeCount);
  pineCones.castShadow = true;

  // --- Tall pine trees ---
  const tallPineTrunkGeo = new THREE.CylinderGeometry(0.07, 0.14, 2.2, 5);
  const tallPineTrunkMat = new THREE.MeshStandardMaterial({
    color: 0x4a2a10, roughness: 0.9, metalness: 0.0,
  });
  const tallPineTrunks = new THREE.InstancedMesh(tallPineTrunkGeo, tallPineTrunkMat, tallPineTrunkCount);
  tallPineTrunks.castShadow = true;

  const tallPineConeGeo = new THREE.ConeGeometry(0.75, 0.9, 6);
  const tallPineConeMat = new THREE.MeshStandardMaterial({
    color: colors.treePine, roughness: 0.75, metalness: 0.0,
  });
  const tallPineCones = new THREE.InstancedMesh(tallPineConeGeo, tallPineConeMat, tallPineConeCount);
  tallPineCones.castShadow = true;

  // --- Oak trees ---
  const oakTrunkGeo = new THREE.CylinderGeometry(0.1, 0.2, 1.4, 5);
  const oakTrunkMat = new THREE.MeshStandardMaterial({
    color: colors.treeTrunk, roughness: 0.85, metalness: 0.0,
  });
  const oakTrunks = new THREE.InstancedMesh(oakTrunkGeo, oakTrunkMat, oakTrunkCount);
  oakTrunks.castShadow = true;

  const oakCanopyGeo = new THREE.IcosahedronGeometry(0.95, 2);
  const oakCanopyMat = new THREE.MeshStandardMaterial({
    color: colors.treeLeafDark, roughness: 0.7, metalness: 0.0,
  });
  const oakCanopies = new THREE.InstancedMesh(oakCanopyGeo, oakCanopyMat, oakCanopyCount);
  oakCanopies.castShadow = true;

  const oakCanopy2Geo = new THREE.IcosahedronGeometry(0.6, 2);
  const oakCanopies2 = new THREE.InstancedMesh(oakCanopy2Geo, oakCanopyMat, oakCanopy2Count);
  oakCanopies2.castShadow = true;

  // --- Birch trees ---
  const birchTrunkGeo = new THREE.CylinderGeometry(0.06, 0.1, 2.0, 5);
  const birchTrunkMat = new THREE.MeshStandardMaterial({
    color: 0xd4c8a8, roughness: 0.6, metalness: 0.05,
  });
  const birchTrunks = new THREE.InstancedMesh(birchTrunkGeo, birchTrunkMat, birchTrunkCount);
  birchTrunks.castShadow = true;

  const birchCanopyGeo = new THREE.SphereGeometry(0.7, 7, 5);
  birchCanopyGeo.scale(1, 1.3, 1);
  const birchCanopyMat = new THREE.MeshStandardMaterial({
    color: 0x5a9a3a, roughness: 0.7, metalness: 0.0,
  });
  const birchCanopies = new THREE.InstancedMesh(birchCanopyGeo, birchCanopyMat, birchCanopyCount);
  birchCanopies.castShadow = true;

  // --- Bush spheres ---
  const bushGeo = new THREE.SphereGeometry(0.45, 6, 5);
  const bushMat = new THREE.MeshStandardMaterial({
    color: colors.treeLeafDark, roughness: 0.8, metalness: 0.0,
  });
  const bushMeshes = new THREE.InstancedMesh(bushGeo, bushMat, bushSphereCount);
  bushMeshes.castShadow = true;

  // --- Ground shadows ---
  const shadowGeo = new THREE.CircleGeometry(0.6, 8);
  const shadowMat = new THREE.MeshStandardMaterial({
    color: 0x1a3310, transparent: true, opacity: 0.3,
    roughness: 1, metalness: 0,
  });
  const shadows = new THREE.InstancedMesh(shadowGeo, shadowMat, shadowCount);

  // === Fill instances ===
  let ptIdx = 0, pcIdx = 0;
  let tptIdx = 0, tpcIdx = 0;
  let otIdx = 0, ocIdx = 0, oc2Idx = 0;
  let btIdx = 0, bcIdx = 0;
  let bsIdx = 0;
  let shIdx = 0;

  const pineLayers = [
    { y: 1.2, rScale: 1.0, hScale: 1.0 },
    { y: 1.8, rScale: 0.78, hScale: 0.83 },
    { y: 2.3, rScale: 0.56, hScale: 0.67 },
  ];

  const tallPineLayers = [
    { y: 1.6, rScale: 1.0, hScale: 1.0 },
    { y: 2.1, rScale: 0.8, hScale: 0.89 },
    { y: 2.5, rScale: 0.6, hScale: 0.78 },
    { y: 2.85, rScale: 0.4, hScale: 0.56 },
  ];

  for (const t of trees) {
    const { x, z, scale, rotY, seed, type } = t;

    switch (type) {
      case 'pine': {
        // Trunk
        dummy.position.set(x, 0.75 * scale, z);
        dummy.rotation.set(0, 0, 0);
        dummy.scale.set(scale, scale, scale);
        dummy.updateMatrix();
        pineTrunks.setMatrixAt(ptIdx, dummy.matrix);
        varyColor(colors.treeTrunk, seed, 0.06).toArray([0, 0, 0] as unknown as number[], 0);
        pineTrunks.setColorAt(ptIdx, varyColor(colors.treeTrunk, seed, 0.06));
        ptIdx++;

        // Cone layers
        for (const l of pineLayers) {
          dummy.position.set(x, l.y * scale, z);
          dummy.rotation.set(0, rotY, 0);
          dummy.scale.set(scale * l.rScale, scale * l.hScale, scale * l.rScale);
          dummy.updateMatrix();
          pineCones.setMatrixAt(pcIdx, dummy.matrix);
          pineCones.setColorAt(pcIdx, varyColor(colors.treePine, seed + 10, 0.1));
          pcIdx++;
        }

        // Shadow
        dummy.position.set(x, 0.01, z);
        dummy.rotation.set(-Math.PI / 2, 0, 0);
        dummy.scale.set(scale, scale, 1);
        dummy.updateMatrix();
        shadows.setMatrixAt(shIdx++, dummy.matrix);
        break;
      }

      case 'tallpine': {
        dummy.position.set(x, 1.1 * scale, z);
        dummy.rotation.set(0, 0, 0);
        dummy.scale.set(scale, scale, scale);
        dummy.updateMatrix();
        tallPineTrunks.setMatrixAt(tptIdx, dummy.matrix);
        tallPineTrunks.setColorAt(tptIdx, varyColor(0x4a2a10, seed, 0.06));
        tptIdx++;

        for (const l of tallPineLayers) {
          dummy.position.set(x, l.y * scale, z);
          dummy.rotation.set(0, rotY, 0);
          dummy.scale.set(scale * l.rScale, scale * l.hScale, scale * l.rScale);
          dummy.updateMatrix();
          tallPineCones.setMatrixAt(tpcIdx, dummy.matrix);
          tallPineCones.setColorAt(tpcIdx, varyColor(colors.treePine, seed + 20, 0.12));
          tpcIdx++;
        }

        dummy.position.set(x, 0.01, z);
        dummy.rotation.set(-Math.PI / 2, 0, 0);
        dummy.scale.set(scale, scale, 1);
        dummy.updateMatrix();
        shadows.setMatrixAt(shIdx++, dummy.matrix);
        break;
      }

      case 'oak': {
        // Trunk
        const trunkTilt = (seededRandom(seed + 40) - 0.5) * 0.1;
        dummy.position.set(x, 0.7 * scale, z);
        dummy.rotation.set(0, 0, trunkTilt);
        dummy.scale.set(scale, scale, scale);
        dummy.updateMatrix();
        oakTrunks.setMatrixAt(otIdx, dummy.matrix);
        oakTrunks.setColorAt(otIdx, varyColor(colors.treeTrunk, seed, 0.08));
        otIdx++;

        // Main canopy
        dummy.position.set(x, 1.7 * scale, z);
        dummy.rotation.set(rotY, rotY * 0.5, 0);
        dummy.scale.set(scale, scale, scale);
        dummy.updateMatrix();
        oakCanopies.setMatrixAt(ocIdx, dummy.matrix);
        const leafColor = seededRandom(seed + 30) > 0.5 ? colors.treeLeafDark : colors.treeLeafLight;
        oakCanopies.setColorAt(ocIdx, varyColor(leafColor, seed + 15, 0.1));
        ocIdx++;

        // Secondary canopy
        const ox2 = (seededRandom(seed + 50) - 0.5) * 0.5 * scale;
        dummy.position.set(x + ox2, 1.5 * scale, z + ox2 * 0.7);
        dummy.rotation.set(0, 0, 0);
        dummy.scale.set(scale, scale, scale);
        dummy.updateMatrix();
        oakCanopies2.setMatrixAt(oc2Idx, dummy.matrix);
        oakCanopies2.setColorAt(oc2Idx, varyColor(leafColor, seed + 16, 0.1));
        oc2Idx++;

        dummy.position.set(x, 0.01, z);
        dummy.rotation.set(-Math.PI / 2, 0, 0);
        dummy.scale.set(scale, scale, 1);
        dummy.updateMatrix();
        shadows.setMatrixAt(shIdx++, dummy.matrix);
        break;
      }

      case 'birch': {
        const bTilt = (seededRandom(seed + 41) - 0.5) * 0.08;
        dummy.position.set(x, 1.0 * scale, z);
        dummy.rotation.set(0, 0, bTilt);
        dummy.scale.set(scale, scale, scale);
        dummy.updateMatrix();
        birchTrunks.setMatrixAt(btIdx, dummy.matrix);
        birchTrunks.setColorAt(btIdx, varyColor(0xd4c8a8, seed, 0.05));
        btIdx++;

        dummy.position.set(x, 2.2 * scale, z);
        dummy.rotation.set(0, rotY, 0);
        dummy.scale.set(scale, scale, scale);
        dummy.updateMatrix();
        birchCanopies.setMatrixAt(bcIdx, dummy.matrix);
        birchCanopies.setColorAt(bcIdx, varyColor(0x5a9a3a, seed + 25, 0.12));
        bcIdx++;

        dummy.position.set(x, 0.01, z);
        dummy.rotation.set(-Math.PI / 2, 0, 0);
        dummy.scale.set(scale, scale, 1);
        dummy.updateMatrix();
        shadows.setMatrixAt(shIdx++, dummy.matrix);
        break;
      }

      case 'bush': {
        // Main bush sphere
        dummy.position.set(x, 0.3 * scale, z);
        dummy.rotation.set(0, 0, 0);
        dummy.scale.set(scale, scale, scale);
        dummy.updateMatrix();
        bushMeshes.setMatrixAt(bsIdx, dummy.matrix);
        bushMeshes.setColorAt(bsIdx, varyColor(colors.treeLeafDark, seed + 35, 0.12));
        bsIdx++;

        // Second bush sphere (60% chance)
        if (seededRandom(seed + 60) > 0.4) {
          const bOx = (seededRandom(seed + 61) - 0.5) * 0.4;
          const s2 = scale * 0.67;
          dummy.position.set(x + bOx, 0.22 * scale, z + bOx * 0.6);
          dummy.scale.set(s2, s2, s2);
          dummy.updateMatrix();
          bushMeshes.setMatrixAt(bsIdx, dummy.matrix);
          bushMeshes.setColorAt(bsIdx, varyColor(colors.treeLeafDark, seed + 36, 0.12));
          bsIdx++;
        } else {
          // Still need to fill the slot (set to zero scale)
          dummy.position.set(0, -10, 0);
          dummy.scale.set(0, 0, 0);
          dummy.updateMatrix();
          bushMeshes.setMatrixAt(bsIdx, dummy.matrix);
          instanceColor.set(0);
          bushMeshes.setColorAt(bsIdx, instanceColor);
          bsIdx++;
        }
        break;
      }
    }
  }

  // Finalize all InstancedMeshes
  const allMeshes = [
    pineTrunks, pineCones,
    tallPineTrunks, tallPineCones,
    oakTrunks, oakCanopies, oakCanopies2,
    birchTrunks, birchCanopies,
    bushMeshes, shadows,
  ];

  for (const mesh of allMeshes) {
    mesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true;
    scene.add(mesh);
  }
}
