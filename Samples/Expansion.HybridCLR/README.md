# Expansion.HybridCLR

可选的 HybridCLR 代码热更新通用扩展。它不修改 UnityRFramework 的 Library 或默认
Runtime，也不提供第二套下载器；DLL、PDB、AOT 补充元数据和 Manifest 均通过现有
`ResourceComponent` 加载。

`CompileAndStage(...)` 默认只发布 DLL，不发布 PDB。Portable PDB 仅用于改善热更新代码
堆栈信息，并非执行热更新代码的必要文件；只有目标 HybridCLR Player 已验证兼容对应符号
格式时，才应显式传入 `includePdb=true`。PDB 加载失败不应成为正式版本的启动依赖。

## 依赖

- UnityRFramework
- HybridCLR 8.13.0+
- IL2CPP 目标平台

核心包不会自动安装 HybridCLR。导入本 Sample 前，应先按 HybridCLR 官方流程安装插件，
再执行 `HybridCLR/Installer...` 完成本机初始化。

## 提供能力

- `HybridCLRHotUpdateManifest`：描述代码版本、目标平台、AOT 元数据和热更新程序集。
- `HybridCLRAssemblyLoader`：加载元数据和程序集，创建 `IHotUpdateEntry` 入口。
- `IHotUpdateEntry` / `IHotUpdateContext`：AOT 与热更新程序集之间的稳定通信契约。
- `HybridCLRArtifactBuilder.ConfigureProject(...)`：由项目或 Demo Builder 显式登记自己的
  热更新 asmdef 与 AOT 元数据程序集。
- `UnityRFramework/Expansion/HybridCLR/配置并校验`：校验当前已有 HybridCLR 配置，并将
  当前平台设置为 IL2CPP；它不会猜测或硬编码业务程序集。
- `UnityRFramework/Expansion/HybridCLR/生成当前平台代码产物`：执行 HybridCLR 官方
  `Generate/All`。完整 Demo 通常直接使用 `Expansion.HybridCLR.Demo` 的构建菜单。

## 生命周期限制

已加载的热更新程序集不能在当前 Player 进程中卸载或替换。框架软重启会停止旧入口，
然后复用同一份已加载 DLL 创建新入口实例；它可以验证业务生命周期，但不会激活服务器上
刚发布的新 DLL。切换代码版本必须完整退出并重新启动 Player。

Editor 运行时只验证 Manifest、资源加载、入口和 UI 链路；真正的动态代码执行必须使用
目标平台的 IL2CPP Player 验收。AOT 补充元数据必须来自同平台、同主包构建生成的裁剪后
程序集，不能跨平台或跨主包混用。

## 项目接入步骤

### 1. 安装与配置

1. 导入 `Expansion.HybridCLR`，安装与当前 Unity 版本兼容的 HybridCLR。
2. 执行 `HybridCLR/Installer...`，确认 Installer 状态正常。
3. 为热更新代码建立独立 asmdef，并设置 `autoReferenced=false`。
4. AOT 程序集只引用本扩展提供的 `IHotUpdateEntry`、`IHotUpdateContext` 等稳定契约，
   不得直接引用热更新程序集。
5. 在项目 Editor Builder 中调用
   `HybridCLRArtifactBuilder.ConfigureProject(hotUpdateAsmdefPath, patchAotAssemblies)`，登记
   热更新程序集及需要补充元数据的 AOT 程序集。

### 2. 首次主包发布

使用构建工具时，启用 `hybridclr` 步骤并选择 `Player` Recipe：

构建窗口中的“热更新程序集”和“AOT 补充元数据程序集”为只读信息，直接显示
HybridCLR Settings 当前实际配置；请通过“打开 HybridCLR 设置”修改，不在 Profile 中维护副本。

1. 构建工具在 Player 构建前自动执行 HybridCLR 官方 `Generate/All`。
2. 构建该平台 Player。成功后，框架后处理器会在
   `HybridCLRData/UnityRFramework/PlayerBaselines/<BuildTarget>.json` 记录 AOT 基线哈希。
3. 再执行 `HotUpdate` Recipe，编译热更新 DLL、校验 Player 基线、生成 Manifest 和
   `.bytes` 产物，并通过后续资源步骤构建发布包。

选择 `Release` Recipe 时，上述动作在一条流水线内按“Generate/All → Player →
CompileDll/整理产物 → Obfuz（按需）→ YooAsset（按需）”顺序执行。

不使用构建工具时，仍可手动调用 `HybridCLRArtifactBuilder.GenerateCurrentTarget()`、构建
Player，再调用 `HybridCLRArtifactBuilder.CompileAndStage(...)`。

不要在第 3、4 步之间再次执行 `Generate/All`。该操作可能改写
`AssembliesPostIl2CppStrip`，框架的基线校验会拒绝继续发布。

### 3. 仅更新热代码

主包、AOT 程序集和目标平台均未变化时：

1. 修改热更新程序集代码。
2. 执行 `HotUpdate` Recipe，或直接调用 `CompileAndStage(...)`；不要重新执行
   `Generate/All`，也不要重新构建 Player。
3. 构建并发布新的资源包版本。
4. 用户完整退出并重新启动 Player 后，下载并执行新 DLL。

已加载程序集不能在当前进程中替换，因此框架软重启不会激活新代码版本。

### 4. 必须重建主包的情况

出现以下任一变化时，应重新执行“生成代码产物 → 构建 Player → 整理产物 → 发布资源包”
完整流程：

- Unity、HybridCLR 或影响 IL2CPP 的插件版本发生变化。
- AOT 程序集、泛型引用、裁剪规则、Scripting Backend 或 API Compatibility 发生变化。
- 新增或删除热更新程序集，或者改变 AOT/热更新程序集边界。
- 切换 Windows、Android、iOS 等目标平台。

只修改热更新程序集内部业务代码时，不需要重建主包。

### 5. 运行时接入

1. 通过现有资源模块先完成资源版本检查和下载。
2. 创建 `HybridCLRHotUpdateContext`，提供资源组件、代码版本和进入主业务的回调。
3. 调用 `HybridCLRAssemblyLoader.LoadAndStartAsync(...)`。
4. 应用关闭或框架重启时调用加载器 `Shutdown()`。
5. 若加载器提示已加载 DLL 与 Manifest 版本不一致，应要求用户完整退出应用，不能在当前
   进程继续重试新版本。

参考 HybridCLR 官方文档：

- https://www.hybridclr.cn/docs/basic/hotupdateassemblysetting
- https://www.hybridclr.cn/docs/basic/runhotupdatecodes
- https://www.hybridclr.cn/docs/basic/buildpipeline
