# UnityRFramework · Expansion.Security.Android

`Expansion.Security.Android` 使用 Android Keystore 中不可导出的 AES-GCM 包装密钥保护
Storage 的安装级 `SaveKey`。它不用于 Config、YooAsset Bundle 等随发布内容分发的
`ContentKey`。

## 使用方式

1. 在 Package Manager 中导入 `Expansion.Security.Android`。
2. 将 Player 的 Android 最低 API Level 设为 23 或更高。
3. 在框架入口 Prefab 的 `Storage` 子物体中，将“默认数据保护”设为“加密并认证”。
4. 保持“自动管理安装级密钥”开启。

Android Player 会自动注册 `AndroidKeystoreKeyStore`。Editor 不模拟 Android Keystore，
仍使用独立 `EditorKeys`；未启用存档加密时不会创建或访问密钥。

## 能力边界

- 包装密钥由 Android Keystore 保存且不可导出，实际 `SaveKey` 以 AES-GCM 密文落盘。
- 应用更新通常保留密钥；卸载应用会删除 Keystore 条目。恢复旧存档或把文件复制到其他设备
  后，因包装密钥不存在，旧存档不可解密。
- 当前包装密钥不要求用户认证，不受锁屏解锁状态限制；如项目要求生物识别或锁屏认证，需
  自行提供更严格的 `IKeyStore`。
- 自动备份不应迁移该扩展的密钥密文目录；即使迁移，目标设备也没有对应 Keystore 密钥。
- Android Keystore 只能提高离线提取成本，不能阻止已控制进程在运行时取得明文。

该扩展需要在 Android 真机 Player 上完成保存、退出、重启、覆盖安装和卸载重装验收；Editor
编译不能替代平台验收。
