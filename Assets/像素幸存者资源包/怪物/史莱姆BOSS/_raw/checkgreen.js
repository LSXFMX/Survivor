const { PNG } = require('pngjs');
const fs = require('fs');
const p = PNG.sync.read(fs.readFileSync('d:/Survivor/Survivor/Assets/Resources/GameIcon.png'));
let green = 0, opaque = 0;
for (let i = 0; i < p.data.length; i += 4) {
  if (p.data[i + 3] > 16) {
    opaque++;
    const r = p.data[i], g = p.data[i + 1], b = p.data[i + 2];
    if (g > 160 && g > r + 20 && g > b + 20) green++;
  }
}
console.log('opaquePx', opaque, 'greenPx', green);
