using System;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建任务恢复决策结果。
    /// </summary>
    public enum BuildRecoveryAction
    {
        /// <summary>无可恢复任务，无事发生。</summary>
        None,

        /// <summary>自动继续任务（同进程 Domain Reload 安全场景，会话 TaskId 匹配）。</summary>
        Resume,

        /// <summary>提示用户选择继续（或重试失败步骤）或作废（GUI 模式）。</summary>
        Prompt,

        /// <summary>直接作废任务（BatchMode 或明确失败）。</summary>
        Abandon,

        /// <summary>另一 Unity 进程正持有该任务的锁且进程存活，本进程不得干预。</summary>
        Busy,

        /// <summary>清理已结束任务的残留状态与锁。</summary>
        Cleanup
    }

    /// <summary>
    /// 构建任务恢复决策与启动检查。
    /// 决策规则（<see cref="Evaluate"/> 纯函数）：
    /// - 已终态任务（成功、作废）清理残留状态与锁；
    /// - 失败、取消与人工处理任务保留状态，交由用户重试、继续或作废；
    /// - 同进程（会话标记的 TaskId 与状态一致）自动恢复，等待编译与资源导入结束后续跑；
    /// - 跨进程时验证锁：持锁进程存活且工程匹配则本进程不干预（Busy）；
    ///   锁残留（进程已退出、工程不匹配、锁缺失或与状态 TaskId 不一致）时
    ///   GUI 提示用户选择，BatchMode 直接作废；
    /// - 损坏状态不自动吞掉：GUI 提示作废或保留，BatchMode 作废并记录错误。
    /// </summary>
    public static class BuildPipelineRecovery
    {
        /// <summary>
        /// SessionState 键：保存当前进程活动构建任务的 TaskId。
        /// SessionState 在 Domain Reload 后保留、进程重启后清空；
        /// 恢复前必须与状态文件中的 TaskId 匹配，不匹配按跨进程残留处理。
        /// </summary>
        public const string SessionStateKey =
            "UnityRFramework.BuildPipeline.ActiveTaskId";

        /// <summary>
        /// 标记当前进程存在活动构建任务，并记录任务 Id。
        /// </summary>
        /// <param name="taskId">活动任务 Id；为空时按清除处理。</param>
        public static void MarkActiveInSession(string taskId)
        {
            SessionState.SetString(SessionStateKey, taskId ?? string.Empty);
        }

        /// <summary>
        /// 清除当前进程的活动任务标记。
        /// </summary>
        public static void ClearSessionMark()
        {
            SessionState.SetString(SessionStateKey, string.Empty);
        }

        /// <summary>
        /// 仅当会话标记记录的是指定任务时清除标记，
        /// 避免清理其他任务的标记。
        /// </summary>
        /// <param name="taskId">待比对的任务 Id。</param>
        public static void ClearSessionMarkIfMatches(string taskId)
        {
            if (string.Equals(GetActiveTaskId(), taskId, StringComparison.Ordinal))
            {
                ClearSessionMark();
            }
        }

        /// <summary>
        /// 获取当前进程会话标记中保存的活动任务 Id；无标记时返回空字符串。
        /// </summary>
        /// <returns>活动任务 Id，无标记时为空字符串。</returns>
        public static string GetActiveTaskId()
        {
            return SessionState.GetString(SessionStateKey, string.Empty) ?? string.Empty;
        }

        /// <summary>
        /// 当前进程会话标记中是否存在活动构建任务（Domain Reload 后仍为 true）。
        /// </summary>
        /// <returns>存在时返回 true。</returns>
        public static bool IsActiveInSession()
        {
            return GetActiveTaskId().Length > 0;
        }

        /// <summary>
        /// 根据任务状态、会话标记与锁信息计算恢复动作（纯函数，可单测）。
        /// 锁信息为空表示锁文件缺失或不可读，按残留处理。
        /// </summary>
        /// <param name="state">任务状态；为空时返回 None。</param>
        /// <param name="sessionTaskId">当前进程会话标记中的任务 Id；无标记为空字符串。</param>
        /// <param name="lockInfo">锁文件内容；缺失或不可读时为空。</param>
        /// <param name="lockHolderAlive">持锁进程是否仍存活。</param>
        /// <param name="lockProjectMatches">锁记录的工程标识是否匹配当前工程。</param>
        /// <param name="batchMode">是否 BatchMode。</param>
        /// <returns>恢复动作。</returns>
        public static BuildRecoveryAction Evaluate(
            BuildPipelineState state,
            string sessionTaskId,
            BuildPipelineLockInfo lockInfo,
            bool lockHolderAlive,
            bool lockProjectMatches,
            bool batchMode)
        {
            if (state == null)
            {
                return BuildRecoveryAction.None;
            }

            if (state.IsTerminal)
            {
                return BuildRecoveryAction.Cleanup;
            }

            if (state.HasEndedRun)
            {
                // 失败、取消与人工处理任务需要用户决策，不自动继续。
                return batchMode
                    ? BuildRecoveryAction.Abandon
                    : BuildRecoveryAction.Prompt;
            }

            bool sameSession =
                !string.IsNullOrEmpty(sessionTaskId)
                && string.Equals(sessionTaskId, state.TaskId, StringComparison.Ordinal);
            if (sameSession)
            {
                return BuildRecoveryAction.Resume;
            }

            if (lockInfo == null)
            {
                // 无锁（缺失或不可读）的跨进程活动任务：按残留处理。
                return batchMode
                    ? BuildRecoveryAction.Abandon
                    : BuildRecoveryAction.Prompt;
            }

            if (!string.Equals(lockInfo.TaskId, state.TaskId, StringComparison.Ordinal)
                || !lockProjectMatches
                || !lockHolderAlive)
            {
                // 锁与状态不一致、锁来自其他工程或持锁进程已退出：
                // 残留锁，恢复时先接管；不永久锁死工具。
                return batchMode
                    ? BuildRecoveryAction.Abandon
                    : BuildRecoveryAction.Prompt;
            }

            return BuildRecoveryAction.Busy;
        }

        /// <summary>
        /// 启动检查入口：由 <see cref="BuildPipelineRecoveryInitializer"/> 在
        /// 编辑器启动与脚本重载后调用。当前进程存在活动任务时跳过（任务正在执行）。
        /// </summary>
        public static void CheckAndRecover()
        {
            if (BuildPipelineRunner.HasActiveTask)
            {
                return;
            }

            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence();
            BuildPipelineStateLoadResult loadResult;
            BuildPipelineState state;
            try
            {
                loadResult = persistence.LoadStateDetailed(out state);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"构建任务状态读取失败，已跳过恢复：{exception.Message}");
                return;
            }

            if (loadResult == BuildPipelineStateLoadResult.Missing)
            {
                if (persistence.HasLock)
                {
                    // 状态缺失但锁残留：无法恢复任务，作废锁避免永久占用。
                    AbandonTask(null, persistence);
                }
                return;
            }

            if (loadResult == BuildPipelineStateLoadResult.Corrupt
                || loadResult == BuildPipelineStateLoadResult.VersionMismatch)
            {
                HandleUnreadableState(loadResult, persistence);
                return;
            }

            persistence.ReadLockInfo(out BuildPipelineLockInfo lockInfo);
            BuildRecoveryAction action = Evaluate(
                state,
                GetActiveTaskId(),
                lockInfo,
                lockInfo != null && lockInfo.IsHolderProcessAlive(),
                lockInfo != null
                    && lockInfo.MatchesProject(BuildPipelinePersistence.GetProjectIdentity()),
                Application.isBatchMode);
            switch (action)
            {
                case BuildRecoveryAction.Resume:
                    WaitUntilIdleAndResume(state, persistence);
                    break;
                case BuildRecoveryAction.Prompt:
                    PromptAndResolve(state, persistence);
                    break;
                case BuildRecoveryAction.Abandon:
                    AbandonTask(state, persistence);
                    break;
                case BuildRecoveryAction.Busy:
                    Debug.Log(
                        $"构建任务（{state.TaskId}）正由其他 Unity 进程执行，"
                        + "本进程不干预。");
                    break;
                case BuildRecoveryAction.Cleanup:
                    Cleanup(state, persistence);
                    break;
            }
        }

        /// <summary>
        /// 作废任务：公开入口，供窗口与命令行使用。
        /// 任务状态标记为作废并记录原因日志，随后释放锁、删除状态与备份并清除活动标记。
        /// 状态为空时仅清理锁文件（如锁残留但状态已丢失）。
        /// </summary>
        /// <param name="state">待作废的任务状态，可为空。</param>
        /// <param name="persistence">持久化实例。</param>
        public static void AbandonTask(
            BuildPipelineState state,
            BuildPipelinePersistence persistence)
        {
            if (persistence == null)
            {
                throw new ArgumentNullException(nameof(persistence));
            }

            if (state != null)
            {
                string reason = string.IsNullOrWhiteSpace(state.ErrorMessage)
                    ? string.Empty
                    : $"，原因：{state.ErrorMessage}";
                Debug.LogError(
                    $"构建任务「{state.ProfileName}」（{state.TaskId}）已作废{reason}");
            }
            else
            {
                Debug.LogError("构建任务状态已丢失，仅清理残留锁。");
            }

            persistence.ReleaseLock();
            persistence.DeleteState();
            BuildPipelineRunner.ClearActive();
        }

        /// <summary>
        /// 处理无法读取的任务状态：不自动吞掉，
        /// GUI 提示用户选择作废或保留，BatchMode 直接作废并记录错误。
        /// </summary>
        /// <param name="loadResult">状态加载结果（损坏或版本不识别）。</param>
        /// <param name="persistence">持久化实例。</param>
        private static void HandleUnreadableState(
            BuildPipelineStateLoadResult loadResult,
            BuildPipelinePersistence persistence)
        {
            string reason = loadResult == BuildPipelineStateLoadResult.VersionMismatch
                ? "状态文件版本高于当前实现，无法安全解析"
                : "状态文件损坏且备份不可用";
            if (Application.isBatchMode)
            {
                Debug.LogError($"构建任务{reason}，BatchMode 下直接作废。");
                AbandonTask(null, persistence);
                return;
            }

            bool discard = EditorUtility.DisplayDialog(
                "构建任务状态无法读取",
                $"{reason}。\n\n选择「作废」将删除状态文件并释放锁；"
                + "选择「保留」将保持现状，构建工具在问题解决前无法启动新任务。",
                "作废",
                "保留");
            if (discard)
            {
                AbandonTask(null, persistence);
            }
            else
            {
                Debug.LogError($"构建任务{reason}，已按用户选择保留现场。");
            }
        }

        /// <summary>
        /// 等待编译与资源导入结束后恢复任务。
        /// </summary>
        /// <param name="state">任务状态。</param>
        /// <param name="persistence">持久化实例。</param>
        private static void WaitUntilIdleAndResume(
            BuildPipelineState state,
            BuildPipelinePersistence persistence)
        {
            void PollAndResume()
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    return;
                }
                EditorApplication.update -= PollAndResume;
                ResumeTask(state, persistence);
            }
            EditorApplication.update += PollAndResume;
        }

        /// <summary>
        /// 弹窗提示用户选择继续（或重试失败步骤）或作废。
        /// </summary>
        /// <param name="state">任务状态。</param>
        /// <param name="persistence">持久化实例。</param>
        private static void PromptAndResolve(
            BuildPipelineState state,
            BuildPipelinePersistence persistence)
        {
            string title;
            string confirmLabel;
            if (state.Phase == BuildPipelinePhase.Failed)
            {
                title = "发现失败的构建任务";
                confirmLabel = "重试失败步骤";
            }
            else if (state.Phase == BuildPipelinePhase.WaitingForEditor)
            {
                title = "发现等待中的构建任务";
                confirmLabel = "继续";
            }
            else
            {
                title = "发现未完成的构建任务";
                confirmLabel = "恢复";
            }

            bool resume = EditorUtility.DisplayDialog(
                title,
                $"构建配置「{state.ProfileName}」的任务（{state.TaskId}）尚未完成，"
                + $"已完成 {state.CompletedSteps.Count} / {state.StepIds.Count} 个步骤。\n"
                + (string.IsNullOrWhiteSpace(state.ErrorMessage)
                    ? string.Empty
                    : $"原因：{state.ErrorMessage}\n")
                + "是否从检查点继续？",
                confirmLabel,
                "作废");
            if (resume)
            {
                WaitUntilIdleAndResume(state, persistence);
            }
            else
            {
                AbandonTask(state, persistence);
            }
        }

        /// <summary>
        /// 清理已终态任务的残留状态与锁，并清除本进程活动标记。
        /// </summary>
        /// <param name="state">任务状态。</param>
        /// <param name="persistence">持久化实例。</param>
        private static void Cleanup(
            BuildPipelineState state,
            BuildPipelinePersistence persistence)
        {
            persistence.ReleaseLock();
            persistence.DeleteState();
            BuildPipelineRunner.ClearActive();
        }

        /// <summary>
        /// 执行恢复续跑：启动内核后由 EditorApplication.update 驱动，
        /// BatchMode 下任务终态自动退出进程；恢复失败时作废任务。
        /// </summary>
        /// <param name="state">任务状态。</param>
        /// <param name="persistence">持久化实例。</param>
        private static void ResumeTask(
            BuildPipelineState state,
            BuildPipelinePersistence persistence)
        {
            try
            {
                BuildPipelineRunner runner = BuildPipelineRunner.Resume(
                    state,
                    persistence.RootDirectory);
                UnityRFrameworkBuildCommand.AttachBatchExit(runner);
                Debug.Log(
                    $"构建任务「{state.ProfileName}」已从检查点恢复，"
                    + $"继续执行步骤 {state.CurrentStepIndex + 1}/{state.StepIds.Count}。");
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"构建任务「{state.ProfileName}」恢复失败：{exception.Message}");
                AbandonTask(state, persistence);
            }
        }
    }

    /// <summary>
    /// 编辑器启动与脚本重载后触发构建任务恢复检查。
    /// </summary>
    [InitializeOnLoad]
    public static class BuildPipelineRecoveryInitializer
    {
        /// <summary>
        /// 注册延迟恢复检查。
        /// </summary>
        static BuildPipelineRecoveryInitializer()
        {
            EditorApplication.delayCall += BuildPipelineRecovery.CheckAndRecover;
        }
    }
}
