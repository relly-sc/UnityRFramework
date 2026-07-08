#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// ConfigComponent 自定义 Inspector。
    /// </summary>
    [CustomEditor(typeof(Runtime.ConfigComponent))]
    public sealed class ConfigComponentEditor : UnityEditor.Editor
    {
        private SerializedProperty configHelperTypeName;

        private void OnEnable()
        {
            configHelperTypeName = serializedObject.FindProperty("configHelperTypeName");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            configHelperTypeName.stringValue = ComponentEditorUtility.HelperTypePopup(
                "Config Helper", configHelperTypeName.stringValue, typeof(Runtime.ConfigHelperBase));

            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif
