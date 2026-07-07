const fs=require('fs');
const s=fs.readFileSync('Assets/Scenes/SampleScene.unity','utf8');
const lines=s.split('\n');
let found=0;
for(let i=0;i<lines.length&&found<20;i++){
  if(lines[i].includes('狼')){
    console.log((i+1)+': '+lines[i]);
    found++;
  }
}
if(found===0) console.log('NO 狼 found');
// also check unicode-escaped forms
for(let i=0;i<lines.length&&found<30;i++){
  if(lines[i].includes('\\u72FC\\u4EBA') || lines[i].includes('\\u72fc\\u4eba')){
    console.log('ESCAPED '+(i+1)+': '+lines[i]);
    found++;
  }
}
