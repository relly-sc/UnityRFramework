using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// Config 构建配置 Inspector，为所有输入、代码生成和输出目录提供选择按钮。
    /// </summary>
    [CustomEditor(typeof(ConfigExportBuildConfiguration))]
    public sealed class ConfigExportBuildConfigurationEditor : UnityEditor.Editor
    {
        /// <summary>绘制 Config 构建配置。</summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty options = serializedObject.FindProperty("Options");
            if (options == null)
            {
                EditorGUILayout.HelpBox(
                    "Options 字段序列化失败。",
                    MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("路径", EditorStyles.boldLabel);
            DrawDirectory(options, "ConfigSourceDirectory", "Config CSV 源目录");
            DrawDirectory(
                options,
                "LocalizationSourceDirectory",
                "Localization CSV 源目录");
            DrawDirectory(options, "GeneratedCodeDirectory", "生成代码目录");
            DrawDirectory(options, "ConfigOutputDirectory", "Config 输出目录");
            DrawDirectory(
                options,
                "LocalizationOutputDirectory",
                "Localization 输出目录");

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Config", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                options.FindPropertyRelative("ExportConfigBundle"));
            EditorGUILayout.PropertyField(
                options.FindPropertyRelative("ConfigBundleName"));
            SerializedProperty releaseFormat = options.FindPropertyRelative(
                "ConfigReleaseFormat");
            releaseFormat.enumValueIndex = EditorGUILayout.Popup(
                "正式数据格式",
                releaseFormat.enumValueIndex,
                new[] { "框架二进制", "JSON 内容（仍输出 .bytes）" });
            SerializedProperty protection = options.FindPropertyRelative(
                "ConfigBinaryProtection");
            protection.enumValueIndex = EditorGUILayout.Popup(
                "正式二进制保护",
                protection.enumValueIndex,
                new[] { "不加密", "加密并校验完整性" });
            if (protection.enumValueIndex != 0)
            {
                EditorGUILayout.PropertyField(
                    options.FindPropertyRelative("ConfigProtectionKeyId"),
                    new GUIContent("密钥编号"));
                EditorGUILayout.PropertyField(
                    options.FindPropertyRelative("ConfigProtectionKeyEnvironmentVariable"),
                    new GUIContent("密钥环境变量"));
                EditorGUILayout.PropertyField(
                    options.FindPropertyRelative("ConfigProtectionSourceRoot"),
                    new GUIContent("运行时加载路径前缀"));
                EditorGUILayout.HelpBox(
                    "环境变量值必须是 Base64 编码的 32 字节密钥；密钥内容不会写入资产。",
                    MessageType.Info);
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Localization", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                options.FindPropertyRelative("ExportLocalizationBundle"));
            EditorGUILayout.PropertyField(
                options.FindPropertyRelative("LocalizationBundleName"));

            EditorGUILayout.Space(4f);
            EditorGUILayout.PropertyField(
                options.FindPropertyRelative("GeneratedNamespace"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ExportJson"));

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("正式发布检查", EditorStyles.boldLabel);
            SerializedProperty leakCheck = serializedObject.FindProperty("ReleaseLeakCheck");
            EditorGUILayout.PropertyField(leakCheck, new GUIContent("检查配置泄漏"));
            if (leakCheck.boolValue)
            {
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("BlockReleaseBuildOnLeak"),
                    new GUIContent("正式构建发现泄漏时阻止"));
                DrawAllowedPaths(serializedObject.FindProperty("LeakCheckAllowedPaths"));
                EditorGUILayout.HelpBox(
                    "检查 Resources、StreamingAssets、启用场景依赖及当前 YooAsset Package。"
                    + "Development Build 只告警；允许路径应仅填写确认可公开的文件或目录。",
                    MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>绘制 Options 下的工程内目录字段。</summary>
        private static void DrawDirectory(
            SerializedProperty options,
            string propertyName,
            string label)
        {
            BuildAssetPathField.DrawProjectDirectory(
                options.FindPropertyRelative(propertyName),
                new GUIContent(label));
        }

        /// <summary>
        /// 绘制允许路径列表。每项接受 Project 视图中的文件或目录拖拽，
        /// 同时保留文件和目录选择入口。
        /// </summary>
        private static void DrawAllowedPaths(SerializedProperty paths)
        {
            if (paths == null)
            {
                return;
            }

            paths.isExpanded = EditorGUILayout.Foldout(
                paths.isExpanded,
                $"允许路径（{paths.arraySize}）",
                true);
            if (!paths.isExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            for (int i = 0; i < paths.arraySize; i++)
            {
                SerializedProperty item = paths.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawAllowedPathObjectField(item, i);
                    if (GUILayout.Button("文件", GUILayout.Width(42f)))
                    {
                        string selected = BuildAssetPathField.PickProjectFile(
                            item.stringValue,
                            string.Empty);
                        if (selected != null)
                        {
                            item.stringValue = selected;
                        }
                    }

                    if (GUILayout.Button("目录", GUILayout.Width(42f)))
                    {
                        string selected = BuildAssetPathField.PickProjectDirectory(
                            item.stringValue);
                        if (selected != null)
                        {
                            item.stringValue = selected;
                        }
                    }

                    if (GUILayout.Button("-", GUILayout.Width(24f)))
                    {
                        paths.DeleteArrayElementAtIndex(i);
                        i--;
                    }
                }
            }

            if (GUILayout.Button("添加允许路径"))
            {
                paths.arraySize++;
                paths.GetArrayElementAtIndex(paths.arraySize - 1).stringValue = string.Empty;
            }

            EditorGUI.indentLevel--;
        }

        private static void DrawAllowedPathObjectField(
            SerializedProperty item,
            int index)
        {
            Object current = string.IsNullOrWhiteSpace(item.stringValue)
                ? null
                : AssetDatabase.LoadMainAssetAtPath(item.stringValue);
            GUIContent label = new GUIContent(
                $"路径 {index + 1}",
                string.IsNullOrWhiteSpace(item.stringValue)
                    ? "从 Project 视图拖入文件或文件夹。"
                    : item.stringValue);

            EditorGUI.BeginChangeCheck();
            Object selected = EditorGUILayout.ObjectField(
                label,
                current,
                typeof(Object),
                false);
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            if (selected == null)
            {
                item.stringValue = string.Empty;
                return;
            }

            string selectedPath = AssetDatabase.GetAssetPath(selected)
                .Replace('\\', '/');
            if (!selectedPath.Equals("Assets", System.StringComparison.Ordinal)
                && !selectedPath.StartsWith(
                    "Assets/",
                    System.StringComparison.Ordinal))
            {
                Debug.LogWarning(
                    $"正式发布检查允许路径必须位于 Assets：'{selectedPath}'。");
                return;
            }

            item.stringValue = selectedPath;
        }
    }
}
