# UnityRFramework Demo

`Samples/Demo` 是 UnityRFramework 的默认实现示例。它不依赖 Expansion 或任何第三方插件，使用：

- `DefaultResourceHelper`：通过 `Resources.Load` 加载资源。
- `JsonConfigHelper`：加载每表一个 JSON 配置文件。
- `JsonLocalizationHelper`：加载 JSON 本地化语言包。
- 默认 UI、Scene、Event、WebRequest、Procedure 等框架实现。

Demo 的职责是验证默认框架能够完成启动、加载数据、进入大厅、交互、切换语言与场景切换；第三方 Helper 的对照示例应放在 `Samples/ExpansionDemo`。

## 当前功能

1. `DemoBoot` 启动框架并进入 `DemoLaunchProcedure`。
2. 启动流程初始化 Resource 模块，加载角色、敌人、任务、行为、奖励五张 JSON 配置表和 `zh-CN` 本地化文件，然后以 Single 模式进入 `DemoHall`。
3. 大厅 UI 从 `Resources/Prefabs/UI/DemoHallUI` 异步打开，展示角色、任务、公告和远征入口。
4. 角色/任务选择通过框架类型化 Event 通知相关 UI；中英文切换会刷新大厅中已创建的本地化文本。
5. 公告通过默认 WebRequest Helper 从 `StreamingAssets/Demo/Demo_Notice.json` 读取。
6. 选择英雄并接取任务后可进入 `DemoExpedition`，完成普通攻击、防御、技能、敌方行动、胜负结算与返回大厅的完整闭环。
7. 大厅顶栏的重启按钮会在不退出应用进程的情况下关闭框架、清理模块和静态组件缓存，再从构建索引 0 重新启动。

`DemoExpedition` 使用同步 FSM 编排回合，Timer 延迟敌方行动；远征 HUD 和战斗实体由场景 Binder 纳入框架管理。Demo 仍不包含存档、联网战斗或热更新。

## Demo 完整闭环规划

### 演示目标

Demo 定位为“小型、可重复运行的框架验收样例”，核心闭环如下：

```text
启动 -> 加载配置与本地化 -> 大厅选择英雄/任务 -> 可选升级
     -> 进入远征 -> 回合战斗 -> 胜负结算 -> 返回大厅 -> 可再次远征
```

本轮不实现存档、联网战斗、热更新或第三方 Helper。默认实现只需证明框架在零第三方依赖下可以正常启动、运行完整闭环并正常关闭。

### 模块覆盖

| 模块 | Demo 中的验证点 |
| --- | --- |
| Resource / Config / Localization | 加载 JSON 配置、语言包和 UGUI 资源 |
| Procedure / Scene | Launch、Menu、Expedition、Return 四段流程及 Single 场景切换 |
| UI | 框架加载的大厅窗口与场景内注册的远征 HUD |
| Entity | 场景内英雄/敌人表现对象注册、离场注销 |
| Fsm | 同步回合状态：玩家回合、敌人回合、结算 |
| Event | 选择、升级、任务、远征开始与结束通知 |
| Timer | 敌方行动和阶段切换延迟，不阻塞主线程 |
| Audio | 大厅/远征 BGM 与操作音效；资源缺失时不影响流程 |
| WebRequest | 从 StreamingAssets 读取大厅公告 |

### 实施阶段

- [x] 阶段 1：完善大厅状态，角色和任务有明确选中反馈；升级扣除金币并刷新界面；条件不足时禁止出征。
- [x] 阶段 2：建立纯运行时战斗数据和同步 FSM，支持普通攻击、防御、技能、敌方行动及胜负判定。
- [x] 阶段 3：用 UGUI 与纯色面板搭建远征 HUD，显示双方生命、回合、战斗日志、操作按钮和结算面板。
- [x] 阶段 4：远征 HUD 通过 `SceneUIFormBinder` 纳入 UI 模块，英雄和敌人通过 `SceneEntityBinder` 纳入 Entity 模块。
- [x] 阶段 5：结算金币/经验和任务状态，进入 Return 流程加载大厅，再切回 Menu，保证可重复远征。
- [x] 阶段 6：接入 Timer、Audio 和中英文文本，异常只影响对应演示能力，不阻断主流程。
- [x] 阶段 7：执行启动、完整战斗、返回大厅、重复远征和退出 Play Mode 验收，要求 Console 无未处理异常。
- [x] 阶段 8：在同一次 Play Mode 中连续执行两次应用内软重启，验证框架模块可重复关闭、重新创建并回到大厅。

