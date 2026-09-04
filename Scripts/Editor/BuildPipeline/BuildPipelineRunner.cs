using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Process = System.Diagnostics.Process;

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
    /// 可恢复构建流水线状态机内核：由 <see cref="EditorApplication.update"/>
    /// 单步推进，每次更新只启动或推进一个阶段；有副作用的步骤执行前落盘检查点，
    /// 声明会切换平台、触发编译或 Domain Reload 的步骤完成后进入等待编辑器状态，
    /// 归还 Unity 控制权，编辑器恢复空闲（或 Domain Reload 后由恢复检查）再继续。
    /// 通过 SessionState 会话标记（保存 TaskId）区分 Domain Reload（同进程自动恢复）
    /// 与进程重启（按锁归属决定提示、接管或不干预）；恢复决策由
    /// <see cref="BuildPipelineRecovery"/> 负责。
    /// 执行语义：
    /// - 每个步骤开始前与完成后均落盘检查点（安全替换 + 备份）；
    /// - 失败立即停止，记录失败步骤与完整异常；失败任务保留状态与锁，
    ///   恢复时重新执行失败步骤（重试）；
    /// - 取消只停止尚未开始的后续步骤，已完成步骤保留；取消任务同样保留状态与锁；
    /// - 步骤返回等待编辑器或声明会触发重载时保存等待原因并归还控制权，会话标记保留；
    /// - 设置事务：任务开始捕获快照，结束（成功、失败、取消）时恢复"应用参数"
    ///   写入的临时设置；恢复失败任务进入人工处理状态；
    /// - 仅任务成功后才释放锁并清理状态；作废经 <see cref="BuildPipelineRecovery.AbandonTask"/>。
    /// </summary>
    public sealed class BuildPipelineRunner
    {
        /// <summary>等待编辑器空闲后的稳定确认更新帧数：busy 结束后需连续空闲该帧数才续跑。</summary>
        private const int ResumeIdleTicks = 5;

        /// <summary>未观察到编译忙时，直接续跑前需要的空闲帧数（放宽确认，避免误判）。</summary>
        private const int FreshIdleTicks = 30;

        /// <summary>当前进程是否存在活动构建任务。</summary>
        public static bool HasActiveTask { get; private set; }

        /// <summary>当前进程的活动运行器；无活动任务时为空。</summary>
        public static BuildPipelineRunner Active { get; private set; }

        /// <summary>
        /// 任务到达终态（成功、失败、取消或人工处理）后触发；窗口与 BatchMode
        /// 通过该事件收尾（记录摘要、映射退出码）。事件在收尾清理前触发。
        /// </summary>
        public static event Action<BuildPipelineRunner> TaskCompleted;

        /// <summary>构建上下文。</summary>
        public BuildPipelineContext Context { get; }

        /// <summary>当前任务状态（执行过程中持续更新）。</summary>
        public BuildPipelineState CurrentState { get; }

        /// <summary>任务是否已到达终态（成功、失败、取消或人工处理）。</summary>
        public bool IsFinished { get; private set; }

        /// <summary>任务终态结果；未结束时为空。</summary>
        public BuildRunResult FinalResult { get; private set; }

        /// <summary>持久化实例。</summary>
        private readonly BuildPipelinePersistence persistence;

        /// <summary>源 Profile 资产；仅用于成功后提交非覆盖构建号。</summary>
        private readonly UnityRFrameworkBuildProfile sourceProfile;

        /// <summary>报告写入函数；测试可注入失败实现。</summary>
        private readonly Func<BuildExecutionReport, string, string, string, string>
            reportWriter;

        /// <summary>取消令牌源。</summary>
        private readonly CancellationTokenSource cts;

        /// <summary>状态机内部阶段。</summary>
        private KernelPhase kernelPhase;

        /// <summary>报告缓冲。</summary>
        private readonly StringBuilder report = new StringBuilder();

        /// <summary>当前步骤开始时刻（ISO 8601）。</summary>
        private string currentStepStartedAt = string.Empty;

        /// <summary>等待编辑器期间是否观察到过编译/导入忙。</summary>
        private bool sawEditorBusy;

        /// <summary>等待编辑器期间已连续空闲的更新帧数。</summary>
        private int idleTicks;

        /// <summary>内核内部阶段。</summary>
        private enum KernelPhase
        {
            /// <summary>步骤间：准备执行下一步骤。</summary>
            BetweenSteps,

            /// <summary>正在执行当前步骤（同步代码段）。</summary>
            ExecuteStep,

            /// <summary>等待编辑器完成编译、导入或 Domain Reload。</summary>
            WaitingEditor,

            /// <summary>已结束。</summary>
            Done
        }

        /// <summary>
        /// 启动新构建任务：加锁、捕获设置事务快照、落盘初始检查点并注册到
        /// EditorApplication.update 驱动。任务由内核异步推进，本方法立即返回。
        /// </summary>
        /// <param name="profile">待执行的构建配置，不能为空。</param>
        /// <param name="persistenceRoot">持久化根目录；为空时使用工程默认目录（测试注入临时目录）。</param>
        /// <param name="steps">显式步骤列表；为空时从注册表自动发现。</param>
        /// <param name="recipeOverride">运行期 Recipe 覆盖；为空时使用 Profile 保存的 Recipe。</param>
        /// <param name="taskOverrides">任务级参数覆盖；仅作用于内存副本。</param>
        /// <param name="reportWriter">报告写入函数；为空时使用默认实现。</param>
        /// <returns>已加锁并落盘初始检查点的运行器。</returns>
        public static BuildPipelineRunner StartNew(
            UnityRFrameworkBuildProfile profile,
            string persistenceRoot = null,
            IReadOnlyList<IBuildPipelineStep> steps = null,
            BuildRecipe? recipeOverride = null,
            BuildTaskOverrides taskOverrides = null,
            Func<BuildExecutionReport, string, string, string, string>
                reportWriter = null)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence(persistenceRoot);
            taskOverrides = taskOverrides ?? new BuildTaskOverrides();
            UnityRFrameworkBuildProfile effectiveProfile =
                taskOverrides.CreateEffectiveProfile(profile);
            List<IBuildPipelineStep> available = PrepareSteps(steps);
            BuildRecipePlan recipePlan =
                BuildRecipePlanner.Create(
                    effectiveProfile,
                    available,
                    requireCoreSteps: steps == null,
                    recipeOverride: recipeOverride);
            if (!recipePlan.IsValid)
            {
                throw new InvalidOperationException(
                    FormatRecipeErrors(recipePlan.Issues));
            }

            // 启动前检查残留任务：非终态状态与锁必须先恢复、重试或作废，
            // 不允许静默覆盖上一任务的检查点。
            BuildPipelineStateLoadResult leftoverResult =
                persistence.LoadStateDetailed(out BuildPipelineState leftover);
            if (leftoverResult == BuildPipelineStateLoadResult.Success
                || leftoverResult == BuildPipelineStateLoadResult.LoadedFromBackup)
            {
                if (leftover != null && !leftover.IsTerminal)
                {
                    throw new InvalidOperationException(
                        $"存在未完成的构建任务（{leftover.ProfileName}，{leftover.TaskId}，"
                        + $"状态 {leftover.Phase}），请先恢复、重试或作废后再启动新任务。");
                }

                // 终态残留由本次启动前的锁获取与后续清理兜底，直接覆盖。
            }
            else if (leftoverResult == BuildPipelineStateLoadResult.Corrupt
                || leftoverResult == BuildPipelineStateLoadResult.VersionMismatch)
            {
                throw new InvalidOperationException(
                    "构建任务状态文件无法读取（"
                    + $"{leftoverResult}），为避免吞掉损坏状态已阻止启动；"
                    + "请重启 Unity 由恢复检查处理，或手动作废 Library/UnityRFramework/"
                    + "BuildPipeline 下的状态与锁文件。");
            }

            string taskId = Guid.NewGuid().ToString("N");
            List<IBuildPipelineStep> ordered =
                new List<IBuildPipelineStep>(recipePlan.Steps);
            CancellationTokenSource source = new CancellationTokenSource();
            BuildPipelineContext previewContext = BuildPipelineContext.Create(
                effectiveProfile,
                ToStepDictionary(ordered),
                source.Token,
                taskId,
                persistenceRoot,
                recipeOverride);
            List<IBuildPipelineStep> usable = FilterUsable(ordered, previewContext);
            BuildPipelineState state = CreateState(
                profile,
                effectiveProfile,
                previewContext,
                usable,
                taskId,
                taskOverrides);

            string lockError;
            if (!persistence.TryAcquireLock(state, out lockError))
            {
                source.Dispose();
                throw new InvalidOperationException(
                    $"无法启动构建任务：{lockError}");
            }

            BuildPipelineContext context;
            try
            {
                context = BuildPipelineContext.Create(
                    effectiveProfile,
                    ToStepDictionary(ordered),
                    source.Token,
                taskId,
                persistenceRoot,
                recipeOverride,
                captureSettingsTransaction: true,
                integrationActivationControllers:
                    GetIntegrationActivationControllers(available));
            }
            catch
            {
                persistence.ReleaseLock();
                source.Dispose();
                throw;
            }

            persistence.SaveState(state);
            return new BuildPipelineRunner(
                context,
                profile,
                persistence,
                source,
                state,
                reportHeader:
                    $"构建结果详情：{state.ProfileName}（任务 {state.TaskId}）",
                reportWriter: reportWriter);
        }

        /// <summary>
        /// 从检查点恢复构建任务。失败与取消任务可恢复以重试失败步骤或继续后续步骤。
        /// 同进程恢复（会话 TaskId 匹配）复用现有锁；跨进程恢复时校验残留锁：
        /// 持锁进程仍存活且工程匹配则拒绝恢复，锁残留时清理后重新获取。
        /// </summary>
        /// <param name="state">待恢复的任务状态，不能为空、不能是终态且必须绑定 TaskId。</param>
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
            if (state.IsTerminal)
            {
                throw new InvalidOperationException(
                    "已结束（成功或作废）的构建任务不能恢复。");
            }
            if (string.IsNullOrEmpty(state.TaskId))
            {
                throw new InvalidOperationException(
                    "任务状态未绑定 TaskId，无法恢复。");
            }
            if (BuildPipelineRecovery.IsPlayerBuildInterrupted(state))
            {
                // 强杀在 Player 编译阶段会损坏 Unity 增量状态，恢复重试只会
                // 再次遇到假成功；唯一路径是作废后先用官方构建完整修复。
                throw new InvalidOperationException(
                    $"任务 {state.TaskId} 在「构建 Player」阶段被强制中断，"
                    + "Unity 增量状态可能已损坏，不支持恢复/重试。"
                    + "请作废本任务，先用官方 Build Settings 完整构建一次"
                    + "（成功产出 exe）修复状态，再用本工具构建。");
            }

            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence(persistenceRoot);
            List<IBuildPipelineStep> ordered = PrepareSteps(steps);
            if (profile == null)
            {
                profile = LoadProfile(state);
            }
            BuildTaskOverrides taskOverrides =
                state.TaskOverrides ?? new BuildTaskOverrides();
            UnityRFrameworkBuildProfile effectiveProfile =
                taskOverrides.CreateEffectiveProfile(profile);

            bool sameSession =
                string.Equals(
                    BuildPipelineRecovery.GetActiveTaskId(),
                    state.TaskId,
                    StringComparison.Ordinal);
            if (!sameSession)
            {
                // 跨进程恢复：验证残留锁归属；持锁进程为其他存活 Unity 进程且工程匹配时
                // 拒绝并发恢复；锁残留（含本进程上次运行遗留、进程已退出或工程不匹配）
                // 则清理后重新获取。
                if (persistence.ReadLockInfo(out BuildPipelineLockInfo holder))
                {
                    int currentProcessId = Process.GetCurrentProcess().Id;
                    if (holder.TaskId == state.TaskId
                        && holder.ProcessId != currentProcessId
                        && holder.MatchesProject(
                            BuildPipelinePersistence.GetProjectIdentity())
                        && holder.IsHolderProcessAlive())
                    {
                        throw new InvalidOperationException(
                            $"任务 {state.TaskId} 正由其他 Unity 进程执行"
                            + $"（PID {holder.ProcessId}），不能并发恢复。");
                    }
                }

                persistence.ReleaseLock();
                string lockError;
                if (!persistence.TryAcquireLock(state, out lockError))
                {
                    throw new InvalidOperationException(
                        $"无法恢复构建任务：{lockError}");
                }
            }

            CancellationTokenSource source = new CancellationTokenSource();
            BuildPipelineContext context = BuildPipelineContext.Create(
                effectiveProfile,
                ToStepDictionary(ordered),
                source.Token,
                state.TaskId,
                persistenceRoot,
                state.Recipe,
                captureSettingsTransaction: true,
                integrationActivationControllers:
                    GetIntegrationActivationControllers(ordered));

            state.Phase = BuildPipelinePhase.Running;
            state.WaitingReason = string.Empty;
            state.UpdatedAt = NowIso();
            persistence.SaveState(state);
            return new BuildPipelineRunner(
                context,
                profile,
                persistence,
                source,
                state,
                reportHeader:
                    $"构建结果详情：{state.ProfileName}（任务 {state.TaskId}，恢复执行），"
                    + $"从步骤 {state.CurrentStepIndex + 1}/{state.StepIds.Count} 继续。",
                reportWriter: null);
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
        /// 推进一次状态机：每次调用最多启动或推进一个阶段。
        /// 由 EditorApplication.update 自动驱动；测试可手动循环调用。
        /// 每次推进前刷新任务进度条（可取消）；原生编译步骤执行期间由
        /// Unity 自身的构建进度条接管，其取消结果同样映射为任务已取消。
        /// </summary>
        public void Tick()
        {
            if (IsFinished || !ReferenceEquals(Active, this))
            {
                return;
            }

            UpdateTaskProgressBar();

            try
            {
                switch (kernelPhase)
                {
                    case KernelPhase.BetweenSteps:
                        TickBetweenSteps();
                        break;
                    case KernelPhase.ExecuteStep:
                        TickExecuteStep();
                        break;
                    case KernelPhase.WaitingEditor:
                        TickWaitingEditor();
                        break;
                }
            }
            catch (Exception exception)
            {
                // 内核自身异常按任务失败处理，保留检查点供重试。
                FailTask($"构建内核异常：{exception.Message}", exception.ToString());
                kernelPhase = KernelPhase.Done;
                Complete(new BuildRunResult(
                    false,
                    false,
                    report.ToString(),
                    CurrentState));
            }
        }

        /// <summary>
        /// 刷新任务级可取消进度条。仅在主线程可交互的时机显示：
        /// 步骤边界与等待编辑器期（此时取消点击会在下一次推进被接收）。
        /// 进入原生编译步骤（构建 Player 等）时隐藏本进度条——
        /// 主线程由 Unity 占据，取消交由 Unity 原生构建进度条
        /// （其取消映射为 BuildReport Cancelled，任务记为已取消、无产物）。
        /// </summary>
        private void UpdateTaskProgressBar()
        {
            if (kernelPhase == KernelPhase.ExecuteStep)
            {
                EditorUtility.ClearProgressBar();
                return;
            }

            int total = Math.Max(1, CurrentState.StepIds.Count);
            float progress = Mathf.Clamp01(
                (float)CurrentState.CurrentStepIndex / total);
            string message = kernelPhase == KernelPhase.WaitingEditor
                ? $"等待编辑器：{CurrentState.WaitingReason}"
                : $"步骤 {CurrentState.CurrentStepIndex + 1}/{total}";

            bool cancelClicked = EditorUtility.DisplayCancelableProgressBar(
                "UnityRFramework 构建任务",
                $"{CurrentState.ProfileName}：{message}",
                progress);
            if (cancelClicked)
            {
                Cancel();
            }
        }

        /// <summary>
        /// 驱动当前进程的活动运行器推进一次（无活动任务时无操作）。
        /// </summary>
        public static void TickActive()
        {
            Active?.Tick();
        }

        /// <summary>
        /// 同步泵：阻塞推进状态机直至任务到达终态。
        /// 仅适用于测试与确认不会触发 Domain Reload 的轻量场景；
        /// 窗口与 BatchMode 应使用 update 驱动与完成事件。
        /// </summary>
        /// <returns>任务终态结果。</returns>
        public BuildRunResult Execute()
        {
            int guard = 0;
            while (!IsFinished)
            {
                Tick();
                guard++;
                if (guard > 100000)
                {
                    throw new InvalidOperationException(
                        "构建状态机推进超过保护上限，可能存在死循环步骤。");
                }
            }

            return FinalResult;
        }

        /// <summary>
        /// 清除活动任务标记（任务结束或作废时调用）。
        /// </summary>
        public static void ClearActive()
        {
            if (Active != null)
            {
                EditorApplication.update -= Active.Tick;
                Active.kernelPhase = KernelPhase.Done;
                Active.IsFinished = true;
                Active = null;
            }

            EditorUtility.ClearProgressBar();
            HasActiveTask = false;
            BuildPipelineRecovery.ClearSessionMark();
        }

        /// <summary>
        /// 创建运行器实例并注册到编辑器更新驱动。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        /// <param name="persistence">持久化实例。</param>
        /// <param name="cts">取消令牌源。</param>
        /// <param name="state">任务状态。</param>
        /// <param name="reportHeader">报告首行。</param>
        /// <param name="reportWriter">报告写入函数；为空时使用默认实现。</param>
        private BuildPipelineRunner(
            BuildPipelineContext context,
            UnityRFrameworkBuildProfile sourceProfile,
            BuildPipelinePersistence persistence,
            CancellationTokenSource cts,
            BuildPipelineState state,
            string reportHeader,
            Func<BuildExecutionReport, string, string, string, string>
                reportWriter)
        {
            Context = context;
            this.sourceProfile = sourceProfile;
            this.persistence = persistence;
            this.cts = cts;
            this.reportWriter = reportWriter ?? BuildReportWriter.Write;
            CurrentState = state;
            kernelPhase = KernelPhase.BetweenSteps;
            report.AppendLine(reportHeader);

            Active = this;
            HasActiveTask = true;
            BuildPipelineRecovery.MarkActiveInSession(state.TaskId);
            EditorApplication.update += Tick;
        }

        /// <summary>
        /// 步骤间阶段：处理取消、完成、步骤缺失，并登记下一步骤的开始检查点。
        /// </summary>
        private void TickBetweenSteps()
        {
            if (cts.IsCancellationRequested)
            {
                CurrentState.Phase = BuildPipelinePhase.Cancelled;
                CurrentState.UpdatedAt = NowIso();
                persistence.SaveState(CurrentState);
                report.AppendLine("构建任务已被取消，未开始的步骤不再执行。");
                kernelPhase = KernelPhase.Done;
                Complete(new BuildRunResult(
                    false,
                    true,
                    report.ToString(),
                    CurrentState));
                return;
            }

            if (CurrentState.CurrentStepIndex >= CurrentState.StepIds.Count)
            {
                CurrentState.Phase = BuildPipelinePhase.Succeeded;
                CurrentState.UpdatedAt = NowIso();
                persistence.SaveState(CurrentState);
                report.AppendLine("构建任务成功完成。");
                kernelPhase = KernelPhase.Done;
                Complete(new BuildRunResult(
                    true,
                    false,
                    report.ToString(),
                    CurrentState));
                return;
            }

            string stepId = CurrentState.StepIds[CurrentState.CurrentStepIndex];
            if (!Context.Steps.TryGetValue(stepId, out IBuildPipelineStep step))
            {
                FailTask(
                    $"步骤 '{stepId}' 的实现缺失，任务无法继续。",
                    string.Empty);
                kernelPhase = KernelPhase.Done;
                Complete(new BuildRunResult(
                    false,
                    false,
                    report.ToString(),
                    CurrentState));
                return;
            }

            // 步骤开始前落盘检查点（包含当前步骤索引）。
            currentStepStartedAt = NowIso();
            CurrentState.UpdatedAt = currentStepStartedAt;
            persistence.SaveState(CurrentState);
            kernelPhase = KernelPhase.ExecuteStep;
        }

        /// <summary>
        /// 执行阶段：执行当前步骤并根据结果提交记录、失败、取消或等待。
        /// </summary>
        private void TickExecuteStep()
        {
            string stepId = CurrentState.StepIds[CurrentState.CurrentStepIndex];
            IBuildPipelineStep step = Context.Steps[stepId];
            BuildStepResult result;
            if (string.Equals(stepId, "core.finalize", StringComparison.Ordinal)
                && (Context.Recipe == BuildRecipe.Player
                    || Context.Recipe == BuildRecipe.Release)
                && !CurrentState.PlayerProduced)
            {
                result = BuildStepResult.Failed(
                    "本次任务未产生 Player，拒绝使用输出目录中的旧产物完成收尾。",
                    null);
            }
            else
            {
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
                StartedAt = currentStepStartedAt,
                FinishedAt = finishedAt
            };

            if (Context.SettingsTransaction != null
                && Context.SettingsTransaction.HasChanges)
            {
                CurrentState.SettingsApplied = true;
            }
            if (result.Status == BuildStepStatus.Succeeded
                && string.Equals(
                    stepId,
                    "core.build-player",
                    StringComparison.Ordinal))
            {
                CurrentState.PlayerProduced = true;
            }

            if (result.Status == BuildStepStatus.Succeeded)
            {
                report.AppendLine(
                    $"[{finishedAt}] 步骤 {step.DisplayName}（{stepId}）成功：{result.Message}");
                CurrentState.CompletedSteps.Add(record);
                CurrentState.CurrentStepIndex++;
                CurrentState.UpdatedAt = NowIso();
                persistence.SaveState(CurrentState);

                if (cts.IsCancellationRequested)
                {
                    // 取消请求在本步骤执行期间到达（如 Player 原生编译无法中断）：
                    // 本步骤已完成并保留记录，后续步骤（含收尾）不再执行。
                    report.AppendLine(
                        "构建任务已被取消（取消请求在本步骤执行期间到达，"
                        + "本步骤已完成，后续步骤不再执行）。");
                    CurrentState.Phase = BuildPipelinePhase.Cancelled;
                    CurrentState.UpdatedAt = NowIso();
                    persistence.SaveState(CurrentState);
                    kernelPhase = KernelPhase.Done;
                    Complete(new BuildRunResult(
                        false,
                        true,
                        report.ToString(),
                        CurrentState));
                    return;
                }

                if (step.SwitchesTarget
                    || step.TriggersCompilation
                    || step.TriggersDomainReload)
                {
                    // 有副作用的步骤完成后归还 Unity 控制权：
                    // 等待编译与资源导入结束（或 Domain Reload 后由恢复检查接管）。
                    EnterWaitingEditor("步骤触发平台切换、编译或 Domain Reload，等待编辑器稳定。");
                    return;
                }

                kernelPhase = KernelPhase.BetweenSteps;
                return;
            }

            if (result.Status == BuildStepStatus.Failed)
            {
                report.AppendLine(
                    $"[{finishedAt}] 步骤 {step.DisplayName}（{stepId}）失败：{result.Message}");
                CurrentState.FailedStep = record;
                FailTask(result.Message, record.ExceptionText);
                kernelPhase = KernelPhase.Done;
                Complete(new BuildRunResult(
                    false,
                    false,
                    report.ToString(),
                    CurrentState));
                return;
            }

            if (result.Status == BuildStepStatus.WaitingForEditor)
            {
                report.AppendLine(
                    $"[{finishedAt}] 步骤 {step.DisplayName}（{stepId}）等待编辑器：{result.Message}");
                CurrentState.CompletedSteps.Add(record);
                CurrentState.CurrentStepIndex++;
                CurrentState.UpdatedAt = NowIso();
                persistence.SaveState(CurrentState);
                EnterWaitingEditor(result.Message);
                return;
            }

            report.AppendLine(
                $"[{finishedAt}] 步骤 {step.DisplayName}（{stepId}）已取消：{result.Message}");
            CurrentState.Phase = BuildPipelinePhase.Cancelled;
            CurrentState.UpdatedAt = NowIso();
            persistence.SaveState(CurrentState);
            kernelPhase = KernelPhase.Done;
            Complete(new BuildRunResult(
                false,
                true,
                report.ToString(),
                CurrentState));
        }

        /// <summary>
        /// 等待阶段：观察编译与资源导入状态，稳定空闲后续跑；
        /// Domain Reload 会终止本运行器，由恢复检查在重载后自动续跑。
        /// </summary>
        private void TickWaitingEditor()
        {
            if (cts.IsCancellationRequested)
            {
                CurrentState.Phase = BuildPipelinePhase.Cancelled;
                CurrentState.UpdatedAt = NowIso();
                persistence.SaveState(CurrentState);
                report.AppendLine("构建任务在等待编辑器期间被取消。");
                kernelPhase = KernelPhase.Done;
                Complete(new BuildRunResult(
                    false,
                    true,
                    report.ToString(),
                    CurrentState));
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                sawEditorBusy = true;
                idleTicks = 0;
                return;
            }

            idleTicks++;
            int required = sawEditorBusy ? ResumeIdleTicks : FreshIdleTicks;
            if (idleTicks < required)
            {
                return;
            }

            // 编辑器已恢复稳定：继续执行。
            CurrentState.Phase = BuildPipelinePhase.Running;
            CurrentState.WaitingReason = string.Empty;
            CurrentState.UpdatedAt = NowIso();
            persistence.SaveState(CurrentState);
            sawEditorBusy = false;
            idleTicks = 0;
            kernelPhase = KernelPhase.BetweenSteps;
        }

        /// <summary>
        /// 进入等待编辑器状态：保存等待原因检查点并归还控制权。
        /// </summary>
        /// <param name="reason">等待原因。</param>
        private void EnterWaitingEditor(string reason)
        {
            CurrentState.Phase = BuildPipelinePhase.WaitingForEditor;
            CurrentState.WaitingReason = reason;
            CurrentState.UpdatedAt = NowIso();
            persistence.SaveState(CurrentState);
            sawEditorBusy = false;
            idleTicks = 0;
            kernelPhase = KernelPhase.WaitingEditor;
        }

        /// <summary>
        /// 将任务置为失败状态并落盘检查点。
        /// </summary>
        /// <param name="message">用户可见失败原因。</param>
        /// <param name="exceptionText">完整异常文本；可为空。</param>
        private void FailTask(string message, string exceptionText)
        {
            CurrentState.Phase = BuildPipelinePhase.Failed;
            CurrentState.ErrorMessage = message;
            if (!string.IsNullOrEmpty(exceptionText)
                && CurrentState.FailedStep != null
                && string.IsNullOrEmpty(CurrentState.FailedStep.ExceptionText))
            {
                CurrentState.FailedStep.ExceptionText = exceptionText;
            }

            CurrentState.UpdatedAt = NowIso();
            persistence.SaveState(CurrentState);
        }

        /// <summary>
        /// 任务终态收尾：恢复设置事务、写报告、提交构建号，
        /// 按"成功或明确作废后才清理"的契约处理状态与锁，并广播完成事件。
        /// </summary>
        /// <param name="result">终态结果。</param>
        private void Complete(BuildRunResult result)
        {
            if (IsFinished)
            {
                return;
            }

            IsFinished = true;
            FinalResult = result;
            EditorUtility.ClearProgressBar();
            EditorApplication.update -= Tick;

            RestoreTransactionIfNeeded();

            // 取消请求到达时「构建 Player」可能已在执行且无法中断：如实交代产物去留。
            if (FinalResult.Cancelled
                && Context.Profile != null
                && Context.OutputError.Length == 0)
            {
                try
                {
                    string productPath =
                        BuildPlayerOptionsFactory.ResolveLocationPath(Context);
                    if (ProductAlreadyBuilt(productPath))
                    {
                        report.AppendLine(
                            "注意：取消请求到达时「构建 Player」已在执行且无法中断，"
                            + "本次 Player 已构建完成，产物已生成并保留："
                            + $"{productPath}。任务按已取消处理——构建号未消耗、"
                            + "收尾步骤未执行；产物可自行保留或删除。");
                        FinalResult = new BuildRunResult(
                            false,
                            true,
                            report.ToString(),
                            CurrentState);
                    }
                }
                catch (Exception exception)
                {
                    Debug.Log($"取消产物检查失败：{exception.Message}");
                }
            }

            bool succeeded = FinalResult.Succeeded;
            bool cancelled = FinalResult.Cancelled;
            bool reportWritten = WriteExecutionReport(
                succeeded,
                cancelled,
                out string reportError);
            if (succeeded
                && !reportWritten
                && Context.Recipe == BuildRecipe.Release)
            {
                CurrentState.Phase = BuildPipelinePhase.Failed;
                CurrentState.ErrorMessage =
                    $"Release 构建报告写入失败：{reportError}";
                CurrentState.UpdatedAt = NowIso();
                persistence.SaveState(CurrentState);
                report.AppendLine($"[错误] {CurrentState.ErrorMessage}");
                FinalResult = new BuildRunResult(
                    false,
                    false,
                    report.ToString(),
                    CurrentState);
                succeeded = false;
                cancelled = false;
            }
            if (succeeded
                && (Context.Recipe == BuildRecipe.Player
                    || Context.Recipe == BuildRecipe.Release))
            {
                // 构建号只在完整成功后提交；失败与取消不消耗正式版本号。
                CommitBuildNumberSafely();
            }

            if (succeeded)
            {
                persistence.ReleaseLock();
                persistence.DeleteState();
                ClearActive();
            }
            else if (CurrentState.Phase == BuildPipelinePhase.ManualIntervention)
            {
                // 回滚失败：保留状态与锁等待人工处理；会话标记清除。
                HasActiveTask = false;
                BuildPipelineRecovery.ClearSessionMarkIfMatches(CurrentState.TaskId);
            }
            else
            {
                // 失败与取消：保留状态与锁等待重试、继续或作废。
                HasActiveTask = false;
                BuildPipelineRecovery.ClearSessionMarkIfMatches(CurrentState.TaskId);
            }

            if (succeeded)
            {
                Debug.Log(FinalResult.ReportText);
            }
            else if (cancelled)
            {
                Debug.Log(FinalResult.ReportText);
            }
            else
            {
                Debug.LogError(FinalResult.ReportText);
            }

            TaskCompleted?.Invoke(this);
        }

        /// <summary>
        /// 按契约恢复设置事务：已生效的事务在任务结束（成功、失败、取消）时恢复；
        /// 恢复失败将任务置为人工处理状态并重写终态结果。
        /// </summary>
        private void RestoreTransactionIfNeeded()
        {
            BuildSettingsTransaction transaction = Context.SettingsTransaction;
            if (transaction == null || !transaction.HasChanges)
            {
                CurrentState.RollbackState = BuildRollbackState.NotRequired;
                return;
            }

            try
            {
                transaction.Restore(Context.IntegrationActivationControllers);
                CurrentState.RollbackState = BuildRollbackState.Succeeded;
                report.AppendLine("临时构建设置已按快照恢复（活动平台按契约保留）。");
            }
            catch (Exception exception)
            {
                CurrentState.Phase = BuildPipelinePhase.ManualIntervention;
                CurrentState.RollbackState = BuildRollbackState.Failed;
                CurrentState.ErrorMessage =
                    $"设置恢复失败，需要人工处理：{exception.Message}";
                CurrentState.UpdatedAt = NowIso();
                persistence.SaveState(CurrentState);
                report.AppendLine(
                    $"[错误] {CurrentState.ErrorMessage}；状态已保留，请检查项目设置后重试或作废。");
                FinalResult = new BuildRunResult(
                    false,
                    false,
                    report.ToString(),
                    CurrentState);
            }
        }

        /// <summary>
        /// 判断产物是否真实落盘：Windows/Android 为文件，
        /// iOS/WebGL 等目录产物为目录。
        /// </summary>
        /// <param name="outputPath">构建报告给出的产物路径。</param>
        /// <returns>文件或目录存在时返回 true。</returns>
        private static bool ProductAlreadyBuilt(string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath))
            {
                return false;
            }

            return File.Exists(outputPath) || Directory.Exists(outputPath);
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
        /// 从全部已注册步骤中收集第三方构建开关控制器。
        /// 不能只检查 Recipe 已选步骤，否则无法关闭未选插件自身的全局回调。
        /// </summary>
        private static IReadOnlyList<IBuildIntegrationActivationController>
            GetIntegrationActivationControllers(
                IReadOnlyList<IBuildPipelineStep> steps)
        {
            List<IBuildIntegrationActivationController> result =
                new List<IBuildIntegrationActivationController>();
            if (steps == null)
            {
                return result;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] is IBuildIntegrationActivationController controller)
                {
                    result.Add(controller);
                }
            }
            return result;
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
        /// <param name="effectiveProfile">已应用任务覆盖的内存副本。</param>
        /// <param name="context">构建上下文。</param>
        /// <param name="usable">可用步骤列表。</param>
        /// <param name="taskId">预生成的任务 Id。</param>
        /// <returns>初始状态。</returns>
        private static BuildPipelineState CreateState(
            UnityRFrameworkBuildProfile profile,
            UnityRFrameworkBuildProfile effectiveProfile,
            BuildPipelineContext context,
            List<IBuildPipelineStep> usable,
            string taskId,
            BuildTaskOverrides taskOverrides)
        {
            BuildPipelineState state = new BuildPipelineState
            {
                TaskId = taskId,
                ProfileGuid = AssetDatabase.AssetPathToGUID(
                    AssetDatabase.GetAssetPath(profile)),
                ProfileAssetPath = AssetDatabase.GetAssetPath(profile),
                ProfileName = profile.name,
                TargetName = effectiveProfile.Platform.Target.ToString(),
                FlavorName = effectiveProfile.Flavor.ToString(),
                Recipe = context.Recipe,
                TaskOverrides = taskOverrides,
                PublicVersion = effectiveProfile.Platform.PublicVersion,
                BuildNumber = effectiveProfile.Platform.BuildNumber,
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
        /// 组装并写入构建报告；成功、失败与取消均会写报告，保证失败构建
        /// 有可定位的步骤与原因。Release 报告失败由调用方阻断成功，其他 Recipe 记录警告。
        /// </summary>
        /// <param name="succeeded">任务是否成功完成。</param>
        /// <param name="cancelled">任务是否被取消。</param>
        private bool WriteExecutionReport(
            bool succeeded,
            bool cancelled,
            out string error)
        {
            error = string.Empty;
            try
            {
                BuildExecutionReport reportModel = BuildExecutionReport.Create(
                    Context,
                    CurrentState,
                    succeeded,
                    cancelled);
                ResolveExecutionReportDestination(
                    out string reportRoot,
                    out string reportDirectory);
                string reportPath = reportWriter(
                    reportModel,
                    reportRoot,
                    reportDirectory,
                    CurrentState.TaskId);
                Debug.Log($"构建报告已写入：{reportPath}");
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                Debug.LogWarning($"构建报告写入失败：{exception.Message}");
                return false;
            }
        }

        /// <summary>
        /// 解析执行报告目录。Assets 与 HotUpdate 不产生 Player，报告写入工程
        /// Bundles/BuildReports/{创建时间}_{Recipe}；Player 与 Release 跟随 Player 产物目录。
        /// </summary>
        private void ResolveExecutionReportDestination(
            out string outputRoot,
            out string outputDirectory)
        {
            if (Context.Recipe == BuildRecipe.Assets
                || Context.Recipe == BuildRecipe.HotUpdate)
            {
                outputRoot = Path.GetFullPath(Path.Combine(
                    Context.ProjectRoot,
                    "Bundles"));
                outputDirectory =
                    BuildReportWriter.GetAssetOnlyReportDirectory(CurrentState);
                return;
            }

            outputRoot = CurrentState.OutputRootAbsolute;
            outputDirectory = CurrentState.OutputDirectory;
        }

        /// <summary>
        /// 提交构建号自动递增；异常只警告，不阻断任务收尾。
        /// </summary>
        private void CommitBuildNumberSafely()
        {
            if (CurrentState.TaskOverrides != null
                && CurrentState.TaskOverrides.HasBuildNumber)
            {
                return;
            }

            try
            {
                BuildVersionResolver.CommitBuildNumber(sourceProfile);
            }
            catch (Exception exception)
            {
                Debug.Log($"构建号自动递增失败：{exception.Message}");
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
