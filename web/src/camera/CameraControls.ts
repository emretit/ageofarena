import * as THREE from 'three';

interface ControlState {
  update: () => void;
}

export function setupControls(
  camera: THREE.OrthographicCamera,
  domElement: HTMLElement,
  mapW: number,
  mapH: number
): ControlState {
  let isPanning = false;
  let lastX = 0;
  let lastY = 0;

  // Kameranın sağ ve yukarı vektörleri (izometrik düzlemde pan için)
  const panRight = new THREE.Vector3();
  const panUp = new THREE.Vector3();

  function updatePanVectors() {
    // Kameranın sağ yönü (X ekseninde pan)
    panRight.setFromMatrixColumn(camera.matrix, 0).normalize();
    panRight.y = 0;
    panRight.normalize();

    // Kameranın "ileri" yönü (Z ekseninde pan, y=0 düzleminde)
    panUp.setFromMatrixColumn(camera.matrix, 2).normalize();
    panUp.y = 0;
    panUp.normalize();
  }

  // Touch/mouse - başla
  function onPointerDown(e: PointerEvent) {
    // Bottom bar'a dokunulmuşsa pan yapma
    if (e.clientY > window.innerHeight - 70) return;

    isPanning = true;
    lastX = e.clientX;
    lastY = e.clientY;
    updatePanVectors();
  }

  // Touch/mouse - hareket
  function onPointerMove(e: PointerEvent) {
    if (!isPanning) return;

    const dx = e.clientX - lastX;
    const dy = e.clientY - lastY;
    lastX = e.clientX;
    lastY = e.clientY;

    const speed = 0.05;

    // Kamerayı pan et
    camera.position.addScaledVector(panRight, -dx * speed);
    camera.position.addScaledVector(panUp, dy * speed);

    // Sınırları kontrol et
    clampCamera();
  }

  // Touch/mouse - bırak
  function onPointerUp() {
    isPanning = false;
  }

  // Zoom (pinch + scroll)
  let lastPinchDist = 0;

  function onTouchStart(e: TouchEvent) {
    if (e.touches.length === 2) {
      lastPinchDist = getTouchDistance(e);
    }
  }

  function onTouchMove(e: TouchEvent) {
    if (e.touches.length === 2) {
      const dist = getTouchDistance(e);
      const delta = dist - lastPinchDist;
      lastPinchDist = dist;

      camera.zoom = Math.max(5, Math.min(20, camera.zoom + delta * 0.02));
      camera.updateProjectionMatrix();
    }
  }

  function onWheel(e: WheelEvent) {
    e.preventDefault();
    camera.zoom = Math.max(5, Math.min(20, camera.zoom - e.deltaY * 0.005));
    camera.updateProjectionMatrix();
  }

  function getTouchDistance(e: TouchEvent): number {
    const dx = e.touches[0].clientX - e.touches[1].clientX;
    const dy = e.touches[0].clientY - e.touches[1].clientY;
    return Math.sqrt(dx * dx + dy * dy);
  }

  function clampCamera() {
    // Basit sınırlama - kameranın haritadan çok uzaklaşmasını engelle
    const margin = 10;
    camera.position.x = Math.max(-margin, Math.min(mapW + margin, camera.position.x));
    camera.position.z = Math.max(-margin, Math.min(mapH + margin, camera.position.z));
  }

  // Event listener'ları bağla
  domElement.addEventListener('pointerdown', onPointerDown);
  domElement.addEventListener('pointermove', onPointerMove);
  domElement.addEventListener('pointerup', onPointerUp);
  domElement.addEventListener('pointercancel', onPointerUp);
  domElement.addEventListener('touchstart', onTouchStart, { passive: true });
  domElement.addEventListener('touchmove', onTouchMove, { passive: true });
  domElement.addEventListener('wheel', onWheel, { passive: false });

  return {
    update() {
      // Gerektiğinde animasyon/inertia eklenebilir
    },
  };
}
