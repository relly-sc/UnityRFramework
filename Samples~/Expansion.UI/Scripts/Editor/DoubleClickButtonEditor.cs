using UnityEditor;
using UnityEditor.UI;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// DoubleClickButton Inspector。
    /// 在 Unity 原生 Button Inspector 后绘制双击参数。
    /// </summary>
    [CustomEditor(typeof(DoubleClickButton))]
    [CanEditMultipleObjects]
    public sealed class DoubleClickButtonEditor : ButtonEditor
    {
        private SerializedProperty doubleClickIntervalProperty;
        private SerializedProperty useUnscaledTimeProperty;
        private SerializedProperty onDoubleClickProperty;

        protected override void OnEnable()
        {
            base.OnEnable();
            doubleClickIntervalProperty = serializedObject.FindProperty(
                "doubleClickInterval");
            useUnscaledTimeProperty = serializedObject.FindProperty(
                "useUnscaledTime");
            onDoubleClickProperty = serializedObject.FindProperty(
                "onDoubleClick");
        }

        /// <inheritdoc />
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            serializedObject.Update();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Double Click",
                EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(doubleClickIntervalProperty);
            EditorGUILayout.PropertyField(useUnscaledTimeProperty);
            EditorGUILayout.PropertyField(onDoubleClickProperty);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
