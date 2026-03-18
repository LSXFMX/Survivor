# 需求文档

## 简介

本功能为局内游戏新增**营地系统**与**源木货币系统**。

营地是场景中固定放置的可攻击对象，继承自 `enemy` 类，拥有独立血条 UI。玩家技能可对营地造成伤害，血量归零后营地进入"已占领"状态：外观改变、额外掉落大量经验、并开始每秒为玩家提供 1 单位源木。

源木是局内全局货币，每局游戏从 0 开始计数，来源仅为已占领的营地。当前源木数量实时显示在战斗 HUD 中。

---

## 词汇表

- **Camp（营地）**：场景中固定不可移动的可攻击对象，继承自 `enemy` 类，可被玩家技能摧毁后占领。
- **YuanMu（源木）**：局内全局货币，每局重置为 0，由已占领营地每秒产出。
- **YuanMuManager（源木管理器）**：全局单例，负责维护当前源木数量并提供增减接口。
- **CampHealthBar（营地血条）**：显示在营地上方世界空间中的血量 UI。
- **BattleUI（战斗界面）**：现有的战斗 HUD 脚本，负责显示玩家状态与计时器，需扩展以显示源木数量。
- **Bulletbase（子弹基类）**：现有子弹基类，通过 `CompareTag("enemy")` 识别并伤害敌人。
- **Attribute（属性基类）**：角色基础属性基类，包含 `health`、`healthmax`、`atk`、`def` 等字段。
- **enemy（敌人基类）**：继承自 `Attribute`，包含状态机（idle/move/dead）、`Destroy1()` 死亡方法及 `expstone` 掉落逻辑。

---

## 需求

### 需求 1：营地作为可攻击对象

**用户故事：** 作为玩家，我希望场景中存在可被技能攻击的营地，以便通过消耗技能资源来占领营地获取收益。

#### 验收标准

1. THE Camp SHALL 继承自 `enemy` 类，并携带 `"enemy"` 标签，使现有 `Bulletbase` 的碰撞伤害逻辑无需修改即可对营地生效。
2. THE Camp SHALL 在场景中保持位置固定，不执行 `enemy` 的 move 状态逻辑，不向玩家移动。
3. THE Camp SHALL 不对玩家造成碰撞伤害，禁用 `enemy` 的 `OnCollisionEnter` 攻击逻辑。
4. WHEN Camp 的 `health` 大于 0，THE Camp SHALL 保持"未占领"外观（默认 Sprite/材质）。
5. IF Camp 受到伤害后 `health` 小于等于 0，THEN THE Camp SHALL 调用占领流程（见需求 2），而非调用 `enemy.Destroy1()` 销毁自身。

### 需求 2：营地占领流程

**用户故事：** 作为玩家，我希望摧毁营地血量后触发占领效果，以便获得经验奖励并持续产出源木。

#### 验收标准

1. WHEN Camp 的 `health` 降至 0 且尚未被占领，THE Camp SHALL 切换为"已占领"外观（替换为占领状态的 Sprite 或材质）。
2. WHEN Camp 被占领，THE Camp SHALL 在占领位置生成额外的 `expstone` 掉落，掉落数量由 `Camp.bonusExpCount` 字段配置（Inspector 可调）。
3. WHEN Camp 被占领，THE Camp SHALL 启动每秒产出协程，每经过 1 秒调用一次 `YuanMuManager.Instance.Add(1)`。
4. WHILE Camp 处于已占领状态，THE Camp SHALL 持续每秒产出 1 单位源木，直到本局游戏结束。
5. THE Camp SHALL 在占领后保持存在于场景中，不执行销毁逻辑。

### 需求 3：营地血条 UI

**用户故事：** 作为玩家，我希望能看到营地的当前血量，以便判断还需要多少攻击才能占领营地。

#### 验收标准

1. THE CampHealthBar SHALL 以世界空间 Canvas 的形式显示在营地对象正上方。
2. WHEN Camp 的 `health` 发生变化，THE CampHealthBar SHALL 在同一帧内更新填充比例，填充比例 = `health / healthmax`。
3. WHILE Camp 的 `health` 大于 0，THE CampHealthBar SHALL 保持可见。
4. WHEN Camp 被占领（`health` 降至 0），THE CampHealthBar SHALL 隐藏或销毁。
5. THE CampHealthBar SHALL 始终朝向主摄像机（Billboard 效果），确保从任意视角均可读。

### 需求 4：源木货币管理

**用户故事：** 作为玩家，我希望源木数量被正确追踪，以便游戏系统能够基于源木数量提供后续功能。

#### 验收标准

1. THE YuanMuManager SHALL 以单例模式存在，在整个局内场景生命周期内唯一。
2. WHEN 一局游戏开始，THE YuanMuManager SHALL 将源木数量初始化为 0。
3. WHEN `YuanMuManager.Add(amount)` 被调用且 `amount` 大于 0，THE YuanMuManager SHALL 将当前源木数量增加 `amount`。
4. IF `YuanMuManager.Add(amount)` 被调用且 `amount` 小于等于 0，THEN THE YuanMuManager SHALL 忽略本次调用，源木数量不变。
5. THE YuanMuManager SHALL 提供只读属性 `Current` 供外部查询当前源木数量。

### 需求 5：战斗 HUD 显示源木数量

**用户故事：** 作为玩家，我希望在战斗界面实时看到当前源木数量，以便了解资源积累情况。

#### 验收标准

1. THE BattleUI SHALL 在现有 HUD 中新增一个 `TextMeshProUGUI` 文本元素，用于显示当前源木数量。
2. WHEN `YuanMuManager.Current` 发生变化，THE BattleUI SHALL 在下一帧 `Update` 中将文本更新为最新数值，格式为 `"源木: {n}"`（其中 `{n}` 为整数）。
3. WHILE 局内游戏进行中，THE BattleUI SHALL 持续显示源木文本，不因其他 UI 状态变化而隐藏。
