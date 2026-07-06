using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using RFramework.WebRequest;
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
    }
}
