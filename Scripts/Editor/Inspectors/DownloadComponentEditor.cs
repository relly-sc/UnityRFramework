#if UNITY_EDITOR

using RFramework;
using UnityEditor;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// DownloadComponent 自定义 Inspector。
    /// </summary>
    [CustomEditor(typeof(Runtime.DownloadComponent))]
    public sealed class DownloadComponentEditor : UnityRFrameworkComponentEditorBase
    {
        private SerializedProperty archiveHelperTypeName;
        private SerializedProperty resumeByDefault;
        private SerializedProperty maxRetries;
        private SerializedProperty retryDelayMilliseconds;
        private SerializedProperty requestTimeoutMilliseconds;

        private void OnEnable()
        {
            archiveHelperTypeName = serializedObject.FindProperty("archiveHelperTypeName");
            resumeByDefault = serializedObject.FindProperty("resumeByDefault");
            maxRetries = serializedObject.FindProperty("maxRetries");
            retryDelayMilliseconds = serializedObject.FindProperty("retryDelayMilliseconds");
            requestTimeoutMilliseconds = serializedObject.FindProperty("requestTimeoutMilliseconds");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.LabelField("Helpers", EditorStyles.boldLabel);
            archiveHelperTypeName.stringValue = ComponentEditorUtility.HelperTypePopup(
                "Archive Helper", archiveHelperTypeName.stringValue, typeof(IArchiveHelper));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Download Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(resumeByDefault);
            EditorGUILayout.PropertyField(maxRetries);
            EditorGUILayout.PropertyField(retryDelayMilliseconds);
            EditorGUILayout.PropertyField(requestTimeoutMilliseconds);
            serializedObject.ApplyModifiedProperties();
            DrawRuntimeInformation();
        }
    }
}

#endif
