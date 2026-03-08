import * as THREE from 'three';
import { GameWorld } from '../entities/GameWorld';
import { GameEntity, UnitEntity } from '../entities/types';
import { HUD } from '../ui/HUD';
import { CommandPanel } from '../ui/CommandPanel';

const RING_POOL_SIZE = 40;
const DRAG_THRESHOLD = 5; // pixels

export class SelectionSystem {
  private camera: THREE.OrthographicCamera;
  private scene: THREE.Scene;
  private world: GameWorld;
  private hud: HUD;
  private commandPanel: CommandPanel;
  private raycaster = new THREE.Raycaster();
  private mouse = new THREE.Vector2();
  private selected: GameEntity[] = [];

  // Ring pool
  private ringPool: THREE.Mesh[] = [];

  // Right-click marker
  private rightClickMarker: THREE.Mesh;
  private markerTimer = 0;

  // Drag-box
  private dragBox: HTMLElement;
  private isDragging = false;
  private dragStart = { x: 0, y: 0 };

  constructor(
    camera: THREE.OrthographicCamera,
    scene: THREE.Scene,
    domElement: HTMLElement,
    world: GameWorld,
    hud: HUD,
    commandPanel: CommandPanel,
  ) {
    this.camera = camera;
    this.scene = scene;
    this.world = world;
    this.hud = hud;
    this.commandPanel = commandPanel;
    this.dragBox = document.getElementById('drag-box')!;

    // Create ring pool
    const ringGeo = new THREE.RingGeometry(0.6, 0.7, 24);
    for (let i = 0; i < RING_POOL_SIZE; i++) {
      const ringMat = new THREE.MeshBasicMaterial({
        color: 0x00ff00,
        transparent: true,
        opacity: 0.7,
        side: THREE.DoubleSide,
        depthWrite: false,
      });
      const ring = new THREE.Mesh(ringGeo, ringMat);
      ring.rotation.x = -Math.PI / 2;
      ring.visible = false;
      this.scene.add(ring);
      this.ringPool.push(ring);
    }

    // Right-click move marker
    const markerGeo = new THREE.RingGeometry(0.2, 0.3, 12);
    const markerMat = new THREE.MeshBasicMaterial({
      color: 0x00ff00,
      transparent: true,
      opacity: 0.6,
      side: THREE.DoubleSide,
      depthWrite: false,
    });
    this.rightClickMarker = new THREE.Mesh(markerGeo, markerMat);
    this.rightClickMarker.rotation.x = -Math.PI / 2;
    this.rightClickMarker.visible = false;
    this.scene.add(this.rightClickMarker);

    // Pointer events for drag-box
    domElement.addEventListener('pointerdown', (e) => {
      if (e.button === 0) {
        this.dragStart = { x: e.clientX, y: e.clientY };
        this.isDragging = false;
      }
    });

    domElement.addEventListener('pointermove', (e) => {
      if (e.buttons & 1) { // left button held
        const dx = e.clientX - this.dragStart.x;
        const dy = e.clientY - this.dragStart.y;
        if (!this.isDragging && Math.sqrt(dx * dx + dy * dy) > DRAG_THRESHOLD) {
          this.isDragging = true;
        }
        if (this.isDragging) {
          this.updateDragBox(e.clientX, e.clientY);
        }
      }
    });

    domElement.addEventListener('pointerup', (e) => {
      if (e.button === 0) {
        if (this.isDragging) {
          this.finishDragBox(e.clientX, e.clientY);
          this.isDragging = false;
          this.dragBox.style.display = 'none';
        } else {
          this.onLeftClick(e);
        }
      }
    });

    // Right click = move command
    domElement.addEventListener('contextmenu', (e) => {
      e.preventDefault();
      this.onRightClick(e);
    });
  }

  private updateDragBox(cx: number, cy: number): void {
    const x1 = Math.min(this.dragStart.x, cx);
    const y1 = Math.min(this.dragStart.y, cy);
    const x2 = Math.max(this.dragStart.x, cx);
    const y2 = Math.max(this.dragStart.y, cy);
    this.dragBox.style.display = 'block';
    this.dragBox.style.left = x1 + 'px';
    this.dragBox.style.top = y1 + 'px';
    this.dragBox.style.width = (x2 - x1) + 'px';
    this.dragBox.style.height = (y2 - y1) + 'px';
  }

