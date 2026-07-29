using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// WebRequest 辅助器基类。
    /// 继承自 MonoBehaviour 并实现 IWebRequestHelper 接口，使 Runtime 层辅助器
    /// 可以通过 <see cref="Helper.CreateHelper{T}(string, T)"/> 统一创建为场景中的 GameObject，
    /// 同时仍能以接口形式注入到 Library 层的 WebRequestModule。
    /// </summary>
    /// <remarks>
    /// 子类需要实现 SendAsync 方法，内部使用 UnityWebRequest 或第三方 HTTP 库完成实际网络通信。
    /// 工具方法（MapMethod、CreateUnityWebRequest、BuildResponse、MapError）均为 protected static，
    /// 供 Runtime 层 Default 实现和 Expansion 层 UniTask 实现共享调用。
    /// </remarks>
    public abstract class WebRequestHelperBase : MonoBehaviour, IWebRequestHelper
    {
        /// <summary>
        /// 执行一次原始 HTTP 请求并返回响应。
        /// 子类必须实现此方法，封装具体的 HTTP 实现细节。
        /// </summary>
        /// <param name="request">完整的请求数据。</param>
        /// <param name="progress">下载进度报告器（0.0 ~ 1.0），可为 null。</param>
        /// <param name="ct">取消令牌，在超时或用户取消时触发。</param>
        /// <returns>HTTP 响应。</returns>
        public abstract Task<WebResponse> SendAsync(WebRequestData request, IProgress<float> progress, CancellationToken ct);

        /// <inheritdoc/>
        public abstract Task DownloadFileAsync(WebRequestData request, string savePath, IProgress<float> progress, CancellationToken ct);

        #region 共享工具方法

        /// <summary>
        /// 将框架内部 <see cref="HttpMethod"/> 枚举映射为 HTTP 动词字符串。
        /// </summary>
        /// <param name="method">框架内部 HTTP 方法枚举。</param>
        /// <returns>对应的 HTTP 动词字符串（GET / POST / PUT / DELETE / HEAD / PATCH）。</returns>
        protected static string MapMethod(HttpMethod method)
        {
            switch (method)
            {
                case HttpMethod.Get:
                    return "GET";
                case HttpMethod.Post:
                    return "POST";
                case HttpMethod.Put:
                    return "PUT";
                case HttpMethod.Delete:
                    return "DELETE";
                case HttpMethod.Head:
                    return "HEAD";
                case HttpMethod.Patch:
                    return "PATCH";
                default:
                    return "GET";
            }
        }

        /// <summary>
        /// 根据 <see cref="WebRequestData"/> 创建完整的 <see cref="UnityWebRequest"/> 实例。
        /// 处理 URL、HTTP 方法、请求头、Body 和 Content-Type 等配置。
        /// WebRequestData.ContentType 已是原始字符串（如 "application/json"），直接透传。
        /// </summary>
        /// <param name="request">框架内部的请求数据封装。</param>
        /// <returns>已配置好的 UnityWebRequest 实例（需调用者释放）。</returns>
        protected static UnityWebRequest CreateUnityWebRequest(WebRequestData request)
        {
            var method = MapMethod(request.Method);
            var url = request.Url;

            var uwr = new UnityWebRequest(url, method);
            uwr.downloadHandler = new DownloadHandlerBuffer();

            if (request.Body != null && request.Body.Length > 0)
            {
                uwr.uploadHandler = new UploadHandlerRaw(request.Body);
                uwr.uploadHandler.contentType = request.ContentType ?? string.Empty;
            }

            // 设置自定义请求头（必须在设置 Body 之后，否则可能被覆盖）
            if (request.Headers != null)
            {
                foreach (var kvp in request.Headers)
                {
                    uwr.SetRequestHeader(kvp.Key, kvp.Value);
                }
            }

            // 超时由 CancellationToken 控制，禁用 UnityWebRequest 内部超时以避免冲突
            uwr.timeout = 0;

            return uwr;
        }

        /// <summary>
        /// 从 <see cref="UnityWebRequest"/> 实例中提取 <see cref="WebResponse"/>。
        /// 使用不可变构造函数创建响应实例，确保 Library 层属性只读约束得到遵守。
        /// </summary>
        /// <param name="uwr">已完成请求的 UnityWebRequest 实例。</param>
        /// <returns>框架内部的 WebResponse 封装。</returns>
        protected static WebResponse BuildResponse(UnityWebRequest uwr)
        {
            var statusCode = (int)uwr.responseCode;
            var contentType = uwr.GetResponseHeader("Content-Type") ?? string.Empty;

            var headers = uwr.GetResponseHeaders();
            var headersDict = (headers != null && headers.Count > 0)
                ? new Dictionary<string, string>(headers)
                : new Dictionary<string, string>();

            var data = uwr.downloadHandler?.data;

            var (error, errorMessage) = MapError(uwr.result, uwr.error);

            return new WebResponse(statusCode, contentType, headersDict, data, error, errorMessage);
        }

        /// <summary>
        /// 将 <see cref="UnityWebRequest.Result"/> 原生错误分类映射为框架内部的 <see cref="WebRequestError"/>。
        /// 同时从 error 字符串中识别超时场景，从而区分 ConnectionTimeout 与 NetworkError。
        /// </summary>
        /// <param name="result">UnityWebRequest 的请求结果枚举。</param>
        /// <param name="error">UnityWebRequest.error 字符串（可为空）。</param>
        /// <returns>元组 (errorCode, errorMessage)。</returns>
        protected static (WebRequestError Error, string ErrorMessage) MapError(UnityWebRequest.Result result, string error)
        {
            switch (result)
            {
                case UnityWebRequest.Result.Success:
                    return (WebRequestError.None, string.Empty);

                case UnityWebRequest.Result.ConnectionError:
                    // 从 error 字符串中识别超时
                    if (!string.IsNullOrEmpty(error) && error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return (WebRequestError.ConnectionTimeout, error);
                    }

                    return (WebRequestError.NetworkError, error ?? "Unknown connection error");

                case UnityWebRequest.Result.ProtocolError:
                    return (WebRequestError.HttpError, error ?? "HTTP protocol error");

                case UnityWebRequest.Result.DataProcessingError:
                    return (WebRequestError.Unknown, error ?? "Data processing error");

                default:
                    return (WebRequestError.Unknown, error ?? "Unknown error");
            }
        }

        #endregion
    }
}
