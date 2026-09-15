#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// ConfigComponent 自定义 Inspector。
    /// </summary>
    [CustomEditor(typeof(Runtime.ConfigComponent))]
    public sealed class ConfigComponentEditor : UnityRFrameworkComponentEditorBase
    {
        private SerializedProperty configHelperTypeName;
        private SerializedProperty protectionMode;
        private SerializedProperty protectedSingleTableFormat;
        private SerializedProperty protectedTableBundleFormat;

        private void OnEnable()
        {
            configHelperTypeName = serializedObject.FindProperty("configHelperTypeName");
            protectionMode = serializedObject.FindProperty("protectionMode");
            protectedSingleTableFormat = serializedObject.FindProperty(
                "protectedSingleTableFormat");
            protectedTableBundleFormat = serializedObject.FindProperty(
                "protectedTableBundleFormat");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            configHelperTypeName.stringValue = ComponentEditorUtility.HelperTypePopup(
                "Config Helper", configHelperTypeName.stringValue, typeof(Runtime.ConfigHelperBase));
            EditorGUILayout.PropertyField(protectionMode, new GUIContent("数据保护"));
            if (protectionMode.enumValueIndex != 0)
            {
                EditorGUILayout.PropertyField(
                    protectedSingleTableFormat,
                    new GUIContent("单表解密后格式"));
                EditorGUILayout.PropertyField(
                    protectedTableBundleFormat,
                    new GUIContent("多表解密后格式"));
                EditorGUILayout.HelpBox(
                    "启用配置保护后，必须在首次加载前通过 SetDataProtector 注入数据保护器。",
                    MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();
            DrawRuntimeInformation();
        }
    }
}

#endif
