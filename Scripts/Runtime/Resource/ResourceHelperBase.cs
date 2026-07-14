using System;
using System.Threading;
using System.Threading.Tasks;
using RFramework.Resource;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 资源辅助器基类。
    /// 继承自 MonoBehaviour 并实现 IResourceHelper 接口，使 Runtime 层资源辅助器
    /// 可以通过 <see cref="Helper.CreateHelper{T}(string, T)"/> 统一创建为场景中的 GameObject，
    /// 同时仍能以接口形式注入到 Library 层的 ResourceModule。
    /// </summary>
    public abstract class ResourceHelperBase : MonoBehaviour, IResourceHelper
    {
        /// <summary>
        /// 初始化资源系统。
        /// 内部完成资源系统的初始化、资源包裹创建与文件系统挂载。
        /// </summary>
        /// <param name="packageName">资源包裹名称。</param>
        /// <param name="playMode">资源运行模式。</param>
        /// <param name="defaultHostServer">默认 CDN 地址（Host 模式使用）。</param>
        /// <param name="fallbackHostServer">备用 CDN 地址。</param>
        public abstract Task InitializeAsync(string packageName, ResourcePlayMode playMode,
            string defaultHostServer, string fallbackHostServer);

        /// <summary>
        /// 销毁资源系统。
        /// 内部释放所有资源句柄、移除资源包裹并销毁资源系统。
        /// </summary>
        public abstract void Destroy();

        /// <summary>
        /// 异步加载资源并返回原始对象。
        /// 辅助器内部持有底层资源句柄，后续通过 ReleaseAsset 释放。
        /// </summary>
        /// <param name="location">资源路径。</param>
        /// <param name="assetType">资源类型。</param>
        /// <param name="priority">加载优先级（越大越优先）。</param>
        /// <param name="ct">取消令牌，调用方已取消时应尽早中止底层加载。</param>
        /// <returns>加载的资源对象。</returns>
        public abstract Task<object> LoadAssetAsync(string location, Type assetType, uint priority,
            CancellationToken ct = default);

        /// <summary>
        /// 同步加载资源并返回原始对象。
        /// </summary>
        /// <param name="location">资源路径。</param>
        /// <param name="assetType">资源类型。</param>
        /// <returns>加载的资源对象。</returns>
        public abstract object LoadAssetSync(string location, Type assetType);

        /// <summary>
        /// 释放指定位置的资源句柄。
        /// </summary>
        /// <param name="location">资源路径。</param>
        public abstract void ReleaseAsset(string location);

        /// <summary>
        /// 异步加载场景。
        /// </summary>
        /// <param name="location">场景资源路径。</param>
        /// <param name="sceneMode">场景加载模式：0=Single 替换当前场景，1=Additive 叠加到当前场景（与 UnityEngine.SceneManagement.LoadSceneMode 值一致）。</param>
        /// <param name="activateOnLoad">是否加载完成后立即激活。</param>
        /// <param name="priority">加载优先级。</param>
        /// <param name="onProgress">进度回调（0~1），可为 null。</param>
        public abstract Task LoadSceneAsync(string location, int sceneMode,
            bool activateOnLoad, uint priority, IProgress<float> onProgress = null);

        /// <summary>
        /// 异步卸载场景。
        /// </summary>
        /// <param name="location">场景资源路径。</param>
        public abstract Task UnloadSceneAsync(string location);

        /// <summary>
        /// 检查指定位置的资源是否存在于资源包裹中。
        /// </summary>
        /// <param name="location">资源路径。</param>
        public abstract bool IsLocationValid(string location);

        /// <summary>
        /// 获取指定资源的下载大小（字节）。
        /// </summary>
        /// <param name="location">资源路径。</param>
        public abstract long GetDownloadSize(string location);
    }
}
