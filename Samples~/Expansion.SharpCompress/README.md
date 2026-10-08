# Expansion.SharpCompress

为 `DownloadModule` 提供基于 SharpCompress 0.50.1 的多格式解压辅助器。

## 支持格式

- ZIP
- RAR
- 7z
- TAR
- GZip
- BZip2

该扩展只负责解压，不负责创建压缩包。RAR 和 7z 的部分特殊压缩算法或加密组合可能受 SharpCompress 自身能力限制，正式项目应使用实际产物在目标平台验收。

RAR 使用顺序读取方式，兼容普通 RAR、RAR5 与固实压缩。分卷 RAR 需要先将全部分卷下载到同一目录，并保留 WinRAR 生成的原始文件名；当前 `DownloadModule` 的单文件下载接口不会自动补齐其他分卷。

## 使用

1. 导入该 Sample。
2. 在 `UnityRFramework` 预制体的 `Download` 组件中，将 `Archive Helper` 选择为 `UnityRFramework.Expansion.SharpCompressArchiveHelper`。
3. 下载时启用解压：

```csharp
DownloadOptions options = new DownloadOptions
{
    ExtractArchive = true,
    ArchiveFormat = ArchiveFormat.Auto,
    ExtractDirectory = targetDirectory,
    ArchivePassword = password
};
```

`Auto` 会按文件内容自动识别格式。明确知道格式时可以指定具体枚举，以便尽早发现格式配置错误。

## 安全边界

- 不调用 SharpCompress 的整包目录解压 API，而是逐条目校验并写入。
- 拒绝目录穿越、绝对路径、Windows 备用数据流和符号链接条目。
- 遵守 `MaxArchiveEntries` 与 `MaxExtractedBytes` 限制。
- 解压在工作线程执行，并支持协作式取消和进度通知。

## 依赖

- SharpCompress 0.50.1，MIT License。
- Microsoft.Bcl.AsyncInterfaces 8.0.0，MIT License。
- System.Text.Encoding.CodePages 8.0.0，MIT License。
- System.Runtime.CompilerServices.Unsafe 6.0.0，MIT License。

依赖 DLL 与许可证放在各自的 `Plugins` 子目录。Unity 2022 已提供其余 `System.*` 运行时程序集，因此不重复随 Sample 分发。
