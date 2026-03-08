import * as THREE from 'three';

export class ZoomLOD {
  private camera: THREE.OrthographicCamera;
  private detailGroups: Map<string, THREE.Object3D[]> = new Map();

  constructor(camera: THREE.OrthographicCamera) {
    this.camera = camera;
  }

  register(level: 'high' | 'medium' | 'low', objects: THREE.Object3D[]): void {
    const existing = this.detailGroups.get(level) || [];
    existing.push(...objects);
    this.detailGroups.set(level, existing);
  }

  update(): void {
    const zoom = this.camera.zoom;

    // High detail: only at zoom > 3
    const highObjs = this.detailGroups.get('high') || [];
    for (const obj of highObjs) {
      obj.visible = zoom > 3;
    }

    // Medium detail: at zoom > 1.5
    const medObjs = this.detailGroups.get('medium') || [];
    for (const obj of medObjs) {
      obj.visible = zoom > 1.5;
    }

    // Low detail: always visible
    // (no action needed)
  }
}
