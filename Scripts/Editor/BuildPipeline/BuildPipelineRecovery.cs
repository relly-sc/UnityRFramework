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

        /// <summary>自动继续任务（同进程 Domain Reload 安全场景）。</summary>
        Resume,

        /// <summary>提示用户选择恢复或作废（GUI 模式）。</summary>
        Prompt,

        /// <summary>直接作废任务（BatchMode 或明确失败）。</summary>
        Abandon,

        /// <summary>清理已结束任务的残留状态与锁。</summary>
        Cleanup
    }

    /// <summary>
    /// 构建任务恢复决策与启动检查。
    /// 决策规则：
    /// - 已完成任务清理残留状态与锁；
    /// - 同进程（SessionState 标记保留）自动恢复，等待编译与资源导入结束后续跑；
    /// - 跨进程 GUI 提示用户选择恢复或作废，不自动继续可能有副作用的构建；
    /// - 跨进程 BatchMode 直接作废，不等待人工确认。
    /// </summary>
    public static class BuildPipelineRecovery
    {
        /// <summary>SessionState 键：标记当前进程存在活动构建任务（Domain Reload 后保留）。</summary>
        public const string SessionStateKey =
            "UnityRFramework.BuildPipeline.ActiveTask";

        /// <summary>
        /// 标记当前进程存在活动构建任务。
        /// SessionState 在 Domain Reload 后保留、进程重启后清空，用于区分两种恢复场景。
        /// </summary>
        public static void MarkActiveInSession()
        {
            SessionState.SetBool(SessionStateKey, true);
        }

        /// <summary>
        /// 清除当前进程的活动任务标记。
        /// </summary>
        public static void ClearSessionMark()
        {
            SessionState.SetBool(SessionStateKey, false);
        }

        /// <summary>
        /// 当前进程是否存在活动构建任务（Domain Reload 后仍为 true）。
        /// </summary>
        /// <returns>存在时返回 true。</returns>
        public static bool IsActiveInSession()
        {
            return SessionState.GetBool(SessionStateKey, false);
        }

        /// <summary>
        /// 根据任务状态与运行模式计算恢复动作（纯函数，可单测）。
        /// </summary>
        /// <param name="state">任务状态；为空时返回 None。</param>
        /// <param name="isSameProcess">是否同一进程（SessionState 标记）。</param>
        /// <param name="batchMode">是否 BatchMode。</param>
        /// <returns>恢复动作。</returns>
        public static BuildRecoveryAction Evaluate(
            BuildPipelineState state,
            bool isSameProcess,
            bool batchMode)
        {
            if (state == null)
            {
                return BuildRecoveryAction.None;
            }
            if (state.IsFinished)
            {
                return BuildRecoveryAction.Cleanup;
            }
            if (isSameProcess)
            {
                return BuildRecoveryAction.Resume;
            }
            return batchMode
                ? BuildRecoveryAction.Abandon
                : BuildRecoveryAction.Prompt;
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
            BuildPipelineState state;
            try
            {
                state = persistence.LoadState();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"构建任务状态读取失败，已跳过恢复：{exception.Message}");
                return;
            }
            if (state == null)
            {
                return;
            }

            BuildRecoveryAction action = Evaluate(
                state,
                IsActiveInSession(),
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
                    Abandon(state, persistence);
                    break;
                case BuildRecoveryAction.Cleanup:
                    Cleanup(state, persistence);
                    break;
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
        /// 弹窗提示用户选择恢复或作废。
        /// </summary>
        /// <param name="state">任务状态。</param>
        /// <param name="persistence">持久化实例。</param>
        private static void PromptAndResolve(
            BuildPipelineState state,
            BuildPipelinePersistence persistence)
        {
            bool resume = EditorUtility.DisplayDialog(
                "发现未完成的构建任务",
                $"构建配置「{state.ProfileName}」的任务（{state.TaskId}）尚未完成，"
                + $"已完成 {state.CompletedSteps.Count} / {state.StepIds.Count} 个步骤。\n"
                + "是否从上次检查点继续？",
                "恢复",
                "作废");
            if (resume)
            {
                WaitUntilIdleAndResume(state, persistence);
            }
            else
            {
                Abandon(state, persistence);
            }
        }

        /// <summary>
        /// 作废任务：记录错误日志并清理残留。
        /// </summary>
        /// <param name="state">任务状态。</param>
        /// <param name="persistence">持久化实例。</param>
        private static void Abandon(
            BuildPipelineState state,
            BuildPipelinePersistence persistence)
        {
            string reason = string.IsNullOrWhiteSpace(state.ErrorMessage)
                ? string.Empty
                : $"，原因：{state.ErrorMessage}";
            Debug.LogError(
                $"构建任务「{state.ProfileName}」（{state.TaskId}）已作废{reason}");
            Cleanup(state, persistence);
        }

        /// <summary>
        /// 清理残留状态与锁，并清除活动标记。
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
        /// 执行恢复续跑；恢复失败时作废任务。
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
                BuildRunResult result = runner.Execute();
                if (result.Succeeded)
                {
                    Debug.Log(
                        $"构建任务「{state.ProfileName}」恢复并成功完成。");
                }
                else if (result.Cancelled)
                {
                    Debug.Log(
                        $"构建任务「{state.ProfileName}」恢复后被取消。");
                }
                else
                {
                    Debug.LogError(
                        $"构建任务「{state.ProfileName}」恢复后失败。");
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"构建任务「{state.ProfileName}」恢复失败：{exception.Message}");
                Abandon(state, persistence);
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
