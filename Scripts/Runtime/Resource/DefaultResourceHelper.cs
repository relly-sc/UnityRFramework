using System;
using System.Collections.Generic;
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
        private readonly Dictionary<string, Object> loadedAssets = new Dictionary<string, Object>();

        /// <summary>
        /// 已加载场景路径集合，用于 UnloadSceneAsync 卸载。
        /// </summary>
        private readonly HashSet<string> loadedScenes = new HashSet<string>();

        /// <inheritdoc/>
        public override Task InitializeAsync(string packageName, ResourcePlayMode playMode,
            string defaultHostServer, string fallbackHostServer)
        {
            // Resources 模式无需初始化
            loadedAssets.Clear();
            loadedScenes.Clear();
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public override void Destroy()
        {
            foreach (var kv in loadedAssets)
            {
                if (kv.Value != null)
                {
                    Resources.UnloadAsset(kv.Value);
                }
            }

            loadedAssets.Clear();
            loadedScenes.Clear();
        }

        /// <inheritdoc/>
        public override Task<object> LoadAssetAsync(string location, Type assetType, uint priority)
        {
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
            string path = StripExtension(location);

            // 检查缓存
            if (loadedAssets.TryGetValue(path, out Object cached) && cached != null)
            {
                return cached;
            }

            Object asset = Resources.Load(path, assetType ?? typeof(Object));
            if (asset == null)
            {
                Log.Warning("DefaultResourceHelper: resource '{0}' not found in Resources.", path);
                return null;
            }

            loadedAssets[path] = asset;
            return asset;
        }

        /// <inheritdoc/>
        public override void ReleaseAsset(string location)
        {
            if (string.IsNullOrEmpty(location))
            {
                return;
            }

            string path = StripExtension(location);
            if (loadedAssets.TryGetValue(path, out Object asset))
            {
                loadedAssets.Remove(path);
                Resources.UnloadAsset(asset);
            }
        }

        /// <inheritdoc/>
        public override async Task LoadSceneAsync(string location, SceneLoadMode sceneMode,
            bool activateOnLoad, uint priority)
        {
            if (string.IsNullOrEmpty(location))
            {
                throw new ArgumentException("DefaultResourceHelper: scene location is null or empty.");
            }

            string sceneName = StripExtension(location);

            LoadSceneMode unityMode = sceneMode == SceneLoadMode.Additive
                ? LoadSceneMode.Additive
                : LoadSceneMode.Single;

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, unityMode);
            if (op == null)
            {
                throw new Exception(
                    $"DefaultResourceHelper: load scene '{sceneName}' failed, scene may not be in Build Settings.");
            }

            op.allowSceneActivation = activateOnLoad;
            op.priority = (int)priority;

            while (!op.isDone)
            {
                await Task.Yield();
            }

            loadedScenes.Add(sceneName);
        }

        /// <inheritdoc/>
        public override async Task UnloadSceneAsync(string location)
        {
            if (string.IsNullOrEmpty(location))
            {
                return;
            }

            string sceneName = StripExtension(location);
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

            string path = StripExtension(location);
            return Resources.Load(path, typeof(Object)) != null;
        }

        /// <inheritdoc/>
        public override long GetDownloadSize(string location)
        {
            // Resources 模式没有远端下载
            return 0;
        }

        /// <summary>
        /// 去掉文件扩展名，Resources.Load 不识别扩展名。
        /// 如 "Prefabs/Player.prefab" → "Prefabs/Player"。
        /// </summary>
        private static string StripExtension(string path)
        {
            int dot = path.LastIndexOf('.');
            if (dot > 0 && dot > path.LastIndexOf('/'))
            {
                return path.Substring(0, dot);
            }

            return path;
        }
    }
}
