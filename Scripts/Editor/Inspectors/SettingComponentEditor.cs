#if UNITY_EDITOR

using RFramework;
using UnityEditor;

namespace UnityRFramework.Editor
{
    [CustomEditor(typeof(Runtime.SettingComponent))]
    public sealed class SettingComponentEditor : UnityRFrameworkComponentEditorBase
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty helper = serializedObject.FindProperty("settingHelperTypeName");
            helper.stringValue = ComponentEditorUtility.HelperTypePopup(
                "设置存储 Helper", helper.stringValue, typeof(ISettingHelper));
            serializedObject.ApplyModifiedProperties();
            DrawRuntimeInformation();
        }
    }
}

#endif
