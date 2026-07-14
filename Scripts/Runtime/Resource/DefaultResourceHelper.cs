using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RFramework.Resource;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 默认资源辅助器（基于 Resources.Load）。
    /// 适用于小体量快速原型项目，无需打资源包。
    /// 生产环境建议切换为 Expansion 层的 YooAssetResourceHelper。
    /// </summary>
    public sealed class DefaultResourceHelper : ResourceHelperBase
    {
        /// <summary>
        /// 已加载资源路径 → 对象引用映射，用于 ReleaseAsset 释放。
        /// </summary>
        private readonly Dictionary<AssetHandleKey, Object> loadedAssets =
            new Dictionary<AssetHandleKey, Object>();

        /// <summary>
        /// 同一 Unity 对象可能以不同类型请求加载。仅当所有类型视图均释放后，才可调用
        /// Resources.UnloadAsset，避免提前卸载仍被另一条 ResourceModule 缓存使用的对象。
        /// </summary>
        private readonly Dictionary<Object, int> assetReferenceCounts = new Dictionary<Object, int>();

        /// <summary>
        /// 已加载场景路径集合，用于 UnloadSceneAsync 卸载。
        /// </summary>
        private readonly HashSet<string> loadedScenes = new HashSet<string>();

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

        /// <inheritdoc/>
        public override Task InitializeAsync(string packageName, ResourcePlayMode playMode,
            string defaultHostServer, string fallbackHostServer)
        {
            // Resources 模式无需初始化
            loadedAssets.Clear();
            assetReferenceCounts.Clear();
            loadedScenes.Clear();
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override void Destroy()
        {
            foreach (var kv in assetReferenceCounts)
            {
                // 仅个体资源（贴图/材质/音频/字体等）可被 Resources.UnloadAsset 卸载；
                // 预制体(GameObject/Component) 不支持该 API，需交由 Resources.UnloadUnusedAssets 回收。
                if (kv.Key != null && !(kv.Key is GameObject) && !(kv.Key is Component))
                {
                    Resources.UnloadAsset(kv.Key);
                }
            }

            loadedAssets.Clear();
            assetReferenceCounts.Clear();
            loadedScenes.Clear();
        }

        /// <inheritdoc/>
        public override Task<object> LoadAssetAsync(string location, Type assetType, uint priority,
            CancellationToken ct = default)
        {
            // 同步路径无法真正中断 Resources.Load，但需在调用方已取消时尽早返回取消，
            // 由上层 ResourceModule 统一处理"首调用者取消"语义（不再发起无意义同步加载）。
            if (ct.IsCancellationRequested)
            {
                return Task.FromCanceled<object>(ct);
            }

            return Task.FromResult(LoadAssetSync(location, assetType));
        }

        /// <inheritdoc/>
        public override object LoadAssetSync(string location, Type assetType)
        {
            if (string.IsNullOrEmpty(location))
            {
                return null;
            }

            // 去扩展名，Resources.Load 不接受扩展名
            string path = location.StripExtension();
            AssetHandleKey key = new AssetHandleKey(path, assetType ?? typeof(Object));

            // 检查缓存
            if (loadedAssets.TryGetValue(key, out Object cached) && cached != null)
            {
                return cached;
            }

            Object asset = Resources.Load(path, assetType ?? typeof(Object));
            if (asset == null)
            {
                Log.Warning("DefaultResourceHelper: resource '{0}' not found in Resources.", path);
                return null;
            }

            loadedAssets[key] = asset;
            if (assetReferenceCounts.TryGetValue(asset, out int referenceCount))
            {
                assetReferenceCounts[asset] = referenceCount + 1;
            }
            else
            {
                assetReferenceCounts.Add(asset, 1);
            }
            return asset;
        }

        /// <inheritdoc/>
        public override void ReleaseAsset(string location, Type assetType)
        {
            if (string.IsNullOrEmpty(location))
            {
                return;
            }

            string path = location.StripExtension();
            AssetHandleKey key = new AssetHandleKey(path, assetType ?? typeof(Object));
            if (loadedAssets.TryGetValue(key, out Object asset))
            {
                loadedAssets.Remove(key);

                if (assetReferenceCounts.TryGetValue(asset, out int referenceCount))
                {
                    if (referenceCount > 1)
                    {
                        assetReferenceCounts[asset] = referenceCount - 1;
                        return;
                    }

                    assetReferenceCounts.Remove(asset);
                }

                // 预制体(GameObject/Component) 不能用 Resources.UnloadAsset 卸载，
                // 否则引擎报错 "UnloadAsset may only be used on individual assets..." 且不会真正释放，
                // 交由 Resources.UnloadUnusedAssets 统一回收即可。
                if (asset is GameObject || asset is Component)
                {
                    return;
                }

                Resources.UnloadAsset(asset);
            }
        }

        /// <inheritdoc/>
        public override async Task LoadSceneAsync(string location, int sceneMode,
            bool activateOnLoad, uint priority, IProgress<float> onProgress = null)
        {
            if (string.IsNullOrEmpty(location))
            {
                throw new ArgumentException("DefaultResourceHelper: scene location is null or empty.");
            }

            if (!activateOnLoad)
            {
                throw new NotSupportedException(
                    "DefaultResourceHelper does not support deferred scene activation. " +
                    "LoadSceneAsync must be called with activateOnLoad set to true.");
            }

            string sceneName = location.StripExtension();



            LoadSceneMode unityMode = (LoadSceneMode)sceneMode;

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, unityMode);
            if (op == null)
            {
                throw new Exception(
                    $"DefaultResourceHelper: load scene '{sceneName}' failed, scene may not be in Build Settings.");
            }

            op.allowSceneActivation = true;
            op.priority = (int)priority;

            while (!op.isDone)
            {
                onProgress?.Report(op.progress);
                await Task.Yield();
            }

            // 报告最终进度（isDone 时 progress 通常为 1，但 Unity 有时停在 0.9，强制补 1）
            onProgress?.Report(1f);

            loadedScenes.Add(sceneName);
        }

        /// <inheritdoc/>
        public override async Task UnloadSceneAsync(string location)
        {
            if (string.IsNullOrEmpty(location))
            {
                return;
            }

            string sceneName = location.StripExtension();
            if (!loadedScenes.Contains(sceneName))
            {
                return;
            }

            AsyncOperation op = SceneManager.UnloadSceneAsync(sceneName);
            if (op != null)
            {
                while (!op.isDone)
                {
                    await Task.Yield();
                }
            }

            loadedScenes.Remove(sceneName);
        }

        /// <inheritdoc/>
        public override bool IsLocationValid(string location)
        {
            if (string.IsNullOrEmpty(location))
            {
                return false;
            }

            string path = location.StripExtension();
            return Resources.Load(path, typeof(Object)) != null;
        }

        /// <inheritdoc/>
        public override long GetDownloadSize(string location)
        {
            // Resources 模式没有远端下载
            return 0;
        }


    }
}
