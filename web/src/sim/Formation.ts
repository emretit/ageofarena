/**
 * Formation — offset grid for multi-unit move commands.
 * Zero three.js imports — pure sim layer.
 */

export const enum FormationType { Grid, Line, Staggered, Wedge }

/**
 * Returns [dx, dz] world-space offsets (relative to group goal) for each unit.
 * Length === count. Offsets are in local move-direction space — caller rotates
 * by the move direction angle before applying.
 */
export function getFormationOffsets(count: number, type: FormationType, spacing = 1.5): [number, number][] {
  const offsets: [number, number][] = [];
  if (count === 0) return offsets;

  switch (type) {
    case FormationType.Line: {
      for (let i = 0; i < count; i++) {
        offsets.push([(i - (count - 1) / 2) * spacing, 0]);
      }
      break;
    }
    case FormationType.Grid:
    default: {
      const cols = Math.ceil(Math.sqrt(count));
      for (let i = 0; i < count; i++) {
        const row = Math.floor(i / cols);
        const col = i % cols;
        offsets.push([(col - (cols - 1) / 2) * spacing, row * spacing]);
      }
      break;
    }
    case FormationType.Staggered: {
      const cols = Math.ceil(count / 2);
      for (let i = 0; i < count; i++) {
        const row = Math.floor(i / cols);
        const col = i % cols;
        const stagger = (row & 1) ? spacing * 0.5 : 0;
        offsets.push([(col - (cols - 1) / 2) * spacing + stagger, row * spacing * 0.87]);
      }
      break;
    }
    case FormationType.Wedge: {
      offsets.push([0, 0]); // leader
      let placed = 1; let ring = 1;
      while (placed < count) {
        const slots = Math.min(ring * 2, count - placed);
        for (let i = 0; i < slots; i++) {
          offsets.push([(i - (slots - 1) / 2) * spacing, ring * spacing]);
          placed++;
        }
        ring++;
      }
      break;
    }
  }
  return offsets;
}
