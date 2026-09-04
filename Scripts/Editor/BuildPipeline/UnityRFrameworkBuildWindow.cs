using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建工具主窗口：Profile 选择、Profile 全参数编辑（平台 / 输出 / 场景）、
    /// 步骤挂载与步骤配置参数编辑、差异预览、校验、任务控制与状态展示。
    /// 本窗口是全部构建参数的编辑主场：Profile 参数在「Profile 参数」分区内联编辑，
    /// 步骤配置参数在步骤区嵌套编辑器编辑，即改即存；
    /// Profile 资产 Inspector 只负责步骤条目的挂载、启停与类型识别。
    /// 窗口只调用构建服务与配置模型，不实现任何具体构建步骤逻辑，
    /// 也不直接同步执行流水线：构建命令经 <see cref="BuildPipelineRunner"/>
    /// 状态机内核异步推进，窗口提供 Profile/Recipe 选择、校验、开始、取消、
    /// 作废与报告入口，并实时展示当前任务状态。Unity 重启后的残留任务不支持
    /// 继续（强杀可能损坏 Unity 增量状态），只提供作废与官方构建修复指引。
    /// </summary>
    public sealed class UnityRFrameworkBuildWindow : EditorWindow
    {
        /// <summary>分区折叠键：Profile 参数。</summary>
        private const string FoldProfileParameters = "ProfileParameters";

        /// <summary>分区折叠键：构建步骤。</summary>
        private const string FoldSteps = "Steps";

        /// <summary>分区折叠键：校验结果。</summary>
        private const string FoldValidation = "Validation";

        /// <summary>分区折叠键：最近构建。</summary>
        private const string FoldLastBuild = "LastBuild";

        /// <summary>分区折叠键：当前任务。</summary>
        private const string FoldCurrentTask = "CurrentTask";

        /// <summary>窗口状态持久化实例。</summary>
        [SerializeField]
        private BuildWindowState windowState = new BuildWindowState();

        /// <summary>
        /// 窗口最近一次启动的任务 Id。随窗口序列化在 Domain Reload 后保留，
        /// 用于脚本重载后重新挂接完成回调（更新最近构建摘要与结果弹窗）。
        /// </summary>
        [SerializeField]
        private string windowStartedTaskId = string.Empty;

        /// <summary>当前选中的 Profile；为 null 表示尚未选择。</summary>
        private UnityRFrameworkBuildProfile selectedProfile;

        /// <summary>当前选中 Profile 的序列化对象，用于窗口内参数编辑；Profile 切换时重建。</summary>
        private SerializedObject profileSerializedObject;

        /// <summary>窗口内全部 Profile 缓存，用于下拉选择。</summary>
        private List<UnityRFrameworkBuildProfile> allProfiles = new List<UnityRFrameworkBuildProfile>();

        /// <summary>校验问题列表缓存（Error 与 Warning）。</summary>
        private List<BuildValidationIssue> validationIssues =
            new List<BuildValidationIssue>();

        /// <summary>资源类步骤 Id；窗口按此集合判断「构建资源」可用性与执行范围。</summary>
        private static readonly string[] ResourceStepIds =
        {
            "config",
            "yooasset"
        };

        /// <summary>分区折叠头样式：保留 Foldout 箭头并使用粗体字体。</summary>
        private static GUIStyle sectionFoldoutStyle;

        /// <summary>是否存在活动构建任务。</summary>
        private static bool IsTaskActive
        {
            get
            {
                return BuildPipelineRunner.HasActiveTask;
            }
        }

        /// <summary>
        /// 打开构建工具窗口。
        /// </summary>
        [MenuItem("UnityRFramework/构建工具")]
        private static void Open()
        {
            UnityRFrameworkBuildWindow window = GetWindow<UnityRFrameworkBuildWindow>("构建工具");
            window.minSize = new Vector2(780f, 560f);
            window.Show();
        }

        /// <summary>
        /// 窗口启用时恢复状态并加载上次选中的 Profile；
        /// 若窗口启动的任务在 Domain Reload 后仍在执行，重新挂接完成回调。
        /// </summary>
        private void OnEnable()
        {
            windowState.Load();
            RefreshProfiles();
            ReloadSelectedProfile();
            if (IsTaskActive
                && BuildPipelineRunner.Active?.CurrentState != null
                && string.Equals(
                    BuildPipelineRunner.Active.CurrentState.TaskId,
                    windowStartedTaskId,
                    StringComparison.Ordinal))
            {
                SubscribeNextTaskCompletion();
            }
        }

        /// <summary>
        /// 窗口关闭前保存选中 Profile 与窗口状态。
        /// </summary>
        private void OnDisable()
        {
            BuildPipelineRunner.TaskCompleted -= OnWindowTaskCompleted;
            completionSubscribed = false;
            windowState.SelectedProfileGuid = BuildProfileEditorUtility.GetGuid(selectedProfile);
            windowState.Save();
        }

        /// <summary>
        /// 编辑器更新：存在活动任务时持续重绘窗口，保证状态实时刷新。
        /// </summary>
        /// <summary>完成回调当前是否已挂接（运行时状态，跨 Reload 复位）。</summary>
        private bool completionSubscribed;

        /// <summary>
        /// 编辑器更新：存在活动任务时持续重绘窗口；若窗口启动的任务
        /// 在 Domain Reload 后由恢复检查重新拉起（此时窗口 OnEnable 已执行过、
        /// 无法在 OnEnable 中看到活动任务），在此补偿挂接完成回调。
        /// </summary>
        private void Update()
        {
            if (!IsTaskActive)
            {
                completionSubscribed = false;
                return;
            }

            if (!completionSubscribed
                && BuildPipelineRunner.Active?.CurrentState != null
                && string.Equals(
                    BuildPipelineRunner.Active.CurrentState.TaskId,
                    windowStartedTaskId,
                    StringComparison.Ordinal))
            {
                SubscribeNextTaskCompletion();
                completionSubscribed = true;
            }

            Repaint();
        }

        /// <summary>
        /// 订阅下一条任务完成通知（仅窗口自己启动或恢复的任务）。
        /// 完成后自动退订并记录摘要、弹窗展示结果；
        /// 测试或恢复检查驱动的其他任务完成不会触发本窗口弹窗。
        /// </summary>
        private void SubscribeNextTaskCompletion()
        {
            BuildPipelineRunner.TaskCompleted -= OnWindowTaskCompleted;
            BuildPipelineRunner.TaskCompleted += OnWindowTaskCompleted;
            completionSubscribed = true;
        }

        /// <summary>
        /// 窗口任务终态回调：仅处理窗口自己启动的任务（TaskId 匹配），
        /// 记录最近构建摘要并弹窗提示结果。
        /// </summary>
        /// <param name="runner">完成的任务运行器。</param>
        private void OnWindowTaskCompleted(BuildPipelineRunner runner)
        {
            BuildPipelineRunner.TaskCompleted -= OnWindowTaskCompleted;
            completionSubscribed = false;
            if (runner == null
                || runner.FinalResult == null
                || runner.CurrentState == null
                || !string.Equals(
                    runner.CurrentState.TaskId,
                    windowStartedTaskId,
                    StringComparison.Ordinal))
            {
                return;
            }

            windowStartedTaskId = string.Empty;
            RecordLastBuild(runner.FinalResult);
            ShowBuildResultDialog(runner.FinalResult);
            Repaint();
        }

        /// <summary>
        /// 绘制窗口主界面。
        /// </summary>
        private void OnGUI()
        {
            // 统一窗口左侧标签列宽度为 200px，所有字段共享同一基准，避免各分区左右错位。
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 200f;
            try
            {
                DrawProfileSection();
                if (selectedProfile == null)
                {
                    EditorGUILayout.Space(6f);
                    EditorGUILayout.HelpBox(
                        "未选择 Profile。请先创建或选择一个构建配置，"
                        + "在选中前不会修改任何项目参数。",
                        MessageType.Info);
                    return;
                }

                // 基础 Profile 参数固定显示；其余设置和命令位于滚动区域。
                DrawCurrentTaskSection();
                DrawProfileBasicSection();

                // 其余内容使用一个滚动容器。让 GUILayout 直接分配剩余高度，
                // 避免 BeginArea 使用布局阶段尚未稳定的矩形而绘制成空白区域。
                windowState.ScrollPosition = EditorGUILayout.BeginScrollView(
                    windowState.ScrollPosition,
                    false,
                    true,
                    GUILayout.ExpandWidth(true),
                    GUILayout.ExpandHeight(true));
                DrawProfileAdvancedSection();
                DrawStepsSection();
                DrawValidationSection();
                DrawLastBuildSection();
                DrawCommandSection();
                EditorGUILayout.EndScrollView();
            }
            finally
            {
                EditorGUIUtility.labelWidth = previousLabelWidth;
            }
        }

        /// <summary>
        /// 在顶部固定区域显示 Profile 基础参数：Description、Enabled、Flavor、Recipe
        /// 及 Recipe 执行范围提示。
        /// </summary>
        private void DrawProfileBasicSection()
        {
            DrawSectionHeader("Profile 参数", FoldProfileParameters, () =>
            {
                if (profileSerializedObject == null)
                {
                    EditorGUILayout.HelpBox(
                        "Profile 序列化对象无效，请重新选择或刷新。",
                        MessageType.Warning);
                    return;
                }

                profileSerializedObject.Update();
                BuildProfileParametersGUI.DrawBasicParameters(profileSerializedObject);
                if (profileSerializedObject.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(selectedProfile);
                    AssetDatabase.SaveAssets();
                    RecomputeDiffs();
                }
            });
        }

        /// <summary>
        /// 刷新 Profile 列表缓存。
        /// </summary>
        private void RefreshProfiles()
        {
            allProfiles = BuildProfileEditorUtility.FindAllProfiles();
        }

        /// <summary>
        /// 按持久化 GUID 重新加载选中 Profile；GUID 失效时回退到列表第一个。
        /// </summary>
        private void ReloadSelectedProfile()
        {
            selectedProfile = BuildProfileEditorUtility.LoadByGuid(windowState.SelectedProfileGuid);
            if (selectedProfile == null && allProfiles.Count > 0)
            {
                selectedProfile = allProfiles[0];
            }

            RebindProfileSerializedObject();
            RecomputeDiffs();
        }

        /// <summary>
        /// 按当前选中 Profile 重建序列化对象；Profile 切换或刷新后调用，
        /// 保证「Profile 参数」分区编辑的是当前资产。
        /// </summary>
        private void RebindProfileSerializedObject()
        {
            profileSerializedObject = selectedProfile == null
                ? null
                : new SerializedObject(selectedProfile);
        }

        /// <summary>
        /// 重算校验结果缓存，供「校验结果」分区展示。
        /// </summary>
        private void RecomputeDiffs()
        {
            validationIssues = new List<BuildValidationIssue>();
            if (selectedProfile == null)
            {
                return;
            }

            validationIssues = new List<BuildValidationIssue>(
                BuildProfileValidator.Validate(selectedProfile).Issues);
        }

        /// <summary>
        /// 应用当前 Profile 参数到编辑器设置并展示脱敏报告。
        /// </summary>
        private void ApplyParameters()
        {
            BuildApplyResult result =
                BuildProfileApplier.Apply(selectedProfile);
            RecomputeDiffs();

            if (result.Succeeded)
            {
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < result.ReportLines.Count; i++)
                {
                    builder.AppendLine("- " + result.ReportLines[i]);
                }

                string report = builder.ToString();
                Debug.Log($"[构建工具] 参数应用成功：\n{report}");
                EditorUtility.DisplayDialog(
                    "应用参数",
                    $"参数应用成功。\n\n{report}",
                    "确定");
                return;
            }

            StringBuilder errorBuilder = new StringBuilder();
            for (int i = 0; i < result.Errors.Count; i++)
            {
                errorBuilder.AppendLine("- " + result.Errors[i]);
            }

            EditorUtility.DisplayDialog(
                "应用参数失败",
                $"参数未应用。\n\n{errorBuilder}",
                "确定");
        }

        /// <summary>
        /// 绘制 Profile 选择区域：下拉、刷新、新建与定位。
        /// </summary>
        private void DrawProfileSection()
        {
            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Profile", EditorStyles.boldLabel, GUILayout.Width(200f));

                int selectedIndex = FindSelectedIndex();
                string[] names = GetProfileNames();
                int newIndex = EditorGUILayout.Popup(selectedIndex, names);
                if (newIndex >= 0 && newIndex != selectedIndex)
                {
                    SelectProfile(newIndex);
                }

                if (GUILayout.Button("刷新", GUILayout.Width(56f)))
                {
                    BuildStepAvailability.InvalidateCache();
                    BuildValidatorRegistry.InvalidateCache();
                    RefreshProfiles();
                    ReloadSelectedProfile();
                }

                if (GUILayout.Button("新建", GUILayout.Width(56f)))
                {
                    CreateNewProfile();
                }

                if (GUILayout.Button("定位", GUILayout.Width(56f)))
                {
                    RevealSelectedProfile();
                }
            }
        }

        /// <summary>
        /// 绘制一行 label + value：左标签固定宽，右值占满剩余并支持选中复制。
        /// 用于摘要、差异等存在长路径/长 ID 的字段，避免 IMGUI 双参 LabelField 截断。
        /// </summary>
        /// <param name="label">字段名（左）。</param>
        /// <param name="value">字段值（右）。</param>
        private static void DrawLabelValue(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    label,
                    GUILayout.Width(100f));
                EditorGUILayout.SelectableLabel(
                    value ?? string.Empty,
                    EditorStyles.label,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight),
                    GUILayout.ExpandWidth(true));
            }
        }

        /// <summary>
        /// 绘制滚动区域中的 Profile 其余设置：平台、输出、场景和宏定义。
        /// </summary>
        private void DrawProfileAdvancedSection()
        {
            DrawSectionHeader("平台、输出与场景设置", FoldProfileParameters + ".Advanced", () =>
            {
                if (profileSerializedObject == null)
                {
                    EditorGUILayout.HelpBox(
                        "Profile 序列化对象无效，请重新选择或刷新。",
                        MessageType.Warning);
                    return;
                }

                profileSerializedObject.Update();
                BuildProfileParametersGUI.DrawAdvancedParameters(
                    profileSerializedObject,
                    ResolveOutputPreview());
                if (profileSerializedObject.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(selectedProfile);
                    AssetDatabase.SaveAssets();
                    RecomputeDiffs();
                }
            });
        }

        /// <summary>
        /// 绘制构建步骤分区：标题行提供 [初始化全部已注册] 挂载入口，
        /// 条目行显示启用态、可用性、创建配置与删除；已挂配置的步骤行下方
        /// 展开配置参数编辑区。本窗口是步骤挂载与参数编辑的主场所。
        /// </summary>
        private void DrawStepsSection()
        {
            DrawSectionHeader("构建步骤", FoldSteps, () =>
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(
                            new GUIContent(
                                "初始化全部已注册",
                                "按当前已注册的步骤清单一键追加全部条目（同 Id 已存在则跳过），未导入的第三方步骤默认关闭。"),
                            GUILayout.Width(150f)))
                    {
                        BuildStepEntriesEditor.InitializeAllKnownSteps(profileSerializedObject);
                        MarkProfileDirtyAndRecompute();
                    }
                }

                if (selectedProfile.Steps.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        "未配置步骤。可点上方【初始化全部已注册】一键追加所有已注册步骤。",
                        MessageType.Info);
                    return;
                }

                for (int i = 0; i < selectedProfile.Steps.Count; i++)
                {
                    BuildStepSettings step = selectedProfile.Steps[i];
                    if (step == null)
                    {
                        continue;
                    }

                    DrawStepEntry(step, i);
                }
            });
        }

        /// <summary>
        /// 步骤挂载操作（添加 / 初始化 / 删除）后标记 Profile 脏并保存，
        /// 同时重算校验结果，保证「校验结果」分区即时刷新。
        /// </summary>
        private void MarkProfileDirtyAndRecompute()
        {
            if (selectedProfile != null)
            {
                EditorUtility.SetDirty(selectedProfile);
                AssetDatabase.SaveAssets();
            }

            RecomputeDiffs();
        }

        /// <summary>
        /// 绘制校验结果分区：错误、警告与通过提示。
        /// </summary>
        private void DrawValidationSection()
        {
            DrawSectionHeader("校验结果", FoldValidation, () =>
            {
                if (validationIssues.Count == 0)
                {
                    EditorGUILayout.HelpBox("Profile 配置通过校验。", MessageType.Info);
                    return;
                }

                string currentGroup = null;
                foreach (BuildValidationIssue issue in validationIssues)
                {
                    if (!string.Equals(issue.Group, currentGroup, StringComparison.Ordinal))
                    {
                        currentGroup = issue.Group;
                        EditorGUILayout.Space(4f);
                        EditorGUILayout.LabelField(currentGroup, EditorStyles.boldLabel);
                    }

                    MessageType type = issue.Level == BuildValidationLevel.Error
                        ? MessageType.Error
                        : MessageType.Warning;
                    EditorGUILayout.HelpBox(
                        $"[{issue.Code}] {issue.Message}",
                        type);
                }
            });
        }

        /// <summary>
        /// 绘制最近构建分区：上次构建的状态、耗时与产物路径。
        /// </summary>
        private void DrawLastBuildSection()
        {
            DrawSectionHeader("最近构建", FoldLastBuild, () =>
            {
                BuildWindowLastBuild last = windowState.LastBuild;
                if (last == null || !last.HasRecord)
                {
                    EditorGUILayout.LabelField("（暂无构建记录）");
                    return;
                }

                DrawLabelValue("状态", last.Status);
                DrawLabelValue("Profile", last.ProfileName);
                DrawLabelValue("平台", last.Platform);
                DrawLabelValue("类型", last.Recipe);
                if (!string.IsNullOrEmpty(last.PlayerVersion))
                {
                    DrawLabelValue("Player 版本", last.PlayerVersion);
                }
                if (!string.IsNullOrEmpty(last.Summary))
                {
                    EditorGUILayout.HelpBox(last.Summary, MessageType.None);
                }
                DrawLabelValue("耗时", $"{last.DurationSeconds:F1} 秒");
                DrawLabelValue(
                    string.IsNullOrEmpty(last.OutputLabel) ? "产物" : last.OutputLabel,
                    last.OutputPath);
                DrawLabelValue("时间", last.TimeText);
            });
        }

        /// <summary>
        /// 绘制当前任务分区：活动任务实时状态，或残留任务的恢复/重试/作废入口。
        /// </summary>
        private void DrawCurrentTaskSection()
        {
            DrawSectionHeader("当前任务", FoldCurrentTask, () =>
            {
                if (IsTaskActive)
                {
                    BuildPipelineRunner active = BuildPipelineRunner.Active;
                    BuildPipelineState state = active != null
                        ? active.CurrentState
                        : null;
                    if (state == null)
                    {
                        EditorGUILayout.HelpBox("构建任务正在初始化…", MessageType.Info);
                        return;
                    }

                    EditorGUILayout.HelpBox(
                        $"任务 {state.TaskId} 正在执行。\n"
                        + $"状态：{GetPhaseText(state.Phase)}\n"
                        + $"进度：{state.CompletedSteps.Count} / {state.StepIds.Count} 个步骤"
                        + (state.StepIds.Count > state.CompletedSteps.Count
                            ? $"（下一步：{state.StepIds[state.CompletedSteps.Count]}）"
                            : string.Empty)
                        + (string.IsNullOrEmpty(state.WaitingReason)
                            ? string.Empty
                            : $"\n等待：{state.WaitingReason}")
                        + (string.IsNullOrEmpty(state.ErrorMessage)
                            ? string.Empty
                            : $"\n错误：{state.ErrorMessage}"),
                        MessageType.Warning);
                    return;
                }

                BuildPipelineState leftover = LoadLeftoverState();
                if (leftover == null)
                {
                    EditorGUILayout.LabelField("（无活动或残留任务）");
                    return;
                }

                bool playerInterrupted =
                    BuildPipelineRecovery.IsPlayerBuildInterrupted(leftover);
                EditorGUILayout.HelpBox(
                    $"发现未完成任务 {leftover.TaskId}（{leftover.ProfileName}）。\n"
                    + $"状态：{GetPhaseText(leftover.Phase)}\n"
                    + $"进度：{leftover.CompletedSteps.Count} / {leftover.StepIds.Count} 个步骤"
                    + (string.IsNullOrEmpty(leftover.ErrorMessage)
                        ? string.Empty
                        : $"\n错误：{leftover.ErrorMessage}")
                    + "\n\nUnity 重启后不支持继续构建（强杀可能损坏 Unity 增量状态，"
                    + "继续构建会跳过原生编译并报假成功）。请作废本任务后重新发起构建"
                    + (playerInterrupted
                        ? "；本次中断发生在「构建 Player」阶段，作废后请先用官方 "
                            + "Build Settings 完整构建一次（成功产出 exe）修复状态。"
                        : "。"),
                    MessageType.Warning);

                if (GUILayout.Button(
                        new GUIContent(
                            "作废任务",
                            "清理任务状态与锁；作废后可用本工具重新发起构建。"),
                        GUILayout.Height(26f)))
                {
                    AbandonLeftoverTask(leftover);
                }
            });
        }

        /// <summary>
        /// 加载残留任务状态；损坏状态返回空并保持现场等待恢复检查处理。
        /// </summary>
        /// <returns>非终态残留状态；无可恢复任务时为空。</returns>
        private static BuildPipelineState LoadLeftoverState()
        {
            BuildPipelinePersistence persistence = new BuildPipelinePersistence();
            try
            {
                BuildPipelineStateLoadResult result =
                    persistence.LoadStateDetailed(out BuildPipelineState state);
                if ((result == BuildPipelineStateLoadResult.Success
                        || result == BuildPipelineStateLoadResult.LoadedFromBackup)
                    && state != null
                    && !state.IsTerminal)
                {
                    return state;
                }
            }
            catch
            {
                // 读取失败按无残留处理，具体原因由恢复检查与控制台输出。
            }

            return null;
        }

        /// <summary>
        /// 加载残留任务状态；损坏状态返回空并保持现场等待恢复检查处理。
        /// </summary>
        /// <param name="leftover">残留任务状态。</param>
        private void AbandonLeftoverTask(BuildPipelineState leftover)
        {
            if (!EditorUtility.DisplayDialog(
                    "作废构建任务",
                    $"确认作废任务 {leftover.TaskId}（{leftover.ProfileName}）？"
                    + "作废后将清理状态与锁，已完成步骤的产物保留。",
                    "作废",
                    "取消"))
            {
                return;
            }

            BuildPipelineRecovery.AbandonTask(
                leftover,
                new BuildPipelinePersistence());
        }

        /// <summary>
        /// 获取任务状态的中文显示名。
        /// </summary>
        /// <param name="phase">任务状态。</param>
        /// <returns>中文显示名。</returns>
        private static string GetPhaseText(BuildPipelinePhase phase)
        {
            switch (phase)
            {
                case BuildPipelinePhase.Running:
                    return "运行中";
                case BuildPipelinePhase.WaitingForEditor:
                    return "等待编辑器";
                case BuildPipelinePhase.Succeeded:
                    return "成功";
                case BuildPipelinePhase.Failed:
                    return "失败";
                case BuildPipelinePhase.Cancelled:
                    return "已取消";
                case BuildPipelinePhase.ManualIntervention:
                    return "人工处理";
                default:
                    return "已作废";
            }
        }

        /// <summary>
        /// 绘制命令区：校验、应用参数与构建命令；构建命令经
        /// <see cref="BuildPipelineRunner"/> 内核异步执行。任务活动期间锁定
        /// 构建命令，仅「取消任务」保持可用。
        /// </summary>
        private void DrawCommandSection()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("命令", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(IsTaskActive))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("重新校验", GUILayout.Height(30f)))
                    {
                        RecomputeDiffs();
                    }

                    if (GUILayout.Button("应用参数", GUILayout.Height(30f)))
                    {
                        ApplyParameters();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!CanBuildAssets()))
                    {
                        if (GUILayout.Button(
                                new GUIContent("构建资源", GetBuildAssetsTooltip()),
                                GUILayout.Height(30f)))
                        {
                            StartBuildWithRecipe(BuildRecipe.Assets);
                        }
                    }

                    if (GUILayout.Button(
                            new GUIContent(
                                "构建 Player",
                                "执行校验 → 切换目标 → 应用参数 → Player 构建，不构建资源。"),
                            GUILayout.Height(30f)))
                    {
                        StartBuildWithRecipe(BuildRecipe.Player);
                    }

                    if (GUILayout.Button(
                            new GUIContent(
                                "按 Recipe 构建",
                                "执行 Profile 当前选择的 Recipe。"),
                            GUILayout.Height(30f)))
                    {
                        StartBuildWithRecipe(selectedProfile.Recipe);
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("打开输出目录", GUILayout.Height(30f)))
                {
                    OpenOutputDirectory();
                }

                using (new EditorGUI.DisabledScope(!IsTaskActive))
                {
                    if (GUILayout.Button(
                            new GUIContent(
                                "取消任务",
                                "停止尚未开始的后续步骤，已完成步骤保留；取消任务保留状态与锁。"),
                            GUILayout.Height(30f)))
                    {
                        BuildPipelineRunner.Active?.Cancel();
                    }
                }
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "构建资源执行 Assets Recipe；构建 Player 执行 Player Recipe；"
                + "按 Recipe 构建执行 Profile 当前选择的 Recipe。"
                + "所有入口均由统一规划器校验阶段、依赖和步骤配置。"
                + "构建命令使用临时设置事务：任务结束（成功、失败、取消）后自动恢复项目设置，"
                + "活动构建平台按契约保留。构建中可点击「取消任务」。",
                MessageType.Info);
            EditorGUILayout.HelpBox(
                "请勿在构建期间强制关闭 Unity——尤其在「构建 Player」阶段被强杀后，"
                + "Unity 增量状态可能损坏，后续构建会跳过原生编译并报告假成功（无 exe 产出）；"
                + "此时需先用官方 Build Settings 完整构建一次修复状态，再用本工具构建。",
                MessageType.Warning);
        }

        /// <summary>
        /// 绘制单个步骤条目：启用态、友好名称、可用性说明、配置参数与删除按钮。
        /// 启用开关变更时写回 Profile 并标记脏，保证窗口内修改持久化。
        /// 可配置步骤在行下方显示其独立配置资产及参数。
        /// </summary>
        /// <param name="step">步骤配置条目。</param>
        /// <param name="index">该条目在 Steps 数组中的索引，供删除按钮使用。</param>
        private void DrawStepEntry(BuildStepSettings step, int index)
        {
            string friendlyName = BuildStepAvailability.GetFriendlyName(step.StepId);
            bool available = BuildStepAvailability.IsAvailable(step.StepId);

            using (new EditorGUILayout.HorizontalScope())
            {
                bool newEnabled = EditorGUILayout.ToggleLeft(
                    new GUIContent($"{friendlyName}（{step.StepId}）", step.StepId),
                    step.Enabled,
                    GUILayout.Width(220f));
                if (newEnabled != step.Enabled)
                {
                    step.Enabled = newEnabled;
                    EditorUtility.SetDirty(selectedProfile);
                    AssetDatabase.SaveAssets();
                }

                if (available)
                {
                    EditorGUILayout.LabelField(
                        "可用",
                        GUILayout.Width(48f));
                }
                else
                {
                    string reason = BuildStepAvailability.GetUnavailableReason(step.StepId);
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.LabelField(
                            new GUIContent(reason, reason),
                            GUILayout.Width(160f));
                    }
                }

                if (GUILayout.Button(
                        new GUIContent(
                            "删除",
                            "从 Profile 中移除此步骤条目；磁盘上的配置文件不会被删除。"),
                        GUILayout.Width(56f)))
                {
                    BuildStepEntriesEditor.RemoveStepEntry(profileSerializedObject, index);
                    MarkProfileDirtyAndRecompute();
                    return;
                }
            }

            // 步骤配置资产在行下方单独绘制，避免字段横向溢出与重叠。
            DrawStepConfigInline(step);
        }

        /// <summary>
        /// 绘制步骤配置资产；无配置类型的步骤不显示参数区。
        /// </summary>
        private void DrawStepConfigInline(BuildStepSettings step)
        {
            if (!BuildProfileStepInfo.TryGet(step.StepId, out BuildProfileStepInfo info))
            {
                return;
            }

            info.DrawParameters(profileSerializedObject);
            EditorGUILayout.Space(4f);
        }


        /// <summary>
        /// 绘制折叠分区头与内容；点击标题切换折叠状态。
        /// </summary>
        /// <param name="title">分区标题。</param>
        /// <param name="foldKey">折叠状态键。</param>
        /// <param name="drawContent">展开时绘制内容的回调。</param>
        private void DrawSectionHeader(string title, string foldKey, Action drawContent)
        {
            if (sectionFoldoutStyle == null)
            {
                sectionFoldoutStyle = new GUIStyle(EditorStyles.foldout);
                sectionFoldoutStyle.fontStyle = FontStyle.Bold;
            }

            EditorGUILayout.Space(6f);
            bool expanded = EditorGUILayout.Foldout(
                !windowState.IsSectionFolded(foldKey),
                title,
                true,
                sectionFoldoutStyle);
            windowState.SetSectionFolded(foldKey, !expanded);
            if (expanded)
            {
                // 不再为分区内容整体缩进：用户要求字段贴左对齐，层级由粗体 Foldout 标题区分即可。
                drawContent();
            }
        }

        /// <summary>
        /// 判断 Profile 是否启用至少一个资源类步骤，用于「构建资源」按钮可用性。
        /// </summary>
        /// <returns>存在已启用的资源类步骤条目时返回 true。</returns>
        private bool CanBuildAssets()
        {
            for (int i = 0; i < ResourceStepIds.Length; i++)
            {
                if (BuildStepConfigLocator.HasEnabledEntry(
                        selectedProfile,
                        ResourceStepIds[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取「构建资源」按钮悬停说明。
        /// </summary>
        /// <returns>可用时说明执行范围；不可用时说明原因。</returns>
        private string GetBuildAssetsTooltip()
        {
            if (CanBuildAssets())
            {
                return "按 Profile 启用条目执行 Assets Recipe（Config / YooAsset）。";
            }
            return "Profile 未启用任何 Assets Recipe 步骤（Config / YooAsset），无法构建资源。";
        }

        /// <summary>
        /// 以指定 Recipe 启动构建任务：前置校验 → 活动任务守卫 → 内核启动。
        /// Recipe 覆盖只作用于本次任务，不修改 Profile 资产；
        /// 任务由状态机内核异步推进，本方法立即返回。
        /// </summary>
        /// <param name="recipe">本次执行使用的 Recipe。</param>
        private void StartBuildWithRecipe(BuildRecipe recipe)
        {
            BuildValidationResult validation =
                BuildProfileValidator.Validate(selectedProfile, recipe);
            if (!validation.CanBuild)
            {
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < validation.Issues.Count; i++)
                {
                    BuildValidationIssue issue = validation.Issues[i];
                    if (issue.Level == BuildValidationLevel.Error)
                    {
                        builder.AppendLine($"- [{issue.Code}] {issue.Message}");
                    }
                }

                EditorUtility.DisplayDialog(
                    "校验未通过",
                    $"构建未启动。\n\n{builder}",
                    "确定");
                return;
            }

            if (IsTaskActive)
            {
                EditorUtility.DisplayDialog(
                    "已有构建任务",
                    "当前已有构建任务在运行，请先等待其完成。",
                    "确定");
                return;
            }

            BuildPipelineState leftover = LoadLeftoverState();
            if (leftover != null)
            {
                EditorUtility.DisplayDialog(
                    "存在未完成任务",
                    $"存在未完成的构建任务（{leftover.ProfileName}，{leftover.TaskId}，"
                    + $"状态 {GetPhaseText(leftover.Phase)}）。\n"
                    + "请先在「当前任务」分区作废该任务（若上次构建在 Player 阶段被中断，"
                    + "还需先用官方 Build Settings 完整构建一次修复状态）后再启动新任务。",
                    "确定");
                return;
            }

            try
            {
                BuildPipelineRunner runner = BuildPipelineRunner.StartNew(
                    selectedProfile,
                    null,
                    null,
                    recipe);
                windowStartedTaskId = runner.CurrentState.TaskId;
                SubscribeNextTaskCompletion();
                Repaint();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[构建工具] 构建启动失败：{exception}");
                EditorUtility.DisplayDialog(
                    "构建启动失败",
                    $"无法启动构建任务：\n{exception.Message}",
                    "确定");
            }
        }

        /// <summary>
        /// 将终态结果写入窗口「最近构建」分区并持久化。
        /// 版本号取任务创建时冻结的值，与实际产物路径保持一致。
        /// </summary>
        /// <param name="result">流水线终态结果。</param>
        private void RecordLastBuild(BuildRunResult result)
        {
            windowState.LastBuild = BuildWindowLastBuild.Create(result.FinalState);
            windowState.Save();
        }

        /// <summary>
        /// 弹出构建结果对话框；报告过长时截断提示，完整报告见 Console。
        /// 失败与取消任务保留状态，提示可恢复或作废。
        /// </summary>
        /// <param name="result">流水线终态结果。</param>
        private static void ShowBuildResultDialog(BuildRunResult result)
        {
            const int maxLength = 1500;
            string report = result.ReportText;
            if (report.Length > maxLength)
            {
                report = report.Substring(0, maxLength)
                    + "\n…（完整报告见 Console）";
            }

            string title;
            string status;
            bool isSuccess = result.Succeeded;
            if (result.FinalState.Phase == BuildPipelinePhase.ManualIntervention)
            {
                title = "构建任务需要人工处理";
                status = "需要人工处理";
            }
            else if (result.Succeeded)
            {
                title = "构建成功";
                status = "构建成功";
            }
            else if (result.Cancelled)
            {
                title = "构建已取消";
                status = "构建已取消";
            }
            else
            {
                title = "构建失败";
                status = "构建失败";
                report += "\n\n任务状态已保留，可在「当前任务」分区重试或作废。";
            }

            BuildResultDialog.Show(title, status, report, isSuccess);
        }

        /// <summary>
        /// 构建结果专用窗口：将终态作为醒目的首要信息展示，详情作为次要信息展示。
        /// </summary>
        private sealed class BuildResultDialog : EditorWindow
        {
            private string status;
            private string report;
            private bool isSuccess;

            public static void Show(
                string title,
                string status,
                string report,
                bool isSuccess)
            {
                BuildResultDialog window =
                    CreateInstance<BuildResultDialog>();
                window.titleContent = new GUIContent(title);
                window.status = status;
                window.report = report;
                window.isSuccess = isSuccess;
                window.minSize = new Vector2(520f, 330f);
                window.maxSize = new Vector2(900f, 700f);
                window.ShowUtility();
            }

            private void OnGUI()
            {
                Color oldColor = GUI.color;
                GUI.color = isSuccess
                    ? new Color(0.12f, 0.55f, 0.2f)
                    : new Color(0.8f, 0.12f, 0.08f);
                GUIStyle statusStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 22,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(8, 8, 10, 10)
                };
                GUILayout.Label(status, statusStyle, GUILayout.Height(52f));
                GUI.color = oldColor;

                EditorGUILayout.LabelField("构建结果详情", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(report ?? string.Empty, MessageType.None);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("确定", GUILayout.Width(90f), GUILayout.Height(26f)))
                {
                    Close();
                }
                GUILayout.EndHorizontal();
            }
        }

        /// <summary>
        /// 由任务状态拼接产物输出目录的绝对路径。
        /// </summary>
        /// <param name="state">任务状态。</param>
        /// <returns>输出目录绝对路径；根目录为空时返回空字符串。</returns>
        private static string ResolveOutputDirectory(BuildPipelineState state)
        {
            string root = state.OutputRootAbsolute;
            if (string.IsNullOrEmpty(root))
            {
                return string.Empty;
            }

            return string.IsNullOrEmpty(state.OutputDirectory)
                ? root
                : Path.Combine(root, state.OutputDirectory);
        }

        /// <summary>
        /// 选中指定索引的 Profile 并刷新缓存。
        /// </summary>
        /// <param name="index">Profile 列表索引。</param>
        private void SelectProfile(int index)
        {
            if (index < 0 || index >= allProfiles.Count)
            {
                return;
            }

            selectedProfile = allProfiles[index];
            windowState.SelectedProfileGuid = BuildProfileEditorUtility.GetGuid(selectedProfile);
            RebindProfileSerializedObject();
            RecomputeDiffs();
        }

        /// <summary>
        /// 查找当前选中 Profile 在列表中的索引。
        /// </summary>
        /// <returns>命中时返回索引；未命中返回 -1。</returns>
        private int FindSelectedIndex()
        {
            if (selectedProfile == null)
            {
                return -1;
            }

            for (int i = 0; i < allProfiles.Count; i++)
            {
                if (allProfiles[i] == selectedProfile)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 获取全部 Profile 名称数组，供下拉选择。
        /// </summary>
        /// <returns>名称数组；无 Profile 时返回占位名称。</returns>
        private string[] GetProfileNames()
        {
            if (allProfiles.Count == 0)
            {
                return new[] { "（无 Profile，请新建）" };
            }

            string[] names = new string[allProfiles.Count];
            for (int i = 0; i < allProfiles.Count; i++)
            {
                names[i] = allProfiles[i].name;
            }

            return names;
        }

        /// <summary>
        /// 弹出新建 Profile 对话框并创建资产。
        /// </summary>
        private void CreateNewProfile()
        {
            CreateProfileDialog.Show(name =>
            {
                UnityRFrameworkBuildProfile profile = BuildProfileEditorUtility.CreateProfile(name);
                if (profile == null)
                {
                    return;
                }

                RefreshProfiles();
                selectedProfile = profile;
                windowState.SelectedProfileGuid = BuildProfileEditorUtility.GetGuid(profile);
                RebindProfileSerializedObject();
                RecomputeDiffs();
            });
        }

        /// <summary>
        /// 在资源面板定位当前选中的 Profile。
        /// </summary>
        private void RevealSelectedProfile()
        {
            if (selectedProfile == null)
            {
                return;
            }

            Selection.activeObject = selectedProfile;
            EditorGUIUtility.PingObject(selectedProfile);
        }

        /// <summary>
        /// 打开输出根目录；仅允许通过边界校验的目录，目录不存在时先创建。
        /// </summary>
        private void OpenOutputDirectory()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            try
            {
                string root = selectedProfile.Output.ResolveRootAbsolute(projectRoot);
                if (!Directory.Exists(root))
                {
                    Directory.CreateDirectory(root);
                }

                EditorUtility.RevealInFinder(root);
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("无法打开输出目录", exception.Message, "确定");
            }
        }

        /// <summary>
        /// 统计 Profile 中有效且启用的场景数。
        /// </summary>
        /// <returns>启用场景数量。</returns>
        private int GetEnabledSceneCount()
        {
            int count = 0;
            foreach (BuildSceneEntry entry in selectedProfile.Scenes)
            {
                if (entry != null && entry.IsValidEnabled())
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 解析输出路径预览文本；解析失败时返回错误说明。
        /// </summary>
        /// <returns>输出路径预览。</returns>
        private string ResolveOutputPreview()
        {
            try
            {
                BuildOutputToken token = new BuildOutputToken(
                    selectedProfile.name,
                    selectedProfile.Platform.ProductName,
                    selectedProfile.Platform.Target.ToString(),
                    selectedProfile.Platform.PublicVersion,
                    selectedProfile.Platform.BuildNumber,
                    selectedProfile.Platform.ScriptingBackend.ToString(),
                    DateTime.Now);

                string directory = selectedProfile.Output.ResolveDirectory(token);
                string fileName = selectedProfile.Output.ResolveFileName(token);
                string outputDirectory = Path.Combine(
                    selectedProfile.Output.OutputRoot,
                    directory);
                if (BuildPlayerOptionsFactory.UsesDirectoryOutput(
                        selectedProfile.Platform.Target))
                {
                    return outputDirectory;
                }

                string platformFileName =
                    BuildPlayerOptionsFactory.ResolvePlatformFileName(
                        selectedProfile,
                        selectedProfile.Platform.Target,
                        fileName);
                return Path.Combine(outputDirectory, platformFileName);
            }
            catch (Exception exception)
            {
                return $"（解析失败：{exception.Message}）";
            }
        }

        /// <summary>
        /// 获取构建分档的中文显示名。
        /// </summary>
        /// <param name="flavor">构建分档。</param>
        /// <returns>中文显示名。</returns>
        private static string GetFlavorText(BuildProfileFlavor flavor)
        {
            switch (flavor)
            {
                case BuildProfileFlavor.Qa:
                    return "测试（Qa）";
                case BuildProfileFlavor.Development:
                    return "开发（Development）";
                default:
                    return "正式（Release）";
            }
        }

        /// <summary>
        /// 新建构建配置的模态对话框：输入资产名称后创建。
        /// </summary>
        private sealed class CreateProfileDialog : EditorWindow
        {
            /// <summary>资产名称输入框内容。</summary>
            private string profileName = "BuildProfile";

            /// <summary>点击创建后的确认回调，参数为资产名称。</summary>
            private Action<string> onConfirm;

            /// <summary>
            /// 显示新建对话框。
            /// </summary>
            /// <param name="confirm">确认回调，参数为资产名称。</param>
            public static void Show(Action<string> confirm)
            {
                CreateProfileDialog dialog = GetWindow<CreateProfileDialog>(true, "新建构建配置");
                dialog.minSize = new Vector2(380f, 150f);
                dialog.onConfirm = confirm;
                dialog.Show();
            }

            /// <summary>
            /// 绘制对话框界面。
            /// </summary>
            private void OnGUI()
            {
                EditorGUILayout.Space(10f);
                profileName = EditorGUILayout.TextField("资产名称", profileName);

                EditorGUILayout.Space(6f);
                EditorGUILayout.HelpBox(
                    "将在 BuildProfiles 目录创建 ScriptableObject 资产，"
                    + "创建后可在 Inspector 中编辑全部构建参数。",
                    MessageType.Info);

                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("创建"))
                    {
                        string name = profileName.Trim();
                        if (string.IsNullOrEmpty(name))
                        {
                            EditorUtility.DisplayDialog("名称无效", "资产名称不能为空。", "确定");
                            return;
                        }

                        onConfirm?.Invoke(name);
                        Close();
                    }

                    if (GUILayout.Button("取消"))
                    {
                        Close();
                    }
                }
            }
        }
    }
}
