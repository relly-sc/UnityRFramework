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

        private void OnEnable()
        {
            localizationHelperTypeName = serializedObject.FindProperty("localizationHelperTypeName");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            localizationHelperTypeName.stringValue = ComponentEditorUtility.HelperTypePopup(
                "Localization Helper", localizationHelperTypeName.stringValue, typeof(Runtime.LocalizationHelperBase));

            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif
