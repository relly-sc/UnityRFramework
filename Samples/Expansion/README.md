# UnityRFramework · Expansion

`Expansion` 用于承载 UnityRFramework 的可选第三方插件适配代码。核心框架不依赖这些插件；只有导入本 Sample 并安装对应依赖后，相关 Helper 才参与编译和运行。

## 定位与边界

| 位置 | 职责 | 第三方依赖 |
|---|---|---|
| `Library` / `Runtime` | 框架接口、默认实现和基础运行能力 | 禁止依赖 |
| `Samples/Expansion` | 第三方 Helper、适配器和配套工具 | 允许按功能引入 |
| `Samples/ExpansionAcceptance` | 第三方接入的专项验收示例 | 允许依赖 Expansion |
| `Samples/ExpansionDemo` | 官方 Demo 的第三方 Helper 覆盖层 | 依赖 Demo 与 Expansion |

Expansion 只负责适配，不把第三方插件的类型或生命周期反向扩散到核心框架。未导入 Expansion 时，框架必须仍可正常编译、启动、关闭和重启。

## 当前状态

| 集成 | 当前实现 | 状态 |
|---|---|---|
| YooAsset | `Scripts/Runtime/Resource/YooAssetResourceHelper.cs` | 阶段 1 EditorSimulate / Offline / Host 验收完成 |
| UniTask | `Scripts/Runtime/WebRequest/UniTaskWebRequestHelper.cs` | 阶段 1 验收完成 |
| Excel 解析扩展 | `Scripts/Editor/Config` | 阶段 2 首版完成 |

2026-07-27 已通过 ExpansionAcceptance 完成 EditorSimulate、Offline 与 Host
端到端验收：二进制 `TextAsset` 加载与卸载、Additive 场景加载与卸载、
Web 成功请求、主动取消，以及不退出进程的框架关闭和重启后重复执行。Host
进一步验证了部分内置、部分远程资源，并在保留旧 `BuiltinCatalog.bytes` 时
从服务器 Package 加载更新后的 `RemoteProbe.json` v2；框架软重启后再次加载
v2 并显示最终 `PASS`。Windows Player 与 Android 真机仍需在发布前验收。

`YooAssetResourceHelper` 同时实现 `IResourceUpdateService`。Host 初始化取得最新
Manifest 后，可通过 `PrepareUpdate()` 统计本地缺失的 Bundle，再通过
`DownloadPreparedUpdateAsync()` 接收文件数量、字节数和进度并完成差量下载。
接口只暴露框架定义的数据结构，不向 Demo 或核心框架泄漏 YooAsset 类型。完整使用
流程见 `ExpansionDemo/README.md`。

需要区分启动预下载和运行时按需下载时，可选接口
`ITaggedResourceUpdateService` 提供 `PrepareUpdateByTags()`。启动流程只为指定标签
创建下载计划；未包含在计划中的资源仍可在调用 `LoadAssetAsync()` 时由 YooAsset
按内置文件、磁盘缓存、远程服务器的顺序自动解析，缺少缓存时执行按需下载。

## 推荐实施顺序

### 阶段 1：复核 YooAsset 与 UniTask

先收口现有适配，避免在未验证的基础上继续增加第三方依赖。

工作内容：

1. 按项目当前安装版本核对 YooAsset、UniTask API 和生命周期。
2. 验证 Helper 初始化、正常请求、取消、异常和关闭行为。
3. 完善 `ExpansionAcceptance`，覆盖框架启动、资源加载、场景切换、Web 请求、关闭和不退出进程的重启。
4. 确认第三方模块失败时不会破坏框架其他模块的运行与关闭。

完成标准：

- Expansion 与 ExpansionAcceptance 编译无错误。
- YooAsset 的 EditorSimulate、Offline、Host 中实际支持的模式均有明确配置和验收结果。
- UniTask Web 请求的成功、失败、取消和 Shutdown 路径均可结束，不遗留悬空任务。
- ExpansionAcceptance 可完成一次“启动 → 使用 → 关闭 → 重启 → 再次使用”。

### 阶段 2：接入轻量 Excel 解析扩展

提供一条容易理解和维护的 Excel 直出管线。该扩展只负责在
Editor 中通过 ExcelDataReader 读取 `.xlsx` / `.xls`，然后复用现有
ConfigPipeline 生成 Config JSON、URFC v2、配置代码，以及 Localization JSON、
URFL v2 和 URLM v1；Runtime 不直接读取 Excel，也不依赖 ExcelDataReader。

Excel 内容继续遵循 Demo 现有约定，不另建一套规则：

