import * as THREE from 'three';
import { CAMERA_OFFSET } from './IsometricCamera';

interface ControlState {
  update: () => void;
  /** Kameranın baktığı nokta (XZ düzlemi). Minimap vs. bunu okuyabilir. */
  target: THREE.Vector3;
  /** Kamerayı belirli bir dünya koordinatına taşı */
  moveTo: (worldX: number, worldZ: number) => void;
}

export function setupControls(
  camera: THREE.OrthographicCamera,
  domElement: HTMLElement,
  mapW: number,
  mapH: number
): ControlState {
  // Kameranın baktığı nokta (XZ ground plane)
  const target = new THREE.Vector3(
    camera.position.x - CAMERA_OFFSET.x,
    0,
    camera.position.z - CAMERA_OFFSET.z,
  );

  const minZoom = 1;
  const maxZoom = 8;

  // Ekran yönlerini XZ düzlemine project et (bir kere hesapla, değişmez)
  const screenRight = new THREE.Vector3();
  const screenUp = new THREE.Vector3();
  {
    const fwd = new THREE.Vector3();
    camera.getWorldDirection(fwd);
    screenRight.crossVectors(fwd, new THREE.Vector3(0, 1, 0)).normalize();
    screenUp.crossVectors(screenRight, fwd).normalize();
    screenUp.y = 0;
    screenUp.normalize();
    screenRight.y = 0;
    screenRight.normalize();
  }

  // Keyboard
  const keys: Record<string, boolean> = {};
  const SCROLL_SPEED = 0.5;

  // Middle-button drag pan
  let isMiddleDragging = false;
  let middleDragLastX = 0;
  let middleDragLastY = 0;

  domElement.style.cursor = 'default';

  // Raycaster for cursor-to-world projection
  const raycaster = new THREE.Raycaster();
  const groundPlane = new THREE.Plane(new THREE.Vector3(0, 1, 0), 0);

  // --- Kamera pozisyonunu target'tan hesapla ---
  function applyCameraPosition() {
    camera.position.set(
      target.x + CAMERA_OFFSET.x,
      CAMERA_OFFSET.y,
      target.z + CAMERA_OFFSET.z,
    );
  }

  function clampTarget() {
    target.x = Math.max(0, Math.min(mapW, target.x));
    target.z = Math.max(0, Math.min(mapH, target.z));
  }

  function moveTo(worldX: number, worldZ: number) {
    target.x = worldX;
    target.z = worldZ;
    clampTarget();
    applyCameraPosition();
  }

  /** Ekran koordinatlarını XZ ground plane üzerindeki world pozisyonuna çevir */
  function screenToWorld(sx: number, sy: number): THREE.Vector3 | null {
    const ndcX = (sx / window.innerWidth) * 2 - 1;
    const ndcY = -(sy / window.innerHeight) * 2 + 1;
    raycaster.setFromCamera(new THREE.Vector2(ndcX, ndcY), camera);
    const intersection = new THREE.Vector3();
    const hit = raycaster.ray.intersectPlane(groundPlane, intersection);
    return hit ? intersection : null;
  }

  // --- Events ---
  function onPointerMove(e: PointerEvent) {
    // Middle-button drag pan (AutoCAD: orta tuş sürükleme = pan)
    if (isMiddleDragging) {
      const dx = e.clientX - middleDragLastX;
      const dy = e.clientY - middleDragLastY;
      middleDragLastX = e.clientX;
      middleDragLastY = e.clientY;
      const panSpeed = 0.5 / camera.zoom;
      target.x -= (screenRight.x * dx + screenUp.x * (-dy)) * panSpeed;
      target.z -= (screenRight.z * dx + screenUp.z * (-dy)) * panSpeed;
      clampTarget();
      applyCameraPosition();
    }
  }

  function onPointerDown(e: PointerEvent) {
    if (e.button === 1) {
      isMiddleDragging = true;
      middleDragLastX = e.clientX;
      middleDragLastY = e.clientY;
      domElement.style.cursor = 'grabbing';
      e.preventDefault();
    }
  }

  function onPointerUp(e: PointerEvent) {
    if (e.button === 1) {
      isMiddleDragging = false;
      domElement.style.cursor = 'default';
    }
  }

  function onKeyDown(e: KeyboardEvent) { keys[e.key.toLowerCase()] = true; }
  function onKeyUp(e: KeyboardEvent) { keys[e.key.toLowerCase()] = false; }

  // AutoCAD-style zoom: cursor pozisyonuna doğru zoom
  function onWheel(e: WheelEvent) {
    e.preventDefault();

    // Zoom öncesi cursor altındaki world pozisyonunu kaydet
    const worldBefore = screenToWorld(e.clientX, e.clientY);

    // Zoom uygula
    const zoomFactor = e.deltaY > 0 ? 0.85 : 1.15;
    camera.zoom = Math.max(minZoom, Math.min(maxZoom, camera.zoom * zoomFactor));
    camera.updateProjectionMatrix();

    // Zoom sonrası aynı ekran noktasının world pozisyonunu hesapla
    if (worldBefore) {
      const worldAfter = screenToWorld(e.clientX, e.clientY);
      if (worldAfter) {
        // Farkı target'a ekle: cursor altındaki nokta sabit kalsın
        target.x += worldBefore.x - worldAfter.x;
        target.z += worldBefore.z - worldAfter.z;
        clampTarget();
        applyCameraPosition();
      }
    }
  }

  // Touch zoom
  let lastPinchDist = 0;
  function onTouchStart(e: TouchEvent) {
    if (e.touches.length === 2) {
      const dx = e.touches[0].clientX - e.touches[1].clientX;
      const dy = e.touches[0].clientY - e.touches[1].clientY;
      lastPinchDist = Math.sqrt(dx * dx + dy * dy);
    }
  }
  function onTouchMove(e: TouchEvent) {
    if (e.touches.length === 2) {
      const dx = e.touches[0].clientX - e.touches[1].clientX;
      const dy = e.touches[0].clientY - e.touches[1].clientY;
      const dist = Math.sqrt(dx * dx + dy * dy);

      // Pinch merkez noktasını hesapla
      const cx = (e.touches[0].clientX + e.touches[1].clientX) / 2;
      const cy = (e.touches[0].clientY + e.touches[1].clientY) / 2;
      const worldBefore = screenToWorld(cx, cy);

      const delta = (dist - lastPinchDist) * 0.01;
      lastPinchDist = dist;
      camera.zoom = Math.max(minZoom, Math.min(maxZoom, camera.zoom + delta * camera.zoom));
      camera.updateProjectionMatrix();

      // Pinch merkezi sabit kalsın
      if (worldBefore) {
        const worldAfter = screenToWorld(cx, cy);
        if (worldAfter) {
          target.x += worldBefore.x - worldAfter.x;
          target.z += worldBefore.z - worldAfter.z;
          clampTarget();
          applyCameraPosition();
        }
      }
    }
  }

  // Bind events
  document.addEventListener('pointermove', onPointerMove);
  domElement.addEventListener('pointerdown', onPointerDown);
  domElement.addEventListener('pointerup', onPointerUp);
  domElement.addEventListener('wheel', onWheel, { passive: false });
  domElement.addEventListener('touchstart', onTouchStart, { passive: true });
  domElement.addEventListener('touchmove', onTouchMove, { passive: true });
  window.addEventListener('keydown', onKeyDown);
  window.addEventListener('keyup', onKeyUp);
  // Prevent default middle-click auto-scroll
  domElement.addEventListener('mousedown', (e) => { if (e.button === 1) e.preventDefault(); });

  return {
    target,
    moveTo,
    update() {
      let dx = 0;
      let dy = 0;

      // WASD / Ok tuşları
      if (keys['a'] || keys['arrowleft'])  dx -= 1;
      if (keys['d'] || keys['arrowright']) dx += 1;
      if (keys['w'] || keys['arrowup'])    dy += 1;
      if (keys['s'] || keys['arrowdown'])  dy -= 1;

      if (dx !== 0 || dy !== 0) {
        const speed = SCROLL_SPEED / camera.zoom;
        target.x += (screenRight.x * dx + screenUp.x * dy) * speed;
        target.z += (screenRight.z * dx + screenUp.z * dy) * speed;
        clampTarget();
        applyCameraPosition();
      }
    },
  };
}
