/**
 * AssetLoader.ts — Loads the CC0 glTF/GLB models from the manifest, bakes each unique
 * file once into a static merged-geometry template (see ModelBake), and exposes per-type
 * lookups. Missing files resolve to null → caller uses procedural geometry (graceful).
 *
 * Usage:
 *   await assetLoader.preload(onProgress);
 *   const baked = assetLoader.getBakedUnit(UnitType.Militia); // null if not found
 *   const def   = assetLoader.getUnitDef(UnitType.Militia);
 */
import * as THREE from "three";
import { GLTFLoader } from "three/examples/jsm/loaders/GLTFLoader.js";
import { UnitType, BuildingType } from "../core/GameTypes";
import { UNIT_MODELS, BUILDING_MODELS, type UnitModelDef, type BuildingModelDef } from "./AssetManifest";
import { bakeModel, type BakedModel } from "./ModelBake";

const UNIT_BASE     = "/assets/models/units/";
const BUILDING_BASE = "/assets/models/buildings/";

export class AssetLoader {
  private readonly _gltf = new GLTFLoader();
  /** Baked templates keyed by file name (deduped — several types share one file). */
  private readonly _unitBaked     = new Map<string, BakedModel | null>();
  private readonly _buildingScene = new Map<string, THREE.Group | null>();
  private _loaded = false;

  /** Load + bake all unique files in the manifest. Missing files → null (no throw). */
  async preload(onProgress?: (loaded: number, total: number) => void): Promise<void> {
    const unitFiles     = [...new Set(Object.values(UNIT_MODELS).map(d => (d as UnitModelDef).file))];
    const buildingFiles = [...new Set(Object.values(BUILDING_MODELS).map(d => (d as BuildingModelDef).file))];
    const total = unitFiles.length + buildingFiles.length;
    let loaded = 0;
    const tick = () => { loaded++; onProgress?.(loaded, total); };

    try {
      await Promise.all([
        ...unitFiles.map(async file => {
          const scene = await this._tryLoad(UNIT_BASE + file);
          try {
            this._unitBaked.set(file, scene ? bakeModel(scene) : null);
          } catch (e) {
            console.warn(`[AssetLoader] bakeModel failed for ${file}; using procedural fallback`, e);
            this._unitBaked.set(file, null);
          }
          tick();
        }),
        ...buildingFiles.map(async file => {
          const scene = await this._tryLoad(BUILDING_BASE + file);
          if (scene) scene.traverse(c => { if ((c as THREE.Mesh).isMesh) { c.castShadow = true; c.receiveShadow = true; } });
          this._buildingScene.set(file, scene);
          tick();
        }),
      ]);
    } catch (e) {
      console.warn("[AssetLoader] preload: unexpected error; falling back to procedural geometry", e);
    }

    this._loaded = true;
  }

  get isLoaded(): boolean { return this._loaded; }

  getUnitDef(type: UnitType): UnitModelDef | undefined { return UNIT_MODELS[type]; }

  /** Baked template for a unit type (shared — clone groups before mutating). */
  getBakedUnit(type: UnitType): BakedModel | null {
    const def = UNIT_MODELS[type];
    return def ? (this._unitBaked.get(def.file) ?? null) : null;
  }

  /**
   * Cloned, UN-scaled building scene node (or null → procedural fallback).
   * Caller (Building.ts) normalises scale to the building's footprint.
   */
  getBuilding(type: BuildingType): THREE.Group | null {
    const def = BUILDING_MODELS[type];
    if (!def) return null;
    const src = this._buildingScene.get(def.file);
    if (!src) return null;
    const cloned = src.clone(true);
    // clone(true) deep-copies the node hierarchy but SHARES materials by reference. Clone
    // them per-instance so a future building tint / HP-fade / highlight can't corrupt every
    // building of the type (and the cached template). Geometry stays shared (cheap).
    cloned.traverse(o => {
      const mesh = o as THREE.Mesh;
      if (!mesh.isMesh || !mesh.material) return;
      mesh.material = Array.isArray(mesh.material)
        ? mesh.material.map(m => m.clone())
        : mesh.material.clone();
    });
    return cloned;
  }

  private async _tryLoad(url: string): Promise<THREE.Group | null> {
    return new Promise(resolve => {
      this._gltf.load(url, gltf => resolve(gltf.scene), undefined, () => resolve(null));
    });
  }
}

/** Singleton — populated after preload(). */
export const assetLoader = new AssetLoader();
