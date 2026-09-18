# Sample.Storage

Storage 存档模块的独立轻量验收 Sample，仅依赖 UnityRFramework 核心。

## 使用方法

1. 在 Package Manager 中导入 `Sample.Storage`。
2. 打开 `GameAssets/Scenes/StorageAcceptance.unity`。
3. 运行场景，通过中文界面测试普通保存、读取、删除、多槽位、GZip 压缩、加密认证、备份恢复和密钥丢失。
4. 验证软重启或构建 Player 前，将该场景添加到 Build Settings，并放在第 0 个启用位置。
5. 保存后执行“软重启框架”或完整退出并重新启动 Player，再读取相同槽位。

框架软重启会重载 Build Settings 的 0 号启动场景。Sample 不会自动修改项目的场景列表；
位置不正确时，“软重启框架”按钮会显示提示，不会跳转到其他 Demo。

场景使用独立目录 `Application.persistentDataPath/SampleStorage` 和专用密钥 `SaveKey.SampleStorage`，不会操作其他 Demo 的存档。

## 备份规则

- 首次保存某个槽位时没有旧文件，因此不会产生备份。
- 再次保存同一槽位且 `CreateBackup` 开启时，覆盖前的主存档成为 `.save.bak`。
- 备份只保留上一份，不是无限历史版本。
- 删除槽位会同时删除主存档、备份和临时文件。
- 读取时的加密和压缩选项必须与主存档一致；选项不匹配会明确报错，不会回退到旧配置的备份。
- “验证损坏后恢复”会读取当前槽位已有数据，覆盖一次以生成备份，再损坏主存档；恢复结果应与执行按钮前保存的数据一致。

## 平台密钥仓

- 未导入平台安全扩展时使用核心 `FileKeyStore`。
- Windows 导入 `Expansion.Security.Windows` 后自动使用当前用户 DPAPI。
- Android 导入 `Expansion.Security.Android` 后在 Android Player 使用 Android Keystore。
- iOS 导入 `Expansion.Security.iOS` 后在 iOS Player 使用 Keychain。
- macOS 导入 `Expansion.Security.macOS` 后在 macOS IL2CPP Player 使用数据保护 Keychain。

界面顶部会显示当前实际创建的密钥仓类型。删除测试密钥后，旧加密存档应报告“密钥不可用”；重建的是新密钥，不能解密旧数据，需要重新保存。

若需要重新生成场景，执行 `UnityRFramework/Samples/生成存档验收场景`。
