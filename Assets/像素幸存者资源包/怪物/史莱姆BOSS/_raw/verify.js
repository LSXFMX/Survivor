const fs = require('fs');
const s = fs.readFileSync('Assets/Scenes/SampleScene.unity', 'utf8').split('\n');
// Find Spawnpoint's script guid
let spawnIdx = -1;
s.forEach((l, i) => { if (l.includes('f9bef4b662361804b8681e5585cee718')) spawnIdx = i; });
if (spawnIdx < 0) { console.log('Spawnpoint script NOT FOUND'); process.exit(1); }
console.log('Spawnpoint script at line', spawnIdx + 1);
// Print lines from script guid down through ~50 lines
for (let i = spawnIdx; i < Math.min(s.length, spawnIdx + 55); i++) {
  console.log((i + 1) + ': ' + s[i]);
}
