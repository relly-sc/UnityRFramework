using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// 阶段 4 可恢复构建流水线内核测试。
    /// 全部用例使用临时持久化目录与内存假步骤，不触碰真实 Library 状态与构建管线。
    /// </summary>
    public class BuildPipelineRunnerTests
    {
        /// <summary>临时持久化根目录。</summary>
        private string tempRoot;

        /// <summary>测试用最小 Profile。</summary>
        private UnityRFrameworkBuildProfile profile;

        /// <summary>
        /// 每个用例前的初始化：创建临时目录与内存 Profile。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            tempRoot = Path.Combine(
                Path.GetTempPath(),
                "URFBuildPipelineTests_" + Guid.NewGuid().ToString("N"));
            profile = ScriptableObject.CreateInstance<UnityRFrameworkBuildProfile>();
            profile.name = "TestProfile";
            profile.Platform.Target = BuildTarget.StandaloneWindows64;
            profile.Output.OutputRoot = tempRoot;
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
        /// 测试假步骤：记录执行次数，返回预设结果，可注入执行回调。
        /// </summary>
        private sealed class RecordingStep : BuildPipelineStepBase
        {
            private readonly string id;
            private readonly int order;
            private readonly BuildStepResult result;
            private readonly Action<BuildPipelineContext> onExecute;
            private readonly BuildPipelineStage stage;

            /// <summary>步骤实际执行次数。</summary>
            public int ExecuteCount;

            /// <summary>
            /// 创建假步骤。
            /// </summary>
            /// <param name="id">步骤 Id。</param>
            /// <param name="order">排序值。</param>
            /// <param name="result">预设返回结果；为空时返回成功。</param>
            /// <param name="onExecute">执行回调，可为空。</param>
            public RecordingStep(
                string id,
                int order = 0,
                BuildStepResult result = null,
                Action<BuildPipelineContext> onExecute = null,
                BuildPipelineStage stage = BuildPipelineStage.PrepareData)
            {
                this.id = id;
                this.order = order;
                this.result = result ?? BuildStepResult.Succeeded("ok");
                this.onExecute = onExecute;
                this.stage = stage;
            }

            /// <summary>步骤所属阶段。</summary>
            public override BuildPipelineStage Stage => stage;

            /// <summary>步骤唯一 Id。</summary>
            public override string Id
            {
                get { return id; }
            }

            /// <summary>排序值。</summary>
            public override int Order
            {
                get { return order; }
            }

            /// <summary>执行步骤：记录次数并触发回调后返回预设结果。</summary>
            public override BuildStepResult Execute(BuildPipelineContext context)
            {
                ExecuteCount++;
                if (onExecute != null)
                {
                    onExecute(context);
                }
                return result;
            }
        }

        /// <summary>
        /// 运行器对注入的乱序步骤按 Order 升序、同 Order 按 Id 字典序执行。
        /// </summary>
        [Test]
        public void Runner_ExecutesStepsInOrder()
        {
            List<string> executed = new List<string>();
            RecordingStep z = new RecordingStep(
                "z", 5, BuildStepResult.Succeeded("ok"),
                context => executed.Add("z"));
            RecordingStep a = new RecordingStep(
                "a", 1, BuildStepResult.Succeeded("ok"),
                context => executed.Add("a"));
            RecordingStep m = new RecordingStep(
                "m", 1, BuildStepResult.Succeeded("ok"),
                context => executed.Add("m"));

            BuildPipelineRunner runner = BuildPipelineRunner.StartNew(
                profile,
                tempRoot,
                new List<IBuildPipelineStep> { z, a, m });
            BuildRunResult result = runner.Execute();

            Assert.That(result.Succeeded, Is.True);
            CollectionAssert.AreEqual(new[] { "a", "m", "z" }, executed);
            Assert.That(result.FinalState.CurrentStepIndex, Is.EqualTo(3));
        }

        /// <summary>
        /// 全部步骤成功后任务进入 Succeeded，且每个步骤只执行一次。
        /// </summary>
        [Test]
        public void Runner_Execute_Success_CompletesAllSteps()
        {
            profile.Platform.PublicVersion = "1.2.0";
            profile.Platform.BuildNumber = 1;
            profile.Platform.AutoIncrementBuildNumber = true;
            RecordingStep a = new RecordingStep("a", 0);
            RecordingStep b = new RecordingStep("b", 1);
            RecordingStep c = new RecordingStep("c", 2);

            BuildPipelineRunner runner = BuildPipelineRunner.StartNew(
                profile,
                tempRoot,
                new List<IBuildPipelineStep> { a, b, c });
            BuildRunResult result = runner.Execute();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Cancelled, Is.False);
            Assert.That(result.FinalState.Phase, Is.EqualTo(BuildPipelinePhase.Succeeded));
            Assert.That(a.ExecuteCount, Is.EqualTo(1));
            Assert.That(b.ExecuteCount, Is.EqualTo(1));
            Assert.That(c.ExecuteCount, Is.EqualTo(1));
            Assert.That(result.FinalState.CompletedSteps.Count, Is.EqualTo(3));
            Assert.That(result.FinalState.PublicVersion, Is.EqualTo("1.2.0"));
            Assert.That(result.FinalState.BuildNumber, Is.EqualTo(1),
                "任务状态必须保留本次产物使用的构建号。");
            Assert.That(profile.Platform.BuildNumber, Is.EqualTo(2),
                "成功后 Profile 应递增为下一次构建号。");
        }

        /// <summary>Assets Recipe 成功不得消耗 Player Build Number。</summary>
        [Test]
        public void Runner_AssetsSuccess_DoesNotIncrementPlayerBuildNumber()
        {
            profile.Recipe = BuildRecipe.Player;
            profile.Platform.BuildNumber = 7;
            profile.Platform.AutoIncrementBuildNumber = true;

            BuildPipelineRunner runner = BuildPipelineRunner.StartNew(
                profile,
                tempRoot,
                new List<IBuildPipelineStep> { new RecordingStep("assets") },
                BuildRecipe.Assets);
            BuildRunResult result = runner.Execute();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(profile.Platform.BuildNumber, Is.EqualTo(7),
                "Assets Recipe 不应递增 Player Build Number。");
        }

        /// <summary>
        /// 命令行覆盖构建号时，步骤读取覆盖值，但源 Profile 计数器不递增。
        /// </summary>
        [Test]
        public void Runner_BuildNumberOverride_DoesNotModifySourceProfile()
        {
            profile.Platform.BuildNumber = 7;
            profile.Platform.AutoIncrementBuildNumber = true;
            int observedBuildNumber = 0;
            BuildTaskOverrides taskOverrides = new BuildTaskOverrides
            {
                HasBuildNumber = true,
                BuildNumber = 99
            };

            BuildPipelineRunner runner = BuildPipelineRunner.StartNew(
                profile,
                tempRoot,
                new List<IBuildPipelineStep>
                {
                    new RecordingStep(
                        "core.build-player",
                        onExecute: context => observedBuildNumber =
                            context.Profile.Platform.BuildNumber,
                        stage: BuildPipelineStage.BuildPlayer)
                },
                BuildRecipe.Player,
                taskOverrides);
            BuildRunResult result = runner.Execute();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(observedBuildNumber, Is.EqualTo(99));
            Assert.That(result.FinalState.BuildNumber, Is.EqualTo(99));
            Assert.That(result.FinalState.TaskOverrides.HasBuildNumber, Is.True);
            Assert.That(profile.Platform.BuildNumber, Is.EqualTo(7));
        }

        /// <summary>
        /// Player/Release 的 Finalize 必须看到本任务成功执行过 BuildPlayer，
        /// 不能把输出目录中的旧文件当成本次构建证据。
        /// </summary>
        [Test]
        public void Runner_FinalizeWithoutCurrentPlayerEvidence_Fails()
        {
            BuildPipelineRunner runner = BuildPipelineRunner.StartNew(
                profile,
                tempRoot,
                new List<IBuildPipelineStep>
                {
                    new RecordingStep(
                        "core.finalize",
                        stage: BuildPipelineStage.Finalize)
                },
                BuildRecipe.Player);

            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex("构建任务开始"));
            BuildRunResult result = runner.Execute();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FinalState.PlayerProduced, Is.False);
            Assert.That(
                result.FinalState.ErrorMessage,
                Does.Contain("本次任务未产生 Player"));
        }

        /// <summary>Release 报告写入失败必须阻断成功并保留失败状态。</summary>
        [Test]
        public void Runner_ReleaseReportWriteFailure_BlocksSuccess()
        {
            BuildPipelineRunner runner = BuildPipelineRunner.StartNew(
                profile,
                tempRoot,
                new List<IBuildPipelineStep>
                {
                    new RecordingStep(
                        "core.build-player",
                        stage: BuildPipelineStage.BuildPlayer),
                    new RecordingStep(
                        "core.finalize",
                        stage: BuildPipelineStage.Finalize)
                },
                BuildRecipe.Release,
                reportWriter: (report, root, directory, taskId) =>
                    throw new IOException("report denied"));

            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("构建报告写入失败"));
            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex("构建任务开始"));
            BuildRunResult result = runner.Execute();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FinalState.Phase, Is.EqualTo(BuildPipelinePhase.Failed));
            Assert.That(result.FinalState.ErrorMessage, Does.Contain("Release 构建报告"));
        }

        /// <summary>Player 报告写入失败记录警告，但不否定已完成的 Player。</summary>
        [Test]
        public void Runner_PlayerReportWriteFailure_IsWarningOnly()
        {
            BuildPipelineRunner runner = BuildPipelineRunner.StartNew(
                profile,
                tempRoot,
                new List<IBuildPipelineStep>
                {
                    new RecordingStep(
                        "core.build-player",
                        stage: BuildPipelineStage.BuildPlayer),
                    new RecordingStep(
                        "core.finalize",
                        stage: BuildPipelineStage.Finalize)
                },
                BuildRecipe.Player,
                reportWriter: (report, root, directory, taskId) =>
                    throw new IOException("report denied"));

            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex("构建报告写入失败"));
            BuildRunResult result = runner.Execute();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.FinalState.Phase, Is.EqualTo(BuildPipelinePhase.Succeeded));
        }

        /// <summary>获取任务锁失败时不得写入设置快照。</summary>
        [Test]
        public void Runner_StartNew_WhenLocked_DoesNotWriteSnapshot()
        {
            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence(tempRoot);
            BuildPipelineState holder = new BuildPipelineState
            {
                TaskId = "holder",
                ProfileName = "Holder"
            };
            Assert.That(persistence.TryAcquireLock(holder, out string error),
                Is.True, error);

            try
            {
                Assert.Throws<InvalidOperationException>(() =>
                    BuildPipelineRunner.StartNew(
                        profile,
                        tempRoot,
                        new List<IBuildPipelineStep>
                        {
                            new RecordingStep("assets")
                        },
                        BuildRecipe.Assets));

                Assert.That(
                    File.Exists(Path.Combine(
                        tempRoot,
                        BuildPipelinePersistence.SnapshotFileName)),
                    Is.False,
                    "未取得锁的任务不得写快照。");
            }
            finally
            {
                persistence.ReleaseLock();
            }
        }

        /// <summary>
        /// 每个步骤执行时检查点已落盘：中途读取状态文件可看到当前索引与已完成记录。
        /// </summary>
        [Test]
        public void Runner_Execute_Success_PersistsCheckpoints()
        {
            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence(tempRoot);
            bool checkpointSeen = false;
            RecordingStep b = new RecordingStep(
                "b",
                1,
                BuildStepResult.Succeeded("ok"),
                context =>
                {
                    BuildPipelineState midState = persistence.LoadState();
                    checkpointSeen = midState != null
                        && midState.CurrentStepIndex == 1
                        && midState.CompletedSteps.Count == 1
                        && midState.CompletedSteps[0].StepId == "a";
                });

            BuildPipelineRunner runner = BuildPipelineRunner.StartNew(
                profile,
                tempRoot,
                new List<IBuildPipelineStep>
                {
                    new RecordingStep("a", 0),
                    b,
                    new RecordingStep("c", 2)
                });
            BuildRunResult result = runner.Execute();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(checkpointSeen, Is.True);
        }

        /// <summary>
        /// 执行中请求取消后，未开始的后续步骤不再执行，任务进入 Cancelled；
        /// 失败与取消任务按契约保留状态与锁等待决策，会话标记同时清除。
        /// </summary>
        [Test]
        public void Runner_Cancel_PreventsFutureSteps()
        {
            BuildPipelineRunner runner = null;
            RecordingStep b = new RecordingStep(
                "b",
                1,
                BuildStepResult.Succeeded("ok"),
                context => runner.Cancel());
            RecordingStep c = new RecordingStep("c", 2);

            runner = BuildPipelineRunner.StartNew(
                profile,
                tempRoot,
                new List<IBuildPipelineStep>
                {
                    new RecordingStep("a", 0),
                    b,
                    c
                });
            BuildRunResult result = runner.Execute();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Cancelled, Is.True);
            Assert.That(result.FinalState.Phase, Is.EqualTo(BuildPipelinePhase.Cancelled));
            Assert.That(c.ExecuteCount, Is.EqualTo(0));

            // 取消任务保留状态与锁，等待继续或作废；会话标记不再指向该任务。
            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence(tempRoot);
            Assert.That(File.Exists(Path.Combine(tempRoot, "task.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(tempRoot, "task.lock")), Is.True);
            Assert.That(BuildPipelineRecovery.GetActiveTaskId(), Is.Empty);

            persistence.ReleaseLock();
            persistence.DeleteState();
        }

        /// <summary>
        /// 已有任务持锁时，再次启动新任务直接失败，不产生第二个任务。
        /// </summary>
        [Test]
        public void Runner_StartNew_WhenLocked_Throws()
        {
            BuildPipelineRunner first = BuildPipelineRunner.StartNew(
                profile,
                tempRoot,
                new List<IBuildPipelineStep> { new RecordingStep("a") });

            Assert.Throws<InvalidOperationException>(() =>
                BuildPipelineRunner.StartNew(
                    profile,
                    tempRoot,
                    new List<IBuildPipelineStep> { new RecordingStep("b") }));

            first.Execute();
        }

        /// <summary>
        /// 任务执行完毕后锁文件与状态文件被清理，活动标记复位，可再次启动新任务。
        /// </summary>
        [Test]
        public void Runner_Finish_ReleasesLockAndCleansState()
        {
            BuildPipelineRunner runner = BuildPipelineRunner.StartNew(
                profile,
                tempRoot,
                new List<IBuildPipelineStep> { new RecordingStep("a") });
            string lockPath = Path.Combine(tempRoot, "task.lock");
            string statePath = Path.Combine(tempRoot, "task.json");

            Assert.That(File.Exists(lockPath), Is.True);
            Assert.That(File.Exists(statePath), Is.True);

            BuildRunResult result = runner.Execute();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(File.Exists(lockPath), Is.False);
            Assert.That(File.Exists(statePath), Is.False);
            Assert.That(BuildPipelineRunner.HasActiveTask, Is.False);

            BuildPipelineRunner again = BuildPipelineRunner.StartNew(
                profile,
                tempRoot,
                new List<IBuildPipelineStep> { new RecordingStep("b") });
            Assert.That(again.Execute().Succeeded, Is.True);
        }

        /// <summary>
        /// 恢复任务时跳过已完成步骤，只执行剩余步骤。
        /// </summary>
        [Test]
        public void Runner_Resume_SkipsCompletedSteps()
        {
            BuildPipelineState state = new BuildPipelineState
            {
                TaskId = "test-task",
                ProfileName = profile.name,
                Phase = BuildPipelinePhase.Running,
                StepIds = new List<string> { "a", "b", "c" },
                CurrentStepIndex = 2
            };
            state.CompletedSteps.Add(
                new BuildStepRecord
                {
                    StepId = "a",
                    Status = BuildStepStatus.Succeeded.ToString()
                });
            state.CompletedSteps.Add(
                new BuildStepRecord
                {
                    StepId = "b",
                    Status = BuildStepStatus.Succeeded.ToString()
                });
            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence(tempRoot);
            persistence.SaveState(state);

            RecordingStep a = new RecordingStep("a", 0);
            RecordingStep b = new RecordingStep("b", 1);
            RecordingStep c = new RecordingStep("c", 2);
            BuildPipelineRunner runner = BuildPipelineRunner.Resume(
                state,
                tempRoot,
                new List<IBuildPipelineStep> { a, b, c },
                profile);
            BuildRunResult result = runner.Execute();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(a.ExecuteCount, Is.EqualTo(0));
            Assert.That(b.ExecuteCount, Is.EqualTo(0));
            Assert.That(c.ExecuteCount, Is.EqualTo(1));
            Assert.That(result.FinalState.CurrentStepIndex, Is.EqualTo(3));
            Assert.That(result.FinalState.CompletedSteps.Count, Is.EqualTo(3));
        }

        /// <summary>
        /// 状态文件不存在时 LoadState 返回空。
        /// </summary>
        [Test]
        public void Persistence_LoadState_WhenMissing_ReturnsNull()
        {
            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence(tempRoot);
            Assert.That(persistence.LoadState(), Is.Null);
        }

        /// <summary>
        /// 状态保存与加载往返一致，字段完整保留。
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
                Phase = BuildPipelinePhase.Running,
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
                    StartedAt = "2026-08-19T00:00:00",
                    FinishedAt = "2026-08-19T00:00:01"
                });

            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence(tempRoot);
            persistence.SaveState(state);

            BuildPipelineState loaded = persistence.LoadState();
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.TaskId, Is.EqualTo("roundtrip"));
            Assert.That(loaded.ProfileName, Is.EqualTo("P"));
            Assert.That(loaded.TargetName, Is.EqualTo("StandaloneWindows64"));
            Assert.That(loaded.FlavorName, Is.EqualTo("Release"));
            Assert.That(loaded.Phase, Is.EqualTo(BuildPipelinePhase.Running));
            Assert.That(loaded.CurrentStepIndex, Is.EqualTo(1));
            Assert.That(loaded.StepIds.Count, Is.EqualTo(2));
            Assert.That(loaded.CompletedSteps.Count, Is.EqualTo(1));
            Assert.That(loaded.CompletedSteps[0].StepId, Is.EqualTo("a"));
            Assert.That(loaded.CompletedSteps[0].Message, Is.EqualTo("ok"));
        }

        /// <summary>
        /// 注册表缓存失效后重新扫描不抛异常，返回列表非空。
        /// </summary>
        [Test]
        public void Registry_InvalidateAndGetAll_NoThrow()
        {
            BuildPipelineStepRegistry.InvalidateCache();
            IReadOnlyList<IBuildPipelineStep> steps =
                BuildPipelineStepRegistry.GetAll();
            Assert.That(steps, Is.Not.Null);
        }
    }
}
