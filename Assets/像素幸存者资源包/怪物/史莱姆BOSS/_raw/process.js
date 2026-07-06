// Chroma-key green -> transparent, autocrop, export clean sprites.
// Run: node process.js   (cwd = this _raw folder)
const { PNG } = require('pngjs');
const fs = require('fs');
const path = require('path');

const RAW = __dirname;
const OUT = path.resolve(__dirname, '..');

// raw file -> output name
const MAP = [
  ['Retro_16_bit_pixel_art_game_sp_2026-07-06T07-51-00.png', 'SlimeBossWalk1.png'],
  ['Retro_16_bit_pixel_art_game_sp_2026-07-06T07-51-02.png', 'SlimeBossWalk2.png'],
  ['Retro_16_bit_pixel_art_game_sp_2026-07-06T07-51-03.png', 'SlimeBossWalk3.png'],
  ['Retro_16_bit_pixel_art_game_sp_2026-07-06T07-51-09.png', 'SlimeBossWalk4.png'],
  ['Retro_16_bit_pixel_art_game_sp_2026-07-06T07-51-11.png', 'SlimeBossDevour1.png'],
  ['Retro_16_bit_pixel_art_game_sp_2026-07-06T07-51-15.png', 'SlimeBossDevour2.png'],
  ['Retro_16_bit_pixel_art_game_sp_2026-07-06T07-52-20.png', 'SlimeBossTransform1.png'],
  ['Retro_16_bit_pixel_art_game_sp_2026-07-06T07-52-24.png', 'SlimeBossTransform2.png'],
  ['Retro_16_bit_pixel_art_game_sp_2026-07-06T07-52-26.png', 'SlimeBossTransform3.png'],
  ['Retro_16_bit_pixel_art_game_sp_2026-07-06T07-52-29.png', 'SlimeBossTransform4.png'],
  ['Retro_16_bit_pixel_art_game_sp_2026-07-06T07-52-54.png', 'SlimeDragonWalk1.png'],
  ['Retro_16_bit_pixel_art_game_sp_2026-07-06T07-52-55.png', 'SlimeDragonWalk2.png'],
  ['Retro_16_bit_pixel_art_game_sp_2026-07-06T07-52-59.png', 'SlimeSword.png'],
  ['Retro_16_bit_pixel_art_game_sp_2026-07-06T07-52-58.png', 'SlimeArrow.png'],
];

function isGreen(r, g, b) {
  // pure chroma green (6,255,4) and its antialiased fringe / glow halo:
  // green must clearly dominate red & blue. Protects cyan (high b) and white (high r).
  return g > 90 && g > r + 25 && g > b + 20;
}

for (const [src, dst] of MAP) {
  const p = PNG.sync.read(fs.readFileSync(path.join(RAW, src)));
  const { width: W, height: H, data } = p;

  // 1) chroma key
  for (let i = 0; i < data.length; i += 4) {
    const r = data[i], g = data[i + 1], b = data[i + 2];
    if (isGreen(r, g, b)) { data[i + 3] = 0; }
  }

  // 2) autocrop to opaque bbox (alpha>16), pad 4px
  let minX = W, minY = H, maxX = -1, maxY = -1;
  for (let y = 0; y < H; y++) {
    for (let x = 0; x < W; x++) {
      if (data[(y * W + x) * 4 + 3] > 16) {
        if (x < minX) minX = x; if (x > maxX) maxX = x;
        if (y < minY) minY = y; if (y > maxY) maxY = y;
      }
    }
  }
  if (maxX < 0) { console.log(dst, 'EMPTY - skipped'); continue; }
  const pad = 4;
  minX = Math.max(0, minX - pad); minY = Math.max(0, minY - pad);
  maxX = Math.min(W - 1, maxX + pad); maxY = Math.min(H - 1, maxY + pad);
  const cw = maxX - minX + 1, ch = maxY - minY + 1;

  const out = new PNG({ width: cw, height: ch });
  for (let y = 0; y < ch; y++) {
    for (let x = 0; x < cw; x++) {
      const si = ((minY + y) * W + (minX + x)) * 4;
      const di = (y * cw + x) * 4;
      out.data[di] = data[si]; out.data[di + 1] = data[si + 1];
      out.data[di + 2] = data[si + 2]; out.data[di + 3] = data[si + 3];
    }
  }
  fs.writeFileSync(path.join(OUT, dst), PNG.sync.write(out));
  console.log(dst.padEnd(24), cw + 'x' + ch);
}
console.log('DONE');
