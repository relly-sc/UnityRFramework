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
- 删除密钥后，旧加密存档会明确报告密钥不可用；框架不会自动生成同名密钥来覆盖该状态。
- 密钥轮换只创建新 `KeyId`，不会自动删除旧密钥。业务确认旧存档迁移完成后再调用
  `InstallSaveKeyProvider.DeleteKey`。
- DPAPI 只能提高离线提取成本，不能阻止已控制当前用户进程的攻击者在运行时取得明文。

Android Keystore 与 Apple Keychain 不由本扩展实现，后续使用各自独立平台扩展接入
`IKeyStore`。
