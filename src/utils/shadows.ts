import * as THREE from 'three';

export function addContactShadow(
  parent: THREE.Group | THREE.Scene,
  x: number,
  z: number,
  radiusX: number,
  radiusZ: number,
  opacity: number = 0.2
): THREE.Mesh {
  const geo = new THREE.CircleGeometry(1, 16);
  const mat = new THREE.MeshBasicMaterial({
    color: 0x000000,
    transparent: true,
    opacity,
    depthWrite: false,
  });
  const shadow = new THREE.Mesh(geo, mat);
  shadow.rotation.x = -Math.PI / 2;
  shadow.position.set(x, 0.005, z);
  shadow.scale.set(radiusX, radiusZ, 1);
  parent.add(shadow);
  return shadow;
}
