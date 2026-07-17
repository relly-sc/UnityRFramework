#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// NetworkComponent 自定义 Inspector。
    /// </summary>
    [CustomEditor(typeof(Runtime.NetworkComponent))]
    public sealed class NetworkComponentEditor : RFrameworkComponentEditor
    {
        private SerializedProperty networkHelperTypeName;
        private SerializedProperty heartbeatInterval;
        private SerializedProperty autoReconnect;
        private SerializedProperty reconnectInterval;

        private void OnEnable()
        {
            networkHelperTypeName = serializedObject.FindProperty("networkHelperTypeName");
            heartbeatInterval = serializedObject.FindProperty("heartbeatInterval");
            autoReconnect = serializedObject.FindProperty("autoReconnect");
            reconnectInterval = serializedObject.FindProperty("reconnectInterval");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Network Helper", EditorStyles.boldLabel);
            networkHelperTypeName.stringValue = ComponentEditorUtility.HelperTypePopup(
                "Network Helper", networkHelperTypeName.stringValue, typeof(Runtime.NetworkHelperBase));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(heartbeatInterval);
            EditorGUILayout.PropertyField(autoReconnect);
            EditorGUILayout.PropertyField(reconnectInterval);

            serializedObject.ApplyModifiedProperties();
            DrawRuntimeInformation();
        }
    }
}

#endif
