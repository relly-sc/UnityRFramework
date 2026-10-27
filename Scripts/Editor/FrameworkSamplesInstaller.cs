using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace UnityRFramework.Editor
{
    /// <summary>安装任务跨 Domain Reload 恢复；仅用户发起时更换包，不在启动时自动升级。</summary>
    [InitializeOnLoad]
    internal static class FrameworkSamplesInstaller
    {
        private const string JobKey = "UnityRFramework.Samples.InstallJob";
        private const string MessageKey = "UnityRFramework.Samples.InstallMessage";

        [Serializable]
        private sealed class Job
        {
            public string catalogPath;
            public string frameworkVersion;
            public string samplesVersion;
            public string[] selected;
            public string[] plannedSamples;
            public string[] approvedPackages;
            public bool import;
            public bool overwrite;
            public bool packagesRequested;
        }

        private static Job job;
        private static ListRequest listing;
        private static AddAndRemoveRequest installing;
        internal static bool IsBusy => job != null;
        internal static string Message => SessionState.GetString(MessageKey, string.Empty);

        static FrameworkSamplesInstaller()
        {
            string saved = SessionState.GetString(JobKey, string.Empty);
            if (!string.IsNullOrEmpty(saved))
            {
                try { job = JsonUtility.FromJson<Job>(saved); }
                catch (Exception error) { Finish("安装任务无法恢复：" + error.Message, true); }
            }
            EditorApplication.update += Tick;
        }

        internal static Sample[] GetSamples(FrameworkSampleCatalog catalog)
        {
            PackageInfo package = PackageInfo.FindForPackageName(FrameworkSampleCatalog.SamplesPackage);
            if (package == null) throw new InvalidOperationException("尚未安装 Samples 包。");
            PackageInfo core = PackageInfo.FindForPackageName(FrameworkSampleCatalog.CorePackage);
            if (core == null || !FrameworkSampleCatalog.IsVersionAtLeast(core.version, catalog.samplesMinimumFrameworkVersion))
                throw new InvalidOperationException($"当前核心版本 {core?.version ?? "未安装"} 低于 Samples 要求的最低核心版本 {catalog.samplesMinimumFrameworkVersion}。");
            if (package.version != catalog.samplesVersion)
                throw new InvalidOperationException($"当前 Samples {package.version}，清单要求 {catalog.samplesVersion}，请先更新 Samples 包。");
            return Sample.FindByPackage(package.name, package.version).ToArray();
        }

        internal static void Start(string catalogPath, string[] selected, bool import, bool overwrite)
        {
            if (FrameworkSampleCatalog.GetDevelopmentSamplesRoot(catalogPath) != null)
                throw new InvalidOperationException("框架开发源码已包含 Samples，无需下载或重复导入。");
            if (IsBusy || SamplesPackageInstaller.IsBusy || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请等待当前任务完成，并在非 Play Mode 下安装。");
            var catalog = FrameworkSampleCatalog.Load(catalogPath);
            var plan = catalog.Resolve(selected);
            if (plan.Length == 0) throw new InvalidOperationException("未选择扩展或示例。");
            var available = GetSamples(catalog);
            foreach (var definition in plan)
                if (!available.Any(s => s.displayName == definition.name))
                    throw new InvalidOperationException("Samples 包中未找到：" + definition.name);
            if (import && !overwrite)
                foreach (var definition in plan)
                {
                    var sample = available.First(s => s.displayName == definition.name);
                    EnsureNoPreviousImports(sample.importPath);
                }
            var installed = PackageInfo.GetAllRegisteredPackages();
            var changes = new List<FrameworkPackageDependency>();
            var lines = new List<string>();
            foreach (var dependency in catalog.GetPackages(plan))
            {
                var current = installed.FirstOrDefault(p => p.name == dependency.name);
                if (Matches(dependency, current)) continue;
                if (current != null && FrameworkPackageDependency.IsProtected(current.source))
                    throw new InvalidOperationException($"{dependency.name} 是本地或嵌入式插件（{current.version}），要求 {dependency.version}。请自行处理来源；安装器不会删除或覆盖它。");
                changes.Add(dependency);
                lines.Add($"{dependency.name}：{current?.version ?? "未安装"} → {dependency.version}（固定版本）");
            }
            if (import)
            {
                foreach (var definition in plan)
                {
                    Sample sample = available.First(s => s.displayName == definition.name);
                    if (!sample.isImported || overwrite)
                        lines.Add((sample.isImported ? "覆盖导入：" : "导入：") + definition.name);
                }
            }
            if (lines.Count == 0) lines.Add("依赖已符合版本清单；检查后无需重复安装。");
            string message = string.Join("\n", lines);
            if (changes.Count > 0)
                message += "\n\n更换插件版本可能影响项目中其他代码，请确认使用此框架版本对应的组合。";
            if (import && overwrite)
                message += "\n\n覆盖导入使用 Unity 的标准 Sample 导入流程，可能删除旧版本导入目录。请先备份其中的业务改动、场景和资源。";
            if (!EditorUtility.DisplayDialog("安装扩展与示例", message, "继续", "取消")) return;
            job = new Job
            {
                catalogPath = catalogPath, frameworkVersion = catalog.frameworkVersion,
                samplesVersion = catalog.samplesVersion,
                selected = selected, approvedPackages = changes.Select(p => p.url).ToArray(),
                plannedSamples = plan.Select(s => s.name).ToArray(),
                import = import, overwrite = overwrite
            };
            Save();
            SessionState.SetString(MessageKey, "正在检查依赖...");
        }

        internal static bool Matches(FrameworkPackageDependency dependency, PackageInfo installed) =>
            installed != null && (installed.errors == null || installed.errors.Length == 0)
            && dependency.Matches(installed.version);

        /// <summary>读取标准导入目录中仍存在的全部版本，不依赖当前安装的 Samples 包版本。</summary>
        internal static Dictionary<string, string[]> GetImportedVersions(string packageRoot)
        {
            var imports = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            if (!Directory.Exists(packageRoot)) return new Dictionary<string, string[]>();
            foreach (string directory in Directory.GetDirectories(packageRoot)
                .Where(p => FrameworkSampleCatalog.IsVersionAtLeast(Path.GetFileName(p), "0.0.0"))
                .OrderByDescending(p => Version.Parse(Path.GetFileName(p))))
            {
                string version = Path.GetFileName(directory);
                foreach (string sample in Directory.GetDirectories(directory))
                {
                    string name = Path.GetFileName(sample);
                    if (!imports.TryGetValue(name, out var versions))
                        imports.Add(name, versions = new List<string>());
                    versions.Add(version);
                }
            }
            return imports.ToDictionary(p => p.Key, p => p.Value.ToArray(), StringComparer.Ordinal);
        }

        internal static void EnsureNoPreviousImports(string importPath)
        {
            string current = Path.GetFullPath(importPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string versionRoot = Path.GetDirectoryName(current);
            string packageRoot = Path.GetDirectoryName(versionRoot);
            if (!Directory.Exists(packageRoot)) return;
            string name = Path.GetFileName(current);
            foreach (string version in Directory.GetDirectories(packageRoot))
                if (version != versionRoot && Directory.Exists(Path.Combine(version, name)))
                    throw new InvalidOperationException("检测到旧版本导入目录：" + Path.Combine(version, name)
                        + "。请先备份项目改动，再选择“同步并重新导入”，避免重复脚本或未经确认删除旧目录。");
        }

        internal static void ValidateChanges(IEnumerable<FrameworkPackageDependency> changes,
            string[] approved, bool alreadyRequested, Func<string, PackageSource?> source)
        {
            foreach (var dependency in changes)
            {
                if (alreadyRequested)
                    throw new InvalidOperationException("安装后的插件版本不符合清单：" + dependency.name);
                PackageSource? current = source(dependency.name);
                if (!approved.Contains(dependency.url)
                    || (current.HasValue && FrameworkPackageDependency.IsProtected(current.Value)))
                    throw new InvalidOperationException("插件状态已变化，请重新确认：" + dependency.name);
            }
        }

        internal static void ValidatePlan(string expectedFrameworkVersion, string actualFrameworkVersion,
            string expectedSamplesVersion, string actualSamplesVersion,
            string[] approvedSamples, IEnumerable<FrameworkSampleDefinition> plan)
        {
            if (actualFrameworkVersion != expectedFrameworkVersion
                || actualSamplesVersion != expectedSamplesVersion || approvedSamples == null
                || !approvedSamples.SequenceEqual(plan.Select(s => s.name)))
                throw new InvalidOperationException("任务期间核心版本、Samples 版本或 Sample 依赖已变化，请重新确认安装。");
        }

        private static void Save() => SessionState.SetString(JobKey, JsonUtility.ToJson(job));

        private static void Tick()
        {
            if (job == null || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                if (installing != null)
                {
                    if (!installing.IsCompleted) return;
                    if (installing.Status != StatusCode.Success)
                        throw new InvalidOperationException(installing.Error?.message ?? "Package Manager 安装失败。");
                    installing = null;
                }
                if (listing == null)
                {
                    listing = Client.List(true, true);
                    return;
                }
                if (!listing.IsCompleted) return;
                if (listing.Status != StatusCode.Success)
                    throw new InvalidOperationException(listing.Error?.message ?? "无法读取已安装插件。");
                PackageInfo[] installed = listing.Result.ToArray();
                listing = null;
                var catalog = FrameworkSampleCatalog.Load(job.catalogPath);
                var plan = catalog.Resolve(job.selected);
                ValidatePlan(job.frameworkVersion, catalog.frameworkVersion, job.samplesVersion,
                    catalog.samplesVersion, job.plannedSamples, plan);
                var changes = catalog.GetPackages(plan).Where(d =>
                    !Matches(d, installed.FirstOrDefault(p => p.name == d.name))).ToArray();
                if (changes.Length > 0)
                {
                    ValidateChanges(changes, job.approvedPackages, job.packagesRequested,
                        name => installed.FirstOrDefault(p => p.name == name)?.source);
                    job.packagesRequested = true;
                    Save();
                    SessionState.SetString(MessageKey, "正在安装固定版本依赖，编译后继续...");
                    // 同一次 UPM 操作安装整组依赖，避免中间版本组合先触发编译。
                    installing = Client.AddAndRemove(changes.Select(p => p.url).ToArray());
                    return;
                }
                if (!job.import) { Finish("固定版本依赖已就绪。", false); return; }
                var prerequisiteErrors = plan.Select(s => FrameworkSampleCatalog.GetPrerequisiteError(s,
                    name => AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == name)?.GetName().Version?.ToString(),
                    name => AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetType(name, false) != null)))
                    .Where(e => e != null).ToArray();
                if (prerequisiteErrors.Length > 0)
                {
                    Finish("UPM 依赖已就绪，但尚未导入代码：\n" + string.Join("\n", prerequisiteErrors), false);
                    return;
                }
                var available = GetSamples(catalog);
                bool overwrite = job.overwrite;
                AssetDatabase.DisallowAutoRefresh();
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (var definition in plan)
                    {
                        var sample = available.First(s => s.displayName == definition.name);
                        if (!overwrite) EnsureNoPreviousImports(sample.importPath);
                        if (sample.isImported && !overwrite) continue;
                        if (!sample.Import(Sample.ImportOptions.OverridePreviousImports))
                            throw new InvalidOperationException("Sample 导入失败：" + definition.name);
                    }
                }
                finally
                {
                    // 释放编辑锁前清除任务，避免编译重载后再次覆盖导入目录。
                    SessionState.EraseString(JobKey);
                    job = null;
                    AssetDatabase.StopAssetEditing();
                    AssetDatabase.AllowAutoRefresh();
                    // 恢复自动刷新不会主动扫描刚复制的 Sample 文件。
                    AssetDatabase.Refresh();
                }
                Finish("扩展与示例导入完成：" + string.Join(", ", plan.Select(s => s.name)), false);
            }
            catch (Exception error) { Finish("安装已停止：" + error.Message, true); }
        }

        private static void Finish(string message, bool error)
        {
            SessionState.EraseString(JobKey);
            SessionState.SetString(MessageKey, message);
            job = null;
            listing = null;
            installing = null;
            if (error) Debug.LogError("[扩展安装] " + message);
            else Debug.Log("[扩展安装] " + message);
        }
    }
}