- Config 第一行为字段名，第二行为字段类型，第三行为字段注释，第四行开始为数据，并包含唯一的 `int Id` 字段。
- Localization 第一行为 `Key,Value`，第二行为 `string,string`，第三行为字段注释，第四行开始为数据。
- 字段类型、枚举、数组、`List<T>`、自定义 Codec、同类型分片和重复 ID/Key 校验均沿用现有 ConfigPipeline 规则。
- 一个工作簿只有一个非空 Sheet 时使用 Excel 文件名作为表名，兼容现有一个文件一张表。
- 一个工作簿包含多个非空 Sheet 时使用 Sheet 名作为表名；同类型分片继续使用
  `表名@分片名` 命名，并沿用重复 ID 校验和运行时合并规则。

工作内容：

当前入口：

1. 菜单 `UnityRFramework/Expansion/Excel 配置表工具`：Config 与 Localization
   使用独立的 Excel 目录、产物目录和导出器选择；Config 额外配置代码目录及命名空间，
   Localization 额外配置多语言容器及容器名。
2. 在 Project 视图右键选中的 `.xlsx` / `.xls` 文件或文件夹，使用
   `UnityRFramework/Excel/Config` 或 `UnityRFramework/Excel/Localization`
   子菜单直接导出。该入口递归处理所选文件夹；Config 固定输出到
   `Assets/Resources/Config`，代码固定输出到
   `Assets/Generated/UnityRFramework/Config`；Localization 固定输出到
   `Assets/Resources/Localization`。
3. JSON 输出位于所选产物目录的 `Json` 子目录；URFC v2 输出位于 `Binary`
   子目录。公式单元格只读取工作簿已保存的计算结果，不实现公式计算引擎。
4. `ExcelDataReader.dll` 和 `ExcelDataReader.DataSet.dll` 只允许 Editor
   平台加载，不得进入 Player；当前实现使用底层 Reader API，不依赖 DataSet API。

Config 自定义格式实现 `IExcelConfigExporter`，再在 Editor 初始化时注册：

```csharp
[InitializeOnLoadMethod]
private static void RegisterExporter()
{
    ExcelConfigExporterRegistry.Register(new ProjectConfigExporter());
}
```

Localization 自定义格式独立实现 `IExcelLocalizationExporter`，通过
`ExcelLocalizationExporterRegistry.Register()` 注册。两类导出器都会收到已经完成
对应 Schema、语言代码、重复 ID 或 Key 校验的数据，返回相对输出路径与字节内容。
相对路径不得逃逸所选产物目录，不同导出器也不能写入同一目标文件。

公式缺少缓存值的精确诊断和 ExpansionDemo 运行时等价验收留在阶段 2 后续项。移除
Expansion 后，默认 CSV 管线仍可独立工作。

这条轻量管线适合规则固定、表结构简单的项目。尚未实际接入的第三方技术不在本文档
中预先声明；完成代码接入和验收后，再补充对应 Helper、依赖和使用说明。

## 通用验收规则

每个第三方适配都必须满足以下规则：

1. 未导入对应 Sample 或未启用对应 Helper 时，核心框架保持零第三方依赖。
2. 依赖缺失时给出清晰的导入说明，不能让错误扩散为核心框架故障。
3. Helper 的初始化、更新、异常、取消、Shutdown 和 Restart 语义与框架模块契约一致。
4. 不在 Library 层直接输出日志；Library 通过 `RFrameworkException` 表达错误，Runtime/Expansion 的运行日志通过 Runtime `Log` 输出。
5. 公开配置需在 Inspector 或文档中说明用途、默认值和生效条件。
6. 至少通过一次 ExpansionAcceptance 端到端运行；涉及 AOT 的集成还必须通过目标平台构建和真机验证。

## 当前 Helper 使用方式

1. 在 Package Manager 中导入 **Expansion** Sample。
2. 手动安装所需第三方包。UPM 不会根据 Sample 内容自动安装可选依赖。
3. 在 `UnityRFramework` 预制体 Inspector 中配置对应 Helper：
   - Resource Helper：`UnityRFramework.Expansion.YooAssetResourceHelper`
   - Web Request Helper：`UnityRFramework.Expansion.UniTaskWebRequestHelper`
4. 按 Helper 的 Inspector 配置初始化参数，再通过 ExpansionAcceptance 验证，不要直接用正式业务场景代替首次验收。

仅导入 `ExpansionAcceptance` 还不能直接运行。Package Manager 只会把 Sample 自身复制到
`Assets/Samples/...`，不会把文件写入宿主工程的 `Assets/StreamingAssets`。
导入 `Expansion` 和 `ExpansionAcceptance` 后，必须先执行：

