# UnityRFramework · Expansion.Demo

`Expansion.Demo` 是官方 Demo 的第三方资源实现和完整资源更新启动示例。它不复制大厅、
配置、UI、战斗或 Procedure 代码，而是复用已导入的 Sample.Demo，并在进入 Demo
流程前执行 YooAsset 资源更新闭环。

启动顺序如下：

1. 初始化 YooAsset Package。
2. Host 模式向服务器请求最新 Package Version，并加载对应 Manifest。
3. 只为 `preload` 标签创建启动差量下载计划；内置文件和磁盘缓存中已有的 Bundle 自动跳过。
4. 没有更新时直接进入官方 Demo。
5. 有更新时显示 UGUI 更新界面，等待用户确认下载。
6. 下载时显示文件数量、已下载/总大小、平均网速和百分比进度。
7. 下载成功后进入官方 Demo；失败时可重新计算差量列表并重试。
8. `ondemand` 标签资源不参与启动下载，进入 Demo 后可通过按需加载验证面板触发静默下载并实例化。

本示例不包含代码热更新。第三方运行模式、取消、软重启和最小资源探针仍由
`Expansion.Tests` 负责。

## 前置条件

1. 导入 `Sample.Demo`。
2. 导入 `Expansion.YooAsset` 与 `Expansion.UniTask` Sample。
3. 导入本 `Expansion.Demo` Sample。
4. 安装 YooAsset 3.0.5 与 UniTask。
5. 先执行 `UnityRFramework/Demo/Export Config and Localization`，生成 Demo 公告等
   项目级 StreamingAssets 文件。

## 生成覆盖层

在非 Play Mode 下执行：

`UnityRFramework/ExpansionDemo/Rebuild Demo Overlay`

构建器会：

1. 自动定位已导入的 Demo，不复制其业务脚本和资源。
2. 生成配置了 `YooAssetResourceHelper` 与 `UniTaskWebRequestHelper` 的框架预制体。
3. 从 DemoBoot 生成 `Generated/Scenes/ExpansionDemoBoot.unity`，替换框架预制体和
   启动入口，并生成序列化 UGUI 更新界面。
4. 创建 `ExpansionDemoPackage`，收集 Demo 的 Resources、业务场景与本 Sample
   `GameAssets/OnDemand/Elastigirl/Elastigirl.fbx` 按需验证模型。
5. 将 ExpansionDemoBoot、DemoHall、DemoExpedition 放入 Build Settings 前三项。

本 Sample 随附 `link.xml`，用于在启用 Player 代码裁剪时保留按需模型所需的
`UnityEngine.SkinnedMeshRenderer`。YooAsset 构建目录中生成的 `link.xml` 只是资源构建
报告，不会自动参与 Player 裁剪。

Demo Collector 使用 Asset GUID 生成短 Bundle 名，不把 UPM Sample 的完整安装路径写入
文件名。这样导入到 `Assets/Samples/UnityRFramework/<版本号>/...` 后也不会因 Windows
路径长度限制导致构建失败。Host 构建菜单会读取 YooAsset Bundle Builder 为当前 Package
保存的文件名样式、压缩、缓存、依赖数据库、内置复制和加解密设置；正式项目仍应按资源
规模和更新粒度设计自己的分组与打包规则。

该 Builder 只写入 `Expansion.Demo/Generated`。即使同时导入并重建
`Expansion.HybridCLR.Demo`，普通 Demo 的框架预制体、启动场景和入口脚本也不会被替换；
再次执行本菜单会把普通 Demo 启动场景切回 Build Settings 第一项。
`ExpansionDemoPackage` 也只收集本 Sample 的资源更新闭环，不包含 HybridCLR DLL、AOT
补充元数据或代码版本 Manifest。

生成完成后，在 YooAsset 构建窗口选择 `ExpansionDemoPackage`：

- EditorSimulate：生成模拟清单后直接运行 ExpansionDemoBoot；差量列表为空，正常进入 Demo。
- Offline：构建并复制全部内置文件后运行；差量列表为空，正常进入 Demo。
- Host：按下文准备内置目录和远程服务器，用于验证完整更新流程。

## Host 完整更新流程

### 首次发布

1. 执行 `UnityRFramework/ExpansionDemo/Build Host Package`。该菜单会为当前平台
   构建新的 `ExpansionDemoPackage` 版本，并增量发布到
   `<工程根目录>/Bundles/ExpansionDemoServer`，不会改写 StreamingAssets。