  private finishDragBox(cx: number, cy: number): void {
    const x1 = Math.min(this.dragStart.x, cx);
    const y1 = Math.min(this.dragStart.y, cy);
    const x2 = Math.max(this.dragStart.x, cx);
    const y2 = Math.max(this.dragStart.y, cy);

    const w = window.innerWidth;
    const h = window.innerHeight;

    // Find all player 0 units inside the drag rectangle
    const found: UnitEntity[] = [];
    for (const unit of this.world.units) {
      if (unit.playerIndex !== 0) continue;
      const screenPos = unit.position.clone().project(this.camera);
      const sx = (screenPos.x + 1) / 2 * w;
      const sy = (-screenPos.y + 1) / 2 * h;
      if (sx >= x1 && sx <= x2 && sy >= y1 && sy <= y2) {
        found.push(unit);
      }
    }

    if (found.length > 0) {
      this.selectMultiple(found);
    }
  }

  private onLeftClick(e: PointerEvent): void {
    const target = e.target as HTMLElement;
    if (target.closest('#bottom-hud') || target.closest('#ui-overlay')) return;

    this.mouse.x = (e.clientX / window.innerWidth) * 2 - 1;
    this.mouse.y = -(e.clientY / window.innerHeight) * 2 + 1;

    this.raycaster.setFromCamera(this.mouse, this.camera);
    const intersects = this.raycaster.intersectObjects(this.scene.children, true);

    let found: GameEntity | null = null;
    for (const hit of intersects) {
      const entity = this.world.getEntityFromObject(hit.object);
      if (entity) {
        found = entity;
        break;
      }
    }

    if (e.shiftKey && found && found.type === 'unit' && found.playerIndex === 0) {
      // Shift+click: toggle unit in selection
      const idx = this.selected.indexOf(found);
      if (idx >= 0) {
        this.selected.splice(idx, 1);
      } else {
        // If currently selecting buildings, clear and start fresh with unit
        if (this.selected.length > 0 && this.selected[0].type === 'building') {
          this.selected = [];
        }
        this.selected.push(found);
      }
      this.updateSelectionDisplay();
    } else {
      // Normal click: select single entity
      this.select(found);
    }
  }

  private onRightClick(e: MouseEvent): void {
    this.mouse.x = (e.clientX / window.innerWidth) * 2 - 1;
    this.mouse.y = -(e.clientY / window.innerHeight) * 2 + 1;

    this.raycaster.setFromCamera(this.mouse, this.camera);
    const plane = new THREE.Plane(new THREE.Vector3(0, 1, 0), 0);
    const target = new THREE.Vector3();
    this.raycaster.ray.intersectPlane(plane, target);
    if (!target) return;

    // Building selected: set rally point
    if (this.selected.length === 1 && this.selected[0].type === 'building' &&
        this.selected[0].playerIndex === 0 && this.selected[0].def.trainable.length > 0) {
      this.selected[0].rallyPoint = new THREE.Vector3(target.x, 0, target.z);

      // Show rally marker (reuse right-click marker with different color)
      this.rightClickMarker.position.set(target.x, 0.05, target.z);
      (this.rightClickMarker.material as THREE.MeshBasicMaterial).color.set(0xffaa00);
      this.rightClickMarker.visible = true;
      this.markerTimer = 1.0;

      // Show rally line briefly
      this.showRallyLine(this.selected[0].position, target);
      return;
    }

    // Units selected: move command
    const selectedUnits = this.selected.filter(
      (ent): ent is UnitEntity => ent.type === 'unit' && ent.playerIndex === 0
    );
    if (selectedUnits.length === 0) return;

    if (selectedUnits.length === 1) {
      const unit = selectedUnits[0];
      unit.targetPos = new THREE.Vector3(target.x, 0, target.z);
      unit.state = 'moving';
    } else {
      // Formation: grid layout
      const cols = Math.ceil(Math.sqrt(selectedUnits.length));
      const spacing = 1.5;
      for (let i = 0; i < selectedUnits.length; i++) {
        const row = Math.floor(i / cols);
        const col = i % cols;
        const offsetX = (col - (cols - 1) / 2) * spacing;
        const offsetZ = (row - (Math.ceil(selectedUnits.length / cols) - 1) / 2) * spacing;
        selectedUnits[i].targetPos = new THREE.Vector3(
          target.x + offsetX, 0, target.z + offsetZ
        );
        selectedUnits[i].state = 'moving';
      }
    }

    // Show green marker
    this.rightClickMarker.position.set(target.x, 0.05, target.z);
    (this.rightClickMarker.material as THREE.MeshBasicMaterial).color.set(0x00ff00);
    this.rightClickMarker.visible = true;
    this.markerTimer = 0.5;
  }

