# UnityRFramework

轻量级 Unity 游戏框架。**Library 层纯 C#（零 UnityEngine 依赖）+ Runtime 层 Helper 桥接**，面向 HybridCLR 热更 + YooAsset 资源管理的现代技术栈。

## 架构

```
Library/RFramework/RFramework/  ← 纯 C# 核心（.NET Standard 2.0，零 Unity/第三方依赖）
Scripts/Runtime/                ← Unity 运行时（Component + Helper 默认实现）
Scripts/Editor/                 ← 编辑器工具（Inspector、菜单项）
Samples~/Expansion/             ← 第三方集成（YooAsset/Luban/HybridCLR，按需 Import）
Samples~/Demo/                  ← 官方示例（仅用内置 Helper，串通全部模块）
```

> `Samples~` 以 `~` 结尾，Unity 不自动编译；经 Package Manager 的 **Import Sample** 才会进入项目编译。

三层命名空间固定为：Library 使用 `RFramework`，Runtime 使用 `UnityRFramework.Runtime`，
Editor 使用 `UnityRFramework.Editor`。模块子文件夹只负责组织文件，不继续扩展命名空间。

所有共享数据通过 Helper 桥接模式解耦：Library 定义 `IXxxHelper` 纯 C# 接口 → Runtime 提供默认实现 → Expansion 提供第三方实现。

## 当前实现约束

- `Fsm` 与 `Procedure` 的生命周期均为同步 `void` 回调。需要网络、资源或场景 I/O 时，在状态内启动 `Task`，在 `OnUpdate` 中确认任务完成且当前状态仍是自身后，再调用同步切换 API。
- FSM 生命周期回调抛出异常后，该 FSM 会停止更新并拒绝新的切换；框架不会猜测如何回滚业务副作用。
- 每一次成功的 `Resource.LoadAssetAsync` / `LoadAssetSync` 都必须对应一次 `UnloadAsset`。若同一对象可能由多个路径或类型加载，使用 `UnloadAsset<T>(location)` 精确归还；旧对象参数重载遇到歧义会抛异常。框架打开的 UI 和显示的 Entity 已自行归还其资源引用，业务层直接加载的资源仍由业务层归还。
- `LoadAssetSync` 不会与相同资源的在途异步加载并行执行；此时应改为等待 `LoadAssetAsync`。`Scene.LoadSceneAsync` 的取消令牌只在操作开始前有效，底层场景加载一旦开始不承诺中途取消或回滚；`Single` 成功后只保留新场景账本。
- `Event.FireAsync` 可跨线程入队、主线程分发；一个事件处理器异常不会丢弃同帧其余排队事件。框架内部生命周期通知使用 `FireSafely`，订阅者异常包装为 `RFrameworkException` 后经 `IEventModule.OnError` 交给 Runtime 记录，不会回滚已经成功的模块操作。
- 网络 Helper 的回调会切回 `NetworkChannel.Update` 所在主线程处理。TCP 默认 Helper 的建连不会同步阻塞 Unity 主线程，WebSocket 不使用公开 `async void` 或同步等待关闭；主动 `Disconnect` 会发布一次断开事件。接收队列与单帧分发均有上限，过载时丢弃后续数据包而非无限占用内存。
- `LoadSceneAsync` 的内置 Resource Helper 当前只支持 `activateOnLoad: true`；延迟激活没有配套激活句柄，因此会显式抛出不支持异常而不会永久等待。
- AudioClip 统一由 `ResourceModule` 加载和精确归还，Audio Helper 只负责 AudioSource 播放、淡入淡出和协程回调。
- 模块优先级同时约束更新和关闭：共同依赖 `ResourceModule` 优先更新、最后关闭；网络优先关闭，以便断连清理期间 Event/Timer 仍可用。
- WebRequest 的并发队列按优先级调度，同优先级 FIFO；只有拿到并发槽位的请求计入 Active。
- YooAsset 扩展会把 Library 的 `object` 资源类型桥接为 `UnityEngine.Object`，以兼容 YooAsset 的资源类型校验。
- Library 层不直接输出日志，错误以 `RFrameworkException` 上报；Runtime 层统一使用 `Log`，Editor 工具可使用 Unity Editor Console。

## 安装

支持通过 Package Manager 的 **git URL** 导入：

1. `Window → Package Manager → + → Add package from git URL`
2. 填入仓库地址（如 `https://github.com/relly-sc/UnityRFramework.git`）
3. 等待编译完成。

**Samples（可选）**：在 Package Manager 中选中本包 → **Samples** → 点击 `Expansion` 或 `Demo` 的 **Import**。

