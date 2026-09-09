#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// UIComponent 自定义 Inspector。
    /// </summary>
    [CustomEditor(typeof(Runtime.UIComponent))]
    public sealed class UIComponentEditor : UnityRFrameworkComponentEditorBase
    {
        private SerializedProperty uiHelperTypeName;
        private SerializedProperty uiRoot;
        private SerializedProperty canvasRoot;
        private SerializedProperty layerRoots;
        private SerializedProperty canvasLayerRoots;

        private void OnEnable()
        {
            uiHelperTypeName = serializedObject.FindProperty("uiHelperTypeName");
            uiRoot = serializedObject.FindProperty("uiRoot");
            canvasRoot = serializedObject.FindProperty("canvasRoot");
            layerRoots = serializedObject.FindProperty("layerRoots");
            canvasLayerRoots = serializedObject.FindProperty("canvasLayerRoots");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            uiHelperTypeName.stringValue = ComponentEditorUtility.HelperTypePopup(
                "UI Helper", uiHelperTypeName.stringValue, typeof(Runtime.UIHelperBase));
            EditorGUILayout.PropertyField(uiRoot, new GUIContent("UI Root"));
            EditorGUILayout.PropertyField(layerRoots, new GUIContent("Layer Roots"), true);
            EditorGUILayout.PropertyField(canvasRoot, new GUIContent("Independent Canvas Root"));
            EditorGUILayout.PropertyField(canvasLayerRoots,
                new GUIContent("Independent Canvas Layer Roots"), true);
            EditorGUILayout.HelpBox(
                "Layer Roots 按 Window Layer 精确匹配；未匹配时使用 UI Root。" +
                "各层容器应是 UI Root 的直接子节点，运行时按层级值排列。" +
                "需要保证跨层显示顺序时，请为每个使用的层级配置独立容器；" +
                "未映射窗口直接放入 UI Root，不保证与专用层级容器的跨层顺序。",
                MessageType.Info);

            serializedObject.ApplyModifiedProperties();
            DrawRuntimeInformation();
        }
    }
}

#endif
