using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using YooAsset.Editor;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// YooAsset 构建配置 Inspector。Package 与构建管线选项均读取 YooAsset
    /// 当前配置，避免手写名称与 Builder 不一致。
    /// </summary>
    [CustomEditor(typeof(YooAssetBuildConfiguration))]
    public sealed class YooAssetBuildConfigurationEditor : UnityEditor.Editor
    {
        /// <summary>绘制 YooAsset 构建配置。</summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty package = serializedObject.FindProperty("PackageName");
            SerializedProperty pipeline = serializedObject.FindProperty("BuildPipelineName");
            SerializedProperty clearCache = serializedObject.FindProperty("ClearBuildCache");

            string[] packageOptions = YooAssetBuildConfiguration.GetPackageOptions();
            string previousPackage = package.stringValue;
            DrawPopup(
                package,
                new GUIContent("Package Name", "来自 YooAsset Bundle Collector Setting。"),
                packageOptions,
                "未配置 Package");

            if (!string.Equals(
                    previousPackage,
                    package.stringValue,
                    StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(package.stringValue))
            {
                pipeline.stringValue =
                    BundleBuilderSetting.GetPackageBuildPipeline(package.stringValue);
                clearCache.boolValue = BundleBuilderSetting.GetPackageClearBuildCache(
                    package.stringValue,
                    pipeline.stringValue);
            }
            else if (string.IsNullOrWhiteSpace(pipeline.stringValue)
                && !string.IsNullOrWhiteSpace(package.stringValue))
            {
                pipeline.stringValue =
                    BundleBuilderSetting.GetPackageBuildPipeline(package.stringValue);
            }

            string[] pipelineOptions =
                YooAssetBuildConfiguration.GetBuildPipelineOptions();
            DrawPopup(
                pipeline,
                new GUIContent("Build Pipeline Name", "来自 YooAsset Builder 已注册构建管线。"),
                pipelineOptions,
                "未发现构建管线");

            if (!string.IsNullOrWhiteSpace(pipeline.stringValue)
                && !YooAssetBuildConfiguration.IsSupportedBuildPipeline(
                    pipeline.stringValue))
            {
                EditorGUILayout.HelpBox(
                    $"当前步骤尚未实现构建管线 '{pipeline.stringValue}'，"
                    + "请选择 Scriptable、Legacy 或 RawFile 构建管线。",
                    MessageType.Error);
            }

            SerializedProperty useCustomVersion =
                serializedObject.FindProperty("UseCustomPackageVersion");
            SerializedProperty packageVersion =
                serializedObject.FindProperty("PackageVersion");
            EditorGUILayout.PropertyField(
                useCustomVersion,
                new GUIContent(
                    "Use Custom Package Version",
                    "关闭时实时采用 YooAsset Bundle Builder 的默认版本规则。"));
            if (useCustomVersion.boolValue)
            {
                EditorGUILayout.PropertyField(
                    packageVersion,
                    new GUIContent("Package Version", "本次构建使用的自定义版本号。"));
                if (string.IsNullOrWhiteSpace(packageVersion.stringValue))
                {
                    EditorGUILayout.HelpBox(
                        "自定义版本为空，将回退到 YooAsset Builder 默认版本。",
                        MessageType.Warning);
                }
            }
            else
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField(
                        new GUIContent(
                            "Package Version",
                            "实时按 YooAsset Bundle Builder 默认规则生成，不写入配置资产。"),
                        YooAssetBuildConfiguration.GetDefaultBuilderVersion());
                }
            }
            EditorGUILayout.PropertyField(clearCache);

            string publishDirectory = string.IsNullOrWhiteSpace(package.stringValue)
                ? "（未选择 Package）"
                : Path.GetFullPath(Path.Combine(
                        BuildAssetPathField.GetProjectRoot(),
                        "Bundles",
                        package.stringValue))
                    .Replace('\\', '/');
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent(
                        "发布目录",
                        publishDirectory),
                    GUILayout.Width(EditorGUIUtility.labelWidth));
                EditorGUILayout.SelectableLabel(
                    publishDirectory,
                    EditorStyles.label,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight),
                    GUILayout.ExpandWidth(true));
            }

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("打开 YooAsset Bundle Builder"))
            {
                BundleBuilderWindow.OpenWindow();
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>绘制只允许从已有选项中选择的字符串下拉框。</summary>
        private static void DrawPopup(
            SerializedProperty property,
            GUIContent label,
            string[] options,
            string emptyMessage)
        {
            if (options == null || options.Length == 0)
            {
                EditorGUILayout.HelpBox(emptyMessage, MessageType.Warning);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField(label, property.stringValue);
                }
                return;
            }

            int current = Array.IndexOf(options, property.stringValue);
            if (current < 0)
            {
                current = 0;
                property.stringValue = options[0];
            }

            int selected = EditorGUILayout.Popup(label, current, options);
            property.stringValue = options[selected];
        }
    }
}
