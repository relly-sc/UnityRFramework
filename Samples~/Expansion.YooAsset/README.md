# UnityRFramework · Expansion.YooAsset

`Expansion.YooAsset` 提供基于 YooAsset 的资源 Helper，用于替换框架默认的
`Resources.Load` / 本地文件实现。它保留 `ResourceModule` 的统一调用接口，并补充
资源更新、按标签下载和磁盘缓存管理能力。

## 前置依赖

- YooAsset 3.0.5 或兼容版本。
- UnityRFramework Runtime。

YooAsset 是可选第三方依赖。导入本 Sample 前应先在宿主项目安装 YooAsset，否则
Sample 中引用 `YooAsset` 命名空间的脚本无法编译。

## 配置

1. 导入 `Expansion.YooAsset` Sample。
2. 在 `ResourceComponent` Inspector 中，将 Resource Helper 设置为
   `UnityRFramework.Expansion.YooAssetResourceHelper`。
3. 填写 YooAsset `Package Name`。
4. 选择 `EditorSimulate`、`Offline` 或 `Host` 运行模式。
5. Host 模式还需填写 `Default Host Server`，并可按需填写备用地址。

框架业务代码仍通过 `GameEntry.Resource` 加载和释放资源，不应直接持有 Helper 内部的
YooAsset Handle。资源引用计数由 `ResourceModule` 管理，最后一个框架引用释放后，
Helper 才释放对应 YooAsset Handle。

## 运行模式

### EditorSimulate

仅用于 Unity Editor。Helper 会使用 YooAsset 编辑器模拟文件系统，适合开发阶段快速
验证收集规则与资源地址，无需预先复制 Bundle 到 StreamingAssets。

### Offline

只使用随 Player 内置的 YooAsset 文件。构建 Package 时需将 Catalog、Manifest、Hash
和所需 Bundle 正确复制到 YooAsset 的 StreamingAssets 目录。

### Host

使用“内置文件 + YooAsset 磁盘缓存 + 远程服务器”的组合。服务器 URL 必须直接指向
包含 `<PackageName>.version`、Manifest、Hash 和 Bundle 的发布目录。

Host 初始化会请求服务器 Package Version 并激活对应 Manifest。成功激活后会记录最后一次
可用版本；后续远程版本请求或新清单加载失败时，若该版本的本地清单仍完整，则回退到该
清单继续启动。首次运行且尚无本地可用版本时仍会报告检查失败，不能仅凭不完整缓存启动。
资源本地不可用时，
YooAsset 可在加载过程中下载所需 Bundle；也可通过更新接口提前计算并下载差量资源。

## Bundle 可选加密

`Expansion.YooAsset` 提供 YooAsset Builder 可直接选择的
`UnityRFramework.Expansion.Editor.UnityRFrameworkBundleEncryptor`。它使用框架的
AES-256-CBC + HMAC-SHA256 数据保护封装，对 Bundle 同时加密并认证；默认仍为
YooAsset 的 `EncryptionNone`，未选择时不会读取密钥或增加运行开销。

启用步骤：

1. 为 Unity Editor 进程设置环境变量 `UNITY_RFRAMEWORK_YOOASSET_KEY`，值为 Base64 编码的
   32 字节密钥。可选设置 `UNITY_RFRAMEWORK_YOOASSET_KEY_ID`；未设置时使用
   `YooAssetBundle`。
2. 重启 Unity，在 `YooAsset/AssetBundle Builder` 中选择目标 Package 和 Pipeline，将
   `Bundle Encryptor` 设为 `UnityRFrameworkBundleEncryptor`。
3. 项目启动时，在调用 `GameEntry.Resource.InitializeAsync()` 前统一注册发布内容密钥：

```csharp
RuntimeKeyProviderRegistry.ConfigureContentKeys(projectKeyProvider);
```

