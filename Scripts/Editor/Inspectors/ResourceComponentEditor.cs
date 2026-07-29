#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// ResourceComponent 自定义 Inspector。
    /// </summary>
    [CustomEditor(typeof(Runtime.ResourceComponent))]
    public sealed class ResourceComponentEditor : RFrameworkComponentEditor
    {
        private SerializedProperty resourceHelperTypeName;
        private SerializedProperty playMode;
        private SerializedProperty packageName;
        private SerializedProperty defaultHostServer;
        private SerializedProperty fallbackHostServer;
        private SerializedProperty autoClearDiskCache;
        private SerializedProperty maxDiskCacheSizeGB;

        private void OnEnable()
        {
            resourceHelperTypeName = serializedObject.FindProperty("resourceHelperTypeName");
            playMode = serializedObject.FindProperty("playMode");
            packageName = serializedObject.FindProperty("packageName");
            defaultHostServer = serializedObject.FindProperty("defaultHostServer");
            fallbackHostServer = serializedObject.FindProperty("fallbackHostServer");
            autoClearDiskCache = serializedObject.FindProperty("autoClearDiskCache");
            maxDiskCacheSizeGB = serializedObject.FindProperty("maxDiskCacheSizeGB");
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

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Optional Disk Cache", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(autoClearDiskCache, new GUIContent("Auto Clear Cache"));
            using (new EditorGUI.DisabledScope(!autoClearDiskCache.boolValue))
            {
                EditorGUILayout.PropertyField(
                    maxDiskCacheSizeGB,
                    new GUIContent("Max Cache Size (GB)"));
            }
            EditorGUILayout.HelpBox(
                "仅对实现 IResourceCacheHelper 的资源辅助器生效。"
                + "内置 Resources 与 Local File Helper 会忽略此配置。",
                MessageType.Info);

            serializedObject.ApplyModifiedProperties();
            DrawRuntimeInformation();
        }
    }
}

#endif
