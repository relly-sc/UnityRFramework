# UnityRFramework

基于 GameFramework 架构理念的轻量级 Unity 游戏框架。**Library 层纯 C#（零 UnityEngine 依赖）+ Runtime 层 Helper 桥接**，面向 HybridCLR 热更 + YooAsset 资源管理的现代技术栈。

## 架构

```
Library/RFramework/RFramework/  ← 纯 C# 核心（.NET Standard 2.0，零 Unity/第三方依赖）
Scripts/Runtime/                ← Unity 运行时（Component + Helper 默认实现）
Scripts/Editor/                 ← 编辑器工具（Inspector、菜单项）
Scripts/Expansion/              ← 第三方集成（YooAsset/Luban/KCP，按需编译）
```

所有共享数据通过 Helper 桥接模式解耦：Library 定义 `IXxxHelper` 纯 C# 接口 → Runtime 提供默认实现 → Expansion 提供第三方实现。

## 模块

| 模块 | 职责 | 入口 | Helper 桥接 |
|------|------|------|:--:|
| **Base** | 框架基础设施：Helpers 注入、Update 驱动、帧率/游戏速度控制 | `GameEntry.Base` | Text / Log / JSON |
| **Log** | 控制台 + 文件双输出，分卷归档，级别过滤 | `Log.Info/Warning/Error` | `ILogHelper` |
| **Event** | 解耦消息通信，类型路由，`Fire<T>`（零 GC）+ `FireAsync<T>`（线程安全） | `GameEntry.Event` | — |
| **Pool** | GameObject 池 + class 池，委托注入，预热 | `GameEntry.Pool` | — |
| **Timer** | delay/interval/duration/maxTriggerCount 四参数模型 | `GameEntry.Timer` | — |
| **Resource** | 资源异步加载，引用计数，并发去重 | `GameEntry.Resource` | `IResourceHelper`（默认 `Resources.Load`，可选 YooAsset） |
| **WebRequest** | HTTP GET/POST/PUT/DELETE，并发控制，超时+重试，multipart 上传+进度 | `GameEntry.WebRequest` | `IWebRequestHelper`（默认 `UnityWebRequest`，可选 `UniTask`） |
| **Config** | 配置表管理与查询，JSON（`DefaultConfigHelper`）+ Luban 双模式 | `GameEntry.Config` | `IConfigHelper` |
| **Fsm** | 通用有限状态机，泛型 Owner，双缓冲安全 | `GameEntry.Fsm` | — |
| **Procedure** | 游戏流程 FSM，Blackboard 跨状态共享数据 | `GameEntry.Procedure` | — |
| **Entity** | 游戏实体生命周期，实体组+对象池，父子附加 | `GameEntry.Entity` | `IEntityHelper` |
| **Scene** | 场景异步加载/卸载，状态追踪，防并发 | `GameEntry.Scene` | — |
| **UI** | UI 窗口栈管理，层级排序，FullScreen 自动隐藏 | `GameEntry.UI` | `IUIHelper` |
| **Audio** | BGM/SFX/UI 三组，AudioSource 池，淡入淡出 | `GameEntry.Audio` | `IAudioHelper` |
| **Network** | 多通道管理，TCP/UDP/WebSocket 三协议，心跳/重连 | `GameEntry.Network` | `INetworkHelper`（默认 `TcpNetworkHelper`） |
| **Localization** | 多语言管理，占位符格式化，Inspector 配置默认语言 | `GameEntry.Localization` | `ILocalizationHelper`（默认 CSV） |

## 快速开始

### 1. 场景配置

将 `Assets/UnityRFramework/Prefabs/UnityRFramework.prefab` 拖入启动场景。预制体包含所有模块 Component，Inspector 中可切换 Helper 实现。

### 2. 访问模块

```csharp
// 内置模块 — 强类型静态属性
GameEntry.Pool.CreateGameObjectPool("Bullet", bulletPrefab, parent: bulletRoot);
GameEntry.Base.FrameRate = 60;
GameEntry.Localization.GetString("ui_login_button");
GameEntry.Network.CreateChannel("Chat").ConnectAsync("127.0.0.1", 9000);

// 自定义模块 — 泛型方法
var shop = GameEntry.Get<ShopComponent>();
```

### 3. 配置 Helper