2. 在 HFS 或其他静态文件服务器中把 `Bundles/ExpansionDemoServer` 设为服务根目录。
   服务器 URL 必须直接对应包含
   `.version`、Manifest、Hash 和 Bundle 的目录。
3. 准备客户端内置目录：
   - 全部内置：构建时使用 `ClearAndCopyAll`。
   - 部分内置：使用 `ClearAndCopyByTags`，并确保
     `BuiltinCatalog.bytes` 与实际内置 Bundle 一致。
   - 全部远程：通过
     `UnityRFramework/Expansion/YooAsset Builtin Catalog`
     为 `ExpansionDemoPackage` 生成空 Catalog。
4. 在生成的 `UnityRFramework.prefab` 上把 Resource 模式改为 `Host`，
   填写 `defaultHostServer`，需要时填写 `fallbackHostServer`。
5. 打开并运行 `Generated/Scenes/ExpansionDemoBoot.unity`。

### 发布资源更新

1. 修改 Demo 使用的配置、语言、音频、Prefab 或场景资源。
2. 再次执行 `UnityRFramework/ExpansionDemo/Build Host Package`。
3. 构建器会把新 Manifest、Hash、Bundle 和 `.version` 发布到同一服务目录；
   `.version` 在构建产物复制过程中覆盖为最新版本。
4. 不替换已发布客户端中的旧 StreamingAssets。
5. 再次启动客户端：旧内置资源仍可使用，变更或新增的 Bundle 会出现在更新界面；
   下载成功后写入 YooAsset 磁盘缓存并进入 Demo。

后续启动会继续检查服务器版本。未改变且已缓存的 Bundle 不会重复下载；资源加载时
YooAsset 按内置文件、磁盘缓存、远程文件系统的能力选择可用来源。
服务器不可达时，Helper 会尝试加载最后一次成功激活且仍完整的本地 Manifest：启动所需
Bundle 已缓存则可以离线进入 Demo；缺少启动资源则仍显示下载需求，实际下载需恢复网络。
全新安装或从未成功激活过版本时没有可回退基线，断网启动会显示检查失败。

切换 Unity Build Target 后必须为目标平台重新构建 Package，不能混用 Windows、
Android、iOS 等平台的 Bundle。

## 地址映射

Demo 默认实现使用 Resources 相对路径。覆盖层通过
`ExpansionDemoResourcesAddressRule` 生成相同 YooAsset Location：

- `Config/Json/Demo_Character.json`
- `Localization/Json/zh-CN.json`
- `Prefabs/UI/DemoHallUI`
- `Audio/music_background.wav`
- `DemoHall`
- `DemoExpedition`

因此 Demo 业务代码不需要判断当前使用 Resources 还是 YooAsset。

## 按需下载验证

构建器把资源分成两类：

- `preload`：Demo 配置、本地化、UI、音频与场景，参加启动更新检查。
- `ondemand`：本 Sample 的 `GameAssets/OnDemand/Elastigirl/Elastigirl.fbx`，地址为
  `ExpansionDemo/OnDemandModel`，不参加启动更新检查。

进入 Demo 后，右上角会显示“YooAsset 按需加载验证”面板。点击“加载远程模型”：

1. 调用 `GetDownloadSize()` 判断本地是否已有该模型 Bundle。
2. 无缓存时，`LoadAssetAsync<GameObject>()` 由 YooAsset 自动从远程服务器静默下载。
3. 下载完成后实例化模型，并在独立预览窗口中显示。
4. 已有磁盘缓存时直接加载，不发生重复网络下载。

验证首次按需下载时，需要先发布包含该模型的最新 Host Package，并确保 HFS 服务目录
指向 `<工程根目录>/Bundles/ExpansionDemoServer`。清理 YooAsset 对应 Package 的磁盘
缓存后重新运行，可再次观察“本地无缓存”路径。

## 已知边界

为了让默认 Demo 与 Expansion.Demo 复用同一份资源，资源源文件仍位于 Sample.Demo 的
`Resources` 目录。Expansion.Demo 实际通过 YooAsset Helper 加载这些资源，但制作
Player 时 Unity 仍可能把它们同时计入 Resources。该结构用于验证 Helper 可替换性，
不作为正式项目的资源目录和包体优化方案。

更新界面目前采用启动期间的固定中文提示，因为本地化资源本身也可能属于待更新内容。
进入 Demo 后继续使用官方 Demo 的中英文语言包。
