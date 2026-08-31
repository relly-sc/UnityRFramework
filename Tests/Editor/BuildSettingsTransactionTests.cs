using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// 阶段 4 / 阶段 5 可自动化验收项的 EditMode 测试：
    /// 参数应用写入（4.1）、临时设置事务恢复（4.2）、应用失败不改设置（4.3）、
    /// 三种输出策略隔离规则（4.4）、构建失败契约——状态/锁/设置/报告（5.4 核心）。
    /// 不执行真实 Player 构建；产物存在性与"启动退出"仍需脚本化/人工验收。
    /// 全部用例使用临时持久化目录，并在 TearDown 恢复被触碰的项目设置基线。
    /// </summary>
    public class BuildSettingsTransactionTests
    {
        /// <summary>临时持久化/输出根目录。</summary>
        private string tempRoot;

        /// <summary>设置基线：产品名。</summary>
        private string baselineProduct;

        /// <summary>设置基线：脚本后端。</summary>
        private ScriptingImplementation baselineBackend;

        /// <summary>覆盖全部受构建工具影响设置的测试基线事务。</summary>
        private BuildSettingsTransaction baselineTransaction;

        /// <summary>
        /// 初始化：记录项目设置基线并创建临时目录。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            tempRoot = Path.Combine(
                Path.GetTempPath(),
                "URFTransactionTests_" + Guid.NewGuid().ToString("N"));
            baselineProduct = PlayerSettings.productName;
            baselineBackend = PlayerSettings.GetScriptingBackend(
                UnityEditor.Build.NamedBuildTarget.Standalone);
            baselineTransaction = BuildSettingsTransaction.Capture(
                new BuildPipelinePersistence(tempRoot),
                "test-baseline");
            baselineTransaction.MarkApplied();
            BuildPipelineRunner.ClearActive();
        }

        /// <summary>
        /// 清理：恢复项目设置基线、清除活动标记并删除临时目录。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            BuildPipelineRunner.ClearActive();
            baselineTransaction?.Restore();
            baselineTransaction = null;
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }

        /// <summary>
        /// 同一任务在 Domain Reload 后重建事务时，必须从持久化状态
        /// 恢复“已应用临时设置”标记，否则终态会跳过回滚。
        /// </summary>
        [Test]
        public void Transaction_RecaptureSameTask_PreservesAppliedState()
        {
            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence(tempRoot);
            BuildSettingsTransaction first =
                BuildSettingsTransaction.Capture(persistence, "reload-task");
            first.MarkApplied();

            BuildSettingsTransaction restored =
                BuildSettingsTransaction.Capture(persistence, "reload-task");

            Assert.That(restored.HasChanges, Is.True,
                "Domain Reload 后必须知道临时设置已经应用。");
        }

        /// <summary>Android 签名密码只能存在于当前进程内存，不能进入快照 JSON。</summary>
        [Test]
        public void SnapshotJson_DoesNotSerializeSigningPasswords()
        {
            BuildSettingsSnapshot snapshot = new BuildSettingsSnapshot
            {
                AndroidKeystorePass = "keystore-secret-value",
                AndroidKeyaliasPass = "alias-secret-value",
                HasInMemorySigningPasswords = true
            };

            string json = JsonUtility.ToJson(snapshot);

            Assert.That(json, Does.Not.Contain("keystore-secret-value"));
            Assert.That(json, Does.Not.Contain("alias-secret-value"));
            Assert.That(json, Does.Not.Contain("HasInMemorySigningPasswords"));
        }

        /// <summary>
        /// 创建合法测试 Profile：标识取唯一值、后端跟随当前值（避免触发编译）、
        /// 场景引用工程内第一个场景资产。
        /// </summary>
        /// <param name="marker">写入公司/产品名的标记文本。</param>
        /// <returns>处于合法状态的测试 Profile。</returns>
        private UnityRFrameworkBuildProfile CreateValidProfile(string marker)
        {
            UnityRFrameworkBuildProfile profile =
                ScriptableObject.CreateInstance<UnityRFrameworkBuildProfile>();
            profile.name = "BuildProfile";
            profile.Platform.CompanyName = $"TestCompany-{marker}";
            profile.Platform.ProductName = $"TestProduct-{marker}";
            profile.Platform.ApplicationIdentifier = $"com.test.{marker.ToLower()}";
            profile.Platform.PublicVersion = "1.0.0";
            profile.Platform.BuildNumber = 1;

            // 后端跟随当前值：避免应用/恢复触发脚本后端切换相关的编译。
            profile.Platform.ScriptingBackend = baselineBackend;

            string[] sceneGuids = AssetDatabase.FindAssets("t:SceneAsset");
            if (sceneGuids.Length > 0)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[0]);
                profile.Scenes.Add(new BuildSceneEntry
                {
                    Scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath)
                });
            }

            return profile;
        }

        /// <summary>
        /// 测试假步骤：执行时改写产品名并标记设置事务生效（模拟"应用构建参数"）。
        /// </summary>
        private sealed class TransformStep : BuildPipelineStepBase
        {
            private readonly string id;
            private readonly string productName;

            /// <summary>步骤唯一 Id。</summary>
            public override string Id
            {
                get { return id; }
            }

            /// <summary>构造假步骤。</summary>
            public TransformStep(string id, string productName)
            {
                this.id = id;
                this.productName = productName;
            }

            /// <summary>归入 Player 构建阶段，保证 Player Recipe 会选中本步骤。</summary>
            public override BuildPipelineStage Stage => BuildPipelineStage.BuildPlayer;

            /// <summary>执行：写真实设置并标记事务。</summary>
            public override BuildStepResult Execute(BuildPipelineContext context)
            {
                PlayerSettings.productName = productName;
                context.SettingsTransaction?.MarkApplied();
                return BuildStepResult.Succeeded("已改写产品名");
            }
        }

        /// <summary>
        /// 测试假步骤：返回预设结果。
        /// </summary>
        private sealed class RecordingStep : BuildPipelineStepBase
        {
            private readonly string id;
            private readonly BuildStepResult result;

            /// <summary>步骤唯一 Id。</summary>
            public override string Id
            {
                get { return id; }
            }

            /// <summary>构造假步骤。</summary>
            public RecordingStep(string id, BuildStepResult result = null)
            {
                this.id = id;
                this.result = result ?? BuildStepResult.Succeeded("ok");
            }

            /// <summary>归入 Player 构建阶段，保证 Player Recipe 会选中本步骤。</summary>
            public override BuildPipelineStage Stage => BuildPipelineStage.BuildPlayer;

            /// <summary>执行步骤。</summary>
            public override BuildStepResult Execute(BuildPipelineContext context)
            {
                return result;
            }
        }

        // ==================== 4.1 参数应用写入 ====================

        /// <summary>
        /// 主动应用 Profile 后：身份与构建场景写入项目设置并保持（验收 4.1）。
        /// 应用前 Profile 校验必须通过。
        /// </summary>
        [Test]
        public void Applier_Apply_WritesIdentityAndScenesToProject()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile("Apply41");
            profile.Platform.ScriptingBackend = baselineBackend;

            BuildApplyResult result = BuildProfileApplier.Apply(profile);

            Assert.That(result.Succeeded, Is.True,
                "应用失败：" + string.Join("\n", result.Errors));
            Assert.That(
                PlayerSettings.companyName,
                Is.EqualTo(profile.Platform.CompanyName));
            Assert.That(
                PlayerSettings.productName,
                Is.EqualTo(profile.Platform.ProductName));
            Assert.That(
                PlayerSettings.GetApplicationIdentifier(
                    UnityEditor.Build.NamedBuildTarget.Standalone),
                Is.EqualTo(profile.Platform.ApplicationIdentifier));

            List<string> expected = new List<string>();
            foreach (BuildSceneEntry entry in profile.Scenes)
            {
                if (entry != null && entry.IsValidEnabled())
                {
                    expected.Add(entry.ResolvePath());
                }
            }

            Assert.That(EditorBuildSettings.scenes.Length, Is.EqualTo(expected.Count));
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.That(EditorBuildSettings.scenes[i].path, Is.EqualTo(expected[i]));
            }
        }

        // ==================== 4.2 临时事务恢复 ====================

        /// <summary>
        /// 临时设置事务端到端：步骤内真实改写产品名并标记事务，
        /// 任务完成后设置恢复基线，回滚状态记为成功（验收 4.2 核心）。
        /// </summary>
        [Test]
        public void Kernel_AppliedTransaction_RestoresSettingsOnCompletion()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile("Tx42");
            profile.Recipe = BuildRecipe.Player;
            profile.Platform.ScriptingBackend = baselineBackend;

            BuildPipelineRunner runner = BuildPipelineRunner.StartNew(
                profile,
                tempRoot,
                new List<IBuildPipelineStep>
                {
                    new TransformStep("core.apply-profile", profile.Platform.ProductName),
                    new RecordingStep("core.build-player"),
                    new RecordingStep("core.finalize")
                },
                BuildRecipe.Player);
            BuildRunResult result = runner.Execute();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(
                PlayerSettings.productName,
                Is.EqualTo(baselineProduct),
                "任务完成后临时产品名必须恢复基线。");
            Assert.That(
                result.FinalState.RollbackState,
                Is.EqualTo(BuildRollbackState.Succeeded));
            Assert.That(
                File.Exists(Path.Combine(tempRoot, "settings-snapshot.json")),
                Is.False,
                "成功任务清理后不应残留设置快照。");
        }

        // ==================== 4.3 应用失败不改设置 ====================

        /// <summary>
        /// 人为制造应用失败（Release 档场景列表为空）：任务失败、
        /// 项目设置保持不变（验收 4.3）。
        /// </summary>
        [Test]
        public void Kernel_ApplyValidationFailure_KeepsSettingsAndFailsTask()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile("Fail43");
            profile.Flavor = BuildProfileFlavor.Release;
            profile.Scenes.Clear();
            profile.Recipe = BuildRecipe.Player;

            BuildPipelineRunner runner = BuildPipelineRunner.StartNew(
                profile,
                tempRoot,
                new List<IBuildPipelineStep>
                {
                    new ApplyBuildProfileStep(),
                    new RecordingStep("core.build-player"),
                    new RecordingStep("core.finalize")
                },
                BuildRecipe.Player);

            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Error, new Regex("构建任务开始"));
            BuildRunResult result = runner.Execute();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FinalState.Phase, Is.EqualTo(BuildPipelinePhase.Failed));
            Assert.That(
                PlayerSettings.productName,
                Is.EqualTo(baselineProduct),
                "应用失败不得改写产品名。");
            Assert.That(
                result.FinalState.RollbackState,
                Is.EqualTo(BuildRollbackState.Succeeded),
                "已标记事务在失败后仍应执行恢复（本次未写差异，恢复为等值写回）。");
        }

        // ==================== 4.4 输出策略规则 ====================

        /// <summary>
        /// 三种输出策略的隔离维度规则（验收 4.4）：
        /// Versioned 四维强制；Overwrite 仅强制平台与后端；Custom 不强制。
        /// </summary>
        [Test]
        public void OutputStrategy_ValidateIsolation_PerStrategyRules()
        {
            UnityRFrameworkBuildProfile MakeProfile(
                BuildOutputStrategy strategy,
                string template)
            {
                UnityRFrameworkBuildProfile profile =
                    ScriptableObject.CreateInstance<UnityRFrameworkBuildProfile>();
                profile.Output.Strategy = strategy;
                profile.Output.DirectoryTemplate = template;
                return profile;
            }

            // Versioned：缺任一隔离维度即 Error。
            List<BuildValidationIssue> versioned = BuildOutputPathResolver
                .ValidateIsolation(MakeProfile(
                    BuildOutputStrategy.Versioned,
                    "{Profile}/{Platform}/{Version}"));
            Assert.That(versioned, Has.Some.Matches<BuildValidationIssue>(
                issue => issue.Message.Contains("ScriptBackend")));

            // Overwrite：平台与后端必须存在；缺版本不报错。
            List<BuildValidationIssue> overwriteOk = BuildOutputPathResolver
                .ValidateIsolation(MakeProfile(
                    BuildOutputStrategy.Overwrite,
                    "dev/{Platform}/{ScriptBackend}"));
            Assert.That(overwriteOk, Is.Empty);
            List<BuildValidationIssue> overwriteBad = BuildOutputPathResolver
                .ValidateIsolation(MakeProfile(
                    BuildOutputStrategy.Overwrite,
                    "dev"));
            Assert.That(overwriteBad, Is.Not.Empty);

            // Custom：不强制占位符。
            List<BuildValidationIssue> custom = BuildOutputPathResolver
                .ValidateIsolation(MakeProfile(
                    BuildOutputStrategy.Custom,
                    "custom-output"));
            Assert.That(custom, Is.Empty);
        }

        // ==================== 5.4 失败契约核心 ====================

        /// <summary>
        /// 构建失败契约核心（验收 5.4 自动化部分）：失败任务保留状态与锁、
        /// 构建号不消耗、项目设置不变、报告落盘可定位。
        /// </summary>
        [Test]
        public void Kernel_FailureContract_StateLockSettingsReport()
        {
            string outputRoot = Path.Combine(tempRoot, "Builds");
            UnityRFrameworkBuildProfile profile = CreateValidProfile("Fail54");
            profile.Output.OutputRoot = outputRoot;
            profile.Platform.ScriptingBackend = baselineBackend;
            profile.Recipe = BuildRecipe.Player;

            BuildPipelineRunner runner = BuildPipelineRunner.StartNew(
                profile,
                tempRoot,
                new List<IBuildPipelineStep>
                {
                    new RecordingStep(
                        "core.build-player",
                        BuildStepResult.Failed("模拟构建失败", null)),
                    new RecordingStep("core.finalize")
                },
                BuildRecipe.Player);

            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Error, new Regex("构建任务开始"));
            BuildRunResult result = runner.Execute();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FinalState.Phase, Is.EqualTo(BuildPipelinePhase.Failed));

            // 状态与锁按契约保留（失败等待重试或作废）。
            Assert.That(File.Exists(Path.Combine(tempRoot, "task.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(tempRoot, "task.lock")), Is.True);
            Assert.That(BuildPipelineRecovery.GetActiveTaskId(), Is.Empty);

            // 项目设置未被改写。
            Assert.That(PlayerSettings.productName, Is.EqualTo(baselineProduct));

            // 失败构建有可定位报告。
            string reportPath = Path.Combine(
                outputRoot,
                result.FinalState.OutputDirectory,
                "build-report.json");
            Assert.That(File.Exists(reportPath), Is.True,
                $"失败构建应写入报告：{reportPath}");

            BuildPipelinePersistence persistence =
                new BuildPipelinePersistence(tempRoot);
            persistence.ReleaseLock();
            persistence.DeleteState();
        }

    }
}