  private rallyLine: THREE.Line | null = null;
  private rallyLineTimer = 0;

  private showRallyLine(from: THREE.Vector3, to: THREE.Vector3): void {
    // Remove old rally line
    if (this.rallyLine) {
      this.scene.remove(this.rallyLine);
      this.rallyLine.geometry.dispose();
    }

    const points = [
      new THREE.Vector3(from.x, 0.1, from.z),
      new THREE.Vector3(to.x, 0.1, to.z),
    ];
    const geo = new THREE.BufferGeometry().setFromPoints(points);
    const mat = new THREE.LineBasicMaterial({ color: 0xffaa00, transparent: true, opacity: 0.6 });
    this.rallyLine = new THREE.Line(geo, mat);
    this.scene.add(this.rallyLine);
    this.rallyLineTimer = 2.0;
  }

  select(entity: GameEntity | null): void {
    this.selected = entity ? [entity] : [];
    this.updateSelectionDisplay();
  }

  selectMultiple(entities: GameEntity[]): void {
    this.selected = entities;
    this.updateSelectionDisplay();
  }

  private updateSelectionDisplay(): void {
    // Update HUD
    if (this.selected.length <= 1) {
      this.hud.showEntity(this.selected[0] || null);
    } else {
      this.hud.showEntities(this.selected);
    }

    // Update command panel
    this.commandPanel.onSelectionChanged(this.selected);

    // Update rings
    for (let i = 0; i < this.ringPool.length; i++) {
      const ring = this.ringPool[i];
      if (i < this.selected.length) {
        const entity = this.selected[i];
        const pos = entity.position;
        const size = entity.type === 'building'
          ? Math.max(entity.def.size.w, entity.def.size.h) * 0.5
          : (entity.def.isMounted ? 0.8 : 0.5);
        ring.scale.set(size, size, size);
        ring.position.set(pos.x, 0.05, pos.z);
        ring.visible = true;
        const ringMat = ring.material as THREE.MeshBasicMaterial;
        ringMat.color.set(entity.playerIndex === 0 ? 0x00ff00 : 0xff4444);
      } else {
        ring.visible = false;
      }
    }
  }

  getSelected(): GameEntity[] {
    return this.selected;
  }

  update(dt: number): void {
    // Update ring positions for moving units
    for (let i = 0; i < this.selected.length && i < this.ringPool.length; i++) {
      const ring = this.ringPool[i];
      if (ring.visible) {
        ring.position.set(
          this.selected[i].position.x,
          0.05,
          this.selected[i].position.z,
        );
      }
    }

    // Fade out right-click marker
    if (this.markerTimer > 0) {
      this.markerTimer -= dt;
      if (this.markerTimer <= 0) {
        this.rightClickMarker.visible = false;
      }
    }

    // Fade out rally line
    if (this.rallyLineTimer > 0) {
      this.rallyLineTimer -= dt;
      if (this.rallyLineTimer <= 0 && this.rallyLine) {
        this.scene.remove(this.rallyLine);
        this.rallyLine.geometry.dispose();
        this.rallyLine = null;
      }
    }
  }
}
