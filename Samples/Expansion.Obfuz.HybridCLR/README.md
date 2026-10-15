# Expansion.Obfuz.HybridCLR

Obfuz 与 HybridCLR 的可选联合构建扩展。普通 Player 混淆只需 `Expansion.Obfuz`，无需导入本扩展。

## 安装与配置

1. 打开 `UnityRFramework → 安装扩展与示例`，选择本扩展安装依赖。
2. 工具安装基础 Obfuz、HybridCLR 扩展及固定版本 Obfuz4HybridCLR。当前版本组合为 Obfuz 3.1.0、HybridCLR 8.13.0、Obfuz4HybridCLR 3.0.1，不支持 HybridCLR 9.x。
3. 按官方要求生成 Obfuz 加密虚拟机及密钥，并通过 HybridCLR/Installer 完成本机初始化，再导入源码。
4. Profile 启用 `hybridclr` 与 `obfuz`，分别绑定现有配置资产；混淆程序集清单继续在 Obfuz Settings 维护，须包含需要混淆的热更程序集。

## 构建行为

- `Player`：准备 AOT 基线，必要时联合生成并重建最终 Player。
- `Release`：完成 Player 后，再混淆热更程序集、重新生成桥接与泛型引用，并部署到 HybridCLR 配置的输出目录，供资源步骤收集。若 YooAsset 开启内置拷贝，资源完成后会再次打包最终 Player。
- `HotUpdate`：使用已有有效 AOT 基线执行热更混淆，不构建 Player。
- 联合步骤自动纳入计划，无需创建额外 Profile 条目。未启用 `hybridclr` 时不执行联合流程。

从旧版 `Expansion.Obfuz` 升级的热更项目需额外导入本扩展；请先备份导入目录中的项目改动，再使用安装工具重新导入，避免新旧步骤重复编译。

本扩展不包含业务 Demo、不创建 asmdef；映射归档与普通 Player 混淆检查由基础扩展提供。
