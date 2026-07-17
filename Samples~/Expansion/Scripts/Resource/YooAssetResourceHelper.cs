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
    /// 完整实现 <see cref="ResourceHelperBase"/> 的所有抽象方法，
    /// 使用 YooAsset 管理资源加载、释放与场景加载。
    /// 依赖于 YooAsset 第三方库。
    /// </summary>
    public sealed class YooAssetResourceHelper : ResourceHelperBase
    {
        /// <summary>
        /// 资源包引用。由 InitializeAsync 中 YooAssets.CreatePackage() 创建。
        /// </summary>
        private ResourcePackage package;

        /// <summary>
        /// 资源路径 → AssetHandle 映射表。
        /// 用于追踪已加载资源的句柄，供 ReleaseAsset 时释放。
        /// </summary>
        private readonly Dictionary<AssetHandleKey, AssetHandle> assetHandles =
            new Dictionary<AssetHandleKey, AssetHandle>();

        /// <summary>
        /// 场景路径 → SceneHandle 映射表。
        /// 用于追踪已加载场景的句柄，供 UnloadSceneAsync 时卸载。
        /// </summary>
        private readonly Dictionary<string, SceneHandle> sceneHandles = new Dictionary<string, SceneHandle>();

        /// <summary>
        /// 资源系统是否已初始化。
        /// </summary>
        private bool isInitialized;

        private bool isDestroying;

        private readonly struct AssetHandleKey : IEquatable<AssetHandleKey>
        {
            public readonly string Location;
            public readonly Type AssetType;

            public AssetHandleKey(string location, Type assetType)
            {
                Location = location;
                AssetType = assetType;
            }

            public bool Equals(AssetHandleKey other)
            {
                return string.Equals(Location, other.Location) && AssetType == other.AssetType;
            }

            public override bool Equals(object obj)
            {
                return obj is AssetHandleKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Location != null ? Location.GetHashCode() : 0) * 397) ^
                           (AssetType != null ? AssetType.GetHashCode() : 0);
                }
            }
        }

        /// <summary>
        /// 初始化资源系统。
        /// 创建 YooAsset 资源包，根据运行模式构建对应的 InitializePackageOptions 并执行初始化。
        /// Host 模式下需要提供 CDN 地址用于远端资源下载。
        /// </summary>
        /// <param name="packageName">资源包裹名称。</param>
        /// <param name="playMode">资源运行模式。</param>
        /// <param name="defaultHostServer">默认 CDN 地址（Host 模式使用）。</param>
        /// <param name="fallbackHostServer">备用 CDN 地址。</param>
        public override async Task InitializeAsync(string packageName, ResourcePlayMode playMode,
            string defaultHostServer, string fallbackHostServer)
        {
            if (isInitialized)
            {
                return;
            }

            // 初始化 YooAssets 全局系统
            YooAssets.Initialize();

            // 创建资源包
            package = YooAssets.CreatePackage(packageName);

            // 根据运行模式选择初始化选项
            InitializePackageOptions options;
            switch (playMode)
            {
                case ResourcePlayMode.EditorSimulate:
                    {
                        // 编辑器模拟模式：使用编辑器内资源，无需构建资源包
                        options = new EditorSimulateModeOptions
                        {
                            EditorFileSystemParameters = FileSystemParameters.CreateDefaultEditorFileSystemParameters(
                                packageName)
                        };
                        break;
                    }
                case ResourcePlayMode.Offline:
                    {
                        // 离线模式：资源全部内置在 StreamingAssets 中
                        options = new OfflinePlayModeOptions
                        {
                            BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters()
                        };
                        break;
                    }
                case ResourcePlayMode.Host:
                    {
                        // 联机模式：内置资源 + 远程 CDN 更新 + 本地缓存
                        var remoteService = new DefaultRemoteService(defaultHostServer, fallbackHostServer);
                        options = new HostPlayModeOptions
                        {
                            BuiltinFileSystemParameters =
                                FileSystemParameters.CreateDefaultBuiltinFileSystemParameters(),
                            CacheFileSystemParameters =
                                FileSystemParameters.CreateDefaultSandboxFileSystemParameters(remoteService)
                        };
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(playMode), playMode,
                        "YooAssetResourceHelper: 不支持的 ResourcePlayMode。");
            }

            // 等待初始化完成
            InitializePackageOperation initOp = package.InitializePackageAsync(options);
            await initOp;

            if (initOp.Status != EOperationStatus.Succeeded)
            {
                throw new Exception(
                    $"YooAssetResourceHelper: 资源包 '{packageName}' 初始化失败，状态: {initOp.Status}，错误: {initOp.Error}");
            }

            isInitialized = true;
        }

        /// <summary>
        /// 销毁资源系统。
        /// 释放所有已追踪的 AssetHandle 和 SceneHandle，并异步销毁资源包。
        /// </summary>
        public override void Destroy()
        {
            if (isDestroying || !isInitialized)
            {
                return;
            }

            isDestroying = true;
            _ = DestroyAsync();
        }

        private async Task DestroyAsync()
        {
            try
            {
                // 释放所有资源句柄
                foreach (KeyValuePair<AssetHandleKey, AssetHandle> kv in assetHandles)
                {
                    kv.Value.Release();
                }

                assetHandles.Clear();

                // 必须按顺序等待场景卸载完成，再销毁包。DestroyPackageAsync 会清理包内调度器，
                // 若与 UnloadSceneAsync 并发，场景卸载操作会访问已销毁的包资源。
                foreach (KeyValuePair<string, SceneHandle> kv in sceneHandles)
                {
                    await kv.Value.UnloadSceneAsync();
                }

                sceneHandles.Clear();

                // 销毁资源包
                if (package != null)
                {
                    await package.DestroyPackageAsync();
                }

                package = null;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                assetHandles.Clear();
                sceneHandles.Clear();
                isInitialized = false;
                isDestroying = false;
            }
        }

        /// <summary>
        /// 异步加载资源并返回原始对象。
        /// 使用 YooAsset 的 ResourcePackage.LoadAssetAsync()，
        /// 加载完成后追踪句柄供后续释放。
        /// </summary>
        /// <param name="location">资源路径。</param>
        /// <param name="assetType">资源类型。</param>
        /// <param name="priority">加载优先级（越大越优先）。</param>
        /// <param name="ct">取消令牌，调用方已取消时应尽早中止底层加载。</param>
        /// <returns>加载的资源对象。</returns>
        public override async Task<object> LoadAssetAsync(string location, Type assetType, uint priority,
            CancellationToken ct = default)
        {
            EnsureInitialized();

            if (ct.IsCancellationRequested)
            {
                throw new OperationCanceledException(ct);
            }

            assetType = NormalizeAssetType(assetType);
            AssetHandle handle = package.LoadAssetAsync(location, assetType, priority);

            // HandleBase 内置 GetAwaiter()，可直接 await；await 期间轮询取消令牌，
            // 调用方取消时释放句柄并抛出 OperationCanceledException。
            try
            {
                while (!handle.IsDone)
                {
                    if (ct.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(ct);
                    }

                    await Task.Yield();
                }

                // 末次轮询后再次确认，避免 yield 间隙内被取消
                if (ct.IsCancellationRequested)
                {
                    throw new OperationCanceledException(ct);
                }

                await handle;
            }
            catch (OperationCanceledException)
            {
                handle.Release();
                throw;
            }

            if (handle.Status != EOperationStatus.Succeeded)
            {
                handle.Release();
                throw new Exception(
                    $"YooAssetResourceHelper: 加载资源 '{location}' 失败，状态: {handle.Status}，错误: {handle.Error}");
            }

            // 追踪句柄
            assetHandles[new AssetHandleKey(location, assetType)] = handle;

            return handle.AssetObject;
        }

        /// <summary>
        /// 同步加载资源并返回原始对象。
        /// 内部使用 LoadAssetSync 方法，定位同一资源路径的已加载句柄。
        /// </summary>
        /// <param name="location">资源路径。</param>
        /// <param name="assetType">资源类型。</param>
        /// <returns>加载的资源对象。</returns>
        public override object LoadAssetSync(string location, Type assetType)
        {
            EnsureInitialized();

            assetType = NormalizeAssetType(assetType);

            // 先检查是否已有缓存的句柄
            AssetHandleKey key = new AssetHandleKey(location, assetType);
            if (assetHandles.TryGetValue(key, out AssetHandle cachedHandle))
            {
                return cachedHandle.AssetObject;
            }

            // 使用 LoadAssetSync 同步加载
            AssetHandle handle = package.LoadAssetSync(location, assetType);

            if (handle.Status != EOperationStatus.Succeeded)
            {
                throw new Exception(
                    $"YooAssetResourceHelper: 同步加载资源 '{location}' 失败，状态: {handle.Status}，错误: {handle.Error}");
            }

            // 追踪句柄
            assetHandles[key] = handle;

            return handle.AssetObject;
        }

        /// <summary>
        /// 释放指定位置的资源句柄。
        /// 从追踪表中移除对应句柄并调用 Release。
        /// </summary>
        /// <param name="location">资源路径。</param>
        public override void ReleaseAsset(string location, Type assetType)
        {
            assetType = NormalizeAssetType(assetType);
            AssetHandleKey key = new AssetHandleKey(location, assetType);
            if (assetHandles.TryGetValue(key, out AssetHandle handle))
            {
                handle.Release();
                assetHandles.Remove(key);
            }
        }

        /// <summary>
        /// 异步加载场景。
        /// 使用 YooAsset 的 ResourcePackage.LoadSceneAsync()。
        /// </summary>
        /// <param name="location">场景资源路径。</param>
        /// <param name="sceneMode">场景加载模式：0=Single 替换当前场景，1=Additive 叠加到当前场景（与 UnityEngine.SceneManagement.LoadSceneMode 值一致）。</param>
        /// <param name="activateOnLoad">是否加载完成后立即激活。</param>
        /// <param name="priority">加载优先级。</param>
        /// <param name="onProgress">进度回调（0~1），可为 null。</param>
        public override async Task LoadSceneAsync(string location, int sceneMode,
            bool activateOnLoad, uint priority, IProgress<float> onProgress = null)
        {
            EnsureInitialized();

            if (!activateOnLoad)
            {
                throw new NotSupportedException(
                    "YooAssetResourceHelper does not support deferred scene activation. " +
                    "LoadSceneAsync must be called with activateOnLoad set to true.");
            }

            // v3 签名: LoadSceneAsync(string, LoadSceneMode, LocalPhysicsMode, bool, uint)
            SceneHandle handle = package.LoadSceneAsync(location,
                (UnityEngine.SceneManagement.LoadSceneMode)sceneMode,
                UnityEngine.SceneManagement.LocalPhysicsMode.None,
                true, priority);

            // 进度上报：SceneHandle.Progress 为 0~1，每帧轮询直到完成
            if (onProgress != null)
            {
                while (!handle.IsDone)
                {
                    onProgress.Report(handle.Progress);
                    await Task.Yield();
                }

                onProgress.Report(handle.Progress);
            }

            await handle;

            if (handle.Status != EOperationStatus.Succeeded)
            {
                throw new Exception(
                    $"YooAssetResourceHelper: 加载场景 '{location}' 失败，状态: {handle.Status}，错误: {handle.Error}");
            }

            // 追踪场景句柄
            if (sceneMode == (int)UnityEngine.SceneManagement.LoadSceneMode.Single)
            {
                sceneHandles.Clear();
            }
            sceneHandles[location] = handle;
        }

        /// <summary>
        /// 异步卸载场景。
        /// 从追踪表中取出 SceneHandle 并调用 UnloadAsync。
        /// </summary>
        /// <param name="location">场景资源路径。</param>
        public override async Task UnloadSceneAsync(string location)
        {
            if (sceneHandles.TryGetValue(location, out SceneHandle handle))
            {
                // v3: UnloadSceneAsync() 返回 UnloadSceneOperation，可直接 await
                await handle.UnloadSceneAsync();
                sceneHandles.Remove(location);
            }
        }

        /// <summary>
        /// 检查指定位置的资源是否存在于资源包裹中。
        /// </summary>
        /// <param name="location">资源路径。</param>
        /// <returns>存在返回 true，否则返回 false。</returns>
        public override bool IsLocationValid(string location)
        {
            EnsureInitialized();
            return package.IsLocationValid(location);
        }

        /// <summary>
        /// 获取指定资源的下载大小（字节）。
        /// 仅在 Host 模式下返回有效值。
        /// </summary>
        /// <param name="location">资源路径。</param>
        /// <returns>资源下载大小（字节）。</returns>
        public override long GetDownloadSize(string location)
        {
            EnsureInitialized();
            return package.GetDownloadSize(location);
        }

        /// <summary>
        /// 确保资源系统已初始化，否则抛出异常。
        /// </summary>
        private void EnsureInitialized()
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException(
                    "YooAssetResourceHelper: 资源系统尚未初始化，请先调用 InitializeAsync。");
            }
        }

        private static Type NormalizeAssetType(Type assetType)
        {
            return assetType == null || assetType == typeof(object) ? typeof(UnityEngine.Object) : assetType;
        }

        /// <summary>
        /// 默认远端资源地址查询服务。
        /// 实现 <see cref="IRemoteService"/> 接口，通过构造函数传入的 CDN 地址拼接远端 URL。
        /// </summary>
        private sealed class DefaultRemoteService : IRemoteService
        {
            /// <summary>
            /// CDN 地址列表。索引 0 为主地址，其余为备用地址。
            /// </summary>
            private readonly string[] urls;

            /// <summary>
            /// 创建默认远端资源地址查询服务。
            /// </summary>
            /// <param name="defaultHostServer">默认 CDN 地址。</param>
            /// <param name="fallbackHostServer">备用 CDN 地址。</param>
            public DefaultRemoteService(string defaultHostServer, string fallbackHostServer)
            {
                urls = new[] { defaultHostServer, fallbackHostServer };
            }

            /// <summary>
            /// 获取指定文件的所有远端候选地址列表。
            /// 按优先级排序：主 URL 在前，备用 URL 在后。
            /// </summary>
            /// <param name="fileName">资源文件名称。</param>
            /// <returns>按优先级排序的远端候选地址列表。</returns>
            public IReadOnlyList<string> GetRemoteUrls(string fileName)
            {
                // 按优先级返回：主 URL → 备用 URL
                return new string[] { $"{urls[0]}/{fileName}", $"{urls[1]}/{fileName}" };
            }
        }
    }
}
