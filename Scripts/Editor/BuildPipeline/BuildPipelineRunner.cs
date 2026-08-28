using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 单次流水线执行结果。
    /// </summary>
    public sealed class BuildRunResult
    {
        /// <summary>是否成功完成全部步骤。</summary>
        public bool Succeeded { get; }

        /// <summary>是否被取消（可能部分步骤已完成）。</summary>
        public bool Cancelled { get; }

        /// <summary>步骤执行报告文本。</summary>
        public string ReportText { get; }

        /// <summary>任务最终状态（含失败步骤与异常）。</summary>
        public BuildPipelineState FinalState { get; }

        /// <summary>
        /// 创建执行结果。
        /// </summary>
        /// <param name="succeeded">是否成功。</param>
        /// <param name="cancelled">是否取消。</param>
        /// <param name="reportText">报告文本。</param>
        /// <param name="finalState">最终状态。</param>
        public BuildRunResult(
            bool succeeded,
            bool cancelled,
            string reportText,
            BuildPipelineState finalState)
        {
            Succeeded = succeeded;
            Cancelled = cancelled;
            ReportText = reportText ?? string.Empty;
            FinalState = finalState;
        }
    }

    /// <summary>
    /// 可恢复构建流水线运行器：负责步骤排序、执行、检查点落盘、取消与项目级锁。
    /// 通过 SessionState 标记区分 Domain Reload（同进程可自动恢复）与进程重启
    /// （需人工确认或作废）；恢复决策由 <see cref="BuildPipelineRecovery"/> 负责。
    /// 执行语义：
    /// - 每个步骤开始前与完成后均落盘检查点；
    /// - 失败立即停止，记录失败步骤与完整异常；
    /// - 取消只停止尚未开始的后续步骤，已完成步骤保留；
    /// - 完成后释放锁、清理状态与进度条相关标记。
    /// </summary>
    public sealed class BuildPipelineRunner
    {
        /// <summary>当前进程是否存在活动构建任务。</summary>
        public static bool HasActiveTask { get; private set; }

        /// <summary>构建上下文。</summary>
        public BuildPipelineContext Context { get; }

        /// <summary>当前任务状态（执行过程中持续更新）。</summary>
        public BuildPipelineState CurrentState { get; }

        private readonly BuildPipelinePersistence persistence;
        private readonly CancellationTokenSource cts;

        /// <summary>
        /// 启动新构建任务。
        /// </summary>
        /// <param name="profile">待执行的构建配置，不能为空。</param>
        /// <param name="persistenceRoot">持久化根目录；为空时使用工程默认目录（测试注入临时目录）。</param>
        /// <param name="steps">显式步骤列表；为空时从注册表自动发现。</param>
        /// <returns>已加锁并落盘初始检查点的运行器。</returns>
        public static BuildPipelineRunner StartNew(
            UnityRFrameworkBuildProfile profile,
            string persistenceRoot = null,
            IReadOnlyList<IBuildPipelineStep> steps = null)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence(persistenceRoot);
            List<IBuildPipelineStep> available = PrepareSteps(steps);
            BuildRecipePlan recipePlan =
                BuildRecipePlanner.Create(
                    profile,
                    available,
                    requireCoreSteps: steps == null);
            if (!recipePlan.IsValid)
            {
                throw new InvalidOperationException(
                    FormatRecipeErrors(recipePlan.Issues));
            }

            List<IBuildPipelineStep> ordered =
                new List<IBuildPipelineStep>(recipePlan.Steps);
            CancellationTokenSource source = new CancellationTokenSource();
            BuildPipelineContext context = BuildPipelineContext.Create(
                profile,
                ToStepDictionary(ordered),
                source.Token);
            List<IBuildPipelineStep> usable = FilterUsable(ordered, context);
            BuildPipelineState state = CreateState(profile, context, usable);

            string lockError;
            if (!persistence.TryAcquireLock(state, out lockError))
            {
                source.Dispose();
                throw new InvalidOperationException(
                    $"无法启动构建任务：{lockError}");
            }

            persistence.SaveState(state);
            MarkActive();
            return new BuildPipelineRunner(
                context,
                persistence,
                source,
                state);
        }

        /// <summary>
        /// 从检查点恢复构建任务。
        /// 同进程恢复（Domain Reload）复用现有锁；跨进程恢复时清理残留锁后重新获取。
        /// </summary>
        /// <param name="state">待恢复的任务状态，不能为空且不能是终态。</param>
        /// <param name="persistenceRoot">持久化根目录；为空时使用工程默认目录。</param>
        /// <param name="steps">显式步骤列表；为空时从注册表自动发现。</param>
        /// <param name="profile">构建配置；为空时从状态中的资产路径重新加载。</param>
        /// <returns>已加锁并恢复检查点的运行器。</returns>
        public static BuildPipelineRunner Resume(
            BuildPipelineState state,
            string persistenceRoot = null,
            IReadOnlyList<IBuildPipelineStep> steps = null,
            UnityRFrameworkBuildProfile profile = null)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            if (state.IsFinished)
            {
                throw new InvalidOperationException(
                    "已结束的构建任务不能恢复。");
            }

            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence(persistenceRoot);
            List<IBuildPipelineStep> ordered = PrepareSteps(steps);
            if (profile == null)
            {
                profile = LoadProfile(state);
            }

            RestoreMissingBuildIdentity(state, profile);

            CancellationTokenSource source = new CancellationTokenSource();
            BuildPipelineContext context = BuildPipelineContext.Create(
                profile,
                ToStepDictionary(ordered),
                source.Token);

            if (!BuildPipelineRecovery.IsActiveInSession())
            {
                // 跨进程恢复：旧进程的锁文件为残留，先清理再重新获取。
                persistence.ReleaseLock();
                string lockError;
                if (!persistence.TryAcquireLock(state, out lockError))
                {
                    source.Dispose();
                    throw new InvalidOperationException(
                        $"无法恢复构建任务：{lockError}");
                }
            }

            state.Phase = BuildPipelinePhase.Running;
            state.UpdatedAt = NowIso();
            persistence.SaveState(state);
            MarkActive();
            return new BuildPipelineRunner(
                context,
                persistence,
                source,
                state);
        }

        /// <summary>
        /// 执行整个流水线（同步阻塞）。
        /// 执行过程中每步开始前与完成后均落盘检查点；
        /// 失败立即停止，取消后不再启动后续步骤。
        /// </summary>
        /// <returns>流水线执行结果。</returns>
        public BuildRunResult Execute()
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine(
                $"构建任务开始：{CurrentState.ProfileName}（{CurrentState.TaskId}）");

            while (CurrentState.CurrentStepIndex < CurrentState.StepIds.Count)
            {
                if (cts.IsCancellationRequested)
                {
                    CurrentState.Phase = BuildPipelinePhase.Cancelled;
                    CurrentState.UpdatedAt = NowIso();
                    persistence.SaveState(CurrentState);
                    report.AppendLine(
                        "构建任务已被取消，未开始的步骤不再执行。");
                    break;
                }

                string stepId = CurrentState.StepIds[CurrentState.CurrentStepIndex];
                IBuildPipelineStep step;
                if (!Context.Steps.TryGetValue(stepId, out step))
                {
                    CurrentState.Phase = BuildPipelinePhase.Failed;
                    CurrentState.ErrorMessage =
                        $"步骤 '{stepId}' 的实现缺失，任务无法继续。";
                    CurrentState.UpdatedAt = NowIso();
                    persistence.SaveState(CurrentState);
                    report.AppendLine(
                        $"步骤 '{stepId}' 的实现缺失，任务失败。");
                    break;
                }

                // 步骤开始前落盘检查点（包含当前步骤索引）。
                string startedAt = NowIso();
                CurrentState.UpdatedAt = startedAt;
                persistence.SaveState(CurrentState);

                BuildStepResult result;
                try
                {
                    result = step.Execute(Context);
                }
                catch (Exception exception)
                {
                    result = BuildStepResult.Failed(
                        $"步骤 {step.DisplayName} 抛出异常：{exception.Message}",
                        exception);
                }

                string finishedAt = NowIso();
                BuildStepRecord record = new BuildStepRecord
                {
                    StepId = stepId,
                    Status = result.Status.ToString(),
                    Message = result.Message,
                    OutputPath = result.OutputPath,
                    ExceptionText = result.Exception != null
                        ? result.Exception.ToString()
                        : string.Empty,
                    StartedAt = startedAt,
                    FinishedAt = finishedAt
                };

                if (result.Status == BuildStepStatus.Succeeded)
                {
                    report.AppendLine(
                        $"[{finishedAt}] 步骤 {step.DisplayName}（{stepId}）成功：{result.Message}");
                    CurrentState.CompletedSteps.Add(record);
                    CurrentState.CurrentStepIndex++;
                    CurrentState.UpdatedAt = NowIso();
                    persistence.SaveState(CurrentState);
                }
                else if (result.Status == BuildStepStatus.Failed)
                {
                    report.AppendLine(
                        $"[{finishedAt}] 步骤 {step.DisplayName}（{stepId}）失败：{result.Message}");
                    CurrentState.FailedStep = record;
                    CurrentState.Phase = BuildPipelinePhase.Failed;
                    CurrentState.ErrorMessage = result.Message;
                    CurrentState.UpdatedAt = NowIso();
                    persistence.SaveState(CurrentState);
                    break;
                }
                else
                {
                    report.AppendLine(
                        $"[{finishedAt}] 步骤 {step.DisplayName}（{stepId}）已取消：{result.Message}");
                    CurrentState.Phase = BuildPipelinePhase.Cancelled;
                    CurrentState.UpdatedAt = NowIso();
                    persistence.SaveState(CurrentState);
                    break;
                }
            }

            if (CurrentState.Phase == BuildPipelinePhase.Running)
            {
                CurrentState.Phase = BuildPipelinePhase.Succeeded;
                CurrentState.UpdatedAt = NowIso();
                persistence.SaveState(CurrentState);
                report.AppendLine("构建任务成功完成。");
            }

            bool succeeded = CurrentState.Phase == BuildPipelinePhase.Succeeded;
            bool cancelled = CurrentState.Phase == BuildPipelinePhase.Cancelled;
            WriteExecutionReport(succeeded, cancelled);
            if (succeeded)
            {
                // 构建号只在完整成功后提交；失败与取消不消耗正式版本号。
                CommitBuildNumberSafely();
            }

            BuildRunResult resultObject = new BuildRunResult(
                succeeded,
                cancelled,
                report.ToString(),
                CurrentState);
            Finish(resultObject);
            return resultObject;
        }

        /// <summary>
        /// 请求取消：只停止尚未开始的后续步骤。
        /// Unity 已进入不可取消的 Player 构建时，步骤需自行检查
        /// <see cref="BuildPipelineContext.CancellationToken"/> 并明确提示。
        /// </summary>
        public void Cancel()
        {
            cts.Cancel();
        }

        /// <summary>
        /// 清除活动任务标记（任务结束或作废时调用）。
        /// </summary>
        public static void ClearActive()
        {
            HasActiveTask = false;
            BuildPipelineRecovery.ClearSessionMark();
        }

        /// <summary>
        /// 创建运行器实例。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        /// <param name="persistence">持久化实例。</param>
        /// <param name="cts">取消令牌源。</param>
        /// <param name="state">任务状态。</param>
        private BuildPipelineRunner(
            BuildPipelineContext context,
            BuildPipelinePersistence persistence,
            CancellationTokenSource cts,
            BuildPipelineState state)
        {
            Context = context;
            this.persistence = persistence;
            this.cts = cts;
            CurrentState = state;
        }

        /// <summary>
        /// 准备步骤列表：注入列表或注册表发现结果，统一做确定性排序。
        /// </summary>
        /// <param name="injected">注入的步骤列表，可为空。</param>
        /// <returns>排序后的步骤列表。</returns>
        private static List<IBuildPipelineStep> PrepareSteps(
            IReadOnlyList<IBuildPipelineStep> injected)
        {
            List<IBuildPipelineStep> steps = injected != null
                ? new List<IBuildPipelineStep>(injected)
                : new List<IBuildPipelineStep>(BuildPipelineStepRegistry.GetAll());
            steps.Sort(BuildPipelineStepRegistry.CompareSteps);
            return steps;
        }

        /// <summary>
        /// 将 Recipe 解析错误合并为启动失败消息。
        /// </summary>
        private static string FormatRecipeErrors(
            IReadOnlyList<BuildValidationIssue> issues)
        {
            StringBuilder builder = new StringBuilder(
                "构建 Recipe 无法执行：");
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Level != BuildValidationLevel.Error)
                {
                    continue;
                }

                builder.AppendLine();
                builder.Append("- ");
                builder.Append(issues[i].Message);
            }

            return builder.ToString();
        }

        /// <summary>
        /// 过滤出可用于当前上下文的步骤；可用性判断抛出异常时按不可用处理。
        /// 步骤同时满足 CanRun 且未被 Profile.Steps 显式禁用时才会进入执行列表；
        /// Profile 未配置该步骤条目时按可用处理，使核心步骤与自动步骤正常执行。
        /// </summary>
        /// <param name="ordered">排序后的步骤列表。</param>
        /// <param name="context">构建上下文。</param>
        /// <returns>可用步骤列表。</returns>
        private static List<IBuildPipelineStep> FilterUsable(
            List<IBuildPipelineStep> ordered,
            BuildPipelineContext context)
        {
            List<IBuildPipelineStep> usable = new List<IBuildPipelineStep>();
            for (int i = 0; i < ordered.Count; i++)
            {
                IBuildPipelineStep step = ordered[i];
                try
                {
                    if (!step.CanRun(context))
                    {
                        continue;
                    }

                    if (IsExplicitlyDisabled(step.Id, context.Profile))
                    {
                        continue;
                    }

                    usable.Add(step);
                }
                catch (Exception exception)
                {
                    Debug.Log(
                        $"步骤 {step.Id} 的可用性判断抛出异常，已排除：{exception.Message}");
                }
            }
            return usable;
        }

        /// <summary>
        /// 判断步骤是否被 Profile.Steps 显式禁用。
        /// 步骤 Id 按大小写不敏感匹配，与 <see cref="BuildStepConfigLocator"/> 保持一致；
        /// Profile 未配置该步骤条目时返回 false；只有显式配置的条目才能禁用步骤。
        /// </summary>
        /// <param name="stepId">步骤唯一 Id。</param>
        /// <param name="profile">构建配置，可为空。</param>
        /// <returns>条目存在且未启用时返回 true。</returns>
        private static bool IsExplicitlyDisabled(
            string stepId,
            UnityRFrameworkBuildProfile profile)
        {
            if (profile == null || profile.Steps == null)
            {
                return false;
            }

            for (int i = 0; i < profile.Steps.Count; i++)
            {
                BuildStepSettings setting = profile.Steps[i];
                if (setting == null)
                {
                    continue;
                }

                if (string.Equals(
                    setting.StepId,
                    stepId,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return !setting.Enabled;
                }
            }

            return false;
        }

        /// <summary>
        /// 将步骤列表转换为 Id 到实例的字典。
        /// </summary>
        /// <param name="steps">步骤列表。</param>
        /// <returns>步骤字典。</returns>
        private static Dictionary<string, IBuildPipelineStep> ToStepDictionary(
            List<IBuildPipelineStep> steps)
        {
            Dictionary<string, IBuildPipelineStep> dictionary =
                new Dictionary<string, IBuildPipelineStep>(StringComparer.Ordinal);
            for (int i = 0; i < steps.Count; i++)
            {
                dictionary[steps[i].Id] = steps[i];
            }
            return dictionary;
        }

        /// <summary>
        /// 创建初始任务状态。
        /// </summary>
        /// <param name="profile">构建配置。</param>
        /// <param name="context">构建上下文。</param>
        /// <param name="usable">可用步骤列表。</param>
        /// <returns>初始状态。</returns>
        private static BuildPipelineState CreateState(
            UnityRFrameworkBuildProfile profile,
            BuildPipelineContext context,
            List<IBuildPipelineStep> usable)
        {
            BuildPipelineState state = new BuildPipelineState
            {
                TaskId = Guid.NewGuid().ToString("N"),
                ProfileGuid = AssetDatabase.AssetPathToGUID(
                    AssetDatabase.GetAssetPath(profile)),
                ProfileAssetPath = AssetDatabase.GetAssetPath(profile),
                ProfileName = profile.name,
                TargetName = profile.Platform.Target.ToString(),
                FlavorName = profile.Flavor.ToString(),
                PublicVersion = profile.Platform.PublicVersion,
                BuildNumber = profile.Platform.BuildNumber,
                OutputRootAbsolute = context.OutputRootAbsolute,
                OutputDirectory = context.OutputDirectory,
                OutputFileName = context.OutputFileName,
                CreatedAt = NowIso(),
                UpdatedAt = NowIso(),
                Phase = BuildPipelinePhase.Running
            };
            for (int i = 0; i < usable.Count; i++)
            {
                state.StepIds.Add(usable[i].Id);
            }
            return state;
        }

        /// <summary>
        /// 为旧版检查点补齐尚未持久化的构建身份。
        /// 正在恢复的任务尚未提交构建号递增，因此可安全读取 Profile 当前值。
        /// </summary>
        /// <param name="state">待恢复任务状态。</param>
        /// <param name="profile">任务使用的构建配置。</param>
        private static void RestoreMissingBuildIdentity(
            BuildPipelineState state,
            UnityRFrameworkBuildProfile profile)
        {
            if (string.IsNullOrEmpty(state.PublicVersion))
            {
                state.PublicVersion = profile.Platform.PublicVersion;
            }
            if (state.BuildNumber <= 0)
            {
                state.BuildNumber = profile.Platform.BuildNumber;
            }
        }

        /// <summary>
        /// 从状态中的资产路径重新加载构建配置。
        /// </summary>
        /// <param name="state">任务状态。</param>
        /// <returns>加载的构建配置。</returns>
        private static UnityRFrameworkBuildProfile LoadProfile(
            BuildPipelineState state)
        {
            if (string.IsNullOrWhiteSpace(state.ProfileAssetPath))
            {
                throw new InvalidOperationException(
                    $"任务 {state.TaskId} 未记录构建配置资产路径，无法恢复。");
            }
            UnityRFrameworkBuildProfile profile =
                AssetDatabase.LoadAssetAtPath<UnityRFrameworkBuildProfile>(
                    state.ProfileAssetPath);
            if (profile == null)
            {
                throw new InvalidOperationException(
                    $"无法加载构建配置资产：{state.ProfileAssetPath}");
            }
            return profile;
        }

        /// <summary>
        /// 标记当前进程存在活动构建任务。
        /// </summary>
        private static void MarkActive()
        {
            HasActiveTask = true;
            BuildPipelineRecovery.MarkActiveInSession();
        }

        /// <summary>
        /// 组装并写入构建报告；成功、失败与取消均会写报告，保证失败构建
        /// 有可定位的步骤与原因。报告写入失败只警告，不阻断任务收尾。
        /// </summary>
        /// <param name="succeeded">任务是否成功完成。</param>
        /// <param name="cancelled">任务是否被取消。</param>
        private void WriteExecutionReport(bool succeeded, bool cancelled)
        {
            try
            {
                BuildExecutionReport report = BuildExecutionReport.Create(
                    Context,
                    CurrentState,
                    succeeded,
                    cancelled);
                string reportPath = BuildReportWriter.Write(
                    report,
                    CurrentState.OutputRootAbsolute,
                    CurrentState.OutputDirectory,
                    CurrentState.TaskId);
                Debug.Log($"构建报告已写入：{reportPath}");
            }
            catch (Exception exception)
            {
                Debug.Log($"构建报告写入失败：{exception.Message}");
            }
        }

        /// <summary>
        /// 提交构建号自动递增；异常只警告，不阻断任务收尾。
        /// </summary>
        private void CommitBuildNumberSafely()
        {
            try
            {
                BuildVersionResolver.CommitBuildNumber(Context.Profile);
            }
            catch (Exception exception)
            {
                Debug.Log($"构建号自动递增失败：{exception.Message}");
            }
        }

        /// <summary>
        /// 收尾：释放锁、清理状态、清除活动标记并输出报告日志。
        /// </summary>
        /// <param name="result">执行结果。</param>
        private void Finish(BuildRunResult result)
        {
            persistence.ReleaseLock();
            persistence.DeleteState();
            ClearActive();
            if (result.Succeeded)
            {
                Debug.Log(result.ReportText);
            }
            else if (result.Cancelled)
            {
                Debug.Log(result.ReportText);
            }
            else
            {
                Debug.LogError(result.ReportText);
            }
        }

        /// <summary>
        /// 获取当前时刻的 ISO 8601 字符串。
        /// </summary>
        /// <returns>ISO 8601 时间字符串。</returns>
        private static string NowIso()
        {
            return DateTime.Now.ToString("o");
        }
    }
}
