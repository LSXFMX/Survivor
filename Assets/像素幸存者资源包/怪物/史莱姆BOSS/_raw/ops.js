const fs = require('fs');
const path = require('path');
const ASSETS = path.resolve(__dirname, '..', '..', '..', '..');
const RESWB = path.join(ASSETS, 'Resources', 'WorldBoss');
fs.mkdirSync(RESWB, { recursive: true });

const SC_WBSLIME = '3dc5ce325f8b6d0425ac167a01869b7e';
const PF_SLIMEWORLD = '391f355ea439b557320a1d6ef1b783c2';

function move(src, dst) {
  if (fs.existsSync(src)) { fs.renameSync(src, dst); console.log('MOVED', path.relative(ASSETS, src), '->', path.relative(ASSETS, dst)); }
  else console.log('SKIP (missing)', path.relative(ASSETS, src));
}

// 1) WorldBossSlime.cs.meta
fs.writeFileSync(path.join(ASSETS, 'C#', 'WorldBoss', 'WorldBossSlime.cs.meta'),
`fileFormatVersion: 2
guid: ${SC_WBSLIME}
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`);
console.log('WROTE WorldBossSlime.cs.meta');

// 2) Move SlimeBoss.prefab (关底 via Resources.Load) into Resources/WorldBoss
const slimeSrc = path.join(ASSETS, '怪物prefab', 'SlimeBoss', 'SlimeBoss.prefab');
const slimeDst = path.join(RESWB, 'SlimeBoss.prefab');
// read content first (before move) to build the world-boss variant
const bossYaml = fs.readFileSync(slimeSrc, 'utf8');
move(slimeSrc, slimeDst);
move(slimeSrc + '.meta', slimeDst + '.meta');

// 3) Move BatBossWorld.prefab into Resources/WorldBoss (for bat auto-wire)
move(path.join(ASSETS, '怪物prefab', 'batboss', 'BatBossWorld.prefab'), path.join(RESWB, 'BatBossWorld.prefab'));
move(path.join(ASSETS, '怪物prefab', 'batboss', 'BatBossWorld.prefab.meta'), path.join(RESWB, 'BatBossWorld.prefab.meta'));

// 4) Build SlimeBossWorld.prefab from SlimeBoss.prefab content
let s = bossYaml;
s = s.replace('m_Name: SlimeBoss', 'm_Name: SlimeBossWorld');
s = s.replace('m_Script: {fileID: 11500000, guid: 4faad8b42e3d7d1857cc3d13f1c52441, type: 3}',
              'm_Script: {fileID: 11500000, guid: ' + SC_WBSLIME + ', type: 3}');
// append world-boss fields right after breathDamageMul line
const eol = s.includes('\r\n') ? '\r\n' : '\n';
s = s.replace('  breathDamageMul: 1' + eol,
  '  breathDamageMul: 1' + eol +
  '  activateRange: 15' + eol +
  '  faction: 2' + eol +
  '  worldBossManager: {fileID: 0}' + eol);
fs.writeFileSync(path.join(RESWB, 'SlimeBossWorld.prefab'), s);
fs.writeFileSync(path.join(RESWB, 'SlimeBossWorld.prefab.meta'),
`fileFormatVersion: 2
guid: ${PF_SLIMEWORLD}
PrefabImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`);
console.log('WROTE SlimeBossWorld.prefab (+meta) in Resources/WorldBoss');
console.log('DONE ops');
