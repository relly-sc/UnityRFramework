# UnityRFramework · Expansion.Security.Windows

`Expansion.Security.Windows` 为 Storage 的安装级 `SaveKey` 提供 Windows 当前用户 DPAPI
保护。它只保护本地存档密钥，不用于 Config、YooAsset Bundle 等需要随发布内容分发的
`ContentKey`。

## 使用方式

1. 在 Package Manager 中导入 `Expansion.Security.Windows`。
2. 在框架入口 `UnityRFramework` Prefab 的 `StorageComponent` 中，将“默认数据保护”设为
   “加密并认证”。
3. 保持“自动管理安装级密钥”开启。
4. 使用默认 `SaveKey`，或在轮换时填写 `SaveKey.版本号`。

Windows Editor 与 Windows Player 会自动发现 `WindowsDpapiKeyStore`。未启用存档加密时
不会创建、读取或保护密钥；未导入本扩展时，Storage 仍可使用核心基础文件密钥仓，不会
产生编译依赖错误。

## 能力边界

- DPAPI 使用当前 Windows 用户范围。同一密钥文件复制到其他用户或其他电脑后不能直接解密。
- Editor 密钥保存到独立的 `EditorKeys` 目录，不与 Player 的 `Keys` 目录共用。
- 核心 `1.13.2` 起，自动管理或 `SetManagedSaveKeyStore` 发现缺失密钥且默认本地存档目录已有主档、备份或临时/回滚文件时，不自动重建密钥；读取现有密文会报告 `KeyUnavailable`。初始化错误可通过 `ManagedSaveKeyStoreError` 查询，组件的 `SaveAsync` / `SaveAutoAsync` 会抛出 `RFrameworkException` 阻止写入，直到恢复原密钥或显式接入项目保护器。
- 该策略保守地检查全部存档文件，包括明文档；首次对已有明文档开启加密，需要显式确认迁移。自定义存档 Helper 无法通过默认目录确认历史数据时，不自动创建密钥。`InstallSaveKeyProvider.EnsureKey` 是显式创建接口，本身不知道是否存在历史存档，不得将它当成丢钥恢复接口。
- 密钥轮换只创建新 `KeyId`，不会自动删除旧密钥。业务确认旧存档迁移完成后再调用
  `InstallSaveKeyProvider.DeleteKey`。
- 轮换应在原密钥仍可用时显式调用 `RotateKey("SaveKey.v2")`，再切换保存 KeyId 并完成迁移；不能仅在 Inspector 改一个缺失的新 KeyId 来绕过历史数据检查。
- DPAPI 只能提高离线提取成本，不能阻止已控制当前用户进程的攻击者在运行时取得明文。

Android Keystore 与 Apple Keychain 不由本扩展实现，后续使用各自独立平台扩展接入
`IKeyStore`。
