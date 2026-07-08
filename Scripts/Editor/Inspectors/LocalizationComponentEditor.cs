#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// LocalizationComponent 自定义 Inspector。
    /// </summary>
    [CustomEditor(typeof(Runtime.LocalizationComponent))]
    public sealed class LocalizationComponentEditor : UnityEditor.Editor
    {
        private SerializedProperty localizationHelperTypeName;
        private SerializedProperty defaultLanguage;

        private void OnEnable()
        {
            localizationHelperTypeName = serializedObject.FindProperty("localizationHelperTypeName");
            defaultLanguage = serializedObject.FindProperty("defaultLanguage");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Helper", EditorStyles.boldLabel);
            localizationHelperTypeName.stringValue = ComponentEditorUtility.HelperTypePopup(
                "Localization Helper", localizationHelperTypeName.stringValue, typeof(Runtime.LocalizationHelperBase));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(defaultLanguage, new GUIContent("Default Language"),
                GUILayout.ExpandWidth(true));

            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif
