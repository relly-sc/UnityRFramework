using System;
using System.Threading;
using System.Threading.Tasks;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 资源更新摘要。
    /// 统计结果只包含本地尚不存在、需要从远程下载的资源文件。
    /// </summary>
    public readonly struct ResourceUpdateInfo
    {
        /// <summary>
        /// 创建资源更新摘要。
        /// </summary>
        /// <param name="fileCount">待下载文件数量。</param>
        /// <param name="totalBytes">待下载总字节数。</param>
        public ResourceUpdateInfo(int fileCount, long totalBytes)
        {
            FileCount = fileCount;
            TotalBytes = totalBytes;
        }

        /// <summary>待下载文件数量。</summary>
        public int FileCount { get; }

        /// <summary>待下载总字节数。</summary>
        public long TotalBytes { get; }

        /// <summary>是否存在需要下载的更新。</summary>
        public bool HasUpdate
        {
            get { return FileCount > 0 || TotalBytes > 0L; }
        }
    }

    /// <summary>
    /// 资源下载进度。
    /// </summary>
    public readonly struct ResourceUpdateProgress
    {
        /// <summary>
        /// 创建资源下载进度。
        /// </summary>
        public ResourceUpdateProgress(
            int totalFileCount,
            int downloadedFileCount,
            long totalBytes,
            long downloadedBytes)
        {
            TotalFileCount = totalFileCount;
            DownloadedFileCount = downloadedFileCount;
            TotalBytes = totalBytes;
            DownloadedBytes = downloadedBytes;
        }

        /// <summary>待下载文件总数。</summary>
        public int TotalFileCount { get; }

        /// <summary>已完成下载的文件数。</summary>
        public int DownloadedFileCount { get; }

        /// <summary>待下载总字节数。</summary>
        public long TotalBytes { get; }

        /// <summary>已下载字节数。</summary>
        public long DownloadedBytes { get; }

        /// <summary>下载进度，范围为 0 到 1。</summary>
        public float Progress
        {
            get
            {
                if (TotalBytes > 0L)
                {
                    return Math.Min(1f, (float)DownloadedBytes / TotalBytes);
                }

                return TotalFileCount > 0
                    ? Math.Min(1f, (float)DownloadedFileCount / TotalFileCount)
                    : 1f;
            }
        }
    }

    /// <summary>
    /// 可选的资源更新服务。
    /// Resource Helper 可实现本接口，为启动流程提供更新检查和差量下载能力。
    /// </summary>
    public interface IResourceUpdateService
    {
        /// <summary>
        /// 根据当前已加载的远程清单创建差量下载计划。
        /// 本地内置或缓存中已经存在的文件不会计入结果。
        /// </summary>
        /// <param name="maximumConcurrency">最大并发下载数量。</param>
        /// <param name="retryCount">单文件失败重试次数。</param>
        /// <returns>需要下载的更新摘要。</returns>
        ResourceUpdateInfo PrepareUpdate(int maximumConcurrency = 8, int retryCount = 3);

        /// <summary>
        /// 下载最近一次 <see cref="PrepareUpdate"/> 创建的更新计划。
        /// 失败或取消后应重新调用 PrepareUpdate，再发起重试。
        /// </summary>
        /// <param name="progress">下载进度接收器。</param>
        /// <param name="ct">取消令牌。</param>
        Task DownloadPreparedUpdateAsync(
            IProgress<ResourceUpdateProgress> progress,
            CancellationToken ct = default);
    }

    /// <summary>
    /// 可选的按标签资源更新服务。
    /// 用于把启动预下载资源与运行时按需下载资源分开。
    /// </summary>
    public interface ITaggedResourceUpdateService
    {
        /// <summary>
        /// 根据资源标签创建差量下载计划。
        /// </summary>
        /// <param name="tags">参与本次更新检查的资源标签。</param>
        /// <param name="maximumConcurrency">最大并发下载数量。</param>
        /// <param name="retryCount">单文件失败重试次数。</param>
        /// <returns>需要下载的更新摘要。</returns>
        ResourceUpdateInfo PrepareUpdateByTags(
            string[] tags,
            int maximumConcurrency = 8,
            int retryCount = 3);
    }
}
