// 抠掉四叶草气运之子的洋红背景，替换 SSR/011.png
const fs = require('fs');
const { PNG } = require('pngjs');

const src = 'Assets/像素幸存者资源包/_chroma/Pixel_art_game_equipment_icon__2026-07-09T05-22-53.png';
const dst = 'Assets/像素幸存者资源包/存档装备图标/抽卡装备/SSR/011.png';

const png = PNG.sync.read(fs.readFileSync(src));
const d = png.data;
for (let i = 0; i < d.length; i += 4) {
  const r = d[i], g = d[i + 1], b = d[i + 2];
  // 纯洋红：r,b 高 g 低
  if (r > 200 && b > 200 && g < 100) d[i + 3] = 0;
  // 偏紫：b > g
  else if (b > g + 20) {
    d[i + 1] = Math.round((r + b) / 2);
    d[i + 3] = Math.round(d[i + 3] * 0.4);
  }
}
fs.writeFileSync(dst, PNG.sync.write(png));
console.log('SSR/011.png replaced:', fs.statSync(dst).size, 'bytes');
