using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using RFramework.WebRequest;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// WebRequest 组件。
    /// 负责 Inspector 配置注入（并发数、超时、重试）并转发调用到 WebRequestModule。
    /// </summary>
    /// <remarks>
    /// 设计约束：
    /// - 不包含业务逻辑（逻辑在 WebRequestModule 中）。
    /// - Inspector 暴露 maxConcurrentRequests / defaultTimeoutMs / maxRetries 供设计师配置。
    /// - 通过 Helper.CreateHelper 反射创建辅助器。
    /// </remarks>
    [AddComponentMenu("UnityRFramework/WebRequest")]
    [DisallowMultipleComponent]
    public sealed class WebRequestComponent : UnityRFrameworkComponent
    {
        /// <summary>
        /// WebRequest 辅助器类型全名。
        /// 必须是继承自 <see cref="WebRequestHelperBase"/> 的 MonoBehaviour 类型。
        /// 默认指向不存在的 DefaultWebRequestHelper，运行时输出 Error 日志。
        /// 请在 Inspector 中配置或在启动流程中通过 SetHelper 方法运行时替换。
        /// </summary>
        [SerializeField]
        [Tooltip("WebRequest 辅助器类型全名。必须是继承自 WebRequestHelperBase 的 MonoBehaviour。")]
        private string webRequestHelperTypeName = "UnityRFramework.Runtime.DefaultWebRequestHelper";

        /// <summary>
        /// 最大并发请求数。0 表示无限制。超过上限的请求将排队等待。
        /// 默认值 5。
        /// </summary>
        [SerializeField]
        [Tooltip("最大并发请求数（0 = 无限制）。超过上限的请求将排队等待。")]
        private int maxConcurrentRequests = 5;

        /// <summary>
        /// 默认超时时间（毫秒）。所有未显式指定超时的请求方法使用此值。
        /// 0 表示无超时限制。默认值 30000（30 秒）。
        /// </summary>
        [SerializeField]
        [Tooltip("默认超时时间（毫秒，0 = 无超时限制）。")]
        private int defaultTimeoutMs = 30000;

        /// <summary>
        /// 默认重试次数。0 表示不重试。
        /// 重试条件：连接超时、请求超时、网络错误、HTTP 5xx。
        /// 不重试：用户取消、HTTP 4xx。
        /// </summary>
        [SerializeField]
        [Tooltip("默认重试次数（0 = 不重试）。")]
        private int maxRetries = 0;

        /// <summary>
        /// WebRequest 模块引用，由 Awake 从 RFrameworkModuleEntry 获取并缓存。
        /// </summary>
        private IWebRequestModule webRequestModule;

        // ====== 生命周期 ======

        /// <inheritdoc />
        protected override void Awake()
        {
            base.Awake();
            webRequestModule = RFrameworkModuleEntry.GetModule<IWebRequestModule>();

            // 注入并发/超时/重试配置
            webRequestModule.SetMaxConcurrentRequests(maxConcurrentRequests);
            webRequestModule.SetDefaultTimeout(defaultTimeoutMs);
            webRequestModule.SetDefaultRetries(maxRetries);

            // 通过统一 Helper 创建器反射创建 MonoBehaviour 辅助器
            WebRequestHelperBase helper = Helper.CreateHelper<WebRequestHelperBase>(webRequestHelperTypeName, null);
            if (helper != null)
            {
                helper.name = $"{helper.GetType().Name} (WebRequest Helper)";
                helper.transform.SetParent(transform);
                webRequestModule.SetHelper(helper);
            }
            else
            {
                Log.Error(
                    "WebRequestComponent: WebRequest 辅助器类型 '{0}' 为 null。"
                    + "请在 Inspector 中配置 WebRequestHelperTypeName 或在启动流程中调用 SetHelper()。",
                    webRequestHelperTypeName);
            }
        }

        // ====== API：配置 ======

        /// <summary>
        /// 设置 WebRequest 辅助器（替换 Inspector 中配置的默认 Helper）。
        /// 在启动流程中调用，传入真实 Helper 实现。
        /// </summary>
        /// <param name="helper">WebRequest 辅助器实例，为 null 时抛出异常。</param>
        public void SetHelper(IWebRequestHelper helper)
        {
            if (helper == null)
            {
                throw new RFrameworkException("WebRequestComponent: helper is invalid.");
            }

            webRequestModule.SetHelper(helper);
        }

        // ====== API：请求 ======

        /// <inheritdoc cref="IWebRequestModule.GetAsync"/>
        public Task<WebResponse> GetAsync(
            string url,
            Dictionary<string, string> queryParams = null,
            Dictionary<string, string> headers = null,
            string tag = null,
            uint priority = 0,
            CancellationToken ct = default)
        {
            return webRequestModule.GetAsync(url, queryParams, headers, tag, priority, ct);
        }

        /// <inheritdoc cref="IWebRequestModule.PostAsync(string, string, MimeType, Dictionary{string, string}, string, uint, CancellationToken)"/>
        public Task<WebResponse> PostAsync(
            string url,
            string body,
            MimeType mimeType = MimeType.Json,
            Dictionary<string, string> headers = null,
            string tag = null,
            uint priority = 0,
            CancellationToken ct = default)
        {
            return webRequestModule.PostAsync(url, body, mimeType, headers, tag, priority, ct);
        }

        /// <inheritdoc cref="IWebRequestModule.PostFormAsync"/>
        public Task<WebResponse> PostFormAsync(
            string url,
            Dictionary<string, string> formFields,
            Dictionary<string, string> headers = null,
            string tag = null,
            uint priority = 0,
            CancellationToken ct = default)
        {
            return webRequestModule.PostFormAsync(url, formFields, headers, tag, priority, ct);
        }

        /// <inheritdoc cref="IWebRequestModule.PostMultipartAsync"/>
        public Task<WebResponse> PostMultipartAsync(
            string url,
            List<MultipartField> fields,
            Dictionary<string, string> headers = null,
            IProgress<float> progress = null,
            string tag = null,
            uint priority = 0,
            CancellationToken ct = default)
        {
            return webRequestModule.PostMultipartAsync(url, fields, headers, progress, tag, priority, ct);
        }

        /// <inheritdoc cref="IWebRequestModule.PutAsync"/>
        public Task<WebResponse> PutAsync(
            string url,
            string body,
            MimeType mimeType = MimeType.Json,
            Dictionary<string, string> headers = null,
            string tag = null,
            uint priority = 0,
            CancellationToken ct = default)
        {
            return webRequestModule.PutAsync(url, body, mimeType, headers, tag, priority, ct);
        }

        /// <inheritdoc cref="IWebRequestModule.DeleteAsync"/>
        public Task<WebResponse> DeleteAsync(
            string url,
            string body = null,
            MimeType mimeType = MimeType.Json,
            Dictionary<string, string> headers = null,
            string tag = null,
            uint priority = 0,
            CancellationToken ct = default)
        {
            return webRequestModule.DeleteAsync(url, body, mimeType, headers, tag, priority, ct);
        }

        /// <inheritdoc cref="IWebRequestModule.SendAsync(WebRequestData, IProgress{float}, CancellationToken)"/>
        public Task<WebResponse> SendAsync(
            WebRequestData request,
            IProgress<float> progress = null,
            CancellationToken ct = default)
        {
            return webRequestModule.SendAsync(request, progress, ct);
        }

        /// <inheritdoc cref="IWebRequestModule.DownloadAsync"/>
        public Task<byte[]> DownloadAsync(
            string url,
            IProgress<float> progress = null,
            Dictionary<string, string> headers = null,
            string tag = null,
            uint priority = 0,
            CancellationToken ct = default)
        {
            return webRequestModule.DownloadAsync(url, progress, headers, tag, priority, ct);
        }

        /// <inheritdoc cref="IWebRequestModule.GetJsonAsync{T}"/>
        public Task<T> GetJsonAsync<T>(
            string url,
            Dictionary<string, string> queryParams = null,
            Dictionary<string, string> headers = null,
            string tag = null,
            CancellationToken ct = default)
        {
            return webRequestModule.GetJsonAsync<T>(url, queryParams, headers, tag, ct);
        }

        // ====== API：管理 ======

        /// <inheritdoc cref="IWebRequestModule.CancelAllByTag"/>
        public void CancelAllByTag(string tag)
        {
            webRequestModule.CancelAllByTag(tag);
        }

        /// <inheritdoc cref="IWebRequestModule.ActiveRequestCount"/>
        public int ActiveRequestCount
        {
            get { return webRequestModule.ActiveRequestCount; }
        }

        /// <inheritdoc cref="IWebRequestModule.QueuedRequestCount"/>
        public int QueuedRequestCount
        {
            get { return webRequestModule.QueuedRequestCount; }
        }
    }
}
