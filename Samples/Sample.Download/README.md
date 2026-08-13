# DownloadModule 验收示例

`Sample.Download` 是内置 `DownloadModule` 的独立轻量验收场景，不依赖
`Sample.Demo`、YooAsset、UniTask 或其他 Expansion。

## 打开方式

1. 打开 `GameAssets/Scenes/DownloadAcceptance.unity`。
2. 确认场景中的 `UnityRFramework` 预制体包含 `WebRequestComponent` 和
   `DownloadComponent`。
3. 进入 Play Mode，在界面中填写文件 URL。

界面中的文件统一写入：

```text
Application.persistentDataPath/UnityRFramework/DownloadAcceptance
```

最终文件名取自 URL 路径的末段；URL 没有有效文件名时回退为 `download.bin`。
下载中的临时文件使用 `<文件名>.part`。取消或网络错误不会删除该分片。

## 控件说明

| 控件 | 作用 |
| --- | --- |
| 下载 URL | HTTP/HTTPS 文件完整地址 |
| 预期字节数 | 可选；完成后校验文件大小 |
| SHA-256 | 可选；完成后校验文件摘要 |
| 解压压缩文件 | 下载提交后使用所选 Helper 安全解压 |
| 解压 Helper | 列出当前项目中可实例化的 `IArchiveHelper` |
| 压缩格式 | `Auto / Zip / Rar / SevenZip / Tar / GZip / BZip2` |
| 密码 | 可选压缩文件密码；具体支持范围由 Helper 决定 |
| 解压目录 | 验收目录下的目标子目录 |
| 解压后删除压缩包 | 仅在解压成功后删除压缩文件 |
| 重新下载 | 删除同名 `.part`，从 0 开始下载 |
| 继续下载 | 保留并尝试续传同名 `.part` |
| 取消 | 取消当前任务并保留 `.part` |
| 删除分片 | 删除当前文件对应的 `.part` |
| 删除结果 | 删除最终文件与当前解压目录 |
| 软重启 | 取消当前任务并调用 `GameEntry.Restart()` |

## 手动验收

### 普通下载与进度

1. 准备一个可访问的 HTTP 文件并填写 URL。
2. 点击“重新下载”。
3. 确认进度、已下载大小、总大小、平均速度和 ETA 持续刷新。
4. 完成后确认最终文件存在，`.part` 已消失。

### 取消与断点续传

1. 下载较大文件，进度开始后点击“取消”。
2. 确认 `.part` 文件仍存在。
3. 点击“继续下载”，确认状态显示“续传”并最终完成。

真实 HTTP 续传要求服务器支持 `Range` 请求。服务器不支持 Range 时，框架会安全地
从头重新获取，不能把该情况视为断点续传成功。

### 大小与 SHA-256 校验

1. 填入正确的预期字节数或 SHA-256，确认下载成功。
2. 改为错误值重新下载，确认任务失败且错误信息可见。
3. 修正校验值后点击“继续下载”或“重新下载”，确认能够恢复。

填写预期字节数后，模块会先尝试发送 HEAD 请求读取服务器的 `Content-Length`：
若服务器明确返回的大小不同，任务会在下载前失败；若服务器不支持 HEAD、未返回长度，
则继续下载并在完成后校验实际文件。预检只是提前失败优化，不能替代最终可信校验。

### 压缩文件解压

1. 准备 ZIP、RAR 或 7z 等可访问的 HTTP 文件。
2. 启用“解压压缩文件”，选择 Helper 和格式；推荐先用 `Auto` 验证内容识别。
3. RAR、7z 等格式选择 `SharpCompress`；Zip64 或加密 ZIP 可选择 `SharpZipLib`。
4. 填写解压目录并开始下载，确认进度、当前条目和解压结果。
5. 分别验证保留压缩包和“解压后删除压缩包”两种设置。

解压期间进度条显示解压后字节进度和当前条目。默认实现使用
`System.IO.Compression`，在后台线程执行纯文件 I/O。项目可通过
`GameEntry.Download.SetArchiveHelper(IArchiveHelper)` 注入 SharpZipLib、SharpCompress 等第三方实现；
第三方 Helper 仍必须遵守目录穿越防护、最大条目数和最大解压字节数限制。

场景只显示当前已导入项目且具有无参构造函数的 Helper。未导入 Expansion 时只有
`Default`，它只支持无密码 ZIP；选择其不支持的格式应得到明确错误。

### 软重启

应用内软重启固定加载 Build Settings 的 0 号场景。要验证软重启，需先将
`DownloadAcceptance.unity` 放到 Build Settings 首位。下载中点击“软重启”后，
框架应完成关闭并重新进入本场景；此前 `.part` 可继续下载。

## 验收标准

- 普通下载、取消、继续、大小校验、SHA-256 校验和多格式解压行为符合界面状态。
- 所有失败均以可见错误结束，不留下永久等待的任务。
- 关闭 Play Mode 或软重启时没有未处理异常。
- 不运行全量 EditMode 测试；框架核心下载逻辑由专项 `DownloadModuleTests` 覆盖，
  本 Sample 只负责真实服务器和 UGUI 交互验收。
