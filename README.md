# UnityRFramework  [![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/relly-sc/UnityRFramework)

轻量级 Unity 游戏框架。**Library 层纯 C#（零 UnityEngine 依赖）+ Runtime 层 Helper 桥接**，默认实现可直接启动、关闭和重启，第三方能力通过 Sample 按需接入。

## Unity 版本

框架当前以 **Unity 2022.3.49f1c1** 作为开发与验证基线。建议项目使用相同的 Unity 版本；低于该版本的 Unity 尚未进行适配测试，不保证编辑器工具、构建流程及相关 API 均可正常使用。

## 架构

```
Library/RFramework/RFramework/  ← 纯 C# 核心（.NET Standard 2.0，零 Unity/第三方依赖）
Scripts/Runtime/                ← Unity 运行时（Component + Helper 默认实现）
Scripts/Editor/                 ← 编辑器工具（Inspector、菜单项）
Samples/Expansion/                ← 与框架模块无关的可选通用组件
Samples/Sample.Demo/            ← 官方示例（仅用内置 Helper，串通全部模块）
Samples/Sample.Download/        ← DownloadModule 独立轻量验收示例
Samples/Expansion.YooAsset/     ← YooAsset 资源辅助器桥接实现
Samples/Expansion.UniTask/      ← UniTask Web 请求辅助器桥接实现
Samples/Expansion.SharpZipLib/  ← SharpZipLib ZIP 解压辅助器桥接实现
Samples/Expansion.SharpCompress/ ← SharpCompress 多格式解压辅助器桥接实现
Samples/Expansion.ExcelDataReader/ ← ExcelDataReader 配置表导出工具（EditorOnly）
Samples/Expansion.Demo/         ← 官方 Demo 的第三方资源实现覆盖层
Samples/Expansion.HybridCLR/    ← HybridCLR 通用代码热更新加载扩展
Samples/Expansion.HybridCLR.Demo/ ← Expansion.Demo 的代码热更新覆盖层
Samples/Expansion.Obfuz/        ← Obfuz 可选代码混淆构建扩展
Samples/Expansion.Tests/        ← 第三方辅助器专项验收场景
```

`main` 开发分支使用 `Samples/` 便于直接编译和维护；GitHub Actions 发布 UPM
分支时会转换为标准 `Samples~/`，安装用户通过 Package Manager 的
**Import Sample** 按需导入。

命名空间按代码层固定：Library 使用 `RFramework`，Runtime 使用
`UnityRFramework.Runtime`，Editor 使用 `UnityRFramework.Editor`，Samples 下
按分类使用 `UnityRFramework.Sample`（Sample.Demo）或
`UnityRFramework.Expansion`（Expansion.* 及通用组件）。模块子文件夹只负责组织文件，不继续扩展命名空间。
所有 Sample 的手写脚本统一放在 `Scripts/Runtime`；仅当存在编辑器脚本时创建
`Scripts/Editor`，不保留空的 Editor 文件夹。
Sample 手写脚本同样遵循框架注释规范：全部注释使用中文，所有 `public/internal`
类型与成员必须提供 XML 注释；生成代码由生成器负责，不手工补改。

所有共享数据通过 Helper 桥接模式解耦：Library 定义 `IXxxHelper` 纯 C# 接口 → Runtime 提供默认实现 → Expansion 提供第三方实现。

## 当前实现约束

- `Fsm` 与 `Procedure` 的生命周期均为同步 `void` 回调。需要网络、资源或场景 I/O 时，在状态内启动 `Task`，在 `OnUpdate` 中确认任务完成且当前状态仍是自身后，再调用同步切换 API。
- FSM 生命周期回调抛出异常后，该 FSM 会停止更新并拒绝新的切换；框架不会猜测如何回滚业务副作用。
- 每一次成功的 `Resource.LoadAssetAsync` / `LoadAssetSync` 都必须对应一次 `UnloadAsset`。若同一对象可能由多个路径或类型加载，使用 `UnloadAsset<T>(location)` 精确归还；旧对象参数重载遇到歧义会抛异常。框架打开的 UI 和显示的 Entity 已自行归还其资源引用，业务层直接加载的资源仍由业务层归还。
- `LoadAssetSync` 不会与相同资源的在途异步加载并行执行；此时应改为等待 `LoadAssetAsync`。`Scene.LoadSceneAsync` 的取消令牌只在操作开始前有效，底层场景加载一旦开始不承诺中途取消或回滚；`Single` 成功后只保留新场景账本。
- `Event.FireAsync` 可跨线程入队、主线程分发；一个事件处理器异常不会丢弃同帧其余排队事件。框架内部生命周期通知使用 `FireSafely`，订阅者异常包装为 `RFrameworkException` 后经 `IEventModule.OnError` 交给 Runtime 记录，不会回滚已经成功的模块操作。
- Entity 的加载编号从请求开始即被占用，`HideEntity` 可取消仍在加载的请求。生命周期回调失败时模块会先清理实体索引、分组和实例所有权再抛出异常；父子附加禁止形成环，一个实体更新失败不会阻断同组其他实体本帧更新。
- 网络 Helper 的回调会切回 `NetworkChannel.Update` 所在主线程处理。TCP 默认 Helper 的建连不会同步阻塞 Unity 主线程，WebSocket 不使用公开 `async void` 或同步等待关闭；主动 `Disconnect` 会发布一次断开事件。接收队列与单帧分发均有上限，过载时丢弃后续数据包而非无限占用内存。
- Runtime 提供的 TCP、UDP、WebSocket Helper 只用于保证基础连接、收发和关闭链路可运行，尚未经过生产环境的严格验证。正式项目必须按自身协议、安全、弱网、移动平台后台和并发需求扩展 `INetworkHelper`，并完成目标平台压力与异常测试。
- `LoadSceneAsync` 的内置 Resource Helper 当前只支持 `activateOnLoad: true`；延迟激活没有配套激活句柄，因此会显式抛出不支持异常而不会永久等待。
- AudioClip 统一由 `ResourceModule` 加载和精确归还，Audio Helper 只负责 AudioSource 播放、淡入淡出和协程回调。
- 模块优先级同时约束更新和关闭：共同依赖 `ResourceModule` 优先更新、最后关闭；网络优先关闭，以便断连清理期间 Event/Timer 仍可用。
- WebRequest 的并发队列按优先级调度，同优先级 FIFO；只有拿到并发槽位的请求计入 Active。
- YooAsset 扩展会把 Library 的 `object` 资源类型桥接为 `UnityEngine.Object`，以兼容 YooAsset 的资源类型校验。
- Library 层不直接输出日志，错误以 `RFrameworkException` 上报；Runtime 层统一使用 `Log`，Editor 工具可使用 Unity Editor Console。
- Editor 工具默认只保留一个最贴合其操作范围的入口：依赖 Project 选择时放在 `Assets/UnityRFramework`，依赖 Hierarchy 选择时放在 `GameObject/UnityRFramework`；只有明确需要两套选择上下文或维护者特别要求时才提供多个入口。

## 安装

支持通过 Package Manager 的 **git URL** 导入：

1. `Window → Package Manager → + → Add package from git URL`
2. 填入 UPM 发布分支地址：`https://github.com/relly-sc/UnityRFramework.git#upm`
3. 等待编译完成。

> 请使用 `#upm` 分支安装。该分支会由 GitHub Actions 根据 `main` 自动生成，
> 并将示例目录发布为 UPM 标准的 `Samples~`；请勿直接使用 `main` 分支安装。

**Samples（可选）**：在 Package Manager 中选中本包 → **Samples** → 按需点击 **Import**。

- `Sample.Demo`：官方可运行示例，**仅依赖内置 Helper、零第三方**。导入后先执行
  `UnityRFramework/Demo/Export Config and Localization`，该菜单会将配置、
  本地化、音频和公告同步到宿主工程的 `Assets/StreamingAssets`，完成后再打开
  `GameAssets/Scenes/DemoBoot.unity`。仅导入 Sample 后直接运行会缺少这些文件。
- `Sample.Download`：内置 `DownloadModule` 的独立轻量验收场景，不依赖其他 Sample；
  覆盖进度、取消、断点续传、大小与 SHA-256 校验、ZIP 解压和软重启。导入后打开
  `GameAssets/Scenes/DownloadAcceptance.unity`，完整步骤见 Sample 自带 README。
- `Expansion.YooAsset`：YooAsset 资源辅助器桥接实现。需手动安装 YooAsset 3.0.5+。
- `Expansion.UniTask`：UniTask Web 请求辅助器桥接实现。需手动安装 UniTask。
- `Expansion.ExcelDataReader`：ExcelDataReader 配置表导出工具，支持从 XLSX/XLS
  生成 Config 与 Localization 数据；已内置 EditorOnly 依赖 DLL，无需手动安装。
- `Expansion.Tests`：第三方 Helper 的可运行专项验收示例。必须先导入
  `Expansion.YooAsset` 与 `Expansion.UniTask`、安装 YooAsset 与 UniTask，再执行菜单
  `UnityRFramework/ExpansionAcceptance/Rebuild Acceptance Assets`。UPM 只复制
  Sample 目录，不会自动生成 `Assets/StreamingAssets` 下的 Web 探针和 YooAsset
  内置包文件；完整准备步骤见 Expansion.Tests 自带 README。
- `Expansion.Demo`：官方 Demo 的第三方 Helper 覆盖层。必须同时导入 `Sample.Demo` 与
  `Expansion.YooAsset`、`Expansion.UniTask`、安装
  YooAsset 与 UniTask，再执行菜单
  `UnityRFramework/ExpansionDemo/Rebuild Demo Overlay`。它复用 Sample.Demo 的业务脚本
  和资源，只生成第三方框架预制体、启动场景与 YooAsset 收集规则。
- `Expansion.SharpZipLib`：可选 ZIP 解压扩展，随 Sample 提供 SharpZipLib 1.4.2 Runtime DLL
  与 MIT 许可证。通过 `GameEntry.Download.SetArchiveHelper(...)` 注入后支持 Zip64、加密 ZIP
  和解压进度；完整用法见该 Sample 的 README。
- `Expansion.SharpCompress`：可选多格式解压扩展，随 Sample 提供 SharpCompress 0.50.1
  Runtime DLL、必要依赖与许可证。支持 ZIP、RAR、7z、TAR、GZip 和 BZip2；完整用法与安全边界
  见该 Sample 的 README。
- `Expansion.HybridCLR`：可选 HybridCLR 代码热更新加载扩展。需手动安装并通过
  `HybridCLR/Installer...` 初始化 HybridCLR；核心包和普通 Demo 不依赖它。
- `Expansion.HybridCLR.Demo`：在 `Expansion.Demo` 的 YooAsset 资源热更闭环上叠加代码
  热更新。必须同时导入 `Sample.Demo`、`Expansion.Demo`、`Expansion.YooAsset`、
  `Expansion.UniTask` 与 `Expansion.HybridCLR`。执行
  `UnityRFramework/Expansion/HybridCLR Demo/重建当前平台覆盖层` 生成当前平台代码产物和
  启动覆盖层；它使用独立的 YooAsset Package、收集分组和 Host 发布目录，不会把代码
  热更新资源写入普通 `Expansion.Demo` 的 Package。详细首包、Host 更新和 Player 验收
  顺序见该 Sample 的 README。
- `Expansion.Obfuz`：可选 Obfuz 代码混淆构建扩展。需手动安装并配置 Obfuz 与
  `Obfuz4HybridCLR`；导入后可在构建工具中启用 Obfuz 步骤。它不属于核心包强制依赖，
  未导入 Obfuz 时不会影响框架、普通 Sample 或其他未启用 Obfuz 步骤的构建。
- `Expansion`：与框架模块无关的通用组件和开发辅助能力。

> 核心包仅依赖 Unity 官方维护的 `com.unity.nuget.newtonsoft-json`；当前已接入的
> YooAsset、UniTask、ExcelDataReader 和 HybridCLR 分别位于对应的可选 Expansion Sample，
> 均不会成为核心包的强制第三方依赖。

## 编辑器构建工具

通过 `UnityRFramework → 构建工具` 打开窗口。构建工具使用项目自己的 Profile 资产统一
管理平台参数、输出目录、构建场景和可选步骤；Profile 默认创建在
`Assets/BuildProfiles`，不会写入框架包目录。

窗口分为三个页签：

- **Profile**：选择或创建 Profile，设置用途分档（Flavor）和构建方案（Recipe），查看
  校验结果、当前任务、最近构建及执行构建命令。
- **平台、输出与场景**：设置目标平台、PlayerSettings 参数、脚本后端、裁剪级别、版本与
  构建号、输出规则和参与构建的场景。
- **构建步骤**：挂载并启用当前项目已经注册的 Config、YooAsset、HybridCLR、Obfuz 等
  步骤。未导入对应 Expansion 或第三方插件时，不会影响核心构建工具；只有主动启用缺失
  能力才会阻止构建。

### Flavor 与 Recipe

Flavor 只表示构建用途，并提供一组可显式应用的调试参数建议，不会自动切换 Mono/IL2CPP：

| Flavor | 推荐用途 | 推荐调试参数 |
|--------|----------|--------------|
| `Release` | 正式发布 | 关闭 Development Build、脚本调试和 Profiler |
| `Qa` | 测试验收 | 开启 Development Build 与脚本调试 |
| `Development` | 开发调试 | 开启 Development Build、脚本调试和自动连接 Profiler |

Recipe 决定本次实际执行范围：

| Recipe | 执行范围 |
|--------|----------|
| `Player` | 应用 Profile 参数并构建 Player，不发布资源或热更代码 |
| `Assets` | 导出 Config 并构建 YooAsset 资源包，不构建 Player |
| `HotUpdate` | 准备 HybridCLR/Obfuz 热更代码并构建 YooAsset 资源包，不构建 Player |
| `Release` | 配置导出、Player 准备与构建、热更代码发布和资源构建的完整流程 |

具体步骤仍以 Profile 中已启用的条目为准。脚本后端必须根据项目技术栈单独选择；例如
使用 HybridCLR 时应选择 IL2CPP，不能依赖 Flavor 自动修改。

### 构建与产物

1. 创建或选择 Profile，填写平台、输出和场景设置。
2. 在“构建步骤”页签点击“初始化全部已注册”，只启用本次需要的步骤并完成其配置。
3. 点击“重新校验”，处理全部错误；警告应根据项目发布要求确认。
4. 使用“构建资源”执行 Assets Recipe，使用“构建 Player”执行 Player Recipe，或使用
   “按 Recipe 构建”执行 Profile 当前选择的 Recipe。

