const fs = require('fs');
const f = 'Assets/Scenes/SampleScene.unity';
let s = fs.readFileSync(f, 'utf8');
// \u79FB\u52A8\u901F\u5EA6 = 移动速度
// \u9632\u5FA1\u529B = 防御力
s = s.replace(
  'description: "\\u79FB\\u52A8\\u901F\\u5EA6\\uFF0B2\\n\\n\\u8BF6\\u54C6..."',
  'description: "\\u9632\\u5FA1\\u529B\\uFF0B2\\n\\n\\u8BF6\\u54C6..."'
);
fs.writeFileSync(f, s);
console.log('scene updated: 移动速度 -> 防御力');
