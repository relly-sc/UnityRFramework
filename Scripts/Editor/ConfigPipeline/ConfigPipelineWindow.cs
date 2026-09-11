using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// Config 与 Localization CSV 校验、JSON 和二进制导出窗口。
    /// </summary>
    public sealed class ConfigPipelineWindow : EditorWindow
    {
        private const string PreferencesKey = "UnityRFramework.ConfigPipeline.Options";

        [SerializeField]
        private ConfigPipelineOptions options = new ConfigPipelineOptions();

        [SerializeField]
        private string reportText = string.Empty;

        private Vector2 scrollPosition;

        [MenuItem("UnityRFramework/CSV 配置表工具")]
        private static void Open()
        {
            ConfigPipelineWindow window = GetWindow<ConfigPipelineWindow>("CSV 配置表工具");
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
            DrawFolderField("输出目录", ref options.ConfigOutputDirectory);
            EditorGUILayout.HelpBox(
                "Json 子目录用于开发查看；Binary 子目录存放正式发布的 .bytes。",
                MessageType.None);
            options.ExportConfigBundle = EditorGUILayout.Toggle(
                new GUIContent("导出多表容器", "同时导出 JSON 与二进制多表容器。"),
                options.ExportConfigBundle);
            using (new EditorGUI.DisabledScope(!options.ExportConfigBundle))
            {
                options.ConfigBundleName = EditorGUILayout.TextField(
                    new GUIContent("容器文件名", "不含 .json 或 .bytes 扩展名。"),
                    options.ConfigBundleName);
            }
            options.ConfigReleaseFormat = (ConfigReleaseDataFormat)EditorGUILayout.Popup(
                "正式数据格式",
                (int)options.ConfigReleaseFormat,
                new[] { "框架二进制", "JSON 内容（仍输出 .bytes）" });
            options.ConfigBinaryProtection = (RFramework.ConfigProtectionMode)
                EditorGUILayout.Popup(
                    "正式二进制保护",
                    (int)options.ConfigBinaryProtection,
                    new[] { "不加密", "加密并校验完整性" });
            if (options.ConfigBinaryProtection
                == RFramework.ConfigProtectionMode.EncryptedAndAuthenticated)
            {
                options.ConfigProtectionKeyId = EditorGUILayout.TextField(
                    new GUIContent("密钥编号", "写入产物用于密钥轮换，不是密钥内容。"),
                    options.ConfigProtectionKeyId);
                options.ConfigProtectionKeyEnvironmentVariable = EditorGUILayout.TextField(
                    new GUIContent("密钥环境变量", "变量值必须是 Base64 编码的 32 字节密钥。"),
                    options.ConfigProtectionKeyEnvironmentVariable);
                options.ConfigProtectionSourceRoot = EditorGUILayout.TextField(
                    new GUIContent(
                        "运行时加载路径前缀",
                        "必须与 LoadConfigAsync 使用的路径一致，例如 Config/Binary。"),
                    options.ConfigProtectionSourceRoot);
                EditorGUILayout.HelpBox(
                    "密钥只从环境变量读取，不会保存到配置资产、EditorPrefs、报告或导出目录。",
                    MessageType.Info);
            }
            options.GeneratedNamespace = EditorGUILayout.TextField(
                new GUIContent(
                    "生成命名空间",
                    "配置行类型与 Codec 的 C# 命名空间。留空时生成到全局命名空间。"),
                options.GeneratedNamespace);

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Localization", EditorStyles.boldLabel);
            DrawFolderField("CSV 目录", ref options.LocalizationSourceDirectory);
            DrawFolderField("输出目录", ref options.LocalizationOutputDirectory);
            options.ExportLocalizationBundle = EditorGUILayout.Toggle(
                new GUIContent("导出多语言容器", "同时导出 JSON 与二进制多语言容器。"),
                options.ExportLocalizationBundle);
            using (new EditorGUI.DisabledScope(!options.ExportLocalizationBundle))
            {
                options.LocalizationBundleName = EditorGUILayout.TextField(
                    new GUIContent("容器文件名", "不含 .json 或 .bytes 扩展名。"),
                    options.LocalizationBundleName);
            }

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

            if (GUILayout.Button("分析体积/导出耗时", GUILayout.Height(26f)))
            {
                Execute(() => ConfigPipelineService.Analyze(options));
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
