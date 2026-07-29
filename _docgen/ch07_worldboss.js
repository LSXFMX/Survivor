const { AlignmentType } = require("docx");
const { H1, H2, H3, P, Bullet, Quote, MakeTable, ImageCentered, ImagePlaceholder, Caption, COLOR } = require("./helpers");

function build() {
  const els = [];
  els.push(H1("第七章　世界首领与社群好感度机制"));

  els.push(H2("7.1　世界首领激活机制"));
  const w1 = [2200, 6120];
  els.push(MakeTable(
    ["社群", "出现门槛"],
    [
      ["蘑菇社群", "中高难度区间起"],
      ["蝙蝠社群", "更高难度区间起"],
      ["狼人社群", "更高难度区间起"],
      ["史莱姆社群", "最高难度区间起"],
    ],
    w1
  ));
  els.push(Bullet("世界首领拥有可视化攻击范围圈提示，玩家进入该范围即触发战斗状态"));
  els.push(Bullet("激活后属性为原关底首领的两倍，额外获得自然回血与接触吸血效果"));

  els.push(H2("7.2　好感度获取方式"));
  els.push(Bullet("攻击命中世界首领：好感度小幅增加"));
  els.push(Bullet("击败世界首领：额外获得大量好感度"));
  els.push(Bullet("击败后需持有对应成就装备才能真正获得世界首领奖励结算，否则仅弹出提示不生效，寓意门和钥匙是打破世界屏障与世界意识的关键物品"));
  els.push(Bullet("击败演出：进入慢动作，延迟揭示社群身份，弹出好感度获得提示与已解锁加成列表"));

  els.push(H2("7.3　好感度数值体系"));
  els.push(Bullet("好感度是每个社群独立计数的数值槽，范围为零到一百，跨局持久保存，不随对局重置，机制类似存档装备"));
  els.push(Bullet("好感度达到某一档位时，会自动解锁该档位及之前所有档位的奖励与加成"));
  els.push(Bullet("好感度对应的局内成长仅在击败对应世界首领后才会生效，永久基础加成除外"));

  els.push(H2("7.4　当前已支持社群"));
  const w2 = [1600, 2200, 2200, 2320];
  els.push(MakeTable(
    ["社群", "获得好感度触发", "退场后出现", "对应首领"],
    [
      ["蘑菇社群", "解锁蘑菇滑板加十，每击败蘑菇首领加一", "蘑菇人领地", "蘑菇人首领"],
      ["蝙蝠社群", "解锁吸血鬼大君加十，每击败吸血鬼首领加一", "吸血鬼领地", "吸血鬼首领"],
      ["狼人社群", "解锁月牙吊坠加十，每击败狼人首领加一", "狼人社群领地", "狼人首领"],
      ["史莱姆社群", "同上模式", "史莱姆社群领地", "史莱姆首领"],
    ],
    w2
  ));

  els.push(ImageCentered("favor_bat_3.png", 180, 180, "蝙蝠社群好感度装备图标"));
  els.push(Caption("图7-1　蝙蝠社群好感度装备图标：血族血统"));
  els.push(ImageCentered("favor_mushroom_0.png", 180, 180, "蘑菇社群好感度装备图标"));
  els.push(Caption("图7-2　蘑菇社群好感度装备图标：孢子之心"));
  els.push(ImageCentered("bat_baby.png", 140, 140, "蝙蝠宝宝"));
  els.push(Caption("图7-3　蝙蝠社群好感度奖励宠物：蝙蝠宝宝"));

  els.push(...ImagePlaceholder("世界首领战斗界面截图，展示攻击范围提示圈与好感度获取提示"));

  return els;
}

module.exports = build;
