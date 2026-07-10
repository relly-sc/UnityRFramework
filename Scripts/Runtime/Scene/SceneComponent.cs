using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using RFramework.Event;
using RFramework.Resource;
using RFramework.Scene;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 场景组件。作为 SceneModule 的运行时包装层，绑定 Unity 生命周期，
    /// 转发所有场景操作到 SceneModule。
    /// Update/Shutdown 由 BaseComponent → RFrameworkModuleEntry 统一调度，
    /// 本组件不写 Update/OnDestroy。
    /// </summary>
    [AddComponentMenu("UnityRFramework/Scene")]
    [DisallowMultipleComponent]
    public sealed class SceneComponent : UnityRFrameworkComponent
    {
        /// <summary>
        /// 场景模块引用，由 Awake 从 RFrameworkModuleEntry 获取并缓存。
        /// </summary>
        private ISceneModule sceneModule;

        /// <summary>
        /// 获取当前主场景名称。
        /// </summary>
        public string CurrentSceneName
        {
            get { return sceneModule != null ? sceneModule.CurrentSceneName : null; }
        }

        /// <summary>
        /// 获取已加载的场景数量。
        /// </summary>
        public int LoadedSceneCount
        {
            get { return sceneModule != null ? sceneModule.LoadedSceneCount : 0; }
        }

        /// <summary>
        /// 获取正在加载的场景数量。
        /// </summary>
        public int LoadingSceneCount
        {
            get { return sceneModule != null ? sceneModule.LoadingSceneCount : 0; }
        }

        protected override void Awake()
        {
            base.Awake();

            sceneModule = RFrameworkModuleEntry.GetModule<ISceneModule>();
            if (sceneModule == null)
            {
                Log.Error("Can not find module '{0}'.", nameof(ISceneModule));
                return;
            }

            // 注入依赖模块（通过接口调用，无需类型转换）
            IResourceModule resourceModule = RFrameworkModuleEntry.GetModule<IResourceModule>();
            IEventModule eventModule = RFrameworkModuleEntry.GetModule<IEventModule>();
            sceneModule.SetDependencies(resourceModule, eventModule);
        }

        /// <inheritdoc cref="ISceneModule.LoadSceneAsync"/>
        public Task LoadSceneAsync(string assetName, int sceneMode = 0,
            bool activateOnLoad = true, uint priority = 0, IProgress<float> onProgress = null,
            object userData = null, CancellationToken ct = default)
        {
            return sceneModule.LoadSceneAsync(assetName, sceneMode, activateOnLoad, priority, onProgress,
                userData, ct);
        }

        /// <inheritdoc cref="ISceneModule.UnloadSceneAsync"/>
        public Task UnloadSceneAsync(string assetName, object userData = null)
        {
            return sceneModule.UnloadSceneAsync(assetName, userData);
        }

        /// <inheritdoc cref="ISceneModule.IsLoaded"/>
        public bool IsLoaded(string assetName)
        {
            return sceneModule.IsLoaded(assetName);
        }

        /// <inheritdoc cref="ISceneModule.IsLoading"/>
        public bool IsLoading(string assetName)
        {
            return sceneModule.IsLoading(assetName);
        }

        /// <inheritdoc cref="ISceneModule.IsUnloading"/>
        public bool IsUnloading(string assetName)
        {
            return sceneModule.IsUnloading(assetName);
        }

        /// <inheritdoc cref="ISceneModule.GetLoadedSceneNames()"/>
        public string[] GetLoadedSceneNames()
        {
            return sceneModule.GetLoadedSceneNames();
        }

        /// <inheritdoc cref="ISceneModule.GetLoadedSceneNames(List{string})"/>
        public void GetLoadedSceneNames(List<string> results)
        {
            sceneModule.GetLoadedSceneNames(results);
        }

        /// <inheritdoc cref="ISceneModule.GetLoadingSceneNames()"/>
        public string[] GetLoadingSceneNames()
        {
            return sceneModule.GetLoadingSceneNames();
        }

        /// <inheritdoc cref="ISceneModule.GetLoadingSceneNames(List{string})"/>
        public void GetLoadingSceneNames(List<string> results)
        {
            sceneModule.GetLoadingSceneNames(results);
        }
    }
}
