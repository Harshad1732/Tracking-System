// Deterministic color palette for shopfloors. Storage always gets the cyan
// "entry" tone; production floors cycle through a fixed palette by sequence,
// falling back to a hash of the code so stable tones survive reordering.

export interface FloorTone {
  base: string;   // primary fill / gradient stop
  dark: string;   // gradient end / hover shadow base
  bg: string;     // subtle tint for surfaces
  text: string;   // foreground that reads on `bg`
}

const STORAGE_TONE: FloorTone = {
  base: '#0891b2',
  dark: '#0e7490',
  bg:   '#ecfeff',
  text: '#0e7490'
};

const PALETTE: FloorTone[] = [
  { base: '#2563eb', dark: '#1e40af', bg: '#eff6ff', text: '#1d4ed8' }, // blue
  { base: '#7c3aed', dark: '#5b21b6', bg: '#f5f3ff', text: '#6d28d9' }, // violet
  { base: '#db2777', dark: '#9d174d', bg: '#fdf2f8', text: '#be185d' }, // pink
  { base: '#ea580c', dark: '#9a3412', bg: '#fff7ed', text: '#c2410c' }, // orange
  { base: '#0d9488', dark: '#115e59', bg: '#f0fdfa', text: '#0f766e' }, // teal
  { base: '#65a30d', dark: '#3f6212', bg: '#f7fee7', text: '#4d7c0f' }, // lime
  { base: '#d97706', dark: '#92400e', bg: '#fffbeb', text: '#b45309' }, // amber
  { base: '#e11d48', dark: '#9f1239', bg: '#fff1f2', text: '#be123c' }  // rose
];

interface FloorLike {
  code: string;
  isStorage: boolean;
  sequenceNo: number;
}

export function floorTone(floor: FloorLike): FloorTone {
  if (floor.isStorage) return STORAGE_TONE;
  // SequenceNo gives a stable, predictable order; fall back to code-hash if seq is missing.
  if (floor.sequenceNo > 0) {
    const idx = Math.floor(floor.sequenceNo / 10) - 1;
    return PALETTE[((idx % PALETTE.length) + PALETTE.length) % PALETTE.length];
  }
  let h = 0;
  for (const ch of floor.code) h = (h * 31 + ch.charCodeAt(0)) | 0;
  return PALETTE[((h % PALETTE.length) + PALETTE.length) % PALETTE.length];
}
