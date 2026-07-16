using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 本地化 Runtime 组件。通过 ResourceComponent 加载每语言文件的原始字节，
    /// 再交给 ILocalizationHelper 解析并缓存到 Library 模块。
    /// </summary>
    [AddComponentMenu("UnityRFramework/Localization")]
    [DisallowMultipleComponent]
    public sealed class LocalizationComponent : UnityRFrameworkComponent
    {
        private const string FallbackLanguage = "zh-CN";
        private const string JsonHelperTypeName =
            "UnityRFramework.Runtime.JsonLocalizationHelper";
        private const string LegacyJsonHelperTypeName =
            "UnityRFramework.Runtime.DefaultLocalizationHelper";
        [SerializeField]
        [Tooltip("本地化解析辅助器类型。默认按 UTF-8 JSON 解析。")]
        private string localizationHelperTypeName = JsonHelperTypeName;

        [SerializeField]
        [Tooltip("默认语言代码。未指定时使用 zh-CN。")]
        private string defaultLanguage = "zh-CN";

        [SerializeField]
        [Tooltip("是否在 Start 时使用内置位置约定自动加载并切换默认语言。")]
        private bool loadDefaultLanguageOnStart = true;

        private readonly Dictionary<LanguageLoadKey, Task> pendingLanguageLoads =
            new Dictionary<LanguageLoadKey, Task>();
        private readonly SemaphoreSlim switchSemaphore = new SemaphoreSlim(1, 1);

        private ILocalizationModule localizationModule;
        private ILocalizationHelper localizationHelper;
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

            if (string.IsNullOrEmpty(localizationHelperTypeName)
                || string.Equals(localizationHelperTypeName, LegacyJsonHelperTypeName,
                    StringComparison.Ordinal))
            {
                localizationHelperTypeName = JsonHelperTypeName;
            }

            LocalizationHelperBase helper = Helper.CreateHelper<LocalizationHelperBase>(
                localizationHelperTypeName, null);
            if (helper != null)
            {
                helper.name = $"{helper.GetType().Name} (Localization Helper)";
                helper.transform.SetParent(transform);
                localizationHelper = helper;
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
            if (!loadDefaultLanguageOnStart)
            {
                return;
            }

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

            localizationHelper = helper;
            localizationModule.SetHelper(helper);
        }

        /// <summary>
        /// 通过 ResourceComponent 加载并解析一个语言文件，不切换当前语言。
        /// 同语言并发加载会共享同一个任务。
        /// </summary>
        public Task LoadLanguageAsync(
            string language, CancellationToken ct = default)
        {
            return LoadLanguageAsync(language, GetDefaultLanguageLocation(language), ct);
        }

        /// <summary>
        /// 通过 ResourceComponent 使用显式资源位置加载语言文件。
        /// location 由资源辅助器解释，可以是 Resources 路径、YooAsset 地址或自定义标识。
        /// </summary>
        public async Task LoadLanguageAsync(
            string language, string location, CancellationToken ct = default)
        {
            ValidateLanguage(language);
            ValidateLocation(location);
            if (localizationModule.HasLanguage(language))
            {
                return;
            }

            LanguageLoadKey loadKey = new LanguageLoadKey(language, location);
            if (!pendingLanguageLoads.TryGetValue(loadKey, out Task loadTask))
            {
                loadTask = LoadLanguageAssetInternalAsync(
                    language, location, lifetimeCts.Token);
                pendingLanguageLoads.Add(loadKey, loadTask);
                _ = RemovePendingLoadWhenCompletedAsync(loadKey, loadTask);
            }

            await AwaitWithCancellationAsync(loadTask, ct);
        }

        /// <summary>
        /// 从 JSON 字符串解析并缓存语言包，不经过资源模块。
        /// </summary>
        public void LoadLanguageFromString(string language, string json)
        {
            localizationModule.LoadLanguageFromString(language, json);
        }

        /// <summary>从资源路径加载多语言容器，不自动切换当前语言。</summary>
        public async Task LoadLanguageBundleAsync(
            string assetPath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                throw new RFrameworkException(
                    "LocalizationComponent: bundle asset path is invalid.");
            }

            ResourceComponent resource = GameEntry.Resource;
            if (resource == null)
            {
                throw new RFrameworkException(
                    "LocalizationComponent: ResourceComponent is required to load "
                    + "a language bundle.");
            }

            await resource.InitializeAsync();
            byte[] bytes = await resource.LoadAssetAsync<byte[]>(assetPath, 0, ct);
            if (bytes == null)
            {
                throw new RFrameworkException(
                    $"LocalizationComponent: Failed to load language bundle '{assetPath}'.");
            }

            try
            {
                localizationModule.LoadLanguageBundle(bytes);
            }
            finally
            {
                resource.UnloadAsset<byte[]>(assetPath);
            }
        }

        /// <summary>从原始字节原子加载多语言容器。</summary>
        public void LoadLanguageBundle(byte[] bytes)
        {
            localizationModule.LoadLanguageBundle(bytes);
        }

        /// <summary>
        /// 确保目标语言已加载，然后串行切换当前语言。
        /// </summary>
        public Task SwitchLanguageAsync(
            string language, CancellationToken ct = default)
        {
            return SwitchLanguageAsync(language, GetDefaultLanguageLocation(language), ct);
        }

        /// <summary>
        /// 使用显式资源位置确保目标语言已加载，然后串行切换当前语言。
        /// </summary>
        public async Task SwitchLanguageAsync(
            string language, string location, CancellationToken ct = default)
        {
            ValidateLanguage(language);
            ValidateLocation(location);
            using CancellationTokenSource linkedCts =
                CancellationTokenSource.CreateLinkedTokenSource(lifetimeCts.Token, ct);
            CancellationToken token = linkedCts.Token;
            await switchSemaphore.WaitAsync(token);
            try
            {
                await LoadLanguageAsync(language, location, token);
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

        public string GetDefaultLanguageLocation(string language)
        {
            ValidateLanguage(language);
            if (!(localizationHelper is ILocalizationLocationProvider provider))
            {
                throw new RFrameworkException(
                    $"LocalizationComponent: Helper '{localizationHelper?.GetType().FullName}' "
                    + "does not provide a default language location. Disable automatic "
                    + "default-language loading and use the overload that accepts an explicit "
                    + "location, or implement ILocalizationLocationProvider.");
            }

            string location = provider.GetLanguageLocation(language.Trim());
            ValidateLocation(location);
            return location;
        }

        [Obsolete("Use GetDefaultLanguageLocation instead.")]
        public string GetLanguageAssetPath(string language)
        {
            return GetDefaultLanguageLocation(language);
        }

        private async Task LoadLanguageAssetInternalAsync(
            string language, string location, CancellationToken ct)
        {
            ResourceComponent resource = GameEntry.Resource;
            if (resource == null)
            {
                throw new RFrameworkException(
                    "LocalizationComponent: ResourceComponent is required to load language files.");
            }

            await resource.InitializeAsync();
            ct.ThrowIfCancellationRequested();

            byte[] bytes = await resource.LoadAssetAsync<byte[]>(location, 0, ct);
            if (bytes == null)
            {
                throw new RFrameworkException(
                    $"LocalizationComponent: Failed to load language asset '{location}'.");
            }

            try
            {
                localizationModule.LoadLanguage(language, bytes);
            }
            finally
            {
                resource.UnloadAsset<byte[]>(location);
            }
        }

        private static async Task AwaitWithCancellationAsync(
            Task task, CancellationToken ct)
        {
            if (!ct.CanBeCanceled || task.IsCompleted)
            {
                await task;
                return;
            }

            TaskCompletionSource<bool> cancellation = new TaskCompletionSource<bool>();
            using (ct.Register(() => cancellation.TrySetResult(true)))
            {
                if (!ReferenceEquals(task, await Task.WhenAny(task, cancellation.Task)))
                {
                    ct.ThrowIfCancellationRequested();
                }
            }

            await task;
        }

        private async Task RemovePendingLoadWhenCompletedAsync(
            LanguageLoadKey loadKey, Task loadTask)
        {
            try
            {
                await loadTask;
            }
            catch
            {
                // 加载异常由实际等待者观察；此任务只负责移除并发去重记录。
            }

            if (pendingLanguageLoads.TryGetValue(loadKey, out Task current)
                && ReferenceEquals(current, loadTask))
            {
                pendingLanguageLoads.Remove(loadKey);
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

        private static void ValidateLocation(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                throw new RFrameworkException(
                    "LocalizationComponent: language asset location is invalid.");
            }
        }

        private readonly struct LanguageLoadKey : IEquatable<LanguageLoadKey>
        {
            public LanguageLoadKey(string language, string location)
            {
                Language = language;
                Location = location;
            }

            private string Language { get; }

            private string Location { get; }

            public bool Equals(LanguageLoadKey other)
            {
                return string.Equals(Language, other.Language, StringComparison.Ordinal)
                    && string.Equals(Location, other.Location, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is LanguageLoadKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Language != null ? Language.GetHashCode() : 0) * 397)
                        ^ (Location != null ? Location.GetHashCode() : 0);
                }
            }
        }
    }
}