“应用参数”会立即把 Profile 参数持久写入当前 Unity 工程设置；普通构建任务则使用临时
设置事务，并在成功、失败或取消后恢复构建前设置。构建期间不要强制关闭 Unity，尤其不要
中断 Player 原生编译；若发生强制中断，应先作废残留任务，再使用 Unity 官方 Build
完整构建一次 Player 后继续。

Player 与 Release 的 `build-report.json` 位于对应 Player 产物目录；Assets 与 HotUpdate
报告位于 `Bundles/BuildReports/{创建时间}_{Recipe}`。只有 Player 或 Release 成功产出
Player 后才会按 Profile 设置递增 Build Number。

当前已实际验收 Windows、Android、macOS、iOS 与 WebGL 构建和运行。Linux 目标选项保留，
但暂未纳入当前版本的实际验收范围。YooAsset、HybridCLR 和 Obfuz 的安装、配置及产物准备
要求见各自 Expansion README。

## 模块

| 模块 | 职责 | 入口 |
|------|------|------|
| **Framework** | 框架启动/停止、模块逐帧调度、全局 Helper 安装及运行参数控制 | `GameEntry.Framework` |
| **Log** | Unity Console + 本地日志文件输出，按大小分卷并清理过期文件 | `Log.Info/Warning/Error` |
| **Event** | 解耦消息通信，类型路由，`Fire<T>`（零 GC）+ `FireSafely<T>`（生命周期通知）+ `FireAsync<T>`（线程安全） | `GameEntry.Event` |
| **Pool** | GameObject 池 + class 池，委托注入，预热 | `GameEntry.Pool` |
| **Timer** | delay/interval/duration/maxTriggerCount 四参数计时器 | `GameEntry.Timer` |
| **Resource** | 资源异步加载，引用计数，并发去重 | `GameEntry.Resource` |
| **WebRequest** | HTTP GET/POST/PUT/DELETE，并发控制，超时+重试，multipart 上传+进度 | `GameEntry.WebRequest` |
| **Download** | 大文件可靠下载，`.part` 断点续传、重试、速度/ETA、大小与 SHA-256 校验 | `GameEntry.Download` |
| **Config** | 配置表管理与查询，默认 JSON + 内置 URFC 二进制，可扩展自定义格式 | `GameEntry.Config` |
| **Fsm** | 同步通用有限状态机，泛型 Owner，生命周期异常后停止运行 | `GameEntry.Fsm` |
| **Procedure** | 同步游戏流程 FSM，Blackboard 跨状态共享数据 | `GameEntry.Procedure` |
| **Entity** | 游戏实体生命周期，实体组自管实例缓存，父子附加 | `GameEntry.Entity` |
| **Scene** | 场景异步加载/卸载，状态追踪，防并发 | `GameEntry.Scene` |
| **UI** | UI 窗口栈管理，层级排序，FullScreen 自动隐藏 | `GameEntry.UI` |
| **Audio** | BGM/SFX/UI 三组，AudioSource 池，淡入淡出 | `GameEntry.Audio` |
| **Network** | 多通道管理，TCP/UDP/WebSocket 三协议，心跳/重连 | `GameEntry.Network` |
| **Localization** | 多语言管理，占位符格式化，Inspector 配置默认语言 | `GameEntry.Localization` |

## 快速开始

将 `Assets/UnityRFramework/Prefabs/UnityRFramework.prefab` 拖入启动场景。预制体包含所有模块 Component，Inspector 中可切换 Helper 实现。访问模块统一走 `GameEntry`：

```csharp
GameEntry.Pool.CreateGameObjectPool("Bullet", bulletPrefab, parent: bulletRoot);
GameEntry.Framework.FrameRate = 60;
GameEntry.Localization.GetString("ui_login_button");
GameEntry.Network.CreateChannel("Chat").ConnectAsync("127.0.0.1", 9000);
```

## 使用说明

### Framework

```csharp
// 帧率与游戏速度
GameEntry.Framework.FrameRate = 60;
GameEntry.Framework.GameSpeed = 1.5f;

// 暂停/恢复
GameEntry.Framework.PauseGame();
GameEntry.Framework.ResumeGame();

// 后台运行与休眠
GameEntry.Framework.RunInBackground = true;
GameEntry.Framework.NeverSleep = true;
```

`UnityRFrameworkController` 以 `-10000` 执行顺序负责框架启动、逐帧调度和停止，并在初始化时安装 Log Helper 与 JSON Helper：

| 类型 | 默认实现 | 可选实现 | 区别与注意事项 |
|---|---|---|---|
| Log | `DefaultLogHelper` | 项目自定义 `ILogHelper` | 同时写 Unity Console 和日志文件。桌面平台写到应用数据目录同级的 `Logs/UnityRFramework`，移动平台写到 `persistentDataPath/Logs/UnityRFramework`；包含分卷和过期清理。 |
| JSON | `DefaultJsonHelper` | `NewtonsoftJsonHelper`、项目自定义 `IJsonHelper` | 只服务 `Utility.Json`，不决定 Config/Localization 的文件格式。 |

框架内置模块和内置 Helper 已处理 IL2CPP 裁剪。项目通过 Inspector 类型名接入自定义
Helper 时，仍需由项目使用 `link.xml` 或 `UnityEngine.Scripting.Preserve` 保留对应类型及
公共无参构造函数；仅把完整类型名写入字符串不会形成 Linker 可识别的静态引用。

`UnityRFrameworkController` 的 `JSON Helper` 默认使用 `DefaultJsonHelper`（`JsonUtility`），
保证最小配置即可启动。需要属性、字典、顶层数组或更完整的 JSON 兼容性时，可在
Inspector 下拉框切换为 `UnityRFramework.Runtime.NewtonsoftJsonHelper`：

```csharp
Utility.Json.SetJsonHelper(new NewtonsoftJsonHelper());

string json = Utility.Json.ToJson(data);
MyData result = Utility.Json.ToObject<MyData>(json);
```

`NewtonsoftJsonHelper` 使用 Unity 官方维护的
`com.unity.nuget.newtonsoft-json`，并明确关闭 `TypeNameHandling`，不会根据输入
JSON 中的 `$type` 创建任意运行时类型。它只替换通用 `Utility.Json` 序列化器，
不改变 `JsonConfigHelper` 或 `JsonLocalizationHelper` 的文件格式与加载路径。

### Log

```csharp
Log.Info("玩家 {0} 登录，等级 {1}", playerName, level);
Log.Warning("资源 {0} 加载超时", assetPath);
Log.Error("连接服务器失败：{0}", errorMessage);
```

Log 不是独立模块；它使用 Framework 初始化的 `ILogHelper`。Runtime 的 `Log` API
采用安全写入，框架尚未启动或已经停止时会忽略迟到日志，避免异步收尾影响关闭流程。默认
`DefaultLogHelper` 会落盘，若项目不允许写本地日志、需要上传日志或需要接入平台 SDK，
应替换 Framework 的 Log Helper，而不是修改业务调用点。

### Event

