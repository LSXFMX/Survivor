const fs = require('fs');
const path = require('path');
const f = path.join(path.resolve(__dirname, '..', '..', '..', '..'), '怪物prefab', 'SlimeBoss', 'SlimeBoss.prefab');
let s = fs.readFileSync(f, 'utf8');
const eol = s.includes('\r\n') ? '\r\n' : '\n';

const startMarker = '  rolename: "\\u53F2\\u83B1\\u59C6BOSS"';
const si = s.indexOf(startMarker);
if (si < 0) { console.log('rolename marker NOT FOUND'); process.exit(1); }
const ei = s.indexOf('--- !u!54 ', si);
if (ei < 0) { console.log('Rigidbody marker NOT FOUND'); process.exit(1); }

const fields = [
  '  rolename: "\\u53F2\\u83B1\\u59C6BOSS"',
  '  health: 500',
  '  healthmax: 500',
  '  atk: 50',
  '  def: 0',
  '  speed: 3',
  '  CR: 0',
  '  CD: 0',
  '  EVA: 0',
  '  DR: 0',
  '  regen: 0',
  '  exp: 0',
  '  expmax: 0',
  '  level: 0',
  '  atknumber: {fileID: 6161263895538339157, guid: fa76286ee73de29449ad1516333066ba, type: 3}',
  '  rolestate: 0',
  '  role: {fileID: 0}',
  '  Sca: 1.6',
  '  material: {fileID: -876546973899608171, guid: 8bef114c142cd4743948e426f1da55a1, type: 3}',
  '  red: {fileID: -876546973899608171, guid: 006fb32767b8c5341bff9e7812b46530, type: 3}',
  '  expstone: {fileID: 7733258394847694891, guid: 2a634f41aed7e5a43bc79c0cd73c4360, type: 3}',
  '  bossScale: 1.6',
  '  dragonScale: 2.6',
  '  slimeSpeed: 3',
  '  mergeInterval: 6',
  '  mergeRadius: 9',
  '  mergeHealthPct: 0.05',
  '  mergeAtkPct: 0.05',
  '  swordUnlockCount: 5',
  '  bowUnlockCount: 10',
  '  swordCd: 6',
  '  bowCd: 6',
  '  weaponScale: 0.5',
  '  heldSwordSprite: {fileID: 21300000, guid: 2822cc87a9c37bddb1ba0ffebe618eef, type: 3}',
  '  heldBowSprite: {fileID: 21300000, guid: 1c37e1c37ea028a35148eaf791733d0b, type: 3}',
  '  swordQiPrefab: {fileID: 7300000000000000003, guid: 43f093b15959e9ad90ccd5a23e214fe6, type: 3}',
  '  arrowPrefab: {fileID: 7300000000000000002, guid: 3de45f5a4bcb6971ac4a4530fb73d96e, type: 3}',
  '  swordQiSpeed: 12',
  '  swordQiLifetime: 1.6',
  '  swordQiDamageMul: 1.2',
  '  arrowCount: 5',
  '  arrowSpread: 35',
  '  arrowSpeed: 18',
  '  arrowLifetime: 3',
  '  arrowDamageMul: 0.6',
  '  dragonHpThreshold: 0.1',
  '  dragonHealPct: 0.5',
  '  dragonDrainDuration: 8',
  '  slimeBreathPrefab: {fileID: 7300000000000000004, guid: f924943ebc3175528033f3881ea76921, type: 3}',
  '  breathInterval: 1',
  '  breathCount: 6',
  '  breathSpread: 70',
  '  breathSpeed: 9',
  '  breathLifetime: 3',
  '  breathDamageMul: 1',
  '',
].join(eol);

s = s.slice(0, si) + fields + s.slice(ei);
fs.writeFileSync(f, s);
console.log('SlimeBoss.prefab MonoBehaviour block rewritten.');
