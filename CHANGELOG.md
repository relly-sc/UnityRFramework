# 变更日志

本文件记录 UnityRFramework 面向使用者的重要变更。版本号遵循 [Semantic Versioning 2.0.0](https://semver.org/lang/zh-CN/)，内容结构参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)。

版本号按以下规则递增，并同步更新核心包、Samples 包及其依赖版本：

- 修复 Bug、小问题或进行兼容性调整：修订号加一，例如 `1.4.0` → `1.4.1`。
- 新增向后兼容的功能：次版本号加一，例如 `1.4.1` → `1.5.0`。
- 重构、重写、架构调整或其他不兼容的大改动：主版本号加一，例如 `1.5.0` → `2.0.0`。
- 每次版本发布都必须在本文件记录新增、变更和修复内容。

## 未发布

## 1.4.4 - 2026-10-09

### 变更

- 编辑器菜单统一归类：CSV 与 Excel 放入配置表工具，第三方扩展及其 Demo 放入 Expansion，普通 Demo 放入 Samples。

### 修复

- 补齐 YooAsset Bundle 加密的默认密钥文件生成与 Player 自动注册流程，避免 Builder 能加密但 Player 未配置解密密钥。
- Config 和 YooAsset 密钥工具区分首次生成与主动更换，首次生成保留已有密钥，避免小版本更新误使旧资源失效。

## 1.4.3 - 2026-10-08

### 变更

- 新建 Build Profile 的 Player 文件名模板简化为 `{ProductName}`，避免默认文件名重复包含平台、版本和构建号而过长。

## 1.4.2 - 2026-10-08

### 修复

- 修复构建工具新增场景条目时默认未启用，导致已选择场景仍无法通过构建校验的问题。

## 1.4.1 - 2026-10-08

### 新增

- 将核心包与 Samples 拆分为两个独立 UPM 发布产物；Samples 包显式依赖同版本核心包。
- 增加 GitHub Actions 发布流程，由 `main` 自动生成核心 `upm` 分支并同步 Samples 发布仓库。
- 增加项目级构建窗口状态隔离，避免不同 Unity 项目共享“最近构建”等 EditorPrefs 数据。
- 补全 RFramework 纯 C# 核心库 README，并增加独立变更日志。

### 变更

- 新建 Build Profile 时默认读取当前项目的平台、公司名称、产品名称和应用标识。
- `Sample.Demo` 根据当前 Config Helper 自动选择 JSON 或框架二进制配置路径。
- `Sample.Demo` 默认恢复为未加密 JSON 配置，附带的二进制配置恢复为可直接读取的标准 URFC 产物。

### 修复

- 修复 UPM 发布包缺少必要 `.meta` 文件导致不可变 Package 目录资源被 Unity 忽略的问题。
- 修复 Sample 升级后 HybridCLR 官方 Demo 输出路径仍指向旧版本目录的问题。
- 修复 `Sample.Demo` 导出器沿用旧生成命名空间，重新导出后会改变配置类型的问题。
- 修复部分 Unity 版本中运行时错误面板使用不可用内置字体的问题。
- 构建 Profile 允许沿用 Unity 新项目默认的 `DefaultCompany` 公司名称。

## 1.4.0 - 2026-10-08

### 新增

- 增加 Storage 核心模块及 Runtime Component，支持多槽位、原子写入、备份恢复、版本迁移、可选压缩和加密认证。
- 增加 Config 可选加密认证、`ConfigKey` 文件和构建期正式发布配置泄漏检查。
- 增加 `DefaultDataProtector`、`IKeyProvider`、`IKeyStore` 与安装级存档密钥管理边界。
- 增加 Windows DPAPI、Android Keystore、iOS Keychain 和 macOS Keychain 可选平台密钥仓扩展。
- 增加 `ProtectedInt`、`ProtectedLong`、`ProtectedFloat` 与内存篡改事件，用于基础数值防护。
- 增加 `Sample.Storage` 和 `Sample.Security` 验收内容。
- 增加 YooAsset Bundle/Manifest 可选保护实现，并保持其与 Config 加密相互独立。
- 构建工具路径字段支持从 Project 窗口拖入文件或文件夹，同时保留手动输入和选择按钮。

### 变更

- Config 构建步骤可选择框架 CSV 或 ExcelDataReader 扩展导出器，并统一 JSON、框架二进制和保护配置。
- UI Component 在未配置全局 Canvas 和 Layer Roots 时可按默认参数自动创建，兼容旧场景和旧 Demo。
- HybridCLR 与 Obfuz 构建步骤完善首次构建、AOT 基线复用和热更新程序集处理。
- 构建发布保留 Config 与 Localization JSON 文件，不再因 Release 清理误删开发或多语言数据。

### 修复

- 修复 macOS Player 未正确启用安装级密钥自动管理的问题。
- 修复 Config 内容格式、Helper 和加密认证路径不一致时产生的加载问题。
- 修复 Android 存档验收界面连续操作和移动端布局问题。

## 历史版本

| 版本 | 日期 | 说明 |
| --- | --- | --- |
| 1.3.0 | 2026-09-09 | 早期功能发布；当时尚未维护独立变更日志，完整差异请查看 Git Tag。 |
| 1.2.0 | 2026-08-12 | YooAsset Demo 构建读取各 Package 的 Builder 设置，并使用短 Bundle 内部名称规避 UPM Sample 路径过长。 |
| 1.1.0 | 2026-08-05 | 早期功能发布；完整差异请查看 Git Tag。 |
| 1.0.0 | 2026-07-29 | 首个公开版本。 |

[未发布]: https://github.com/relly-sc/UnityRFramework/compare/1.4.4...HEAD
[1.4.4]: https://github.com/relly-sc/UnityRFramework/compare/1.4.3...1.4.4
[1.4.3]: https://github.com/relly-sc/UnityRFramework/compare/1.4.2...1.4.3
[1.4.2]: https://github.com/relly-sc/UnityRFramework/compare/1.4.1...1.4.2
[1.4.1]: https://github.com/relly-sc/UnityRFramework/compare/1.4.0...1.4.1
[1.4.0]: https://github.com/relly-sc/UnityRFramework/releases/tag/1.4.0
