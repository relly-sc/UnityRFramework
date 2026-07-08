#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// UIComponent 自定义 Inspector。
    /// </summary>
    [CustomEditor(typeof(Runtime.UIComponent))]
    public sealed class UIComponentEditor : UnityEditor.Editor
    {
        private SerializedProperty uiHelperTypeName;

        private void OnEnable()
        {
            uiHelperTypeName = serializedObject.FindProperty("uiHelperTypeName");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            uiHelperTypeName.stringValue = ComponentEditorUtility.HelperTypePopup(
                "UI Helper", uiHelperTypeName.stringValue, typeof(Runtime.UIHelperBase));

            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif
