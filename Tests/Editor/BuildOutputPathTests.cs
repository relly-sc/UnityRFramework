using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// 阶段 7 输出目录、版本与构建报告测试。
    /// 覆盖四维目录隔离校验、路径解析、版本展示与构建号提交、
    /// 报告组装与脱敏、报告写入与中文摘要。
    /// </summary>
    public sealed class BuildOutputPathTests
    {
        /// <summary>测试用工程根目录绝对路径。</summary>
        private static string ProjectRoot =>
            Path.GetDirectoryName(Application.dataPath) ?? string.Empty;

        /// <summary>测试用任务 Id。</summary>
        private const string TestTaskId = "test-task-0001";

        /// <summary>
        /// 创建一份填充了合法标识与平台的测试 Profile，并从项目资产库加载第一个场景。
        /// 报告组装依赖场景列表与平台参数，测试必须提供完整 Profile。
        /// </summary>
        /// <returns>处于合法状态的测试 Profile。</returns>
        private static UnityRFrameworkBuildProfile CreateValidProfile()
        {
            UnityRFrameworkBuildProfile profile =
                ScriptableObject.CreateInstance<UnityRFrameworkBuildProfile>();
            profile.name = "TestProfile";
            profile.Platform.CompanyName = "TestCompany";
            profile.Platform.ProductName = "TestProduct";
            profile.Platform.ApplicationIdentifier = "com.test.product";
            profile.Platform.PublicVersion = "1.0.0";
            profile.Platform.BuildNumber = 12;
            profile.Platform.ScriptingBackend = ScriptingImplementation.IL2CPP;
            profile.Platform.Target = BuildTarget.StandaloneWindows64;

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
        /// 创建携带合法 Profile 的构建上下文；输出解析使用默认模板（含四维占位符）。
        /// </summary>
        /// <returns>构建上下文。</returns>
        private static BuildPipelineContext CreateContext()
        {
            return BuildPipelineContext.Create(
                CreateValidProfile(),
                null,
                default);
        }

        /// <summary>
        /// 创建包含两个已完成步骤与一个失败步骤的任务状态，时刻使用 ISO 8601 格式。
        /// </summary>
        /// <param name="profileOutputPath">步骤输出路径，可为空。</param>
        /// <param name="errorText">失败步骤的异常文本，可为空。</param>
        /// <returns>任务状态。</returns>
        private static BuildPipelineState CreateState(
            string profileOutputPath,
            string errorText)
        {
            BuildPipelineState state = new BuildPipelineState
            {
                TaskId = TestTaskId,
                PublicVersion = "1.0.0",
                BuildNumber = 12,
                CreatedAt = new DateTime(
                    2026, 8, 19, 10, 0, 0, DateTimeKind.Local).ToString("o"),
                UpdatedAt = new DateTime(
                    2026, 8, 19, 10, 0, 30, DateTimeKind.Local).ToString("o"),
                ErrorMessage = string.Empty,
                Phase = BuildPipelinePhase.Failed
            };

            state.CompletedSteps.Add(new BuildStepRecord
            {
                StepId = "step.alpha",
                Status = BuildStepStatus.Succeeded.ToString(),
                Message = "第一步完成",
                OutputPath = profileOutputPath,
                ExceptionText = string.Empty,
                StartedAt = new DateTime(
                    2026, 8, 19, 10, 0, 1, DateTimeKind.Local).ToString("o"),
                FinishedAt = new DateTime(
                    2026, 8, 19, 10, 0, 5, DateTimeKind.Local).ToString("o")
            });

            state.FailedStep = new BuildStepRecord
            {
                StepId = "step.beta",
                Status = BuildStepStatus.Failed.ToString(),
                Message = "第二步失败",
                OutputPath = string.Empty,
                ExceptionText = errorText,
                StartedAt = new DateTime(
                    2026, 8, 19, 10, 0, 6, DateTimeKind.Local).ToString("o"),
                FinishedAt = new DateTime(
                    2026, 8, 19, 10, 0, 8, DateTimeKind.Local).ToString("o")
            };

            return state;
        }

        // ---- BuildOutputPathResolver.ResolvePlayerDirectoryAbsolute ----

        /// <summary>
        /// 正常输入时输出根目录与输出目录应组合为完整绝对路径。
        /// 比较前统一分隔符，避免 Windows 反斜杠与模板斜杠的差异。
        /// </summary>
        [Test]
        public void Resolve_NormalContext_ReturnsCombinedAbsolutePath()
        {
            BuildPipelineContext context = CreateContext();

            string resolved = BuildOutputPathResolver
                .ResolvePlayerDirectoryAbsolute(context);

            Assert.That(resolved, Is.Not.Empty);
            Assert.That(Path.IsPathRooted(resolved), Is.True);
            Assert.That(
                resolved.Replace('\\', '/'),
                Does.Contain(context.OutputDirectory),
                "解析结果应包含输出目录。");
        }

        /// <summary>
        /// null 上下文应返回空字符串。
        /// </summary>
        [Test]
        public void Resolve_NullContext_ReturnsEmpty()
        {
            string resolved = BuildOutputPathResolver
                .ResolvePlayerDirectoryAbsolute(null);

            Assert.That(resolved, Is.EqualTo(string.Empty));
        }

        /// <summary>
        /// 输出根目录为空时应返回空字符串。
        /// </summary>
        [Test]
        public void Resolve_EmptyOutputRoot_ReturnsEmpty()
        {
            BuildPipelineContext context = new BuildPipelineContext(
                CreateValidProfile(),
                BuildTarget.StandaloneWindows64,
                ProjectRoot,
                string.Empty,
                "Dir",
                "File",
                new Dictionary<string, IBuildPipelineStep>(),
                default,
                DateTime.Now,
                string.Empty);

            string resolved = BuildOutputPathResolver
                .ResolvePlayerDirectoryAbsolute(context);

            Assert.That(resolved, Is.EqualTo(string.Empty));
        }

        /// <summary>
        /// 输出目录为空时应返回空字符串。
        /// </summary>
        [Test]
        public void Resolve_EmptyOutputDirectory_ReturnsEmpty()
        {
            BuildPipelineContext context = new BuildPipelineContext(
                CreateValidProfile(),
                BuildTarget.StandaloneWindows64,
                ProjectRoot,
                Path.Combine(ProjectRoot, "Builds"),
                string.Empty,
                "File",
                new Dictionary<string, IBuildPipelineStep>(),
                default,
                DateTime.Now,
                string.Empty);

            string resolved = BuildOutputPathResolver
                .ResolvePlayerDirectoryAbsolute(context);

            Assert.That(resolved, Is.EqualTo(string.Empty));
        }

        // ---- BuildOutputPathResolver.ValidateIsolation ----

        /// <summary>
        /// null Profile 校验隔离应返回空列表，不抛异常。
        /// </summary>
        [Test]
        public void ValidateIsolation_NullProfile_ReturnsEmpty()
        {
            List<BuildValidationIssue> issues =
                BuildOutputPathResolver.ValidateIsolation(null);

            Assert.That(issues, Is.Empty);
        }

        /// <summary>
        /// Profile 输出配置为空时校验隔离应返回空列表。
        /// </summary>
        [Test]
        public void ValidateIsolation_NullOutput_ReturnsEmpty()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();
            profile.Output = null;

            List<BuildValidationIssue> issues =
                BuildOutputPathResolver.ValidateIsolation(profile);

            Assert.That(issues, Is.Empty);
        }

        /// <summary>
        /// 输出目录模板为空时校验应报 Error。
        /// </summary>
        [Test]
        public void ValidateIsolation_EmptyTemplate_ReportsError()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();
            profile.Output.DirectoryTemplate = "  ";

            List<BuildValidationIssue> issues =
                BuildOutputPathResolver.ValidateIsolation(profile);

            Assert.That(issues, Is.Not.Empty);
            Assert.That(
                issues,
                Has.Some.Property("Level").EqualTo(
                    BuildValidationLevel.Error));
        }

        /// <summary>
        /// 模板缺少 Profile 占位符时应报 Error。
        /// </summary>
        [Test]
        public void ValidateIsolation_MissingProfile_ReportsError()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();
            profile.Output.DirectoryTemplate =
                "{Platform}/{ScriptBackend}/{Version}-{BuildNumber}";

            List<BuildValidationIssue> issues =
                BuildOutputPathResolver.ValidateIsolation(profile);

            Assert.That(issues, Is.Not.Empty);
            Assert.That(
                issues,
                Has.Some.Property("Message").Contains(
                    BuildOutputSettings.ProfilePlaceholder));
        }

        /// <summary>
        /// 模板缺少 Platform 占位符时应报 Error。
        /// </summary>
        [Test]
        public void ValidateIsolation_MissingPlatform_ReportsError()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();
            profile.Output.DirectoryTemplate =
                "{Profile}/{ScriptBackend}/{Version}-{BuildNumber}";

            List<BuildValidationIssue> issues =
                BuildOutputPathResolver.ValidateIsolation(profile);

            Assert.That(issues, Is.Not.Empty);
            Assert.That(
                issues,
                Has.Some.Property("Message").Contains(
                    BuildOutputSettings.PlatformPlaceholder));
        }

        /// <summary>
        /// 模板缺少 ScriptBackend 占位符时应报 Error，且描述包含脚本后端隔离风险。
        /// </summary>
        [Test]
        public void ValidateIsolation_MissingScriptBackend_ReportsError()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();
            profile.Output.DirectoryTemplate =
                "{Profile}/{Platform}/{Version}-{BuildNumber}";

            List<BuildValidationIssue> issues =
                BuildOutputPathResolver.ValidateIsolation(profile);

            Assert.That(issues, Is.Not.Empty);
            Assert.That(
                issues,
                Has.Some.Property("Message").Contains(
                    BuildOutputSettings.ScriptBackendPlaceholder));
            Assert.That(
                issues,
                Has.Some.Property("Message").Contains("Mono 与 IL2CPP"));
        }

        /// <summary>
        /// 模板缺少 Version 占位符时应报 Error。
        /// </summary>
        [Test]
        public void ValidateIsolation_MissingVersion_ReportsError()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();
            profile.Output.DirectoryTemplate =
                "{Profile}/{Platform}/{ScriptBackend}/{BuildNumber}";

            List<BuildValidationIssue> issues =
                BuildOutputPathResolver.ValidateIsolation(profile);

            Assert.That(issues, Is.Not.Empty);
            Assert.That(
                issues,
                Has.Some.Property("Message").Contains(
                    BuildOutputSettings.VersionPlaceholder));
        }

        /// <summary>
        /// 模板包含全部四个隔离占位符时应无问题。
        /// </summary>
        [Test]
        public void ValidateIsolation_FullTemplate_ReturnsEmpty()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();
            profile.Output.DirectoryTemplate =
                "{Profile}/{Platform}/{ScriptBackend}/{Version}-{BuildNumber}";

            List<BuildValidationIssue> issues =
                BuildOutputPathResolver.ValidateIsolation(profile);

            Assert.That(issues, Is.Empty);
        }

        // ---- BuildVersionResolver ----

        /// <summary>
        /// null Profile 解析展示版本应返回空字符串。
        /// </summary>
        [Test]
        public void ResolveDisplayVersion_NullProfile_ReturnsEmpty()
        {
            string version = BuildVersionResolver
                .ResolveDisplayVersion(null);

            Assert.That(version, Is.EqualTo(string.Empty));
        }

        /// <summary>
        /// 合法 Profile 应返回公共版本与构建号的组合文本。
        /// </summary>
        [Test]
        public void ResolveDisplayVersion_ValidProfile_ReturnsVersionText()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();

            string version = BuildVersionResolver
                .ResolveDisplayVersion(profile);

            Assert.That(version, Is.EqualTo("1.0.0 (Build 12)"));
        }

        /// <summary>
        /// 未开启自动递增时提交构建号不应改变当前值。
        /// </summary>
        [Test]
        public void CommitBuildNumber_Disabled_KeepsBuildNumber()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();
            profile.Platform.AutoIncrementBuildNumber = false;

            BuildVersionResolver.CommitBuildNumber(profile, false);

            Assert.That(profile.Platform.BuildNumber, Is.EqualTo(12));
        }

        /// <summary>
        /// 开启自动递增且不持久化时提交构建号应递增内存值。
        /// 持久化跳过保证测试不触碰磁盘资产。
        /// </summary>
        [Test]
        public void CommitBuildNumber_EnabledNoPersist_IncrementsBuildNumber()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();

            BuildVersionResolver.CommitBuildNumber(profile, false);

            Assert.That(profile.Platform.BuildNumber, Is.EqualTo(13));
        }

        // ---- BuildExecutionReport.Create ----

        /// <summary>
        /// 上下文与状态均为 null 时仍应生成基础报告，不抛异常。
        /// </summary>
        [Test]
        public void Create_NullContextAndState_ReturnsBaseReport()
        {
            BuildExecutionReport report = BuildExecutionReport.Create(
                null,
                null,
                false,
                false);

            Assert.That(report, Is.Not.Null);
            Assert.That(report.SchemaVersion, Is.EqualTo(
                BuildExecutionReport.CurrentSchemaVersion));
            Assert.That(report.UnityVersion, Is.Not.Empty);
            Assert.That(report.Succeeded, Is.False);
            Assert.That(report.Cancelled, Is.False);
        }

        /// <summary>
        /// 合法上下文与状态应填充 Profile、输出与步骤字段。
        /// </summary>
        [Test]
        public void Create_ValidInputs_FillsProfileAndStateFields()
        {
            BuildPipelineContext context = CreateContext();
            BuildPipelineState state = CreateState(string.Empty, string.Empty);

            BuildExecutionReport report = BuildExecutionReport.Create(
                context,
                state,
                false,
                false);

            Assert.That(report.TaskId, Is.EqualTo(TestTaskId));
            Assert.That(report.ProfileName, Is.EqualTo("TestProfile"));
            Assert.That(report.ProfileFlavor, Is.EqualTo(
                BuildProfileFlavor.Release.ToString()));
            Assert.That(report.TargetPlatform, Is.EqualTo(
                BuildTarget.StandaloneWindows64.ToString()));
            Assert.That(report.ScriptBackend, Is.EqualTo(
                ScriptingImplementation.IL2CPP.ToString()));
            Assert.That(report.PublicVersion, Is.EqualTo("1.0.0"));
            Assert.That(report.BuildNumber, Is.EqualTo(12));
            Assert.That(report.OutputDirectory, Is.Not.Empty);
            Assert.That(report.PlayerOutputPath, Is.Not.Empty);
            Assert.That(report.Steps.Count, Is.EqualTo(2),
                "已完成步骤与失败步骤都应出现在报告中。");
            Assert.That(report.Steps[0].StepId, Is.EqualTo("step.alpha"));
            Assert.That(report.Steps[0].Status, Is.EqualTo(
                BuildStepStatus.Succeeded.ToString()));
            Assert.That(report.Steps[1].StepId, Is.EqualTo("step.beta"));
            Assert.That(report.DurationSeconds, Is.GreaterThan(0.0));
        }

        /// <summary>
        /// 失败步骤应追加在已完成步骤之后，并携带脱敏异常文本。
        /// </summary>
        [Test]
        public void Create_FailedStep_AppendedAfterCompletedSteps()
        {
            BuildPipelineContext context = CreateContext();
            BuildPipelineState state = CreateState(
                string.Empty,
                "模拟异常：构建失败。");

            BuildExecutionReport report = BuildExecutionReport.Create(
                context,
                state,
                false,
                false);

            Assert.That(report.Steps.Count, Is.EqualTo(2));
            Assert.That(report.Steps[0].StepId, Is.EqualTo("step.alpha"));
            Assert.That(report.Steps[1].StepId, Is.EqualTo("step.beta"));
            Assert.That(report.Steps[1].Error, Does.Contain("模拟异常"));
        }

        /// <summary>
        /// 步骤输出路径应映射到报告并经脱敏处理（用户主目录替换为波浪号）。
        /// </summary>
        [Test]
        public void Create_StepOutputPath_RedactedIntoReport()
        {
            string userProfile = Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile);
            string rawOutput = Path.Combine(
                userProfile,
                "Builds",
                "TestProfile");

            BuildPipelineContext context = CreateContext();
            BuildPipelineState state = CreateState(rawOutput, string.Empty);

            BuildExecutionReport report = BuildExecutionReport.Create(
                context,
                state,
                true,
                false);

            string stepOutput = report.Steps[0].OutputPath;
            Assert.That(stepOutput, Does.StartWith("~/Builds/TestProfile"));
            Assert.That(stepOutput, Does.Not.Contain(userProfile),
                "报告不得包含完整本机用户目录。");
        }

        /// <summary>
        /// 第三方摘要探测不应抛异常，且固定输出四个集成条目。
        /// </summary>
        [Test]
        public void Create_ThirdPartySummary_NoThrowAndExpectedCount()
        {
            BuildExecutionReport report = BuildExecutionReport.Create(
                CreateContext(),
                CreateState(string.Empty, string.Empty),
                true,
                false);

            Assert.That(report.ThirdParty.Count, Is.EqualTo(4));
            for (int i = 0; i < report.ThirdParty.Count; i++)
            {
                Assert.That(report.ThirdParty[i].Name, Is.Not.Empty);
            }
        }

        /// <summary>
        /// 超长异常文本应被截断，报告体积受控。
        /// </summary>
        [Test]
        public void Create_LongException_TruncatedInReport()
        {
            string longError = new string('e', 5000);
            BuildPipelineContext context = CreateContext();
            BuildPipelineState state = CreateState(string.Empty, longError);

            BuildExecutionReport report = BuildExecutionReport.Create(
                context,
                state,
                false,
                false);

            string error = report.Steps[1].Error;
            Assert.That(error.Length, Is.LessThanOrEqualTo(2100));
            Assert.That(error, Does.EndWith("（截断）"));
        }

        // ---- BuildExecutionReport.RedactPath / RedactText ----

        /// <summary>
        /// 空路径脱敏应返回空字符串。
        /// </summary>
        [Test]
        public void RedactPath_Empty_ReturnsEmpty()
        {
            Assert.That(BuildExecutionReport.RedactPath(null),
                Is.EqualTo(string.Empty));
            Assert.That(BuildExecutionReport.RedactPath(string.Empty),
                Is.EqualTo(string.Empty));
        }

        /// <summary>
        /// 用户主目录前缀应替换为波浪号，且分隔符统一为斜杠。
        /// </summary>
        [Test]
        public void RedactPath_UserProfilePrefix_ReplacedWithTilde()
        {
            string userProfile = Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile);
            string raw = userProfile + @"\AppData\Local\Temp\x.txt";

            string redacted = BuildExecutionReport.RedactPath(raw);

            Assert.That(redacted, Does.StartWith("~/AppData/Local/Temp/x.txt"));
            Assert.That(redacted, Does.Not.Contain("\\"));
            Assert.That(redacted, Does.Not.Contain(userProfile));
        }

        /// <summary>
        /// 文本中的用户主目录（正斜杠与反斜杠两种形式）应替换为波浪号。
        /// </summary>
        [Test]
        public void RedactText_UserProfileForwardAndBackslash_ReplacedWithTilde()
        {
            string userProfile = Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile);
            string forward = userProfile.Replace('\\', '/');
            string text = $"读取配置失败：{forward}/Config/a.json "
                + $"与 {userProfile}\\Config\\b.json 均不可用。";

            string redacted = BuildExecutionReport.RedactText(text);

            Assert.That(redacted, Does.Not.Contain(userProfile));
            Assert.That(redacted, Does.Not.Contain(forward));
            Assert.That(
                redacted,
                Does.Contain("~/Config/a.json"));
            Assert.That(
                redacted,
                Does.Contain("~\\Config\\b.json"),
                "反斜杠形式替换后剩余部分应原样保留反斜杠。");
        }

        /// <summary>
        /// 不包含用户主目录的文本应原样返回。
        /// </summary>
        [Test]
        public void RedactText_NoUserProfile_Unchanged()
        {
            string text = "普通的错误消息，不包含本机路径。";

            string redacted = BuildExecutionReport.RedactText(text);

            Assert.That(redacted, Is.EqualTo(text));
        }

        // ---- BuildReportWriter ----

        /// <summary>
        /// 输出目录可用时应写入 JSON 报告，且可反序列化回报告模型。
        /// </summary>
        [Test]
        public void Write_ValidOutput_WritesJsonFileRoundTrip()
        {
            string tempRoot = Path.Combine(
                Path.GetTempPath(),
                "URFReport_" + Guid.NewGuid().ToString("N"));
            try
            {
                BuildExecutionReport report = BuildExecutionReport.Create(
                    CreateContext(),
                    CreateState(string.Empty, string.Empty),
                    true,
                    false);

                string filePath = BuildReportWriter.Write(
                    report,
                    tempRoot,
                    "Output",
                    TestTaskId);

                Assert.That(File.Exists(filePath), Is.True);
                Assert.That(
                    Path.GetFileName(filePath),
                    Is.EqualTo(BuildReportWriter.ReportFileName));

                string json = File.ReadAllText(filePath);
                BuildExecutionReport loaded =
                    JsonUtility.FromJson<BuildExecutionReport>(json);
                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded.TaskId, Is.EqualTo(TestTaskId));
                Assert.That(loaded.Succeeded, Is.True);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        /// <summary>
        /// 输出目录缺失时应回退写入工程 Library 回退目录，保证失败构建仍有报告。
        /// </summary>
        [Test]
        public void Write_MissingOutput_FallsBackToLibrary()
        {
            string taskId = "urf-fallback-" + Guid.NewGuid().ToString("N");
            BuildExecutionReport report = BuildExecutionReport.Create(
                CreateContext(),
                CreateState(string.Empty, string.Empty),
                false,
                false);

            string filePath = BuildReportWriter.Write(
                report,
                string.Empty,
                string.Empty,
                taskId);

            try
            {
                string normalized = filePath.Replace('\\', '/');
                string expected = "Library/UnityRFramework/"
                    + BuildReportWriter.FallbackDirectoryName + "/" + taskId
                    + "/" + BuildReportWriter.ReportFileName;
                Assert.That(
                    normalized,
                    Does.EndWith(expected),
                    "回退路径应符合 Library/UnityRFramework/BuildReports/{任务 Id}/build-report.json。");
                Assert.That(File.Exists(filePath), Is.True);
            }
            finally
            {
                string fallbackDir = Path.Combine(
                    ProjectRoot,
                    "Library",
                    "UnityRFramework",
                    BuildReportWriter.FallbackDirectoryName,
                    taskId);
                if (Directory.Exists(fallbackDir))
                {
                    Directory.Delete(fallbackDir, true);
                }
            }
        }

        /// <summary>
        /// null 报告生成中文摘要应返回占位文本。
        /// </summary>
        [Test]
        public void BuildTextSummary_NullReport_ReturnsPlaceholder()
        {
            string summary = BuildReportWriter.BuildTextSummary(null);

            Assert.That(summary, Is.EqualTo("构建报告为空。"));
        }

        /// <summary>
        /// 有效报告的中文摘要应包含结果、平台、版本与步骤等关键字段。
        /// </summary>
        [Test]
        public void BuildTextSummary_ValidReport_ContainsKeyFields()
        {
            BuildPipelineContext context = CreateContext();
            BuildPipelineState state = CreateState(string.Empty, string.Empty);

            BuildExecutionReport report = BuildExecutionReport.Create(
                context,
                state,
                true,
                false);

            string summary = BuildReportWriter.BuildTextSummary(report);

            Assert.That(summary, Does.Contain("构建报告：TestProfile"));
            Assert.That(summary, Does.Contain("结果：成功"));
            Assert.That(summary, Does.Contain("StandaloneWindows64"));
            Assert.That(summary, Does.Contain("版本：1.0.0"));
            Assert.That(summary, Does.Contain("step.alpha"));
            Assert.That(summary, Does.Contain("step.beta"));
        }
    }
}
