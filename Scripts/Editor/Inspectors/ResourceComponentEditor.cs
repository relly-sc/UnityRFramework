#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// ResourceComponent 自定义 Inspector。
    /// </summary>
    [CustomEditor(typeof(Runtime.ResourceComponent))]
    public sealed class ResourceComponentEditor : UnityEditor.Editor
    {
        private SerializedProperty resourceHelperTypeName;
        private SerializedProperty playMode;
        private SerializedProperty packageName;
        private SerializedProperty defaultHostServer;
        private SerializedProperty fallbackHostServer;

        private void OnEnable()
        {
            resourceHelperTypeName = serializedObject.FindProperty("resourceHelperTypeName");
            playMode = serializedObject.FindProperty("playMode");
            packageName = serializedObject.FindProperty("packageName");
            defaultHostServer = serializedObject.FindProperty("defaultHostServer");
            fallbackHostServer = serializedObject.FindProperty("fallbackHostServer");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Resource Helper", EditorStyles.boldLabel);
            resourceHelperTypeName.stringValue = ComponentEditorUtility.HelperTypePopup(
                "Resource Helper", resourceHelperTypeName.stringValue, typeof(Runtime.ResourceHelperBase));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(playMode);
            EditorGUILayout.PropertyField(packageName);
            EditorGUILayout.PropertyField(defaultHostServer);
            EditorGUILayout.PropertyField(fallbackHostServer);

            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif
