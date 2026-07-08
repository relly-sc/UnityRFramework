#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// BaseComponent 自定义 Inspector。
    /// 提供 Text/Log/JSON 三个 Helper 类型下拉框和运行参数的可视化编辑。
    /// </summary>
    [CustomEditor(typeof(Runtime.BaseComponent))]
    public sealed class BaseComponentEditor : UnityEditor.Editor
    {
        private SerializedProperty textHelperTypeName;
        private SerializedProperty logHelperTypeName;
        private SerializedProperty jsonHelperTypeName;
        private SerializedProperty frameRate;
        private SerializedProperty gameSpeed;
        private SerializedProperty runInBackground;
        private SerializedProperty neverSleep;

        private void OnEnable()
        {
            textHelperTypeName = serializedObject.FindProperty("textHelperTypeName");
            logHelperTypeName = serializedObject.FindProperty("logHelperTypeName");
            jsonHelperTypeName = serializedObject.FindProperty("jsonHelperTypeName");
            frameRate = serializedObject.FindProperty("frameRate");
            gameSpeed = serializedObject.FindProperty("gameSpeed");
            runInBackground = serializedObject.FindProperty("runInBackground");
            neverSleep = serializedObject.FindProperty("neverSleep");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Helpers", EditorStyles.boldLabel);

            textHelperTypeName.stringValue = ComponentEditorUtility.HelperTypePopup(
                "Text Helper", textHelperTypeName.stringValue, typeof(RFramework.Utility.Text.ITextHelper));

            logHelperTypeName.stringValue = ComponentEditorUtility.HelperTypePopup(
                "Log Helper", logHelperTypeName.stringValue, typeof(RFramework.RFrameworkLog.ILogHelper));

            jsonHelperTypeName.stringValue = ComponentEditorUtility.HelperTypePopup(
                "JSON Helper", jsonHelperTypeName.stringValue, typeof(RFramework.Utility.Json.IJsonHelper));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Settings", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(frameRate, new GUIContent("Frame Rate"),
                GUILayout.ExpandWidth(true));
            EditorGUILayout.PropertyField(gameSpeed, new GUIContent("Game Speed"),
                GUILayout.ExpandWidth(true));
            EditorGUILayout.PropertyField(runInBackground, new GUIContent("Run In Background"),
                GUILayout.ExpandWidth(true));
            EditorGUILayout.PropertyField(neverSleep, new GUIContent("Never Sleep"),
                GUILayout.ExpandWidth(true));

            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif
