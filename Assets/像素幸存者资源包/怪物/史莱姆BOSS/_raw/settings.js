const fs = require('fs');
const s = fs.readFileSync('d:/Survivor/Survivor/Assets/Scenes/SampleScene.unity', 'utf8');
const lines = s.split('\n');
for (let i = 0; i < lines.length; i++) {
  if (i + 1 >= 56630 && i + 1 <= 56710) console.log((i + 1) + ': ' + lines[i]);
}
