# Expansion.Obfuz

可选的 Obfuz 构建扩展。它为 UnityRFramework 构建工具提供 Obfuz 步骤，并通过
Obfuz4HybridCLR 处理 HybridCLR 热更新程序集。核心框架、普通 Sample 和未启用 Obfuz
步骤的构建不依赖本扩展。

## 依赖

- `Expansion.HybridCLR`
- Obfuz
- Obfuz4HybridCLR

依赖均需由使用者按官方文档手动安装、生成加密虚拟机及密钥，并完成运行时初始化。

## 设置

1. 打开 `Obfuz/Settings...`。
2. 在 `Assembly Settings/Assemblies To Obfuscate` 中加入需要混淆的程序集。热更新闭环
   至少应包含 HybridCLR Settings 中配置的热更新程序集。
3. 如果同时混淆 `Assembly-CSharp` 并启用 `Call Obfus`，在
   `Call Obfus Settings/Rule Files` 中加入本 Sample 的
   `UnityRFrameworkCallObfuscation.xml`。该规则按 Obfuz 官方建议排除对
   `UnityEngine.*` API 的调用混淆，避免 Unity 原生泛型 API 被代理改写后返回异常结果。
4. 在 UnityRFramework 构建 Profile 中启用 `hybridclr` 与 `obfuz` 步骤，使用
   `Release` 完成 Player、AOT 基线、热更新程序集混淆和 YooAsset 资源发布闭环。

Obfuz Settings 是混淆参数的唯一事实源；构建工具只校验并执行，不会自动改写这些设置。

参考官方文档：

- https://www.obfuz.com/docs/manual/configuration
- https://www.obfuz.com/docs/manual/call-obfuscation
- https://www.obfuz.com/docs/manual/hybridclr
