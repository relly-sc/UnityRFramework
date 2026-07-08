#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// Component 自定义 Inspector 通用工具。
    /// 提供 Helper 类型下拉框绘制等共享能力。
    /// </summary>
    public static class ComponentEditorUtility
    {
        /// <summary>
        /// 绘制 Helper 类型下拉框。
        /// 自动扫描当前 AppDomain 中继承自 <paramref name="baseHelperType"/> 的所有非抽象类。
        /// </summary>
        /// <param name="label">字段标签。</param>
        /// <param name="currentTypeName">当前选中的类型全名。</param>
        /// <param name="baseHelperType">Helper 基类类型。</param>
        /// <returns>用户选中的类型全名。</returns>
        public static string HelperTypePopup(string label, string currentTypeName, Type baseHelperType)
        {
            if (baseHelperType == null)
            {
                EditorGUILayout.LabelField(label, "Base helper type is null.");
                return currentTypeName;
            }

            // 扫描所有派生非抽象类型
            List<Type> helperTypes = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.IsClass && !type.IsAbstract && baseHelperType.IsAssignableFrom(type))
                        {
                            helperTypes.Add(type);
                        }
                    }
                }
                catch
                {
                    // 忽略无法加载的程序集
                }
            }

            helperTypes = helperTypes.OrderBy(t => t.FullName).ToList();

            List<string> displayNames = helperTypes.Select(t => t.FullName).ToList();

            // 当前值若不在扫描结果中，保留在首位供用户选择
            int selectedIndex = 0;
            if (!string.IsNullOrEmpty(currentTypeName))
            {
                int found = displayNames.FindIndex(n => n == currentTypeName);
                if (found >= 0)
                {
                    selectedIndex = found;
                }
                else
                {
                    displayNames.Insert(0, currentTypeName);
                    selectedIndex = 0;
                }
            }

            int newIndex = EditorGUILayout.Popup(label, selectedIndex, displayNames.ToArray());
            return displayNames[newIndex];
        }
    }
}

#endif