```csharp
// 定义消息（struct = 零 GC）
public struct PlayerDeadEventArgs { public int PlayerId; public Vector3 Position; }

// 订阅
GameEntry.Event.Subscribe<PlayerDeadEventArgs>(OnPlayerDead);

// 同步发布（零 GC，立即分发）
GameEntry.Event.Fire(new PlayerDeadEventArgs { PlayerId = 1, Position = pos });

// 异步发布（线程安全，下一帧分发）
GameEntry.Event.FireAsync(new PlayerDeadEventArgs { PlayerId = 1, Position = pos });

// EventGroup 批量管理订阅生命周期
private EventGroup eventGroup;

private void OnEnable()
{
    eventGroup = GameEntry.Event.CreateGroup();
    eventGroup.Subscribe<PlayerDeadEventArgs>(OnPlayerDead);
    eventGroup.Subscribe<GameOverEventArgs>(OnGameOver);
}

private void OnDisable()
{
    eventGroup.Dispose(); // 自动取消所有订阅
}
```

Event 模块没有 Helper，订阅、同步分发和线程安全异步入队均由 Library 实现。

### Pool

```csharp
// class 池（委托注入）
var dataPool = GameEntry.Pool.CreatePool<BulletData>("BulletData",
    createFunc: () => new BulletData(),
    onSpawn: obj => obj.Reset(),
    onUnspawn: obj => obj.Clear(),
    capacity: 64);

var data = dataPool.Spawn();
dataPool.Unspawn(data);

// GameObject 池（一行创建）
var bulletPool = GameEntry.Pool.CreateGameObjectPool(
    "Bullet", bulletPrefab, parent: bulletRoot, prewarmCount: 20);

var bullet = bulletPool.Spawn();   // SetActive(true)
bulletPool.Unspawn(bullet);        // SetActive(false) + 挂回 parent
```

Pool 模块没有 Helper。普通对象池由 Library 管理，GameObject 的激活、失活和父节点恢复
由 Runtime 提供的工厂委托接入，不需要在 Inspector 选择实现。

### Timer

```csharp
// 一次性延迟 3 秒
var timer = Timer.CreateOnce(3f, () => Log.Info("3 秒后执行"));

// 每 1 秒重复，忽略 Time.timeScale
var timer = Timer.CreateRepeat(0f, 1f, () => Tick(), ignorTimescale: true);

// 注册到模块开始运行
GameEntry.Timer.Register(timer);

// 暂停 / 恢复 / 取消
timer.Pause();
timer.Resume();
timer.Cancel();
```

Timer 模块没有 Helper，由框架 Update 驱动；计时使用逻辑时间还是不受缩放的真实时间，
由创建计时器时的参数决定。

### Resource

```csharp
// 默认 Resources Helper：相对于任意 Resources 目录，扩展名可写可不写
var prefab = await GameEntry.Resource.LoadAssetAsync<GameObject>("Prefabs/Player.prefab");

// Build Settings 场景使用场景路径
await GameEntry.Resource.LoadSceneAsync("Assets/Scenes/Battle.unity", 1); // sceneMode: 1=Additive 叠加

// 卸载
GameEntry.Resource.UnloadAsset(prefab);
await GameEntry.Resource.UnloadSceneAsync("Assets/Scenes/Battle.unity");
GameEntry.Resource.UnloadUnusedAssets();
```

资源与场景异步加载均可传入 `IProgress<float>`：

```csharp
var progress = new Progress<float>(value =>
{
    float percent = value * 100f;
});

Texture2D texture = await GameEntry.Resource.LoadAssetAsync<Texture2D>(
    "Images/guide.png", ct: cancellationToken, onProgress: progress);
await GameEntry.Resource.LoadSceneAsync(
    "Scenes/Hall", onProgress: progress);
```

`DefaultResourceHelper` 的资源入口底层使用同步 `Resources.Load`，只能报告开始 0 和完成 1；
其场景入口使用 `SceneManager.LoadSceneAsync`，可连续报告进度。`LocalFileResourceHelper` 的本地文件请求
和 `YooAssetResourceHelper` 的资源/场景加载可连续报告底层进度。缓存命中直接报告 1；同资源并发去重时，
后加入的等待者只报告 0/1，不共享首请求的中间进度。

| 内置 Helper | 实现与加载顺序 | location 规则 | 适用范围与注意事项 |
|---|---|---|---|
| `DefaultResourceHelper` | `Resources.Load`；场景使用 `SceneManager` | Unity 资源传相对 `Resources` 目录的路径，扩展名会被移除；场景必须加入 Build Settings，可传完整路径或场景名 | 零配置、小项目和原型。没有版本、远端下载或磁盘更新能力。`Resources.Load` 的异步入口只是 Task 形式，底层仍不能真正取消。 |
| `LocalFileResourceHelper` | `persistentDataPath` 同名文件 → `StreamingAssets` → `DefaultResourceHelper` | 本地文件必须是安全的相对路径并保留扩展名，例如 `Audio/guide.ogg`；回退 Resources 时仍按 Resources 相对路径解释 | XR、展陈和需要现场替换文件的项目。只直接构造文本、字节、图片和音频；Prefab、Material、Component 和场景仍回退默认实现。Android/WebGL 的 StreamingAssets 必须异步读取。 |

Resource Helper 决定所有上层模块中“资源路径”的含义。Entity、UI、Audio、Config 和
Localization 不会再次改写路径；切换 Helper 后必须同步检查这些模块传入的 location。

`LocalFileResourceHelper` 用于频繁替换文字、配置、语音、图片和视频的本地项目，加载优先级为：

```text
persistentDataPath 同名覆盖文件 -> StreamingAssets 随包文件 -> Resources 兜底
```

```csharp
// 文件位置必须带扩展名；二进制始终使用 byte[]，避免文本重编码。
byte[] table = await GameEntry.Resource.LoadAssetAsync<byte[]>("Config/items.bytes");
Texture2D image = await GameEntry.Resource.LoadAssetAsync<Texture2D>("Images/guide.png");
AudioClip voice = await GameEntry.Resource.LoadAssetAsync<AudioClip>("Audio/guide.wav");

// 通过框架 Audio 模块播放文件音频时使用异步入口。
await GameEntry.Audio.PlayBgmAsync("Audio/guide.ogg", loop: true);
await GameEntry.Audio.PlaySfxAsync("Audio/click.wav");

// VideoClip 不是运行时可构造资源，VideoPlayer 直接消费本地路径或 URL。
videoPlayer.url = GameEntry.Resource.GetAssetUrl("Video/guide.mp4");

// 下载器替换 persistentDataPath 中的同名音频后，清缓存再播放。
GameEntry.Audio.ClearCache();
```

可替换文件支持 `byte[]`、`string`、`TextAsset`、PNG/JPG `Texture2D`/`Sprite`，以及
由 Unity 运行时解码的 WAV/OGG/MP3 `AudioClip`。框架不实现音频编解码器，也不保证每种
格式在所有目标平台完全一致；兼容性优先可使用 WAV，体积优先可使用 OGG，均需在目标设备验收。
Prefab、Material、Component 等 Unity 资产和场景继续使用
Resources/Build Settings。Android 与 WebGL 的 StreamingAssets 位于 URL 中，必须异步加载；
音频在所有平台也只支持异步文件加载。外部下载器应把更新写入 `persistentDataPath` 的同名相对路径，
已缓存资源需先 `UnloadAsset<T>(location)`，再重新加载才能看到新文件；Audio 模块使用
`ClearCache()` 停止播放并清空其内部音频缓存。

