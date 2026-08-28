using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HybridCLR.Editor;
using HybridCLR.Editor.Settings;
using RFramework;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// HybridCLR 热更准备步骤：生成桥接代码、编译热更程序集并暂存到资源目录。
    /// 复用 <see cref="HybridCLRArtifactBuilder"/> 与 <see cref="HybridCLRPlayerBaseline"/>，
    /// 不复制第三方逻辑。仅当 Profile 配置并启用了 hybridclr 步骤条目时参与构建；
    /// 未配置条目时跳过，避免导入 Expansion.HybridCLR 但未配置 Profile 时意外执行。
    /// 完整流程要求最近一次 Player 构建的 AOT 基线存在，以保证热更产物与 Player 匹配；
    /// 首次构建请先在步骤配置中开启 GenerateOnly，或先构建 IL2CPP Player 建立基线。
    /// 本步骤会生成代码并触发脚本编译，属于参数应用之后的主动作步骤。
    /// </summary>
    public sealed class HybridCLRBuildStep : BuildPipelineStepBase, IBuildStepInspector
    {
        /// <summary>错误码：HybridCLR 热更准备。</summary>
        private const string StepCode = "HYBRIDCLR";

        /// <summary>校验分组：构建步骤。</summary>
        private const string StepGroup = "构建步骤";

        /// <summary>获取步骤唯一 Id。</summary>
        public override string Id
        {
            get
            {
                return "hybridclr";
            }
        }

        /// <summary>获取步骤显示名称。</summary>
        public override string DisplayName
        {
            get
            {
                return "HybridCLR 热更准备";
            }
        }

        public override BuildPipelineStage Stage => BuildPipelineStage.PrepareCode;

        public override Type ConfigurationType =>
            typeof(HybridClrBuildConfiguration);

        /// <summary>获取步骤排序值：位于应用参数之后、YooAsset 打包之前。</summary>
        public override int Order
        {
            get
            {
                return 24;
            }
        }

        /// <summary>获取是否触发脚本编译：生成桥接代码与编译热更程序集都会触发编译。</summary>
        public override bool TriggersCompilation
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// 判断步骤是否可用于当前构建上下文：仅当 Profile 配置并启用了
        /// hybridclr 步骤条目时可用。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        /// <returns>条目已启用时返回 true。</returns>
        public override bool CanRun(BuildPipelineContext context)
        {
            if (context == null || context.Profile == null)
            {
                return false;
            }

            return BuildStepConfigLocator.HasEnabledEntry(
                context.Profile,
                Id);
        }

        /// <summary>
        /// 执行前置校验：只读检查步骤配置完整性，不生成任何代码。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        /// <param name="issues">追加问题条目的目标集合。</param>
        public override void Validate(
            BuildPipelineContext context,
            ICollection<BuildValidationIssue> issues)
        {
            if (context == null || context.Profile == null || issues == null)
            {
                return;
            }

            HybridClrBuildConfiguration settings =
                BuildStepConfigLocator.GetConfiguration<HybridClrBuildConfiguration>(
                    context.Profile,
                    Id);
            if (settings == null)
            {
                issues.Add(BuildValidationIssue.Error(
                    StepCode,
                    "HybridCLR 步骤未绑定 HybridClrBuildConfiguration 配置资产。",
                    StepGroup));
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.EntryTypeName))
            {
                issues.Add(BuildValidationIssue.Error(
                    StepCode,
                    "HybridCLR 步骤配置缺少 EntryTypeName（热更新入口类型全名）。",
                    StepGroup));
            }

            if (string.IsNullOrWhiteSpace(settings.OutputAssetRoot)
                || (!settings.OutputAssetRoot.Equals(
                        "Assets",
                        StringComparison.Ordinal)
                    && !settings.OutputAssetRoot.StartsWith(
                        "Assets/",
                        StringComparison.Ordinal)))
            {
                issues.Add(BuildValidationIssue.Error(
                    StepCode,
                    "HybridCLR 步骤配置的 OutputAssetRoot 必须以 Assets/ 开头，"
                    + "当前值：" + settings.OutputAssetRoot + "。",
                    StepGroup));
            }

            // 插件设置为唯一事实源：程序集清单缺失时在此前置校验阶段报错，
            // 引导到 HybridCLR 设置窗口，而不是等执行阶段才失败。
            string[] hotUpdateAssemblies = SettingsUtil
                .HotUpdateAssemblyNamesExcludePreserved
                .ToArray();
            if (hotUpdateAssemblies.Length == 0)
            {
                issues.Add(BuildValidationIssue.Error(
                    StepCode,
                    "HybridCLR 未配置热更新程序集（hotUpdateAssemblyDefinitions），"
                    + "请在 HybridCLR Settings 中配置。",
                    StepGroup));
            }

            string[] patchAotAssemblies =
                HybridCLRSettings.Instance.patchAOTAssemblies;
            if (patchAotAssemblies == null || patchAotAssemblies.Length == 0)
            {
                issues.Add(BuildValidationIssue.Error(
                    StepCode,
                    "HybridCLR 未配置 AOT 补充元数据程序集（patchAOTAssemblies），"
                    + "请在 HybridCLR Settings 中配置。",
                    StepGroup));
            }
        }

        /// <summary>
        /// 执行 HybridCLR 热更准备：仅生成模式生成桥接代码；
        /// 完整模式额外编译热更程序集并暂存产物（含 AOT 基线校验）。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        /// <returns>成功返回生成结果；配置缺失或执行失败返回失败结果。</returns>
        public override BuildStepResult Execute(BuildPipelineContext context)
        {
            HybridClrBuildConfiguration settings =
                BuildStepConfigLocator.GetConfiguration<HybridClrBuildConfiguration>(
                    context.Profile,
                    Id);
            if (settings == null)
            {
                return BuildStepResult.Failed(
                    "HybridCLR 步骤未绑定 HybridClrBuildConfiguration 配置资产。",
                    null);
            }

            if (string.IsNullOrWhiteSpace(settings.EntryTypeName))
            {
                return BuildStepResult.Failed(
                    "HybridCLR 步骤配置缺少 EntryTypeName（热更新入口类型全名）。",
                    null);
            }

            try
            {
                // 校验并启用 HybridCLR 设置；要求已配置热更与 AOT 程序集。
                HybridCLRArtifactBuilder.ConfigureAndValidate();

                if (settings.GenerateOnly)
                {
                    // 仅生成桥接代码：供首次 Player 构建使用，不编译热更程序集。
                    HybridCLRArtifactBuilder.GenerateCurrentTarget();
                    return BuildStepResult.Succeeded(
                        $"HybridCLR 桥接代码已生成（仅生成模式，未编译热更），"
                        + $"目标：{context.Target}。请构建 IL2CPP Player 后关闭 GenerateOnly "
                        + "再执行完整热更准备。");
                }

                string codeVersion = string.IsNullOrWhiteSpace(settings.CodeVersion)
                    ? DateTime.Now.ToString("yyyy-MM-dd-HHmmss")
                    : settings.CodeVersion.Trim();

                // 程序集清单以 HybridCLRSettings 为唯一事实源，不在步骤配置中
                // 维护平行副本；缺失时直接失败并引导到插件设置窗口。
                string[] hotUpdateAssemblies = SettingsUtil
                    .HotUpdateAssemblyNamesExcludePreserved
                    .ToArray();
                if (hotUpdateAssemblies.Length == 0)
                {
                    throw new RFrameworkException(
                        "HybridCLR 未配置热更新程序集（hotUpdateAssemblyDefinitions），"
                        + "请在 HybridCLR Settings 中配置。");
                }

                string[] patchAotAssemblies =
                    HybridCLRSettings.Instance.patchAOTAssemblies;
                if (patchAotAssemblies == null || patchAotAssemblies.Length == 0)
                {
                    throw new RFrameworkException(
                        "HybridCLR 未配置 AOT 补充元数据程序集（patchAOTAssemblies），"
                        + "请在 HybridCLR Settings 中配置。");
                }

                string targetName = HybridCLRArtifactBuilder.CompileAndStage(
                    settings.OutputAssetRoot,
                    settings.EntryTypeName,
                    codeVersion,
                    hotUpdateAssemblies,
                    patchAotAssemblies,
                    settings.IncludePdb);
                return BuildStepResult.Succeeded(
                    $"HybridCLR 热更产物已生成：代码版本 {codeVersion}，"
                    + $"目标 {targetName}，输出 {settings.OutputAssetRoot}。");
            }
            catch (RFrameworkException exception)
            {
                return BuildStepResult.Failed(
                    $"HybridCLR 准备失败：{exception.Message}",
                    exception);
            }
            catch (Exception exception)
            {
                return BuildStepResult.Failed(
                    $"HybridCLR 准备失败：{exception.Message}",
                    exception);
            }
        }

        /// <summary>
        /// 绘制 HybridCLR 步骤编辑器区：Profile 内嵌字段（输出目录、入口类型、版本、PDB、仅生成）
        /// 与 HybridCLR Settings 只读展示区，底部附「打开 HybridCLR 设置」按钮。
        /// </summary>
        /// <param name="profileSO">当前 Profile 的 SerializedObject。</param>
        /// <param name="stepId">步骤唯一 Id。</param>
        public void DrawInspector(SerializedObject profileSO, string stepId)
        {
            EditorGUILayout.Space(4f);
            DrawHybridCLRSettingsReadonly();
            EditorGUILayout.Space(8f);
            if (GUILayout.Button("打开 HybridCLR 设置", GUILayout.Height(24f)))
            {
                MenuProvider.OpenSettings();
            }
        }

        /// <summary>
        /// 绘制 HybridCLR Settings 只读展示区，从插件 Instance 读取真实配置并显示。
        /// </summary>
        private static void DrawHybridCLRSettingsReadonly()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("HybridCLR Settings（只读）", EditorStyles.boldLabel);

                HybridCLRSettings settings = HybridCLRSettings.Instance;
                EditorGUILayout.LabelField("enable", settings.enable.ToString());
                EditorGUILayout.LabelField("热更新程序集数",
                    settings.hotUpdateAssemblyDefinitions?.Length.ToString() ?? "0");
                EditorGUILayout.LabelField("AOT 补充元数据程序集数",
                    settings.patchAOTAssemblies?.Length.ToString() ?? "0");
                EditorGUILayout.LabelField("热更 DLL 输出目录", settings.hotUpdateDllCompileOutputRootDir);
                EditorGUILayout.LabelField("AOT 基线目录", settings.strippedAOTDllOutputRootDir);
                EditorGUILayout.LabelField("link.xml 输出", settings.outputLinkFile);
                EditorGUILayout.LabelField("AOTGenericReferences 输出", settings.outputAOTGenericReferenceFile);
            }
        }

        /// <summary>
        /// 绘制 Assets/ 相对路径字段：左侧标签 + 文本框 + 「选择」按钮。
        /// </summary>
        private static void DrawAssetFolderField(SerializedProperty property, GUIContent label)
        {
            if (property == null) return;
            float labelWidth = EditorGUIUtility.labelWidth;
            float buttonWidth = 48f;
            float buttonSpacing = 4f;
            Rect lineRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            Rect labelRect = new Rect(lineRect.x, lineRect.y, labelWidth, lineRect.height);
            float fieldWidth = lineRect.width - labelWidth - buttonWidth - buttonSpacing;
            Rect fieldRect = new Rect(lineRect.x + labelWidth, lineRect.y, fieldWidth, lineRect.height);
            Rect buttonRect = new Rect(fieldRect.xMax + buttonSpacing, lineRect.y, buttonWidth, lineRect.height);
            EditorGUI.PrefixLabel(labelRect, label);
            string value = property.stringValue;
            string newValue = EditorGUI.TextField(fieldRect, value);
            if (!string.Equals(newValue, value, StringComparison.Ordinal))
                property.stringValue = newValue;
            if (GUI.Button(buttonRect, new GUIContent("选择")))
            {
                string startDir = string.IsNullOrWhiteSpace(value)
                    ? Application.dataPath
                    : Path.Combine(Application.dataPath, value.Replace("Assets/", string.Empty).TrimStart('/'));
                string picked = EditorUtility.OpenFolderPanel(label.text, startDir, string.Empty);
                if (!string.IsNullOrEmpty(picked))
                {
                    if (picked.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase))
                        property.stringValue = "Assets" + picked.Substring(Application.dataPath.Length).Replace('\\', '/');
                    else
                        EditorUtility.DisplayDialog("路径超出 Assets", "请选择工程 Assets 目录内的文件夹。", "确定");
                }
            }
        }
    }
}
