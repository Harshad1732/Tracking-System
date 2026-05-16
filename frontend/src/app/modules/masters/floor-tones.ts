// Deterministic color palette for shopfloors. Three layers of fallback:
//   1. explicit `color` set on the shopfloor master  — wins
//   2. storage floors get the cyan "entry" tone
//   3. production floors cycle through a fixed palette by sequence, falling back to a
//      hash of the code so stable tones survive reordering.

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

export const PALETTE: FloorTone[] = [
  { base: '#2563eb', dark: '#1e40af', bg: '#eff6ff', text: '#1d4ed8' }, // blue
  { base: '#7c3aed', dark: '#5b21b6', bg: '#f5f3ff', text: '#6d28d9' }, // violet
  { base: '#db2777', dark: '#9d174d', bg: '#fdf2f8', text: '#be185d' }, // pink
  { base: '#ea580c', dark: '#9a3412', bg: '#fff7ed', text: '#c2410c' }, // orange
  { base: '#0d9488', dark: '#115e59', bg: '#f0fdfa', text: '#0f766e' }, // teal
  { base: '#65a30d', dark: '#3f6212', bg: '#f7fee7', text: '#4d7c0f' }, // lime
  { base: '#d97706', dark: '#92400e', bg: '#fffbeb', text: '#b45309' }, // amber
  { base: '#e11d48', dark: '#9f1239', bg: '#fff1f2', text: '#be123c' }  // rose
];

export interface ColorChoice {
  label: string;
  value: string | null; // null = auto
  base: string;         // swatch fill
}

/** The "Color" dropdown options for the Shopfloor master form. */
export const COLOR_CHOICES: ColorChoice[] = [
  { label: 'Auto (by sequence)', value: null,      base: '#94a3b8' },
  { label: 'Cyan (storage)',     value: STORAGE_TONE.base, base: STORAGE_TONE.base },
  ...PALETTE.map((p, i) => ({
    label: ['Blue', 'Violet', 'Pink', 'Orange', 'Teal', 'Lime', 'Amber', 'Rose'][i],
    value: p.base,
    base: p.base
  }))
];

interface FloorLike {
  code: string;
  isStorage: boolean;
  sequenceNo: number;
  color?: string | null;
}

/**
 * Resolves the explicit `color` (if set) to a full FloorTone by deriving the darker
 * gradient stop from the base. Without a stored darker shade we approximate by mixing
 * the base toward black — close enough for the gradient, and keeps the schema small.
 */
function toneFromHex(base: string): FloorTone {
  // Match the base against the palette first so we get its exact darker stop. Falls back
  // to a programmatic shade if the user picked something off-palette.
  const found = PALETTE.find(p => p.base.toLowerCase() === base.toLowerCase());
  if (found) return found;
  if (base.toLowerCase() === STORAGE_TONE.base.toLowerCase()) return STORAGE_TONE;

  const darker = shade(base, -0.25);
  const lighter = shade(base, 0.85);
  return { base, dark: darker, bg: lighter, text: darker };
}

function shade(hex: string, amount: number): string {
  // amount in [-1, 1]; negative darkens, positive lightens toward white.
  const c = hex.replace('#', '');
  if (c.length !== 6) return hex;
  const r = parseInt(c.slice(0, 2), 16);
  const g = parseInt(c.slice(2, 4), 16);
  const b = parseInt(c.slice(4, 6), 16);
  const mix = (ch: number) => amount >= 0
    ? Math.round(ch + (255 - ch) * amount)
    : Math.round(ch * (1 + amount));
  const to2 = (n: number) => Math.max(0, Math.min(255, n)).toString(16).padStart(2, '0');
  return `#${to2(mix(r))}${to2(mix(g))}${to2(mix(b))}`;
}

export function floorTone(floor: FloorLike): FloorTone {
  // 1. Explicit override wins.
  if (floor.color) return toneFromHex(floor.color);
  // 2. Storage default.
  if (floor.isStorage) return STORAGE_TONE;
  // 3. SequenceNo-driven palette, with code-hash fallback when seq is missing.
  if (floor.sequenceNo > 0) {
    const idx = Math.floor(floor.sequenceNo / 10) - 1;
    return PALETTE[((idx % PALETTE.length) + PALETTE.length) % PALETTE.length];
  }
  let h = 0;
  for (const ch of floor.code) h = (h * 31 + ch.charCodeAt(0)) | 0;
  return PALETTE[((h % PALETTE.length) + PALETTE.length) % PALETTE.length];
}
