using System;
using System.Collections;
using System.Collections.Generic;
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
    [UnityEngine.Scripting.Preserve]
    public class DefaultWebRequestHelper : WebRequestHelperBase
    {
        private readonly Dictionary<UnityWebRequest, Action> activeRequests =
            new Dictionary<UnityWebRequest, Action>();

        /// <inheritdoc />
        public override Task<WebResponse> SendAsync(WebRequestData request, IProgress<float> progress, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<WebResponse>();
            StartCoroutine(SendCoroutine(request, progress, ct, tcs));
            return tcs.Task;
        }

        /// <inheritdoc />
        public override Task<WebResponse> DownloadFileAsync(
            WebRequestData request,
            string savePath,
            bool append,
            IProgress<WebDownloadProgress> progress,
            CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<WebResponse>();
            StartCoroutine(DownloadFileCoroutine(request, savePath, append, progress, ct, tcs));
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

            activeRequests.Add(uwr, () => tcs.TrySetCanceled(ct));

            // 阶段 2：等待异步操作完成（不能在这之外包裹 try-catch，否则 yield return 无法编译）
            var asyncOp = uwr.SendWebRequest();
            while (!asyncOp.isDone)
            {
                if (ct.IsCancellationRequested)
                {
                    uwr.Abort();
                    uwr.Dispose();
                    activeRequests.Remove(uwr);
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
                activeRequests.Remove(uwr);
                uwr.Dispose();
            }
        }

        /// <summary>
        /// 流式下载协程：使用 DownloadHandlerFile 直接将数据写入磁盘，不经过内存缓存。
        /// </summary>
        private IEnumerator DownloadFileCoroutine(
            WebRequestData request,
            string savePath,
            bool append,
            IProgress<WebDownloadProgress> progress,
            CancellationToken ct,
            TaskCompletionSource<WebResponse> tcs)
        {
            UnityWebRequest uwr = null;
            long initialLength = append && File.Exists(savePath)
                ? new FileInfo(savePath).Length
                : 0L;

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
                uwr.downloadHandler = new DownloadHandlerFile(savePath, append) { removeFileOnAbort = false };

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

            activeRequests.Add(uwr, () => tcs.TrySetCanceled(ct));

            // 阶段 2：等待完成
            var asyncOp = uwr.SendWebRequest();
            while (!asyncOp.isDone)
            {
                if (ct.IsCancellationRequested)
                {
                    uwr.Abort();
                    uwr.Dispose();
                    activeRequests.Remove(uwr);
                    tcs.TrySetCanceled(ct);
                    yield break;
                }

                progress?.Report(BuildDownloadProgress(uwr, initialLength));
                yield return null;
            }

            // 阶段 3：返回完整 HTTP 元数据，由上层决定是否重试或保留临时文件
            try
            {
                progress?.Report(BuildDownloadProgress(uwr, initialLength));
                tcs.TrySetResult(BuildFileResponse(uwr));
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            finally
            {
                activeRequests.Remove(uwr);
                uwr.Dispose();
            }
        }

        private void OnDisable()
        {
            foreach (KeyValuePair<UnityWebRequest, Action> pair in activeRequests)
            {
                pair.Key.Abort();
                pair.Key.Dispose();
                pair.Value.Invoke();
            }

            activeRequests.Clear();
        }
    }
}