- `Expansion`：第三方集成（YooAsset 资源、UniTask Web 请求等）。UPM 不为 Sample 解析依赖，需手动安装其引用的包（YooAsset / UniTask / Luban / HybridCLR，按所用 Helper 而定）。详见 `Samples~/Expansion/README.md`。
- `Demo`：官方可运行示例，**仅依赖内置 Helper、零第三方**；含演示用美术/音频资源，开发者可不导入。

> 核心包 `dependencies` 为空：框架本身不强制任何第三方库，按需引入即可。

## 模块

| 模块 | 职责 | 入口 |
|------|------|------|
| **Base** | 框架基础设施：Helpers 注入、Update 驱动、帧率/游戏速度控制 | `GameEntry.Base` |
| **Log** | 控制台 + 文件双输出，分卷归档，级别过滤 | `Log.Info/Warning/Error` |
| **Event** | 解耦消息通信，类型路由，`Fire<T>`（零 GC）+ `FireSafely<T>`（生命周期通知）+ `FireAsync<T>`（线程安全） | `GameEntry.Event` |
| **Pool** | GameObject 池 + class 池，委托注入，预热 | `GameEntry.Pool` |
| **Timer** | delay/interval/duration/maxTriggerCount 四参数计时器 | `GameEntry.Timer` |
| **Resource** | 资源异步加载，引用计数，并发去重 | `GameEntry.Resource` |
| **WebRequest** | HTTP GET/POST/PUT/DELETE，并发控制，超时+重试，multipart 上传+进度 | `GameEntry.WebRequest` |
| **Config** | 配置表管理与查询，默认 JSON + 内置 URFC 二进制，可扩展自定义格式 | `GameEntry.Config` |
| **Fsm** | 同步通用有限状态机，泛型 Owner，生命周期异常后停止运行 | `GameEntry.Fsm` |
| **Procedure** | 同步游戏流程 FSM，Blackboard 跨状态共享数据 | `GameEntry.Procedure` |
| **Entity** | 游戏实体生命周期，实体组+对象池，父子附加 | `GameEntry.Entity` |
| **Scene** | 场景异步加载/卸载，状态追踪，防并发 | `GameEntry.Scene` |
| **UI** | UI 窗口栈管理，层级排序，FullScreen 自动隐藏 | `GameEntry.UI` |
| **Audio** | BGM/SFX/UI 三组，AudioSource 池，淡入淡出 | `GameEntry.Audio` |
| **Network** | 多通道管理，TCP/UDP/WebSocket 三协议，心跳/重连 | `GameEntry.Network` |
| **Localization** | 多语言管理，占位符格式化，Inspector 配置默认语言 | `GameEntry.Localization` |

## 快速开始

将 `Assets/UnityRFramework/Prefabs/UnityRFramework.prefab` 拖入启动场景。预制体包含所有模块 Component，Inspector 中可切换 Helper 实现。访问模块统一走 `GameEntry`：

```csharp
GameEntry.Pool.CreateGameObjectPool("Bullet", bulletPrefab, parent: bulletRoot);
GameEntry.Base.FrameRate = 60;
GameEntry.Localization.GetString("ui_login_button");
GameEntry.Network.CreateChannel("Chat").ConnectAsync("127.0.0.1", 9000);
```

## 使用说明

### Base

```csharp
// 帧率与游戏速度
GameEntry.Base.FrameRate = 60;
GameEntry.Base.GameSpeed = 1.5f;

// 暂停/恢复
GameEntry.Base.PauseGame();
GameEntry.Base.ResumeGame();

// 后台运行与休眠
GameEntry.Base.RunInBackground = true;
GameEntry.Base.NeverSleep = true;
```

### Log

```csharp
Log.Info("玩家 {0} 登录，等级 {1}", playerName, level);
Log.Warning("资源 {0} 加载超时", assetPath);
Log.Error("连接服务器失败：{0}", errorMessage);
```

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

### Resource

```csharp
// 异步加载
var prefab = await GameEntry.Resource.LoadAssetAsync<GameObject>("Assets/Prefabs/Player.prefab");

// 场景异步加载
await GameEntry.Resource.LoadSceneAsync("Assets/Scenes/Battle.unity", 1); // sceneMode: 1=Additive 叠加

// 卸载
GameEntry.Resource.UnloadAsset(prefab);
await GameEntry.Resource.UnloadSceneAsync("Assets/Scenes/Battle.unity");
GameEntry.Resource.UnloadUnusedAssets();

// Helper 切换：Inspector 选 DefaultResourceHelper（Resources.Load）或 YooAssetResourceHelper
```

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

### Config

