using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// Config 与 Localization CSV 校验和导出窗口。
    /// </summary>
    public sealed class ConfigPipelineWindow : EditorWindow
    {
        private const string PreferencesKey = "UnityRFramework.ConfigPipeline.Options";

        [SerializeField]
        private ConfigPipelineOptions options = new ConfigPipelineOptions();

        [SerializeField]
        private string reportText = string.Empty;

        private Vector2 scrollPosition;

        [MenuItem("UnityRFramework/配置表工具")]
        private static void Open()
        {
            ConfigPipelineWindow window = GetWindow<ConfigPipelineWindow>("配置表工具");
            window.minSize = new Vector2(620f, 430f);
            window.Show();
        }

        private void OnEnable()
        {
            string json = EditorPrefs.GetString(PreferencesKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    JsonUtility.FromJsonOverwrite(json, options);
                }
                catch
                {
                    options = new ConfigPipelineOptions();
                }
            }
        }

        private void OnDisable()
        {
            EditorPrefs.SetString(PreferencesKey, JsonUtility.ToJson(options));
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Config", EditorStyles.boldLabel);
            DrawFolderField("CSV 目录", ref options.ConfigSourceDirectory);
            DrawFolderField("生成代码目录", ref options.GeneratedCodeDirectory);
            DrawFolderField("二进制目录", ref options.ConfigBinaryDirectory);
            options.GeneratedNamespace = EditorGUILayout.TextField(
                new GUIContent("生成命名空间", "配置行类型与 Codec 的 C# 命名空间。"),
                options.GeneratedNamespace);

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Localization", EditorStyles.boldLabel);
            DrawFolderField("CSV 目录", ref options.LocalizationSourceDirectory);
            DrawFolderField("二进制目录", ref options.LocalizationBinaryDirectory);

            EditorGUILayout.Space(12f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("校验全部", GUILayout.Height(28f)))
                {
                    Execute(() => ConfigPipelineService.ValidateAll(options));
                }

                if (GUILayout.Button("导出 Config", GUILayout.Height(28f)))
                {
                    Execute(() => ConfigPipelineService.ExportConfig(options));
                }

                if (GUILayout.Button("导出 Localization", GUILayout.Height(28f)))
                {
                    Execute(() => ConfigPipelineService.ExportLocalization(options));
                }

                if (GUILayout.Button("全部导出", GUILayout.Height(28f)))
                {
                    Execute(() => ConfigPipelineService.ExportAll(options));
                }
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("结果", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.TextArea(reportText, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void DrawFolderField(string label, ref string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                value = EditorGUILayout.TextField(label, value);
                if (GUILayout.Button("选择", GUILayout.Width(56f)))
                {
                    string initial = ResolveAbsolutePath(value);
                    string selected = EditorUtility.OpenFolderPanel(label, initial, string.Empty);
                    if (!string.IsNullOrEmpty(selected))
                    {
                        string projectPath = ToProjectPath(selected);
                        if (projectPath == null)
                        {
                            EditorUtility.DisplayDialog(
                                "路径无效", "请选择当前 Unity 工程 Assets 目录内的文件夹。", "确定");
                        }
                        else
                        {
                            value = projectPath;
                            GUI.FocusControl(null);
                        }
                    }
                }
            }
        }

        private void Execute(Func<ConfigPipelineReport> action)
        {
            try
            {
                ConfigPipelineReport report = action();
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < report.Messages.Count; i++)
                {
                    builder.AppendLine(report.Messages[i]);
                }

                reportText = builder.ToString();
            }
            catch (Exception ex)
            {
                reportText = ex.ToString();
                Debug.LogException(ex);
            }

            Repaint();
        }

        private static string ResolveAbsolutePath(string projectPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            return string.IsNullOrEmpty(projectRoot) || string.IsNullOrEmpty(projectPath)
                ? Application.dataPath
                : Path.GetFullPath(Path.Combine(projectRoot, projectPath));
        }

        private static string ToProjectPath(string absolutePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                return null;
            }

            string root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar)
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
