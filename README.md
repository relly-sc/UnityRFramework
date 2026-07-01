# UnityRFramework

轻量级 Unity 游戏框架，参考 GameFramework 架构思路，面向 HybridCLR 热更 + YooAsset 资源管理的现代技术栈。

**设计理念：接口优先、组件归组件、逻辑归逻辑。用 C# 标准库，不做"为封装而封装"。**

## 技术栈

| 领域 | 选型 | 备注 |
|------|------|------|
| 资源更新 | YooAsset | 成熟稳定，支持分包、加密 |
| 代码热更 | HybridCLR | 纯 C# 热更，无需 Lua / ILRuntime |
| 异步 | UniTask | 零 GC 异步，替代协程 |
| 配置 | Luban | 表格配置转代码 + 二进制 |
| 序列化 | MemoryPack | 零 GC 序列化，性能优于 Protobuf |
| UI | UGUI | 原生支持，生态成熟 |
| 动画 | DOTween | 老牌补间动画库 |
| 依赖注入 | VContainer | 前期不用，后期接入 |
| 事件系统 | 前期自研 → 后期 MessagePipe | 用接口抽象，后期可无缝切换 |

## 三层架构

```
Unity Client
├── Launcher 层 (AOT)
│   ├── 启动入口
│   ├── YooAsset 初始化
│   ├── HybridCLR 初始化
│   └── 热更 DLL 加载
│
├── Framework 层 (AOT — 接口 + 核心实现)
│   ├── Log / Event / Pool / Timer    ← Phase 0 基石
│   ├── Resource / Config            ← Phase 1 核心
│   ├── Procedure / UI / Audio       ← Phase 2 游戏流程
│   └── Save / Network               ← Phase 3 扩展
│
└── Game / HotUpdate 层 (热更 DLL)
    └── Entry / Procedures / Gameplay / UI / Network
```

## 模块概览

| 模块 | 职责 | 核心接口 | 状态 |
|------|------|----------|:--:|
| Log | 统一日志输出（控制台/文件），级别过滤 | `ILogModule` | ✅ |
| Pool | GameObject 池 + class 池，委托注入 | `IPoolModule` | ✅ |
| Event | 解耦消息通信，`Subscribe<T>` / `Publish<T>` | `IEventBus` | 待建 |
| Timer | 帧计时器 + 时间计时器 | `ITimerModule` | 待建 |
| Resource | YooAsset 封装，异步加载，引用计数 | `IResourceModule` | 待建 |
| Config | Luban 集成，二段式加载 | `IConfigModule` | 待建 |
| Procedure | FSM 状态机引擎 | `IProcedureModule` | 待建 |
| UI | UI 组管理，生命周期 | `IUIModule` | 待建 |
| Audio | BGM/SFX/UI 三组，AudioSource 池 | `IAudioModule` | 待建 |
| Save | MemoryPack 序列化，多槽位 | `ISaveModule` | 待建 |
| Network | 连接管理，心跳，断线重连 | `INetworkModule` | 待建 |

## 快速开始

### 场景配置

启动场景中挂 `UnityRFramework` 预制体（`Assets/UnityRFramework/Prefabs/UnityRFramework.prefab`），包含：

```
"UnityRFramework" (根节点, DontDestroyOnLoad)
├─ GameEntry            ← 框架入口，Inspector 可视化所有子模块
├─ "Base"               ← 框架基础设施
│    └─ BaseComponent   (Helpers + Update 驱动 + Shutdown)
├─ "Pool"               ← 对象池模块
│    └─ PoolComponent   (PoolModule 包装)
└─ ...（将来每个模块一个子节点 + Component）
```

### 访问模块

所有模块通过 `GameEntry` 统一入口访问：

```csharp
// 内置模块：强类型静态属性
GameEntry.Pool.CreatePool<Bullet>("Bullet", ...);
GameEntry.Base.FrameRate = 60;

// 自定义模块：泛型方法（零维护成本，不改 GameEntry）
var shop = GameEntry.Get<ShopComponent>();
shop.BuyItem(itemId);
```

## Component 设计原则

每个模块配一个薄 Component（≤50 行），挂在 `RFramework` 根节点上。Component 只做三件事：