```csharp
// 默认 Helper 从 UTF-8 JSON TextAsset 加载；文件位于 Assets/Resources/Config/Json/items.json
await GameEntry.Config.LoadConfigAsync<ItemConfig>("Config/Json/items.json");

// 二进制：先用 UnityRFramework/配置表工具生成 URFC v2 和静态 Codec，
// 再在 Inspector 选择 BinaryConfigHelper
await GameEntry.Config.LoadConfigAsync<ItemConfig>("Config/Binary/items.bytes");

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

// 自定义模式：继承 ConfigHelperBase，适配 Luban 或项目私有二进制格式
```

`ParseConfig(Type, byte[])` 的字节格式由当前 `IConfigHelper` 决定。框架默认 JSON；`BinaryConfigHelper` 兼容反射映射的 URFC v1，并使用生成 Codec 读取带 TableId、SchemaHash 和 CRC32 的 URFC v2。项目私有格式可直接继承 `ConfigHelperBase`。

JSON 与 URFC v2 均支持显式历史 Schema 迁移。二进制实现 `IBinaryConfigMigration` 并注册到
`BinaryConfigMigrationRegistry`；JSON 实现 `IJsonConfigMigration` 并注册到
`JsonConfigMigrationRegistry`。生成代码会通过 `ConfigSchemaRegistry` 注册两种格式共用的
当前 `TableId/SchemaHash`。迁移目标必须等于当前 Schema，未知、未来或目标过期的 Schema
均拒绝。JSON 新格式为 `Tables -> 表名 -> { TableId, SchemaHash, Rows }`；旧的
`Tables -> 数组`、`Items` 和顶层数组仍可读取，但无 SchemaHash，不能参与显式迁移。

框架没有独立 DataModule，配置数据统一由 ConfigModule 管理。零第三方 Editor 转换工具位于菜单 `UnityRFramework/配置表工具`：Config 与 Localization CSV 均使用“字段名、类型、注释”三行表头，第四行开始为数据。Config 必须包含唯一 `int Id`；Localization 固定为 `Key,Value`、`string,string`，并以唯一 `string Key` 为主键。工具同时生成 JSON、配置行、静态 Codec、URFC v2 和带 CRC32 的 URFL v2，并仅在内容变化时写入。默认流程由 Excel 手动导出 UTF-8 CSV，再由工具生成 JSON/`.bytes`；直接读取 XLSX 的方案放在第三方 Expansion。Config 的 JSON/`.bytes` 共用一个输出目录，Localization 也共用一个输出目录，两类模块的输出目录必须分开。生成命名空间留空时，配置行和 Codec 生成到全局命名空间。Runtime 仍兼容读取无 CRC 的 URFL v1。独立验收场景位于 `Assets/UnityRFramework/Tests/Runtime/ConfigPipelineAcceptance`，固定源数据位于 `Assets/UnityRFramework/Tests/Fixtures/ConfigPipeline`；测试只使用 `Acceptance_*` 数据，不依赖 Samples/Demo。Demo 的 `Demo_*` 源文件、生成代码和运行时产物分别位于 `Samples/Demo/ConfigSource`、`Samples/Demo/Generated`、`Samples/Demo/GameAssets/Resources`。可通过 `UnityRFramework/Tests` 下的菜单导出测试数据、重建场景、运行 Play Mode 验收或构建包含 Test Assemblies 的专用 Player。

“共用一个输出目录”指共用一个可选择的根目录；工具会自动生成 `Json/` 和
`Binary/` 子目录，避免 `Resources.Load` 无法区分同名 `.json`/`.bytes`。

Config 复杂字段第一批支持内联枚举、基础类型一维数组和 `List<T>`。类型示例为
`enum<Idle=0|Run=1>`、`int[]`、`List<string>`；集合值使用 `|` 分隔，`\|` 表示
普通竖线。字符串支持 `\n`、`\r`、`\t`、`\\`，CSV 引号字段中的真实换行也会保留。
Config JSON 使用框架内置的受限解析器按公开字段类型精确转换，无第三方依赖，
并避免 `JsonUtility` 对 `decimal` 和 `char` 的静默丢值。
Config JSON 根结构为 `Tables -> 表名 -> 行数组`；手动 CSV 流程以文件名作为表名，
未来 Excel 扩展直接使用 Sheet 名。Runtime 仍兼容旧 `Items` 包装和顶层数组。

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

### Procedure

