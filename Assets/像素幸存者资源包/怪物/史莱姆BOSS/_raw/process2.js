const { PNG } = require('pngjs');
const fs = require('fs');
const path = require('path');
const RAW = __dirname;
const OUT = path.resolve(__dirname, '..');

const MAP = [
  ['Retro_16_bit_pixel_art_game_sp_2026-07-06T09-18-13.png', 'SlimeBow.png'],
  ['Retro_16_bit_pixel_art_game_ef_2026-07-06T09-18-14.png', 'SlimeSwordQi.png'],
  ['Retro_16_bit_pixel_art_game_sp_2026-07-06T09-18-19.png', 'SlimeBlob.png'],
];
function isGreen(r, g, b) { return g > 90 && g > r + 25 && g > b + 20; }

for (const [src, dst] of MAP) {
  const p = PNG.sync.read(fs.readFileSync(path.join(RAW, src)));
  const { width: W, height: H, data } = p;
  for (let i = 0; i < data.length; i += 4) {
    if (isGreen(data[i], data[i + 1], data[i + 2])) data[i + 3] = 0;
  }
  let minX = W, minY = H, maxX = -1, maxY = -1;
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    if (data[(y * W + x) * 4 + 3] > 16) {
      if (x < minX) minX = x; if (x > maxX) maxX = x;
      if (y < minY) minY = y; if (y > maxY) maxY = y;
    }
  }
  const pad = 4;
  minX = Math.max(0, minX - pad); minY = Math.max(0, minY - pad);
  maxX = Math.min(W - 1, maxX + pad); maxY = Math.min(H - 1, maxY + pad);
  const cw = maxX - minX + 1, ch = maxY - minY + 1;
  const out = new PNG({ width: cw, height: ch });
  for (let y = 0; y < ch; y++) for (let x = 0; x < cw; x++) {
    const si = ((minY + y) * W + (minX + x)) * 4, di = (y * cw + x) * 4;
    out.data[di] = data[si]; out.data[di + 1] = data[si + 1];
    out.data[di + 2] = data[si + 2]; out.data[di + 3] = data[si + 3];
  }
  fs.writeFileSync(path.join(OUT, dst), PNG.sync.write(out));
  console.log(dst.padEnd(20), cw + 'x' + ch);
}
const c = require('crypto');
console.log('GUIDS:');
for (const n of ['spr_bow','spr_qi','spr_blob','pf_qi','pf_blob']) console.log(n + '=' + c.randomBytes(16).toString('hex'));
