#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// DebuggerComponent 自定义 Inspector。
    /// 使用筛选后的 KeyCode 下拉框替代默认的巨大枚举列表，解决弹窗漂移问题。
    /// </summary>
    [CustomEditor(typeof(Runtime.DebuggerComponent))]
    public sealed class DebuggerComponentEditor : UnityEditor.Editor
    {
        /// <summary>
        /// Toggle Key 可选键范围：F1-F12 + ~/Tab/Esc/Space/Enter。
        /// </summary>
        private static readonly KeyCode[] ToggleKeys =
        {
            KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4, KeyCode.F5, KeyCode.F6,
            KeyCode.F7, KeyCode.F8, KeyCode.F9, KeyCode.F10, KeyCode.F11, KeyCode.F12,
            KeyCode.BackQuote, KeyCode.Tab, KeyCode.Escape, KeyCode.Space, KeyCode.Return
        };

        /// <summary>
        /// Switch Tab Key 可选键范围：Tab + 左右箭头 + F1-F12。
        /// </summary>
        private static readonly KeyCode[] SwitchTabKeys =
        {
            KeyCode.Tab, KeyCode.LeftArrow, KeyCode.RightArrow,
            KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4, KeyCode.F5, KeyCode.F6,
            KeyCode.F7, KeyCode.F8, KeyCode.F9, KeyCode.F10, KeyCode.F11, KeyCode.F12
        };

        private SerializedProperty activeWindowType;
        private SerializedProperty toggleKey;
        private SerializedProperty windowScale;
        private SerializedProperty autoDpiScale;
        private SerializedProperty maxLogEntries;
        private SerializedProperty switchTabKey;
        private SerializedProperty infoFilter;
        private SerializedProperty warningFilter;
        private SerializedProperty errorFilter;

        private void OnEnable()
        {
            activeWindowType = serializedObject.FindProperty("activeWindowType");
            toggleKey = serializedObject.FindProperty("toggleKey");
            windowScale = serializedObject.FindProperty("windowScale");
            autoDpiScale = serializedObject.FindProperty("autoDpiScale");
            maxLogEntries = serializedObject.FindProperty("maxLogEntries");
            switchTabKey = serializedObject.FindProperty("switchTabKey");
            infoFilter = serializedObject.FindProperty("infoFilter");
            warningFilter = serializedObject.FindProperty("warningFilter");
            errorFilter = serializedObject.FindProperty("errorFilter");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Window
            EditorGUILayout.LabelField("Window", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(activeWindowType);
            DrawKeyCodePopup(toggleKey, "Toggle Key", ToggleKeys);
            EditorGUILayout.PropertyField(windowScale);
            EditorGUILayout.PropertyField(autoDpiScale);

            EditorGUILayout.Space();

            // Console
            EditorGUILayout.LabelField("Console", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(maxLogEntries);
            DrawKeyCodePopup(switchTabKey, "Switch Tab Key", SwitchTabKeys);
            EditorGUILayout.PropertyField(infoFilter);
            EditorGUILayout.PropertyField(warningFilter);
            EditorGUILayout.PropertyField(errorFilter);

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 绘制筛选后的 KeyCode 下拉框，替代默认的全枚举列表。
        /// </summary>
        /// <param name="prop">序列化属性。</param>
        /// <param name="label">显示标签。</param>
        /// <param name="allowedKeys">允许的键值列表。</param>
        private static void DrawKeyCodePopup(SerializedProperty prop, string label, KeyCode[] allowedKeys)
        {
            KeyCode current = (KeyCode)prop.intValue;
            List<string> names = new List<string>(allowedKeys.Length);
            int selectedIndex = 0;

            for (int i = 0; i < allowedKeys.Length; i++)
            {
                names.Add(allowedKeys[i].ToString());
                if (allowedKeys[i] == current)
                {
                    selectedIndex = i;
                }
            }

            int newIndex = EditorGUILayout.Popup(label, selectedIndex, names.ToArray());
            if (newIndex != selectedIndex)
            {
                prop.intValue = (int)allowedKeys[newIndex];
            }
        }
    }
}

#endif
