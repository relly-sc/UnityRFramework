using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 配置模块 Unity 组件。
    /// 负责编排配置加载流程（ResourceModule 加载字节 → ConfigModule 解析缓存），
    /// 并通过 <see cref="Helper.CreateHelper{T}(string, T)"/> 或 SetHelper 方法注入 IConfigHelper 实现。
    /// 默认 HelperTypeName 指向 DefaultConfigHelper（UTF-8 JSON 字节 + JSON 字符串），
    /// 也可在 Inspector 中配置其他 Helper，或在启动流程中调用 SetHelper() 注入实现。
    /// </summary>
    [AddComponentMenu("UnityRFramework/Config Component")]
    public sealed class ConfigComponent : UnityRFrameworkComponent
    {
        /// <summary>
        /// 配置辅助器类型全名。
        /// 必须是继承自 <see cref="ConfigHelperBase"/> 的 MonoBehaviour 类型。
        /// 默认为 DefaultConfigHelper（UTF-8 JSON 字节 + JSON 字符串），
        /// 可在启动流程中通过 SetHelper 方法运行时替换。
        /// </summary>
        [SerializeField]
        [Tooltip("配置辅助器类型全名。必须是继承自 ConfigHelperBase 的 MonoBehaviour。")]
        private string configHelperTypeName = "UnityRFramework.Runtime.DefaultConfigHelper";

        /// <summary>
        /// 配置模块引用，由 Awake 从 RFrameworkModuleEntry 获取并缓存。
        /// </summary>
        private IConfigModule configModule;

        protected override void Awake()
        {
            base.Awake();
            configModule = RFrameworkModuleEntry.GetModule<IConfigModule>();

            // IL2CPP 构建时 [SerializeField] private 字段可能因 stripping 被损毁，
            // 防御性检查：值异常时回退到代码默认值
            if (string.IsNullOrEmpty(configHelperTypeName) || configHelperTypeName.StartsWith("System."))
            {
                configHelperTypeName = "UnityRFramework.Runtime.DefaultConfigHelper";
            }

            // 通过统一 Helper 创建器反射创建 MonoBehaviour 辅助器
            ConfigHelperBase helper = Helper.CreateHelper<ConfigHelperBase>(configHelperTypeName, null);
            if (helper != null)
            {
                helper.name = $"{helper.GetType().Name} (Config Helper)";
                helper.transform.SetParent(transform);
                configModule.SetHelper(helper);
            }
            else
            {
                Log.Error(
                    "ConfigComponent: 配置辅助器类型 '{0}' 为 null。"
                    + "请在 Inspector 中配置 ConfigHelperTypeName 或在启动流程中调用 SetHelper()。",
                    configHelperTypeName);
            }
        }

        /// <summary>
        /// 设置配置辅助器（替换 Inspector 中配置的默认 Helper）。
        /// 在启动流程中调用，传入真实 Helper 实现。
        /// </summary>
        /// <param name="helper">配置辅助器实例，为 null 时抛出异常。</param>
        public void SetHelper(IConfigHelper helper)
        {
            if (helper == null)
            {
                throw new RFrameworkException("ConfigComponent: helper is invalid.");
            }

            configModule.SetHelper(helper);
        }

        /// <summary>
        /// 异步加载配置表。
        /// 通过 ResourceComponent 加载 TextAsset 字节数据，再调用 ConfigModule 解析并缓存。
        /// DefaultConfigHelper 按 UTF-8 JSON 解析，BinaryConfigHelper 按 URFC v1/v2 解析；
        /// 自定义 Helper 可定义自己的字节格式。
        /// </summary>
        /// <typeparam name="T">配置行类型（如 ItemConfig）。</typeparam>
        /// <param name="assetPath">配置资源路径（如 .json 或 .bytes）。</param>
        /// <param name="ct">取消令牌。</param>
        public async Task LoadConfigAsync<T>(string assetPath, CancellationToken ct = default) where T : class
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                throw new RFrameworkException("ConfigComponent: assetPath is null or empty.");
            }

            // 通过 ResourceComponent 加载字节数据
            ResourceComponent resource = GameEntry.Resource;
            if (resource == null)
            {
                throw new RFrameworkException(
                    "ConfigComponent: ResourceComponent not found. Ensure Resource module is initialized first.");
            }

            await resource.InitializeAsync();
            TextAsset textAsset = await resource.LoadAssetAsync<TextAsset>(assetPath, 0, ct);
            if (textAsset == null)
            {
                throw new RFrameworkException(
                    $"ConfigComponent: Failed to load config asset '{assetPath}'.");
            }

            try
            {
                configModule.LoadConfig<T>(textAsset.bytes);
            }
            finally
            {
                resource.UnloadAsset<TextAsset>(assetPath);
            }
        }

        /// <summary>
        /// 从已加载的原始字节解析配置表。格式由当前 IConfigHelper 决定。
        /// </summary>
        public void LoadConfig<T>(byte[] bytes) where T : class
        {
            configModule.LoadConfig<T>(bytes);
        }

        /// <summary>
        /// 从 JSON 字符串加载配置表。适用于运行时动态生成配置、编辑器预览等场景。
        /// </summary>
        /// <typeparam name="T">配置行类型。</typeparam>
        /// <param name="json">JSON 字符串。</param>
        public void LoadConfigFromString<T>(string json) where T : class
        {
            configModule.LoadConfigFromString<T>(json);
        }

        /// <summary>
        /// 卸载指定类型的配置表。
        /// </summary>
        public void UnloadConfig<T>() where T : class
        {
            configModule.UnloadConfig<T>();
        }

        /// <summary>
        /// 卸载所有已加载的配置表。
        /// </summary>
        public void UnloadAllConfigs()
        {
            configModule.UnloadAllConfigs();
        }

        /// <summary>
        /// 检查指定类型的配置表是否已加载。
        /// </summary>
        public bool HasConfig<T>() where T : class
        {
            return configModule.HasConfig<T>();
        }

        /// <summary>
        /// 获取指定 ID 的配置行。
        /// </summary>
        public T GetConfig<T>(int id) where T : class
        {
            return configModule.GetConfig<T>(id);
        }

        /// <summary>
        /// 检查是否存在指定 ID 的配置行。
        /// </summary>
        public bool HasConfigRow<T>(int id) where T : class
        {
            return configModule.HasConfigRow<T>(id);
        }

        /// <summary>
        /// 获取所有配置行。
        /// </summary>
        public IReadOnlyList<T> GetAllConfigs<T>() where T : class
        {
            return configModule.GetAllConfigs<T>();
        }

        /// <summary>
        /// 获取当前已加载的配置表数量。
        /// </summary>
        public int ConfigCount
        {
            get { return configModule.ConfigCount; }
        }
    }
}
