const fs = require('fs');
const f = 'Assets/Scenes/SampleScene.unity';
let s = fs.readFileSync(f, 'utf8');
const eol = s.includes('\r\n') ? '\r\n' : '\n';

// Find the Spawnpoint block's slimePrefab field
const anchor = '  slimePrefab: {fileID: 3331652924953612050, guid: 42b53bffe7bf493d8b5e24e185de5015,';
const bi = s.indexOf(anchor);
if (bi < 0) { console.log('ANCHOR NOT FOUND'); process.exit(1); }
// slimePrefab line ends with "type: 3}" then eol — insert after it
const end = s.indexOf(eol, bi) + eol.length;

const ins =
  '  slimeBossPrefab: {fileID: 3331652924953612050, guid: adf2b732a3b383d9f60a36ef7d9c9775,' + eol +
  '    type: 3}' + eol;

if (s.indexOf('slimeBossPrefab: {fileID: 3331652924953612050, guid: adf2b7', bi) >= 0) {
  console.log('ALREADY WIRED in Spawnpoint');
  process.exit(0);
}

s = s.slice(0, end) + ins + s.slice(end);
fs.writeFileSync(f, s);
console.log('SPAWNPOINT slimeBossPrefab WIRED');
