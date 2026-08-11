# Expansion.HybridCLR.Demo

这是 `Expansion.Demo` 的可选代码热更新覆盖层。它复用已有的官方 Demo 业务、更新 UI
和资源热更流程，但使用独立的 `ExpansionHybridCLRDemoPackage`、收集分组、生成预制体、
启动场景与 Host 发布目录，并增加 HybridCLR DLL、AOT 补充元数据、代码版本 Manifest
与热更新入口 UI。

不需要代码热更新的项目只导入 `Expansion.Demo`，无需导入本 Sample。

## 导入依赖

按以下顺序导入并安装：

1. `Sample.Demo`
2. `Expansion.YooAsset`，并安装 YooAsset 3.0.5+
3. `Expansion.UniTask`，并安装 UniTask
4. `Expansion.Demo`
5. `Expansion.HybridCLR`，并安装 HybridCLR 8.13.0+
6. `Expansion.HybridCLR.Demo`
7. 执行 `HybridCLR/Installer...` 完成 HybridCLR 本机初始化

## 首次构建

1. 切换到目标平台。
2. 执行
   `UnityRFramework/Expansion/HybridCLR Demo/重建当前平台覆盖层`。
   工具会设置 IL2CPP、执行 HybridCLR `Generate/All`、在本 Sample 的 `Generated` 目录
   创建独立框架预制体和序列化 UGUI、重建独立启动场景，并追加 `hotupdate` 收集规则。
3. 为 `ExpansionHybridCLRDemoPackage` 准备内置目录。全部资源远程时，通过
   `UnityRFramework/Expansion/YooAsset Builtin Catalog` 选择该 Package 并生成空
   `BuiltinCatalog`；存在内置 Bundle 时，按实际内置文件生成 Catalog。不能复用
   `ExpansionDemoPackage` 的 Catalog。
4. 构建该平台 Player。此步骤会生成与该 Player 严格对应的裁剪后 AOT 程序集。
5. 执行
   `UnityRFramework/Expansion/HybridCLR Demo/构建 Host Package`。工具只重编热更新 DLL，
   并使用第 4 步 Player 留下的 AOT 裁剪产物生成 Manifest 和 Host Package，产物发布到
   `Bundles/ExpansionHybridCLRDemoServer`。不要在第 4、5 步之间再次执行 `Generate/All`。
   工具会校验 Player Build 后记录的 AOT 哈希；缺少基线或文件被改写时会拒绝发布。
6. 按 `Expansion.Demo` README 配置 Host URL 和本地 HTTP 服务，但使用本 Sample 的
   `ExpansionHybridCLRDemoServer` 服务目录。

构建 Player 前，必须在
`Expansion.HybridCLR.Demo/Generated/Prefabs/UnityRFramework.prefab` 的
Resource 组件上把模式设为 `Host`，并填写直接指向服务器包目录的
`defaultHostServer`。Builder 首次生成时仍使用 EditorSimulate；后续重建会保留该 Prefab
已有的模式和主/备用 Host URL，不再覆盖手动配置。

生成的启动场景位于
`Expansion.HybridCLR.Demo/Generated/Scenes/ExpansionHybridCLRDemoBoot.unity`，挂载
`ExpansionHybridCLRDemoGameEntry`；普通 Expansion Demo 继续使用自己的
`Expansion.Demo/Generated/Scenes/ExpansionDemoBoot.unity` 和 `ExpansionDemoGameEntry`。
两个 Builder 不再覆盖对方的生成物，执行哪个重建菜单，哪个启动场景就会成为 Build
Settings 第一项。HybridCLR Demo 仍复用 Expansion.Demo 的业务资源和流程，因此导入依赖
顺序保持不变，但这些业务资源会被独立收集到 `ExpansionHybridCLRDemoPackage`；运行时
不会依赖或加载 `ExpansionDemoPackage`。

启动入口会检查 `preload + hotupdate` 两类标签：有差量时沿用现有 UI 显示文件数、大小、
网速和进度；资源就绪后加载 AOT 元数据与热更新 DLL，显示代码版本，再由热更新按钮进入
现有 Demo。

## 后续代码更新

1. 修改 `UnityRFramework.HotUpdate` 中的代码或 UI 行为。
2. 直接执行“构建 Host Package”；工具会重编热更新 DLL、生成新代码版本并发布，
   不会覆盖现有 Player 的 AOT 基线。
3. 不替换已发布 Player。
4. 完整退出 Player 进程后重新启动，下载差量并验证新代码版本。

框架软重启不会激活新 DLL。它只会复用当前进程已经加载的程序集，用于验证入口
`Shutdown()` 和再次启动是否正确。

## 验收边界

