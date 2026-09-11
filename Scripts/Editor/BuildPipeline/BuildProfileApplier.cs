using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 参数应用结果：成功标志、是否切换平台、脱敏应用报告与错误列表。
    /// 报告中的密码与私钥相关值一律脱敏为星号，绝不输出密码本体。
    /// </summary>
    public sealed class BuildApplyResult
    {
        /// <summary>
        /// 创建应用结果。
        /// </summary>
        /// <param name="succeeded">是否全部应用成功。</param>
        /// <param name="platformSwitched">是否发生了平台切换。</param>
        /// <param name="reportLines">脱敏应用报告行。</param>
        /// <param name="errors">错误描述列表；成功时为空。</param>
        public BuildApplyResult(
            bool succeeded,
            bool platformSwitched,
            IReadOnlyList<string> reportLines,
            IReadOnlyList<string> errors)
        {
            Succeeded = succeeded;
            PlatformSwitched = platformSwitched;
            ReportLines = reportLines ?? Array.Empty<string>();
            Errors = errors ?? Array.Empty<string>();
        }

        /// <summary>获取是否全部应用成功。</summary>
        public bool Succeeded { get; }

        /// <summary>获取是否发生了平台切换。</summary>
        public bool PlatformSwitched { get; }

        /// <summary>获取脱敏应用报告行。</summary>
        public IReadOnlyList<string> ReportLines { get; }

        /// <summary>获取错误描述列表；成功时为空。</summary>
        public IReadOnlyList<string> Errors { get; }
    }

    /// <summary>
    /// 构建参数应用器：将 Profile 配置写入 PlayerSettings 与 EditorBuildSettings。
    /// 应用前自动执行构建前校验，存在 Error 级问题时拒绝应用且不改变任何状态；
    /// 应用过程只写参数与场景，不启动任何构建任务。
    /// 密码与私钥只从环境变量读取，报告一律脱敏。
    /// </summary>
    public static class BuildProfileApplier
    {
        /// <summary>
        /// 应用 Profile 全部参数到编辑器设置。
        /// </summary>
        /// <param name="profile">待应用的构建配置。</param>
        /// <returns>应用结果，含脱敏报告或错误列表。</returns>
        public static BuildApplyResult Apply(
            UnityRFrameworkBuildProfile profile)
        {
            if (profile == null)
            {
                return Failed("构建配置为空，无法应用参数。");
            }

            BuildValidationResult validation =
                BuildProfileValidator.Validate(profile);
            if (!validation.CanBuild)
            {
                List<string> errors = new List<string>();
                IReadOnlyList<BuildValidationIssue> issues = validation.Errors;
                for (int i = 0; i < issues.Count; i++)
                {
                    errors.Add(issues[i].Message);
                }

                return new BuildApplyResult(
                    false,
                    false,
                    Array.Empty<string>(),
                    errors);
            }

            List<string> report = new List<string>();
            try
            {
                bool switched = EnsurePlatform(profile, report);
                ApplyIdentity(profile, report);
                ApplyVersion(profile, report);
                ApplyCompilation(profile, report);
                ApplyDebug(profile, report);
                ApplyDefineSymbols(profile, report);
                ApplyScenes(profile, report);
                ApplyPlatformSpecific(profile, report);

                return new BuildApplyResult(true, switched, report, Array.Empty<string>());
            }
            catch (Exception exception)
            {
                report.Add($"应用中断：{exception.Message}");
                return new BuildApplyResult(
                    false,
                    false,
                    report,
                    new List<string> { exception.Message });
            }
        }

        /// <summary>
        /// 确认目标平台与当前活动平台一致，不一致时切换。
        /// </summary>
        /// <param name="profile">待应用的构建配置。</param>
        /// <param name="report">追加报告行的目标列表。</param>
        /// <returns>是否发生了平台切换。</returns>
        private static bool EnsurePlatform(
            UnityRFrameworkBuildProfile profile,
            ICollection<string> report)
        {
            BuildTarget target = profile.Platform.Target;
            BuildTarget current = EditorUserBuildSettings.activeBuildTarget;
            if (current == target)
            {
                report.Add($"平台: 已是 {target}，无需切换。");
                return false;
            }

            BuildTargetGroup targetGroup =
                UnityEditor.BuildPipeline.GetBuildTargetGroup(target);
            bool switched =
                EditorUserBuildSettings.SwitchActiveBuildTarget(
                    targetGroup,
                    target);
            if (!switched)
            {
                throw new InvalidOperationException(
                    $"切换到目标平台 '{target}' 失败，参数未应用。");
            }

            report.Add($"平台: {current} → {target}。");
            return true;
        }

        /// <summary>
        /// 应用公司名、产品名与应用标识。
        /// </summary>
        /// <param name="profile">待应用的构建配置。</param>
        /// <param name="report">追加报告行的目标列表。</param>
        private static void ApplyIdentity(
            UnityRFrameworkBuildProfile profile,
            ICollection<string> report)
        {
            string oldCompany = PlayerSettings.companyName;
            string newCompany = profile.Platform.CompanyName;
            if (!string.Equals(oldCompany, newCompany, StringComparison.Ordinal))
            {
                PlayerSettings.companyName = newCompany;
                report.Add($"公司名称: {Display(oldCompany)} → {newCompany}。");
            }

            string oldProduct = PlayerSettings.productName;
            string newProduct = profile.Platform.ProductName;
            if (!string.Equals(oldProduct, newProduct, StringComparison.Ordinal))
            {
                PlayerSettings.productName = newProduct;
                report.Add($"产品名称: {Display(oldProduct)} → {newProduct}。");
            }

            NamedBuildTarget namedTarget = GetNamedTarget(profile.Platform.Target);
            string oldIdentifier =
                PlayerSettings.GetApplicationIdentifier(namedTarget);
            string newIdentifier = profile.Platform.ApplicationIdentifier;
            if (!string.Equals(
                    oldIdentifier,
                    newIdentifier,
                    StringComparison.Ordinal))
            {
                PlayerSettings.SetApplicationIdentifier(
                    namedTarget,
                    newIdentifier);
                report.Add($"应用标识: {Display(oldIdentifier)} → {newIdentifier}。");
            }
        }

        /// <summary>
        /// 应用公共版本与平台构建号。
        /// </summary>
        /// <param name="profile">待应用的构建配置。</param>
        /// <param name="report">追加报告行的目标列表。</param>
        private static void ApplyVersion(
            UnityRFrameworkBuildProfile profile,
            ICollection<string> report)
        {
            string oldVersion = PlayerSettings.bundleVersion;
            string newVersion = profile.Platform.PublicVersion;
            if (!string.Equals(oldVersion, newVersion, StringComparison.Ordinal))
            {
                PlayerSettings.bundleVersion = newVersion;
                report.Add($"公共版本: {Display(oldVersion)} → {newVersion}。");
            }

            if (profile.Platform.Target == BuildTarget.Android)
            {
                int oldCode = PlayerSettings.Android.bundleVersionCode;
                int newCode = profile.Platform.BuildNumber;
                if (oldCode != newCode)
                {
                    PlayerSettings.Android.bundleVersionCode = newCode;
                    report.Add($"Android versionCode: {oldCode} → {newCode}。");
                }
            }
            else if (profile.Platform.Target == BuildTarget.iOS)
            {
                string oldNumber = PlayerSettings.iOS.buildNumber;
                string newNumber = profile.Platform.BuildNumber.ToString();
                if (!string.Equals(oldNumber, newNumber, StringComparison.Ordinal))
                {
                    PlayerSettings.iOS.buildNumber = newNumber;
                    report.Add(
                        $"iOS buildNumber: {Display(oldNumber)} → {newNumber}。");
                }
            }
        }

        /// <summary>
        /// 应用脚本后端、API 级别、裁剪与 IL2CPP 参数。
        /// </summary>
        /// <param name="profile">待应用的构建配置。</param>
        /// <param name="report">追加报告行的目标列表。</param>
        private static void ApplyCompilation(
            UnityRFrameworkBuildProfile profile,
            ICollection<string> report)
        {
            NamedBuildTarget namedTarget = GetNamedTarget(profile.Platform.Target);

            ScriptingImplementation oldBackend =
                PlayerSettings.GetScriptingBackend(namedTarget);
            ScriptingImplementation newBackend = profile.Platform.ScriptingBackend;
            if (oldBackend != newBackend)
            {
                PlayerSettings.SetScriptingBackend(namedTarget, newBackend);
                report.Add($"脚本后端: {oldBackend} → {newBackend}。");
            }

            ApiCompatibilityLevel oldApi =
                PlayerSettings.GetApiCompatibilityLevel(namedTarget);
            ApiCompatibilityLevel newApi = profile.Platform.ApiCompatibilityLevel;
            if (oldApi != newApi)
            {
                PlayerSettings.SetApiCompatibilityLevel(namedTarget, newApi);
                report.Add($"API 兼容级别: {oldApi} → {newApi}。");
            }

            ManagedStrippingLevel oldStripping =
                PlayerSettings.GetManagedStrippingLevel(namedTarget);
            ManagedStrippingLevel newStripping =
                profile.Platform.ManagedStrippingLevel;
            if (oldStripping != newStripping)
            {
                PlayerSettings.SetManagedStrippingLevel(
                    namedTarget,
                    newStripping);
                report.Add($"托管裁剪级别: {oldStripping} → {newStripping}。");
            }

            bool oldIncrementalGc = PlayerSettings.gcIncremental;
            bool newIncrementalGc = profile.Platform.IncrementalGC;
            if (oldIncrementalGc != newIncrementalGc)
            {
                PlayerSettings.gcIncremental = newIncrementalGc;
                report.Add(
                    $"增量式 GC: {oldIncrementalGc} → {newIncrementalGc}。");
            }

            if (newBackend == ScriptingImplementation.IL2CPP)
            {
                Il2CppCodeGeneration oldGeneration =
                    PlayerSettings.GetIl2CppCodeGeneration(namedTarget);
                Il2CppCodeGeneration newGeneration =
                    profile.Platform.Il2CppCodeGeneration;
                if (oldGeneration != newGeneration)
                {
                    PlayerSettings.SetIl2CppCodeGeneration(
                        namedTarget,
                        newGeneration);
                    report.Add(
                        $"IL2CPP 代码生成: {oldGeneration} → {newGeneration}。");
                }

                Il2CppCompilerConfiguration oldCompiler =
                    PlayerSettings.GetIl2CppCompilerConfiguration(
                        namedTarget);
                Il2CppCompilerConfiguration newCompiler =
                    profile.Platform.CppCompilerConfiguration;
                if (oldCompiler != newCompiler)
                {
                    PlayerSettings.SetIl2CppCompilerConfiguration(
                        namedTarget,
                        newCompiler);
                    report.Add(
                        $"IL2CPP 编译器: {oldCompiler} → {newCompiler}。");
                }

            }
        }

        /// <summary>
        /// 应用 Development Build 与调试参数。
        /// </summary>
        /// <param name="profile">待应用的构建配置。</param>
        /// <param name="report">追加报告行的目标列表。</param>
        private static void ApplyDebug(
            UnityRFrameworkBuildProfile profile,
            ICollection<string> report)
        {
            ApplyBoolSetting(
                "Development Build",
                EditorUserBuildSettings.development,
                profile.Platform.DevelopmentBuild,
                value => EditorUserBuildSettings.development = value,
                report);

            ApplyBoolSetting(
                "脚本调试",
                EditorUserBuildSettings.allowDebugging,
                profile.Platform.ScriptDebugging,
                value => EditorUserBuildSettings.allowDebugging = value,
                report);

            ApplyBoolSetting(
                "自动连接 Profiler",
                EditorUserBuildSettings.connectProfiler,
                profile.Platform.AutoconnectProfiler,
                value => EditorUserBuildSettings.connectProfiler = value,
                report);

            ApplyBoolSetting(
                "Deep Profiling",
                EditorUserBuildSettings.buildWithDeepProfilingSupport,
                profile.Platform.DeepProfiling,
                value => EditorUserBuildSettings.buildWithDeepProfilingSupport =
                    value,
                report);
        }

        /// <summary>
        /// 应用脚本宏：先移除 RemoveDefineSymbols，再合并 Profile 宏。
        /// </summary>
        /// <param name="profile">待应用的构建配置。</param>
        /// <param name="report">追加报告行的目标列表。</param>
        private static void ApplyDefineSymbols(
            UnityRFrameworkBuildProfile profile,
            ICollection<string> report)
        {
            NamedBuildTarget namedTarget = GetNamedTarget(profile.Platform.Target);
            string rawDefines =
                PlayerSettings.GetScriptingDefineSymbols(namedTarget);

            HashSet<string> merged = new HashSet<string>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(rawDefines))
            {
                string[] current = rawDefines.Split(';');
                for (int i = 0; i < current.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(current[i]))
                    {
                        merged.Add(current[i].Trim());
                    }
                }
            }

            for (int i = 0; i < profile.Platform.RemoveDefineSymbols.Count; i++)
            {
                merged.Remove(profile.Platform.RemoveDefineSymbols[i]);
            }

            for (int i = 0; i < profile.Platform.DefineSymbols.Count; i++)
            {
                merged.Add(profile.Platform.DefineSymbols[i]);
            }

            List<string> sorted = new List<string>(merged);
            sorted.Sort(StringComparer.Ordinal);
            string[] newDefines = sorted.ToArray();

            PlayerSettings.SetScriptingDefineSymbols(namedTarget, newDefines);
            report.Add($"脚本宏: {newDefines.Length} 条生效"
                + $"（移除 {profile.Platform.RemoveDefineSymbols.Count} 条，"
                + $"配置 {profile.Platform.DefineSymbols.Count} 条）。");
        }

        /// <summary>
        /// 同步场景列表到 EditorBuildSettings。
        /// </summary>
        /// <param name="profile">待应用的构建配置。</param>
        /// <param name="report">追加报告行的目标列表。</param>
        private static void ApplyScenes(
            UnityRFrameworkBuildProfile profile,
            ICollection<string> report)
        {
            List<EditorBuildSettingsScene> scenes =
                new List<EditorBuildSettingsScene>();
            for (int i = 0; i < profile.Scenes.Count; i++)
            {
                BuildSceneEntry entry = profile.Scenes[i];
                if (entry == null || !entry.IsValidEnabled())
                {
                    continue;
                }

                scenes.Add(
                    new EditorBuildSettingsScene(entry.ResolvePath(), true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            report.Add($"构建场景: 已同步 {scenes.Count} 个启用场景。");
        }

        /// <summary>
        /// 应用平台专有参数（Android / iOS）。
        /// </summary>
        /// <param name="profile">待应用的构建配置。</param>
        /// <param name="report">追加报告行的目标列表。</param>
        private static void ApplyPlatformSpecific(
            UnityRFrameworkBuildProfile profile,
            ICollection<string> report)
        {
            BuildTarget target = profile.Platform.Target;
            if (target == BuildTarget.Android)
            {
                ApplyAndroid(profile, report);
            }
            else if (target == BuildTarget.iOS)
            {
                ApplyIos(profile, report);
            }
        }

        /// <summary>
        /// 应用 Android 专有参数；密码从环境变量读取，报告脱敏。
        /// </summary>
        /// <param name="profile">待应用的构建配置。</param>
        /// <param name="report">追加报告行的目标列表。</param>
        private static void ApplyAndroid(
            UnityRFrameworkBuildProfile profile,
            ICollection<string> report)
        {
            AndroidArchitecture oldArchitecture =
                PlayerSettings.Android.targetArchitectures;
            AndroidArchitecture newArchitecture =
                profile.Platform.AndroidArchitecture;
            if (oldArchitecture != newArchitecture)
            {
                PlayerSettings.Android.targetArchitectures = newArchitecture;
                report.Add($"Android 架构: {oldArchitecture} → {newArchitecture}。");
            }

            AndroidSdkVersions oldSdk = PlayerSettings.Android.targetSdkVersion;
            AndroidSdkVersions newSdk = profile.Platform.AndroidTargetSdk;
            if (oldSdk != newSdk)
            {
                PlayerSettings.Android.targetSdkVersion = newSdk;
                report.Add($"Android 目标 SDK: {oldSdk} → {newSdk}。");
            }

            bool oldAppBundle = EditorUserBuildSettings.buildAppBundle;
            bool newAppBundle = profile.Platform.AndroidBuildAppBundle;
            if (oldAppBundle != newAppBundle)
            {
                EditorUserBuildSettings.buildAppBundle = newAppBundle;
                report.Add($"Android 产物: {(oldAppBundle ? "AAB" : "APK")}"
                    + $" → {(newAppBundle ? "AAB" : "APK")}。");
            }

            if (string.IsNullOrWhiteSpace(profile.Platform.AndroidKeystoreName))
            {
                if (PlayerSettings.Android.useCustomKeystore)
                {
                    PlayerSettings.Android.useCustomKeystore = false;
                    report.Add("Android 签名: 恢复默认调试签名。");
                }

                return;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName =
                profile.Platform.AndroidKeystoreName;
            PlayerSettings.Android.keyaliasName =
                profile.Platform.AndroidKeystoreAlias;

            string keystorePass = Environment.GetEnvironmentVariable(
                profile.Platform.AndroidKeystorePassEnvVar);
            string aliasPass = Environment.GetEnvironmentVariable(
                profile.Platform.AndroidKeyAliasPassEnvVar);
            PlayerSettings.Android.keystorePass = keystorePass ?? string.Empty;
            PlayerSettings.Android.keyaliasPass = aliasPass ?? string.Empty;

            report.Add(
                $"Android 签名: Keystore '{profile.Platform.AndroidKeystoreName}'，"
                + "密码已从环境变量读取（脱敏）。");
        }

        /// <summary>
        /// 应用 iOS 专有参数。
        /// </summary>
        /// <param name="profile">待应用的构建配置。</param>
        /// <param name="report">追加报告行的目标列表。</param>
        private static void ApplyIos(
            UnityRFrameworkBuildProfile profile,
            ICollection<string> report)
        {
            iOSSdkVersion oldSdk = PlayerSettings.iOS.sdkVersion;
            iOSSdkVersion newSdk = profile.Platform.IosTargetSdk;
            if (oldSdk != newSdk)
            {
                PlayerSettings.iOS.sdkVersion = newSdk;
                report.Add($"iOS 目标 SDK: {oldSdk} → {newSdk}。");
            }
        }

        /// <summary>
        /// 应用单个布尔型编辑器设置，值变化时写回并记录报告。
        /// </summary>
        /// <param name="label">设置显示名。</param>
        /// <param name="oldValue">当前值。</param>
        /// <param name="newValue">目标值。</param>
        /// <param name="setter">写回委托。</param>
        /// <param name="report">追加报告行的目标列表。</param>
        private static void ApplyBoolSetting(
            string label,
            bool oldValue,
            bool newValue,
            Action<bool> setter,
            ICollection<string> report)
        {
            if (oldValue == newValue)
            {
                return;
            }

            setter(newValue);
            report.Add($"{label}: {oldValue} → {newValue}。");
        }

        /// <summary>
        /// 获取目标平台对应的 NamedBuildTarget。
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

        /// <summary>
        /// 构造失败结果。
        /// </summary>
        /// <param name="message">失败原因描述。</param>
        /// <returns>失败的应用结果。</returns>
        private static BuildApplyResult Failed(string message)
        {
            return new BuildApplyResult(
                false,
                false,
                Array.Empty<string>(),
                new List<string> { message });
        }

        /// <summary>
        /// 展示字符串：空值显示为（空）。
        /// </summary>
        /// <param name="value">原始字符串。</param>
        /// <returns>展示用字符串。</returns>
        private static string Display(string value)
        {
            return string.IsNullOrEmpty(value) ? "（空）" : value;
        }
    }
}
