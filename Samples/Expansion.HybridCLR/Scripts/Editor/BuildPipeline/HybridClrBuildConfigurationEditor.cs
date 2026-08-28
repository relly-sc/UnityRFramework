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
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("GenerateOnly"));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
