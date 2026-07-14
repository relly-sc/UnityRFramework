using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 本地化 Runtime 组件。通过 ResourceComponent 加载每语言一个 TextAsset，
    /// 再交给 ILocalizationHelper 解析并缓存到 Library 模块。
    /// </summary>
    [AddComponentMenu("UnityRFramework/Localization")]
    [DisallowMultipleComponent]
    public sealed class LocalizationComponent : UnityRFrameworkComponent
    {
        private const string FallbackLanguage = "zh-CN";

        [SerializeField]
        [Tooltip("本地化解析辅助器类型。默认按 UTF-8 JSON 解析。")]
        private string localizationHelperTypeName = "UnityRFramework.Runtime.DefaultLocalizationHelper";

        [SerializeField]
        [Tooltip("默认语言代码。未指定时使用 zh-CN。")]
        private string defaultLanguage = "zh-CN";

        [SerializeField]
        [Tooltip("语言文件根路径。默认对应 Resources/Localization。")]
        private string languageAssetRoot = "Localization";

        [SerializeField]
        [Tooltip("语言文件扩展名。JSON 使用 .json，BinaryLocalizationHelper 使用 .bytes。")]
        private string languageFileExtension = ".json";

        private readonly Dictionary<string, Task> pendingLanguageLoads = new Dictionary<string, Task>();
        private readonly SemaphoreSlim switchSemaphore = new SemaphoreSlim(1, 1);

        private ILocalizationModule localizationModule;
        private CancellationTokenSource lifetimeCts;

        public string CurrentLanguage => localizationModule?.CurrentLanguage;

        public IReadOnlyList<string> SupportedLanguages =>
            localizationModule != null ? localizationModule.SupportedLanguages : Array.Empty<string>();

        public int LoadedLanguageCount => localizationModule?.LoadedLanguageCount ?? 0;

        protected override void Awake()
        {
            base.Awake();
            lifetimeCts = new CancellationTokenSource();

            localizationModule = RFrameworkModuleEntry.GetModule<ILocalizationModule>();
            if (localizationModule == null)
            {
                Log.Error("Can not find module '{0}'.", nameof(ILocalizationModule));
                return;
            }

            LocalizationHelperBase helper = Helper.CreateHelper<LocalizationHelperBase>(
                localizationHelperTypeName, null);
            if (helper != null)
            {
                helper.name = $"{helper.GetType().Name} (Localization Helper)";
                helper.transform.SetParent(transform);
                localizationModule.SetHelper(helper);
            }
            else
            {
                Log.Error(
                    "LocalizationComponent: Helper type '{0}' is null. Configure it in Inspector or call SetHelper().",
                    localizationHelperTypeName);
            }
        }

        private void Start()
        {
            string language = string.IsNullOrWhiteSpace(defaultLanguage)
                ? FallbackLanguage
                : defaultLanguage.Trim();
            _ = LoadDefaultLanguageAsync(language);
        }

        private void OnDestroy()
        {
            if (lifetimeCts == null)
            {
                return;
            }

            lifetimeCts.Cancel();
            lifetimeCts.Dispose();
            lifetimeCts = null;
            pendingLanguageLoads.Clear();
        }

        public void SetHelper(ILocalizationHelper helper)
        {
            if (helper == null)
            {
                throw new RFrameworkException("LocalizationComponent: helper is invalid.");
            }

            localizationModule.SetHelper(helper);
        }

        /// <summary>
        /// 通过 ResourceComponent 加载并解析一个语言文件，不切换当前语言。
        /// 同语言并发加载会共享同一个任务。
        /// </summary>
        public async Task LoadLanguageAsync(string language)
        {
            ValidateLanguage(language);
            if (localizationModule.HasLanguage(language))
            {
                return;
            }

            if (!pendingLanguageLoads.TryGetValue(language, out Task loadTask))
            {
                loadTask = LoadLanguageAssetInternalAsync(language, lifetimeCts.Token);
                pendingLanguageLoads.Add(language, loadTask);
            }

            try
            {
                await loadTask;
            }
            finally
            {
                if (pendingLanguageLoads.TryGetValue(language, out Task current)
                    && ReferenceEquals(current, loadTask))
                {
                    pendingLanguageLoads.Remove(language);
                }
            }
        }

        /// <summary>
        /// 从 JSON 字符串解析并缓存语言包，不经过资源模块。
        /// </summary>
        public void LoadLanguageFromString(string language, string json)
        {
            localizationModule.LoadLanguageFromString(language, json);
        }

        /// <summary>
        /// 确保目标语言已加载，然后串行切换当前语言。
        /// </summary>
        public async Task SwitchLanguageAsync(string language)
        {
            ValidateLanguage(language);
            CancellationToken token = lifetimeCts.Token;
            await switchSemaphore.WaitAsync(token);
            try
            {
                await LoadLanguageAsync(language);
                token.ThrowIfCancellationRequested();
                localizationModule.SwitchLanguage(language);
            }
            finally
            {
                switchSemaphore.Release();
            }
        }

        /// <summary>
        /// 同步切换到已经加载的语言。
        /// </summary>
        public void SwitchLanguage(string language)
        {
            localizationModule.SwitchLanguage(language);
        }

        public bool HasLanguage(string language)
        {
            return localizationModule != null && localizationModule.HasLanguage(language);
        }

        public void UnloadLanguage(string language)
        {
            localizationModule.UnloadLanguage(language);
        }

        public string GetString(string key)
        {
            return localizationModule.GetString(key);
        }

        public string GetString(string key, params object[] args)
        {
            return localizationModule.GetString(key, args);
        }

        public bool HasString(string key)
        {
            return localizationModule.HasString(key);
        }

        public string GetLanguageAssetPath(string language)
        {
            ValidateLanguage(language);
            string root = (languageAssetRoot ?? string.Empty).Trim().Trim('/', '\\');
            string extension = string.IsNullOrWhiteSpace(languageFileExtension)
                ? ".json"
                : languageFileExtension.Trim();
            if (!extension.StartsWith(".", StringComparison.Ordinal))
            {
                extension = "." + extension;
            }

            string fileName = language + extension;
            return string.IsNullOrEmpty(root) ? fileName : root + "/" + fileName;
        }

        private async Task LoadLanguageAssetInternalAsync(string language, CancellationToken ct)
        {
            ResourceComponent resource = GameEntry.Resource;
            if (resource == null)
            {
                throw new RFrameworkException(
                    "LocalizationComponent: ResourceComponent is required to load language files.");
            }

            await resource.InitializeAsync();
            ct.ThrowIfCancellationRequested();

            string assetPath = GetLanguageAssetPath(language);
            TextAsset textAsset = await resource.LoadAssetAsync<TextAsset>(assetPath, 0, ct);
            if (textAsset == null)
            {
                throw new RFrameworkException(
                    $"LocalizationComponent: Failed to load language asset '{assetPath}'.");
            }

            try
            {
                localizationModule.LoadLanguage(language, textAsset.bytes);
            }
            finally
            {
                resource.UnloadAsset<TextAsset>(assetPath);
            }
        }

        private async Task LoadDefaultLanguageAsync(string language)
        {
            try
            {
                await SwitchLanguageAsync(language);
            }
            catch (OperationCanceledException) when (lifetimeCts == null || lifetimeCts.IsCancellationRequested)
            {
                // 组件销毁时的预期取消。
            }
            catch (Exception ex)
            {
                Log.Error(
                    "LocalizationComponent: Failed to load default language '{0}'. {1}", language, ex.Message);
            }
        }

        private static void ValidateLanguage(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                throw new RFrameworkException("LocalizationComponent: language code is invalid.");
            }
        }
    }
}