最近一次完整回归验收（2026-07-16）：中文/英文切换、英雄升级、两项任务、攻击/防御/技能、两次远征结算与返回大厅均通过；场景切换后远征 UI/Entity 无残留，退出 Play Mode 前后 Console 和编译均为 0 错误。

软重启验收（2026-07-17）：进入一次 Play Mode 后连续点击两次大厅重启按钮，每次均完成 `DemoBoot -> DemoHall`，运行期间 Console 为 0 错误。

### 验收标准

1. 未选择英雄或任务时不能进入远征，并给出可见提示。
2. 英雄升级会扣除配置中的金币、提高等级并立即刷新大厅数据。
3. 远征中三个操作按钮可推进同步 FSM，敌方行动由 Timer 延迟触发。
4. 胜利发放配置奖励并完成任务；失败不发放奖励，均可返回大厅。
5. 远征场景中的 UI 和 Entity 能被框架模块查询，离场后不残留注册项。
6. 从 `DemoBoot` 启动到退出 Play Mode 全程无未处理异常；单个可选音频资源缺失时仅记录警告。
7. 不退出应用进程连续软重启两次，每次均能重新初始化框架并进入大厅，不保留旧模块引用或重复组件。

## 运行

1. 打开 `GameAssets/Scenes/DemoBoot.unity`。
2. 确认场景中的 `UnityRFramework` 预制体保留 `JsonConfigHelper` 和 `JsonLocalizationHelper` 覆盖。
3. Play。启动成功后会自动加载 `DemoHall` 并打开大厅 UI。

应用内软重启固定加载构建索引 0，因此移动端打包时必须将 `DemoBoot` 放在 Build Settings 场景列表首位。

启动成功的关键日志：

```text
[Demo] Launch: initializing resource module...
[Demo] Config: loaded 4 tables.
[Demo] Scene: DemoHall loaded.
[Demo] Menu: hall ready. waiting for player action...
```

## 数据目录

```text
Samples/Demo/
├── ConfigSource/
│   ├── Config/                 # 手工维护的 CSV/XLSX 源文件
│   └── Localization/           # 手工维护的 CSV/XLSX 源文件
├── Generated/                  # 配置表工具生成的行类型和 Codec
├── GameAssets/
│   ├── Resources/
│   │   ├── Config/Json/        # Demo 运行时加载的单表 JSON
│   │   ├── Localization/Json/  # Demo 运行时加载的语言 JSON
│   │   └── Prefabs/UI/         # 大厅 UI 预制体
│   └── Scenes/                 # DemoBoot、DemoHall、DemoExpedition
└── Scripts/
```

`Config/Json`、`Localization/Json` 下同时保留 bundle 产物，供多表合一能力验收；当前 Demo 启动为清晰展示“单表一个文件”的默认用法，逐表加载 `Demo_Character`、`Demo_Quest`、`Demo_Action` 和 `Demo_Reward`。

## 更新数据

默认管线不直接读取 Excel/XLSX。先手工将源表导出为 UTF-8 CSV，再通过 `UnityRFramework/配置表工具` 导出 JSON 与二进制产物。

本 Demo 运行的是 JSON 路径：

- Config：`Config/Json/<表名>`
- Localization：`Localization/Json/<语言代码>`

二进制产物会一并导出，用于 Config/Localization 管线验收或后续切换 `BinaryConfigHelper`、`BinaryLocalizationHelper` 的独立场景；不要在默认 Demo 场景中混用 JSON Helper 与 `.bytes` 文件。
