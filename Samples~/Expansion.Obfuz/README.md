# Expansion.Obfuz

可选的 Obfuz 构建扩展。它为 UnityRFramework 构建工具提供 Obfuz 步骤，并通过
Obfuz4HybridCLR 处理 HybridCLR 热更新程序集。核心框架、普通 Sample 和未启用 Obfuz
步骤的构建不依赖本扩展。

## 依赖

- `Expansion.HybridCLR`
- Obfuz
- Obfuz4HybridCLR

当前验收版本组合：Unity `2022.3.49f1c1`、HybridCLR `8.13.0`、Obfuz `3.1.0`、
Obfuz4HybridCLR `3.0.1`。其他版本需要重新验证联合生成入口和运行时兼容性。

依赖均需由使用者按官方文档手动安装、生成加密虚拟机及密钥，并完成运行时初始化。

## 设置

1. 打开 `Obfuz/Settings...`。
2. 在 `Assembly Settings/Assemblies To Obfuscate` 中加入需要混淆的程序集。热更新闭环
   至少应包含 HybridCLR Settings 中配置的热更新程序集。
3. 在 UnityRFramework 构建 Profile 中启用 `hybridclr` 与 `obfuz` 步骤，使用
   `Release` 完成 Player、AOT 基线、热更新程序集混淆和 YooAsset 资源发布闭环。

Obfuz Settings 是混淆参数的唯一事实源；构建工具只校验并执行，不会自动改写这些设置。

每次 Obfuz 热更混淆或完整发布成功后，构建工具会把当前符号映射复制到：
`Bundles/ObfuzMappings/{目标平台}/{公共版本-构建号}/{时间}/symbol-mapping.xml`。
该目录用于异常堆栈还原和版本追溯，不会进入 Player 或 UPM 包。发生混淆相关异常时，
使用与 Player、热更新程序集完全相同版本的映射文件，再按 Obfuz 官方工具进行还原；
不要用其他构建批次的映射文件替换。

参考官方文档：

- https://www.obfuz.com/docs/manual/configuration
- https://www.obfuz.com/docs/manual/call-obfuscation
- https://www.obfuz.com/docs/manual/hybridclr
