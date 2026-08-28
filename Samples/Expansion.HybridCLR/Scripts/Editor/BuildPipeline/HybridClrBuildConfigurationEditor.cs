using System.Collections.Generic;
using System.Linq;
using HybridCLR.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// HybridCLR 构建配置 Inspector，为热更产物目录提供选择按钮。
    /// </summary>
    [CustomEditor(typeof(HybridClrBuildConfiguration))]
    public sealed class HybridClrBuildConfigurationEditor : UnityEditor.Editor
    {
        /// <summary>绘制 HybridCLR 构建配置。</summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            BuildAssetPathField.DrawProjectDirectory(
                serializedObject.FindProperty("OutputAssetRoot"),
                new GUIContent("Output Asset Root", "热更产物输出目录，必须位于工程内。"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("EntryTypeName"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("CodeVersion"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("IncludePdb"));

            EditorGUILayout.Space(4f);
            DrawHybridCLRSettingsReadonly();

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("打开 HybridCLR 设置"))
            {
                MenuProvider.OpenSettings();
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>显示构建时实际读取的 HybridCLR 官方程序集配置。</summary>
        private static void DrawHybridCLRSettingsReadonly()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    "HybridCLR Settings（只读）",
                    EditorStyles.boldLabel);

                HybridCLRSettings settings = HybridCLRSettings.Instance;
                EditorGUILayout.LabelField("启用", settings.enable.ToString());
                DrawReadonlyList(
                    "热更新程序集",
                    settings.hotUpdateAssemblyDefinitions?
                        .Where(item => item != null)
                        .Select(item => item.name));
                DrawReadonlyList(
                    "AOT 补充元数据程序集",
                    settings.patchAOTAssemblies);
            }
        }

        /// <summary>以不可编辑文本显示程序集名称列表。</summary>
        private static void DrawReadonlyList(
            string label,
            IEnumerable<string> values)
        {
            string text = string.Join(
                ", ",
                values?.Where(value => !string.IsNullOrWhiteSpace(value))
                    ?? Enumerable.Empty<string>());
            if (string.IsNullOrEmpty(text))
            {
                text = "未配置";
            }

            EditorGUILayout.LabelField(label);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextArea(text, GUILayout.MinHeight(32f));
            }
        }
    }
}
