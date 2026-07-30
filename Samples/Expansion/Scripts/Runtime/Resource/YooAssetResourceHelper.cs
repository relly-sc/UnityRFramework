using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using UnityEngine;
using UnityRFramework.Runtime;
using YooAsset;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 基于 YooAsset v3 的资源辅助器。
    /// 资源句柄由本辅助器持有，资源引用计数由框架 ResourceModule 统一管理。
    /// </summary>
    public sealed class YooAssetResourceHelper : ResourceHelperBase, IResourceCacheHelper,
        IResourceUpdateService, ITaggedResourceUpdateService
    {
        private const long DefaultMaxCacheBytes = 4L * 1024L * 1024L * 1024L;

        private static readonly object lifecycleLock = new object();
        private static Task cleanupTask = Task.CompletedTask;

        private readonly Dictionary<AssetHandleKey, AssetHandle> assetHandles =
            new Dictionary<AssetHandleKey, AssetHandle>();
        private readonly Dictionary<string, SceneHandle> sceneHandles =
            new Dictionary<string, SceneHandle>();

        private ResourcePackage package;
        private ResourceDownloaderOperation preparedDownloader;
        private YooAssetCacheController cacheController;
        private bool autoClearCacheEnabled = true;
        private long maxCacheBytes = DefaultMaxCacheBytes;
        private ResourcePlayMode currentPlayMode;
        private bool isInitialized;
        private bool isDestroying;

        /// <inheritdoc />
        public long CacheSizeBytes
        {
            get
            {
                return cacheController != null
                    ? cacheController.CacheSizeBytes
                    : 0L;
            }
        }

        /// <inheritdoc />
        public void ConfigureCache(bool autoClearEnabled, long maxBytes)
        {
            autoClearCacheEnabled = autoClearEnabled;
            maxCacheBytes = Math.Max(1L, maxBytes);
        }

        /// <inheritdoc />
        public ResourceUpdateInfo PrepareUpdate(
            int maximumConcurrency = 8,
            int retryCount = 3)
        {
            return PrepareUpdateInternal(null, maximumConcurrency, retryCount);
        }

        /// <inheritdoc />
        public ResourceUpdateInfo PrepareUpdateByTags(
            string[] tags,
            int maximumConcurrency = 8,
            int retryCount = 3)
        {
            if (tags == null || tags.Length == 0)
            {
                throw new RFrameworkException(
                    "YooAssetResourceHelper: update tags cannot be null or empty.");
            }

            return PrepareUpdateInternal(tags, maximumConcurrency, retryCount);
        }

        private ResourceUpdateInfo PrepareUpdateInternal(
            string[] tags,
            int maximumConcurrency,
            int retryCount)
        {
            EnsureInitialized();
            if (currentPlayMode != ResourcePlayMode.Host)
            {
                preparedDownloader = null;
                return default;
            }

            if (preparedDownloader != null && !preparedDownloader.IsDone)
            {
                throw new RFrameworkException(
                    "YooAssetResourceHelper: an update download is already running.");
            }

            ResourceDownloaderOptions options = tags == null
                ? new ResourceDownloaderOptions(
                    Math.Max(1, maximumConcurrency),
                    Math.Max(0, retryCount))
                : new ResourceDownloaderOptions(
                    tags,
                    Math.Max(1, maximumConcurrency),
                    Math.Max(0, retryCount));
            preparedDownloader = package.CreateResourceDownloader(options);
            return new ResourceUpdateInfo(
                preparedDownloader.TotalDownloadCount,
                preparedDownloader.TotalDownloadBytes);
        }

        /// <inheritdoc />
        public async Task DownloadPreparedUpdateAsync(
            IProgress<ResourceUpdateProgress> progress,
            CancellationToken ct = default)
        {
            EnsureInitialized();
            ResourceDownloaderOperation downloader = preparedDownloader;
            if (downloader == null)
            {
                throw new RFrameworkException(
                    "YooAssetResourceHelper: call PrepareUpdate before downloading.");
            }

            if (downloader.IsDone)
            {
                throw new RFrameworkException(
                    "YooAssetResourceHelper: the prepared downloader has already completed. "
                    + "Call PrepareUpdate again before retrying.");
            }

            preparedDownloader = null;
            SynchronizationContext ownerContext = SynchronizationContext.Current;
            void ReportProgress(DownloadProgressChangedEventArgs args)
            {
                progress?.Report(new ResourceUpdateProgress(
                    args.TotalDownloadCount,
                    args.CurrentDownloadCount,
                    args.TotalDownloadBytes,
                    args.CurrentDownloadBytes));
            }

            downloader.DownloadProgressChanged += ReportProgress;
            using CancellationTokenRegistration registration = ct.Register(() =>
            {
                if (ownerContext != null)
                {
                    ownerContext.Post(_ => downloader.CancelDownload(), null);
                }
                else
                {
                    downloader.CancelDownload();
                }
            });

            try
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(new ResourceUpdateProgress(
                    downloader.TotalDownloadCount,
                    0,
                    downloader.TotalDownloadBytes,
                    0L));
                downloader.StartDownload();
                await downloader;
                ct.ThrowIfCancellationRequested();

                if (downloader.Status != EOperationStatus.Succeeded)
                {
                    throw new RFrameworkException(
                        "YooAssetResourceHelper: update download failed. "
                        + $"Status: {downloader.Status}, Error: {downloader.Error}");
                }

                progress?.Report(new ResourceUpdateProgress(
                    downloader.TotalDownloadCount,
                    downloader.TotalDownloadCount,
                    downloader.TotalDownloadBytes,
                    downloader.TotalDownloadBytes));
            }
            finally
            {
                downloader.DownloadProgressChanged -= ReportProgress;
            }
        }

        private readonly struct AssetHandleKey : IEquatable<AssetHandleKey>
        {
            /// <summary>资源地址。</summary>
            public readonly string Location;

            /// <summary>框架请求的资源类型。</summary>
            public readonly Type AssetType;

            /// <summary>
            /// 创建资源句柄缓存键。
            /// </summary>
            /// <param name="location">资源地址。</param>
            /// <param name="assetType">框架请求的资源类型。</param>
            public AssetHandleKey(string location, Type assetType)
            {
                Location = location;
                AssetType = assetType;
            }

            /// <inheritdoc />
            public bool Equals(AssetHandleKey other)
            {
                return string.Equals(Location, other.Location, StringComparison.Ordinal)
                    && AssetType == other.AssetType;
            }

            /// <inheritdoc />
            public override bool Equals(object obj)
            {
                return obj is AssetHandleKey other && Equals(other);
            }

            /// <inheritdoc />
            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Location != null ? Location.GetHashCode() : 0) * 397)
                        ^ (AssetType != null ? AssetType.GetHashCode() : 0);
                }
            }
        }

        /// <summary>
        /// 初始化 YooAsset 资源包。
        /// EditorSimulate 模式会先生成模拟清单；软重启时会等待旧资源包完成异步清理。
        /// </summary>
        /// <param name="packageName">资源包名称。</param>
        /// <param name="playMode">资源运行模式。</param>
        /// <param name="defaultHostServer">Host 模式主服务器地址。</param>
        /// <param name="fallbackHostServer">Host 模式备用服务器地址。</param>
        public override async Task InitializeAsync(string packageName, ResourcePlayMode playMode,
            string defaultHostServer, string fallbackHostServer)
        {
            if (isInitialized)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(packageName))
            {
                throw new RFrameworkException("YooAssetResourceHelper: packageName is invalid.");
            }

            await GetPendingCleanupTask();

            if (!YooAssets.IsInitialized)
            {
                YooAssets.Initialize();
            }

            if (YooAssets.TryGetPackage(packageName, out ResourcePackage existingPackage))
            {
                throw new RFrameworkException(
                    $"YooAssetResourceHelper: resource package '{packageName}' already exists. "
                    + "Use a unique package name or release the previous owner first.");
            }

            package = YooAssets.CreatePackage(packageName);
            try
            {
                string cachePackageRoot = playMode == ResourcePlayMode.Host
                    ? YooAssetCacheController.GetDefaultPackageRoot(packageName)
                    : null;
                InitializePackageOptions options = CreateInitializeOptions(
                    packageName,
                    playMode,
                    defaultHostServer,
                    fallbackHostServer,
                    cachePackageRoot);
                InitializePackageOperation operation = package.InitializePackageAsync(options);
                await operation;

                if (operation.Status != EOperationStatus.Succeeded)
                {
                    throw new RFrameworkException(
                        $"YooAssetResourceHelper: initialize package '{packageName}' failed. "
                        + $"Status: {operation.Status}, Error: {operation.Error}");
                }

                RequestPackageVersionOperation versionOperation =
                    package.RequestPackageVersionAsync();
                await versionOperation;
                if (versionOperation.Status != EOperationStatus.Succeeded)
                {
                    throw new RFrameworkException(
                        $"YooAssetResourceHelper: request package version '{packageName}' failed. "
                        + $"Status: {versionOperation.Status}, Error: {versionOperation.Error}");
                }

                LoadPackageManifestOptions manifestOptions =
                    new LoadPackageManifestOptions(versionOperation.PackageVersion, 60);
                LoadPackageManifestOperation manifestOperation =
                    package.LoadPackageManifestAsync(manifestOptions);
                await manifestOperation;
                if (manifestOperation.Status != EOperationStatus.Succeeded)
                {
                    throw new RFrameworkException(
                        $"YooAssetResourceHelper: load package manifest '{packageName}' failed. "
                        + $"Status: {manifestOperation.Status}, Error: {manifestOperation.Error}");
                }

                currentPlayMode = playMode;
                if (playMode == ResourcePlayMode.Host)
                {
                    cacheController = new YooAssetCacheController(cachePackageRoot);
                    await TryAutoTrimCacheAsync();
                }

                isDestroying = false;
                isInitialized = true;
            }
            catch (Exception exception)
            {
                ResourcePackage failedPackage = package;
                package = null;
                await QueueCleanup(failedPackage, Array.Empty<SceneHandle>());
                if (exception is RFrameworkException)
                {
                    throw;
                }

                throw new RFrameworkException(
                    $"YooAssetResourceHelper: initialize package '{packageName}' failed.", exception);
            }
        }

        /// <summary>
        /// 释放当前辅助器持有的句柄，并异步清理 YooAsset 资源包。
        /// ResourceModule 的关闭接口为同步接口，因此软重启通过静态清理屏障等待包清理完成。
        /// </summary>
        public override void Destroy()
        {
            if (isDestroying)
            {
                return;
            }

            isDestroying = true;
            isInitialized = false;
            preparedDownloader?.CancelDownload();
            preparedDownloader = null;

            FlushCacheUsage();

            foreach (KeyValuePair<AssetHandleKey, AssetHandle> pair in assetHandles)
            {
                if (pair.Value != null && pair.Value.IsValid)
                {
                    pair.Value.Release();
                }
            }

            assetHandles.Clear();

            SceneHandle[] scenes = new SceneHandle[sceneHandles.Count];
            sceneHandles.Values.CopyTo(scenes, 0);
            sceneHandles.Clear();

            ResourcePackage packageToDestroy = package;
            package = null;
            cacheController = null;
            currentPlayMode = default;
            _ = QueueCleanup(packageToDestroy, scenes);
        }

        /// <summary>
        /// 异步加载 Unity 资源或文本资源。
        /// 请求类型为 byte[] 或 string 时，资源必须作为 TextAsset 收集进普通资源包。
        /// </summary>
        /// <param name="location">资源地址。</param>
        /// <param name="assetType">框架请求的资源类型。</param>
        /// <param name="priority">加载优先级。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>加载后的资源对象。</returns>
        public override async Task<object> LoadAssetAsync(string location, Type assetType, uint priority,
            CancellationToken ct = default)
        {
            EnsureInitialized();
            ct.ThrowIfCancellationRequested();

            Type requestedType = NormalizeRequestedType(assetType);
            Type yooAssetType = ResolveYooAssetType(requestedType);
            AssetHandleKey key = new AssetHandleKey(location, requestedType);
            if (assetHandles.TryGetValue(key, out AssetHandle cachedHandle))
            {
                RecordCacheAccess(location);
                return ConvertAsset(cachedHandle.AssetObject, requestedType, location);
            }

            AssetHandle handle = package.LoadAssetAsync(location, yooAssetType, priority);
            try
            {
                while (!handle.IsDone)
                {
                    ct.ThrowIfCancellationRequested();
                    await Task.Yield();
                }

                ct.ThrowIfCancellationRequested();
                await handle;

                if (handle.Status != EOperationStatus.Succeeded)
                {
                    throw new RFrameworkException(
                        $"YooAssetResourceHelper: load asset '{location}' failed. "
                        + $"Status: {handle.Status}, Error: {handle.Error}");
                }

                object result = ConvertAsset(handle.AssetObject, requestedType, location);
                assetHandles.Add(key, handle);
                RecordCacheAccess(location);
                return result;
            }
            catch
            {
                if (handle.IsValid)
                {
                    handle.Release();
                }

                throw;
            }
        }

        /// <summary>
        /// 同步加载 Unity 资源或文本资源。
        /// </summary>
        /// <param name="location">资源地址。</param>
        /// <param name="assetType">框架请求的资源类型。</param>
        /// <returns>加载后的资源对象。</returns>
        public override object LoadAssetSync(string location, Type assetType)
        {
            EnsureInitialized();

            Type requestedType = NormalizeRequestedType(assetType);
            Type yooAssetType = ResolveYooAssetType(requestedType);
            AssetHandleKey key = new AssetHandleKey(location, requestedType);
            if (assetHandles.TryGetValue(key, out AssetHandle cachedHandle))
            {
                RecordCacheAccess(location);
                return ConvertAsset(cachedHandle.AssetObject, requestedType, location);
            }

            AssetHandle handle = package.LoadAssetSync(location, yooAssetType);
            try
            {
                if (handle.Status != EOperationStatus.Succeeded)
                {
                    throw new RFrameworkException(
                        $"YooAssetResourceHelper: load asset '{location}' synchronously failed. "
                        + $"Status: {handle.Status}, Error: {handle.Error}");
                }

                object result = ConvertAsset(handle.AssetObject, requestedType, location);
                assetHandles.Add(key, handle);
                RecordCacheAccess(location);
                return result;
            }
            catch
            {
                if (handle.IsValid)
                {
                    handle.Release();
                }

                throw;
            }
        }

        /// <summary>
        /// 释放指定地址与请求类型对应的 YooAsset 句柄。
        /// </summary>
        /// <param name="location">资源地址。</param>
        /// <param name="assetType">加载时使用的框架请求类型。</param>
        public override void ReleaseAsset(string location, Type assetType)
        {
            Type requestedType = NormalizeRequestedType(assetType);
            AssetHandleKey key = new AssetHandleKey(location, requestedType);
            if (!assetHandles.TryGetValue(key, out AssetHandle handle))
            {
                return;
            }

            assetHandles.Remove(key);
            if (handle.IsValid)
            {
                handle.Release();
            }
        }

        /// <summary>
        /// 异步加载场景并持有场景句柄。
        /// </summary>
        /// <param name="location">场景地址。</param>
        /// <param name="sceneMode">Unity 场景加载模式数值。</param>
        /// <param name="activateOnLoad">是否加载完成后立即激活。</param>
        /// <param name="priority">加载优先级。</param>
        /// <param name="onProgress">进度回调。</param>
        public override async Task LoadSceneAsync(string location, int sceneMode,
            bool activateOnLoad, uint priority, IProgress<float> onProgress = null)
        {
            EnsureInitialized();

            if (!activateOnLoad)
            {
                throw new RFrameworkException(
                    "YooAssetResourceHelper: deferred scene activation is not exposed by the current "
                    + "IResourceHelper contract. activateOnLoad must be true.");
            }

            SceneHandle handle = package.LoadSceneAsync(
                location,
                (UnityEngine.SceneManagement.LoadSceneMode)sceneMode,
                UnityEngine.SceneManagement.LocalPhysicsMode.None,
                true,
                priority);

            try
            {
                while (!handle.IsDone)
                {
                    onProgress?.Report(handle.Progress);
                    await Task.Yield();
                }

                await handle;
                if (handle.Status != EOperationStatus.Succeeded)
                {
                    throw new RFrameworkException(
                        $"YooAssetResourceHelper: load scene '{location}' failed. "
                        + $"Status: {handle.Status}, Error: {handle.Error}");
                }

                onProgress?.Report(1f);

                if (sceneMode == (int)UnityEngine.SceneManagement.LoadSceneMode.Single)
                {
                    ReleaseReplacedSceneHandles();
                }

                if (sceneHandles.TryGetValue(location, out SceneHandle previousHandle)
                    && previousHandle.IsValid)
                {
                    previousHandle.Release();
                }

                sceneHandles[location] = handle;
                RecordCacheAccess(location);
            }
            catch
            {
                if (handle.IsValid)
                {
                    handle.Release();
                }

                throw;
            }
        }

        /// <summary>
        /// 异步卸载场景并释放对应句柄。
        /// </summary>
        /// <param name="location">场景地址。</param>
        public override async Task UnloadSceneAsync(string location)
        {
            if (!sceneHandles.TryGetValue(location, out SceneHandle handle))
            {
                return;
            }

            UnloadSceneOperation operation = handle.UnloadSceneAsync();
            await operation;
            if (operation.Status != EOperationStatus.Succeeded)
            {
                throw new RFrameworkException(
                    $"YooAssetResourceHelper: unload scene '{location}' failed. "
                    + $"Status: {operation.Status}, Error: {operation.Error}");
            }

            sceneHandles.Remove(location);
        }

        /// <summary>
        /// 检查资源地址是否存在。
        /// </summary>
        /// <param name="location">资源地址。</param>
        /// <returns>存在返回 true。</returns>
        public override bool IsLocationValid(string location)
        {
            EnsureInitialized();
            return package.IsLocationValid(location);
        }

        /// <summary>
        /// 获取资源下载大小。
        /// </summary>
        /// <param name="location">资源地址。</param>
        /// <returns>下载字节数。</returns>
        public override long GetDownloadSize(string location)
        {
            EnsureInitialized();
            return package.GetDownloadSize(location);
        }

        private static InitializePackageOptions CreateInitializeOptions(
            string packageName,
            ResourcePlayMode playMode,
            string defaultHostServer,
            string fallbackHostServer,
            string cachePackageRoot)
        {
            switch (playMode)
            {
                case ResourcePlayMode.EditorSimulate:
#if UNITY_EDITOR
                    PackageBuildResult buildResult = EditorSimulateBuildInvoker.Build(
                        packageName, (int)EBundleType.VirtualAssetBundle);
                    return new EditorSimulateModeOptions
                    {
                        EditorFileSystemParameters =
                            FileSystemParameters.CreateDefaultEditorFileSystemParameters(
                                buildResult.PackageRootDirectory)
                    };
#else
                    throw new PlatformNotSupportedException(
                        "YooAssetResourceHelper: EditorSimulate is only available in Unity Editor.");
#endif

                case ResourcePlayMode.Offline:
                    return new OfflinePlayModeOptions
                    {
                        BuiltinFileSystemParameters =
                            FileSystemParameters.CreateDefaultBuiltinFileSystemParameters()
                    };

                case ResourcePlayMode.Host:
                    if (string.IsNullOrWhiteSpace(defaultHostServer))
                    {
                        throw new RFrameworkException(
                            "YooAssetResourceHelper: Host mode requires defaultHostServer.");
                    }

                    DefaultRemoteService remoteService =
                        new DefaultRemoteService(defaultHostServer, fallbackHostServer);
                    return new HostPlayModeOptions
                    {
                        BuiltinFileSystemParameters =
                            FileSystemParameters.CreateDefaultBuiltinFileSystemParameters(),
                        CacheFileSystemParameters =
                            FileSystemParameters.CreateDefaultSandboxFileSystemParameters(
                                remoteService,
                                cachePackageRoot)
                    };

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(playMode), playMode, "Unsupported resource play mode.");
            }
        }

        private static Type NormalizeRequestedType(Type assetType)
        {
            return assetType == null || assetType == typeof(object)
                ? typeof(UnityEngine.Object)
                : assetType;
        }

        private static Type ResolveYooAssetType(Type requestedType)
        {
            if (requestedType == typeof(byte[]) || requestedType == typeof(string))
            {
                return typeof(TextAsset);
            }

            if (!typeof(UnityEngine.Object).IsAssignableFrom(requestedType))
            {
                throw new RFrameworkException(
                    $"YooAssetResourceHelper: unsupported asset type '{requestedType.FullName}'. "
                    + "Only UnityEngine.Object, byte[] and string are supported.");
            }

            return requestedType;
        }

        private static object ConvertAsset(
            UnityEngine.Object assetObject,
            Type requestedType,
            string location)
        {
            if (requestedType == typeof(byte[]) || requestedType == typeof(string))
            {
                if (!(assetObject is TextAsset textAsset))
                {
                    throw new RFrameworkException(
                        $"YooAssetResourceHelper: '{location}' must be collected as a TextAsset "
                        + $"when loaded as {requestedType.Name}.");
                }

                return requestedType == typeof(byte[])
                    ? (object)CopyBytes(textAsset.bytes)
                    : textAsset.text;
            }

            if (assetObject == null || !requestedType.IsInstanceOfType(assetObject))
            {
                throw new RFrameworkException(
                    $"YooAssetResourceHelper: asset '{location}' is not compatible with "
                    + $"'{requestedType.FullName}'.");
            }

            return assetObject;
        }

        private static byte[] CopyBytes(byte[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<byte>();
            }

            byte[] copy = new byte[source.Length];
            Buffer.BlockCopy(source, 0, copy, 0, source.Length);
            return copy;
        }

        private void ReleaseReplacedSceneHandles()
        {
            foreach (KeyValuePair<string, SceneHandle> pair in sceneHandles)
            {
                if (pair.Value != null && pair.Value.IsValid)
                {
                    pair.Value.Release();
                }
            }

            sceneHandles.Clear();
        }

        private void EnsureInitialized()
        {
            if (!isInitialized || isDestroying || package == null)
            {
                throw new RFrameworkException(
                    "YooAssetResourceHelper: resource package is not initialized or is shutting down.");
            }
        }

        private async Task TryAutoTrimCacheAsync()
        {
            if (!autoClearCacheEnabled
                || currentPlayMode != ResourcePlayMode.Host
                || cacheController == null
                || package == null)
            {
                return;
            }

            try
            {
                long after = await cacheController.TrimAsync(
                    package,
                    maxCacheBytes);
                long before = cacheController.CacheSizeBeforeTrimBytes;
                if (before > maxCacheBytes || after > maxCacheBytes)
                {
                    Log.Info(
                        "YooAsset cache cleanup completed. Package: {0}, "
                        + "Before: {1:F2} MB, After: {2:F2} MB, Limit: {3:F2} MB.",
                        package.PackageName,
                        BytesToMegabytes(before),
                        BytesToMegabytes(after),
                        BytesToMegabytes(maxCacheBytes));
                }

                if (after > maxCacheBytes)
                {
                    Log.Warning(
                        "YooAsset cache remains above the configured limit. "
                        + "Package: {0}, Current: {1:F2} MB, Limit: {2:F2} MB.",
                        package.PackageName,
                        BytesToMegabytes(after),
                        BytesToMegabytes(maxCacheBytes));
                }
            }
            catch (Exception exception)
            {
                Log.Warning(
                    "YooAsset automatic cache cleanup failed and resource initialization "
                    + "will continue. Package: {0}, Error: {1}",
                    package.PackageName,
                    exception);
            }
        }

        private void RecordCacheAccess(string location)
        {
            try
            {
                if (cacheController == null)
                {
                    return;
                }

                cacheController.RecordAccess(location);
                AssetInfo assetInfo = package?.GetAssetInfo(location);
                if (assetInfo == null || !assetInfo.IsValid)
                {
                    return;
                }

                cacheController.RecordAccess(assetInfo.Address);
                cacheController.RecordAccess(assetInfo.AssetPath);
            }
            catch (Exception exception)
            {
                Log.Warning(
                    "YooAsset cache access record failed. Location: {0}, Error: {1}",
                    location,
                    exception.Message);
            }
        }

        private void FlushCacheUsage()
        {
            try
            {
                cacheController?.Flush();
            }
            catch (Exception exception)
            {
                Log.Warning(
                    "YooAsset cache usage flush failed: {0}",
                    exception.Message);
            }
        }

        private static double BytesToMegabytes(long bytes)
        {
            return bytes / (1024d * 1024d);
        }

        private static Task GetPendingCleanupTask()
        {
            lock (lifecycleLock)
            {
                return cleanupTask;
            }
        }

        private static Task QueueCleanup(ResourcePackage packageToDestroy, SceneHandle[] scenes)
        {
            lock (lifecycleLock)
            {
                cleanupTask = CleanupChainAsync(cleanupTask, packageToDestroy, scenes);
                return cleanupTask;
            }
        }

        private static async Task CleanupChainAsync(
            Task previousCleanup,
            ResourcePackage packageToDestroy,
            SceneHandle[] scenes)
        {
            try
            {
                await previousCleanup;
                await CleanupPackageAsync(packageToDestroy, scenes);
            }
            catch (Exception exception)
            {
                ReportCleanupError("YooAssetResourceHelper cleanup failed: {0}", exception);
            }
        }

        private static async Task CleanupPackageAsync(
            ResourcePackage packageToDestroy,
            SceneHandle[] scenes)
        {
            if (!YooAssets.IsInitialized)
            {
                return;
            }

            for (int i = 0; i < scenes.Length; i++)
            {
                if (!YooAssets.IsInitialized)
                {
                    return;
                }

                SceneHandle scene = scenes[i];
                if (scene == null || !scene.IsValid)
                {
                    continue;
                }

                try
                {
                    UnloadSceneOperation unloadOperation = scene.UnloadSceneAsync();
                    await unloadOperation;
                    if (!YooAssets.IsInitialized)
                    {
                        return;
                    }

                    if (unloadOperation.Status != EOperationStatus.Succeeded)
                    {
                        ReportCleanupError(
                            "YooAssetResourceHelper: unload scene during cleanup failed: {0}",
                            unloadOperation.Error);
                    }
                }
                catch (Exception exception)
                {
                    ReportCleanupError(
                        "YooAssetResourceHelper: unload scene during cleanup failed: {0}",
                        exception);
                }
            }

            if (packageToDestroy == null || !YooAssets.IsInitialized)
            {
                return;
            }

            string packageName = packageToDestroy.PackageName;
            DestroyPackageOperation destroyOperation = packageToDestroy.DestroyPackageAsync();
            await destroyOperation;
            if (destroyOperation.Status != EOperationStatus.Succeeded)
            {
                throw new RFrameworkException(
                    $"YooAssetResourceHelper: destroy package '{packageName}' failed. "
                    + $"Status: {destroyOperation.Status}, Error: {destroyOperation.Error}");
            }

            if (YooAssets.TryGetPackage(packageName, out ResourcePackage registeredPackage)
                && ReferenceEquals(registeredPackage, packageToDestroy))
            {
                YooAssets.RemovePackage(packageName);
            }
        }

        private static void ReportCleanupError(string format, params object[] args)
        {
            if (!RFrameworkLog.IsInitialized)
            {
                return;
            }

            try
            {
                Log.Error(format, args);
            }
            catch (RFrameworkException)
            {
                // 生命周期检查与实际写入之间，日志辅助器仍可能被关闭。
            }
        }

        private sealed class DefaultRemoteService : IRemoteService
        {
            private readonly string defaultHostServer;
            private readonly string fallbackHostServer;

            /// <summary>
            /// 创建 YooAsset 远端地址服务。
            /// </summary>
            /// <param name="defaultHostServer">主服务器地址。</param>
            /// <param name="fallbackHostServer">备用服务器地址。</param>
            public DefaultRemoteService(string defaultHostServer, string fallbackHostServer)
            {
                this.defaultHostServer = TrimTrailingSlash(defaultHostServer);
                this.fallbackHostServer = TrimTrailingSlash(fallbackHostServer);
            }

            /// <summary>
            /// 获取远端文件候选地址。
            /// </summary>
            /// <param name="fileName">YooAsset 请求的文件名。</param>
            /// <returns>按优先级排列的地址。</returns>
            public IReadOnlyList<string> GetRemoteUrls(string fileName)
            {
                if (string.IsNullOrWhiteSpace(fallbackHostServer)
                    || string.Equals(defaultHostServer, fallbackHostServer, StringComparison.Ordinal))
                {
                    return new[] { $"{defaultHostServer}/{fileName}" };
                }

                return new[]
                {
                    $"{defaultHostServer}/{fileName}",
                    $"{fallbackHostServer}/{fileName}"
                };
            }

            private static string TrimTrailingSlash(string url)
            {
                return string.IsNullOrWhiteSpace(url) ? string.Empty : url.TrimEnd('/');
            }
        }
    }
}
