# 实现计划：营地与源木系统

## 概述

按照设计文档，依次实现 `YuanMuManager`（单例）、`Camp`（继承 enemy）、`CampHealthBar`（血条 UI），最后扩展 `battleUI` 显示源木数量。每步均在前一步基础上构建，最终完成系统联通。

## 任务

- [x] 1. 实现 YuanMuManager 单例
  - 在 `Assets/C#/Camp/` 目录下创建 `YuanMuManager.cs`
  - 实现经典 Unity 单例模式：静态 `Instance` 属性，`Awake` 中赋值并防止重复实例
  - 私有字段 `_current`，初始值为 0
  - 公开只读属性 `Current`
  - 公开方法 `Add(int amount)`：`amount <= 0` 时直接 return，否则累加
  - _需求：4.1, 4.2, 4.3, 4.4, 4.5_

  - [ ]* 1.1 为 YuanMuManager 编写属性测试
    - **属性 5：源木累加正确性**
    - **验证需求：4.3**
    - **属性 6：非正数 Add 调用无副作用**
    - **验证需求：4.4**
    - 在 EditMode 测试中手动生成随机正整数序列（≥100次），验证 `Current == sum`
    - 随机生成非正整数（≥100次），验证 `Current` 不变
    - 注释格式：`// Feature: camp-and-yuanmu-system, Property 5/6: ...`

- [x] 2. 实现 CampHealthBar
  - 在 `Assets/C#/Camp/` 目录下创建 `CampHealthBar.cs`
  - 字段：`fillImage: Image`（Inspector 拖入），`camp: Camp`（Awake 中 `GetComponentInParent` 获取）
  - `Update()` 中每帧调用 `UpdateBar`，传入 `(float)camp.health / camp.healthmax`
  - `UpdateBar(float ratio)`：设置 `fillImage.fillAmount = ratio`
  - `Hide()`：禁用所属 Canvas GameObject
  - Billboard：`LateUpdate()` 中 `transform.LookAt(Camera.main.transform)`
  - _需求：3.1, 3.2, 3.3, 3.4, 3.5_

  - [ ]* 2.1 为 CampHealthBar 编写属性测试
    - **属性 7：血条填充比例正确性**
    - **验证需求：3.2**
    - 随机生成 `health ∈ [0, healthmax]`（≥100次），验证 `fillAmount ≈ health/healthmax`，误差 < 1e-6

- [x] 3. 实现 Camp 类
  - 在 `Assets/C#/Camp/` 目录下创建 `Camp.cs`，继承 `enemy`
  - 字段：`bonusExpCount: int`、`capturedSprite: Sprite`、`healthBar: CampHealthBar`、私有 `isCaptured: bool`
  - 覆写 `FixedUpdate()`：`rolestate` 强制保持 `idle`，跳过 move 分支，不执行位置更新
  - 覆写 `OnCollisionEnter(Collision)`：空方法，禁用对玩家的碰撞伤害
  - 覆写 `Destroy1()`：检查 `isCaptured`，若已占领则 return；否则调用 `Capture()`
  - 私有方法 `Capture()`：
    - 设置 `isCaptured = true`
    - 若 `capturedSprite != null`，替换 `SpriteRenderer.sprite`
    - 循环生成 `bonusExpCount` 个 `expstone`
    - 若 `healthBar != null`，调用 `healthBar.Hide()`
    - 启动 `YuanMuCoroutine()`
  - 私有协程 `YuanMuCoroutine()`：`while(true)` 循环，`yield return new WaitForSeconds(1f)`，调用 `YuanMuManager.Instance.Add(1)`
  - _需求：1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5_

  - [ ]* 3.1 为 Camp 编写属性测试（位置不变性）
    - **属性 1：营地位置不变性**
    - **验证需求：1.2**
    - 随机初始位置（≥100次），多次调用 `FixedUpdate`，验证 `position` 不变

  - [ ]* 3.2 为 Camp 编写属性测试（碰撞不伤害玩家）
    - **属性 2：碰撞不伤害玩家**
    - **验证需求：1.3**
    - 随机玩家血量（≥100次），触发 `OnCollisionEnter`，验证血量不变

  - [ ]* 3.3 为 Camp 编写属性测试（占领后 GameObject 持续存在）
    - **属性 3：占领后 GameObject 持续存在**
    - **验证需求：1.5, 2.5**
    - 随机 `healthmax`（≥100次），累计伤害 >= healthmax 后，验证 `isCaptured == true` 且 GameObject 未被销毁

  - [ ]* 3.4 为 Camp 编写属性测试（占领掉落数量正确性）
    - **属性 4：占领掉落数量正确性**
    - **验证需求：2.2**
    - 随机 `bonusExpCount`（≥100次），触发占领，验证生成的 expstone 数量 == bonusExpCount

- [ ] 4. 检查点 - 确认核心逻辑正确
  - 确保所有已实现的测试通过，如有疑问请向用户确认。

- [x] 5. 扩展 battleUI 显示源木数量
  - 修改 `Assets/C#/battleUI.cs`
  - 新增公开字段 `yuanmuText: TextMeshProUGUI`（Inspector 拖入）
  - 在 `Update()` 末尾追加：
    ```csharp
    if (YuanMuManager.Instance != null)
        yuanmuText.text = "源木: " + YuanMuManager.Instance.Current;
    ```
  - _需求：5.1, 5.2, 5.3_

  - [ ]* 5.1 为 battleUI 源木文本编写属性测试
    - **属性 8：源木文本格式正确性**
    - **验证需求：5.2**
    - 随机非负整数 n（≥100次），设置 `YuanMuManager.Current == n`，调用 `Update`，验证 `yuanmuText.text == "源木: " + n`

- [ ] 6. 最终检查点 - 确保所有测试通过
  - 确保所有测试通过，如有疑问请向用户确认。

## 备注

- 标有 `*` 的子任务为可选测试任务，可跳过以加快 MVP 进度
- 每个任务均引用具体需求条目以保证可追溯性
- 属性测试采用 Unity Test Framework EditMode + 手动随机生成方案（≥100次循环）
- 每条属性测试注释格式：`// Feature: camp-and-yuanmu-system, Property {编号}: {属性描述}`
- `Camp` 需挂载 `"enemy"` 标签，`Bulletbase` 无需任何修改即可对营地造成伤害
