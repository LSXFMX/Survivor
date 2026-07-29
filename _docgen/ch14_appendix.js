const { AlignmentType } = require("docx");
const { H1, H2, H3, P, Bullet, Num, Quote, MakeTable, ImageCentered, Caption, ImagePlaceholder, COLOR } = require("./helpers");

function build() {
  const els = [];
  els.push(H1("第十四章　未来规划与设计展望"));

  els.push(H2("14.1　后续玩法方向"));
  els.push(P("在现有系统基础上，还有以下几个方向值得继续深入设计与完善："));
  els.push(Num("局外大营地：将局内小型营地的占领玩法延伸至局外，打造一个可持续经营的家园场景，加入挂机采集玩法与角色互动，参考经营类游戏的休闲节奏"));
  els.push(Num("史莱姆社群好感度装备完整数值：补全第四个社群的好感度奖励曲线，与前三个社群形成完整闭环"));
  els.push(Num("继承装备体系：设计一套可反复刷取的词条系统，为长期玩家提供更深的数值成长空间"));
  els.push(Num("隐藏结局分支：完善随机事件解锁的隐藏结局选项，为游戏提供更强的叙事收束与重复游玩动机"));

  els.push(H2("14.2　数值与体验平衡方向"));
  els.push(Bullet("持续观察高难度区间的通关节奏，适度调整世界首领的触发范围与强度成长曲线，让挑战感保持在合理区间"));
  els.push(Bullet("无尽模式的资源消耗与强度成长需要长期验证，确保长时间游玩仍有正向反馈而不失控"));
  els.push(Bullet("试炼挑战的逐层数值曲线需结合玩家平均输出能力持续微调，避免过早卡关或过于轻松"));

  els.push(H2("14.3　叙事与世界观延展"));
  els.push(P("好感度系统本质上是将首领设计与角色叙事结合的尝试：每一个社群都对应一段被世界意识封藏的往事，玩家通过战斗逐步揭开真相并解放对应角色。这一叙事框架具备很强的延展性，后续可以："));
  els.push(Bullet("为每个社群补充更完整的背景故事与对话内容，通过营地场景呈现"));
  els.push(Bullet("在终局首领战中进一步强化社群解放程度与首领强度的联动，让玩家的养成选择在叙事上也留下痕迹"));
  els.push(Bullet("围绕隐藏结局设计多结局分支，让不同的游玩风格对应不同的故事收尾"));

  els.push(ImageCentered("dragon_gold.png", 200, 200, "终局首领设计"));
  els.push(Caption("图14-1　终局首领形态：黄金龙"));

  return els;
}

module.exports = build;
