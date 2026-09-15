using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// Profile 步骤条目（Steps 数组）的数据操作实现：追加条目、初始化全部
    /// 已注册步骤与按索引移除条目，由构建工具窗口统一调用。
    /// </summary>
    internal static class BuildStepEntriesEditor
    {
        /// <summary>
        /// 在 Steps 数组末尾追加一个新条目，可指定步骤 Id 与默认启用态。
        /// 步骤 Id 留空时条目保持可编辑但不会被识别为已注册步骤。
        /// </summary>
        /// <param name="serializedObject">Profile 资产的序列化对象。</param>
        /// <param name="stepId">新条目的步骤 Id；空字符串表示用户后续手动填写。</param>
        /// <param name="enabledByDefault">新条目的默认启用态。</param>
        public static void AppendStepEntry(
            SerializedObject serializedObject,
            string stepId,
            bool enabledByDefault)
        {
            SerializedProperty steps = serializedObject.FindProperty("Steps");
            if (steps == null)
            {
                return;
            }

            steps.arraySize++;
            serializedObject.ApplyModifiedProperties();

            SerializedProperty entry = steps.GetArrayElementAtIndex(steps.arraySize - 1);
            if (entry == null)
            {
                return;
            }

            SerializedProperty id = entry.FindPropertyRelative("StepId");
            SerializedProperty enabled = entry.FindPropertyRelative("Enabled");
            SerializedProperty configuration =
                entry.FindPropertyRelative("Configuration");
            if (id != null)
            {
                id.stringValue = stepId ?? string.Empty;
            }

            if (enabled != null)
            {
                enabled.boolValue = enabledByDefault;
            }

            if (configuration != null)
            {
                configuration.objectReferenceValue = null;
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 从 Steps 数组中删除所有 StepId 为空的条目；保留已识别步骤与关联配置。
        /// </summary>
        /// <param name="serializedObject">Profile 资产的序列化对象。</param>
        public static void RemoveAllEmptyStepEntries(SerializedObject serializedObject)
        {
            SerializedProperty steps = serializedObject.FindProperty("Steps");
            if (steps == null)
            {
                return;
            }

            for (int i = steps.arraySize - 1; i >= 0; i--)
            {
                string id = steps.GetArrayElementAtIndex(i)
                    ?.FindPropertyRelative("StepId")?.stringValue;
                if (string.IsNullOrWhiteSpace(id))
                {
                    steps.DeleteArrayElementAtIndex(i);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 按 BuildStepAvailability 已知步骤清单一键追加全部条目；同 Id 已存在则保留，
        /// 并为全部已注册可配置步骤恢复或创建配置子资产。默认状态按步骤是否已导入决定：
        /// 已导入默认启用，未导入默认关闭以便后续按需启用。
        /// </summary>
        /// <param name="serializedObject">Profile 资产的序列化对象。</param>
        public static void InitializeAllKnownSteps(SerializedObject serializedObject)
        {
            SerializedProperty steps = serializedObject.FindProperty("Steps");
            if (steps == null)
            {
                return;
            }

            HashSet<string> existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < steps.arraySize; i++)
            {
                string id = steps.GetArrayElementAtIndex(i)
                    ?.FindPropertyRelative("StepId")?.stringValue;
                if (!string.IsNullOrEmpty(id))
                {
                    existing.Add(id);
                }
            }

            IReadOnlyList<(string Id, string FriendlyName)> known =
                BuildStepAvailability.EnumerateKnownSteps();
            int appended = 0;
            for (int i = 0; i < known.Count; i++)
            {
                string id = known[i].Id;
                if (string.IsNullOrEmpty(id) || existing.Contains(id))
                {
                    continue;
                }

                bool enabled = BuildStepAvailability.IsAvailable(id);
                AppendStepEntry(serializedObject, id, enabled);
                appended++;
            }

            int configured = 0;
            if (serializedObject.targetObject is UnityRFrameworkBuildProfile profile)
            {
                for (int i = 0; i < known.Count; i++)
                {
                    string id = known[i].Id;
                    BuildStepSettings entry =
                        BuildStepConfigLocator.FindEntry(profile, id);
                    if (entry == null)
                    {
                        continue;
                    }

                    ScriptableObject previous = entry.Configuration;
                    ScriptableObject current =
                        BuildProfileEditorUtility.CreateStepConfiguration(
                            profile,
                            id);
                    if (current != null && current != previous)
                    {
                        configured++;
                    }
                }

                serializedObject.Update();
            }

            Debug.Log(
                $"构建步骤: 初始化完成，追加 {appended} 个步骤条目，"
                + $"恢复、修复或创建 {configured} 个配置资产。");
        }

        /// <summary>
        /// 从 Steps 数组中移除指定索引处的步骤条目；磁盘上的配置文件不会被删除。
        /// </summary>
        /// <param name="serializedObject">Profile 资产的序列化对象。</param>
        /// <param name="index">要移除的索引。</param>
        public static void RemoveStepEntry(SerializedObject serializedObject, int index)
        {
            SerializedProperty steps = serializedObject.FindProperty("Steps");
            if (steps == null || index < 0 || index >= steps.arraySize)
            {
                return;
            }

            steps.DeleteArrayElementAtIndex(index);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
