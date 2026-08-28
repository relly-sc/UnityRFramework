using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// Build Recipe、固定阶段和步骤依赖契约测试。
    /// </summary>
    public sealed class BuildRecipeTests
    {
        private sealed class TestConfiguration : ScriptableObject
        {
        }

        private sealed class TestStep : BuildPipelineStepBase
        {
            private readonly string id;
            private readonly BuildPipelineStage stage;
            private readonly int order;
            private readonly IReadOnlyList<string> dependencies;

            public TestStep(
                string id,
                BuildPipelineStage stage,
                int order = 0,
                params string[] dependencies)
            {
                this.id = id;
                this.stage = stage;
                this.order = order;
                this.dependencies = dependencies ?? Array.Empty<string>();
            }

            public override string Id => id;

            public override BuildPipelineStage Stage => stage;

            public override int Order => order;

            public override IReadOnlyList<string> Dependencies => dependencies;

            public override BuildStepResult Execute(BuildPipelineContext context)
            {
                return BuildStepResult.Succeeded("ok");
            }
        }

        private static UnityRFrameworkBuildProfile CreateProfile(BuildRecipe recipe)
        {
            UnityRFrameworkBuildProfile profile =
                ScriptableObject.CreateInstance<UnityRFrameworkBuildProfile>();
            profile.Recipe = recipe;
            return profile;
        }

        private static void AddStep(
            UnityRFrameworkBuildProfile profile,
            string id,
            bool enabled = true,
            ScriptableObject configuration = null)
        {
            profile.Steps.Add(new BuildStepSettings
            {
                StepId = id,
                Enabled = enabled,
                Configuration = configuration
            });
        }

        [Test]
        public void PlayerRecipe_ExcludesAssetStages()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile(BuildRecipe.Player);
            AddStep(profile, "config");
            List<IBuildPipelineStep> available = new List<IBuildPipelineStep>
            {
                new TestStep("core.validate", BuildPipelineStage.Validate),
                new TestStep("core.switch-target", BuildPipelineStage.SwitchTarget),
                new TestStep("core.apply-profile", BuildPipelineStage.ApplySettings),
                new TestStep("config", BuildPipelineStage.PrepareData),
                new TestStep("core.build-player", BuildPipelineStage.BuildPlayer),
                new TestStep("core.finalize", BuildPipelineStage.Finalize)
            };

            BuildRecipePlan plan = BuildRecipePlanner.Create(profile, available);

            Assert.That(plan.IsValid, Is.True);
            Assert.That(plan.Contains("config"), Is.False);
            Assert.That(plan.Contains("core.build-player"), Is.True);
        }

        [Test]
        public void ReleaseRecipe_SortsByStageThenDependency()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile(BuildRecipe.Release);
            AddStep(profile, "prepare");
            AddStep(profile, "obfuscate");
            List<IBuildPipelineStep> available = new List<IBuildPipelineStep>
            {
                new TestStep(
                    "obfuscate",
                    BuildPipelineStage.PrepareCode,
                    0,
                    "prepare"),
                new TestStep("core.finalize", BuildPipelineStage.Finalize),
                new TestStep("core.build-player", BuildPipelineStage.BuildPlayer),
                new TestStep("core.apply-profile", BuildPipelineStage.ApplySettings),
                new TestStep("core.switch-target", BuildPipelineStage.SwitchTarget),
                new TestStep("prepare", BuildPipelineStage.PrepareCode, 100),
                new TestStep("core.validate", BuildPipelineStage.Validate)
            };

            BuildRecipePlan plan = BuildRecipePlanner.Create(profile, available);

            Assert.That(plan.IsValid, Is.True);
            CollectionAssert.AreEqual(
                new[]
                {
                    "core.validate",
                    "core.switch-target",
                    "core.apply-profile",
                    "core.build-player",
                    "prepare",
                    "obfuscate",
                    "core.finalize"
                },
                plan.StepIds);
        }

        [Test]
        public void EnabledStepWithoutImplementation_IsError()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile(BuildRecipe.Release);
            AddStep(profile, "missing.expansion");

            BuildRecipePlan plan = BuildRecipePlanner.Create(
                profile,
                Array.Empty<IBuildPipelineStep>());

            Assert.That(plan.IsValid, Is.False);
            Assert.That(
                plan.Issues,
                Has.Some.Matches<BuildValidationIssue>(
                    issue => issue.Level == BuildValidationLevel.Error
                        && issue.Message.Contains("missing.expansion")));
        }

        [Test]
        public void MissingDependency_IsError()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile(BuildRecipe.HotUpdate);
            AddStep(profile, "obfuscate");
            TestStep step = new TestStep(
                "obfuscate",
                BuildPipelineStage.PrepareCode,
                0,
                "compile");

            BuildRecipePlan plan = BuildRecipePlanner.Create(
                profile,
                new[] { step });

            Assert.That(plan.IsValid, Is.False);
            Assert.That(
                plan.Issues,
                Has.Some.Matches<BuildValidationIssue>(
                    issue => issue.Message.Contains("compile")));
        }

        [Test]
        public void CircularDependency_IsError()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile(BuildRecipe.HotUpdate);
            AddStep(profile, "a");
            AddStep(profile, "b");
            List<IBuildPipelineStep> available = new List<IBuildPipelineStep>
            {
                new TestStep("a", BuildPipelineStage.PrepareCode, 0, "b"),
                new TestStep("b", BuildPipelineStage.PrepareCode, 0, "a")
            };

            BuildRecipePlan plan = BuildRecipePlanner.Create(profile, available);

            Assert.That(plan.IsValid, Is.False);
            Assert.That(
                plan.Issues,
                Has.Some.Matches<BuildValidationIssue>(
                    issue => issue.Message.Contains("循环依赖")));
        }

        [Test]
        public void ConfigurationLocator_ReturnsOnlyMatchingType()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile(BuildRecipe.Assets);
            TestConfiguration configuration =
                ScriptableObject.CreateInstance<TestConfiguration>();
            AddStep(profile, "config", configuration: configuration);

            Assert.That(
                BuildStepConfigLocator.GetConfiguration<TestConfiguration>(
                    profile,
                    "config"),
                Is.SameAs(configuration));
            Assert.That(
                BuildStepConfigLocator.GetConfiguration<ConfigExportBuildConfiguration>(
                    profile,
                    "config"),
                Is.Null);
        }

        [Test]
        public void WaitingResult_HasDistinctStatus()
        {
            BuildStepResult result = BuildStepResult.WaitingForEditor("等待编译");

            Assert.That(result.Status, Is.EqualTo(BuildStepStatus.WaitingForEditor));
        }
    }
}
