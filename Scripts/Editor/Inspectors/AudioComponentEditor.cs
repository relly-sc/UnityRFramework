#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// AudioComponent 自定义 Inspector。
    /// </summary>
    [CustomEditor(typeof(Runtime.AudioComponent))]
    public sealed class AudioComponentEditor : UnityEditor.Editor
    {
        private SerializedProperty audioHelperTypeName;

        private void OnEnable()
        {
            audioHelperTypeName = serializedObject.FindProperty("audioHelperTypeName");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            audioHelperTypeName.stringValue = ComponentEditorUtility.HelperTypePopup(
                "Audio Helper", audioHelperTypeName.stringValue, typeof(Runtime.AudioHelperBase));

            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif
