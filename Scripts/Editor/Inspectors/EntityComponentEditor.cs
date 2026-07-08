#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// EntityComponent 自定义 Inspector。
    /// </summary>
    [CustomEditor(typeof(Runtime.EntityComponent))]
    public sealed class EntityComponentEditor : UnityEditor.Editor
    {
        private SerializedProperty entityHelperTypeName;

        private void OnEnable()
        {
            entityHelperTypeName = serializedObject.FindProperty("entityHelperTypeName");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            entityHelperTypeName.stringValue = ComponentEditorUtility.HelperTypePopup(
                "Entity Helper", entityHelperTypeName.stringValue, typeof(Runtime.EntityHelperBase));

            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif
