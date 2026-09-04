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
            EditorGUILayout.PropertyField(serializedObject.FindProperty("JsonLeakCheck"));

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
    }
}
