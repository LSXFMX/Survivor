const fs = require('fs');
const s = fs.readFileSync('Assets/Scenes/SampleScene.unity', 'utf8').split('\n');
function dump(a, b) { for (let i = a; i <= b; i++) console.log((i + 1) + ': ' + s[i]); console.log('----'); }
// find all lines containing script guid of battleUI and the two prefab-field regions
s.forEach((l, i) => { if (l.includes('bd1c095ce0d8e5740be2bdfdb6aa40a4')) console.log('battleUI script at line', i + 1); });
console.log('=== region ~22040 ===');
dump(22040, 22055);
console.log('=== region ~33905 ===');
dump(33905, 33925);
