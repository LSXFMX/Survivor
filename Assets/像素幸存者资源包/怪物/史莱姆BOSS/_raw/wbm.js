const fs = require('fs');
const s = fs.readFileSync('Assets/Scenes/SampleScene.unity', 'utf8').split('\n');
let idx = -1;
s.forEach((l, i) => { if (l.includes('8ae8bc7bf3895e8408e64ea451781b8e')) idx = i; });
if (idx < 0) { console.log('WorldBossManager NOT FOUND'); process.exit(0); }
console.log('WorldBossManager MonoBehaviour at line', idx + 1);
for (let i = idx; i < Math.min(s.length, idx + 90); i++) console.log((i + 1) + ': ' + s[i]);
