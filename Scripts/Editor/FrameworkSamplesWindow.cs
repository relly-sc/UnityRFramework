using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace UnityRFramework.Editor
{
    /// <summary>按核心清单安装兼容的依赖，之后导入所需扩展及示例。</summary>
    internal sealed class FrameworkSamplesWindow : EditorWindow
    {
        private FrameworkSampleCatalog catalog;
        private string catalogPath;
        private string developmentSamplesRoot;
        private PackageInfo core;
        private Vector2 scroll;
        private string error;
        private string importedSamplesRoot;
        private Dictionary<string, string[]> importedVersions;
        private string importError;

        [MenuItem("UnityRFramework/安装扩展与示例")]
        internal static void Open()
        {
            var window = GetWindow<FrameworkSamplesWindow>("扩展与示例");
            window.minSize = new Vector2(480, 360);
            window.Show();
        }

        private void OnEnable()
        {
            importedSamplesRoot = null;
            try
            {
                catalogPath = FrameworkSampleCatalog.GetCatalogPath(this);
                catalog = FrameworkSampleCatalog.Load(catalogPath);
                developmentSamplesRoot = FrameworkSampleCatalog.GetDevelopmentSamplesRoot(catalogPath);
                core = PackageInfo.FindForPackageName(FrameworkSampleCatalog.CorePackage);
                if (core != null && core.version != catalog.frameworkVersion)
                    throw new InvalidOperationException("核心包与扩展依赖清单版本不一致。");
                error = null;
            }
            catch (Exception exception) { catalog = null; error = exception.Message; }
        }

        private void OnInspectorUpdate() => Repaint();

        private void OnFocus() => importedSamplesRoot = null;

        private void OnProjectChange()
        {
            importedSamplesRoot = null;
            Repaint();
        }

        private void RefreshImports(PackageInfo samples)
        {
            string root = Path.Combine(Application.dataPath, "Samples", samples?.displayName ?? "UnityRFramework Samples");
            if (root == importedSamplesRoot) return;
            importedSamplesRoot = root;
            try
            {
                importedVersions = FrameworkSamplesInstaller.GetImportedVersions(root);
                importError = null;
            }
            catch (Exception exception)
            {
                importedVersions = null;
                importError = "无法读取示例导入目录：" + exception.Message;
            }
        }

        /// <summary>区分推荐版本、旧版及多版本导入；版本号来自目录，不推断用户文件是否修改。</summary>
        internal static string GetImportStatus(string[] versions, string availableVersion)
        {
            if (versions.Length == 0) return "未导入";
            string status = "已导入 " + string.Join("、", versions);
            if (versions.Length > 1) return status + "（多个版本，请检查重复脚本）";
            if (versions[0] == availableVersion) return status + "（与推荐版本一致）";
            return status + (FrameworkSampleCatalog.IsVersionAtLeast(availableVersion, versions[0])
                ? "（旧版，可更新至 " + availableVersion + "）"
                : "（与推荐版本 " + availableVersion + " 不同）");
        }

        private void OnGUI()
        {
            if (catalog == null) { EditorGUILayout.HelpBox(error ?? "无法读取版本清单。", MessageType.Error); return; }
            var samples = PackageInfo.FindForPackageName(FrameworkSampleCatalog.SamplesPackage);
            bool development = developmentSamplesRoot != null;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("框架 " + (core?.version ?? catalog.frameworkVersion));
                GUILayout.FlexibleSpace();
                GUILayout.Label(development ? "开发源码" : samples == null ? "Samples 未安装" : "Samples " + samples.version);
                if (GUILayout.Button(EditorGUIUtility.IconContent("Refresh", "|刷新版本与安装状态"), EditorStyles.toolbarButton,
                    GUILayout.Width(24))) OnEnable();
            }
            bool versionMatches = samples != null && samples.version == catalog.samplesVersion
                && core != null && FrameworkSampleCatalog.IsVersionAtLeast(core.version, catalog.samplesMinimumFrameworkVersion);
            if (!development) RefreshImports(samples);
            if (development)
                EditorGUILayout.HelpBox("当前为框架开发源码，直接使用本地 Samples，无需安装或重复导入。", MessageType.Info);
            else if (!versionMatches)
            {
                string message = samples == null
                    ? $"尚未安装 Samples 包，建议安装 {catalog.samplesVersion}。"
                    : samples.version != catalog.samplesVersion
                        ? $"当前 Samples {samples.version}，建议安装 {catalog.samplesVersion}。"
                        : $"当前核心版本不满足 Samples 的最低要求 {catalog.samplesMinimumFrameworkVersion}。";
                EditorGUILayout.HelpBox(message, MessageType.Warning);
                using (new EditorGUI.DisabledScope(FrameworkSamplesInstaller.IsBusy || SamplesPackageInstaller.IsBusy))
                    if (GUILayout.Button(samples == null ? "安装 Samples" : "更新 Samples")) SamplesPackageInstaller.Install();
            }
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (!development && !string.IsNullOrEmpty(importError)) EditorGUILayout.HelpBox(importError, MessageType.Error);
            if (!development && !string.IsNullOrEmpty(FrameworkSamplesInstaller.Message))
                EditorGUILayout.HelpBox(FrameworkSamplesInstaller.Message, MessageType.Info);
            var installed = PackageInfo.GetAllRegisteredPackages();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var definition in catalog.samples)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(definition.name, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(definition.description, EditorStyles.wordWrappedLabel);
                    string availableVersion = catalog.samplesVersion;
                    string[] versions = importedVersions != null && importedVersions.TryGetValue(definition.name, out var found)
                        ? found : Array.Empty<string>();
                    bool sourceExists = development && Directory.Exists(Path.Combine(developmentSamplesRoot, definition.name));
                    Color color = development ? sourceExists ? ReadyColor : PendingColor
                        : versions.Length == 0 ? Color.gray
                        : versions.Length == 1 && versions[0] == availableVersion ? ReadyColor : PendingColor;
                    var statusStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
                    statusStyle.normal.textColor = color;
                    EditorGUILayout.LabelField(development ? sourceExists ? "源码已包含" : "本地源码缺失"
                        : importedVersions == null ? "无法确认导入状态" : GetImportStatus(versions, availableVersion), statusStyle);
                    var plan = catalog.Resolve(new[] { definition.name });
                    var dependencies = catalog.GetPackages(plan);
                    foreach (var dependency in dependencies)
                    {
                        var current = installed.FirstOrDefault(p => p.name == dependency.name);
                        string status = FrameworkSamplesInstaller.Matches(dependency, current) ? "已就绪"
                            : current == null ? "未安装" : "需同步";
                        EditorGUILayout.LabelField($"{dependency.name}  {dependency.version}", EditorStyles.wordWrappedLabel);
                        var dependencyStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
                        dependencyStyle.normal.textColor = status == "已就绪" ? ReadyColor
                            : current == null ? Color.gray : PendingColor;
                        EditorGUILayout.LabelField($"当前：{current?.version ?? "无"}  ·  {status}", dependencyStyle);
                    }
                    foreach (var required in plan.Where(s => !string.IsNullOrEmpty(s.notice)))
                        EditorGUILayout.LabelField(required.notice, EditorStyles.wordWrappedLabel);
                    if (!development)
                    {
                        using (new EditorGUI.DisabledScope(!versionMatches || FrameworkSamplesInstaller.IsBusy || SamplesPackageInstaller.IsBusy
                            || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (dependencies.Length > 0 && GUILayout.Button("安装依赖")) BeginInstall(definition.name, false, false);
                            if (GUILayout.Button(dependencies.Length > 0 ? "安装并导入" : "导入")) BeginInstall(definition.name, true, false);
                            if (GUILayout.Button(dependencies.Length > 0 ? "同步并重新导入" : "重新导入")) BeginInstall(definition.name, true, true);
                        }
                    }
                    if (!string.IsNullOrEmpty(definition.helpUrl) && GUILayout.Button("官方依赖说明"))
                        Application.OpenURL(definition.helpUrl);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private static Color ReadyColor => EditorGUIUtility.isProSkin
            ? new Color(0.45f, 0.85f, 0.55f) : new Color(0.12f, 0.42f, 0.2f);

        private static Color PendingColor => EditorGUIUtility.isProSkin
            ? new Color(1f, 0.75f, 0.3f) : new Color(0.55f, 0.32f, 0.02f);

        private void BeginInstall(string name, bool import, bool overwrite)
        {
            try { error = null; FrameworkSamplesInstaller.Start(catalogPath, new[] { name }, import, overwrite); }
            catch (Exception exception) { error = exception.Message; }
        }
    }
}
