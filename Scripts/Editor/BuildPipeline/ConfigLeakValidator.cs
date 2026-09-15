using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>检查正式产物是否包含开发配置源、开发 JSON 或密钥文件。</summary>
    public sealed class ConfigLeakValidator : IBuildValidator
    {
        private const string Code = "CONFIG_LEAK";
        private const string ScanCode = "CONFIG_LEAK_SCAN";
        private const string Group = "配置发布安全";

        /// <summary>获取校验器唯一 Id。</summary>
        public string Id => "core.config-leak";

        /// <summary>执行只读泄漏检查。</summary>
        public void Validate(
            BuildValidationContext context,
            ICollection<BuildValidationIssue> issues)
        {
            if (context?.Profile == null || issues == null
                || !BuildStepConfigLocator.HasEnabledEntry(context.Profile, "config"))
            {
                return;
            }

            ConfigExportBuildConfiguration settings =
                BuildStepConfigLocator.GetConfiguration<ConfigExportBuildConfiguration>(
                    context.Profile,
                    "config");
            if (settings == null || !settings.ReleaseLeakCheck)
            {
                return;
            }

            ConfigLeakScanResult result = ConfigLeakScanner.Scan(context, settings);
            bool block = context.Profile.Flavor == BuildProfileFlavor.Release
                && !context.Profile.Platform.DevelopmentBuild
                && settings.BlockReleaseBuildOnLeak;
            for (int i = 0; i < result.Findings.Count; i++)
            {
                ConfigLeakFinding finding = result.Findings[i];
                string message = $"{finding.Reason}：{finding.AssetPath}。";
                issues.Add(block
                    ? BuildValidationIssue.Error(Code, message, Group)
                    : BuildValidationIssue.Warning(Code, message, Group));
            }

            for (int i = 0; i < result.Warnings.Count; i++)
            {
                issues.Add(BuildValidationIssue.Warning(
                    ScanCode,
                    result.Warnings[i],
                    Group));
            }
        }
    }

    /// <summary>配置泄漏扫描结果。</summary>
    internal sealed class ConfigLeakScanResult
    {
        internal List<ConfigLeakFinding> Findings { get; } =
            new List<ConfigLeakFinding>();

        internal List<string> Warnings { get; } = new List<string>();
    }

    /// <summary>单条确定性泄漏证据。</summary>
    internal readonly struct ConfigLeakFinding
    {
        internal ConfigLeakFinding(string assetPath, string reason)
        {
            AssetPath = assetPath;
            Reason = reason;
        }

        internal string AssetPath { get; }

        internal string Reason { get; }
    }

    /// <summary>收集实际打包路径并识别确定性的配置泄漏。</summary>
    internal static class ConfigLeakScanner
    {
        private static readonly HashSet<string> SensitiveExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".key", ".pem", ".p12", ".pfx", ".jks", ".keystore", ".env"
            };

        private static readonly HashSet<string> DevelopmentManifestNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "UnityRFramework.ConfigCode.manifest",
                "UnityRFramework.ConfigJson.manifest",
                "UnityRFramework.LocalizationJson.manifest"
            };

        internal static ConfigLeakScanResult Scan(
            BuildValidationContext context,
            ConfigExportBuildConfiguration settings)
        {
            ConfigLeakScanResult result = new ConfigLeakScanResult();
            ConfigPipelineOptions options = settings.Options ?? new ConfigPipelineOptions();
            bool generatedJsonWillBeRemoved = WillRemoveGeneratedJson(context, settings);
            HashSet<string> includedPaths = CollectIncludedAssetPaths(context, result.Warnings);
            HashSet<string> reportedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string rawPath in includedPaths)
            {
                string path = NormalizeAssetPath(rawPath);
                if (string.IsNullOrEmpty(path)
                    || AssetDatabase.IsValidFolder(path)
                    || IsAllowed(path, settings.LeakCheckAllowedPaths)
                    || !TryGetLeakReason(
                        path,
                        options,
                        generatedJsonWillBeRemoved,
                        out string reason)
                    || !reportedPaths.Add(path))
                {
                    continue;
                }

                result.Findings.Add(new ConfigLeakFinding(path, reason));
            }

            result.Findings.Sort(
                (left, right) => string.CompareOrdinal(left.AssetPath, right.AssetPath));
            return result;
        }

        private static HashSet<string> CollectIncludedAssetPaths(
            BuildValidationContext context,
            ICollection<string> warnings)
        {
            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] allAssets = AssetDatabase.GetAllAssetPaths();
            for (int i = 0; i < allAssets.Length; i++)
            {
                string path = NormalizeAssetPath(allAssets[i]);
                if (IsResourcesAsset(path) || IsWithin(path, "Assets/StreamingAssets"))
                {
                    paths.Add(path);
                }
            }

            try
            {
                AddSceneDependencies(context.Profile, paths);
            }
            catch (Exception exception)
            {
                warnings.Add(
                    $"读取启用场景依赖失败：{exception.Message}。"
                    + "本次仍检查 Resources、StreamingAssets 和 YooAsset 收集结果。");
            }
            AddYooAssetCollectedPaths(context.Profile, paths, warnings);
            return paths;
        }

        internal static void AddSceneDependencies(
            UnityRFrameworkBuildProfile profile,
            ISet<string> paths,
            Func<string, string[]> dependencyResolver = null)
        {
            if (profile?.Scenes == null)
            {
                return;
            }

            for (int i = 0; i < profile.Scenes.Count; i++)
            {
                BuildSceneEntry entry = profile.Scenes[i];
                if (entry == null || !entry.Enabled || entry.Scene == null)
                {
                    continue;
                }

                string scenePath = AssetDatabase.GetAssetPath(entry.Scene);
                string[] dependencies = dependencyResolver != null
                    ? dependencyResolver(scenePath)
                    : AssetDatabase.GetDependencies(scenePath, true);
                for (int j = 0; j < dependencies.Length; j++)
                {
                    paths.Add(NormalizeAssetPath(dependencies[j]));
                }
            }
        }

        private static void AddYooAssetCollectedPaths(
            UnityRFrameworkBuildProfile profile,
            ISet<string> paths,
            ICollection<string> warnings)
        {
            BuildStepSettings entry = BuildStepConfigLocator.FindEntry(profile, "yooasset");
            if (entry == null || !entry.Enabled || entry.Configuration == null)
            {
                return;
            }

            SerializedObject configuration = new SerializedObject(entry.Configuration);
            string packageName = configuration.FindProperty("PackageName")?.stringValue?.Trim();
            if (string.IsNullOrEmpty(packageName))
            {
                return;
            }

            Type settingDataType = FindType("YooAsset.Editor.BundleCollectorSettingData");
            if (settingDataType == null)
            {
                return;
            }

            try
            {
                PropertyInfo settingProperty = settingDataType.GetProperty(
                    "Setting",
                    BindingFlags.Public | BindingFlags.Static);
                object setting = settingProperty?.GetValue(null);
                MethodInfo beginCollect = setting?.GetType().GetMethod(
                    "BeginCollect",
                    BindingFlags.Public | BindingFlags.Instance);
                object collectResult = beginCollect?.Invoke(
                    setting,
                    new object[] { packageName, true, false });
                PropertyInfo assetsProperty = collectResult?.GetType().GetProperty("CollectAssets");
                IEnumerable assets = assetsProperty?.GetValue(collectResult) as IEnumerable;
                if (assets == null)
                {
                    warnings.Add(
                        $"无法读取 YooAsset Package '{packageName}' 的实际收集结果，"
                        + "本次仅检查 Resources、StreamingAssets 和场景依赖。");
                    return;
                }

                foreach (object asset in assets)
                {
                    AddEditorAssetInfoPath(GetPropertyValue(asset, "AssetInfo"), paths);
                    IEnumerable dependencies = GetPropertyValue(asset, "DependAssets") as IEnumerable;
                    if (dependencies == null)
                    {
                        continue;
                    }

                    foreach (object dependency in dependencies)
                    {
                        AddEditorAssetInfoPath(dependency, paths);
                    }
                }
            }
            catch (Exception exception)
            {
                Exception cause = exception is TargetInvocationException invocation
                    && invocation.InnerException != null
                        ? invocation.InnerException
                        : exception;
                warnings.Add(
                    $"读取 YooAsset Package '{packageName}' 收集结果失败：{cause.Message}。"
                    + "本次仍检查 Resources、StreamingAssets 和场景依赖。");
            }
        }

        private static void AddEditorAssetInfoPath(object assetInfo, ISet<string> paths)
        {
            if (assetInfo == null)
            {
                return;
            }

            FieldInfo field = assetInfo.GetType().GetField(
                "AssetPath",
                BindingFlags.Public | BindingFlags.Instance);
            string path = field?.GetValue(assetInfo) as string;
            if (!string.IsNullOrWhiteSpace(path))
            {
                paths.Add(NormalizeAssetPath(path));
            }
        }

        private static object GetPropertyValue(object target, string propertyName)
        {
            return target?.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance)?.GetValue(target);
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static bool TryGetLeakReason(
            string path,
            ConfigPipelineOptions options,
            bool generatedJsonWillBeRemoved,
            out string reason)
        {
            string extension = Path.GetExtension(path);
            if (SensitiveExtensions.Contains(extension) || ContainsPrivateKeyMarker(path))
            {
                reason = "检测到密钥或证书文件进入构建内容";
                return true;
            }

            if (IsConfigSource(path, options))
            {
                reason = "检测到 CSV/Excel 配置源进入构建内容";
                return true;
            }

            if (!generatedJsonWillBeRemoved && IsDevelopmentOutput(path, options))
            {
                reason = "检测到开发 JSON 或开发 manifest 进入构建内容";
                return true;
            }

            if (DevelopmentManifestNames.Contains(Path.GetFileName(path)))
            {
                reason = "检测到开发 manifest 进入构建内容";
                return true;
            }

            reason = string.Empty;
            return false;
        }

        private static bool WillRemoveGeneratedJson(
            BuildValidationContext context,
            ConfigExportBuildConfiguration settings)
        {
            if (settings.ExportJson)
            {
                return false;
            }

            BuildRecipePlan plan = BuildRecipePlanner.Create(
                context.Profile,
                recipeOverride: context.Recipe);
            if (!plan.IsValid)
            {
                return false;
            }

            for (int i = 0; i < plan.StepIds.Count; i++)
            {
                if (string.Equals(plan.StepIds[i], "config", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsConfigSource(string path, ConfigPipelineOptions options)
        {
            string extension = Path.GetExtension(path);
            bool sourceExtension = extension.Equals(".csv", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".xls", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase);
            return sourceExtension
                && (IsWithin(path, options.ConfigSourceDirectory)
                    || IsWithin(path, options.LocalizationSourceDirectory));
        }

        private static bool IsDevelopmentOutput(string path, ConfigPipelineOptions options)
        {
            string configJsonRoot = CombineAssetPath(options.ConfigOutputDirectory, "Json");
            string localizationJsonRoot = CombineAssetPath(
                options.LocalizationOutputDirectory,
                "Json");
            if (!IsWithin(path, configJsonRoot) && !IsWithin(path, localizationJsonRoot))
            {
                return false;
            }

            return Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase)
                || DevelopmentManifestNames.Contains(Path.GetFileName(path));
        }

        private static bool ContainsPrivateKeyMarker(string assetPath)
        {
            string extension = Path.GetExtension(assetPath);
            if (!extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".bytes", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string fullPath = Path.GetFullPath(Path.Combine(
                BuildAssetPathField.GetProjectRoot(),
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
            FileInfo info = new FileInfo(fullPath);
            if (!info.Exists || info.Length > 1024 * 1024)
            {
                return false;
            }

            try
            {
                string content = File.ReadAllText(fullPath);
                return content.IndexOf("-----BEGIN PRIVATE KEY-----", StringComparison.Ordinal) >= 0
                    || content.IndexOf("-----BEGIN RSA PRIVATE KEY-----", StringComparison.Ordinal) >= 0
                    || content.IndexOf("-----BEGIN EC PRIVATE KEY-----", StringComparison.Ordinal) >= 0;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static bool IsAllowed(string path, IList<string> allowedPaths)
        {
            if (allowedPaths == null)
            {
                return false;
            }

            for (int i = 0; i < allowedPaths.Count; i++)
            {
                if (IsWithin(path, allowedPaths[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsResourcesAsset(string path)
        {
            return path.StartsWith("Assets/Resources/", StringComparison.OrdinalIgnoreCase)
                || path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsWithin(string path, string root)
        {
            string normalizedPath = NormalizeAssetPath(path).TrimEnd('/');
            string normalizedRoot = NormalizeAssetPath(root).TrimEnd('/');
            if (string.IsNullOrEmpty(normalizedPath) || string.IsNullOrEmpty(normalizedRoot))
            {
                return false;
            }

            return normalizedPath.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                || normalizedPath.StartsWith(
                    normalizedRoot + "/",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string CombineAssetPath(string left, string right)
        {
            return NormalizeAssetPath(left).TrimEnd('/') + "/" + right.TrimStart('/');
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Trim().Replace('\\', '/');
        }
    }
}
