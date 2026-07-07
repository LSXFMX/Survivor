const fs=require('fs');
const path=require('path');
const ASSETS=path.resolve(__dirname,'..','..','..','..');
const RESWB=path.join(ASSETS,'Resources','WorldBoss');
const f=path.join(ASSETS,'怪物prefab','WolfBoss','WolfBoss.prefab');
let s=fs.readFileSync(f,'utf8');
const eol=s.includes('\r\n')?'\r\n':'\n';
const SC_WBWOLF='ad15b546734fddd5ed35978fc5c4fac1';
const PF_WOLFWORLD='ece7d09d1d1a0e74b4390872cd040771';

// 1) WorldBossWolf.cs.meta
fs.writeFileSync(path.join(ASSETS,'C#','WorldBoss','WorldBossWolf.cs.meta'),
`fileFormatVersion: 2
guid: ${SC_WBWOLF}
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
console.log('WROTE WorldBossWolf.cs.meta');

// 2) Build WolfBossWorld.prefab
s=s.replace('m_Name: WolfBoss','m_Name: WolfBossWorld');
s=s.replace('m_Script: {fileID: 11500000, guid: a2f09da00f414aae875b8c11d4d5d1cc, type: 3}',
           'm_Script: {fileID: 11500000, guid: '+SC_WBWOLF+', type: 3}');
// Append world-boss fields after clawScreenFrames entries (last serialized field before Rigidbody)
s=s.replace('  clawScreenFrames:'+eol,
  '  clawScreenFrames:'+eol+
  '  activateRange: 15'+eol+
  '  faction: 2'+eol+
  '  worldBossManager: {fileID: 0}'+eol);
fs.writeFileSync(path.join(RESWB,'WolfBossWorld.prefab'),s);
fs.writeFileSync(path.join(RESWB,'WolfBossWorld.prefab.meta'),
`fileFormatVersion: 2
guid: ${PF_WOLFWORLD}
PrefabImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`);
console.log('WROTE WolfBossWorld.prefab (+meta) in Resources/WorldBoss');
console.log('DONE wolf');
