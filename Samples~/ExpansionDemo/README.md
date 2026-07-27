# UnityRFramework · ExpansionDemo

`ExpansionDemo` 是第三方 Helper 的最小端到端验收场景，不复制正式 Demo 的业务
玩法。当前覆盖 YooAsset 3.0.3-beta 与 UniTask WebRequest Helper。

## 前置依赖

- `com.tuyoogame.yooasset` 3.0.3-beta
- `com.cysharp.unitask`
- `Samples/Expansion` 中的第三方 Helper

MemoryPack、NPOI 和 EPPlus 当前均不是本示例依赖。

## 生成验收资产

在 Unity 菜单执行：

`UnityRFramework/ExpansionDemo/Rebuild Acceptance Assets`

构建器会在编辑器中完成以下工作：

1. 生成二进制 `TextAsset` 测试文件和 Additive 内容场景。
2. 复制框架预制体，并配置 `YooAssetResourceHelper` 与
   `UniTaskWebRequestHelper`。
3. 生成序列化 UGUI 启动场景；运行时代码只更新文本和绑定事件，不控制布局。
4. 创建 `ExpansionDemoPackage` 收集规则。
5. 将启动场景设为 Build Settings 第 0 项，以便验证框架软重启。

## 自动验收流程

打开并运行：

`GameAssets/Scenes/ExpansionDemo.unity`

默认会自动执行：

1. 初始化 YooAsset EditorSimulate 资源包并激活包清单。
2. 以 `byte[]` 加载、校验和卸载普通 Bundle 内的二进制 `TextAsset`。
3. 加载并卸载 `ExpansionContent` Additive 场景。
4. 通过 UniTask Helper 请求本机 UnitySkills `/health`。
5. 对一个在飞 Web 请求主动取消，并校验结果为 `WebRequestError.Aborted`。
6. 请求框架软重启，等待旧 YooAsset 包异步销毁后重新初始化。
7. 重启后再次执行上述全部链路。

最终状态区应出现：

`PASS：框架重启后全部第三方链路再次通过。`

## 2026-07-27 验收结果

- Unity C# 编译错误：0。
- 首次启动、资源、场景、Web 成功请求与取消：通过。
- Restart 关闭、重新启动和第二轮完整链路：通过。
- Play Mode 运行捕获新增未处理错误：0。
- Quit 关闭流程：通过。

本轮只验证 EditorSimulate。Offline 需要先生成并部署内置 YooAsset 包；Host 需要
准备远端清单、资源服务器及主/备用地址，完成后应分别补做目标平台验收。

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
        └── Scenes/ExpansionContent.unity
```

`Raw` 是示例目录的历史命名；当前文件按普通 `TextAsset` 收集，不是 YooAsset
RawFile 包。