第三方 Resource Helper 的地址、运行模式、下载、缓存和场景规则不属于核心默认契约。
当前 YooAsset 实现见 `Samples/Expansion.YooAsset/`。

### WebRequest

```csharp
// GET 请求
var response = await GameEntry.WebRequest.GetAsync("https://api.example.com/data");

// POST JSON（需自行序列化为 JSON 字符串，框架不自动序列化对象）
string json = "{\"username\":\"player1\",\"password\":\"123456\"}";
var result = await GameEntry.WebRequest.PostAsync("https://api.example.com/login", json);

// 带超时和取消
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var data = await GameEntry.WebRequest.GetAsync(url, ct: cts.Token);
```

核心只提供 `DefaultWebRequestHelper`：基于 `UnityWebRequest + Coroutine`，普通响应保存在
内存。`GameEntry.WebRequest.DownloadFileAsync` 是一次性流式文件下载，直接写目标路径，失败或取消时删除未完成文件；
`DownloadFileRangeAsync` 是供 Download 模块和自定义下载器使用的底层 Range 写入接口，会返回 HTTP 状态与响应头。
它不负责 JSON 对象序列化、登录态、签名或业务重试；这些由调用方或项目封装处理。
依赖 UniTask 的实现属于 Expansion，见 `Samples/Expansion.UniTask/`。

### Download

```csharp
string savePath = Path.Combine(Application.persistentDataPath, "Patch", "content.bytes");
var options = new DownloadOptions
{
    ExpectedSize = manifest.Size,
    ExpectedSha256 = manifest.Sha256,
    MaxRetries = 2
};

var progress = new Progress<DownloadProgress>(value =>
{
    DownloadStage stage = value.Stage; // Preflight / Downloading / Verifying / Extracting
    float percent = value.Progress;
    double mbPerSecond = value.BytesPerSecond / (1024d * 1024d);
    TimeSpan? eta = value.EstimatedRemaining;
});

DownloadResult result = await GameEntry.Download.DownloadAsync(
    downloadUrl, savePath, options, progress, cancellationToken);
```

下载 ZIP 并在校验后解压：

```csharp
var options = new DownloadOptions
{
    ExpectedSha256 = manifest.Sha256,
    ExtractArchive = true,
    ArchiveFormat = ArchiveFormat.Auto,
    ExtractDirectory = Path.Combine(Application.persistentDataPath, "Content"),
    DeleteArchiveAfterExtraction = false,
    MaxArchiveEntries = 10000,
    MaxExtractedBytes = 8L * 1024L * 1024L * 1024L
};

DownloadResult result = await GameEntry.Download.DownloadAsync(
    zipUrl, zipSavePath, options, progress, cancellationToken);
```

Download 模块依赖 WebRequest 模块，默认使用 `目标路径.part` 保存分片。服务端支持 HTTP Range 时会续传；
若服务端忽略 Range 并返回完整内容，会删除旧分片后重新完整下载。成功后先校验预期大小和可选 SHA-256，
再将 `.part` 提交为最终文件。用户取消、网络失败或框架关闭会保留分片，便于下次继续；
同一最终路径不允许并发下载。它不负责上传、下载任务持久化、后台系统通知或业务版本管理。

设置 `ExpectedSize` 后默认先通过 HEAD 尝试读取远端 `Content-Length`，明确不一致时会在传输前失败；
服务器不支持 HEAD、未提供长度或使用非 identity 的 `Content-Encoding` 时继续下载。远端声明可能缺失或不可信，
因此完成后的实际文件大小校验始终保留。可通过 `PreflightRemoteSize = false` 关闭这次额外 HEAD 请求。

ZIP 使用 .NET 标准库在后台线程解压，不依赖第三方，并通过同一 `DownloadProgress` 报告解压阶段、
解压后字节和当前条目。模块先解压到独立临时目录，拒绝绝对路径和 `../` 路径穿越，
并限制条目数与解压总大小；全部成功后才替换正式目录。解压失败会清理临时目录、保留已校验的压缩文件，
已有正式目录保持不变。默认不删除压缩文件；内置 `DefaultArchiveHelper` 只支持无密码 ZIP。项目可实现
`IArchiveHelper` 并通过 `GameEntry.Download.SetArchiveHelper(...)` 注入第三方解压器，
但仍须实现路径边界、条目数和解压总大小限制。

可选 `Expansion.SharpZipLib` 和 `Expansion.SharpCompress` 已实现该接口。前者专注 ZIP、Zip64
和加密 ZIP；后者支持 ZIP、RAR、7z、TAR、GZip 和 BZip2。导入对应 Sample 后，推荐直接在
`DownloadComponent` Inspector 的 `Archive Helper` 下拉框选择所需 Helper。
它属于 Download 模块自身配置，不放在全局 Controller 上。也可以在首次下载前通过代码注入：

```csharp
GameEntry.Download.SetArchiveHelper(new SharpZipLibArchiveHelper());
```

它们不会自动替换核心默认实现，避免仅导入 Sample 就改变现有项目行为。单次任务密码通过
`DownloadOptions.ArchivePassword` 传入；具体依赖、能力边界和用法见对应 Sample 的 README。

### Config

```csharp
// 默认 Helper 从 UTF-8 JSON 原始字节加载；文件位于 Assets/Resources/Config/Json/items.json
await GameEntry.Config.LoadConfigAsync<ItemConfig>("Config/Json/items.json");

// 二进制：先用 UnityRFramework/配置表工具生成 URFC v2 和静态 Codec，
// 再在 Inspector 选择 BinaryConfigHelper
await GameEntry.Config.LoadConfigAsync<ItemConfig>("Config/Binary/items.bytes");

// 多表容器：当前 Helper 必须为 JsonConfigHelper 或 BinaryConfigHelper
await GameEntry.Config.LoadConfigBundleAsync("Config/Json/ConfigBundle.json");
// BinaryConfigHelper 对应加载 Config/Binary/ConfigBundle.bytes

// JSON 模式（直接解析字符串）
string json = "[{\"Id\":1,\"Name\":\"Sword\"},{\"Id\":2,\"Name\":\"Shield\"}]";
GameEntry.Config.LoadConfigFromString<ItemConfig>(json);

// 按 ID 查询
var item = GameEntry.Config.GetConfig<ItemConfig>(1);

// 遍历全部
var all = GameEntry.Config.GetAllConfigs<ItemConfig>();
foreach (ItemConfig item in all) { Log.Info($"{item.Id}: {item.Name}"); }

// 安全检查
if (GameEntry.Config.HasConfigRow<ItemConfig>(1001)) { ... }

// 自定义模式：继承 ConfigHelperBase，适配项目私有二进制或文本格式
```

| 内置 Helper | 单表格式 | Bundle 格式 | 默认 location 示例 | 注意事项 |
|---|---|---|---|---|
| `JsonConfigHelper` | UTF-8 JSON | JSON 多表容器 | `Config/Json/Item.json` | 默认选择，便于检查和手工排错；解析器按生成行类型转换，不等同于 Base 的 JSON Helper。 |
| `BinaryConfigHelper` | URFC v2，兼容 URFC v1 | URFM v1 | `Config/Binary/Item.bytes` | 需要使用配置表工具生成 `.bytes`；URFC v2 依赖生成 Codec，并校验 TableId、SchemaHash 和 CRC32。 |
| 自定义 `ConfigHelperBase` | 项目自定 | 实现 `IConfigBundleHelper` 后可支持 | 调用方显式传入 | `ParseConfig(Type, byte[])` 必须与导出端完全一致；Helper 只解析字节，文件从哪里加载仍由 Resource Helper 决定。 |