```csharp
// Procedure 的每个状态是一个 ProcedureStateBase 子类
public class LoginProcedure : ProcedureStateBase
{
    private Task connectTask;

    public override void OnEnter()
    {
        string serverIP = GameEntry.Procedure.Blackboard.Get<string>("ServerIP");
        connectTask = GameEntry.Network.ConnectAsync(serverIP, 9000);
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
GameEntry.Procedure.Initialize(new LoginProcedure(), new HallProcedure());
GameEntry.Procedure.StartProcedure<LoginProcedure>();
```

### Entity

```csharp
// 加载并显示实体（需指定实体组名称）
long playerId = 1001;
var player = await GameEntry.Entity.ShowEntityAsync(playerId, "Assets/Prefabs/Player.prefab", "DefaultGroup");

// 先加载子实体，再按实体编号挂载（父子附加，非按资源路径）
long weaponId = 2001;
await GameEntry.Entity.ShowEntityAsync(weaponId, "Assets/Prefabs/Sword.prefab", "DefaultGroup");
GameEntry.Entity.AttachEntity(weaponId, playerId);

// 隐藏（进入对象池等待复用或销毁）
GameEntry.Entity.HideEntity(playerId);
```

### Scene

```csharp
// 异步加载
var scene = await GameEntry.Scene.LoadSceneAsync("Assets/Scenes/Battle.unity");

// 卸载
await GameEntry.Scene.UnloadSceneAsync("Assets/Scenes/Battle.unity");

// 判断是否已加载
if (GameEntry.Scene.IsLoaded("Assets/Scenes/Battle.unity")) { ... }
```

### UI

```csharp
// 打开窗口（windowLayer 数值越大越靠前；fullScreen 覆盖时自动隐藏下层 UI）
var ui = await GameEntry.UI.OpenUIFormAsync("Assets/UI/Dialog.prefab", windowLayer: 10, fullScreen: true);

// 关闭（按资源路径）
GameEntry.UI.CloseUIForm("Assets/UI/Dialog.prefab");
```

### Audio

```csharp
// BGM
GameEntry.Audio.PlayBgm("Assets/Audio/bgm_main.mp3");
GameEntry.Audio.PauseBgm();
GameEntry.Audio.StopBgm();

// 音效
GameEntry.Audio.PlaySfx("Assets/Audio/sfx_click.mp3");

// UI 音效
GameEntry.Audio.PlayUI("Assets/Audio/ui_confirm.mp3");

// 音量控制
GameEntry.Audio.BgmVolume = 0.8f;
GameEntry.Audio.SfxVolume = 1f;

// AudioSource 池自动管理，无需手动创建/销毁
```

### Network

```csharp
// 单服务器
await GameEntry.Network.ConnectAsync("127.0.0.1", 9000);
GameEntry.Network.RegisterHandler(1001, OnMessage);
GameEntry.Network.Send(1001, data);

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

// Helper 切换：Inspector 可选 Tcp / Udp / WebSocket
```

### Localization

```csharp
// 默认从 Resources/Localization/Json/{language}.json 加载 Key/Value 项
// Inspector 未指定语言时使用 zh-CN，并在 Start 时异步加载

// 查询文本
var text = GameEntry.Localization.GetString("ui_login_button");

// 带占位符
var welcome = GameEntry.Localization.GetString("ui_welcome", playerName);

// 切换语言
await GameEntry.Localization.SwitchLanguageAsync("en-US");

// 二进制语言包：Inspector 选择 BinaryLocalizationHelper，并将扩展名设为 .bytes
```

## 技术栈

Runtime 层默认实现，通过 Helper 桥接可自由替换。Library 层不依赖任何第三方库。

| 领域 | 默认实现 | 可替换方案 |
|------|---------|-----------|
| 资源管理 | `DefaultResourceHelper`（Resources.Load） | YooAsset、Addressables |
| 配置表 | `JsonConfigHelper`（JSON）/ `BinaryConfigHelper`（URFC v1/v2） | Luban、自定义二进制/文本格式 |
| 本地化 | `JsonLocalizationHelper`（JSON）/ `BinaryLocalizationHelper`（URFL v2，兼容 v1） | 自定义二进制/文本格式 |
| 网络传输 | `TcpNetworkHelper` | Udp / WebSocket / KCP（Expansion） |
| 代码热更 | HybridCLR | ILRuntime |
| 异步 | Task（Library / Runtime 统一） | Awaitable、Coroutine |
| 序列化 | MemoryPack | Protobuf |
| UI | UGUI | UI Toolkit、FairyGUI |

## 参考项目

- [GameFramework](https://github.com/EllanJiang/GameFramework) — 架构蓝本
- [UniFramework](https://github.com/gmhevinci/UniFramework) — 轻量工具集参考
- [TEngine](https://github.com/Alex-Rachel/TEngine) — YooAsset + HybridCLR 集成参考
