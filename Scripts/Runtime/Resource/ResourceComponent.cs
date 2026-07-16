using System;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using UnityEngine;
using UnityEngine.SceneManagement;
// 消除 System.Object 与 UnityEngine.Object 的歧义
using Object = UnityEngine.Object;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 资源模块 Unity 组件。
    /// 负责 Inspector 配置注入（PlayMode、Server URL）并转发调用到 ResourceModule。
    /// 默认使用基于 Unity Resources API 的 DefaultResourceHelper；也可切换到
    /// LocalFileResourceHelper，按 persistentDataPath、StreamingAssets、Resources 的顺序
    /// 加载可替换文件，或在 Expansion 中接入 YooAsset 等实现。
    /// </summary>
    [AddComponentMenu("UnityRFramework/Resource Component")]
    public sealed class ResourceComponent : UnityRFrameworkComponent
    {
        /// <summary>
        /// 资源辅助器类型全名。
        /// 必须是继承自 <see cref="ResourceHelperBase"/> 的 MonoBehaviour 类型。
        /// 默认为基于 Resources.Load 的 DefaultResourceHelper，也可选择内置 LocalFileResourceHelper，
        /// 也可在启动流程中通过 SetHelper 方法运行时替换。
        /// </summary>
        [SerializeField]
        [Tooltip("资源辅助器类型全名。必须是继承自 ResourceHelperBase 的 MonoBehaviour。")]
        private string resourceHelperTypeName = "UnityRFramework.Runtime.DefaultResourceHelper";

        [SerializeField]
        [Tooltip("资源运行模式")]
        private ResourcePlayMode playMode = ResourcePlayMode.EditorSimulate;

        [SerializeField]
        [Tooltip("资源包裹名称")]
        private string packageName = "DefaultPackage";

        [SerializeField]
        [Tooltip("默认远程 CDN 地址（Host 模式必须）")]
        private string defaultHostServer = "";

        [SerializeField]
        [Tooltip("备用远程 CDN 地址")]
        private string fallbackHostServer = "";

        /// <summary>
        /// 资源模块引用，由 Awake 从 RFrameworkModuleEntry 获取并缓存。
        /// </summary>
        private IResourceModule resourceModule;

        /// <summary>
        /// 当前资源辅助器，用于访问 URL 解析等可选扩展能力。
        /// </summary>
        private IResourceHelper resourceHelper;

        protected override void Awake()
        {
            base.Awake();
            resourceModule = RFrameworkModuleEntry.GetModule<IResourceModule>();

            // 通过统一 Helper 创建器反射创建 MonoBehaviour 辅助器
            ResourceHelperBase helper = Helper.CreateHelper<ResourceHelperBase>(resourceHelperTypeName, null);
            if (helper != null)
            {
                helper.name = $"{helper.GetType().Name} (Resource Helper)";
                helper.transform.SetParent(transform);
                resourceHelper = helper;
                resourceModule.SetHelper(helper);
            }
            else
            {
                Log.Error(
                    "ResourceComponent: 资源辅助器类型 '{0}' 为 null。"
                    + "请在 Inspector 中配置 ResourceHelperTypeName 或在启动流程中调用 SetHelper()。",
                    resourceHelperTypeName);
            }

            // 配置资源运行参数
            resourceModule.SetPlayMode(playMode);
            resourceModule.SetPackageName(packageName);
            resourceModule.SetRemoteServiceUrl(defaultHostServer, fallbackHostServer);
        }

        /// <summary>
        /// 设置资源辅助器（替换 Inspector 中配置的默认 Helper）。
        /// 在启动流程中调用，传入真实 Helper 实现。
        /// </summary>
        /// <param name="helper">资源辅助器实例，为 null 时抛出异常。</param>
        public void SetHelper(IResourceHelper helper)
        {
            if (helper == null)
            {
                throw new RFrameworkException("ResourceComponent: helper is invalid.");
            }

            resourceHelper = helper;
            resourceModule.SetHelper(helper);
        }

        /// <summary>
        /// 初始化资源系统
        /// </summary>
        public Task InitializeAsync()
        {
            return resourceModule.InitializeAsync();
        }

        /// <summary>
        /// 异步加载资源
        /// </summary>
        public Task<T> LoadAssetAsync<T>(string location, uint priority = 0, CancellationToken ct = default)
            where T : class
        {
            return resourceModule.LoadAssetAsync<T>(location, priority, ct);
        }

        /// <summary>
        /// 同步加载资源
        /// </summary>
        public T LoadAssetSync<T>(string location) where T : class
        {
            return resourceModule.LoadAssetSync<T>(location);
        }

        /// <summary>
        /// 异步加载场景
        /// </summary>
        /// <param name="sceneMode">场景加载模式：0=Single 替换当前场景，1=Additive 叠加到当前场景（与 UnityEngine.SceneManagement.LoadSceneMode 值一致）</param>
        /// <param name="onProgress">进度回调（0~1），可为 null</param>
        public Task LoadSceneAsync(string location, int sceneMode = 0,
            bool activateOnLoad = true, uint priority = 0, IProgress<float> onProgress = null)
        {
            return resourceModule.LoadSceneAsync(location, sceneMode, activateOnLoad, priority, onProgress);
        }

        /// <summary>
        /// 异步卸载场景
        /// </summary>
        public Task UnloadSceneAsync(string location)
        {
            return resourceModule.UnloadSceneAsync(location);
        }

        /// <summary>
        /// 卸载资源
        /// </summary>
        public void UnloadAsset(Object asset)
        {
            resourceModule.UnloadAsset(asset);
        }

        /// <summary>
        /// 卸载非 UnityEngine.Object 资源，例如 byte[] 或 string。
        /// </summary>
        /// <param name="asset">资源模块返回的资源对象。</param>
        public void UnloadAsset(object asset)
        {
            resourceModule.UnloadAsset(asset);
        }

        /// <summary>
        /// 按资源路径和类型精确归还一次加载引用。
        /// </summary>
        public void UnloadAsset<T>(string location) where T : class
        {
            resourceModule.UnloadAsset<T>(location);
        }

        /// <summary>
        /// 释放未使用的资源
        /// </summary>
        public void UnloadUnusedAssets()
        {
            resourceModule.UnloadUnusedAssets();
        }

        /// <summary>
        /// 检查资源是否存在
        /// </summary>
        public bool HasAsset(string location)
        {
            return resourceModule.HasAsset(location);
        }

        /// <summary>
        /// 获取资源下载大小
        /// </summary>
        public long GetDownloadSize(string location)
        {
            return resourceModule.GetDownloadSize(location);
        }

        /// <summary>
        /// 获取资源的物理路径或 URL，可直接赋给 VideoPlayer.url 等 URL 消费方。
        /// 当前资源辅助器必须实现 IResourceUrlProvider。
        /// </summary>
        /// <param name="location">资源辅助器可识别的逻辑位置。</param>
        /// <returns>目标平台可访问的文件路径或 URL。</returns>
        public string GetAssetUrl(string location)
        {
            if (!(resourceHelper is IResourceUrlProvider provider))
            {
                throw new RFrameworkException(
                    $"Resource helper '{resourceHelper?.GetType().FullName}' does not provide asset URLs.");
            }

            return provider.GetAssetUrl(location);
        }
    }
}
