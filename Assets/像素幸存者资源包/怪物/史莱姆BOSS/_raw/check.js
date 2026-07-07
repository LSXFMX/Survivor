const fs = require('fs');
const path = require('path');
function walk(d, out) {
  for (const f of fs.readdirSync(d, { withFileTypes: true })) {
    const p = path.join(d, f.name);
    if (f.isDirectory()) {
      if (['node_modules', 'Library', 'Logs', 'Temp', 'obj', 'bin'].includes(f.name)) continue;
      walk(p, out);
    } else if (p.endsWith('.cs') || p.endsWith('.txt')) {
      const s = fs.readFileSync(p, 'utf8');
      if (/[【『]/.test(s)) out.push(p.replace(/\\/g, '/'));
    }
  }
  return out;
}
const r = [];
walk('Assets', r);
r.forEach(f => console.log(f));