`projectKeyProvider` 必须实现 `RFramework.IKeyProvider`，并能按加密数据中保存的 `KeyId`
返回密钥副本。不要把正式密钥直接序列化到 Prefab、ScriptableObject 或源码中。框架构建步骤
会读取 YooAsset Builder 的当前加密器选择，并在正式打包前校验环境变量，不维护第二份加密开关。
同一注册入口也供受保护 Config 使用；未启用 Config 或 Bundle 加密时，注册提供器不会改变
明文加载流程。`YooAssetBundleProtection.Configure` 仍保留为 YooAsset 专用显式覆盖入口。

当前实现属于整包内存解密，适合提高常规资源提取成本，但加载时会同时占用加密数据和解密后
数据的内存。应控制单个 Bundle 大小；超大资源需要项目自行提供流式解密器。该能力不替代
Config 的可选数据保护，也不承诺客户端密钥无法被提取。

Config 加密与 Bundle 加密可以叠加：YooAsset 先还原 Bundle，ConfigModule 再还原其中的
配置 `.bytes`，两层保护格式和认证上下文互不冲突。通常只为同一保护目标选择一层即可；项目
同时需要保护其他资源时允许叠加，并应为 Config 与 YooAsset Bundle 使用不同的环境变量、
`KeyId` 和密钥材料，避免一个密钥泄漏同时失去两层保护。

## 更新与缓存

### WebGL 平台差异

WebGL Player 仍选择 `Offline` 或 `Host`，Helper 自动使用 YooAsset `WebPlayModeOptions`：

- `Offline` 使用 `WebServerFileSystem`，从网站的 StreamingAssets/yoo 下加载随构建部署的资源。
- `Host` 使用 `WebNetworkFileSystem`，从配置的主/备用服务器加载清单和资源，不初始化
  `BuiltinFileSystem` 或 `SandboxFileSystem`，也不要求空 Builtin Catalog。
- Web 文件系统按加载请求获取 Bundle，YooAsset 3.0.5 的预下载列表不代表浏览器缺失资源列表；
  更新检查显示无需下载，不代表资源已全部缓存。不要承诺桌面版的预下载进度、磁盘缓存
  配额/LRU 清理或断网回退能力，浏览器缓存由 Unity Web 缓存及浏览器管理。
- 资源必须构建为 WebGL 平台，并通过 HTTP/HTTPS 部署；跨域访问需由服务器允许 CORS。

以下磁盘缓存说明适用于非 WebGL 平台。

`YooAssetResourceHelper` 同时实现以下扩展接口：

- `IResourceUpdateService`：准备并下载全部差量资源。
- `ITaggedResourceUpdateService`：按资源标签准备差量下载。
- `IResourceCacheHelper`：配置缓存上限、自动清理并主动执行清理。

更新接口仅在 Host 模式下具有远程下载语义。缓存上限由 `ResourceComponent` Inspector
统一配置；达到上限后，缓存控制器按最后访问时间清理可删除文件。发布前仍需在目标
平台验证磁盘权限、缓存目录和网络中断恢复行为。

## 场景加载

Helper 加载场景时先检查 Player Build Settings。已加入 Build Settings 的场景由 Unity
`SceneManager` 加载；找不到时再交给 YooAsset。因此项目可以同时使用不进 Bundle 的
内置场景和由 YooAsset 管理的远程场景。

## 内置 Catalog 工具

菜单入口：

`UnityRFramework/Expansion/YooAsset Builtin Catalog`

该工具根据 YooAsset `BundleCollectorSetting` 中的 Package 列表工作，可生成：

- 全部资源远程时所需的空 `BuiltinCatalog.bytes`。
- 部分资源内置时，与实际内置 Bundle 对应的 Catalog。

非 WebGL 的 Host 模式即使没有内置 Bundle，也仍需要有效的空 Builtin Catalog 来初始化 YooAsset
内置文件系统。

## 验收示例

- `Expansion.YooAsset.Demo/Acceptance`：验证 EditorSimulate、Offline、Host、混合内置/远程、缓存与框架重启。
- `Expansion.YooAsset.Demo`：验证启动更新提示、差量下载、进度显示和资源按需静默下载。

本 Sample 只提供资源后端及配套编辑器工具，不包含代码热更新，也不替业务层决定资源
标签、发布目录、更新确认界面或失败重试交互。