1. **Inspector 配置注入** — `[SerializeField]` 暴露配置给策划
2. **Unity 生命周期桥接** — 需要 `Update()` 的模块由 Component 驱动
3. **GameEntry 统一入口** — 业务层通过 `GameEntry.Xxx` 访问模块

所有业务逻辑留在 Library 层 Module，Component 仅负责创建 Module、缓存引用、转发调用。

## Pool 模块使用示例

已完成的 Pool 模块支持 class 池和 GameObject 池，通过委托注入行为。

```csharp
// class 池（委托回调）
var pool = GameEntry.Pool.CreatePool<MyClass>("MyPool",
    createFunc: () => new MyClass(),
    onSpawn: obj => obj.Reset(),
    onUnspawn: obj => obj.Clear(),
    capacity: 32);

var obj = pool.Spawn();
pool.Unspawn(obj);

// class 池（IPoolable 接口，无需传委托）
var dataPool = GameEntry.Pool.CreatePool<BulletData>("BulletData",
    createFunc: () => new BulletData(),
    capacity: 64);

// GameObject 池（一行创建）
var bulletPool = GameEntry.Pool.CreateGameObjectPool(
    "Bullet", bulletPrefab,
    parent: bulletRoot,
    prewarmCount: 20,
    capacity: 64);

var bullet = bulletPool.Spawn();   // SetActive(true)
bulletPool.Unspawn(bullet);        // SetActive(false) + 挂回 parent
```

## 与 GameFramework 的核心差异

| 维度 | GameFramework | UnityRFramework |
|------|---------------|-----------------|
| 数据结构 | 自定义 `GameFrameworkLinkedList` 等包装类 | C# 标准 `LinkedList<T>` |
| 对象池包装 | `ObjectBase` 强制继承 | 委托注入，零包装 |
| Component 厚度 | 含业务逻辑（UIComponent 200+ 行） | ≤50 行，纯配置+转发 |
| Module 入口 | Component-first | Module-first，Component 做装饰层 |

## 目录结构

```
Assets/UnityRFramework/
├── Library/RFramework/RFramework/  ← 纯 C# 模块，不依赖 UnityEngine
│   ├── Base/                        ← RFrameworkModule、RFrameworkModuleEntry
│   ├── Log/                         ← RFrameworkLog、RFrameworkLogLevel
│   └── Pool/                        ← IPoolModule、PoolModule、ObjectPool<T>
├── Scripts/Runtime/                 ← Unity 运行时
│   ├── Base/                        ← UnityRFrameworkComponent、ComponentEntry
│   ├── GameEntry.cs                 ← 框架入口 MonoBehaviour
│   ├── Utility/                     ← DefaultLogHelper
│   └── Pool/                        ← PoolComponent
├── Scripts/Editor/                  ← 编辑器工具
└── Prefabs/                         ← UnityRFramework.prefab
```

## 代码风格

- UTF-8 without BOM，LF 换行
- 驼峰命名：私有字段 `camelCase`（不加 `_`），公有 `PascalCase`，接口 `I` 前缀
- 4 空格缩进，Allman 花括号（左花括号换行）
- 中文注释，公开成员必须 XML 注释
- 模块命名：接口 `IXxxModule`，实现 `XxxModule`

详见 `框架设计.md` 第六章。

## 参考项目

- [GameFramework](https://github.com/EllanJiang/GameFramework) — 架构参考，取其结构去其重量
- [StarForce](https://github.com/EllanJiang/StarForce) — GF 官方示例，参考 GameEntry + 扩展方法模式
- [UniFramework](https://github.com/gmhevinci/UniFramework) — 轻量级工具集，已集成 UniLog（日志文件写入）

## 反模式禁令

- 禁止 `GameObject.Find()` / `FindObjectOfType()`
- 禁止 `DontDestroyOnLoad` 单例滥用
- 禁止 500+ 行 God MonoBehaviour
- 禁止跨对象 `GetComponent<XXX>()` 硬引用
- 禁止魔法字符串（tag / layer / animator 参数）
- 禁止 `Update()` 中写可用事件驱动的逻辑
- 禁止直接使用 `Debug.Log`，一律通过框架的 `LogModule` 输出
