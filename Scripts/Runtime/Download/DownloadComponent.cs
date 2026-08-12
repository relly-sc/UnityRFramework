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
        private const string DefaultArchiveHelperTypeName = "RFramework.DefaultZipArchiveHelper";

        [SerializeField]
        [Tooltip("压缩文件解压辅助器类型全名。默认使用 .NET ZIP 实现，可选择 SharpZipLib 等扩展。")]
        private string archiveHelperTypeName = DefaultArchiveHelperTypeName;

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
            InstallArchiveHelper();
        }

        /// <summary>
        /// 设置压缩文件解压辅助器。传入 null 时恢复框架默认 ZIP 实现。
        /// 可在项目启动流程中注入 SharpZipLib 等第三方实现。
        /// </summary>
        public void SetArchiveHelper(IArchiveHelper helper)
        {
            downloadModule.SetArchiveHelper(helper);
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

        private void InstallArchiveHelper()
        {
            if (string.IsNullOrWhiteSpace(archiveHelperTypeName))
            {
                archiveHelperTypeName = DefaultArchiveHelperTypeName;
            }

            Type helperType = Utility.Assembly.GetType(archiveHelperTypeName);
            if (helperType == null || !typeof(IArchiveHelper).IsAssignableFrom(helperType))
            {
                throw new RFrameworkException(
                    $"DownloadComponent: archive helper '{archiveHelperTypeName}' is missing or invalid.");
            }

            try
            {
                downloadModule.SetArchiveHelper(
                    (IArchiveHelper)Activator.CreateInstance(helperType));
            }
            catch (Exception ex) when (!(ex is RFrameworkException))
            {
                throw new RFrameworkException(
                    $"DownloadComponent: archive helper '{archiveHelperTypeName}' could not be created.", ex);
            }
        }
    }
}
