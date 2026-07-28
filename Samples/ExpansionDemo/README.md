# UnityRFramework · ExpansionDemo

`ExpansionDemo` 是第三方 Helper 的最小端到端验收场景，不复制正式 Demo 的业务
玩法。当前覆盖 YooAsset 3.0.5 与 UniTask WebRequest Helper。

## 前置依赖

- `com.tuyoogame.yooasset` 3.0.5
- `com.cysharp.unitask`
- `Samples/Expansion` 中的第三方 Helper

MemoryPack、NPOI 和 EPPlus 当前均不是本示例依赖。

## Package Manager 导入后必做

`ExpansionDemo` 不能在刚导入后直接进入 Play Mode。UPM 的 Import Sample 只复制
Sample 目录，不允许 Sample 在导入过程中自动向宿主工程
`Assets/StreamingAssets` 写文件，因此必须完成以下准备：

1. 先导入 `Expansion` Sample，再导入 `ExpansionDemo` Sample。
2. 手动安装 YooAsset 3.0.5 与 UniTask；UPM 不会根据 Sample 代码自动安装
   这些可选依赖。
3. 等待 Unity 完成编译。
4. 在非 Play Mode 下执行
   `UnityRFramework/ExpansionDemo/Rebuild Acceptance Assets`。
5. 确认已生成
   `Assets/StreamingAssets/ExpansionDemo/WebProbe.txt`。
6. 打开当前导入目录中的
   `GameAssets/Scenes/ExpansionDemo.unity`，再按下 Play。

构建器通过自身脚本路径动态定位 Sample，因此同时兼容开发工程中的
`Assets/UnityRFramework/Samples/ExpansionDemo` 和 Package Manager 导入后的
`Assets/Samples/<包名>/<版本>/ExpansionDemo`，不要求用户移动 Sample。

`Rebuild Acceptance Assets` 会重新复制示例框架预制体，并将 Resource 模式恢复为
`EditorSimulate`、清空 Host 地址。需要测试 Offline 或 Host 时，应先执行 Rebuild，
再修改示例预制体上的模式和服务器地址。

## 生成验收资产

在 Unity 菜单执行：

`UnityRFramework/ExpansionDemo/Rebuild Acceptance Assets`

构建器会在编辑器中完成以下工作：

1. 生成内置二进制探针、远程 JSON 探针和 Additive 内容场景。
2. 复制框架预制体，并配置 `YooAssetResourceHelper` 与
   `UniTaskWebRequestHelper`。
3. 生成序列化 UGUI 启动场景；运行时代码只更新文本和绑定事件，不控制布局。
4. 创建 `ExpansionDemoPackage` 收集规则。
5. 将启动场景设为 Build Settings 第 0 项，以便验证框架软重启。

其中 `Assets/StreamingAssets/ExpansionDemo/WebProbe.txt` 位于 Sample 目录之外，
删除 Sample 时 Unity 不会自动删除它；重新导入或文件缺失时再次执行 Rebuild 即可。

## 各模式最短运行步骤

### EditorSimulate

1. 执行 `Rebuild Acceptance Assets`。
2. 保持示例框架预制体的 Resource 模式为 `EditorSimulate`。
3. 直接运行验收场景。

EditorSimulate 不需要提前构建 Bundle，但仍需要 Rebuild 生成普通 WebRequest
使用的 `Assets/StreamingAssets/ExpansionDemo/WebProbe.txt`。

### Offline

1. 执行 Rebuild。
2. 在 YooAsset 构建窗口构建 `ExpansionDemoPackage`，设置
   `Bundled Copy Option = ClearAndCopyAll`。
3. 确认 `Assets/StreamingAssets/yoo/ExpansionDemoPackage` 中存在
   `BuiltinCatalog.bytes`、版本、Manifest、Hash 和全部 Bundle。
4. 将示例框架预制体的 Resource 模式改为 `Offline` 后运行。

完整验收流程会加载 `RemoteProbe.json`，因此 Offline 模式必须使用
`ClearAndCopyAll`，不能只复制 `builtin` 标签。

### Host

1. 执行 Rebuild。
2. 构建新的 Package Version，并将该版本完整产物部署到服务器。
3. 按下文“Host 混合包验收”或“纯远程 Host 模式”准备
   `Assets/StreamingAssets/yoo/ExpansionDemoPackage`。
4. 将示例框架预制体的 Resource 模式改为 `Host`，填写
   `defaultHostServer`，需要时填写 `fallbackHostServer`。
5. 运行场景并确认最终显示 `PASS`。

服务器 URL 必须直接对应包含 `.version`、Manifest、Hash 和 Bundle 的目录。
切换 Unity Build Target 后必须为目标平台重新构建 YooAsset Package；Windows 的
Bundle 不得直接作为 Android、iOS 等平台的内置或远程产物。

## 自动验收流程

打开并运行：

`GameAssets/Scenes/ExpansionDemo.unity`

默认会自动执行：

