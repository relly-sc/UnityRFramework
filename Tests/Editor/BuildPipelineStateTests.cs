using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Process = System.Diagnostics.Process;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// 阶段 2 状态、锁与持久化专项测试：
    /// 状态往返与版本拒绝、备份恢复、损坏文件、锁身份与陈旧锁判定、
    /// 恢复决策矩阵、失败/取消任务保留状态、等待编辑器与重复启动。
    /// 全部用例使用临时持久化目录与内存假步骤，不触碰真实 Library 状态与构建管线。
    /// </summary>
    public class BuildPipelineStateTests
    {
        /// <summary>临时持久化根目录。</summary>
        private string tempRoot;

        /// <summary>测试用最小 Profile。</summary>
        private UnityRFrameworkBuildProfile profile;

        /// <summary>
        /// 每个用例前的初始化：创建临时目录、内存 Profile 并清空会话标记。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            tempRoot = Path.Combine(
                Path.GetTempPath(),
                "URFBuildStateTests_" + Guid.NewGuid().ToString("N"));
            profile = ScriptableObject.CreateInstance<UnityRFrameworkBuildProfile>();
            profile.name = "TestProfile";
            profile.Platform.Target = BuildTarget.StandaloneWindows64;
            BuildPipelineRunner.ClearActive();
        }

        /// <summary>
        /// 每个用例后的清理：清除活动标记并删除临时目录。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            BuildPipelineRunner.ClearActive();
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }

        /// <summary>
        /// 测试假步骤：记录执行次数，返回预设结果，可标记设置事务生效。
        /// </summary>
        private sealed class RecordingStep : BuildPipelineStepBase
        {
            private readonly string id;
            private readonly BuildStepResult result;

            /// <summary>步骤实际执行次数。</summary>
            public int ExecuteCount;

            /// <summary>是否在执行时标记设置事务生效（模拟"应用参数"步骤）。</summary>
            public bool MarksTransaction;

            /// <summary>
            /// 创建假步骤。
            /// </summary>
            /// <param name="id">步骤 Id。</param>
            /// <param name="result">预设返回结果；为空时返回成功。</param>
            public RecordingStep(string id, BuildStepResult result = null)
            {
                this.id = id;
                this.result = result ?? BuildStepResult.Succeeded("ok");
            }

            /// <summary>步骤唯一 Id。</summary>
            public override string Id
            {
                get { return id; }
            }

            /// <summary>执行步骤：记录次数、按需标记事务并返回预设结果。</summary>
            public override BuildStepResult Execute(BuildPipelineContext context)
            {
                ExecuteCount++;
                if (MarksTransaction)
                {
                    context.SettingsTransaction?.MarkApplied();
                }

                return result;
            }
        }

        /// <summary>
        /// 测试假步骤：首次执行失败，之后成功，用于重试语义验证。
        /// </summary>
        private sealed class FlakyStep : BuildPipelineStepBase
        {
            private readonly string id;

            /// <summary>步骤实际执行次数。</summary>
            public int ExecuteCount;

            /// <summary>
            /// 创建假步骤。
            /// </summary>
            /// <param name="id">步骤 Id。</param>
            public FlakyStep(string id)
            {
                this.id = id;
            }

            /// <summary>步骤唯一 Id。</summary>
            public override string Id
            {
                get { return id; }
            }

            /// <summary>执行步骤：首次失败，之后成功。</summary>
            public override BuildStepResult Execute(BuildPipelineContext context)
            {
                ExecuteCount++;
                return ExecuteCount == 1
                    ? BuildStepResult.Failed("首次执行失败", null)
                    : BuildStepResult.Succeeded("重试成功");
            }
        }

        // ==================== 状态与版本契约 ====================

        /// <summary>
        /// 状态保存与加载往返一致，包含实际 Recipe、任务覆盖与恢复字段。
        /// </summary>
        [Test]
        public void Persistence_StateRoundTrip_PreservesData()
        {
            BuildPipelineState state = new BuildPipelineState
            {
                TaskId = "roundtrip",
                ProfileName = "P",
                TargetName = "StandaloneWindows64",
                FlavorName = "Release",
                Phase = BuildPipelinePhase.WaitingForEditor,
                WaitingReason = "等待脚本编译",
                RollbackState = BuildRollbackState.Pending,
                TaskOverrides = new BuildTaskOverrides
                {
                    HasBuildNumber = true,
                    BuildNumber = 42
                },
                StepIds = new List<string> { "a", "b" },
                CurrentStepIndex = 1,
                CreatedAt = DateTime.Now.ToString("o"),
                UpdatedAt = DateTime.Now.ToString("o")
            };
            state.CompletedSteps.Add(
                new BuildStepRecord
                {
                    StepId = "a",
                    Status = BuildStepStatus.Succeeded.ToString(),
                    Message = "ok",
                    StartedAt = "2026-08-29T00:00:00",
                    FinishedAt = "2026-08-29T00:00:01"
                });

            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence(tempRoot);
            persistence.SaveState(state);

            Assert.That(
                persistence.LoadStateDetailed(out BuildPipelineState loaded),
                Is.EqualTo(BuildPipelineStateLoadResult.Success));
            Assert.That(loaded.TaskId, Is.EqualTo("roundtrip"));
            Assert.That(
                loaded.SerializedVersion,
                Is.EqualTo(BuildPipelineState.CurrentSerializedVersion));
            Assert.That(loaded.Phase, Is.EqualTo(BuildPipelinePhase.WaitingForEditor));
            Assert.That(loaded.WaitingReason, Is.EqualTo("等待脚本编译"));
            Assert.That(loaded.RollbackState, Is.EqualTo(BuildRollbackState.Pending));
            Assert.That(loaded.CurrentStepIndex, Is.EqualTo(1));
            Assert.That(loaded.StepIds.Count, Is.EqualTo(2));
            Assert.That(loaded.CompletedSteps.Count, Is.EqualTo(1));
            Assert.That(loaded.CompletedSteps[0].StepId, Is.EqualTo("a"));
            Assert.That(loaded.CompletedSteps[0].Message, Is.EqualTo("ok"));
            Assert.That(loaded.TaskOverrides.HasBuildNumber, Is.True);
            Assert.That(loaded.TaskOverrides.BuildNumber, Is.EqualTo(42));
        }

        /// <summary>
        /// 状态文件不存在时加载返回 Missing，便捷方法返回空。
        /// </summary>
        [Test]
        public void Persistence_LoadState_WhenMissing_ReturnsNull()
        {
            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence(tempRoot);
            Assert.That(
                persistence.LoadStateDetailed(out _),
                Is.EqualTo(BuildPipelineStateLoadResult.Missing));
            Assert.That(persistence.LoadState(), Is.Null);
        }

        /// <summary>
        /// 旧版本状态不自动迁移，必须显式作废后重新开始任务。
        /// </summary>
        [Test]
        public void Persistence_LoadOlderVersion_ReturnsVersionMismatch()
        {
            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence(tempRoot);
            Directory.CreateDirectory(tempRoot);
            string legacyJson = new StringBuilder()
                .AppendLine("{")
                .AppendLine("    \"SerializedVersion\": 1,")
                .AppendLine("    \"TaskId\": \"legacy-task\",")
                .AppendLine("    \"ProfileName\": \"LegacyProfile\",")
                .AppendLine("    \"PhaseName\": \"Running\",")
                .AppendLine("    \"StepIds\": [\"a\"],")
                .AppendLine("    \"CurrentStepIndex\": 0")
                .AppendLine("}")
                .ToString();
            File.WriteAllText(
                Path.Combine(tempRoot, "task.json"),
                legacyJson,
                new UTF8Encoding(false));

            Assert.That(
                persistence.LoadStateDetailed(out BuildPipelineState state),
                Is.EqualTo(BuildPipelineStateLoadResult.VersionMismatch));
            Assert.That(state, Is.Null);
        }

        /// <summary>
        /// 状态文件版本高于当前实现时返回 VersionMismatch，不自动解析。
        /// </summary>
        [Test]
        public void Persistence_LoadNewerVersion_ReturnsVersionMismatch()
        {
            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence(tempRoot);
            Directory.CreateDirectory(tempRoot);
            File.WriteAllText(
                Path.Combine(tempRoot, "task.json"),
                "{\"SerializedVersion\": 99, \"TaskId\": \"future\", "
                    + "\"PhaseName\": \"Running\"}",
                new UTF8Encoding(false));

            Assert.That(
                persistence.LoadStateDetailed(out BuildPipelineState state),
                Is.EqualTo(BuildPipelineStateLoadResult.VersionMismatch));
            Assert.That(state, Is.Null);
        }

        /// <summary>
        /// 保存第二次时自动生成备份；主文件损坏后从备份恢复上一份状态。
        /// </summary>
        [Test]
        public void Persistence_CorruptState_FallsBackToBackup()
        {
            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence(tempRoot);
            BuildPipelineState first = new BuildPipelineState
            {
                TaskId = "first-task",
                ProfileName = "P1",
                Phase = BuildPipelinePhase.Running
            };
            persistence.SaveState(first);
            Assert.That(
                File.Exists(Path.Combine(tempRoot, "task.json.bak")),
                Is.False,
                "首次保存不应产生备份文件。");

            BuildPipelineState second = new BuildPipelineState
            {
                TaskId = "second-task",
                ProfileName = "P2",
                Phase = BuildPipelinePhase.Running
            };
            persistence.SaveState(second);
            Assert.That(
                File.Exists(Path.Combine(tempRoot, "task.json.bak")),
                Is.True,
                "第二次保存应将上一份状态写入备份。");

            // 主文件损坏：合法 JSON 但 PhaseName 为空，判定为损坏。
            File.WriteAllText(
                Path.Combine(tempRoot, "task.json"),
                "{\"SerializedVersion\": 2, \"TaskId\": \"broken\", \"PhaseName\": \"\"}",
                new UTF8Encoding(false));

            Assert.That(
                persistence.LoadStateDetailed(out BuildPipelineState restored),
                Is.EqualTo(BuildPipelineStateLoadResult.LoadedFromBackup));
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.TaskId, Is.EqualTo("first-task"));
        }

        /// <summary>
        /// 主文件与备份都不可用时返回 Corrupt，状态保持为空且不自动删除文件。
        /// </summary>
        [Test]
        public void Persistence_CorruptStateWithoutBackup_ReturnsCorrupt()
        {
            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence(tempRoot);
            Directory.CreateDirectory(tempRoot);
            File.WriteAllText(
                Path.Combine(tempRoot, "task.json"),
                "   ",
                new UTF8Encoding(false));

            Assert.That(
                persistence.LoadStateDetailed(out BuildPipelineState state),
                Is.EqualTo(BuildPipelineStateLoadResult.Corrupt));
            Assert.That(state, Is.Null);
            Assert.That(
                File.Exists(Path.Combine(tempRoot, "task.json")),
                Is.True,
                "损坏状态不得被加载流程自动删除。");
        }

        // ==================== 锁与工程标识 ====================

        /// <summary>
        /// 锁文件记录任务 Id、进程 Id、工程标识和创建时间，读取往返一致。
        /// </summary>
        [Test]
        public void Lock_AcquireAndRead_RoundTripsIdentity()
        {
            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence(tempRoot);
            BuildPipelineState state = new BuildPipelineState
            {
                TaskId = "lock-task",
                Phase = BuildPipelinePhase.Running
            };

            Assert.That(persistence.TryAcquireLock(state, out string error), Is.True);
            Assert.That(error, Is.Empty);

            Assert.That(persistence.ReadLockInfo(out BuildPipelineLockInfo info), Is.True);
            Assert.That(info.TaskId, Is.EqualTo("lock-task"));
            Assert.That(info.ProcessId, Is.EqualTo(Process.GetCurrentProcess().Id));
            Assert.That(
                info.ProjectId,
                Is.EqualTo(BuildPipelinePersistence.GetProjectIdentity()));
            Assert.That(info.CreatedAt, Is.Not.Empty);
            Assert.That(
                info.MatchesProject(BuildPipelinePersistence.GetProjectIdentity()),
                Is.True);
            Assert.That(info.IsHolderProcessAlive(), Is.True,
                "当前测试进程即为持锁的 Unity 进程。");
        }

        /// <summary>
        /// 工程标识稳定且不匹配其他工程标识。
        /// </summary>
        [Test]
        public void Lock_ProjectIdentity_StableAndDistinct()
        {
            string identity = BuildPipelinePersistence.GetProjectIdentity();
            Assert.That(identity, Is.Not.Empty);
            Assert.That(identity.Length, Is.EqualTo(8));
            Assert.That(
                BuildPipelinePersistence.GetProjectIdentity(),
                Is.EqualTo(identity));
        }

        /// <summary>
        /// 持锁进程 Id 不存在时判定为不存活（陈旧锁）。
        /// </summary>
        [Test]
        public void Lock_StaleHolderProcess_ReportedNotAlive()
        {
            BuildPipelineLockInfo info = new BuildPipelineLockInfo
            {
                TaskId = "dead-task",
                ProcessId = int.MaxValue,
                ProjectId = BuildPipelinePersistence.GetProjectIdentity()
            };
            Assert.That(info.IsHolderProcessAlive(), Is.False);
        }

        /// <summary>
        /// 锁的工程标识与当前工程不一致时判定为外来锁。
        /// </summary>
        [Test]
        public void Lock_ForeignProject_Mismatches()
        {
            BuildPipelineLockInfo info = new BuildPipelineLockInfo
            {
                TaskId = "foreign-task",
                ProcessId = Process.GetCurrentProcess().Id,
                ProjectId = "00000000"
            };
            Assert.That(
                info.MatchesProject(BuildPipelinePersistence.GetProjectIdentity()),
                Is.False);
        }

        /// <summary>
        /// 已有陈旧锁（持锁进程不存在）时再次获取锁失败，
        /// 错误信息明确说明锁已残留且可安全作废，不会永久锁死工具。
        /// </summary>
        [Test]
        public void Lock_TryAcquire_WithStaleLock_FailsWithAbandonGuidance()
        {
            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence(tempRoot);
            Directory.CreateDirectory(tempRoot);
            string staleLockJson = JsonUtility.ToJson(
                new BuildPipelineLockInfo
                {
                    TaskId = "dead-task",
                    ProcessId = int.MaxValue,
                    ProjectId = BuildPipelinePersistence.GetProjectIdentity(),
                    CreatedAt = DateTime.Now.ToString("o")
                },
                true);
            File.WriteAllText(
                Path.Combine(tempRoot, "task.lock"),
                staleLockJson,
                new UTF8Encoding(false));

            Assert.That(
                persistence.TryAcquireLock(
                    new BuildPipelineState { TaskId = "new-task" },
                    out string error),
                Is.False);
            Assert.That(error, Does.Contain("残留锁"));
        }

        // ==================== 恢复决策矩阵 ====================

        /// <summary>
        /// 恢复决策纯函数覆盖状态、会话 TaskId、锁归属与运行模式的全部组合。
        /// </summary>
        [Test]
        public void Recovery_Evaluate_CoversDecisionMatrix()
        {
            Assert.That(
                BuildPipelineRecovery.Evaluate(
                    null, string.Empty, null, false, false, false),
                Is.EqualTo(BuildRecoveryAction.None));

            // 终态任务一律清理残留。
            BuildPipelineState succeeded = new BuildPipelineState
            {
                TaskId = "t",
                Phase = BuildPipelinePhase.Succeeded
            };
            BuildPipelineState abandoned = new BuildPipelineState
            {
                TaskId = "t",
                Phase = BuildPipelinePhase.Abandoned
            };
            Assert.That(
                BuildPipelineRecovery.Evaluate(
                    succeeded, string.Empty, null, false, false, true),
                Is.EqualTo(BuildRecoveryAction.Cleanup));
            Assert.That(
                BuildPipelineRecovery.Evaluate(
                    abandoned, string.Empty, null, false, false, false),
                Is.EqualTo(BuildRecoveryAction.Cleanup));

            // 失败、取消与人工处理：保留状态交用户决策；BatchMode 直接作废。
            BuildPipelinePhase[] decisionPhases =
            {
                BuildPipelinePhase.Failed,
                BuildPipelinePhase.Cancelled,
                BuildPipelinePhase.ManualIntervention
            };
            foreach (BuildPipelinePhase phase in decisionPhases)
            {
                BuildPipelineState ended = new BuildPipelineState
                {
                    TaskId = "t",
                    Phase = phase
                };
                Assert.That(
                    BuildPipelineRecovery.Evaluate(
                        ended, "t", null, false, false, false),
                    Is.EqualTo(BuildRecoveryAction.Prompt),
                    $"状态 {phase} 在 GUI 下应提示用户决策。");
                Assert.That(
                    BuildPipelineRecovery.Evaluate(
                        ended, "t", null, false, false, true),
                    Is.EqualTo(BuildRecoveryAction.Abandon),
                    $"状态 {phase} 在 BatchMode 下应直接作废。");
            }

            // 运行中任务：会话 TaskId 匹配时同进程自动恢复。
            BuildPipelineState running = new BuildPipelineState
            {
                TaskId = "task-1",
                Phase = BuildPipelinePhase.Running
            };
            Assert.That(
                BuildPipelineRecovery.Evaluate(
                    running, "task-1", null, false, false, false),
                Is.EqualTo(BuildRecoveryAction.Resume));

            // 会话 TaskId 不匹配按跨进程残留处理。
            Assert.That(
                BuildPipelineRecovery.Evaluate(
                    running, "other-task", null, false, false, false),
                Is.EqualTo(BuildRecoveryAction.Prompt));

            // 跨进程：锁缺失视为残留。
            Assert.That(
                BuildPipelineRecovery.Evaluate(
                    running, string.Empty, null, false, false, false),
                Is.EqualTo(BuildRecoveryAction.Prompt));
            Assert.That(
                BuildPipelineRecovery.Evaluate(
                    running, string.Empty, null, false, false, true),
                Is.EqualTo(BuildRecoveryAction.Abandon));

            // 跨进程：锁与状态 TaskId 不一致视为残留。
            BuildPipelineLockInfo otherTaskLock = new BuildPipelineLockInfo
            {
                TaskId = "other-task",
                ProcessId = 1234
            };
            Assert.That(
                BuildPipelineRecovery.Evaluate(
                    running,
                    string.Empty,
                    otherTaskLock,
                    true,
                    true,
                    false),
                Is.EqualTo(BuildRecoveryAction.Prompt));

            // 跨进程：锁来自其他工程或持锁进程已退出，均为残留锁。
            BuildPipelineLockInfo foreignLock = new BuildPipelineLockInfo
            {
                TaskId = "task-1",
                ProcessId = 1234,
                ProjectId = "00000000"
            };
            Assert.That(
                BuildPipelineRecovery.Evaluate(
                    running, string.Empty, foreignLock, true, false, false),
                Is.EqualTo(BuildRecoveryAction.Prompt));

            BuildPipelineLockInfo deadLock = new BuildPipelineLockInfo
            {
                TaskId = "task-1",
                ProcessId = int.MaxValue,
                ProjectId = BuildPipelinePersistence.GetProjectIdentity()
            };
            Assert.That(
                BuildPipelineRecovery.Evaluate(
                    running, string.Empty, deadLock, false, true, false),
                Is.EqualTo(BuildRecoveryAction.Prompt));

            // 跨进程：锁完整匹配且持锁进程存活时，本进程不干预。
            BuildPipelineLockInfo liveLock = new BuildPipelineLockInfo
            {
                TaskId = "task-1",
                ProcessId = 1234,
                ProjectId = BuildPipelinePersistence.GetProjectIdentity()
            };
            Assert.That(
                BuildPipelineRecovery.Evaluate(
                    running, string.Empty, liveLock, true, true, false),
                Is.EqualTo(BuildRecoveryAction.Busy));
        }

        /// <summary>
        /// 会话标记保存并读取任务 Id；清除与按任务匹配清除行为正确。
        /// </summary>
        [Test]
        public void Recovery_SessionMark_StoresTaskId()
        {
            Assert.That(BuildPipelineRecovery.GetActiveTaskId(), Is.Empty);
            Assert.That(BuildPipelineRecovery.IsActiveInSession(), Is.False);

            BuildPipelineRecovery.MarkActiveInSession("task-a");
            Assert.That(BuildPipelineRecovery.GetActiveTaskId(), Is.EqualTo("task-a"));
            Assert.That(BuildPipelineRecovery.IsActiveInSession(), Is.True);

            BuildPipelineRecovery.ClearSessionMarkIfMatches("task-b");
            Assert.That(BuildPipelineRecovery.GetActiveTaskId(), Is.EqualTo("task-a"));

            BuildPipelineRecovery.ClearSessionMarkIfMatches("task-a");
            Assert.That(BuildPipelineRecovery.GetActiveTaskId(), Is.Empty);

            BuildPipelineRecovery.MarkActiveInSession("task-a");
            BuildPipelineRecovery.ClearSessionMark();
            Assert.That(BuildPipelineRecovery.GetActiveTaskId(), Is.Empty);
        }

        // ==================== 运行器生命周期契约 ====================

        /// <summary>
        /// 失败任务保留状态与锁，阻止重复启动；作废后清理并允许启动新任务。
        /// </summary>
        [Test]
        public void Runner_Failure_KeepsStateAndLock_UntilAbandoned()
        {
            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence(tempRoot);
            FlakyStep flaky = new FlakyStep("a");
            BuildPipelineRunner runner = BuildPipelineRunner.StartNew(
                profile,
                tempRoot,
                new List<IBuildPipelineStep> { flaky });

            LogAssert.Expect(LogType.Error, new Regex("构建任务开始"));
            BuildRunResult result = runner.Execute();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FinalState.Phase, Is.EqualTo(BuildPipelinePhase.Failed));
            Assert.That(result.FinalState.FailedStep.StepId, Is.EqualTo("a"));
            Assert.That(flaky.ExecuteCount, Is.EqualTo(1));

            // 失败任务保留状态与锁；会话标记不再指向该任务。
            Assert.That(File.Exists(Path.Combine(tempRoot, "task.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(tempRoot, "task.lock")), Is.True);
            Assert.That(BuildPipelineRecovery.GetActiveTaskId(), Is.Empty);
            Assert.That(BuildPipelineRunner.HasActiveTask, Is.False);

            // 残留未完成任务阻止新任务启动。
            Assert.Throws<InvalidOperationException>(() =>
                BuildPipelineRunner.StartNew(
                    profile,
                    tempRoot,
                    new List<IBuildPipelineStep> { new RecordingStep("b") }));

            // 重试语义：恢复后重新执行失败步骤。
            BuildPipelineState failedState = persistence.LoadState();
            BuildPipelineRunner retry = BuildPipelineRunner.Resume(
                failedState,
                tempRoot,
                new List<IBuildPipelineStep> { flaky },
                profile);
            BuildRunResult retryResult = retry.Execute();

            Assert.That(retryResult.Succeeded, Is.True);
            Assert.That(flaky.ExecuteCount, Is.EqualTo(2));
            Assert.That(File.Exists(Path.Combine(tempRoot, "task.json")), Is.False);
            Assert.That(File.Exists(Path.Combine(tempRoot, "task.lock")), Is.False);
        }

        /// <summary>
        /// 失败任务可被明确作废：作废后状态与锁清理，可启动新任务。
        /// </summary>
        [Test]
        public void Runner_Failure_AbandonTask_CleansEverything()
        {
            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence(tempRoot);
            BuildPipelineRunner runner = BuildPipelineRunner.StartNew(
                profile,
                tempRoot,
                new List<IBuildPipelineStep>
                {
                    new RecordingStep(
                        "a",
                        BuildStepResult.Failed("配置错误", null))
                });

            LogAssert.Expect(LogType.Error, new Regex("构建任务开始"));
            BuildRunResult result = runner.Execute();
            Assert.That(result.Succeeded, Is.False);

            LogAssert.Expect(LogType.Error, new Regex("已作废"));
            BuildPipelineRecovery.AbandonTask(
                persistence.LoadState(),
                persistence);

            Assert.That(File.Exists(Path.Combine(tempRoot, "task.json")), Is.False);
            Assert.That(File.Exists(Path.Combine(tempRoot, "task.lock")), Is.False);
            Assert.That(persistence.LoadState(), Is.Null);
        }

        /// <summary>
        /// 步骤返回等待编辑器时任务进入等待状态并保留会话标记；
        /// 内核在编辑器空闲稳定后自动续跑并完成剩余步骤。
        /// </summary>
        [Test]
        public void Runner_WaitingForEditor_PersistsThenAutoResumes()
        {
            BuildPipelineRunner runner = BuildPipelineRunner.StartNew(
                profile,
                tempRoot,
                new List<IBuildPipelineStep>
                {
                    new RecordingStep(
                        "a",
                        BuildStepResult.WaitingForEditor("等待脚本编译"))
                });

            // 推进到等待阶段，等待原因与状态应已落盘。
            int guard = 0;
            while (runner.CurrentState.Phase != BuildPipelinePhase.WaitingForEditor
                && guard < 100)
            {
                runner.Tick();
                guard++;
            }

            Assert.That(
                runner.CurrentState.Phase,
                Is.EqualTo(BuildPipelinePhase.WaitingForEditor));
            Assert.That(
                runner.CurrentState.WaitingReason,
                Is.EqualTo("等待脚本编译"));
            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence(tempRoot);
            BuildPipelineState persisted = persistence.LoadState();
            Assert.That(persisted, Is.Not.Null);
            Assert.That(
                persisted.Phase,
                Is.EqualTo(BuildPipelinePhase.WaitingForEditor),
                "等待阶段应落盘 WaitingForEditor 检查点。");
            Assert.That(persisted.WaitingReason, Is.EqualTo("等待脚本编译"));

            // 继续推进：等待阶段在空闲稳定后自动续跑并完成任务。
            BuildRunResult result = PumpUntilFinished(runner);

            Assert.That(result.Succeeded, Is.True,
                "等待编辑器空闲后内核应自动续跑并完成任务。");
            Assert.That(
                result.FinalState.CompletedSteps[0].Status,
                Is.EqualTo(BuildStepStatus.WaitingForEditor.ToString()),
                "等待结果按已完成步骤提交，续跑后不得重复执行。");
        }

        /// <summary>
        /// 等待编辑器阶段请求取消：任务进入取消状态，状态与锁保留，
        /// 会话标记清除，可再次恢复或作废。
        /// </summary>
        [Test]
        public void Runner_CancelDuringWaiting_KeepsStateAndLock()
        {
            BuildPipelineRunner runner = BuildPipelineRunner.StartNew(
                profile,
                tempRoot,
                new List<IBuildPipelineStep>
                {
                    new RecordingStep(
                        "a",
                        BuildStepResult.WaitingForEditor("等待脚本编译"))
                });

            // 推进到等待阶段。
            int guard = 0;
            while (runner.CurrentState.Phase != BuildPipelinePhase.WaitingForEditor
                && guard < 100)
            {
                runner.Tick();
                guard++;
            }

            Assert.That(
                runner.CurrentState.Phase,
                Is.EqualTo(BuildPipelinePhase.WaitingForEditor));
            string waitingTaskId = runner.CurrentState.TaskId;
            Assert.That(
                BuildPipelineRecovery.GetActiveTaskId(),
                Is.EqualTo(waitingTaskId),
                "等待编辑器任务的会话标记必须保留。");

            runner.Cancel();
            runner.Tick();

            Assert.That(runner.IsFinished, Is.True);
            Assert.That(runner.FinalResult.Cancelled, Is.True);
            Assert.That(File.Exists(Path.Combine(tempRoot, "task.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(tempRoot, "task.lock")), Is.True);
            Assert.That(BuildPipelineRecovery.GetActiveTaskId(), Is.Empty);

            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence(tempRoot);
            persistence.ReleaseLock();
            persistence.DeleteState();
        }

        /// <summary>
        /// 任务到达终态后触发 TaskCompleted 事件并携带终态结果。
        /// </summary>
        [Test]
        public void Runner_TaskCompleted_EventFiresWithFinalResult()
        {
            BuildPipelineRunner completed = null;
            BuildPipelineRunner.TaskCompleted += OnCompleted;
            try
            {
                BuildPipelineRunner runner = BuildPipelineRunner.StartNew(
                    profile,
                    tempRoot,
                    new List<IBuildPipelineStep> { new RecordingStep("a") });
                runner.Execute();
            }
            finally
            {
                BuildPipelineRunner.TaskCompleted -= OnCompleted;
            }

            Assert.That(completed, Is.Not.Null);
            Assert.That(completed.FinalResult, Is.Not.Null);
            Assert.That(completed.FinalResult.Succeeded, Is.True);

            void OnCompleted(BuildPipelineRunner runner)
            {
                completed = runner;
            }
        }

        /// <summary>
        /// 运行期 Recipe 覆盖只影响本次任务规划，不修改 Profile 资产的持久化选择。
        /// </summary>
        [Test]
        public void Runner_StartNew_WithRecipeOverride_DoesNotMutateProfile()
        {
            profile.Recipe = BuildRecipe.Release;

            BuildPipelineRunner runner = BuildPipelineRunner.StartNew(
                profile,
                tempRoot,
                new List<IBuildPipelineStep>
                {
                    new RecordingStep("a"),
                    new RecordingStep("b")
                },
                BuildRecipe.Assets);

            Assert.That(profile.Recipe, Is.EqualTo(BuildRecipe.Release),
                "运行期 Recipe 覆盖不得写回 Profile 资产。");
            Assert.That(runner.Execute().Succeeded, Is.True);
        }

        /// <summary>
        /// 没有启用"应用参数"步骤时事务不生效，任务结束后回滚状态为 NotRequired，
        /// 项目设置不被触碰。
        /// </summary>
        [Test]
        public void Runner_WithoutApplyStep_RollbackNotRequired()
        {
            BuildPipelineRunner runner = BuildPipelineRunner.StartNew(
                profile,
                tempRoot,
                new List<IBuildPipelineStep> { new RecordingStep("a") });
            BuildRunResult result = runner.Execute();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(
                result.FinalState.RollbackState,
                Is.EqualTo(BuildRollbackState.NotRequired));
            Assert.That(
                File.Exists(Path.Combine(tempRoot, "settings-snapshot.json")),
                Is.False,
                "成功任务清理后不应残留设置快照文件。");
        }

        /// <summary>
        /// 标记事务生效的任务在结束后恢复设置，回滚状态记为成功；
        /// 当前用例只验证回滚簿记（恢复写回真实项目设置由手动验收覆盖）。
        /// </summary>
        [Test]
        public void Runner_WithAppliedTransaction_RecordsRollbackSucceeded()
        {
            BuildPipelineRunner runner = BuildPipelineRunner.StartNew(
                profile,
                tempRoot,
                new List<IBuildPipelineStep>
                {
                    new RecordingStep("apply", BuildStepResult.Succeeded("ok"))
                    {
                        MarksTransaction = true
                    },
                    new RecordingStep("b")
                });
            BuildRunResult result = runner.Execute();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(
                result.FinalState.RollbackState,
                Is.EqualTo(BuildRollbackState.Succeeded),
                "已生效事务结束后应记录回滚成功。");
        }

        /// <summary>
        /// Player 编译阶段中断判定：失败于构建 Player/收尾，或检查点停在构建 Player
        /// 之前（步骤执行中被杀）的任务判定为已中断；其他中断不误报。
        /// </summary>
        [Test]
        public void Recovery_IsPlayerBuildInterrupted_MatchesOnlyPlayerStage()
        {
            Assert.That(
                BuildPipelineRecovery.IsPlayerBuildInterrupted(null),
                Is.False);

            BuildStepRecord MakeFailedRecord(string stepId)
            {
                return new BuildStepRecord
                {
                    StepId = stepId,
                    Status = BuildStepStatus.Failed.ToString()
                };
            }

            BuildPipelineState failedAtPlayer = new BuildPipelineState
            {
                TaskId = "t",
                Phase = BuildPipelinePhase.Failed,
                FailedStep = MakeFailedRecord("core.build-player")
            };
            Assert.That(
                BuildPipelineRecovery.IsPlayerBuildInterrupted(failedAtPlayer),
                Is.True,
                "失败于构建 Player 的任务应判定为 Player 阶段中断。");

            BuildPipelineState failedAtFinalize = new BuildPipelineState
            {
                TaskId = "t",
                Phase = BuildPipelinePhase.Failed,
                FailedStep = MakeFailedRecord("core.finalize")
            };
            Assert.That(
                BuildPipelineRecovery.IsPlayerBuildInterrupted(failedAtFinalize),
                Is.True,
                "假成功被收尾拦截的任务同样属于 Player 阶段中断。");

            BuildPipelineState failedAtValidate = new BuildPipelineState
            {
                TaskId = "t",
                Phase = BuildPipelinePhase.Failed,
                FailedStep = MakeFailedRecord("core.validate")
            };
            Assert.That(
                BuildPipelineRecovery.IsPlayerBuildInterrupted(failedAtValidate),
                Is.False,
                "校验阶段失败不属于 Player 阶段中断。");

            BuildPipelineState killedDuringPlayer = new BuildPipelineState
            {
                TaskId = "t",
                Phase = BuildPipelinePhase.Running,
                StepIds = new List<string>
                {
                    "core.validate",
                    "core.build-player",
                    "core.finalize"
                },
                CurrentStepIndex = 1
            };
            Assert.That(
                BuildPipelineRecovery.IsPlayerBuildInterrupted(killedDuringPlayer),
                Is.True,
                "检查点停在构建 Player 之前说明进程在步骤执行中被杀。");

            BuildPipelineState killedDuringValidate = new BuildPipelineState
            {
                TaskId = "t",
                Phase = BuildPipelinePhase.Running,
                StepIds = new List<string>
                {
                    "core.validate",
                    "core.build-player",
                    "core.finalize"
                },
                CurrentStepIndex = 0
            };
            Assert.That(
                BuildPipelineRecovery.IsPlayerBuildInterrupted(killedDuringValidate),
                Is.False,
                "检查点停在校验阶段不误报。");

            BuildPipelineState waitingBeforePlayer = new BuildPipelineState
            {
                TaskId = "t",
                Phase = BuildPipelinePhase.WaitingForEditor,
                WaitingReason = "等待编译",
                StepIds = new List<string>
                {
                    "core.validate",
                    "core.apply-profile",
                    "core.build-player",
                    "core.finalize"
                },
                CurrentStepIndex = 2
            };
            Assert.That(
                BuildPipelineRecovery.IsPlayerBuildInterrupted(waitingBeforePlayer),
                Is.False,
                "等待编辑器状态（步骤后正常重载等待）不得误判为 Player 阶段中断。");
        }

        /// <summary>
        /// Player 编译阶段被强制中断的任务禁止恢复/重试：内核恢复入口直接拒绝，
        /// 唯一路径是作废后先用官方构建完整修复。
        /// </summary>
        [Test]
        public void Runner_Resume_PlayerInterruptedState_Throws()
        {
            BuildPipelineState interrupted = new BuildPipelineState
            {
                TaskId = "killed-mid-player",
                Phase = BuildPipelinePhase.Running,
                StepIds = new List<string>
                {
                    "core.validate",
                    "core.build-player",
                    "core.finalize"
                },
                CurrentStepIndex = 1
            };

            Assert.Throws<InvalidOperationException>(() =>
                BuildPipelineRunner.Resume(
                    interrupted,
                    tempRoot,
                    new List<IBuildPipelineStep> { new RecordingStep("core.build-player") },
                    profile));

            // 失败于构建 Player 的终态外状态同样拒绝。
            BuildPipelineState failedAtPlayer = new BuildPipelineState
            {
                TaskId = "failed-at-player",
                Phase = BuildPipelinePhase.Failed,
                StepIds = new List<string>
                {
                    "core.validate",
                    "core.build-player",
                    "core.finalize"
                },
                CurrentStepIndex = 1,
                FailedStep = new BuildStepRecord
                {
                    StepId = "core.build-player",
                    Status = BuildStepStatus.Failed.ToString()
                }
            };
            Assert.Throws<InvalidOperationException>(() =>
                BuildPipelineRunner.Resume(
                    failedAtPlayer,
                    tempRoot,
                    new List<IBuildPipelineStep> { new RecordingStep("core.build-player") },
                    profile));
        }

        /// <summary>
        /// 推进内核直至任务到达终态；超过保护上限时使测试失败。
        /// </summary>
        /// <param name="runner">任务运行器。</param>
        /// <returns>终态结果。</returns>
        private static BuildRunResult PumpUntilFinished(BuildPipelineRunner runner)
        {
            int guard = 0;
            while (!runner.IsFinished && guard < 500)
            {
                runner.Tick();
                guard++;
            }

            Assert.That(runner.IsFinished, Is.True, "内核推进超过测试保护上限。");
            return runner.FinalResult;
        }

        /// <summary>
        /// 存在非终态残留状态时禁止启动新任务，不静默覆盖检查点。
        /// </summary>
        [Test]
        public void Runner_StartNew_WithUnfinishedState_Throws()
        {
            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence(tempRoot);
            persistence.SaveState(new BuildPipelineState
            {
                TaskId = "orphan-task",
                ProfileName = "OldProfile",
                Phase = BuildPipelinePhase.Running
            });

            Assert.Throws<InvalidOperationException>(() =>
                BuildPipelineRunner.StartNew(
                    profile,
                    tempRoot,
                    new List<IBuildPipelineStep> { new RecordingStep("a") }));
        }

        /// <summary>
        /// 存在终态残留状态时允许启动新任务并覆盖残留。
        /// </summary>
        [Test]
        public void Runner_StartNew_WithTerminalState_Succeeds()
        {
            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence(tempRoot);
            persistence.SaveState(new BuildPipelineState
            {
                TaskId = "done-task",
                ProfileName = "OldProfile",
                Phase = BuildPipelinePhase.Succeeded
            });

            BuildPipelineRunner runner = BuildPipelineRunner.StartNew(
                profile,
                tempRoot,
                new List<IBuildPipelineStep> { new RecordingStep("a") });
            Assert.That(runner.Execute().Succeeded, Is.True);
        }

        /// <summary>
        /// 状态文件损坏时启动新任务被阻止，不吞掉损坏状态。
        /// </summary>
        [Test]
        public void Runner_StartNew_WithCorruptState_Throws()
        {
            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence(tempRoot);
            Directory.CreateDirectory(tempRoot);
            File.WriteAllText(
                Path.Combine(tempRoot, "task.json"),
                "   ",
                new UTF8Encoding(false));

            Assert.Throws<InvalidOperationException>(() =>
                BuildPipelineRunner.StartNew(
                    profile,
                    tempRoot,
                    new List<IBuildPipelineStep> { new RecordingStep("a") }));
        }

        /// <summary>
        /// 本进程遗留的锁（失败或等待任务残留）不阻止恢复：恢复时接管锁并继续执行。
        /// 其他存活 Unity 进程持锁的并发拒绝由 Evaluate 的 Busy 分支与
        /// 锁存活判定（Lock_StaleHolderProcess_ReportedNotAlive）共同保证。
        /// </summary>
        [Test]
        public void Runner_Resume_WithOwnProcessLeftoverLock_TakesOver()
        {
            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence(tempRoot);
            BuildPipelineState state = new BuildPipelineState
            {
                TaskId = "leftover-task",
                ProfileAssetPath = string.Empty,
                Phase = BuildPipelinePhase.Running,
                StepIds = new List<string> { "a" }
            };

            // 锁记录当前测试进程（即 Unity Editor 进程），
            // 会话标记为空：恢复按跨进程路径走，但持锁进程是本进程，应放行接管。
            persistence.TryAcquireLock(state, out _);

            BuildPipelineRunner runner = BuildPipelineRunner.Resume(
                state,
                tempRoot,
                new List<IBuildPipelineStep> { new RecordingStep("a") },
                profile);
            Assert.That(runner.Execute().Succeeded, Is.True);
        }

        /// <summary>
        /// 构建号只在完整成功后提交的既有契约不受阶段 2 改动影响。
        /// </summary>
        [Test]
        public void Runner_Failure_DoesNotCommitBuildNumber()
        {
            profile.Platform.PublicVersion = "1.0.0";
            profile.Platform.BuildNumber = 7;
            profile.Platform.AutoIncrementBuildNumber = true;

            BuildPipelineRunner runner = BuildPipelineRunner.StartNew(
                profile,
                tempRoot,
                new List<IBuildPipelineStep>
                {
                    new RecordingStep(
                        "a",
                        BuildStepResult.Failed("失败", null))
                });

            LogAssert.Expect(LogType.Error, new Regex("构建任务开始"));
            BuildRunResult result = runner.Execute();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FinalState.BuildNumber, Is.EqualTo(7));
            Assert.That(profile.Platform.BuildNumber, Is.EqualTo(7),
                "失败任务不得消耗构建号。");
        }
    }
}
