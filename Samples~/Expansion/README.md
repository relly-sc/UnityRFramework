# UnityRFramework · Expansion Sample

可选第三方集成示例。**核心框架不依赖任何第三方包**，本 Sample 提供对接主流插件的 Helper 实现，按需导入。

## 当前包含

| 文件 | 对接插件 | 说明 |
|------|----------|------|
| `Scripts/Resource/YooAssetResourceHelper.cs` | YooAsset (`com.tuyoogame.yooasset`) | 基于 YooAsset v3 的资源辅助器，支持 EditorSimulate / Offline / Host 三种模式 |
| `Scripts/WebRequest/UniTaskWebRequestHelper.cs` | UniTask (`com.cysharp.unitask`) | 基于 UniTask 的 Web 请求辅助器 |

## 使用方式

1. 在 Package Manager 中选中 UnityRFramework → Samples → 点击 **Expansion** 的 Import。
2. 由于 UPM 无法为 Sample 自动解析依赖，请**手动**在项目中安装被引用的包：
   - YooAsset：`https://github.com/tuyoogame/YooAsset` （或 openupm `com.tuyoogame.yooasset`）
   - UniTask：`https://github.com/Cysharp/UniTask`
3. 在 `UnityRFramework` 预制体的 Inspector 中，将 `Resource Helper Type Name` 改为
   `UnityRFramework.Expansion.YooAssetResourceHelper`，`Web Request Helper Type Name` 改为对应类型。

## 计划接入（待补）

- [ ] **Luban** 配置辅助器（`Config` 模块）：对接 Luban 导出的二进制/JSON 配置
- [ ] **HybridCLR** 热更新辅助器：提供 AOT 泛型实例补充与热更新 dll 加载入口
- [ ] 其他第三方 Helper（按社区需求逐步补充）

> 注意：开发期本目录命名为 `Samples/`（无 `~`），Unity 会随宿主工程自动编译，便于本地调试；发布前统一改名为 `Samples~/`，经 Package Manager 导入后才会进入 `Assets/Samples/` 参与编译。`package.json` 的 `samples` 字段已按发布态声明三个 Sample（Expansion / Demo / ExpansionDemo）。

## 三个 Sample 的关系

```
Samples/
├── Expansion/         ← 纯代码：第三方 Helper 实现（无场景，不单独运行）← 本目录
├── Demo/              ← 完整 Demo：内置 Helper，零第三方
└── ExpansionDemo/     ← 完整 Demo：复用本目录的 Helper，对接第三方，与 Demo 对照
```

若需可运行的第三方集成示例，请导入 **ExpansionDemo Sample**——它复制了 Demo 的骨架并用本目录的 YooAsset/UniTask Helper 替换内置默认。