`ParseConfig(Type, byte[])` 的字节格式由当前 `IConfigHelper` 决定。框架默认 JSON；`BinaryConfigHelper` 兼容反射映射的 URFC v1，并使用生成 Codec 读取带 TableId、SchemaHash 和 CRC32 的 URFC v2。JSON/二进制默认 Helper 还实现可选 `IConfigBundleHelper`，分别读取 JSON 多表容器与 URFM v1。项目私有格式可直接继承 `ConfigHelperBase`。

JSON 与 URFC v2 均支持显式历史 Schema 迁移。二进制实现 `IBinaryConfigMigration` 并注册到
`BinaryConfigMigrationRegistry`；JSON 实现 `IJsonConfigMigration` 并注册到
`JsonConfigMigrationRegistry`。生成代码会通过 `ConfigSchemaRegistry` 注册两种格式共用的
当前 `TableId/SchemaHash`。迁移目标必须等于当前 Schema，未知、未来或目标过期的 Schema
均拒绝。JSON 新格式为 `Tables -> 表名 -> { TableId, SchemaHash, Rows }`；旧的
`Tables -> 数组`、`Items` 和顶层数组仍可读取，但无 SchemaHash，不能参与显式迁移。

框架没有独立 DataModule，配置数据统一由 ConfigModule 管理。零第三方 Editor 转换工具位于菜单 `UnityRFramework/配置表工具`：Config 与 Localization CSV 均使用“字段名、类型、注释”三行表头，第四行开始为数据。Config 必须包含唯一 `int Id`；Config 第一行任意位置以 `!` 开头的字段名表示整列策划备注，该列不会进入校验、代码、SchemaHash、JSON 或二进制产物。Localization 固定为 `Key,Value`、`string,string`，并以唯一 `string Key` 为主键。工具同时生成 JSON、配置行、静态 Codec、URFC v2、URFM v1 多表容器、带 CRC32 的 URFL v2 和 URLM v1 多语言容器，并仅在内容变化时写入。默认流程由 Excel 手动导出 UTF-8 CSV，再由工具生成 JSON/`.bytes`。可选 Expansion 提供 ExcelDataReader Editor 工具，以明确分区直接把 `.xlsx` / `.xls` Config 导出为 JSON、URFC v2 和配置代码，把 Localization 导出为 JSON、URFL v2 和 URLM v1，不让 Excel 依赖进入 Runtime。Config 的 JSON/`.bytes` 共用一个输出目录，Localization 也共用一个输出目录，两类模块的输出目录必须分开。生成命名空间留空时，配置行和 Codec 生成到全局命名空间。独立验收场景位于 `Assets/UnityRFramework/Tests/Runtime/ConfigPipelineAcceptance`，固定源数据位于 `Assets/UnityRFramework/Tests/Fixtures/ConfigPipeline`；测试只使用 `Acceptance_*` 数据，不依赖 Samples/Sample.Demo。Demo 的 `Demo_*` 源文件、生成代码和运行时产物分别位于 `Samples/Sample.Demo/ConfigSource`、`Samples/Sample.Demo/Generated`、`Samples/Sample.Demo/GameAssets/Resources`。可通过 `UnityRFramework/Tests` 下的菜单导出测试数据、重建场景、运行 Play Mode 验收或构建包含 Test Assemblies 的专用 Player。

同一业务集合需要拆成多个源文件时，使用 `逻辑表名@分片名.csv`，例如
`Warrior@1000_1999.csv` 与 `Warrior@2000_2999.csv`。两者只生成一个 `WarriorConfig`，
运行时合并后仍通过 `GetConfig<WarriorConfig>(id)` 查询；跨分片重复 Id 或 Schema 不一致会使
整个导出/加载失败。`ConfigCount` 按行类型计数。若两张表只是结构相同但业务语义独立，
应使用不同逻辑表名和不同生成类型，而不是同类型分片。

重复调用 `LoadConfig<T>` 或 `LoadConfigFromString<T>` 会按行类型替换旧表，不会追加数据。
`LoadConfigBundleAsync` 只合并当前 Bundle 内部的同类型分片，并整体替换该类型的已有缓存；
它不会与此前单独加载的表增量合并。Bundle 解析或提交失败时仍保留原缓存。

“共用一个输出目录”指共用一个可选择的根目录；工具会自动生成 `Json/` 和
`Binary/` 子目录，避免 `Resources.Load` 无法区分同名 `.json`/`.bytes`。

配置表工具提供“分析体积/导出耗时”按钮，只在内存中生成各格式并报告 JSON、URFC、
URFM、URFL、URLM 大小、Deflate 估算和耗时，不写入资源。默认实现只保证无明显重复复制
和查询分配，不内置压缩、字符串池、变长整数或差量协议；需要极限性能时应在 Expansion
中接入项目专用 Helper。

Config 复杂字段第一批支持内联枚举、基础类型一维数组和 `List<T>`。类型示例为
`enum<Idle=0|Run=1>`、`int[]`、`List<string>`；集合值使用 `|` 分隔，`\|` 表示
普通竖线。字符串支持 `\n`、`\r`、`\t`、`\\`，CSV 引号字段中的真实换行也会保留。
Config JSON 使用框架内置的受限解析器按公开字段类型精确转换，无第三方依赖，
并避免 `JsonUtility` 对 `decimal` 和 `char` 的静默丢值。
Config JSON 根结构为 `Tables -> 分片名 -> { TableId, SchemaHash, Rows }`；手动 CSV
流程以文件名作为分片名，`@` 前部分作为逻辑表名。Expansion Excel 工具在单 Sheet
工作簿中使用文件名，在多 Sheet 工作簿中使用 Sheet 名。
Config JSON 使用当前 `Tables -> 分片名 -> { TableId, SchemaHash, Rows }` 结构；历史结构需要先转换，或通过显式注册的迁移器升级。

ConfigPipeline 支持项目注册自定义标量字段 Codec。实现 `IConfigFieldCodec` 后，需要提供
唯一类型关键字、公开的运行时类型、与其对应的完整 C# 类型名、大于 0 的 `SchemaVersion`、CSV 解析、
JSON 字符串转换和 URFC 二进制读写，并在 Editor 导出前及 Player 加载前通过
`ConfigFieldCodecRegistry.Register()` 注册。`SchemaVersion` 参与表 `SchemaHash`，线格式
变化时必须递增并重新导出。当前自定义字段只支持标量，JSON 表示固定为字符串，不支持
自定义类型集合或对象型 JSON。

Editor 代码生成可以实现 `IConfigCodeGenerator` 并通过
`ConfigCodeGeneratorRegistry.Set()` 替换，`Reset()` 恢复框架默认生成器。自定义生成器仍须
遵守当前 URFC v2、`IBinaryConfigCodec` 和自动注册契约；需要改变整个文件格式时应实现
自定义 `ConfigHelperBase`。

### Fsm

