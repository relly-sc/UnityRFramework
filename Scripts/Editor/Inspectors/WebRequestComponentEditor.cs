#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// WebRequestComponent 自定义 Inspector。
    /// </summary>
    [CustomEditor(typeof(Runtime.WebRequestComponent))]
    public sealed class WebRequestComponentEditor : RFrameworkComponentEditor
    {
        private SerializedProperty webRequestHelperTypeName;
        private SerializedProperty maxConcurrentRequests;
        private SerializedProperty defaultTimeoutMs;
        private SerializedProperty maxRetries;

        private void OnEnable()
        {
            webRequestHelperTypeName = serializedObject.FindProperty("webRequestHelperTypeName");
            maxConcurrentRequests = serializedObject.FindProperty("maxConcurrentRequests");
            defaultTimeoutMs = serializedObject.FindProperty("defaultTimeoutMs");
            maxRetries = serializedObject.FindProperty("maxRetries");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("WebRequest Helper", EditorStyles.boldLabel);
            webRequestHelperTypeName.stringValue = ComponentEditorUtility.HelperTypePopup(
                "WebRequest Helper", webRequestHelperTypeName.stringValue, typeof(Runtime.WebRequestHelperBase));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(maxConcurrentRequests);
            EditorGUILayout.PropertyField(defaultTimeoutMs);
            EditorGUILayout.PropertyField(maxRetries);

            serializedObject.ApplyModifiedProperties();
            DrawRuntimeInformation();
        }
    }
}

#endif
