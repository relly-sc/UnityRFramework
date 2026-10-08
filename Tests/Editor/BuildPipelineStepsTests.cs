using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// 阶段 5 步骤与选项工厂测试。
    /// 覆盖 BuildPlayerOptionsFactory 的选项组装、输出路径解析与互斥校验，
    /// 以及核心步骤的可用性与取消/幂等路径；不触发真实 Player 构建。
    /// </summary>
    public class BuildPipelineStepsTests
    {
        /// <summary>创建一个内存构建配置，默认 Windows 平台。</summary>
        private static UnityRFrameworkBuildProfile CreateProfile()
        {
            UnityRFrameworkBuildProfile profile =
                ScriptableObject.CreateInstance<UnityRFrameworkBuildProfile>();
            profile.name = "TestProfile";
            profile.Platform.CompanyName = "TestCompany";
            profile.Platform.ProductName = "TestProduct";
            profile.Platform.ApplicationIdentifier = "com.test.product";
            profile.Platform.PublicVersion = "1.0.0";
            profile.Platform.Target = BuildTarget.StandaloneWindows64;
            return profile;
        }

        /// <summary>用指定配置与取消令牌创建构建上下文。</summary>
        private static BuildPipelineContext CreateContext(
            UnityRFrameworkBuildProfile profile,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return BuildPipelineContext.Create(
                profile,
                new Dictionary<string, IBuildPipelineStep>(),
                cancellationToken);
        }

        /// <summary>无 Profile 的上下文：输出字段为空、OutputError 为空。</summary>
        private static BuildPipelineContext CreateEmptyContext()
        {
            return BuildPipelineContext.Create(
                null,
                new Dictionary<string, IBuildPipelineStep>(),
                CancellationToken.None);
        }

        [Test]
        public void Factory_Create_WithoutProfile_Throws()
        {
            BuildPipelineContext context = CreateEmptyContext();

            Assert.Throws<System.InvalidOperationException>(
                () => BuildPlayerOptionsFactory.Create(context));
        }

        [Test]
        public void Factory_Create_WithOutputError_Throws()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile();
            profile.Output.OutputRoot = "Assets";

            BuildPipelineContext context = CreateContext(profile);
            Assert.IsNotEmpty(context.OutputError);

            Assert.Throws<System.InvalidOperationException>(
                () => BuildPlayerOptionsFactory.Create(context));
        }

        [Test]
        public void Factory_ResolveOptions_NoDevelopmentBuild_ReturnsNone()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile();
            profile.Platform.DevelopmentBuild = false;
            profile.Platform.ScriptDebugging = true;

            BuildOptions options =
                BuildPlayerOptionsFactory.ResolveOptions(profile);

            Assert.AreEqual(BuildOptions.None, options);
        }

        [Test]
        public void Factory_ResolveOptions_DevelopmentBuild_IncludesDebugOptions()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile();
            profile.Platform.DevelopmentBuild = true;
            profile.Platform.ScriptDebugging = true;
            profile.Platform.AutoconnectProfiler = true;
            profile.Platform.DeepProfiling = true;

            BuildOptions options =
                BuildPlayerOptionsFactory.ResolveOptions(profile);

            Assert.AreEqual(
                BuildOptions.Development
                | BuildOptions.AllowDebugging
                | BuildOptions.ConnectToHost
                | BuildOptions.EnableDeepProfilingSupport,
                options);
        }

        [Test]
        public void Factory_ResolveLocation_Windows_AppendsExe()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile();
            BuildPipelineContext context = CreateContext(profile);

            string location =
                BuildPlayerOptionsFactory.ResolveLocationPath(context);

            Assert.IsTrue(
                location.EndsWith(".exe", System.StringComparison.OrdinalIgnoreCase),
                $"Windows 产物应带 .exe 扩展名，实际：{location}");
        }

        [Test]
        public void Factory_ResolveLocation_Windows_KeepsProvidedExe()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile();
            profile.Output.FileNameTemplate = "{ProductName}.exe";
            BuildPipelineContext context = CreateContext(profile);

            string location =
                BuildPlayerOptionsFactory.ResolveLocationPath(context);

            StringAssert.EndsWith("TestProduct.exe", location);
        }

        [Test]
        public void Factory_ResolveLocation_Android_AppendsApk()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile();
            profile.Platform.Target = BuildTarget.Android;
            profile.Platform.AndroidBuildAppBundle = false;
            BuildPipelineContext context = CreateContext(profile);

            string location =
                BuildPlayerOptionsFactory.ResolveLocationPath(context);

            StringAssert.EndsWith(".apk", location);
        }

        [Test]
        public void Factory_ResolveLocation_Android_AppendsAab()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile();
            profile.Platform.Target = BuildTarget.Android;
            profile.Platform.AndroidBuildAppBundle = true;
            BuildPipelineContext context = CreateContext(profile);

            string location =
                BuildPlayerOptionsFactory.ResolveLocationPath(context);

            StringAssert.EndsWith(".aab", location);
        }

        [Test]
        public void Factory_ResolveLocation_Ios_ReturnsDirectory()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile();
            profile.Platform.Target = BuildTarget.iOS;
            BuildPipelineContext context = CreateContext(profile);

            string location =
                BuildPlayerOptionsFactory.ResolveLocationPath(context);

            // iOS 位置是目录而非文件：应包含输出目录，
            // 且不应携带平台产物扩展名（版本号含点，不能用 Path.GetExtension 判断）。
            Assert.IsTrue(
                location.EndsWith(
                    context.OutputDirectory,
                    System.StringComparison.Ordinal),
                $"iOS 位置应为输出目录，实际：{location}");
            Assert.IsFalse(
                location.EndsWith(".exe", System.StringComparison.OrdinalIgnoreCase)
                || location.EndsWith(".apk", System.StringComparison.OrdinalIgnoreCase)
                || location.EndsWith(".aab", System.StringComparison.OrdinalIgnoreCase),
                $"iOS 位置不应是平台产物文件，实际：{location}");
        }

        [Test]
        public void Factory_ResolveLocation_Linux_DoesNotAppendExtension()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile();
            profile.Platform.Target = BuildTarget.StandaloneLinux64;
            profile.Output.FileNameTemplate = "{ProductName}";
            BuildPipelineContext context = CreateContext(profile);

            string location =
                BuildPlayerOptionsFactory.ResolveLocationPath(context);

            StringAssert.EndsWith("TestProduct", location);
        }

        [Test]
        public void Factory_ResolveLocation_Mac_AppendsApp()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile();
            profile.Platform.Target = BuildTarget.StandaloneOSX;
            BuildPipelineContext context = CreateContext(profile);

            string location =
                BuildPlayerOptionsFactory.ResolveLocationPath(context);

            StringAssert.EndsWith(".app", location);
        }

        [Test]
        public void Factory_ResolveLocation_WebGl_ReturnsDirectory()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile();
            profile.Platform.Target = BuildTarget.WebGL;
            BuildPipelineContext context = CreateContext(profile);

            string location =
                BuildPlayerOptionsFactory.ResolveLocationPath(context);

            Assert.That(
                location,
                Does.EndWith(context.OutputDirectory));
            Assert.That(
                BuildPlayerOptionsFactory.UsesDirectoryOutput(
                    BuildTarget.WebGL),
                Is.True);
        }

        [Test]
        public void Factory_ValidateDevelopmentOptions_Release_ReturnsNull()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile();
            profile.Flavor = BuildProfileFlavor.Release;
            profile.Platform.ScriptDebugging = true;

            Assert.IsNull(
                BuildPlayerOptionsFactory.ValidateDevelopmentOptions(profile));
        }

        [Test]
        public void Factory_ValidateDevelopmentOptions_DebugWithoutDevBuild_ReturnsError()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile();
            profile.Flavor = BuildProfileFlavor.Qa;
            profile.Platform.DevelopmentBuild = false;
            profile.Platform.ScriptDebugging = true;

            BuildValidationIssue? issue =
                BuildPlayerOptionsFactory.ValidateDevelopmentOptions(profile);

            Assert.IsTrue(issue.HasValue);
            Assert.AreEqual(
                BuildValidationLevel.Error, issue.Value.Level);
        }

        [Test]
        public void Factory_ValidateDevelopmentOptions_DevBuildEnabled_ReturnsNull()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile();
            profile.Flavor = BuildProfileFlavor.Qa;
            profile.Platform.DevelopmentBuild = true;
            profile.Platform.ScriptDebugging = true;

            Assert.IsNull(
                BuildPlayerOptionsFactory.ValidateDevelopmentOptions(profile));
        }

        [Test]
        public void SwitchBuildTargetStep_CanRun_WithoutProfile_ReturnsFalse()
        {
            SwitchBuildTargetStep step = new SwitchBuildTargetStep();

            Assert.IsFalse(step.CanRun(CreateEmptyContext()));
        }

        [Test]
        public void SwitchBuildTargetStep_Execute_SameTarget_Succeeds()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile();
            profile.Platform.Target = EditorUserBuildSettings.activeBuildTarget;
            BuildPipelineContext context = CreateContext(profile);

            SwitchBuildTargetStep step = new SwitchBuildTargetStep();
            BuildStepResult result = step.Execute(context);

            Assert.AreEqual(BuildStepStatus.Succeeded, result.Status);
            StringAssert.Contains("无需切换", result.Message);
        }

        [Test]
        public void ApplyBuildProfileStep_CanRun_WithoutProfile_ReturnsFalse()
        {
            ApplyBuildProfileStep step = new ApplyBuildProfileStep();

            Assert.IsFalse(step.CanRun(CreateEmptyContext()));
        }

        [Test]
        public void BuildPlayerStep_CanRun_WithOutputError_ReturnsFalse()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile();
            profile.Output.OutputRoot = "Assets";
            BuildPipelineContext context = CreateContext(profile);
            Assert.IsNotEmpty(context.OutputError);

            BuildPlayerStep step = new BuildPlayerStep();

            Assert.IsFalse(step.CanRun(context));
        }

        [Test]
        public void BuildPlayerStep_Execute_Cancelled_ReturnsCancelled()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile();
            CancellationTokenSource source = new CancellationTokenSource();
            source.Cancel();
            BuildPipelineContext context = CreateContext(profile, source.Token);

            BuildPlayerStep step = new BuildPlayerStep();
            BuildStepResult result = step.Execute(context);

            Assert.AreEqual(BuildStepStatus.Cancelled, result.Status);
        }

        [Test]
        public void FinalizeBuildStep_CanRun_WithOutputError_ReturnsFalse()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile();
            profile.Output.OutputRoot = "Assets";
            BuildPipelineContext context = CreateContext(profile);
            Assert.IsNotEmpty(context.OutputError);

            FinalizeBuildStep step = new FinalizeBuildStep();

            Assert.IsFalse(step.CanRun(context));
        }

        [Test]
        public void Registry_DiscoverCoreSteps_ContainsAllFive()
        {
            IReadOnlyList<IBuildPipelineStep> steps =
                BuildPipelineStepRegistry.GetAll();
            HashSet<string> ids = new HashSet<string>();
            for (int i = 0; i < steps.Count; i++)
            {
                ids.Add(steps[i].Id);
            }

            Assert.IsTrue(ids.Contains("core.validate"));
            Assert.IsTrue(ids.Contains("core.switch-target"));
            Assert.IsTrue(ids.Contains("core.apply-profile"));
            Assert.IsTrue(ids.Contains("core.build-player"));
            Assert.IsTrue(ids.Contains("core.finalize"));
        }
    }
}
