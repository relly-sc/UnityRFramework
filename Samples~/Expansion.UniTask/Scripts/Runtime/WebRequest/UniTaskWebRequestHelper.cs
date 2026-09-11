using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using RFramework;
using UnityRFramework.Runtime;
using UnityEngine.Networking;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 基于 UniTask 的 WebRequest 辅助器实现（依赖 Cysharp.Threading.Tasks 第三方库）。
    /// 使用 UniTask.Yield 替代协程驱动，struct-based awaiter 零 GC 分配，支持可配置 PlayerLoop 时机。
    /// </summary>
    /// <remarks>
    /// 性能优于 DefaultWebRequestHelper，但需要项目引入 UniTask 包。
    /// 使用时在 WebRequestComponent 的 Inspector 中将 webRequestHelperTypeName 设为此类的全名，
    /// 或通过启动流程调用 WebRequestComponent.SetHelper(new UniTaskWebRequestHelper())。
    /// </remarks>
    public class UniTaskWebRequestHelper : WebRequestHelperBase
    {
        /// <inheritdoc />
        public override async Task<WebResponse> SendAsync(
            WebRequestData request,
            IProgress<float> progress,
            CancellationToken ct)
        {
            UnityWebRequest uwr = null;

            try
            {
                ct.ThrowIfCancellationRequested();
                uwr = CreateUnityWebRequest(request);
                UnityWebRequestAsyncOperation asyncOperation = uwr.SendWebRequest();

                // 不使用 UnityWebRequestAsyncOperation.ToUniTask：它会把 HTTP 4xx/5xx 直接抛成
                // UnityWebRequestException，框架需要 BuildResponse 保留状态码后交给模块层重试策略。
                while (!asyncOperation.isDone)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    progress?.Report(asyncOperation.progress);
                }

                progress?.Report(1f);
                return BuildResponse(uwr);
            }
            catch (OperationCanceledException)
            {
                uwr?.Abort();
                throw;
            }
            finally
            {
                uwr?.Dispose();
            }
        }

        /// <inheritdoc />
        public override async Task<WebResponse> DownloadFileAsync(
            WebRequestData request,
            string savePath,
            bool append,
            IProgress<WebDownloadProgress> progress,
            CancellationToken ct)
        {
            UnityWebRequest uwr = null;

            try
            {
                ct.ThrowIfCancellationRequested();

                // 自动创建父目录
                string dir = System.IO.Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                }

                long initialLength = append && System.IO.File.Exists(savePath)
                    ? new System.IO.FileInfo(savePath).Length
                    : 0L;

                uwr = new UnityWebRequest(request.Url, MapMethod(request.Method));
                uwr.downloadHandler = new DownloadHandlerFile(savePath, append) { removeFileOnAbort = false };

                if (request.Headers != null)
                {
                    foreach (var kv in request.Headers)
                    {
                        uwr.SetRequestHeader(kv.Key, kv.Value);
                    }
                }

                uwr.timeout = 0;

                UnityWebRequestAsyncOperation asyncOperation = uwr.SendWebRequest();

                while (!asyncOperation.isDone)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    progress?.Report(BuildDownloadProgress(uwr, initialLength));
                }

                progress?.Report(BuildDownloadProgress(uwr, initialLength));
                return BuildFileResponse(uwr);
            }
            catch (OperationCanceledException)
            {
                uwr?.Abort();
                throw;
            }
            finally
            {
                uwr?.Dispose();
            }
        }
    }
}
