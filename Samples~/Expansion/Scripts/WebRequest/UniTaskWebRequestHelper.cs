using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using RFramework;
using UnityRFramework.Runtime;
using UnityEngine.Networking;

namespace UnityRFramework.Expansion.WebRequest
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
        public override async Task<WebResponse> SendAsync(WebRequestData request, IProgress<float> progress, CancellationToken ct)
        {
            UnityWebRequest uwr = null;

            try
            {
                uwr = CreateUnityWebRequest(request);
                var asyncOp = uwr.SendWebRequest();

                // UniTask.Yield 挂载到 Update 时机，struct 零分配
                // 每帧轮询 isDone 并报告进度，ct 被取消时自动抛出 OperationCanceledException
                while (!asyncOp.isDone)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    progress?.Report(asyncOp.progress);
                }

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
        public override async Task DownloadFileAsync(WebRequestData request, string savePath, IProgress<float> progress, CancellationToken ct)
        {
            UnityWebRequest uwr = null;

            try
            {
                // 自动创建父目录
                string dir = System.IO.Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                {
                    System.IO.Directory.CreateDirectory(dir);
                }

                uwr = new UnityWebRequest(request.Url, "GET");
                uwr.downloadHandler = new DownloadHandlerFile(savePath) { removeFileOnAbort = true };

                if (request.Headers != null)
                {
                    foreach (var kv in request.Headers)
                    {
                        uwr.SetRequestHeader(kv.Key, kv.Value);
                    }
                }

                uwr.timeout = 0;

                var asyncOp = uwr.SendWebRequest();

                while (!asyncOp.isDone)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    progress?.Report(asyncOp.progress);
                }

                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    throw new System.Exception(
                        string.Format("UniTask download failed: {0} ({1})", uwr.error, uwr.responseCode));
                }
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
