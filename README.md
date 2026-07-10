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

所有共享数据通过 Helper 桥接模式解耦：Library 定义 `IXxxHelper` 纯 C# 接口 → Runtime 提供默认实现 → Expansion 提供第三方实现。

## 安装

支持通过 Package Manager 的 **git URL** 导入：

1. `Window → Package Manager → + → Add package from git URL`
2. 填入仓库地址（如 `https://github.com/relly-sc/UnityRFramework.git?path=/Assets/UnityRFramework`，`path` 指向包根目录）
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
| **Event** | 解耦消息通信，类型路由，`Fire<T>`（零 GC）+ `FireAsync<T>`（线程安全） | `GameEntry.Event` |
| **Pool** | GameObject 池 + class 池，委托注入，预热 | `GameEntry.Pool` |
| **Timer** | delay/interval/duration/maxTriggerCount 四参数计时器 | `GameEntry.Timer` |
| **Resource** | 资源异步加载，引用计数，并发去重 | `GameEntry.Resource` |
| **WebRequest** | HTTP GET/POST/PUT/DELETE，并发控制，超时+重试，multipart 上传+进度 | `GameEntry.WebRequest` |
| **Config** | 配置表管理与查询，JSON + Luban 双模式 | `GameEntry.Config` |
| **Fsm** | 通用有限状态机，泛型 Owner，双缓冲安全 | `GameEntry.Fsm` |
| **Procedure** | 游戏流程 FSM，Blackboard 跨状态共享数据 | `GameEntry.Procedure` |
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
// JSON 模式（直接解析字符串）
string json = "[{\"Id\":1,\"Name\":\"Sword\"},{\"Id\":2,\"Name\":\"Shield\"}]";
GameEntry.Config.LoadConfigFromString<ItemConfig>(json);

// 按 ID 查询
var item = GameEntry.Config.GetConfig<ItemConfig>(1);

// 遍历全部
var all = GameEntry.Config.GetAllConfigs<ItemConfig>();
foreach (var i in all) { Debug.Log($"{i.Id}: {i.Name}"); }

// 安全检查
if (GameEntry.Config.HasConfigRow<ItemConfig>(1001)) { ... }

// Luban 模式：将 Helper 切换为 LubanConfigHelper，通过 LoadConfigAsync 加载 .bytes
```

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
    protected override void OnEnter()
    {
        // 从 Blackboard 读取数据（键值对，跨状态共享）
        var serverIP = GameEntry.Procedure.Blackboard.Get<string>("ServerIP");

        // 连接服务器
        await GameEntry.Network.ConnectAsync(serverIP, 9000);

        // 跳转到下一个流程
        GameEntry.Procedure.ChangeProcedure<HallProcedure>();
    }
}

// 启动流程
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
// Inspector 配置默认语言后自动加载，无需手动初始化

// 查询文本
var text = GameEntry.Localization.GetString("ui_login_button");

// 带占位符
var welcome = GameEntry.Localization.GetString("ui_welcome", playerName);

// 切换语言
await GameEntry.Localization.SwitchLanguageAsync("en-US");
```

## 技术栈

Runtime 层默认实现，通过 Helper 桥接可自由替换。Library 层不依赖任何第三方库。

| 领域 | 默认实现 | 可替换方案 |
|------|---------|-----------|
| 资源管理 | `DefaultResourceHelper`（Resources.Load） | YooAsset、Addressables |
| 配置表 | `DefaultConfigHelper`（JSON） | Luban、CSV |
| 网络传输 | `TcpNetworkHelper` | Udp / WebSocket / KCP（Expansion） |
| 代码热更 | HybridCLR | ILRuntime |
| 异步 | Task（Library / Runtime 统一） | Awaitable、Coroutine |
| 序列化 | MemoryPack | Protobuf |
| UI | UGUI | UI Toolkit、FairyGUI |

## 参考项目

- [GameFramework](https://github.com/EllanJiang/GameFramework) — 架构蓝本
- [UniFramework](https://github.com/gmhevinci/UniFramework) — 轻量工具集参考
- [TEngine](https://github.com/Alex-Rachel/TEngine) — YooAsset + HybridCLR 集成参考
