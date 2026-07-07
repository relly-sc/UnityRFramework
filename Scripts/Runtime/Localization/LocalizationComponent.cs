using System.Threading.Tasks;
using RFramework;
using RFramework.Event;
using RFramework.Localization;
using RFramework.Resource;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 本地化组件。负责 Inspector 配置 + 纯转发到 LocalizationModule。
    /// </summary>
    [AddComponentMenu("UnityRFramework/Localization")]
    [DisallowMultipleComponent]
    public sealed class LocalizationComponent : UnityRFrameworkComponent
    {
        /// <summary>
        /// 本地化辅助器类型全名。
        /// </summary>
        [SerializeField]
        [Tooltip("本地化辅助器类型全名。必须是继承自 LocalizationHelperBase 的 MonoBehaviour。")]
        private string localizationHelperTypeName = "UnityRFramework.Runtime.DefaultLocalizationHelper";

        /// <summary>
        /// 本地化模块引用。
        /// </summary>
        private ILocalizationModule localizationModule;

        /// <summary>
        /// 当前语言代码。
        /// </summary>
        public string CurrentLanguage
        {
            get { return localizationModule != null ? localizationModule.CurrentLanguage : null; }
        }

        /// <inheritdoc/>
        protected override void Awake()
        {
            base.Awake();

            localizationModule = RFrameworkModuleEntry.GetModule<ILocalizationModule>();
            if (localizationModule == null)
            {
                Log.Error("Can not find module '{0}'.", nameof(ILocalizationModule));
                return;
            }

            IResourceModule resourceModule = RFrameworkModuleEntry.GetModule<IResourceModule>();
            IEventModule eventModule = RFrameworkModuleEntry.GetModule<IEventModule>();
            localizationModule.SetDependencies(resourceModule, eventModule);

            LocalizationHelperBase helper = Helper.CreateHelper<LocalizationHelperBase>(localizationHelperTypeName, null);
            if (helper != null)
            {
                localizationModule.SetHelper(helper);
                helper.transform.SetParent(transform);
            }
            else
            {
                Log.Error(
                    "LocalizationComponent: Helper type '{0}' is null. Configure in Inspector or call SetHelper().",
                    localizationHelperTypeName);
            }
        }

        /// <summary>
        /// 设置本地化辅助器（运行时替换）。
        /// </summary>
        public void SetHelper(ILocalizationHelper helper)
        {
            if (helper == null)
            {
                throw new RFrameworkException("LocalizationComponent: helper is invalid.");
            }

            localizationModule.SetHelper(helper);
        }

        /// <inheritdoc cref="ILocalizationModule.LoadLanguageAsync"/>
        public Task LoadLanguageAsync(string language)
        {
            return localizationModule.LoadLanguageAsync(language);
        }

        /// <inheritdoc cref="ILocalizationModule.SwitchLanguageAsync"/>
        public Task SwitchLanguageAsync(string language)
        {
            return localizationModule.SwitchLanguageAsync(language);
        }

        /// <inheritdoc cref="ILocalizationModule.UnloadLanguage"/>
        public void UnloadLanguage(string language)
        {
            localizationModule.UnloadLanguage(language);
        }

        /// <inheritdoc cref="ILocalizationModule.GetString(string)"/>
        public string GetString(string key)
        {
            return localizationModule.GetString(key);
        }

        /// <inheritdoc cref="ILocalizationModule.GetString(string, object[])"/>
        public string GetString(string key, params object[] args)
        {
            return localizationModule.GetString(key, args);
        }

        /// <inheritdoc cref="ILocalizationModule.HasString"/>
        public bool HasString(string key)
        {
            return localizationModule.HasString(key);
        }
    }
}
