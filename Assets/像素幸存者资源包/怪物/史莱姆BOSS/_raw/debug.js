const fs = require('fs');
const s = fs.readFileSync('Assets/Scenes/SampleScene.unity', 'utf8').split('\n');

// Show the entire Spawnpoint MonoBehaviour block
let inBlock = false;
let blockStart = -1;
for (let i = 0; i < s.length; i++) {
  if (s[i].includes('m_Script: {fileID: 11500000, guid: 90ffbe9600faa204281ca486670920d6')) {
    blockStart = i;
    for (let j = i; j < Math.min(s.length, i + 40); j++) {
      console.log((j+1) + ': ' + s[j]);
    }
    break;
  }
}

// Also check if SlimeBoss prefab itself has issues — check for duplicate IDs in prefab
console.log('\n=== Checking SlimeBoss.prefab for duplicate IDs ===');
const pf = fs.readFileSync('Assets/怪物prefab/SlimeBoss/SlimeBoss.prefab', 'utf8').split('\n');
const ids = {};
let dupes = false;
pf.forEach((l, i) => {
  const m = l.match(/^--- !u![0-9]+ &(\d+)$/);
  if (m) {
    if (ids[m[1]]) { console.log('DUPLICATE ID: ' + m[1] + ' at line ' + (i+1) + ' (also at line ' + ids[m[1]] + ')'); dupes = true; }
    else ids[m[1]] = i+1;
  }
});
if (!dupes) console.log('No duplicate IDs in SlimeBoss.prefab');

// Check SlimeBoss.cs.meta references the script correctly
console.log('\n=== SlimeBoss.cs.meta ===');
const meta = fs.readFileSync('Assets/C#/SlimeBoss.cs.meta', 'utf8');
console.log(meta);
