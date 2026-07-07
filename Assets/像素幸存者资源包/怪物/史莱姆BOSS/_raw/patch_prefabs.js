const fs=require('fs');
const path=require('path');
const ASSETS=path.resolve(__dirname,'..','..','..','..');
const RESWB=path.join(ASSETS,'Resources','WorldBoss');

// Patch world boss prefab stats: HP/ATK doubled, regen set
function patchPrefab(relPath, overrides){
  const f=path.join(RESWB,relPath);
  let s=fs.readFileSync(f,'utf8');
  for(const [k,v] of Object.entries(overrides)){
    const re=new RegExp('^  '+k+':\\s*\\d+(\\.\\d+)?','m');
    if(s.match(re)) s=s.replace(re,'  '+k+': '+v);
    else {
      // Insert after expstone line
      s=s.replace('  expstone: {','  '+k+': '+v+'\n  expstone: {');
    }
  }
  fs.writeFileSync(f,s);
  console.log(relPath.padEnd(30),Object.entries(overrides).map(([k,v])=>k+'='+v).join(', '));
}
patchPrefab('BatBossWorld.prefab',{health:'1000',healthmax:'1000',atk:'100',naturalHealPctPerSecond:'0.2'});
patchPrefab('WolfBossWorld.prefab',{health:'1000',healthmax:'1000',atk:'100'});
patchPrefab('SlimeBossWorld.prefab',{health:'1000',healthmax:'1000',atk:'100'});
console.log('DONE prefab patching');
