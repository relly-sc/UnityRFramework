using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 已注册构建步骤的编辑器描述。配置绘制基于步骤条目的独立配置资产，
    /// 不在核心中维护第三方步骤字段或固定步骤清单。
    /// </summary>
    public sealed class BuildProfileStepInfo
    {
        private readonly IBuildPipelineStep step;

        private BuildProfileStepInfo(IBuildPipelineStep step)
        {
            this.step = step;
        }

        public string StepId => step.Id;

        public string DisplayName => step.DisplayName;

        /// <summary>
        /// 绘制步骤配置引用及配置资产 Inspector。
        /// </summary>
        public void DrawParameters(SerializedObject profileSerializedObject)
        {
            if (profileSerializedObject == null)
            {
                return;
            }

            SerializedProperty steps = profileSerializedObject.FindProperty("Steps");
            SerializedProperty entry = FindEntry(steps, StepId);
            if (entry == null)
            {
                return;
            }

            SerializedProperty configuration =
                entry.FindPropertyRelative("Configuration");
            if (configuration == null)
            {
                return;
            }

            EditorGUILayout.PropertyField(
                configuration,
                new GUIContent("配置资产"));
            profileSerializedObject.ApplyModifiedProperties();

            ScriptableObject asset =
                configuration.objectReferenceValue as ScriptableObject;
            if (asset == null)
            {
                if (step.ConfigurationType != null)
                {
                    EditorGUILayout.HelpBox(
                        $"步骤需要 {step.ConfigurationType.Name} 配置资产。",
                        MessageType.Error);
                    if (GUILayout.Button("创建配置资产"))
                    {
                        UnityRFrameworkBuildProfile profile =
                            profileSerializedObject.targetObject
                                as UnityRFrameworkBuildProfile;
                        BuildProfileEditorUtility.CreateStepConfiguration(
                            profile,
                            StepId);
                        profileSerializedObject.Update();
                    }
                }
                return;
            }

            if (step.ConfigurationType != null
                && !step.ConfigurationType.IsInstanceOfType(asset))
            {
                EditorGUILayout.HelpBox(
                    $"配置类型错误，应为 {step.ConfigurationType.Name}，"
                    + $"实际为 {asset.GetType().Name}。",
                    MessageType.Error);
                return;
            }

            UnityEditor.Editor editor =
                UnityEditor.Editor.CreateEditor(asset);
            if (editor != null)
            {
                try
                {
                    editor.OnInspectorGUI();
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(editor);
                }
            }
        }

        public static bool TryGet(string stepId, out BuildProfileStepInfo info)
        {
            info = null;
            if (string.IsNullOrWhiteSpace(stepId))
            {
                return false;
            }

            IReadOnlyList<IBuildPipelineStep> steps =
                BuildPipelineStepRegistry.GetAll();
            for (int i = 0; i < steps.Count; i++)
            {
                if (!string.Equals(
                        steps[i].Id,
                        stepId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                info = new BuildProfileStepInfo(steps[i]);
                return true;
            }

            return false;
        }

        public static bool HasParameters(string stepId)
        {
            return TryGet(stepId, out BuildProfileStepInfo info)
                && info.step.ConfigurationType != null;
        }

        private static SerializedProperty FindEntry(
            SerializedProperty steps,
            string stepId)
        {
            if (steps == null)
            {
                return null;
            }

            for (int i = 0; i < steps.arraySize; i++)
            {
                SerializedProperty entry = steps.GetArrayElementAtIndex(i);
                string id = entry.FindPropertyRelative("StepId")?.stringValue;
                if (string.Equals(id, stepId, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
