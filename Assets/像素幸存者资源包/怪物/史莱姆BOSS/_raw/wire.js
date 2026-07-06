const fs = require('fs');
const f = 'Assets/Scenes/SampleScene.unity';
let s = fs.readFileSync(f, 'utf8');
const eol = s.includes('\r\n') ? '\r\n' : '\n';
const anchor = 'batBossPrefab: {fileID: 5991937037412530573';
const bi = s.indexOf(anchor);
if (bi < 0) { console.log('ANCHOR NOT FOUND'); process.exit(1); }
const bsi = s.indexOf('bossSpawnPoint: {fileID: 0}', bi);
if (bsi < 0) { console.log('bossSpawnPoint NOT FOUND'); process.exit(1); }

const already = s.indexOf('slimeBossPrefab:', bi);
if (already >= 0 && already < bsi) { console.log('ALREADY WIRED'); process.exit(0); }

const lineStart = s.lastIndexOf(eol, bsi) + eol.length;
const ins =
  '  wolfBossPrefab: {fileID: 3331652924953612050, guid: 6215e29cbe7d4c9eaff46ae17d27ef66,' + eol +
  '    type: 3}' + eol +
  '  slimeBossPrefab: {fileID: 3331652924953612050, guid: adf2b732a3b383d9f60a36ef7d9c9775,' + eol +
  '    type: 3}' + eol;
s = s.slice(0, lineStart) + ins + s.slice(lineStart);
fs.writeFileSync(f, s);
console.log('WIRED OK eol=' + JSON.stringify(eol));
