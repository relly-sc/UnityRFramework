# UnityRFramework · Expansion.Security.iOS

`Expansion.Security.iOS` 使用系统 Keychain 保存 Storage 的安装级 `SaveKey`。它不用于
Config、YooAsset Bundle 等随发布内容分发的 `ContentKey`。

## 使用方式

1. 在 Package Manager 中导入 `Expansion.Security.iOS`。
2. 在框架入口 Prefab 的 `Storage` 子物体中，将“默认数据保护”设为“加密并认证”。
3. 保持“自动管理安装级密钥”开启。
4. 正常构建 iOS Xcode 工程；扩展中的 Objective-C++ 源文件会随工程编译。

iOS Player 会自动注册 `IosKeychainKeyStore`。Editor 仍使用独立 `EditorKeys`；未启用存档
加密时不会创建或访问 Keychain 项。

## 能力边界

- Keychain 项使用 `AfterFirstUnlockThisDeviceOnly`：设备重启后首次解锁前不可用，首次解锁后
  可供应用访问，并且不会迁移到其他设备。
- Keychain 项通常在应用升级后保留；系统卸载应用后是否保留不能作为产品恢复协议依赖。
  需要“卸载即清除”的项目应额外保存安装标识并在首次启动时清理旧项。
- 存档文件通过备份或手工复制到其他设备后不可解密。跨设备存档应改用项目账号体系管理的
  独立密钥，而不是安装级 `SaveKey`。
- Keychain 只能提高离线提取成本，不能阻止已控制进程在运行时取得明文。

该扩展需要在 iOS 真机 Player 上完成保存、退出、重启、设备重启、覆盖安装和卸载重装验收；
Editor 与模拟器结果不能替代真机边界确认。macOS Player 不使用本扩展。
