/**
 * AssetLoader.ts — GLTF model cache with progress callbacks + graceful fallback.
 * If a model file is missing (404), returns null → caller uses procedural geometry.
 *
 * Usage:
 *   const loader = new AssetLoader();
 *   await loader.preload(onProgress);
 *   const gltf = loader.getUnit(UnitType.Militia); // null if not found
 */
import * as THREE from "three";
import { GLTFLoader } from "three/examples/jsm/loaders/GLTFLoader.js";
import { UnitType, BuildingType } from "../core/GameTypes";
import { UNIT_MODELS, BUILDING_MODELS } from "./AssetManifest";

const UNIT_BASE     = "/assets/models/units/";
const BUILDING_BASE = "/assets/models/buildings/";

type GltfScene = THREE.Group;

export class AssetLoader {
  private readonly _gltf = new GLTFLoader();
  private readonly _units     = new Map<UnitType, GltfScene | null>();
  private readonly _buildings = new Map<BuildingType, GltfScene | null>();
  private _loaded = false;

  /**
   * Attempt to load all models in the manifest.
   * Missing files silently resolve to null (no throw).
   * onProgress called with (loaded, total).
   */
  async preload(onProgress?: (loaded: number, total: number) => void): Promise<void> {
    const unitEntries     = Object.entries(UNIT_MODELS)     as Array<[string, { file: string; scale: number; yawOffset: number }]>;
    const buildingEntries = Object.entries(BUILDING_MODELS) as Array<[string, { file: string; scale: number }]>;
    const total = unitEntries.length + buildingEntries.length;
    let loaded = 0;

    const tick = () => { loaded++; onProgress?.(loaded, total); };

    await Promise.all([
      ...unitEntries.map(async ([type, def]) => {
        const t = Number(type) as UnitType;
        const scene = await this._tryLoad(UNIT_BASE + def.file);
        if (scene) {
          scene.scale.setScalar(def.scale);
          scene.rotation.y = def.yawOffset;
        }
        this._units.set(t, scene);
        tick();
      }),
      ...buildingEntries.map(async ([type, def]) => {
        const t = Number(type) as BuildingType;
        const scene = await this._tryLoad(BUILDING_BASE + def.file);
        if (scene) scene.scale.setScalar(def.scale);
        this._buildings.set(t, scene);
        tick();
      }),
    ]);

    this._loaded = true;
  }

  get isLoaded(): boolean { return this._loaded; }

  /** Returns a CLONED scene node (ready to add to scene), or null if not loaded. */
  getUnit(type: UnitType): GltfScene | null {
    const src = this._units.get(type);
    return src ? src.clone() : null;
  }

  getBuilding(type: BuildingType): GltfScene | null {
    const src = this._buildings.get(type);
    return src ? src.clone() : null;
  }

  private async _tryLoad(url: string): Promise<GltfScene | null> {
    return new Promise(resolve => {
      this._gltf.load(
        url,
        gltf => {
          // Apply team-color tinting: traverse and make materials cloneable
          gltf.scene.traverse(child => {
            if (child instanceof THREE.Mesh) {
              child.castShadow = true;
              child.receiveShadow = true;
            }
          });
          resolve(gltf.scene);
        },
        undefined,
        () => resolve(null), // 404 or parse error → graceful fallback
      );
    });
  }
}

/** Singleton — imported wherever needed. Populated after preload(). */
export const assetLoader = new AssetLoader();