```csharp
// 创建状态机（首参为拥有者对象，非名称；返回 IFsm 实例）
var fsm = GameEntry.Fsm.CreateFsm(owner,
    new IdleState(), new PatrolState(), new CombatState());

// 切换状态
fsm.ChangeState<CombatState>();

// 销毁（传入 IFsm 实例）
GameEntry.Fsm.DestroyFsm(fsm);
```

Fsm 模块没有 Helper，是同步、通用的纯状态机实现。

### Procedure

```csharp
// Procedure 的每个状态是一个 ProcedureStateBase 子类
public class LoginProcedure : ProcedureStateBase
{
    private Task connectTask;

    public override void OnEnter()
    {
        string serverIP = GameEntry.Procedure.Blackboard.Get<string>("ServerIP");
        connectTask = GameEntry.Network.DefaultChannel.ConnectAsync(serverIP, 9000);
    }

    public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        if (connectTask == null || !connectTask.IsCompleted)
        {
            return;
        }

        if (connectTask.IsCompletedSuccessfully &&
            ReferenceEquals(GameEntry.Procedure.CurrentProcedure, this))
        {
            GameEntry.Procedure.ChangeProcedure<HallProcedure>();
        }
    }
}

// 启动流程
// 自动发现 LoginProcedure 所在业务程序集中的全部 Procedure 状态
GameEntry.Procedure.InitializeFromAssembly<LoginProcedure>();
GameEntry.Procedure.StartProcedure<LoginProcedure>();
```

自动发现仅扫描显式指定的业务程序集；所有状态必须提供公共无参构造函数。需要构造参数、工厂创建或希望完全避免反射时，仍可使用 `Initialize(new LoginProcedure(), new HallProcedure())` 手动注入。自定义 asmdef 以 IL2CPP 发布时，应在 `link.xml` 中保留对应业务程序集。

Procedure 模块没有 Helper，内部复用同步生命周期约定；异步 I/O 应由状态启动并在
`OnUpdate` 检查完成，不能把异步生命周期重新扩散到通用 FSM。

### Entity

```csharp
// 实体组容量是整个组的缓存总上限；0 表示不限制。
GameEntry.Entity.CreateEntityGroup("DefaultGroup", 30f, 32, 120f);

// 加载并显示实体（需指定实体组名称）
long playerId = 1001;
var player = await GameEntry.Entity.ShowEntityAsync(playerId, "Prefabs/Player.prefab", "DefaultGroup");

// 先加载子实体，再按实体编号挂载（父子附加，非按资源路径）
long weaponId = 2001;
await GameEntry.Entity.ShowEntityAsync(weaponId, "Prefabs/Sword.prefab", "DefaultGroup");
GameEntry.Entity.AttachEntity(weaponId, playerId);

// 隐藏（进入组内实例缓存等待复用或释放）
GameEntry.Entity.HideEntity(playerId);

// 场景中预先放置的实体可挂 SceneEntityBinder，或通过代码登记。
// 它参与实体组、查询、更新和父子附加，但不会进入实例缓存或被模块销毁。
IEntity sceneNpc = GameEntry.Entity.RegisterSceneEntity(
    sceneNpcObject, 10001, "SceneNpc", "Scene", createGroupIfMissing: true);
GameEntry.Entity.UnregisterSceneEntity(10001);
```

`DefaultEntityHelper` 只负责对 Resource 返回的 Prefab 执行 `Instantiate/Destroy`，
不加载资源、不解释路径；地址规则完全取决于当前 Resource Helper。
Entity 使用自身的按资源地址缓存，不依赖通用 Pool 模块。`AutoReleaseInterval` 控制扫描间隔，
`ExpireTime` 控制闲置过期时间，`Capacity` 是整个组的缓存总上限。场景实体由外部持有，
注销和框架关闭时只结束生命周期，不销毁其 GameObject。

### Scene

```csharp
// 异步加载
await GameEntry.Scene.LoadSceneAsync("Assets/Scenes/Battle.unity");

// 卸载
await GameEntry.Scene.UnloadSceneAsync("Assets/Scenes/Battle.unity");

// 判断是否已加载
if (GameEntry.Scene.IsLoaded("Assets/Scenes/Battle.unity")) { ... }
```

Scene 模块没有独立 Helper，只管理加载状态、防并发和事件，然后把实际操作委托给
Resource Helper。`DefaultResourceHelper` 和 `LocalFileResourceHelper` 只加载 Build Settings
场景；第三方实现的来源和地址规则见对应 Expansion 文档。加载与卸载必须传同一个
location，`activateOnLoad:false` 当前不受支持。

### UI

```csharp
// 打开窗口（windowLayer 数值越大越靠前；fullScreen 覆盖时自动隐藏下层 UI）
var ui = await GameEntry.UI.OpenUIFormAsync("UI/Dialog.prefab", windowLayer: 10, fullScreen: true);

// 关闭（按资源路径）
GameEntry.UI.CloseUIForm("UI/Dialog.prefab");

// 场景中预先放置的 UI 可挂 SceneUIFormBinder，或通过代码登记。
// 它参与窗口栈、统一更新和全屏暂停，但对象仍由场景持有。
IUIForm battleHud = GameEntry.UI.RegisterSceneUIForm(
    battleHudObject, "BattleHUD", UILayer.HUD);
GameEntry.UI.UnregisterSceneUIForm("BattleHUD");
```

`DefaultUIHelper` 只负责实例化和销毁 Resource 返回的 UI Prefab，不负责加载和地址转换。
因此默认 Resources 模式使用相对 Resources 的路径；切换 YooAsset 后使用对应 Address。
场景内 UI 通过 `SceneUIFormBinder` 登记，所有权仍属于场景，不经过 Helper 实例化或销毁。
同一资源地址不能重复打开或重复加载；加载期间调用关闭会取消本次打开。相同层级按打开顺序
排列，后打开的窗口位于更上层。框架加载的窗口在关闭、取消或创建失败时会销毁实例并归还
资源引用；当前 UI 模块不使用对象池。

`UnityRFramework.prefab` 的 `UI` 节点已提供默认 `Screen Space - Overlay` Canvas，以及
Bottom、HUD、Panel、Popup、System、Top 六个普通层级。普通 UI Prefab 不需要自带 Canvas，
会按 `WindowLayer` 挂到对应容器并随框架跨场景保留。

根节点自带 Canvas 的 UI 会挂到 `Canvas Root` 下对应的 `Canvas Layer Roots`。框架只管理其
父节点、生命周期和逻辑窗口栈，不修改该 Prefab 的 Render Mode、Camera、CanvasScaler、
Sorting Layer、Order in Layer 或 GraphicRaycaster；独立 Canvas 之间的实际渲染顺序由项目
自行配置。场景 UI 如需跨场景保留，应放在框架的普通或独立 Canvas 层级下再登记。
UGUI 交互还需要场景中存在一个有效的 `EventSystem` 和与项目输入方案匹配的 Input Module；
框架不自动选择旧输入系统或新输入系统的实现，项目中应避免同时存在多个 EventSystem。

### Audio

```csharp
// BGM
GameEntry.Audio.PlayBgm("Audio/bgm_main.mp3");
GameEntry.Audio.PauseBgm();
GameEntry.Audio.StopBgm();

// 音效
GameEntry.Audio.PlaySfx("Audio/sfx_click.mp3");

// UI 音效
GameEntry.Audio.PlayUI("Audio/ui_confirm.mp3");

// 音量控制
GameEntry.Audio.BgmVolume = 0.8f;
GameEntry.Audio.SfxVolume = 1f;

// AudioSource 池自动管理，无需手动创建/销毁
```

