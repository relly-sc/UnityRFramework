using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace UnityRFramework.Editor
{
    [Serializable]
    internal sealed class FrameworkPackageDependency
    {
        public string name;
        public string version;
        public string url;

        internal bool Matches(string installedVersion, PackageSource source, string revision)
        {
            if (installedVersion != version) return false;
            return source != PackageSource.Git || revision == url.Substring(url.LastIndexOf('#') + 1);
        }

        internal static bool IsProtected(PackageSource source) =>
            source == PackageSource.Local || source == PackageSource.Embedded;
    }

    [Serializable]
    internal sealed class FrameworkSampleDefinition
    {
        public string name;
        public string description;
        public string[] dependencies;
        public string[] packages;
        public string requiredAssembly;
        public string assemblyVersion;
        public string requiredType;
        public string notice;
        public string helpUrl;
    }

    /// <summary>随框架发布的固定版本清单；核心不引用任何第三方类型。</summary>
    [Serializable]
    internal sealed class FrameworkSampleCatalog
    {
        internal const string CorePackage = "com.relly-sc.unityrframework";
        internal const string SamplesPackage = "com.relly-sc.unityrframework.samples";
        public string frameworkVersion;
        public FrameworkPackageDependency[] packages;
        public FrameworkSampleDefinition[] samples;

        internal static FrameworkSampleCatalog Load(string path)
        {
            var catalog = JsonUtility.FromJson<FrameworkSampleCatalog>(File.ReadAllText(path));
            catalog.Validate();
            return catalog;
        }

        internal void Validate()
        {
            if (string.IsNullOrEmpty(frameworkVersion) || samples == null || packages == null)
                throw new InvalidDataException("扩展安装清单缺少版本、Samples 或依赖信息。");
            if (samples.Any(s => s == null || string.IsNullOrEmpty(s.name))
                || packages.Any(p => p == null || string.IsNullOrEmpty(p.name)
                    || string.IsNullOrEmpty(p.version) || string.IsNullOrEmpty(p.url)
                    || !p.url.StartsWith("https://", StringComparison.Ordinal)
                    || p.url.LastIndexOf('#') < 0 || p.url.EndsWith("#", StringComparison.Ordinal))
                || samples.Select(s => s.name).Distinct().Count() != samples.Length
                || packages.Select(p => p.name).Distinct().Count() != packages.Length)
                throw new InvalidDataException("扩展安装清单包含重复名称或未固定版本的依赖。");
            foreach (var sample in samples) Resolve(new[] { sample.name });
        }

        /// <summary>按依赖优先排序并去重；未知项或循环引用不允许进入安装阶段。</summary>
        internal FrameworkSampleDefinition[] Resolve(IEnumerable<string> selected)
        {
            var definitions = samples.ToDictionary(s => s.name, StringComparer.Ordinal);
            var result = new List<FrameworkSampleDefinition>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in selected) Visit(name);
            return result.ToArray();

            void Visit(string name)
            {
                if (visited.Contains(name)) return;
                if (!definitions.TryGetValue(name, out var sample))
                    throw new InvalidDataException("未声明的 Sample 依赖：" + name);
                if (!visiting.Add(name)) throw new InvalidDataException("Sample 依赖循环：" + name);
                foreach (string dependency in sample.dependencies ?? Array.Empty<string>()) Visit(dependency);
                foreach (string dependency in sample.packages ?? Array.Empty<string>())
                    if (!packages.Any(p => p.name == dependency))
                        throw new InvalidDataException("未声明的插件依赖：" + dependency);
                visiting.Remove(name);
                visited.Add(name);
                result.Add(sample);
            }
        }

        internal FrameworkPackageDependency[] GetPackages(IEnumerable<FrameworkSampleDefinition> plan)
        {
            var names = new HashSet<string>(plan.SelectMany(s => s.packages ?? Array.Empty<string>()));
            return packages.Where(p => names.Contains(p.name)).ToArray();
        }

        /// <summary>不在缺少 NuGet 本体或生成类型时导入会导致编译失败的脚本。</summary>
        internal static string GetPrerequisiteError(FrameworkSampleDefinition sample,
            Func<string, string> assemblyVersion, Func<string, bool> hasType)
        {
            if (!string.IsNullOrEmpty(sample.requiredAssembly)
                && assemblyVersion(sample.requiredAssembly) != sample.assemblyVersion)
                return sample.name + "：" + sample.notice;
            if (!string.IsNullOrEmpty(sample.requiredType) && !hasType(sample.requiredType))
                return sample.name + "：" + sample.notice;
            return null;
        }

        internal static string GetDevelopmentSamplesRoot(string catalogPath)
        {
            string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(catalogPath), "../.."));
            // 发布包不包含 Tools~，不能仅凭项目名称或已导入的 Samples 目录判断。
            return File.Exists(Path.Combine(root, "Tools~", "SamplesPackage.json"))
                ? Path.Combine(root, "Samples") : null;
        }

        internal static string GetCatalogPath(EditorWindow window)
        {
            var package = PackageInfo.FindForAssembly(typeof(FrameworkSampleCatalog).Assembly);
            string root = package != null ? package.resolvedPath
                : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(
                    AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(window))), "../.."));
            return Path.Combine(root, "Scripts", "Editor", "FrameworkSamples.json");
        }
    }
}
