/**
 * ModelBake.ts — Bakes a (possibly skinned + multi-mesh) glTF scene into a small
 * set of STATIC merged geometries, one per material.
 *
 * Why: KayKit characters are SkinnedMeshes split into 12–15 modular sub-meshes that
 * share a single texture-atlas material; Quaternius models use a handful of materials.
 * For an AoE2-style zoom we render a static rest/bind pose (procedural bob/lunge gives
 * the motion — see Anim.ts), so the skeleton is irrelevant. Baking lets us:
 *   • clone a cheap static Mesh per unit (Iteration 1), and
 *   • later build one InstancedMesh per (type,material) group (Iteration 2)
 * without the cost/breakage of cloning skeletons.
 *
 * The bind-pose vertices already live in each mesh's geometry; we just apply the
 * mesh world matrix, strip skinning/extra attributes, merge by material, then
 * normalise so the model is 1.0 unit tall and centred on the ground at the origin.
 * Callers scale by a desired world-height (see AssetManifest `scale`).
 */
import * as THREE from "three";
import { mergeGeometries } from "three/examples/jsm/utils/BufferGeometryUtils.js";

export interface BakedGroup {
  geometry: THREE.BufferGeometry; // normalised: 1.0 tall, feet at y=0, centred on XZ
  material: THREE.Material;       // the source material (shared template; clone to tint)
}

export interface BakedModel {
  groups: BakedGroup[];
  nativeHeight: number; // pre-normalisation height (debug)
}

/** Attributes we keep; skinIndex/skinWeight/tangent etc. are dropped so merges align. */
const KEEP_ATTRS = ["position", "normal", "uv"] as const;

function stripGeometry(src: THREE.BufferGeometry): THREE.BufferGeometry {
  const g = new THREE.BufferGeometry();
  for (const name of KEEP_ATTRS) {
    const attr = src.getAttribute(name);
    if (attr) g.setAttribute(name, attr.clone());
  }
  if (src.index) g.setIndex(src.index.clone());
  return g;
}

export function bakeModel(scene: THREE.Object3D): BakedModel {
  scene.updateMatrixWorld(true);

  // Collect baked geometry grouped by material (keyed by material.uuid).
  const byMat = new Map<string, { material: THREE.Material; geos: THREE.BufferGeometry[] }>();

  scene.traverse(obj => {
    const mesh = obj as THREE.Mesh;
    if (!(mesh as THREE.Object3D).type?.includes("Mesh") || !mesh.geometry) return;
    const mats = Array.isArray(mesh.material) ? mesh.material : [mesh.material];
    // Multi-material meshes split by group; KayKit/Quaternius use one material per mesh,
    // so the common path is a single material covering the whole geometry.
    const baseGeo = stripGeometry(mesh.geometry);
    baseGeo.applyMatrix4(mesh.matrixWorld);
    const mat = mats[0] ?? new THREE.MeshStandardMaterial({ color: 0x999999 });
    const key = mat.uuid;
    let entry = byMat.get(key);
    if (!entry) { entry = { material: mat, geos: [] }; byMat.set(key, entry); }
    entry.geos.push(baseGeo);
  });

  // Merge per material, compute combined bounds for normalisation.
  // If a merge fails (mismatched attribute sets — e.g. one sub-mesh lacks uv),
  // keep the sub-meshes UN-merged rather than silently dropping all but the first.
  const merged: { material: THREE.Material; geometry: THREE.BufferGeometry }[] = [];
  const bbox = new THREE.Box3();
  for (const { material, geos } of byMat.values()) {
    let parts: THREE.BufferGeometry[];
    if (geos.length === 1) {
      parts = geos;
    } else {
      const m = mergeGeometries(geos, false);
      if (m) parts = [m];
      else { console.warn("[ModelBake] mergeGeometries failed; keeping sub-meshes un-merged"); parts = geos; }
    }
    for (const g of parts) {
      g.computeBoundingBox();
      if (g.boundingBox) bbox.union(g.boundingBox);
      merged.push({ material, geometry: g });
    }
  }

  const size = new THREE.Vector3();
  bbox.getSize(size);
  const height = size.y || 1;
  const cx = (bbox.min.x + bbox.max.x) / 2;
  const cz = (bbox.min.z + bbox.max.z) / 2;
  const inv = 1 / height;

  const groups: BakedGroup[] = merged.map(({ material, geometry }) => {
    // Recentre on XZ, drop feet to y=0, scale to unit height.
    geometry.translate(-cx, -bbox.min.y, -cz);
    geometry.scale(inv, inv, inv);
    geometry.computeVertexNormals();
    return { geometry, material };
  });

  return { groups, nativeHeight: height };
}
