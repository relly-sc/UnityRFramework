# UnityRFramework · Expansion.SharpZipLib

`Expansion.SharpZipLib` 为 `DownloadModule` 提供基于 SharpZipLib 的可选
`IArchiveHelper` 实现。导入后核心框架 API、下载、断点续传、校验和目录事务语义保持不变，
仅替换 ZIP 解压实现。

## 依赖与许可证

- 内置 `ICSharpCode.SharpZipLib.dll`：1.4.2.13。
- 上游仓库：https://github.com/icsharpcode/SharpZipLib
- 许可证：MIT，原文位于 `Plugins/SharpZipLib/LICENSE.txt`。

DLL 会参与 Editor 和 Player 编译，以便在 Play Mode 和目标平台运行该 Runtime 扩展。

## 使用

推荐在 `DownloadComponent` Inspector 的 `Archive Helper` 下拉框选择
`UnityRFramework.Expansion.SharpZipLibArchiveHelper`。默认值为
`RFramework.DefaultArchiveHelper`，导入本 Sample 不会自动改变已有项目行为。

也可以在发起需要 ZIP 解压的下载前通过代码注入：

```csharp
using UnityRFramework.Expansion;
using UnityRFramework.Runtime;

GameEntry.Download.SetArchiveHelper(new SharpZipLibArchiveHelper());
```

加密 ZIP 可设置密码：

```csharp
GameEntry.Download.SetArchiveHelper(
    new SharpZipLibArchiveHelper("zip-password"));
```

之后仍使用原有 `DownloadAsync`：

```csharp
DownloadResult result = await GameEntry.Download.DownloadAsync(
    url,
    savePath,
    new DownloadOptions
    {
        ExtractArchive = true,
        ArchiveFormat = ArchiveFormat.Zip,
        ExtractDirectory = extractDirectory
    },
    progress,
    cancellationToken);
```

## 行为边界

- 支持普通 ZIP、Zip64，以及 SharpZipLib 1.4.2 可读取的传统/AES 加密 ZIP。
- 解压在后台线程执行，通过框架 `ArchiveProgress`/`DownloadProgress` 报告进度。
- 保留框架要求的路径穿越防护、最大条目数和最大解压字节数限制。
- `DownloadModule` 仍负责临时目录、正式目录替换、失败恢复和压缩包删除策略。
- 本实现不处理 RAR；Tar、GZip、BZip2 虽然 SharpZipLib 提供底层能力，但当前
- 该 Helper 只接受 `Auto` 或 `Zip`；RAR、7z 等格式请使用 `Expansion.SharpCompress`。
