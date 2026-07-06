const fs = require('fs');
const f = 'Assets/Scenes/SampleScene.unity';
const s = fs.readFileSync(f, 'utf8');

// Count all occurrences of slimeBossPrefab
let count = 0, pos = -1;
while ((pos = s.indexOf('slimeBossPrefab', pos + 1)) >= 0) count++;
console.log('slimeBossPrefab occurrences in scene:', count);

// Count slimePrefab occurrences
count = 0; pos = -1;
while ((pos = s.indexOf('slimePrefab:', pos + 1)) >= 0) count++;
console.log('slimePrefab occurrences in scene:', count);

// Check file size and timestamp
const st = fs.statSync(f);
console.log('Scene file size:', (st.size / 1024 / 1024).toFixed(2), 'MB');
console.log('Scene last modified:', st.mtime.toISOString());

// Check if the Spawnpoint block has the right fields in order
const anchor = 'guid: 90ffbe9600faa204281ca486670920d6';
const si = s.indexOf(anchor);
if (si < 0) { console.log('Spawnpoint script NOT FOUND'); process.exit(1); }
// Find next fields after the script assignment
const next50 = s.substring(si, si + 2500);
const fields = ['enemylayer', 'b:', 'enemy:', 'SpawnTimer', 'maxenemy', 'timer', 'batPrefab', 'wolfPrefab', 'slimePrefab', 'slimeBossPrefab', 'wolfBossPrefab'];
let prev = 0;
for (const fld of fields) {
  const idx = next50.indexOf(fld, prev);
  if (idx >= 0) {
    console.log('  found', fld, 'at offset', idx);
    prev = idx;
  } else {
    console.log('  MISSING', fld);
  }
}

// Check if SlimeBoss.prefab loads correctly
const pfPath = 'Assets/怪物prefab/SlimeBoss/SlimeBoss.prefab';
console.log('\nSlimeBoss.prefab exists:', fs.existsSync(pfPath));
if (fs.existsSync(pfPath)) {
  const pf = fs.readFileSync(pfPath, 'utf8');
  console.log('SlimeBoss.prefab size:', pf.length, 'bytes');
  console.log('Contains "SlimeBoss":', pf.includes('m_Script: {fileID: 11500000, guid: 4faad8b42e3d7d1857cc3d13f1c52441'));
  console.log('Contains "SlimeBossProjectile":', pf.includes('SlimeSword'));
}
