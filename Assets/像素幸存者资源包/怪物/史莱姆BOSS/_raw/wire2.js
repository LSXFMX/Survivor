const fs = require('fs');
const path = require('path');

const f = path.resolve(__dirname, '..', '..', '..', '..', 'Scenes', 'SampleScene.unity');
let s = fs.readFileSync(f, 'utf8');
const eol = s.includes('\r\n') ? '\r\n' : '\n';

// Locate the Spawnpoint slimePrefab line (specific to Spawnpoint, not battleUI)
const anchor = '  slimePrefab: {fileID: 3331652924953612050, guid: 42b53bffe7bf493d8b5e24e185de5015,';
const bi = s.indexOf(anchor);
if (bi < 0) { console.log('ANCHOR NOT FOUND'); process.exit(1); }

// Ensure this is the Spawnpoint occurrence (between Spawnpoint GameObject tag and the next ---)
// Just insert right after the "type: 3}" line following the anchor
const afterAnchor = s.indexOf('    type: 3}', bi);
if (afterAnchor < 0) { console.log('TYPE:3 NOT FOUND'); process.exit(1); }

// Find end of slimePrefab's type:3 line
const end = s.indexOf(eol, afterAnchor) + eol.length;

const ins =
  '  slimeBossPrefab: {fileID: 3331652924953612050, guid: adf2b732a3b383d9f60a36ef7d9c9775,' + eol +
  '    type: 3}' + eol;

// Already there?
if (s.indexOf('guid: adf2b732a3b383d9f60a36ef7d9c9775', bi) >= 0 &&
    s.indexOf('guid: adf2b732a3b383d9f60a36ef7d9c9775', bi) < s.indexOf('---', bi)) {
  console.log('ALREADY PRESENT in Spawnpoint block');
  process.exit(0);
}

s = s.slice(0, end) + ins + s.slice(end);
fs.writeFileSync(f, s);
console.log('INSERTED slimeBossPrefab into Spawnpoint block');