1. 按 ResourceComponent 当前模式初始化 YooAsset 资源包并激活包清单。
2. 以 `byte[]` 加载、校验和卸载普通 Bundle 内的二进制 `TextAsset`。
3. 加载并卸载 `ExpansionContent` Additive 场景。
4. 加载并解析 `RemoteProbe.json`，输出远程探针版本和消息。
5. 通过 UniTask Helper 读取构建器生成的本地 StreamingAssets 探针文件。
6. 对探针请求主动取消，并校验结果为 `WebRequestError.Aborted`。
7. 请求框架软重启，等待旧 YooAsset 包异步销毁后重新初始化。
8. 重启后再次执行上述全部链路。

最终状态区应出现：

`PASS：框架重启后全部第三方链路再次通过。`

## 2026-07-27 验收结果

- Unity C# 编译错误：0。
- 首次启动、资源、场景、Web 成功请求与取消：通过。
- Restart 关闭、重新启动和第二轮完整链路：通过。
- Play Mode 运行捕获新增未处理错误：0。
- Quit 关闭流程：通过。
- EditorSimulate：通过。
- Offline：通过。
- Host 纯远程初始化与加载：通过。
- Host 部分内置、部分远程：通过。
- 保留旧 `BuiltinCatalog.bytes`，服务器 Package 更新后加载
  `RemoteProbe.json` v2：通过。
- 框架软重启后再次加载 v2 并显示最终 `PASS`：通过。

以上为 Unity Editor 环境验收；Windows Player 与 Android 真机仍属于目标平台
发布前验收。

## Host 混合包验收

当前收集规则为：

- `ExpansionProbe.bytes`：`builtin` 标签，随首包内置。
- `ExpansionContent.unity`：`builtin` 标签，随首包内置。
- `RemoteProbe.json`：无 `builtin` 标签，只从远程服务器或本地缓存加载。

首次构建客户端基线包时，在 YooAsset 构建窗口设置：

```text
Bundled Copy Option = ClearAndCopyByTags
Bundled Copy Params = builtin
```

构建完成后，`Assets/StreamingAssets/yoo/ExpansionDemoPackage` 应包含 Catalog、
版本、Manifest、Hash，以及 `ExpansionProbe`、`ExpansionContent` 和必要依赖的
Bundle；不应包含 `RemoteProbe` Bundle。服务器目录部署该版本的完整构建产物。

验证远程更新时：

1. 先运行一次基线版本，确认状态区输出 `RemoteProbe` 的当前版本。
2. 修改 `GameAssets/YooAsset/Remote/RemoteProbe.json` 中的 `version` 和
   `message`。
3. 使用新的 Package Version 重新构建，但将 `Bundled Copy Option` 设为
   `None`，不要覆盖基线客户端的 StreamingAssets 和旧 `BuiltinCatalog.bytes`。
4. 将新版本的完整构建产物更新到服务器，最后替换
   `ExpansionDemoPackage.version`。
5. 清理旧测试缓存或使用首次未下载该版本的客户端运行，确认状态区输出新的
   `version` 和 `message`，同时两个内置资源仍能正常加载。

`RemoteProbe.json` 只在文件不存在时由构建器创建，执行
`Rebuild Acceptance Assets` 不会覆盖手动修改的版本内容。

## 纯远程 Host 模式

纯远端 Host 模式不能直接删除整个内置包目录。YooAsset v3 的
`BuiltinFileSystem` 即使不包含任何内置 Bundle，也仍需读取本地
`BuiltinCatalog.bytes`。

先执行：

`UnityRFramework/Expansion/YooAsset Builtin Catalog`

在工具中选择 `ExpansionDemoPackage`，然后点击：

`生成空 Catalog（全部资源远程）`

工具会在以下目录生成空的内置目录文件：

`Assets/StreamingAssets/yoo/ExpansionDemoPackage/BuiltinCatalog.bytes`

空目录文件只用于初始化 Builtin 文件系统，不会将任何 Bundle 标记为内置资源。
版本文件、Manifest 和 Bundle 仍会从 `defaultHostServer` 下载。服务器根目录应
直接包含 `ExpansionDemoPackage.version`、对应版本的 Manifest 和所有 Bundle，
不要在 URL 与这些文件之间额外嵌套平台、包名或版本目录。

## 目录

```text
ExpansionDemo/
├── Scripts/
│   ├── Editor/ExpansionDemoBuilder.cs
│   └── Runtime/ExpansionDemoController.cs
└── GameAssets/
    ├── Prefabs/UnityRFramework.prefab
    ├── Scenes/ExpansionDemo.unity
    └── YooAsset/
        ├── Raw/ExpansionProbe.bytes
        ├── Remote/RemoteProbe.json
        └── Scenes/ExpansionContent.unity
```

`Raw` 是示例目录的历史命名；当前文件按普通 `TextAsset` 收集，不是 YooAsset
RawFile 包。

构建器还会生成 `Assets/StreamingAssets/ExpansionDemo/WebProbe.txt`，因此 Web
验收不依赖 UnitySkills REST、外部网站或公网连接。
