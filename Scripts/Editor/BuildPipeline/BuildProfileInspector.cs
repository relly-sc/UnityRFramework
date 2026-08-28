using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建配置资产的只读 Inspector：仅展示步骤挂载概览。
    /// 平台参数、输出设置、场景列表与步骤配置参数的编辑全部集中在构建工具窗口
    /// （UnityRFramework → 构建工具）；本 Inspector 不做任何编辑交互，
    /// 仅以只读方式展示步骤条目（名称 / Id / 启用状态 / 参数摘要），
    /// 引导用户到构建工具窗口进行实际操作，避免两处编辑入口不一致。
    /// </summary>
    [CustomEditor(typeof(UnityRFrameworkBuildProfile))]
    public sealed class BuildProfileInspector : UnityEditor.Editor
    {
        /// <summary>
        /// 绘制 Profile 资产 Inspector：仅步骤挂载区。
        /// 绘制期间序列化对象可能被 Unity 释放（资产删除、重新导入、切换选择、
        /// 创建配置时切换 Selection 等），统一捕获并降级为提示，不向 Console 抛错。
        /// </summary>
        public override void OnInspectorGUI()
        {
            try
            {
                if (serializedObject == null || serializedObject.targetObject == null)
                {
                    DrawStaleAssetHint();
                    return;
                }

                DrawCoreSections();
            }
            catch (NullReferenceException)
            {
                // 序列化对象在绘制途中被释放（常见于创建配置后切换选择、
                // 测试删除资产、资产重新导入等场景）。停止本次绘制并提示，
                // Unity 会在下一帧重建 Inspector；不能在此访问 serializedObject。
                DrawStaleAssetHint();
            }
        }

        /// <summary>
        /// 绘制 Profile 资产的核心分区内容；仅在序列化对象有效时调用。
        /// 本 Inspector 只绘制步骤挂载区，参数编辑全部在构建工具窗口。
        /// </summary>
        private void DrawCoreSections()
        {
            serializedObject.Update();

            DrawStepsSection();

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 绘制资产已失效提示；不访问 serializedObject，可安全在异常路径调用。
        /// </summary>
        private static void DrawStaleAssetHint()
        {
            EditorGUILayout.HelpBox(
                "目标资产已失效或正被刷新（被删除/重新导入/切换选择），请重新选择。",
                MessageType.Warning);
        }

        /// <summary>
        /// 绘制构建步骤分区（只读）：标题行提示编辑入口，逐行展示步骤名称、Id、启用状态与参数摘要。
        /// 不提供添加 / 初始化 / 清理 / 删除 / 启用开关等任何编辑操作。
        /// </summary>
        private void DrawStepsSection()
        {
            EditorGUILayout.LabelField("构建步骤（只读）", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "步骤编辑请在「构建工具」窗口进行（UnityRFramework → 构建工具）。本处仅展示当前配置。",
                MessageType.Info);

            SerializedProperty steps = serializedObject.FindProperty("Steps");
            if (steps == null)
            {
                EditorGUILayout.HelpBox(
                    "Steps 字段序列化失败，请检查 UnityRFrameworkBuildProfile 定义。",
                    MessageType.Error);
                return;
            }

            if (steps.arraySize == 0)
            {
                EditorGUILayout.HelpBox("未配置步骤。", MessageType.Info);
                return;
            }

            for (int i = 0; i < steps.arraySize; i++)
            {
                DrawStepEntry(steps.GetArrayElementAtIndex(i), i);
            }
        }

        /// <summary>
        /// 只读绘制单个步骤条目行：步骤名称 / Id + 启用状态 + 参数摘要。
        /// 不绘制启用开关与删除按钮，编辑操作统一在构建工具窗口进行。
        /// </summary>
        /// <param name="entry">步骤条目序列化属性。</param>
        /// <param name="index">该条目在 Steps 数组中的索引，仅用于定位，不用于编辑。</param>
        private void DrawStepEntry(SerializedProperty entry, int index)
        {
            SerializedProperty stepIdProperty = entry.FindPropertyRelative("StepId");
            SerializedProperty enabledProperty = entry.FindPropertyRelative("Enabled");
            SerializedProperty configurationProperty =
                entry.FindPropertyRelative("Configuration");

            string stepId = stepIdProperty.stringValue;
            string friendlyName = string.IsNullOrWhiteSpace(stepId)
                ? "（空步骤）"
                : BuildStepAvailability.GetFriendlyName(stepId);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent($"{friendlyName}（{stepId}）", stepId),
                    GUILayout.Width(220f));

                EditorGUILayout.LabelField(
                    enabledProperty.boolValue ? "已启用" : "已禁用",
                    GUILayout.Width(60f));

                if (!string.IsNullOrWhiteSpace(stepId))
                {
                    DrawStepConfigSummary(stepId, configurationProperty);
                }
            }
        }

        /// <summary>
        /// 绘制步骤参数的简要摘要（只读），供快速预览。
        /// 实际参数编辑请在构建工具窗口的步骤区进行。
        /// </summary>
        /// <param name="stepId">步骤唯一 Id。</param>
        private static void DrawStepConfigSummary(
            string stepId,
            SerializedProperty configurationProperty)
        {
            if (!BuildProfileStepInfo.HasParameters(stepId))
            {
                return;
            }

            UnityEngine.Object configuration =
                configurationProperty?.objectReferenceValue;
            string summary = configuration != null
                ? $"{configuration.GetType().Name}: {configuration.name}"
                : "未绑定配置资产";
            EditorGUILayout.LabelField(
                new GUIContent(summary),
                GUILayout.Width(280f));
        }
    }
}
