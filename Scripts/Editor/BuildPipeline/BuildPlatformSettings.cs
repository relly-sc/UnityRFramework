using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 平台构建参数配置，覆盖平台、标识、版本、编译、调试、宏与平台专有参数。
    /// 使用 Unity 原生枚举保存，应用参数时直接写入 PlayerSettings，减少映射层。
    /// 敏感密码字段只保存环境变量名，不保存密码本体。
    /// </summary>
    [Serializable]
    public sealed class BuildPlatformSettings
    {
        /// <summary>公共版本号格式校验规则：主版本.次版本.修订号。</summary>
        private static readonly Regex PublicVersionPattern =
            new Regex(@"^\d+\.\d+\.\d+$", RegexOptions.Compiled);

        /// <summary>目标构建平台，限定常用桌面、移动与 WebGL 平台。</summary>
        [Tooltip("目标构建平台：Windows、Linux、macOS、Android、iOS 或 WebGL。")]
        public BuildTarget Target = BuildTarget.StandaloneWindows64;

        /// <summary>公司名称，写入 PlayerSettings.companyName。</summary>
        [Tooltip("公司名称，写入 PlayerSettings.companyName。")]
        public string CompanyName = string.Empty;

        /// <summary>产品名称，写入 PlayerSettings.productName。</summary>
        [Tooltip("产品名称，写入 PlayerSettings.productName。")]
        public string ProductName = string.Empty;

        /// <summary>应用标识，Android/iOS 包名；必须为三段式，写入 PlayerSettings 对应平台。</summary>
        [Tooltip("应用标识（包名），必须为三段式。")]
        public string ApplicationIdentifier = string.Empty;

        /// <summary>公共版本号，格式为主版本.次版本.修订号，手动维护。</summary>
        [Tooltip("公共版本号，格式为主版本.次版本.修订号，手动维护。")]
        public string PublicVersion = "0.1.0";

        /// <summary>平台构建号，映射 Android versionCode 与 iOS buildNumber；成功构建后自动递增。</summary>
        [Tooltip("平台构建号，映射 Android versionCode 与 iOS buildNumber。")]
        public int BuildNumber;

        /// <summary>是否在构建成功后自动递增构建号；关闭后由人工维护。</summary>
        [Tooltip("是否在构建成功后自动递增构建号。")]
        public bool AutoIncrementBuildNumber = true;

        /// <summary>脚本后端，由项目技术栈与插件约束显式配置，不受 Flavor 自动控制。</summary>
        [Tooltip("脚本后端；请按项目技术栈与插件要求显式配置，Flavor 不会自动修改。")]
        public ScriptingImplementation ScriptingBackend = ScriptingImplementation.IL2CPP;

        /// <summary>API 兼容级别，默认 .NET Standard 2.1。</summary>
        [Tooltip("API 兼容级别：.NET Standard 2.1 或 .NET Framework。")]
        public ApiCompatibilityLevel ApiCompatibilityLevel =
            ApiCompatibilityLevel.NET_Standard;

        /// <summary>托管裁剪级别，正式构建建议 Medium 以上。</summary>
        [Tooltip("托管裁剪级别。")]
        public ManagedStrippingLevel ManagedStrippingLevel =
            ManagedStrippingLevel.Medium;

        /// <summary>IL2CPP 代码生成策略。</summary>
        [Tooltip("IL2CPP 代码生成策略。")]
        public Il2CppCodeGeneration Il2CppCodeGeneration =
            Il2CppCodeGeneration.OptimizeSpeed;

        /// <summary>IL2CPP C++ 编译器配置。</summary>
        [Tooltip("IL2CPP C++ 编译器配置。")]
        public Il2CppCompilerConfiguration CppCompilerConfiguration =
            Il2CppCompilerConfiguration.Release;

        /// <summary>是否启用增量式 GC；Android/iOS IL2CPP 构建建议启用。</summary>
        [Tooltip("是否启用增量式 GC。")]
        public bool IncrementalGC = true;

        /// <summary>是否启用 Development Build；正式 Profile 必须关闭。</summary>
        [Tooltip("是否启用 Development Build；正式 Profile 必须关闭。")]
        public bool DevelopmentBuild;

        /// <summary>是否启用脚本调试；仅测试与开发 Profile 启用。</summary>
        [Tooltip("是否启用脚本调试。")]
        public bool ScriptDebugging;

        /// <summary>是否自动连接 Profiler；仅开发 Profile 启用。</summary>
        [Tooltip("是否自动连接 Profiler。")]
        public bool AutoconnectProfiler;

        /// <summary>是否启用 Deep Profiling；仅开发 Profile 启用。</summary>
        [Tooltip("是否启用 Deep Profiling。")]
        public bool DeepProfiling;

        /// <summary>公共脚本宏定义，所有平台统一追加。</summary>
        [Tooltip("公共脚本宏定义，所有平台统一追加。")]
        public List<string> DefineSymbols = new List<string>();

        /// <summary>移除脚本宏定义，应用参数时从现有宏中删除。</summary>
        [Tooltip("移除脚本宏定义，应用参数时从现有宏中删除。")]
        public List<string> RemoveDefineSymbols = new List<string>();

        /// <summary>Android 是否输出 AAB；关闭时输出 APK。</summary>
        [Tooltip("Android 是否输出 AAB；关闭时输出 APK。")]
        public bool AndroidBuildAppBundle;

        /// <summary>Android 目标架构，默认仅 ARM64。</summary>
        [Tooltip("Android 目标架构。")]
        public AndroidArchitecture AndroidArchitecture =
            AndroidArchitecture.ARM64;

        /// <summary>Android 目标 SDK 版本，默认自动。</summary>
        [Tooltip("Android 目标 SDK 版本。")]
        public AndroidSdkVersions AndroidTargetSdk =
            AndroidSdkVersions.AndroidApiLevelAuto;

        /// <summary>Android Keystore 文件路径，相对项目根目录。</summary>
        [Tooltip("Android Keystore 文件路径，相对项目根目录。")]
        public string AndroidKeystoreName = string.Empty;

        /// <summary>Android Keystore 别名。</summary>
        [Tooltip("Android Keystore 别名。")]
        public string AndroidKeystoreAlias = string.Empty;

        /// <summary>Android Keystore 密码来源环境变量名，不保存密码本体。</summary>
        [Tooltip("Android Keystore 密码来源环境变量名。")]
        public string AndroidKeystorePassEnvVar = string.Empty;

        /// <summary>Android Key Alias 密码来源环境变量名，不保存密码本体。</summary>
        [Tooltip("Android Key Alias 密码来源环境变量名。")]
        public string AndroidKeyAliasPassEnvVar = string.Empty;

        /// <summary>iOS 目标 SDK：真机或模拟器。</summary>
        [Tooltip("iOS 目标 SDK。")]
        public iOSSdkVersion IosTargetSdk = iOSSdkVersion.DeviceSDK;

        /// <summary>
        /// 校验平台设置的合法性与一致性。
        /// </summary>
        /// <returns>错误描述列表；无错误时返回空列表。</returns>
        public List<string> Validate()
        {
            List<string> errors = new List<string>();

            if (!IsSupportedTarget(Target))
            {
                errors.Add(
                    $"不支持的构建平台 '{Target}'，仅支持 Windows、Linux、macOS、"
                    + "Android、iOS 与 WebGL。");
            }

            if (ScriptingBackend != ScriptingImplementation.Mono2x
                && ScriptingBackend != ScriptingImplementation.IL2CPP)
            {
                errors.Add($"不支持的脚本后端 '{ScriptingBackend}'，仅支持 Mono 与 IL2CPP。");
            }
            else if (RequiresIl2Cpp(Target)
                && ScriptingBackend != ScriptingImplementation.IL2CPP)
            {
                errors.Add($"平台 '{Target}' 只支持 IL2CPP 脚本后端。");
            }

            if (ApiCompatibilityLevel != ApiCompatibilityLevel.NET_Standard
                && ApiCompatibilityLevel != ApiCompatibilityLevel.NET_Unity_4_8)
            {
                errors.Add(
                    $"不支持的 API 兼容级别 '{ApiCompatibilityLevel}'，"
                    + "仅支持 .NET Standard 2.1 与 .NET Framework。");
            }

            if (!IsSupportedStrippingLevel(ManagedStrippingLevel))
            {
                errors.Add(
                    $"不支持的托管裁剪级别 '{ManagedStrippingLevel}'，"
                    + "仅支持 Minimal、Low、Medium 与 High。");
            }

            if (Il2CppCodeGeneration != Il2CppCodeGeneration.OptimizeSpeed
                && Il2CppCodeGeneration != Il2CppCodeGeneration.OptimizeSize)
            {
                errors.Add($"不支持的 IL2CPP 代码生成策略 '{Il2CppCodeGeneration}'。");
            }

            if (string.IsNullOrWhiteSpace(CompanyName))
            {
                errors.Add("公司名称不能为空。");
            }
            else if (string.Equals(
                         CompanyName,
                         "DefaultCompany",
                         StringComparison.Ordinal))
            {
                errors.Add("公司名称不能为 DefaultCompany，请填写实际公司名。");
            }

            if (string.IsNullOrWhiteSpace(ProductName))
            {
                errors.Add("产品名称不能为空。");
            }

            if (!IsValidApplicationIdentifier(ApplicationIdentifier))
            {
                errors.Add(
                    $"应用标识 '{ApplicationIdentifier}' 非法，必须为三段式包名，"
                    + "每段以字母开头，只允许字母、数字、下划线与点。");
            }

            if (!IsValidPublicVersion(PublicVersion))
            {
                errors.Add(
                    $"公共版本号 '{PublicVersion}' 非法，格式必须为主版本.次版本.修订号。");
            }

            if (BuildNumber < 0)
            {
                errors.Add("构建号不能为负数。");
            }

            if (Target == BuildTarget.Android)
            {
                ValidateAndroid(errors);
            }
            return errors;
        }

        /// <summary>
        /// 校验 Android 平台专有参数。
        /// </summary>
        /// <param name="errors">追加错误描述的目标列表。</param>
        private void ValidateAndroid(List<string> errors)
        {
            if (AndroidArchitecture == 0)
            {
                errors.Add("Android 目标架构不能为空，至少选择一种架构。");
            }

            if (AndroidBuildAppBundle)
            {
                if (string.IsNullOrWhiteSpace(AndroidKeystoreName))
                {
                    errors.Add("Android AAB 必须配置 Keystore 文件路径。");
                }

                if (string.IsNullOrWhiteSpace(AndroidKeystoreAlias))
                {
                    errors.Add("Android AAB 必须配置 Keystore 别名。");
                }
            }
        }

        /// <summary>
        /// 判断目标平台是否在首版支持范围内。
        /// </summary>
        /// <param name="target">待判断的构建平台。</param>
        /// <returns>支持时返回 true。</returns>
        public static bool IsSupportedTarget(BuildTarget target)
        {
            return target == BuildTarget.StandaloneWindows64
                || target == BuildTarget.StandaloneLinux64
                || target == BuildTarget.StandaloneOSX
                || target == BuildTarget.Android
                || target == BuildTarget.iOS
                || target == BuildTarget.WebGL;
        }

        /// <summary>
        /// 判断目标平台是否强制使用 IL2CPP。
        /// </summary>
        /// <param name="target">目标构建平台。</param>
        /// <returns>iOS 或 WebGL 返回 true。</returns>
        public static bool RequiresIl2Cpp(BuildTarget target)
        {
            return target == BuildTarget.iOS || target == BuildTarget.WebGL;
        }

        /// <summary>
        /// 判断托管裁剪级别是否属于 Unity 2022.3 Player Settings 提供的常用选项。
        /// </summary>
        /// <param name="level">托管裁剪级别。</param>
        /// <returns>Minimal、Low、Medium 或 High 返回 true。</returns>
        public static bool IsSupportedStrippingLevel(ManagedStrippingLevel level)
        {
            return level == ManagedStrippingLevel.Minimal
                || level == ManagedStrippingLevel.Low
                || level == ManagedStrippingLevel.Medium
                || level == ManagedStrippingLevel.High;
        }

        /// <summary>
        /// 校验应用标识是否为三段式合法包名。
        /// </summary>
        /// <param name="identifier">待校验的应用标识。</param>
        /// <returns>合法时返回 true。</returns>
        public static bool IsValidApplicationIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return false;
            }

            string[] segments = identifier.Split('.');
            if (segments.Length < 3)
            {
                return false;
            }

            foreach (string segment in segments)
            {
                if (string.IsNullOrEmpty(segment)
                    || !char.IsLetter(segment[0]))
                {
                    return false;
                }

                foreach (char character in segment)
                {
                    if (!char.IsLetterOrDigit(character)
                        && character != '_')
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 校验公共版本号格式是否为主版本.次版本.修订号。
        /// </summary>
        /// <param name="version">待校验的版本号。</param>
        /// <returns>格式合法时返回 true。</returns>
        public static bool IsValidPublicVersion(string version)
        {
            return !string.IsNullOrWhiteSpace(version)
                && PublicVersionPattern.IsMatch(version);
        }
    }
}
