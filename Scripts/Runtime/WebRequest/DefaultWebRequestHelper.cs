using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using UnityEngine.Networking;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 基于 UnityWebRequest 的默认 WebRequest 辅助器实现（纯 Unity，零第三方依赖）。
    /// 使用 StartCoroutine + TaskCompletionSource 桥接 Unity 协程式异步与 Task 异步模式。
    /// </summary>
    /// <remarks>
    /// WebRequestComponent 的 webRequestHelperTypeName 默认指向此类型，
    /// 创建后即可作为默认的 HTTP 通信通道使用，无需额外配置。
    /// </remarks>
    public class DefaultWebRequestHelper : WebRequestHelperBase
    {
        /// <inheritdoc />
        public override Task<WebResponse> SendAsync(WebRequestData request, IProgress<float> progress, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<WebResponse>();
            StartCoroutine(SendCoroutine(request, progress, ct, tcs));
            return tcs.Task;
        }

        /// <inheritdoc />
        public override Task DownloadFileAsync(WebRequestData request, string savePath, IProgress<float> progress, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<bool>();
            StartCoroutine(DownloadFileCoroutine(request, savePath, progress, ct, tcs));
            return tcs.Task;
        }

        /// <summary>
        /// 协程驱动的主请求逻辑。
        /// 分三阶段执行以避免 C# 限制（yield return 不能出现在 try-catch 块内）：
        /// 阶段 1 — 创建请求；阶段 2 — 等待完成（含进度和取消）；阶段 3 — 构建响应。
        /// </summary>
        private IEnumerator SendCoroutine(WebRequestData request, IProgress<float> progress, CancellationToken ct, TaskCompletionSource<WebResponse> tcs)
        {
            UnityWebRequest uwr = null;

            // 阶段 1：创建请求
            try
            {
                uwr = CreateUnityWebRequest(request);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
                yield break;
            }

            // 阶段 2：等待异步操作完成（不能在这之外包裹 try-catch，否则 yield return 无法编译）
            var asyncOp = uwr.SendWebRequest();
            while (!asyncOp.isDone)
            {
                if (ct.IsCancellationRequested)
                {
                    uwr.Abort();
                    uwr.Dispose();
                    tcs.TrySetCanceled(ct);
                    yield break;
                }

                progress?.Report(asyncOp.progress);
                yield return null;
            }

            // 阶段 3：构建响应
            try
            {
                var response = BuildResponse(uwr);
                tcs.TrySetResult(response);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            finally
            {
                uwr.Dispose();
            }
        }

        /// <summary>
        /// 流式下载协程：使用 DownloadHandlerFile 直接将数据写入磁盘，不经过内存缓存。
        /// </summary>
        private IEnumerator DownloadFileCoroutine(WebRequestData request, string savePath, IProgress<float> progress, CancellationToken ct, TaskCompletionSource<bool> tcs)
        {
            UnityWebRequest uwr = null;

            // 阶段 1：创建请求，使用 DownloadHandlerFile
            try
            {
                string dir = Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string method = MapMethod(request.Method);
                uwr = new UnityWebRequest(request.Url, method);
                uwr.downloadHandler = new DownloadHandlerFile(savePath) { removeFileOnAbort = true };

                if (request.Headers != null)
                {
                    foreach (var kv in request.Headers)
                    {
                        uwr.SetRequestHeader(kv.Key, kv.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
                yield break;
            }

            // 阶段 2：等待完成
            var asyncOp = uwr.SendWebRequest();
            while (!asyncOp.isDone)
            {
                if (ct.IsCancellationRequested)
                {
                    uwr.Abort();
                    uwr.Dispose();
                    tcs.TrySetCanceled(ct);
                    yield break;
                }

                progress?.Report(asyncOp.progress);
                yield return null;
            }

            // 阶段 3：检查结果
            try
            {
                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    tcs.TrySetResult(true);
                }
                else
                {
                    tcs.TrySetException(new System.Exception(
                        string.Format("Download failed: {0} ({1})", uwr.error, uwr.responseCode)));
                }
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            finally
            {
                uwr.Dispose();
            }
        }
    }
}
