using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using RFramework;
using UnityEngine;
using UnityRFramework.Runtime;
using YooAsset;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 管理 YooAsset 包的磁盘缓存容量与资源位置访问记录。
    /// 所有删除操作均通过 ResourcePackage.ClearCacheAsync 完成，不直接删除缓存文件。
    /// </summary>
    internal sealed class YooAssetCacheController
    {
        private const string UsageFileName = "UnityRFrameworkCacheUsage.json";
        private const string BundleFilesFolderName = "BundleFiles";
        private const int ClearBatchSize = 8;
        private const float SaveIntervalSeconds = 30f;

        private readonly string usageFilePath;
        private readonly string bundleFilesRoot;
        private readonly Dictionary<string, long> accessTicks =
            new Dictionary<string, long>(StringComparer.Ordinal);

        private bool isDirty;
        private float lastSaveTime = float.NegativeInfinity;

        /// <summary>
        /// 创建指定 YooAsset 包根目录的缓存控制器。
        /// </summary>
        /// <param name="packageRoot">YooAsset 沙盒文件系统使用的包根目录。</param>
        internal YooAssetCacheController(string packageRoot)
        {
            if (string.IsNullOrWhiteSpace(packageRoot))
            {
                throw new RFrameworkException(
                    "YooAssetCacheController: packageRoot is invalid.");
            }

            usageFilePath = Path.Combine(packageRoot, UsageFileName);
            bundleFilesRoot = Path.Combine(packageRoot, BundleFilesFolderName);
            LoadUsage();
        }

        /// <summary>
        /// 获取 YooAsset 默认缓存规则下的指定包根目录。
        /// 该路径会同时传给 YooAsset 3.0.5 沙盒文件系统，确保统计与实际缓存目录一致。
        /// </summary>
        /// <param name="packageName">YooAsset 包名称。</param>
        /// <returns>指定包的绝对缓存根目录。</returns>
        internal static string GetDefaultPackageRoot(string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                throw new RFrameworkException(
                    "YooAssetCacheController: packageName is invalid.");
            }

            return Path.Combine(GetDefaultCacheRoot(), packageName);
        }

        /// <summary>
        /// 获取最近一次统计到的缓存大小。
        /// </summary>
        internal long CacheSizeBytes { get; private set; }

        /// <summary>
        /// 获取最近一次清理开始前统计到的缓存大小。
        /// </summary>
        internal long CacheSizeBeforeTrimBytes { get; private set; }

        /// <summary>
        /// 记录资源位置最近一次成功使用的时间。
        /// </summary>
        /// <param name="location">YooAsset 资源位置。</param>
        internal void RecordAccess(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return;
            }

            accessTicks[location] = DateTime.UtcNow.Ticks;
            isDirty = true;
            if (Time.realtimeSinceStartup - lastSaveTime >= SaveIntervalSeconds)
            {
                Flush();
            }
        }

        /// <summary>
        /// 将访问记录写入磁盘。
        /// </summary>
        internal void Flush()
        {
            if (!isDirty)
            {
                return;
            }

            string directory = Path.GetDirectoryName(usageFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            UsageFile data = new UsageFile();
            foreach (KeyValuePair<string, long> pair in accessTicks)
            {
                data.Entries.Add(new UsageEntry
                {
                    Location = pair.Key,
                    LastAccessUtcTicks = pair.Value
                });
            }

            data.Entries.Sort((left, right) =>
                string.CompareOrdinal(left.Location, right.Location));

            string temporaryPath = usageFilePath + ".tmp";
            File.WriteAllText(
                temporaryPath,
                JsonUtility.ToJson(data),
                new UTF8Encoding(false));
            File.Copy(temporaryPath, usageFilePath, true);
            File.Delete(temporaryPath);
            isDirty = false;
            lastSaveTime = Time.realtimeSinceStartup;
        }

        /// <summary>
        /// 使用 YooAsset 官方清理接口将缓存收缩到指定容量以内。
        /// </summary>
        /// <param name="package">已经初始化并加载活动清单的资源包。</param>
        /// <param name="maxCacheBytes">缓存容量上限。</param>
        /// <returns>清理完成后的缓存字节数。</returns>
        internal async Task<long> TrimAsync(
            ResourcePackage package,
            long maxCacheBytes)
        {
            if (package == null)
            {
                throw new RFrameworkException(
                    "YooAssetCacheController: package is invalid.");
            }

            if (maxCacheBytes <= 0)
            {
                throw new RFrameworkException(
                    "YooAssetCacheController: maxCacheBytes must be greater than zero.");
            }

            CacheSizeBytes = CalculateCacheSize();
            CacheSizeBeforeTrimBytes = CacheSizeBytes;
            if (CacheSizeBytes <= maxCacheBytes)
            {
                return CacheSizeBytes;
            }

            await ClearAsync(
                package,
                new ClearCacheOptions(ClearCacheMethods.ClearUnusedBundleFiles));
            CacheSizeBytes = CalculateCacheSize();
            if (CacheSizeBytes <= maxCacheBytes)
            {
                return CacheSizeBytes;
            }

            List<CacheCandidate> candidates = BuildCandidates(package);
            for (int start = 0;
                 start < candidates.Count && CacheSizeBytes > maxCacheBytes;
                 start += ClearBatchSize)
            {
                int count = Math.Min(
                    ClearBatchSize,
                    candidates.Count - start);
                string[] locations = new string[count];
                for (int i = 0; i < count; i++)
                {
                    locations[i] = candidates[start + i].Location;
                }

                await ClearAsync(
                    package,
                    new ClearCacheOptions(
                        ClearCacheMethods.ClearBundleFilesByLocations,
                        locations));
                CacheSizeBytes = CalculateCacheSize();
            }

            return CacheSizeBytes;
        }

        private static async Task ClearAsync(
            ResourcePackage package,
            ClearCacheOptions options)
        {
            ClearCacheOperation operation = package.ClearCacheAsync(options);
            await operation;
            if (operation.Status != EOperationStatus.Succeeded)
            {
                throw new RFrameworkException(
                    $"YooAsset cache cleanup failed. "
                    + $"Status: {operation.Status}, Error: {operation.Error}");
            }
        }

        private List<CacheCandidate> BuildCandidates(ResourcePackage package)
        {
            AssetInfo[] assetInfos = package.GetAllAssetInfos();
            Dictionary<string, CacheCandidate> candidates =
                new Dictionary<string, CacheCandidate>(StringComparer.Ordinal);
            for (int i = 0; i < assetInfos.Length; i++)
            {
                AssetInfo assetInfo = assetInfos[i];
                if (assetInfo == null || !assetInfo.IsValid)
                {
                    continue;
                }

                string location = !string.IsNullOrEmpty(assetInfo.Address)
                    ? assetInfo.Address
                    : assetInfo.AssetPath;
                if (string.IsNullOrEmpty(location))
                {
                    continue;
                }

                long lastAccess = GetLastAccessTicks(
                    assetInfo.Address,
                    assetInfo.AssetPath);
                if (!candidates.TryGetValue(
                        location,
                        out CacheCandidate existing)
                    || lastAccess < existing.LastAccessUtcTicks)
                {
                    candidates[location] = new CacheCandidate(
                        location,
                        lastAccess);
                }
            }

            List<CacheCandidate> result =
                new List<CacheCandidate>(candidates.Values);
            result.Sort((left, right) =>
            {
                int timeComparison = left.LastAccessUtcTicks.CompareTo(
                    right.LastAccessUtcTicks);
                return timeComparison != 0
                    ? timeComparison
                    : string.CompareOrdinal(left.Location, right.Location);
            });
            return result;
        }

        private long GetLastAccessTicks(string address, string assetPath)
        {
            long result = 0L;
            if (!string.IsNullOrEmpty(address)
                && accessTicks.TryGetValue(address, out long addressTicks))
            {
                result = Math.Max(result, addressTicks);
            }

            if (!string.IsNullOrEmpty(assetPath)
                && accessTicks.TryGetValue(assetPath, out long pathTicks))
            {
                result = Math.Max(result, pathTicks);
            }

            return result;
        }

        private long CalculateCacheSize()
        {
            if (!Directory.Exists(bundleFilesRoot))
            {
                return 0L;
            }

            long totalSize = 0L;
            string[] files = Directory.GetFiles(
                bundleFilesRoot,
                "*",
                SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                totalSize = checked(totalSize + new FileInfo(files[i]).Length);
            }

            return totalSize;
        }

        private void LoadUsage()
        {
            if (!File.Exists(usageFilePath))
            {
                return;
            }

            try
            {
                UsageFile data = JsonUtility.FromJson<UsageFile>(
                    File.ReadAllText(usageFilePath, Encoding.UTF8));
                if (data?.Entries == null)
                {
                    return;
                }

                for (int i = 0; i < data.Entries.Count; i++)
                {
                    UsageEntry entry = data.Entries[i];
                    if (entry == null
                        || string.IsNullOrWhiteSpace(entry.Location)
                        || entry.LastAccessUtcTicks <= 0)
                    {
                        continue;
                    }

                    accessTicks[entry.Location] = entry.LastAccessUtcTicks;
                }
            }
            catch (Exception exception)
            {
                Log.Warning(
                    "YooAsset cache usage file is invalid and will be rebuilt: {0}",
                    exception.Message);
                accessTicks.Clear();
            }
        }

        private static string GetDefaultCacheRoot()
        {
            string basePath;
#if UNITY_EDITOR
            basePath = Path.Combine(
                Path.GetDirectoryName(Application.dataPath) ?? Application.dataPath,
                "Library");
#elif UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX
            basePath = Application.dataPath;
#else
            basePath = Application.persistentDataPath;
#endif
            string yooFolderName = YooAssetConfiguration.GetYooFolderName();
            return string.IsNullOrEmpty(yooFolderName)
                ? basePath
                : Path.Combine(basePath, yooFolderName);
        }

        private readonly struct CacheCandidate
        {
            internal CacheCandidate(
                string location,
                long lastAccessUtcTicks)
            {
                Location = location;
                LastAccessUtcTicks = lastAccessUtcTicks;
            }

            internal string Location { get; }

            internal long LastAccessUtcTicks { get; }
        }

        [Serializable]
        private sealed class UsageFile
        {
            public List<UsageEntry> Entries = new List<UsageEntry>();
        }

        [Serializable]
        private sealed class UsageEntry
        {
            public string Location;

            public long LastAccessUtcTicks;
        }
    }
}
