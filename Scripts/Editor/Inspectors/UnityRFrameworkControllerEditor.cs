#if UNITY_EDITOR

using RFramework;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// UnityRFrameworkController 的 Helper 与运行参数 Inspector。
    /// </summary>
    [CustomEditor(typeof(Runtime.UnityRFrameworkController))]
    public sealed class UnityRFrameworkControllerEditor : UnityRFrameworkComponentEditorBase
    {
        private SerializedProperty logSinkTypeName;
        private SerializedProperty jsonHelperTypeName;
        private SerializedProperty frameRate;
        private SerializedProperty gameSpeed;
        private SerializedProperty runInBackground;
        private SerializedProperty neverSleep;

        private void OnEnable()
        {
            logSinkTypeName = serializedObject.FindProperty("logSinkTypeName");
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
            logSinkTypeName.stringValue = ComponentEditorUtility.HelperTypePopup(
                "Log Sink", logSinkTypeName.stringValue, typeof(ILogSink));
            jsonHelperTypeName.stringValue = ComponentEditorUtility.HelperTypePopup(
                "JSON Helper", jsonHelperTypeName.stringValue,
                typeof(RFramework.Utility.Json.IJsonHelper));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(frameRate, new GUIContent("Frame Rate"));
            EditorGUILayout.PropertyField(gameSpeed, new GUIContent("Game Speed"));
            EditorGUILayout.PropertyField(
                runInBackground, new GUIContent("Run In Background"));
            EditorGUILayout.PropertyField(neverSleep, new GUIContent("Never Sleep"));

            serializedObject.ApplyModifiedProperties();
            DrawRuntimeInformation();
        }
    }
}

#endif
