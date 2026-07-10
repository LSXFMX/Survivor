// 抠掉眩晕星星环的绿幕 → Assets/Resources/Effects/StunStars.png
const fs = require('fs');
const path = require('path');
const { PNG } = require('pngjs');

const SRC = 'Assets/Resources/Effects/_stun/Pixel_art_game_status_effect_s_2026-07-09T03-23-01.png';
const OUT = 'Assets/Resources/Effects/StunStars.png';

const png = PNG.sync.read(fs.readFileSync(SRC));
const { width: W, height: H, data } = png;

// 纯绿幕：g 高、r/b 低、g 明显大于 r 与 b。黄星(r高)与白螺旋(r,g,b皆高)因 r>120 被保留。
const isGreen = (r, g, b) => g > 150 && r < 120 && b < 120 && g > r * 1.3 && g > b * 1.3;
const isHalo  = (r, g, b) => g > 110 && g > r * 1.15 && g > b * 1.15 && r < 160 && b < 160;

for (let i = 0; i < data.length; i += 4) {
  const r = data[i], g = data[i + 1], b = data[i + 2];
  if (isGreen(r, g, b)) { data[i + 3] = 0; }
  else if (isHalo(r, g, b)) {
    // 半透明绿边：去绿调 + 削弱 alpha
    data[i + 1] = Math.round((r + b) / 2);
    data[i + 3] = Math.round(data[i + 3] * 0.4);
  }
}

fs.mkdirSync(path.dirname(OUT), { recursive: true });
fs.writeFileSync(OUT, PNG.sync.write(png));
console.log('StunStars.png', W + 'x' + H);
