---
name: unityrframework
description: 在已安装 UnityRFramework 的 Unity 项目中开发、接入或排查框架功能时使用；帮助 AI 选择核心模块、构建工具或可选 Expansion，并依据当前安装版本核对 API。普通 Unity 项目或框架本身的发布维护不适用。
---

# UnityRFramework 项目开发

本技能供 AI 在**使用框架的项目**中工作，不是另一份固定版本的 API 手册。先确认项目实际安装的包和版本，再查该版本的 README 与源码；不要把开发仓库、Samples 和第三方插件都当作消费项目的必备部分。

## 找到当前框架

1. 查看 `Packages/manifest.json`、`Packages/packages-lock.json`，确认 `com.relly-sc.unityrframework` 和可选的 `com.relly-sc.unityrframework.samples`。若项目直接嵌入源码，也检查 `Assets/UnityRFramework/Package.json`。
2. 在 `Packages/com.relly-sc.unityrframework/`、`Library/PackageCache/com.relly-sc.unityrframework@*/` 或 `Assets/UnityRFramework/` 中定位当前版本的 `README.md`、`Scripts/Runtime/` 和 `Library/RFramework/RFramework/`。根据实际文件位置选一个，不要写死 Sample 版本目录。
3. 需要可选功能时，检查项目是否安装对应第三方包，以及是否已导入相应 Sample。Sample 的说明可能在 `Packages/com.relly-sc.unityrframework.samples/Samples~/`，也可能在 `Assets/Samples/` 的实际导入目录。
4. 找不到框架时先说明安装要求，不要生成依赖不存在类型的业务代码。

## 选用能力

- 普通应用设置、存档、配置表、本地化、UI、资源、网络等核心模块：读 [核心模块参考](references/核心模块.md)，再读核心包 README 的对应章节和接口源码。
- 广告接入：先读核心模块参考中的“广告模块当前能力”，核对项目实际使用的 Helper；当前框架仅提供统一接口和空实现，具体平台 SDK 适配需另外实现。
- 构建 Player、资源、热更新或排查构建 Profile：读 [构建参考](references/构建工具.md)，并核对当前 `Scripts/Editor/BuildPipeline/`。
- YooAsset、HybridCLR、Obfuz、Excel、UI 扩展及其他第三方能力：读 [可选扩展参考](references/可选扩展.md) 和该 Sample 的 README。未安装扩展时不要假定相关 API 存在。

## 实施约束

- 业务代码优先使用 `UnityRFramework.Runtime.GameEntry` 的模块入口；未提供专用属性的组件可用 `GameEntry.Get<T>()` 查询。Library 的 `RFramework` 接口用于扩展和测试。框架入口 Prefab 负责 Component/Helper，不要在业务脚本中另建一套模块实例。
- 在消费项目的 `Assets/` 下创建业务脚本、配置和场景。Git/UPM 安装的 `Packages/` 或 `Library/PackageCache/` 通常不可写；不要在其中保存业务改动。
- Helper 决定资源地址、加载来源和平台行为。写资源路径前先确认当前 `ResourceComponent` 的 Helper、YooAsset Package/Address 或 Resources 目录，不能把一种 Helper 的路径规则套到另一种上。
- Inspector 中的可选项、构建步骤、密钥文件和第三方插件必须按当前项目实际配置解释。不要擅自切换 Helper、启用加密、生成或更换密钥、安装第三方依赖。
- 给出可编译的用法时，核对方法签名、命名空间、异步返回值和对应的 `Unload`/关闭操作。对平台或 Player 行为，只报告本轮实际验证过的结果。