`UnityRFramework/ExpansionAcceptance/Rebuild Acceptance Assets`

该菜单会按脚本实际所在位置定位导入后的 Sample，生成 WebRequest 探针、验收场景、
YooAsset Collector 和示例框架预制体。Offline/Host 所需的 YooAsset
`StreamingAssets/yoo/<PackageName>` 内容仍需通过 YooAsset 构建窗口或内置目录
工具生成。详细模式步骤见 `ExpansionAcceptance/README.md`。

当前开发工程已安装 YooAsset 与 UniTask，Excel 扩展携带 EditorOnly 的
ExcelDataReader DLL。每个 Helper 只依赖自己实际使用的插件。

### YooAsset Host 磁盘缓存

当前 `YooAssetResourceHelper` 按 YooAsset 3.0.5 API 实现可选磁盘缓存容量治理。
在 `ResourceComponent` Inspector 中启用 `Auto Clear Cache` 并设置
`Max Cache Size (GB)` 后，Helper 会在 Host 模式加载活动清单后执行一次检查：

1. 使用 YooAsset 官方 `ClearUnusedBundleFiles` 清理不再属于当前清单的 Bundle。
2. 若仍超过上限，按框架持久化的资源访问时间从旧到新分批调用
   `ClearBundleFilesByLocations`。
3. 清理失败只记录警告，不阻断资源模块和框架启动。

访问记录保存在该 Package 的沙盒缓存根目录
`UnityRFrameworkCacheUsage.json`。Helper 会把同一次成功加载的原始 location、
Address 与 AssetPath 一并记录，并在正常关闭时刷新文件。

YooAsset 3.0.5 没有公开内部 Bundle 的最后访问时间与自定义淘汰策略，因此这是
Location 级近似 LRU。多个资源或依赖共享同一 Bundle 时，官方按 Location 清理仍可能
删除整个共享 Bundle，后续访问会重新下载。实现不会直接删除 YooAsset 缓存目录，
EditorSimulate 与 Offline 模式也不会执行下载缓存清理。

### YooAsset Host 内置目录工具

菜单 `UnityRFramework/Expansion/YooAsset Builtin Catalog` 会从
`BundleCollectorSetting` 动态读取 Package，并输出到 YooAsset 当前配置的
StreamingAssets 根目录。

- `生成空 Catalog（全部资源远程）`：只生成 Host 初始化所需的空
  `BuiltinCatalog.bytes`，不把任何 Bundle 标记为内置资源。
- `根据内置目录生成 Catalog（包含首包资源）`：读取所选 Package 的版本、
  Manifest 和现有 Bundle，重新生成与实际首包文件一致的 Catalog。

构建时使用 `ClearAndCopyAll` 或 `ClearAndCopyByTags`，YooAsset 会自动处理首包
文件；手动移除或调整内置 Bundle 后，应使用本工具重新生成 Catalog，避免目录
记录与实际文件不一致。

YooAsset v3 的 EditorSimulate 单包只能使用一种虚拟 Bundle 类型。当前
`YooAssetResourceHelper` 面向同时包含 Prefab、场景、JSON 和二进制配置的普通
资源包，因此 `byte[]` 与 `string` 从普通 Bundle 内的 `TextAsset` 转换，不按
RawFile 规则收集。需要直接访问视频等原生文件时，应使用独立 RawFile 包及专用
适配，不要与当前混合资源包共用同一包配置。

## Sample 关系

```text
Samples/
├── Expansion/         第三方 Helper 与工具，本目录，不单独运行
├── Demo/              使用框架默认实现的完整示例
├── ExpansionAcceptance/ 第三方运行模式与异常路径专项验收
└── ExpansionDemo/     复用 Demo 业务的资源更新与第三方 Helper 完整闭环
```

Demo 与 ExpansionDemo 应覆盖相同的主要业务链路：前者验证默认实现，后者在复用
业务代码的基础上增加资源版本检查、更新提示、差量下载，再验证第三方实现。
ExpansionDemo 不得复制出第二套玩法实现。ExpansionAcceptance 独立负责运行模式、
远程资源、取消和软重启等专项探针。

## 开发与发布约定

开发阶段使用 `Samples/`，让 Unity 随宿主工程编译并便于调试；发布 UPM 包时使用 `Samples~/`，由 Package Manager 按 `package.json` 的 `samples` 声明导入。

本开发工程只维护 `Samples/` 下的内容；`Samples~/` 发布副本由维护者在发布前
手动全量同步。新增第三方集成时，应更新本文件的状态、依赖、配置方式和验收结果。
