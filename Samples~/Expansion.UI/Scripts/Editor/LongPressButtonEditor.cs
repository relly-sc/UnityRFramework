using UnityEditor;
using UnityEditor.UI;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// LongPressButton Inspector。
    /// 在 Unity 原生 Button Inspector 后绘制长按参数。
    /// </summary>
    [CustomEditor(typeof(LongPressButton))]
    [CanEditMultipleObjects]
    public sealed class LongPressButtonEditor : ButtonEditor
    {
        private SerializedProperty longPressDurationProperty;
        private SerializedProperty useUnscaledTimeProperty;
        private SerializedProperty onLongPressProperty;

        protected override void OnEnable()
        {
            base.OnEnable();
            longPressDurationProperty = serializedObject.FindProperty(
                "longPressDuration");
            useUnscaledTimeProperty = serializedObject.FindProperty(
                "useUnscaledTime");
            onLongPressProperty = serializedObject.FindProperty(
                "onLongPress");
        }

        /// <inheritdoc />
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            serializedObject.Update();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Long Press",
                EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(longPressDurationProperty);
            EditorGUILayout.PropertyField(useUnscaledTimeProperty);
            EditorGUILayout.PropertyField(onLongPressProperty);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