每个模块 Component 的 Inspector 中可通过下拉框切换 Helper 实现（如 Network 可选 `TcpNetworkHelper` / `UdpNetworkHelper` / `WebSocketNetworkHelper`），或运行时替换：

```csharp
GameEntry.Network.GetChannel("Login").SetHelper(new WebSocketNetworkHelper());
```

## 使用示例

### Pool

```csharp
// class 池（委托回调）
var pool = GameEntry.Pool.CreatePool<MyClass>("MyPool",
    createFunc: () => new MyClass(),
    onSpawn: obj => obj.Reset(),
    capacity: 32);

// GameObject 池（一行创建）
var bulletPool = GameEntry.Pool.CreateGameObjectPool(
    "Bullet", bulletPrefab, parent: bulletRoot, prewarmCount: 20);

var bullet = bulletPool.Spawn();
bulletPool.Unspawn(bullet);
```

### Event

```csharp
// 定义消息（struct = 零 GC）
public struct PlayerDeadEventArgs { public int playerId; }

// 订阅
GameEntry.Event.Subscribe<PlayerDeadEventArgs>(OnPlayerDead);

// 同步发布（零 GC）
GameEntry.Event.Fire(new PlayerDeadEventArgs { playerId = 1 });

// EventGroup 批量管理订阅生命周期
private EventGroup eventGroup;
private void OnEnable() => eventGroup = GameEntry.Event.CreateGroup()
    .Subscribe<PlayerDeadEventArgs>(OnPlayerDead);
private void OnDisable() => eventGroup.Dispose();
```

### Network — 多服务器

```csharp
var login = GameEntry.Network.CreateChannel("Login");
login.RegisterHandler(1001, OnLoginResponse);
await login.ConnectAsync("127.0.0.1", 9001);

var chat = GameEntry.Network.CreateChannel("Chat");
await chat.ConnectAsync("127.0.0.1", 9002);
chat.Send(2001, messageBytes);

// 事件带通道名
GameEntry.Event.Subscribe<NetworkConnectedEvent>(e =>
    Debug.Log($"通道 [{e.ChannelName}] 已连接"));
```

### Config — JSON 模式

```csharp
string json = "[{\"Id\":1,\"Name\":\"Sword\"},{\"Id\":2,\"Name\":\"Shield\"}]";
GameEntry.Config.LoadConfigFromString<ItemConfig>(json);
var item = GameEntry.Config.GetConfig<ItemConfig>(1);
```

## 与 GameFramework 的核心差异

| 维度 | GameFramework | UnityRFramework |
|------|---------------|-----------------|
| 数据结构 | 自定义 `GameFrameworkLinkedList` 等 | C# 标准 `LinkedList<T>` |
| 对象池 | `ObjectBase` 强制继承 | 委托注入，零包装 |
| Component | 含业务逻辑（UIComponent 200+ 行） | 纯配置+转发，≤100 行 |
| Helper 惯例 | Default 占位抛出异常 | 提供可用的默认实现（如 `Resources.Load`） |
| 网络 | 单连接 | 多通道（登录/聊天/游戏服并存） |
| 配置 | 仅 Luban | JSON + Luban 双模式 |

## 技术栈

以下为 Runtime 层默认实现，通过 Helper 桥接可自由替换。Library 层不依赖任何第三方库。

| 领域 | 默认实现 | 可替换方案 |
|------|---------|-----------|
| 资源管理 | YooAsset | Addressables |
| 配置表 | Luban | JSON/CSV（默认 `DefaultConfigHelper`） |
| 代码热更 | HybridCLR | ILRuntime |
| 异步 | Task（Library）/ UniTask（Runtime） | Awaitable、Coroutine |
| 序列化 | MemoryPack | Protobuf |
| UI | UGUI | UI Toolkit、FairyGUI |
| 动画 | DOTween | Animator |

## 参考项目

- [GameFramework](https://github.com/EllanJiang/GameFramework) — 架构蓝本
- [StarForce](https://github.com/EllanJiang/StarForce) — GameEntry + 扩展方法模式
- [UniFramework](https://github.com/gmhevinci/UniFramework) — 轻量工具集参考
- [ET](https://github.com/egametang/ET) — Entity 数据/逻辑分离思想
- [TEngine](https://github.com/Alex-Rachel/TEngine) — YooAsset + HybridCLR 集成参考
