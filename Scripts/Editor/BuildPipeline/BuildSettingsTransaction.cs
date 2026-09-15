using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 第三方集成全局开关快照。使用步骤 Id 定位 Expansion 提供的控制器，
    /// 避免核心程序集直接引用第三方插件类型。
    /// </summary>
    [Serializable]
    public sealed class BuildIntegrationActivationSnapshot
    {
        /// <summary>关联的 Profile 步骤 Id。</summary>
        public string StepId = string.Empty;

        /// <summary>任务开始前的全局启用状态。</summary>
        public bool Enabled;
    }

    /// <summary>
    /// 构建设置快照的可序列化数据。全部字段为基元类型与字符串，
    /// JsonUtility 可直接往返，随任务检查点持久化到状态目录，
    /// 保证 Domain Reload 或进程重启后仍能按任务恢复原始设置。
    /// 活动构建平台按契约不在恢复范围内（首版允许保留目标平台）。
    /// </summary>
    [Serializable]
    public sealed class BuildSettingsSnapshot
    {
        /// <summary>快照归属的任务 Id；加载时校验匹配。</summary>
        public string TaskId = string.Empty;

        /// <summary>快照创建时刻（ISO 8601 字符串）。</summary>
        public string CreatedAt = string.Empty;

        /// <summary>临时设置是否已开始应用；先持久化后写项目设置。</summary>
        public bool Applied;

        /// <summary>
        /// 本任务临时隔离的第三方构建开关原值。
        /// 在修改插件 Settings 前写入快照，支持 Domain Reload 后恢复。
        /// </summary>
        public List<BuildIntegrationActivationSnapshot> IntegrationActivations =
            new List<BuildIntegrationActivationSnapshot>();

        /// <summary>快照时的活动平台名称（仅作记录，不参与恢复）。</summary>
        public string ActiveTargetName = string.Empty;

        // ===== PlayerSettings：身份与版本 =====

        /// <summary>公司名称。</summary>
        public string CompanyName = string.Empty;

        /// <summary>产品名称。</summary>
        public string ProductName = string.Empty;

        /// <summary>当前平台的应用标识。</summary>
        public string ApplicationIdentifier = string.Empty;

        /// <summary>公共版本号（bundleVersion）。</summary>
        public string BundleVersion = string.Empty;

        /// <summary>Android versionCode。</summary>
        public int AndroidBundleVersionCode;

        /// <summary>iOS buildNumber。</summary>
        public string IosBuildNumber = string.Empty;

        // ===== PlayerSettings：编译与裁剪（按当前平台的 NamedBuildTarget） =====

        /// <summary>脚本后端名称（ScriptingImplementation）。</summary>
        public string ScriptingBackendName = string.Empty;

        /// <summary>API 兼容级别名称（ApiCompatibilityLevel）。</summary>
        public string ApiCompatibilityName = string.Empty;

        /// <summary>托管裁剪级别名称（ManagedStrippingLevel）。</summary>
        public string StrippingLevelName = string.Empty;

        /// <summary>IL2CPP 代码生成名称（Il2CppCodeGeneration）。</summary>
        public string Il2CppCodeGenerationName = string.Empty;

        /// <summary>IL2CPP 编译器配置名称（Il2CppCompilerConfiguration）。</summary>
        public string Il2CppCompilerConfigurationName = string.Empty;

        /// <summary>是否启用增量式 GC。</summary>
        public bool IncrementalGc;

        /// <summary>脚本宏（分号分隔，仅当前平台）。</summary>
        public string DefineSymbols = string.Empty;

        // ===== EditorUserBuildSettings：调试参数 =====

        /// <summary>Development Build。</summary>
        public bool Development;

        /// <summary>脚本调试。</summary>
        public bool ScriptDebugging;

        /// <summary>自动连接 Profiler。</summary>
        public bool ConnectProfiler;

        /// <summary>Deep Profiling。</summary>
        public bool DeepProfiling;

        // ===== 场景列表（"路径|启用"形式） =====

        /// <summary>EditorBuildSettings 场景列表。</summary>
        public List<string> Scenes = new List<string>();

        // ===== Android 专有 =====

        /// <summary>Android 架构名称（AndroidArchitecture）。</summary>
        public string AndroidArchitectureName = string.Empty;

        /// <summary>Android 目标 SDK 名称（AndroidSdkVersions）。</summary>
        public string AndroidTargetSdkName = string.Empty;

        /// <summary>是否构建 AAB。</summary>
        public bool AndroidBuildAppBundle;

        /// <summary>是否使用自定义 Keystore。</summary>
        public bool AndroidUseCustomKeystore;

        /// <summary>Keystore 路径。</summary>
        public string AndroidKeystoreName = string.Empty;

        /// <summary>Keystore Alias。</summary>
        public string AndroidKeyaliasName = string.Empty;

        /// <summary>Keystore 密码（仅当前进程内存，不写入快照 JSON）。</summary>
        [NonSerialized]
        public string AndroidKeystorePass = string.Empty;

        /// <summary>Alias 密码（仅当前进程内存，不写入快照 JSON）。</summary>
        [NonSerialized]
        public string AndroidKeyaliasPass = string.Empty;

        /// <summary>
        /// 当前进程是否捕获过签名密码。Domain Reload 后该值为 false，
        /// 恢复时不得用未序列化的空值覆盖 Unity 当前内存值。
        /// </summary>
        [NonSerialized]
        public bool HasInMemorySigningPasswords;

        // ===== iOS 专有 =====

        /// <summary>iOS 目标 SDK 名称（iOSSdkVersion）。</summary>
        public string IosTargetSdkName = string.Empty;
    }

    /// <summary>
    /// 构建设置事务：任务开始时捕获快照并持久化，
    /// "应用参数"步骤将 Profile 写入项目设置并标记事务已生效；
    /// 任务结束（成功、失败、取消）时按契约恢复快照——成功默认也恢复，
    /// 活动构建平台不在恢复范围。恢复失败由运行器映射为人工处理状态。
    /// </summary>
    public sealed class BuildSettingsTransaction
    {
        /// <summary>快照数据。</summary>
        public BuildSettingsSnapshot Snapshot { get; }

        /// <summary>是否已有步骤写入过项目设置；未生效的事务在结束时跳过恢复。</summary>
        public bool HasChanges { get; private set; }

        /// <summary>快照归属的持久化实例；为空表示仅内存事务（测试用）。</summary>
        private readonly BuildPipelinePersistence persistence;

        /// <summary>
        /// 创建事务（内部）；外部统一通过 <see cref="Capture"/> 获取。
        /// </summary>
        /// <param name="snapshot">快照数据。</param>
        /// <param name="persistence">持久化实例，可为空。</param>
        private BuildSettingsTransaction(
            BuildSettingsSnapshot snapshot,
            BuildPipelinePersistence persistence)
        {
            Snapshot = snapshot;
            this.persistence = persistence;
            HasChanges = snapshot != null && snapshot.Applied;
        }

        /// <summary>
        /// 捕获当前项目设置快照并持久化到任务目录。
        /// 任务目录已存在同 TaskId 的快照时（恢复场景）直接复用，
        /// 保证恢复基线是任务开始前的原始状态而非本次恢复时的已应用状态。
        /// </summary>
        /// <param name="persistence">任务状态目录；为空时使用工程默认目录。</param>
        /// <param name="taskId">任务 Id。</param>
        /// <returns>构建设置事务。</returns>
        public static BuildSettingsTransaction Capture(
            BuildPipelinePersistence persistence,
            string taskId)
        {
            if (persistence == null)
            {
                throw new ArgumentNullException(nameof(persistence));
            }

            BuildSettingsSnapshot existing = persistence.LoadSnapshot();
            if (existing != null
                && string.Equals(existing.TaskId, taskId, StringComparison.Ordinal))
            {
                return new BuildSettingsTransaction(existing, persistence);
            }

            BuildSettingsSnapshot snapshot = CaptureCurrent();
            snapshot.TaskId = taskId ?? string.Empty;
            snapshot.CreatedAt = DateTime.Now.ToString("o");
            persistence.SaveSnapshot(snapshot);
            return new BuildSettingsTransaction(snapshot, persistence);
        }

        /// <summary>
        /// 标记事务已生效：此后任务结束时会执行恢复。
        /// 由"应用构建参数"步骤在开始写入设置前调用。
        /// </summary>
        public void MarkApplied()
        {
            HasChanges = true;
            Snapshot.Applied = true;
            persistence?.SaveSnapshot(Snapshot);
        }

        /// <summary>
        /// 对 Player/Release 任务隔离 Profile 未启用的第三方构建回调。
        /// 只会临时关闭开关，不会替已选步骤自动开启插件；原值先持久化再修改。
        /// </summary>
        /// <param name="controllers">各 Expansion 提供的全局开关控制器。</param>
        /// <param name="profile">当前有效 Profile。</param>
        /// <param name="recipe">本任务实际 Recipe。</param>
        /// <returns>本次实际关闭的集成数量。</returns>
        public int ApplyIntegrationIsolation(
            IReadOnlyList<IBuildIntegrationActivationController> controllers,
            UnityRFrameworkBuildProfile profile,
            BuildRecipe recipe)
        {
            if (!HasChanges
                || profile == null
                || (recipe != BuildRecipe.Player
                    && recipe != BuildRecipe.Release)
                || controllers == null
                || controllers.Count == 0)
            {
                return 0;
            }

            Dictionary<string, IBuildIntegrationActivationController> unique =
                BuildControllerMap(controllers);
            List<IBuildIntegrationActivationController> toDisable =
                new List<IBuildIntegrationActivationController>();
            bool snapshotChanged = false;

            foreach (KeyValuePair<string, IBuildIntegrationActivationController> pair
                in unique)
            {
                if (BuildStepConfigLocator.HasEnabledEntry(profile, pair.Key))
                {
                    continue;
                }

                BuildIntegrationActivationSnapshot existing =
                    FindIntegrationSnapshot(pair.Key);
                if (existing == null)
                {
                    existing = new BuildIntegrationActivationSnapshot
                    {
                        StepId = pair.Key,
                        Enabled = pair.Value.IsEnabled
                    };
                    Snapshot.IntegrationActivations.Add(existing);
                    snapshotChanged = true;
                }

                if (pair.Value.IsEnabled)
                {
                    toDisable.Add(pair.Value);
                }
            }

            // 必须先保存原值，避免设置写入触发 Domain Reload 后失去恢复基线。
            if (snapshotChanged)
            {
                persistence?.SaveSnapshot(Snapshot);
            }

            for (int i = 0; i < toDisable.Count; i++)
            {
                toDisable[i].SetEnabled(false);
            }

            return toDisable.Count;
        }

        /// <summary>
        /// 将项目设置恢复为快照值；仅写回与当前值不同的字段。
        /// 活动构建平台按契约不恢复。任何字段恢复失败都会抛出异常，
        /// 由运行器映射为人工处理状态。
        /// </summary>
        public void Restore(
            IReadOnlyList<IBuildIntegrationActivationController> controllers = null)
        {
            List<Exception> errors = new List<Exception>();
            try
            {
                RestoreIntegrationActivations(controllers);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            try
            {
                RestoreProjectSettings();
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }

            if (errors.Count == 1)
            {
                throw errors[0];
            }
            if (errors.Count > 1)
            {
                throw new AggregateException("多个临时构建设置恢复失败。", errors);
            }
        }

        /// <summary>恢复 Unity 项目构建设置。</summary>
        private void RestoreProjectSettings()
        {
            if (!HasChanges)
            {
                return;
            }

            BuildTarget snapshotTarget = Enum.TryParse(
                Snapshot.ActiveTargetName,
                out BuildTarget parsedTarget)
                ? parsedTarget
                : EditorUserBuildSettings.activeBuildTarget;
            NamedBuildTarget namedTarget = GetNamedTarget(snapshotTarget);

            if (!string.Equals(PlayerSettings.companyName, Snapshot.CompanyName, StringComparison.Ordinal))
            {
                PlayerSettings.companyName = Snapshot.CompanyName;
            }

            if (!string.Equals(PlayerSettings.productName, Snapshot.ProductName, StringComparison.Ordinal))
            {
                PlayerSettings.productName = Snapshot.ProductName;
            }

            if (!string.Equals(
                    PlayerSettings.GetApplicationIdentifier(namedTarget),
                    Snapshot.ApplicationIdentifier,
                    StringComparison.Ordinal))
            {
                PlayerSettings.SetApplicationIdentifier(
                    namedTarget,
                    Snapshot.ApplicationIdentifier);
            }

            if (!string.Equals(PlayerSettings.bundleVersion, Snapshot.BundleVersion, StringComparison.Ordinal))
            {
                PlayerSettings.bundleVersion = Snapshot.BundleVersion;
            }

            if (PlayerSettings.Android.bundleVersionCode != Snapshot.AndroidBundleVersionCode)
            {
                PlayerSettings.Android.bundleVersionCode = Snapshot.AndroidBundleVersionCode;
            }

            if (!string.Equals(PlayerSettings.iOS.buildNumber, Snapshot.IosBuildNumber, StringComparison.Ordinal))
            {
                PlayerSettings.iOS.buildNumber = Snapshot.IosBuildNumber;
            }

            ScriptingImplementation backend = (ScriptingImplementation)Enum.Parse(
                typeof(ScriptingImplementation),
                Snapshot.ScriptingBackendName);
            if (PlayerSettings.GetScriptingBackend(namedTarget) != backend)
            {
                PlayerSettings.SetScriptingBackend(namedTarget, backend);
            }

            ApiCompatibilityLevel api = (ApiCompatibilityLevel)Enum.Parse(
                typeof(ApiCompatibilityLevel),
                Snapshot.ApiCompatibilityName);
            if (PlayerSettings.GetApiCompatibilityLevel(namedTarget) != api)
            {
                PlayerSettings.SetApiCompatibilityLevel(namedTarget, api);
            }

            ManagedStrippingLevel stripping = (ManagedStrippingLevel)Enum.Parse(
                typeof(ManagedStrippingLevel),
                Snapshot.StrippingLevelName);
            if (PlayerSettings.GetManagedStrippingLevel(namedTarget) != stripping)
            {
                PlayerSettings.SetManagedStrippingLevel(namedTarget, stripping);
            }

            if (PlayerSettings.gcIncremental != Snapshot.IncrementalGc)
            {
                PlayerSettings.gcIncremental = Snapshot.IncrementalGc;
            }

            if (backend == ScriptingImplementation.IL2CPP)
            {
                if (!string.IsNullOrEmpty(Snapshot.Il2CppCodeGenerationName))
                {
                    Il2CppCodeGeneration generation =
                        (Il2CppCodeGeneration)Enum.Parse(
                            typeof(Il2CppCodeGeneration),
                            Snapshot.Il2CppCodeGenerationName);
                    if (PlayerSettings.GetIl2CppCodeGeneration(namedTarget) != generation)
                    {
                        PlayerSettings.SetIl2CppCodeGeneration(namedTarget, generation);
                    }
                }

                if (!string.IsNullOrEmpty(Snapshot.Il2CppCompilerConfigurationName))
                {
                    Il2CppCompilerConfiguration compiler =
                        (Il2CppCompilerConfiguration)Enum.Parse(
                            typeof(Il2CppCompilerConfiguration),
                            Snapshot.Il2CppCompilerConfigurationName);
                    if (PlayerSettings.GetIl2CppCompilerConfiguration(namedTarget) != compiler)
                    {
                        PlayerSettings.SetIl2CppCompilerConfiguration(
                            namedTarget,
                            compiler);
                    }
                }
            }

            if (!string.Equals(
                    PlayerSettings.GetScriptingDefineSymbols(namedTarget),
                    Snapshot.DefineSymbols,
                    StringComparison.Ordinal))
            {
                PlayerSettings.SetScriptingDefineSymbols(
                    namedTarget,
                    Snapshot.DefineSymbols);
            }

            if (EditorUserBuildSettings.development != Snapshot.Development)
            {
                EditorUserBuildSettings.development = Snapshot.Development;
            }

            if (EditorUserBuildSettings.allowDebugging != Snapshot.ScriptDebugging)
            {
                EditorUserBuildSettings.allowDebugging = Snapshot.ScriptDebugging;
            }

            if (EditorUserBuildSettings.connectProfiler != Snapshot.ConnectProfiler)
            {
                EditorUserBuildSettings.connectProfiler = Snapshot.ConnectProfiler;
            }

            if (EditorUserBuildSettings.buildWithDeepProfilingSupport
                != Snapshot.DeepProfiling)
            {
                EditorUserBuildSettings.buildWithDeepProfilingSupport =
                    Snapshot.DeepProfiling;
            }

            RestoreScenes();

            if (PlayerSettings.Android.targetArchitectures.ToString()
                != Snapshot.AndroidArchitectureName)
            {
                PlayerSettings.Android.targetArchitectures =
                    (AndroidArchitecture)Enum.Parse(
                        typeof(AndroidArchitecture),
                        Snapshot.AndroidArchitectureName);
            }

            if (PlayerSettings.Android.targetSdkVersion.ToString()
                != Snapshot.AndroidTargetSdkName)
            {
                PlayerSettings.Android.targetSdkVersion =
                    (AndroidSdkVersions)Enum.Parse(
                        typeof(AndroidSdkVersions),
                        Snapshot.AndroidTargetSdkName);
            }

            if (EditorUserBuildSettings.buildAppBundle != Snapshot.AndroidBuildAppBundle)
            {
                EditorUserBuildSettings.buildAppBundle = Snapshot.AndroidBuildAppBundle;
            }

            if (PlayerSettings.Android.useCustomKeystore != Snapshot.AndroidUseCustomKeystore)
            {
                PlayerSettings.Android.useCustomKeystore = Snapshot.AndroidUseCustomKeystore;
            }

            if (!string.Equals(
                    PlayerSettings.Android.keystoreName,
                    Snapshot.AndroidKeystoreName,
                    StringComparison.Ordinal))
            {
                PlayerSettings.Android.keystoreName = Snapshot.AndroidKeystoreName;
            }

            if (!string.Equals(
                    PlayerSettings.Android.keyaliasName,
                    Snapshot.AndroidKeyaliasName,
                    StringComparison.Ordinal))
            {
                PlayerSettings.Android.keyaliasName = Snapshot.AndroidKeyaliasName;
            }

            if (Snapshot.HasInMemorySigningPasswords)
            {
                PlayerSettings.Android.keystorePass = Snapshot.AndroidKeystorePass;
                PlayerSettings.Android.keyaliasPass = Snapshot.AndroidKeyaliasPass;
            }

            if (PlayerSettings.iOS.sdkVersion.ToString() != Snapshot.IosTargetSdkName)
            {
                PlayerSettings.iOS.sdkVersion = (iOSSdkVersion)Enum.Parse(
                    typeof(iOSSdkVersion),
                    Snapshot.IosTargetSdkName);
            }
        }

        /// <summary>恢复被本任务临时关闭的第三方集成开关。</summary>
        private void RestoreIntegrationActivations(
            IReadOnlyList<IBuildIntegrationActivationController> controllers)
        {
            if (!HasChanges
                || Snapshot.IntegrationActivations == null
                || Snapshot.IntegrationActivations.Count == 0)
            {
                return;
            }

            Dictionary<string, IBuildIntegrationActivationController> map =
                BuildControllerMap(controllers);
            List<Exception> errors = new List<Exception>();
            for (int i = 0; i < Snapshot.IntegrationActivations.Count; i++)
            {
                BuildIntegrationActivationSnapshot entry =
                    Snapshot.IntegrationActivations[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.StepId))
                {
                    continue;
                }

                if (!map.TryGetValue(
                        entry.StepId,
                        out IBuildIntegrationActivationController controller))
                {
                    errors.Add(new InvalidOperationException(
                        $"无法恢复第三方集成 '{entry.StepId}'：当前工程未发现对应控制器。"));
                    continue;
                }

                try
                {
                    if (controller.IsEnabled != entry.Enabled)
                    {
                        controller.SetEnabled(entry.Enabled);
                    }
                }
                catch (Exception exception)
                {
                    errors.Add(new InvalidOperationException(
                        $"恢复第三方集成 '{entry.StepId}' 失败：{exception.Message}",
                        exception));
                }
            }

            if (errors.Count == 1)
            {
                throw errors[0];
            }
            if (errors.Count > 1)
            {
                throw new AggregateException("第三方集成开关恢复失败。", errors);
            }
        }

        /// <summary>按步骤 Id 建立控制器索引，并拒绝重复或无效实现。</summary>
        private static Dictionary<string, IBuildIntegrationActivationController>
            BuildControllerMap(
                IReadOnlyList<IBuildIntegrationActivationController> controllers)
        {
            Dictionary<string, IBuildIntegrationActivationController> result =
                new Dictionary<string, IBuildIntegrationActivationController>(
                    StringComparer.OrdinalIgnoreCase);
            if (controllers == null)
            {
                return result;
            }

            for (int i = 0; i < controllers.Count; i++)
            {
                IBuildIntegrationActivationController controller = controllers[i];
                if (controller == null || string.IsNullOrWhiteSpace(controller.StepId))
                {
                    throw new InvalidOperationException(
                        "发现 null 或 StepId 为空的第三方构建开关控制器。");
                }
                if (result.ContainsKey(controller.StepId))
                {
                    throw new InvalidOperationException(
                        $"第三方构建开关控制器 StepId '{controller.StepId}' 重复。");
                }
                result.Add(controller.StepId, controller);
            }

            return result;
        }

        /// <summary>查找已持久化的第三方集成开关原值。</summary>
        private BuildIntegrationActivationSnapshot FindIntegrationSnapshot(
            string stepId)
        {
            if (Snapshot.IntegrationActivations == null)
            {
                Snapshot.IntegrationActivations =
                    new List<BuildIntegrationActivationSnapshot>();
                return null;
            }

            for (int i = 0; i < Snapshot.IntegrationActivations.Count; i++)
            {
                BuildIntegrationActivationSnapshot entry =
                    Snapshot.IntegrationActivations[i];
                if (entry != null
                    && string.Equals(
                        entry.StepId,
                        stepId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }
            return null;
        }

        /// <summary>
        /// 捕获当前编辑器的项目设置。
        /// </summary>
        /// <returns>当前设置快照。</returns>
        private static BuildSettingsSnapshot CaptureCurrent()
        {
            BuildTarget activeTarget = EditorUserBuildSettings.activeBuildTarget;
            NamedBuildTarget namedTarget = GetNamedTarget(activeTarget);
            ScriptingImplementation backend =
                PlayerSettings.GetScriptingBackend(namedTarget);

            BuildSettingsSnapshot snapshot = new BuildSettingsSnapshot
            {
                ActiveTargetName = activeTarget.ToString(),
                CompanyName = PlayerSettings.companyName,
                ProductName = PlayerSettings.productName,
                ApplicationIdentifier =
                    PlayerSettings.GetApplicationIdentifier(namedTarget),
                BundleVersion = PlayerSettings.bundleVersion,
                AndroidBundleVersionCode = PlayerSettings.Android.bundleVersionCode,
                IosBuildNumber = PlayerSettings.iOS.buildNumber,
                ScriptingBackendName = backend.ToString(),
                ApiCompatibilityName =
                    PlayerSettings.GetApiCompatibilityLevel(namedTarget).ToString(),
                StrippingLevelName =
                    PlayerSettings.GetManagedStrippingLevel(namedTarget).ToString(),
                Il2CppCodeGenerationName =
                    PlayerSettings.GetIl2CppCodeGeneration(namedTarget).ToString(),
                Il2CppCompilerConfigurationName =
                    PlayerSettings.GetIl2CppCompilerConfiguration(namedTarget).ToString(),
                IncrementalGc = PlayerSettings.gcIncremental,
                DefineSymbols =
                    PlayerSettings.GetScriptingDefineSymbols(namedTarget),
                Development = EditorUserBuildSettings.development,
                ScriptDebugging = EditorUserBuildSettings.allowDebugging,
                ConnectProfiler = EditorUserBuildSettings.connectProfiler,
                DeepProfiling =
                    EditorUserBuildSettings.buildWithDeepProfilingSupport,
                AndroidArchitectureName =
                    PlayerSettings.Android.targetArchitectures.ToString(),
                AndroidTargetSdkName =
                    PlayerSettings.Android.targetSdkVersion.ToString(),
                AndroidBuildAppBundle = EditorUserBuildSettings.buildAppBundle,
                AndroidUseCustomKeystore = PlayerSettings.Android.useCustomKeystore,
                AndroidKeystoreName = PlayerSettings.Android.keystoreName,
                AndroidKeyaliasName = PlayerSettings.Android.keyaliasName,
                AndroidKeystorePass = PlayerSettings.Android.keystorePass,
                AndroidKeyaliasPass = PlayerSettings.Android.keyaliasPass,
                HasInMemorySigningPasswords = true,
                IosTargetSdkName = PlayerSettings.iOS.sdkVersion.ToString()
            };

            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                snapshot.Scenes.Add(
                    $"{scenes[i].path}|{(scenes[i].enabled ? "1" : "0")}");
            }

            return snapshot;
        }

        /// <summary>
        /// 恢复 EditorBuildSettings 场景列表。
        /// </summary>
        private void RestoreScenes()
        {
            List<EditorBuildSettingsScene> scenes =
                new List<EditorBuildSettingsScene>(Snapshot.Scenes.Count);
            for (int i = 0; i < Snapshot.Scenes.Count; i++)
            {
                string entry = Snapshot.Scenes[i];
                int separator = entry.LastIndexOf('|');
                if (separator <= 0)
                {
                    continue;
                }

                scenes.Add(new EditorBuildSettingsScene(
                    entry.Substring(0, separator),
                    entry.EndsWith("|1", StringComparison.Ordinal)));
            }

            EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
            if (current.Length == scenes.Count)
            {
                bool identical = true;
                for (int i = 0; i < current.Length; i++)
                {
                    if (current[i].path != scenes[i].path
                        || current[i].enabled != scenes[i].enabled)
                    {
                        identical = false;
                        break;
                    }
                }

                if (identical)
                {
                    return;
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>
        /// 获取当前活动平台对应的 NamedBuildTarget。
        /// </summary>
        /// <param name="target">构建平台。</param>
        /// <returns>对应的命名构建目标。</returns>
        private static NamedBuildTarget GetNamedTarget(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneWindows64:
                case BuildTarget.StandaloneLinux64:
                case BuildTarget.StandaloneOSX:
                    return NamedBuildTarget.Standalone;
                case BuildTarget.Android:
                    return NamedBuildTarget.Android;
                case BuildTarget.iOS:
                    return NamedBuildTarget.iOS;
                case BuildTarget.WebGL:
                    return NamedBuildTarget.WebGL;
                default:
                    throw new InvalidOperationException(
                        $"不支持的构建平台 '{target}'。");
            }
        }
    }
}
