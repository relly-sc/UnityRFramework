# UnityRFramework · Expansion.Security.macOS

`Expansion.Security.macOS` 使用 macOS 数据保护 Keychain 保存 Storage 的安装级 `SaveKey`。
它不用于 Config、YooAsset Bundle 等随发布内容分发的 `ContentKey`。

## 使用方式

1. 在 Package Manager 中导入 `Expansion.Security.macOS`。
2. 目标系统为 macOS 10.15 或更高，Player 使用 IL2CPP 后端；Mono Player 不启用本扩展。
3. 在框架入口 Prefab 的 `Storage` 子物体中，将“默认数据保护”设为“加密并认证”。
4. 保持“自动管理安装级密钥”开启，然后构建并签名 `.app`。

macOS IL2CPP Player 会自动注册 `MacOsKeychainKeyStore`。Objective-C++ 源码由 Unity 随
IL2CPP 生成代码一起编译，不需要维护 Intel 与 Apple Silicon 两套预编译库。Editor 仍使用
独立 `EditorKeys`；未启用存档加密时不会访问 Keychain。

## 能力边界

- 所有查询均指定 `kSecUseDataProtectionKeychain`，不使用旧式文件 Keychain。
- Keychain 项使用 `AfterFirstUnlockThisDeviceOnly`，不会通过备份迁移到其他设备。
- 应用升级和覆盖安装通常保留 Keychain 项；卸载、重新签名、更换 Bundle Identifier 或
  Keychain Access Group 后的行为不能作为稳定恢复协议依赖。
- 从其他 Mac 复制来的存档无法使用本机安装级 `SaveKey` 解密。跨设备存档应使用项目账号
  体系管理的独立密钥。
- Mono Player 不注册本扩展，将使用核心基础文件密钥仓。正式 macOS 发布建议使用 IL2CPP。
- Keychain 只能提高离线提取成本，不能阻止已控制进程在运行时取得明文。

需要在 Intel 或 Apple Silicon Mac 的实际 Player 中验证保存、退出、重启、覆盖安装、改签名、
改 Bundle Identifier 和复制存档。至少验收项目实际支持的 CPU 架构。
