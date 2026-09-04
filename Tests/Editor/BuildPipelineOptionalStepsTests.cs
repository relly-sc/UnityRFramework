using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// 可选步骤按需发现、配置资产和依赖契约测试。
    /// 不调用第三方插件的真实构建 API。
    /// </summary>
    public sealed class BuildPipelineOptionalStepsTests
    {
        private static UnityRFrameworkBuildProfile CreateProfile(BuildRecipe recipe)
        {
            UnityRFrameworkBuildProfile profile =
                ScriptableObject.CreateInstance<UnityRFrameworkBuildProfile>();
            profile.Recipe = recipe;
            return profile;
        }

        private static IBuildPipelineStep FindStep(string id)
        {
            IReadOnlyList<IBuildPipelineStep> steps =
                BuildPipelineStepRegistry.GetAll();
            for (int i = 0; i < steps.Count; i++)
            {
                if (string.Equals(
                        steps[i].Id,
                        id,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return steps[i];
                }
            }

            return null;
        }

        private static BuildStepSettings AddConfiguredStep(
            UnityRFrameworkBuildProfile profile,
            string id,
            bool enabled = true)
        {
            IBuildPipelineStep step = FindStep(id);
            Assert.That(step, Is.Not.Null, $"步骤 '{id}' 未注册。");
            BuildStepSettings setting = new BuildStepSettings
            {
                StepId = id,
                Enabled = enabled
            };
            if (step.ConfigurationType != null)
            {
                setting.Configuration =
                    ScriptableObject.CreateInstance(step.ConfigurationType);
            }

            profile.Steps.Add(setting);
            return setting;
        }

        [Test]
        public void RegisteredOptionalSteps_AreAvailable()
        {
            BuildStepAvailability.InvalidateCache();

            Assert.That(BuildStepAvailability.IsAvailable("config"), Is.True);
            Assert.That(BuildStepAvailability.IsAvailable("hybridclr"), Is.True);
            Assert.That(BuildStepAvailability.IsAvailable("yooasset"), Is.True);
            Assert.That(BuildStepAvailability.IsAvailable("obfuz"), Is.True);
        }

        [Test]
        public void UnknownStep_IsUnavailableWithActionableReason()
        {
            Assert.That(
                BuildStepAvailability.IsAvailable("missing.expansion"),
                Is.False);
            StringAssert.Contains(
                "没有已加载的实现",
                BuildStepAvailability.GetUnavailableReason("missing.expansion"));
        }

        [Test]
        public void MissingOptionalStep_DisabledEntry_DoesNotInvalidateRecipe()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile(BuildRecipe.Assets);
            profile.Steps.Add(new BuildStepSettings
            {
                StepId = "missing.expansion",
                Enabled = false
            });

            BuildRecipePlan plan = BuildRecipePlanner.Create(
                profile,
                Array.Empty<IBuildPipelineStep>(),
                requireCoreSteps: false);

            Assert.That(
                plan.IsValid,
                Is.True,
                "未安装第三方插件且未启用对应步骤时不应阻断构建。");
            Assert.That(plan.StepIds, Is.Empty);
        }

        [Test]
        public void MissingOptionalStep_EnabledEntry_ReportsConfigurationError()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile(BuildRecipe.Assets);
            profile.Steps.Add(new BuildStepSettings
            {
                StepId = "missing.expansion",
                Enabled = true
            });

            BuildRecipePlan plan = BuildRecipePlanner.Create(
                profile,
                Array.Empty<IBuildPipelineStep>(),
                requireCoreSteps: false);

            Assert.That(plan.IsValid, Is.False);
            Assert.That(
                plan.Issues,
                Has.Some.Matches<BuildValidationIssue>(issue =>
                    issue.Message.Contains("没有对应实现")));
        }

        [TestCase("config", BuildPipelineStage.PrepareData)]
        [TestCase("hybridclr", BuildPipelineStage.PrepareCode)]
        [TestCase("obfuz", BuildPipelineStage.PrepareCode)]
        [TestCase("yooasset", BuildPipelineStage.BuildAssets)]
        public void OptionalStep_DeclaresStageAndConfiguration(
            string id,
            BuildPipelineStage expectedStage)
        {
            IBuildPipelineStep step = FindStep(id);

            Assert.That(step, Is.Not.Null);
            Assert.That(step.Stage, Is.EqualTo(expectedStage));
            Assert.That(step.ConfigurationType, Is.Not.Null);
            Assert.That(
                typeof(ScriptableObject).IsAssignableFrom(step.ConfigurationType),
                Is.True);
        }

        [Test]
        public void ObfuzStep_RequiresHybridClr()
        {
            IBuildPipelineStep step = FindStep("obfuz");

            Assert.That(step, Is.Not.Null);
            CollectionAssert.Contains(step.Dependencies, "hybridclr");
        }

        [Test]
        public void ObfuzConfiguration_DoesNotDuplicatePluginSettings()
        {
            IBuildPipelineStep step = FindStep("obfuz");

            Assert.That(step, Is.Not.Null);
            Assert.That(step.ConfigurationType, Is.Not.Null);
            FieldInfo[] fields = step.ConfigurationType.GetFields(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly);

            Assert.That(
                fields,
                Is.Empty,
                "Obfuz 参数应只在 Obfuz Settings 中维护，构建配置资产仅作为步骤入口。 ");
        }

        [Test]
        public void HybridClrConfiguration_ContainsOnlyFrameworkPublishingParameters()
        {
            IBuildPipelineStep step = FindStep("hybridclr");
            FieldInfo[] fields = step.ConfigurationType.GetFields(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.DeclaredOnly);

            CollectionAssert.AreEquivalent(
                new[]
                {
                    "OutputAssetRoot",
                    "EntryTypeName",
                    "CodeVersion",
                    "IncludePdb"
                },
                Array.ConvertAll(fields, field => field.Name));
        }

        [Test]
        public void HybridClrPlayerPreparation_IsAutomaticAndHiddenFromProfileSteps()
        {
            IBuildPipelineStep step = FindStep("hybridclr.prepare-player");

            Assert.That(step, Is.Not.Null);
            Assert.That(step, Is.InstanceOf<IAutomaticBuildPipelineStep>());
            Assert.That(step.Stage, Is.EqualTo(BuildPipelineStage.PreparePlayer));
            Assert.That(step.ConfigurationType, Is.Null);
            Assert.That(
                BuildStepAvailability.EnumerateKnownSteps(),
                Has.None.Matches<(string Id, string FriendlyName)>(
                    item => item.Id == "hybridclr.prepare-player"));
        }

        [Test]
        public void ConfigStep_MissingSourceDirectories_AddsErrors()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile(BuildRecipe.Assets);
            ConfigExportBuildConfiguration configuration =
                ScriptableObject.CreateInstance<ConfigExportBuildConfiguration>();
            configuration.Options.ConfigSourceDirectory =
                "Assets/NoSuchConfig_" + Guid.NewGuid().ToString("N");
            configuration.Options.LocalizationSourceDirectory =
                "Assets/NoSuchLocalization_" + Guid.NewGuid().ToString("N");
            profile.Steps.Add(new BuildStepSettings
            {
                StepId = "config",
                Enabled = true,
                Configuration = configuration
            });
            ConfigExportStep step = new ConfigExportStep();
            List<BuildValidationIssue> issues = new List<BuildValidationIssue>();
            BuildPipelineContext context = BuildPipelineContext.Create(
                profile,
                new Dictionary<string, IBuildPipelineStep>(),
                default);

            step.Validate(context, issues);

            Assert.That(
                issues,
                Has.Exactly(2).Matches<BuildValidationIssue>(
                    issue => issue.Code == "CONFIG"
                        && issue.Level == BuildValidationLevel.Error));
        }

        [Test]
        public void HotUpdateRecipe_OrdersImportedStepsByStageAndDependency()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile(BuildRecipe.HotUpdate);
            AddConfiguredStep(profile, "hybridclr");
            AddConfiguredStep(profile, "obfuz");
            AddConfiguredStep(profile, "yooasset");

            BuildRecipePlan plan = BuildRecipePlanner.Create(profile);

            Assert.That(plan.IsValid, Is.True);
            Assert.That(
                IndexOf(plan.StepIds, "hybridclr"),
                Is.LessThan(IndexOf(plan.StepIds, "obfuz")));
            Assert.That(
                IndexOf(plan.StepIds, "obfuz"),
                Is.LessThan(IndexOf(plan.StepIds, "yooasset")));
        }

        [Test]
        public void PlayerRecipe_AutomaticallyPreparesHybridClrBeforePlayer()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile(BuildRecipe.Player);
            AddConfiguredStep(profile, "hybridclr");

            BuildRecipePlan plan = BuildRecipePlanner.Create(profile);

            Assert.That(plan.IsValid, Is.True);
            Assert.That(plan.Contains("hybridclr.prepare-player"), Is.True);
            Assert.That(plan.Contains("hybridclr"), Is.False);
            Assert.That(
                IndexOf(plan.StepIds, "hybridclr.prepare-player"),
                Is.LessThan(IndexOf(plan.StepIds, "core.build-player")));
        }

        [Test]
        public void ReleaseRecipe_PreparesCodeAndAssetsBeforeBuildingPlayer()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile(BuildRecipe.Release);
            AddConfiguredStep(profile, "hybridclr");
            AddConfiguredStep(profile, "obfuz");
            AddConfiguredStep(profile, "yooasset");

            BuildRecipePlan plan = BuildRecipePlanner.Create(profile);

            Assert.That(plan.IsValid, Is.True);
            Assert.That(
                IndexOf(plan.StepIds, "hybridclr"),
                Is.LessThan(IndexOf(plan.StepIds, "obfuz")));
            Assert.That(
                IndexOf(plan.StepIds, "obfuz"),
                Is.LessThan(IndexOf(plan.StepIds, "yooasset")));
            Assert.That(
                IndexOf(plan.StepIds, "yooasset"),
                Is.LessThan(IndexOf(plan.StepIds, "hybridclr.prepare-player")));
            Assert.That(
                IndexOf(plan.StepIds, "hybridclr.prepare-player"),
                Is.LessThan(IndexOf(plan.StepIds, "core.build-player")));
        }

        [Test]
        public void DisabledOptionalStep_IsNotSelected()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile(BuildRecipe.Assets);
            AddConfiguredStep(profile, "config", false);

            BuildRecipePlan plan = BuildRecipePlanner.Create(profile);

            Assert.That(plan.IsValid, Is.True);
            Assert.That(plan.Contains("config"), Is.False);
        }

        private static int IndexOf(
            IReadOnlyList<string> values,
            string value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(
                        values[i],
                        value,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