核心只提供 `DefaultAudioHelper`：一个 BGM AudioSource、一个 UI AudioSource 和最多 16 个
并发 SFX AudioSource，负责播放、淡入淡出和完成回调。AudioClip 始终由 ResourceModule
加载并归还，因此路径规则仍由 Resource Helper 决定。`PlayBgm/PlaySfx` 使用同步资源入口；
LocalFile 在 Android/WebGL 或文件音频场景应使用 `PlayBgmAsync/PlaySfxAsync`。

### Network

> **使用限制**：内置 `TcpNetworkHelper`、`UdpNetworkHelper` 和
> `WebSocketNetworkHelper` 是基础参考实现，尚未经过严格生产验证。它们不能替代
> 项目针对自有协议、加密认证、弱网、移动端后台恢复、代理/TLS、攻击流量和高并发
> 场景的实现与测试。正式项目应扩展 `INetworkHelper`，并在目标平台完成专项验收。

```csharp
// 单服务器
var channel = GameEntry.Network.DefaultChannel;
channel.RegisterHandler(1001, OnMessage);
await channel.ConnectAsync("127.0.0.1", 9000);
channel.Send(1001, data);

// 多服务器
var login = GameEntry.Network.CreateChannel("Login");
login.RegisterHandler(1001, OnLoginResponse);
await login.ConnectAsync("127.0.0.1", 9001);

var chat = GameEntry.Network.CreateChannel("Chat");
await chat.ConnectAsync("127.0.0.1", 9002);
chat.Send(2001, msgBytes);

// 心跳和重连
login.HeartbeatInterval = 10f;
login.AutoReconnect = true;
login.ReconnectInterval = 3f;

// 事件（带通道名）
GameEntry.Event.Subscribe<NetworkConnectedEvent>(e =>
    Log.Info("通道 [{0}] 已连接", e.ChannelName));

// 基础 Helper 可在 Inspector 选择 Tcp / Udp / WebSocket
// 正式项目建议注入经过专项测试的自定义 INetworkHelper
```

| 内置 Helper | 传输与帧格式 | 适用范围与注意事项 |
|---|---|---|
| `DefaultNetworkHelper` | 空实现 | 仅用于明确禁用网络；连接和发送不会产生真实网络流量。 |
| `TcpNetworkHelper` | `TcpClient`；`总长度(4) + 消息ID(4) + 消息体`，小端序 | 可靠字节流，处理了粘包/拆包；服务端必须使用同一长度语义。 |
| `UdpNetworkHelper` | `UdpClient`；每个数据报为 `消息ID(4) + 消息体` | 无连接、不保证到达或顺序，适合允许丢包的数据。 |
| `WebSocketNetworkHelper` | `ClientWebSocket`；每条消息为 `消息ID(4) + 消息体` | 适合 Web 服务端；URI、TLS、代理和平台兼容性必须专项验证。 |

`NetworkComponent` 默认选择 `TcpNetworkHelper`；`DefaultNetworkHelper` 虽保留“Default”
名称，但它是无网络占位实现，不是可通信的默认协议。

### Localization

```csharp
// JsonLocalizationHelper 默认使用 Localization/Json/{language}.json
// BinaryLocalizationHelper 默认使用 Localization/Binary/{language}.bytes
// Inspector 未指定语言时使用 zh-CN，并可在 Start 时异步加载

// 查询文本
var text = GameEntry.Localization.GetString("ui_login_button");

// 带占位符
var welcome = GameEntry.Localization.GetString("ui_welcome", playerName);

// 切换语言
await GameEntry.Localization.SwitchLanguageAsync("en-US");

// 非标准目录、YooAsset 地址或自定义 location 显式传入，不由组件拼接
await GameEntry.Localization.SwitchLanguageAsync(
    "en-US", "Localization/English");

// 二进制语言：Inspector 选择 BinaryLocalizationHelper 即使用内置二进制位置约定

// 自定义 Helper 实现 ILocalizationLocationProvider 后也支持自动推导 location；
// 未实现时关闭自动加载，并使用上面的显式 location 重载

// 多语言容器只负责批量预载，不自动切换当前语言
await GameEntry.Localization.LoadLanguageBundleAsync(
    "Localization/Json/LocalizationBundle.json");
// BinaryLocalizationHelper 对应加载 Localization/Binary/LocalizationBundle.bytes
```

| 内置 Helper | 单语言格式 | Bundle 格式 | 自动 location | 注意事项 |
|---|---|---|---|---|
| `JsonLocalizationHelper` | UTF-8 JSON Key/Value | JSON 多语言容器 | `Localization/Json/{language}.json` | 默认选择，文件可读性好。 |
| `BinaryLocalizationHelper` | URFL v2，兼容 v1 | URLM v1 | `Localization/Binary/{language}.bytes` | 需要由配置表工具生成；URFL v2 包含 CRC32。 |
| 自定义 Helper | 项目自定 | 实现 `ILocalizationBundleHelper` 后可支持 | 实现 `ILocalizationLocationProvider` 后可自动推导 | 未实现位置提供器时必须显式传 location，并关闭组件的启动自动加载。 |

Localization Helper 只负责解析和默认地址推导，实际文件仍由 Resource Helper 加载。
因此同一个自动 location 在 Resources、LocalFile 和 YooAsset 下分别表示 Resources 相对路径、
本地相对文件路径和 YooAsset Address；切换 Resource Helper 时必须确保产物放置或收集正确。

## 当前技术栈

只列出仓库中已经实现并可配置使用的技术。

| 领域 | 核心默认实现 | 已接入的可选扩展 |
|------|-------------|-----------------|
| 资源管理 | `Resources.Load`、Persistent/Streaming 本地文件 | YooAsset 3.0.5 |
| 配置表 | JSON、URFC/URFM 二进制 | ExcelDataReader Editor 导表 |
| 本地化 | JSON、URFL/URLM 二进制 | ExcelDataReader Editor 导表 |
| Web 请求 | UnityWebRequest + Task | UniTask WebRequest Helper |
| 网络传输 | TCP、UDP、WebSocket 基础 Helper | 项目自定义 `INetworkHelper` |
| 异步 | `System.Threading.Tasks.Task`、Unity Coroutine | UniTask 仅用于 Expansion Helper |
| JSON | JsonUtility、Unity Newtonsoft Json | 项目自定义 `IJsonHelper` |
| UI | UGUI | 无 |

## 参考项目

- [GameFramework](https://github.com/EllanJiang/GameFramework) — 架构蓝本
- [UniFramework](https://github.com/gmhevinci/UniFramework) — 轻量工具集参考
- [TEngine](https://github.com/Alex-Rachel/TEngine) — 资源与模块组织参考

## 许可证与第三方声明

UnityRFramework 原创代码采用 [Apache License 2.0](./LICENSE)。随包 DLL、字体、Demo
素材、UPM 依赖及可选 Expansion 集成的来源和许可证见
[THIRD-PARTY-NOTICES.md](./THIRD-PARTY-NOTICES.md)。各第三方内容继续适用其原许可证。
