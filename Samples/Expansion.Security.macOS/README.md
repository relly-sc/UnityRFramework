# UnityRFramework · Expansion.Security.macOS

`Expansion.Security.macOS` 优先使用 macOS 数据保护 Keychain 保存 Storage 的安装级 `SaveKey`。
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

- 正常情况下，读写和删除均优先指定 `kSecUseDataProtectionKeychain`，密钥项使用
  `AfterFirstUnlockThisDeviceOnly`，不会通过备份迁移到其他设备。
- 为兼容部分 macOS Player 的签名、权限或系统环境，当数据保护 Keychain 返回
  `errSecMissingEntitlement` 或 `errSecUnimplemented` 时，扩展会回退到旧式文件 Keychain。
  读取时还会在数据保护 Keychain 返回 `errSecItemNotFound` 时查询旧式 Keychain，
  以兼容已经保存到旧式仓的密钥。
- 因此，旧式文件 Keychain 不是完全不会访问的路径：在上述回退条件下，写入也可能创建
  旧式 Keychain 项。旧式路径不保证具备数据保护 Keychain 的同等保护属性，不应把两者视为
  相同的安全级别。
- 删除密钥时会清理数据保护 Keychain，并在允许的回退结果下继续清理旧式 Keychain 的同名项。
- 只有数据保护 Keychain 路径成功时，才能按本节的 `AfterFirstUnlockThisDeviceOnly` 规则描述
  该密钥项；如果发生旧式回退，应以当前 macOS 的文件 Keychain 生命周期和访问控制为准。
- 应用升级和覆盖安装通常保留 Keychain 项；卸载、重新签名、更换 Bundle Identifier 或
  Keychain Access Group 后的行为不能作为稳定恢复协议依赖。
- 从其他 Mac 复制来的存档无法使用本机安装级 `SaveKey` 解密。跨设备存档应使用项目账号
  体系管理的独立密钥。
- Mono Player 不注册本扩展，将使用核心基础文件密钥仓。正式 macOS 发布建议使用 IL2CPP。
- Keychain 只能提高离线提取成本，不能阻止已控制进程在运行时取得明文。

需要在 Intel 或 Apple Silicon Mac 的实际 Player 中验证保存、退出、重启、覆盖安装、改签名、
改 Bundle Identifier、复制存档，以及在缺少数据保护 Keychain entitlement 或系统不支持时的
回退行为。至少验收项目实际支持的 CPU 架构；不能仅凭 Editor 中的 Keychain 名称判断 Player
实际使用的是哪一种 Keychain。
