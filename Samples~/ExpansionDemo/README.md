# UnityRFramework · ExpansionDemo Sample（计划清单）

使用 **Expansion 第三方辅助器**（YooAsset、UniTask 等）的完整可运行 Demo。
与 Demo（内置 Helper 版）形成对照：同样串通全部框架模块，但底层走第三方链。

> 本 Sample 按阶段推进，不一次性写完。本文档即进度清单，每完成一项勾掉一行。
> **前置条件**：必须先 Import Expansion Sample 并安装对应第三方包（YooAsset、UniTask 等），详见 `Samples/Expansion/README.md`。

---

## 设计原则

- **对接第三方**：Resource → YooAssetHelper，WebRequest → UniTaskWebRequestHelper，后续扩展 Luban/HybridCLR。
- **与 Demo 共享骨架**：复用 Demo 的 `DemoGameEntry` / `DemoProcedure` 架构，区别仅在 prefab 的 `xxxHelperTypeName` 配置项。
- **自包含**：ExpansionDemo 的资源、场景、脚本全部在本目录内，不污染核心包和 Demo。
- **可跳过**：开发者若只用内置 Helper，可以不 Import 本 Sample。

---

## 阶段一：前置准备

> 目标：确认 Expansion + 第三方环境可用。

- [ ] **安装第三方包**
  - [ ] Import Expansion Sample
  - [ ] 安装 YooAsset（如 `com.tuyoogame.yooasset`）
  - [ ] 安装 UniTask（如 `com.cysharp.unitask`）
  - [ ] 编译通过，确认 YooAsset / UniTask asmdef 可被引用
- [ ] **验证 Helper 可用**
  - [ ] YooAsset 包初始化成功（Editor Simulation 模式）
  - [ ] UniTask WebRequest 能发起网络请求

---

## 阶段二：ExpansionDemo 骨架

> 目标：比照 Demo 骨架，配好第三方 Helper 后框架能启动。

- [ ] **目录结构**
  - [ ] `Samples/ExpansionDemo/Scripts/` **不建 `.asmdef`**：随宿主编入 `Assembly-CSharp`
  - [ ] `Samples/ExpansionDemo/Scenes/ExpansionDemo.unity`
  - [ ] `Samples/ExpansionDemo/Prefabs/`
- [ ] **ExpansionDemoGameEntry.cs**
  - [ ] 复用 DemoGameEntry 引导逻辑，加载 UnityRFramework 预制体
- [ ] **UnityRFramework 预制体（本 Sample 专用副本）**
  - [ ] `resourceHelperTypeName` → `UnityRFramework.Expansion.YooAssetResourceHelper`
  - [ ] `webRequestHelperTypeName` → `UnityRFramework.Expansion.UniTaskWebRequestHelper`
  - [ ] 其余 Helper 保持默认（核心内置）
- [ ] **ExpansionDemoProcedure 流程**
  - [ ] `ExpansionLaunchProcedure` → `ExpansionMenuProcedure` → `ExpansionGameProcedure`
  - [ ] 状态切换日志验证流程通

---

## 阶段三：逐模块演示（跟 Demo 对照）

> 目标：跟 Demo 同样的模块清单，但走第三方 Helper 路径，证明"切换一个配置就换一套底层实现"。

| 模块 | Demo（内置 Helper） | ExpansionDemo（第三方 Helper） |
|------|---------------------|-------------------------------|
| Resource | `DefaultResourceHelper`（Resources） | `YooAssetResourceHelper`（YooAsset） |
| WebRequest | `DefaultWebRequestHelper`（HttpClient） | `UniTaskWebRequestHelper`（UniTask） |
| Config | `DefaultConfigHelper`（JSON） | 待接入 Luban（后续） |
| 其余 13 模块 | 内置 Helper | 与 Demo 完全相同 |

- [ ] **Resource 演示**：用 YooAsset 加载一个测试资源并显示
- [ ] **WebRequest 演示**：用 UniTask 发起一次网络请求
- [ ] **其余模块**：直接复用 Demo 的演示组件（所有模块接口一致，Helper 换掉对上层透明）
- [ ] **验证**：与 Demo 同样的操作流程，底层 Helper 不同但行为一致

---

## 阶段四：内置资源

- [ ] **YooAsset 测试资源**：一个最小 AssetBundle 测试包（或 Editor Simulation 模式下的虚拟资源）
- [ ] **UniTask 演示场景**：一个 `async void` / `UniTask` 风格调用的展示脚本
- [ ] **验证**：单独导入 ExpansionDemo 后场景可运行（前提：Expansion + 第三方包已安装）

---

## 与 Demo / Expansion 的关系

```
Samples/
├── Expansion/         ← 纯代码：第三方 Helper 实现（YooAsset、UniTask 等）
├── Demo/              ← 完整 Demo：内置 Helper，零第三方
└── ExpansionDemo/     ← 完整 Demo：复用 Expansion Helper，对接第三方 ← 本目录
```

- **ExpansionDemo 依赖 Expansion**：必须先 Import Expansion + 安装第三方包。
- **ExpansionDemo 与 Demo 骨架相通**：同一套 Procedure/GameEntry 模式，不同 prefab 配置。
- **Expansion 自己不跑**：它是纯 Helper 代码，不包含场景/流程。

---

## 进度总览

| 阶段 | 内容 | 状态 |
|------|------|:--:|
| 一 · 前置准备 | 安装第三方包 + 验证 | ⬜ 待开始 |
| 二 · 骨架 | ExpansionDemo 场景 + prefab 副本 + Procedure | ⬜ 待开始 |
| 三 · 逐模块演示 | Resource/WebRequest 走第三方，其余复用 Demo | ⬜ 待开始 |
| 四 · 内置资源 | YooAsset 测试资源 | ⬜ 待开始 |

> 下一步：等 Demo 完成第一阶段后，复制骨架改造为 ExpansionDemo（改 prefab 的 HelperTypeName 配置即可）。