- EditorSimulate：验证 Manifest、入口实例化、热更新 UI 和进入 Demo。
- Windows IL2CPP Player：完成 v1 首次下载、软重启、v2 差量下载和完整进程重启验收。
- Android 等平台：必须重新生成对应平台 DLL、AOT 元数据和 YooAsset Bundle，不能复用
  Windows 产物。

## Windows 人工验收流程

以下流程用于验证完整资源热更与代码热更新闭环。每一步都应在前一步通过后继续。

### A. 验收前准备

- [ ] HFS 或其他静态文件服务已启动。
- [ ] `defaultHostServer` 能直接访问服务器上的 Package 目录，例如
      `http://192.168.3.155/ExpansionHybridCLRDemoServer`。
- [ ] 服务器已完整部署 `Bundles/ExpansionHybridCLRDemoServer` 内容；发布时最后替换
      `ExpansionHybridCLRDemoPackage.version`，避免客户端先看到尚未上传完成的新版本。
- [ ] 启动场景中 `manifestLocation` 指向当前平台，例如
      `HotUpdate/StandaloneWindows64/Manifest`。
- [ ] Player 使用 Host 模式和 IL2CPP 构建。

### B. v1 首次启动与下载

1. 完整退出 Player。
2. 如需模拟全新安装，在 Player 关闭后清理该应用的 YooAsset Sandbox 缓存；不要删除
   服务器文件。
3. 启动 v1 Player。
4. 预期出现更新提示，显示待下载文件数和总大小。
5. 点击下载，检查进度、已下载大小和网速持续更新，直至下载完成。
6. 预期显示 HybridCLR 热更新入口 UI，其代码版本与服务器 Manifest 的
   `codeVersion` 一致。
7. 点击“进入 Demo”，确认大厅、场景、UI 和基础交互正常。

通过标准：无 `metadata type not match`、程序集加载失败、入口类型不存在或未处理异常。

本 Demo 默认不发布 PDB。若自行开启 PDB 后出现 `BadImageFormatException: LoadPDB Error:7`，
表示当前 Player 不接受该符号文件格式；关闭 `includePdb` 后重新构建 Host Package 即可，
无需重建 Player。

### C. 缓存与按需复用

1. 完整退出并重新启动同一个 Player，不清理缓存、不发布新版本。
2. 预期不重复下载已有文件，或下载计划显示 0 个文件。
3. 热更新入口仍显示相同代码版本，并能再次进入 Demo。

断网验收前至少在线成功启动一次，使 Helper 记录最后一次可用 Package Version。随后完整
退出或执行框架软重启并断开网络：本地 Manifest、启动 Bundle、热更新 DLL 和 AOT 元数据
均完整时应继续进入相同代码版本；缺少任一启动资源时应停留在下载或错误界面，而不是使用
不完整缓存继续启动。全新安装且没有本地可用版本不属于可离线启动场景。

### D. 框架软重启

1. 在未发布新代码版本时点击 Demo 的“重启”按钮。
2. 预期框架依次关闭并重新启动，热更新入口和 Demo 能再次进入。
3. 代码版本保持不变，因为软重启复用当前进程已加载的 DLL。

软重启只验收生命周期，不用于激活新代码。

### E. v2 代码热更新

1. 修改 `UnityRFramework.HotUpdate` 中一处可见文字或行为。
2. 不重新构建 Player，直接执行
   `UnityRFramework/Expansion/HybridCLR Demo/构建 Host Package`。
3. 将新的 `Bundles/ExpansionHybridCLRDemoServer` 内容部署到服务器，最后更新 `.version` 文件。
4. 保持旧 Player 文件不变，完整结束 Player 进程后重新启动。
5. 预期检测到差量文件；点击下载后显示新的 `codeVersion` 和新行为。
6. 点击“进入 Demo”，确认正式业务仍可运行。

如果服务器发布 v2 后只执行框架软重启，加载器应拒绝把已加载的 v1 DLL伪装成 v2，并提示
完整重启 Player。这是预期保护行为。

### F. 关闭与异常检查

- [ ] 点击游戏内退出按钮能正常结束进程。
- [ ] 编辑器停止 Play Mode、框架软重启和 Player 退出均无未处理异常。
- [ ] YooAsset 在关闭期间输出的 `operation has been aborted` 警告可单独记录；只要是主动
      取消且没有资源继续访问、日志关闭后写入或崩溃，不视为热更新失败。
- [ ] 断网、错误 Host URL 或缺失资源时，更新 UI 能显示错误并允许重试或退出。

## 构建产物位置

- Player：由使用者选择的 Unity Build 输出目录。
- Host 目录：`Bundles/ExpansionHybridCLRDemoServer`。
- Package：`ExpansionHybridCLRDemoPackage`。

代码版本和 Package 版本由每次“构建 Host Package”生成，不在文档中固定记录。
