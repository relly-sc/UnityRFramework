# UnityRFramework · Expansion.UniTask

`Expansion.UniTask` 提供 `UniTaskWebRequestHelper`，使用 UniTask 驱动
`UnityWebRequest` 的异步等待，同时保持框架原有 `WebRequestModule` API 不变。

## 前置依赖

- UniTask（`com.cysharp.unitask`）。
- UnityRFramework Runtime。

UniTask 是可选第三方依赖。导入本 Sample 前应先在宿主项目安装 UniTask，否则 Sample
中引用 `Cysharp.Threading.Tasks` 的脚本无法编译。

## 配置

1. 导入 `Expansion.UniTask` Sample。
2. 在 `WebRequestComponent` Inspector 中，将 WebRequest Helper 设置为
   `UnityRFramework.Expansion.UniTaskWebRequestHelper`。
3. 业务代码继续通过 `GameEntry.WebRequest` 发起请求或下载文件。

无需把业务 API 改成 `UniTask`。Helper 内部使用 `UniTask.Yield` 等待 UnityWebRequest，
对外仍遵守框架定义的 `Task`、进度和 `CancellationToken` 契约。

## 行为说明

- 普通请求保留 HTTP 状态码并转换为框架 `WebResponse`，由模块层统一应用重试策略。
- 文件下载使用 `DownloadHandlerFile`，自动创建父目录。
- 请求取消时会调用 `UnityWebRequest.Abort()`。
- 被取消的文件下载会删除未完成文件。
- Helper 不负责业务鉴权、协议序列化、断点续传或业务错误码处理。

本实现仍受 UnityWebRequest 和目标平台网络栈限制。正式发布前应在目标平台验证 HTTPS
证书、代理、超时、后台切换和网络中断等项目实际使用场景。

完整的成功请求、主动取消和框架重启验收流程见 `Expansion.YooAsset.Demo/Acceptance`。
