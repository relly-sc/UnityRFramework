# Expansion.Obfuz

可选的普通 Player 代码混淆构建扩展。只依赖 Obfuz，不依赖 HybridCLR、Obfuz4HybridCLR 或 YooAsset。
YooAsset 的 Offline/Host 模式与代码混淆无关，按项目需要单独配置。

## 安装与准备

1. 打开 `UnityRFramework → 安装扩展与示例`，选择本扩展，先安装依赖。
2. 当前固定 Obfuz 版本为 `3.1.0`。按官方流程生成 `GeneratedEncryptionVirtualMachine` 与密钥，再导入扩展源码。
3. 静态密钥须位于 `Assets/Resources/Obfuz/defaultStaticSecretKey.bytes`，由扩展的运行时初始化器读取。
4. 在 Obfuz Settings 配置混淆程序集和引用关系，启用构建混淆开关。反射、序列化和字符串入口按业务需要排除。
5. 在框架 Profile 中启用 `obfuz` 并绑定配置资产，使用 `Player` 或 `Release` 构建；无需启用 `hybridclr`。

Obfuz Settings 是混淆参数的唯一事实源。框架不会复制程序集清单、密钥及输出参数，也不会替用户永久修改插件设置。
Profile 未启用 `obfuz` 时仍会临时关闭其构建回调，并在任务结束恢复原值，避免其他构建意外混淆。

## 构建与映射

普通 Player 在 Unity 构建期间由 Obfuz 官方回调执行混淆。框架检查本次回调是否成功；
未收到成功结果或混淆失败时构建任务失败，不把原生 Player 构建成功等同于混淆成功。

成功后映射归档到：

`Bundles/ObfuzMappings/{目标平台}/{公共版本-构建号}/{时间}/symbol-mapping.xml`

该目录不进入 Player 或 UPM 包。还原异常堆栈应使用对应构建批次的映射，不能用其他版本替换。

## HybridCLR 联合混淆

需要热更新时额外导入 `Expansion.Obfuz.HybridCLR`，由其处理 Obfuz4HybridCLR 联合准备、AOT 基线、热更程序集混淆及最终 Player 重建。
Profile 仍启用 `hybridclr` 与 `obfuz`，不增加额外业务步骤。普通 Player 无需安装这部分依赖。

旧版热更项目升级后需通过安装工具重新导入基础扩展和联合扩展，先备份原导入目录中的业务改动，避免新旧步骤重复编译。

## 官方参考

- https://www.obfuz.com/docs/manual/configuration
- https://www.obfuz.com/docs/manual/call-obfuscation
- https://www.obfuz.com/docs/manual/hybridclr
