using System;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 可靠文件下载组件。
    /// 提供断点续传、失败重试、下载进度、大小与 SHA-256 校验。
    /// </summary>
    [AddComponentMenu("UnityRFramework/Download")]
    [DisallowMultipleComponent]
    public sealed class DownloadComponent : UnityRFrameworkComponent
    {
        [SerializeField]
        [Tooltip("默认是否保留并续传 .part 临时文件。")]
        private bool resumeByDefault = true;

        [SerializeField]
        [Min(0)]
        [Tooltip("默认网络失败重试次数。")]
        private int maxRetries = 2;

        [SerializeField]
        [Min(0)]
        [Tooltip("首次重试等待毫秒数，后续按 2 倍退避。")]
        private int retryDelayMilliseconds = 500;

        [SerializeField]
        [Min(0)]
        [Tooltip("单次 HTTP 请求超时毫秒数；0 表示不限制总时长。")]
        private int requestTimeoutMilliseconds;

        private IDownloadModule downloadModule;

        /// <inheritdoc />
        protected override void Awake()
        {
            base.Awake();
            downloadModule = RFrameworkModuleHost.Get<IDownloadModule>();
        }

        /// <summary>
        /// 下载文件。未提供 options 时使用 Inspector 默认设置。
        /// </summary>
        public Task<DownloadResult> DownloadAsync(
            string url,
            string savePath,
            DownloadOptions options = null,
            IProgress<DownloadProgress> progress = null,
            CancellationToken ct = default)
        {
            return downloadModule.DownloadAsync(
                url,
                savePath,
                options ?? CreateDefaultOptions(),
                progress,
                ct);
        }

        /// <inheritdoc cref="IDownloadModule.CancelAll" />
        public void CancelAll()
        {
            downloadModule.CancelAll();
        }

        /// <inheritdoc cref="IDownloadModule.ActiveDownloadCount" />
        public int ActiveDownloadCount => downloadModule?.ActiveDownloadCount ?? 0;

        /// <inheritdoc cref="IDownloadModule.ActiveDownloadedBytes" />
        public long ActiveDownloadedBytes => downloadModule?.ActiveDownloadedBytes ?? 0L;

        private DownloadOptions CreateDefaultOptions()
        {
            return new DownloadOptions
            {
                Resume = resumeByDefault,
                MaxRetries = maxRetries,
                RetryDelayMilliseconds = retryDelayMilliseconds,
                RequestTimeoutMilliseconds = requestTimeoutMilliseconds
            };
        }
    }
}
