using System.Threading;
using System.Threading.Tasks;
using RFramework;
using RFramework.Resource;
using UnityEngine;
using UnityEngine.SceneManagement;
// 消除 System.Object 与 UnityEngine.Object 的歧义
using Object = UnityEngine.Object;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 资源模块 Unity 组件。
    /// 负责 Inspector 配置注入（PlayMode、Server URL）并转发调用到 ResourceModule。
    /// 默认 HelperTypeName 指向 DefaultResourceHelper（不包含任何实现），
    /// 请在 Inspector 中配置 ResourceHelperTypeName 或在启动流程中调用 SetHelper() 注入真实实现。
    /// </summary>
    [AddComponentMenu("UnityRFramework/Resource Component")]
    public sealed class ResourceComponent : UnityRFrameworkComponent
    {
        /// <summary>
        /// 资源辅助器类型全名。
        /// 必须是继承自 <see cref="ResourceHelperBase"/> 的 MonoBehaviour 类型。
        /// 默认为 DefaultResourceHelper（不含实现），请配置为真实 Helper 类型全名，
        /// 或在启动流程中通过 SetHelper 方法运行时替换。
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

        private IResourceModule resourceModule;

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
            where T : Object
        {
            return resourceModule.LoadAssetAsync<T>(location, priority, ct);
        }

        /// <summary>
        /// 同步加载资源
        /// </summary>
        public T LoadAssetSync<T>(string location) where T : Object
        {
            return resourceModule.LoadAssetSync<T>(location);
        }

        /// <summary>
        /// 异步加载场景
        /// </summary>
        public Task LoadSceneAsync(string location, LoadSceneMode sceneMode = LoadSceneMode.Single,
            bool activateOnLoad = true, uint priority = 0)
        {
            SceneLoadMode mode = sceneMode == LoadSceneMode.Additive ? SceneLoadMode.Additive : SceneLoadMode.Single;
            return resourceModule.LoadSceneAsync(location, mode, activateOnLoad, priority);
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
    }
}
