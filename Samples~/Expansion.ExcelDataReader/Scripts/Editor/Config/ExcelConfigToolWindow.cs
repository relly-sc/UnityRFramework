using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// Excel 配置表校验、代码生成及多格式导出窗口。
    /// </summary>
    public sealed class ExcelConfigToolWindow : EditorWindow
    {
        [SerializeField]
        private ExcelConfigExportOptions options = new ExcelConfigExportOptions();

        [SerializeField]
        private ExcelLocalizationExportOptions localizationOptions =
            new ExcelLocalizationExportOptions();

        [SerializeField]
        private string reportText = string.Empty;

        private Vector2 scrollPosition;

        /// <summary>
        /// 打开 Excel 配置表工具。
        /// </summary>
        [MenuItem("UnityRFramework/Expansion/Excel 配置表工具")]
        public static void Open()
        {
            ExcelConfigToolWindow window =
                GetWindow<ExcelConfigToolWindow>("Excel 配置表工具");
            window.minSize = new Vector2(680f, 620f);
            window.Show();
        }

        private void OnEnable()
        {
            options = ExcelConfigToolPreferences.LoadConfig();
            localizationOptions =
                ExcelConfigToolPreferences.LoadLocalization();
        }

        private void OnDisable()
        {
            ExcelConfigToolPreferences.SaveConfig(options);
            ExcelConfigToolPreferences.SaveLocalization(localizationOptions);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Config", EditorStyles.boldLabel);
            DrawFolderField("Excel 目录", ref options.SourceDirectory, true);
            DrawFolderField("输出目录", ref options.OutputDirectory, false);
            options.GenerateCode = EditorGUILayout.Toggle(
                new GUIContent("生成配置代码", "生成配置行类型与 URFC Codec。"),
                options.GenerateCode);
            using (new EditorGUI.DisabledScope(!options.GenerateCode))
            {
                DrawFolderField(
                    "生成代码目录", ref options.GeneratedCodeDirectory, false);
                options.GeneratedNamespace = EditorGUILayout.TextField(
                    new GUIContent(
                        "生成命名空间",
                        "配置行类型与 Codec 的命名空间，留空时使用全局命名空间。"),
                    options.GeneratedNamespace);
            }

            EditorGUILayout.LabelField("导出格式", EditorStyles.miniBoldLabel);
            DrawConfigExporterToggles();

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Localization", EditorStyles.boldLabel);
            DrawFolderField(
                "Excel 目录", ref localizationOptions.SourceDirectory, true);
            DrawFolderField(
                "输出目录", ref localizationOptions.OutputDirectory, false);
            localizationOptions.ExportBundle = EditorGUILayout.Toggle(
                new GUIContent(
                    "导出多语言容器",
                    "同时导出 JSON 与 URLM 二进制多语言容器。"),
                localizationOptions.ExportBundle);
            using (new EditorGUI.DisabledScope(
                !localizationOptions.ExportBundle))
            {
                localizationOptions.BundleName = EditorGUILayout.TextField(
                    new GUIContent(
                        "容器文件名",
                        "不含 .json 或 .bytes 扩展名。"),
                    localizationOptions.BundleName);
            }

            EditorGUILayout.LabelField("导出格式", EditorStyles.miniBoldLabel);
            DrawLocalizationExporterToggles();

            EditorGUILayout.Space(12f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("校验 Config", GUILayout.Height(28f)))
                {
                    Execute(() => ExcelConfigExportService.Validate(options));
                }

                if (GUILayout.Button("导出 Config", GUILayout.Height(28f)))
                {
                    Execute(() => ExcelConfigExportService.Export(options));
                }

                if (GUILayout.Button(
                    "校验 Localization", GUILayout.Height(28f)))
                {
                    Execute(() =>
                        ExcelLocalizationExportService.Validate(
                            localizationOptions));
                }

                if (GUILayout.Button(
                    "导出 Localization", GUILayout.Height(28f)))
                {
                    Execute(() =>
                        ExcelLocalizationExportService.Export(
                            localizationOptions));
                }
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("结果", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.TextArea(reportText, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void DrawConfigExporterToggles()
        {
            IReadOnlyList<IExcelConfigExporter> exporters =
                ExcelConfigExporterRegistry.GetAll();
            for (int i = 0; i < exporters.Count; i++)
            {
                IExcelConfigExporter exporter = exporters[i];
                bool selected = options.SelectedExporterIds.Contains(exporter.Id);
                bool next = EditorGUILayout.ToggleLeft(exporter.DisplayName, selected);
                if (next == selected)
                {
                    continue;
                }

                if (next)
                {
                    options.SelectedExporterIds.Add(exporter.Id);
                }
                else
                {
                    options.SelectedExporterIds.RemoveAll(
                        id => string.Equals(
                            id, exporter.Id, StringComparison.OrdinalIgnoreCase));
                }
            }
        }

        private void DrawLocalizationExporterToggles()
        {
            IReadOnlyList<IExcelLocalizationExporter> exporters =
                ExcelLocalizationExporterRegistry.GetAll();
            for (int i = 0; i < exporters.Count; i++)
            {
                IExcelLocalizationExporter exporter = exporters[i];
                bool selected =
                    localizationOptions.SelectedExporterIds.Contains(exporter.Id);
                bool next =
                    EditorGUILayout.ToggleLeft(exporter.DisplayName, selected);
                if (next == selected)
                {
                    continue;
                }

                if (next)
                {
                    localizationOptions.SelectedExporterIds.Add(exporter.Id);
                }
                else
                {
                    localizationOptions.SelectedExporterIds.RemoveAll(
                        id => string.Equals(
                            id,
                            exporter.Id,
                            StringComparison.OrdinalIgnoreCase));
                }
            }
        }

        private void DrawFolderField(
            string label, ref string value, bool allowExternal)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                value = EditorGUILayout.TextField(label, value);
                if (!GUILayout.Button("选择", GUILayout.Width(56f)))
                {
                    return;
                }

                string selected = EditorUtility.OpenFolderPanel(
                    label, ResolveAbsolutePath(value), string.Empty);
                if (string.IsNullOrEmpty(selected))
                {
                    return;
                }

                string projectPath = ToProjectPath(selected);
                if (!allowExternal && projectPath == null)
                {
                    EditorUtility.DisplayDialog(
                        "路径无效", "输出目录必须位于当前 Unity 工程的 Assets 下。", "确定");
                    return;
                }

                value = projectPath ?? Path.GetFullPath(selected);
                GUI.FocusControl(null);
            }
        }

        private void Execute(Func<ExcelConfigExportReport> action)
        {
            try
            {
                ExcelConfigExportReport report = action();
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < report.Messages.Count; i++)
                {
                    builder.AppendLine(report.Messages[i]);
                }

                reportText = builder.ToString();
                ExcelConfigToolPreferences.SaveConfig(options);
                ExcelConfigToolPreferences.SaveLocalization(
                    localizationOptions);
            }
            catch (Exception ex)
            {
                reportText = ex.ToString();
                Debug.LogException(ex);
            }

            Repaint();
        }

        private static string ResolveAbsolutePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Application.dataPath;
            }

            if (Path.IsPathRooted(path))
            {
                return Path.GetFullPath(path);
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            return string.IsNullOrEmpty(projectRoot)
                ? Application.dataPath
                : Path.GetFullPath(Path.Combine(projectRoot, path));
        }

        private static string ToProjectPath(string absolutePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                return null;
            }

            string root = Path.GetFullPath(projectRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(absolutePath);
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string relative = path.Substring(root.Length).Replace('\\', '/');
            return relative.Equals("Assets", StringComparison.Ordinal)
                || relative.StartsWith("Assets/", StringComparison.Ordinal)
                ? relative
                : null;
        }
    }
}
