const fs = require('fs');
const path = require('path');
const files = [
  'Assets/C#/Audio/AudioPanelUI.cs',
  'Assets/C#/battleUI.cs',
  'Assets/C#/ClearRecordManager.cs',
  'Assets/C#/Upgrade/ChoiceUI.cs',
  'Assets/C#/WolfBoss.cs',
  'Assets/Player/PlayerSkinOverrider.cs',
  'Assets/Player/PlayerSkinSkillBuff.cs',
  'Assets/SaveEquipment/DeleteArchiveConfirm.cs',
  'Assets/Skill/TombDomain/MindControlled.cs',
];
for (const f of files) {
  const lines = fs.readFileSync(f, 'utf8').split('\n');
  for (let i = 0; i < lines.length; i++) {
    if (/[【『]/.test(lines[i])) {
      console.log(f + ':' + (i + 1) + ': ' + lines[i].trim().substring(0, 100));
    }
  }
}
