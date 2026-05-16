// Generates a 100-row Excel file matching the format the Import dialog expects.
// Run from the frontend directory: `node scripts/gen-sample-sheets.cjs`
// Output: ../sample-data/glass-sheets-100.xlsx

const XLSX = require('xlsx');
const fs = require('fs');
const path = require('path');

const customers = [
  'ABC Builders', 'XYZ Contractors', 'Metro Glass Works', 'Skyline Architects',
  'Heritage Interiors', 'Aurora Construction', 'Bluegate Realty',
  'Crystal Facades', 'Delta Build', 'Evergreen Homes'
];

const glassTypes = ['Clear', 'Tinted', 'Tempered', 'Laminated', 'Frosted', 'Reflective'];
const thicknesses = [4, 5, 6, 8, 10, 12];
const widths  = [600, 800, 900, 1000, 1200, 1500, 1800, 2100, 2400];
const heights = [600, 900, 1200, 1500, 1800, 2100, 2400, 2700, 3000];
const remarksPool = [
  '', '', '', // many sheets have no remark
  'Site delivery',
  'Customer pickup',
  'Priority order',
  'Quality grade A',
  'Re-cut allowed if needed',
  'Polished edge required'
];

function pick(arr, i) { return arr[i % arr.length]; }

const rows = [];
for (let i = 0; i < 100; i++) {
  const sheetNo  = `GS-${String(1001 + i).padStart(4, '0')}`;
  const orderNo  = `SO-${String(100 + Math.floor(i / 4)).padStart(3, '0')}`;
  const customer = pick(customers, i * 7);
  const glass    = pick(glassTypes, i * 3);
  const t        = pick(thicknesses, i);
  const w        = pick(widths, i * 5);
  const h        = pick(heights, i * 11);
  const qty      = (i % 5) + 1;
  const remark   = pick(remarksPool, i * 13);

  rows.push({
    SheetNo: sheetNo,
    OrderNo: orderNo,
    Customer: customer,
    GlassType: glass,
    Thickness: t,
    Width: w,
    Height: h,
    Quantity: qty,
    Remarks: remark
  });
}

const sheet = XLSX.utils.json_to_sheet(rows, {
  header: ['SheetNo', 'OrderNo', 'Customer', 'GlassType', 'Thickness', 'Width', 'Height', 'Quantity', 'Remarks']
});

// Set column widths for readability
sheet['!cols'] = [
  { wch: 10 }, // SheetNo
  { wch: 10 }, // OrderNo
  { wch: 22 }, // Customer
  { wch: 12 }, // GlassType
  { wch: 10 }, // Thickness
  { wch: 8 },  // Width
  { wch: 8 },  // Height
  { wch: 9 },  // Quantity
  { wch: 28 }, // Remarks
];

const wb = XLSX.utils.book_new();
XLSX.utils.book_append_sheet(wb, sheet, 'Sheets');

const outDir = path.resolve(__dirname, '..', '..', 'sample-data');
fs.mkdirSync(outDir, { recursive: true });
const outFile = path.join(outDir, 'glass-sheets-100.xlsx');
XLSX.writeFile(wb, outFile);

console.log(`Wrote ${rows.length} rows to ${outFile}`);
