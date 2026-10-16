using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
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
        public void ObfuzStep_DoesNotRequireHybridClrAndJointStepIsAutomatic()
        {
            IBuildPipelineStep step = FindStep("obfuz");

            Assert.That(step, Is.Not.Null);
            Assert.That(step.Dependencies, Is.Empty);
            var joint = FindStep("obfuz.hotupdate");
            Assert.That(joint, Is.InstanceOf<IAutomaticBuildPipelineStep>());
            CollectionAssert.Contains(joint.Dependencies, "hybridclr");
            CollectionAssert.Contains(joint.Dependencies, "obfuz");
        }

        [TestCase(BuildRecipe.Player)]
        [TestCase(BuildRecipe.Release)]
        public void OrdinaryPlayerObfuzPlan_DoesNotIncludeHybridClrOrRebuild(BuildRecipe recipe)
        {
            var profile = CreateProfile(recipe);
            AddConfiguredStep(profile, "obfuz");
            try
            {
                var plan = BuildRecipePlanner.Create(profile);
                Assert.That(plan.IsValid, Is.True);
                Assert.That(plan.Contains("obfuz.validate-player"), Is.True);
                Assert.That(plan.Contains("obfuz.finalize-player"), Is.True);
                Assert.That(plan.Contains("hybridclr.prepare-player"), Is.False);
                Assert.That(plan.Contains("obfuz.prepare-player"), Is.False);
                Assert.That(plan.Contains("obfuz.rebuild-player"), Is.False);
                Assert.That(plan.Contains("obfuz.hotupdate"), Is.False);
                Assert.That(IndexOf(plan.StepIds, "obfuz.validate-player"), Is.LessThan(IndexOf(plan.StepIds, "core.build-player")));
                Assert.That(IndexOf(plan.StepIds, "obfuz.finalize-player"), Is.LessThan(IndexOf(plan.StepIds, "core.finalize")));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile.Steps[0].Configuration);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ObfuzPlayerResult_MissingOrFailedCallbackCannotReportSuccess()
        {
            Type step = FindStep("obfuz").GetType();
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            const string key = "UnityRFramework.Obfuz.PlayerResult";
            string previous = SessionState.GetString(key, string.Empty);
            try
            {
                step.GetMethod("ResetPlayerResult", flags).Invoke(null, null);
                Assert.That(FindStep("obfuz.finalize-player").Execute(null).Status, Is.EqualTo(BuildStepStatus.Failed));
                step.GetMethod("RecordPlayerResult", flags).Invoke(null, new object[] { false });
                Assert.That(FindStep("obfuz.finalize-player").Execute(null).Status, Is.EqualTo(BuildStepStatus.Failed));
                step.GetMethod("RecordPlayerResult", flags).Invoke(null, new object[] { true });
                Assert.That(step.GetProperty("PlayerSucceeded", flags).GetValue(null), Is.True);
                step.GetMethod("ResetPlayerResult", flags).Invoke(null, null);
                Assert.That(step.GetProperty("PlayerSucceeded", flags).GetValue(null), Is.False);
            }
            finally { SessionState.SetString(key, previous); }
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
        public void HybridClrConfiguration_MigratesMissingOfficialDemoVersionPath()
        {
            IBuildPipelineStep step = FindStep("hybridclr");
            ScriptableObject configuration =
                ScriptableObject.CreateInstance(step.ConfigurationType);
            try
            {
                FieldInfo outputRoot = step.ConfigurationType.GetField(
                    "OutputAssetRoot",
                    BindingFlags.Instance | BindingFlags.Public);
                outputRoot.SetValue(
                    configuration,
                    "Assets/Samples/UnityRFramework/1.2.0/"
                    + "Expansion.HybridCLR.Demo/GameAssets/HotUpdate");
                MethodInfo migrate = step.ConfigurationType.GetMethod(
                    "TryMigrateOfficialDemoPath",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(migrate.Invoke(configuration, null), Is.True);
                string migratedPath = (string)outputRoot.GetValue(configuration);
                Assert.That(
                    AssetDatabase.IsValidFolder(migratedPath),
                    Is.True);
                Assert.That(
                    migratedPath,
                    Does.EndWith(
                        "/Expansion.HybridCLR.Demo/GameAssets/HotUpdate"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(configuration);
            }
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

        [TestCase(ConfigExportTool.Csv)]
        [TestCase(ConfigExportTool.Excel)]
        public void ConfigOnlyExport_DoesNotRequireOrTouchLocalization(ConfigExportTool tool)
        {
            const string workbook = "Assets/UnityRFramework/Samples/Sample.Demo/ConfigSource/Config/Demo_Reward.xlsx";
            if (tool == ConfigExportTool.Excel && !File.Exists(workbook)) Assert.Ignore("Excel 示例源未导入。");
            string root = "Assets/Temp/FrameworkBuildTests/" + Guid.NewGuid().ToString("N");
            var profile = CreateProfile(BuildRecipe.Assets);
            var configuration = ScriptableObject.CreateInstance<ConfigExportBuildConfiguration>();
            try
            {
                Directory.CreateDirectory(root + "/Source");
                if (tool == ConfigExportTool.Excel) File.Copy(workbook, root + "/Source/Demo_Reward.xlsx");
                else File.WriteAllText(root + "/Source/BuildTestRow.csv", "Id,Name\nint,string\n编号,名称\n1,Hello");
                Directory.CreateDirectory(root + "/Localization");
                string sentinel = root + "/Localization/keep.json";
                File.WriteAllText(sentinel, "user-owned");
                configuration.ExportTool = tool;
                configuration.ExportLocalization = false;
                configuration.Options.ConfigSourceDirectory = root + "/Source";
                configuration.Options.LocalizationSourceDirectory = string.Empty;
                configuration.Options.LocalizationOutputDirectory = root + "/Localization";
                configuration.Options.ConfigOutputDirectory = root + "/Config";
                configuration.Options.GeneratedCodeDirectory = root + "/Generated";
                configuration.Options.GeneratedNamespace = "BuildTests";
                profile.Steps.Add(new BuildStepSettings { StepId = "config", Enabled = true, Configuration = configuration });
                var context = BuildPipelineContext.Create(profile, new Dictionary<string, IBuildPipelineStep>(), default);
                var step = new ConfigExportStep();
                var issues = new List<BuildValidationIssue>();
                step.Validate(context, issues);
                Assert.That(issues, Is.Empty);
                var result = step.Execute(context);
                Assert.That(result.Status, Is.EqualTo(BuildStepStatus.Succeeded), result.Message);
                Assert.That(Directory.GetFiles(root + "/Config/Binary", "*.bytes"), Is.Not.Empty);
                Assert.That(File.ReadAllText(sentinel), Is.EqualTo("user-owned"));
                CollectionAssert.AreEqual(new[] { sentinel }, Directory.GetFiles(root + "/Localization")
                    .Where(path => !path.EndsWith(".meta", StringComparison.Ordinal)).Select(path => path.Replace('\\', '/')));
                configuration.ExportLocalization = true;
                configuration.Options.LocalizationSourceDirectory = root + "/Source";
                configuration.Options.LocalizationOutputDirectory = configuration.Options.ConfigOutputDirectory;
                step.Validate(context, issues);
                Assert.That(issues, Has.Count.EqualTo(1));
                StringAssert.Contains("output directories must be different", issues[0].Message);
                Assert.That(step.Execute(context).Status, Is.EqualTo(BuildStepStatus.Failed));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
                if (File.Exists(root + ".meta")) File.Delete(root + ".meta");
                AssetDatabase.Refresh();
                UnityEngine.Object.DestroyImmediate(configuration);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void LocalizationOnlyExport_DoesNotRequireConfigSourceExcelOrConfigKey()
        {
            string root = "Assets/Temp/FrameworkBuildTests/" + Guid.NewGuid().ToString("N");
            var profile = CreateProfile(BuildRecipe.Assets);
            var configuration = ScriptableObject.CreateInstance<ConfigExportBuildConfiguration>();
            try
            {
                Directory.CreateDirectory(root + "/Languages");
                File.WriteAllText(root + "/Languages/en.csv", "Key,Value\nstring,string\n键,值\nhello,Hello");
                configuration.ExportConfig = false;
                configuration.ExportTool = ConfigExportTool.Excel;
                configuration.Options.ConfigSourceDirectory = string.Empty;
                configuration.Options.ConfigOutputDirectory = string.Empty;
                configuration.Options.GeneratedCodeDirectory = string.Empty;
                configuration.Options.ConfigBinaryProtection = RFramework.ConfigProtectionMode.EncryptedAndAuthenticated;
                configuration.Options.ConfigProtectionSourceRoot = "";
                configuration.Options.LocalizationSourceDirectory = root + "/Languages";
                configuration.Options.LocalizationOutputDirectory = root + "/Output";
                profile.Steps.Add(new BuildStepSettings { StepId = "config", Enabled = true, Configuration = configuration });
                var context = BuildPipelineContext.Create(profile, new Dictionary<string, IBuildPipelineStep>(), default);
                var step = new ConfigExportStep();
                var issues = new List<BuildValidationIssue>();
                step.Validate(context, issues);
                Assert.That(issues, Is.Empty);
                var result = step.Execute(context);
                Assert.That(result.Status, Is.EqualTo(BuildStepStatus.Succeeded), result.Message);
                Assert.That(File.Exists(root + "/Output/Json/en.json"), Is.True);
                configuration.ExportLocalization = false;
                step.Validate(context, issues);
                Assert.That(issues, Has.Count.EqualTo(1));
                Assert.That(step.Execute(context).Status, Is.EqualTo(BuildStepStatus.Failed));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
                if (File.Exists(root + ".meta")) File.Delete(root + ".meta");
                AssetDatabase.Refresh();
                UnityEngine.Object.DestroyImmediate(configuration);
                UnityEngine.Object.DestroyImmediate(profile);
            }
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
            Assert.That(plan.Contains("obfuz.hotupdate"), Is.True);
            Assert.That(
                IndexOf(plan.StepIds, "hybridclr"),
                Is.LessThan(IndexOf(plan.StepIds, "obfuz.hotupdate")));
            Assert.That(
                IndexOf(plan.StepIds, "obfuz.hotupdate"),
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
        public void ReleaseRecipe_BuildsPlayerBeforePreparingCodeAndAssets()
        {
            UnityRFrameworkBuildProfile profile = CreateProfile(BuildRecipe.Release);
            AddConfiguredStep(profile, "hybridclr");
            AddConfiguredStep(profile, "obfuz");
            AddConfiguredStep(profile, "yooasset");

            BuildRecipePlan plan = BuildRecipePlanner.Create(profile);

            Assert.That(plan.IsValid, Is.True);
            Assert.That(
                IndexOf(plan.StepIds, "hybridclr"),
                Is.LessThan(IndexOf(plan.StepIds, "obfuz.hotupdate")));
            Assert.That(
                IndexOf(plan.StepIds, "obfuz.hotupdate"),
                Is.LessThan(IndexOf(plan.StepIds, "yooasset")));
            Assert.That(
                IndexOf(plan.StepIds, "hybridclr.prepare-player"),
                Is.LessThan(IndexOf(plan.StepIds, "core.build-player")));
            Assert.That(
                IndexOf(plan.StepIds, "core.build-player"),
                Is.LessThan(IndexOf(plan.StepIds, "hybridclr")));
        }

        [TestCase(BuildRecipe.Release, false, "ClearAndCopyAll", true)]
        [TestCase(BuildRecipe.Release, true, "None", true)]
        [TestCase(BuildRecipe.Release, true, "ClearAndCopyAll", true)]
        [TestCase(BuildRecipe.Release, true, "ClearAndCopyByTags", false)]
        [TestCase(BuildRecipe.Release, true, "OnlyCopyAll", true)]
        [TestCase(BuildRecipe.Release, true, "OnlyCopyByTags", true)]
        [TestCase(BuildRecipe.Assets, true, "ClearAndCopyAll", true)]
        [TestCase(BuildRecipe.HotUpdate, true, "ClearAndCopyAll", true)]
        [TestCase(BuildRecipe.Player, true, "ClearAndCopyAll", true)]
        public void YooAssetBuiltinRelease_OrdersResourcesAndFinalPlayer(
            BuildRecipe recipe, bool hybridEnabled, string copyOption, bool overridePipeline)
        {
            var profile = CreateProfile(recipe);
            var yoo = AddConfiguredStep(profile, "yooasset").Configuration;
            AddConfiguredStep(profile, "hybridclr", hybridEnabled);
            AddConfiguredStep(profile, "obfuz");
            AddConfiguredStep(profile, "config");
            string package = "FrameworkOrderTest_" + Guid.NewGuid().ToString("N");
            const string pipeline = "LegacyBuildPipeline";
            string prefix = PlayerSettings.productGUID + "_" + package;
            Type builder = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("YooAsset.Editor.BundleBuilderSetting"))
                .First(type => type != null);
            MethodInfo setCopy = builder.GetMethod("SetPackageBundledCopyOption");
            object copy = Enum.Parse(setCopy.GetParameters()[2].ParameterType, copyOption);
            try
            {
                yoo.GetType().GetField("PackageName").SetValue(yoo, package);
                yoo.GetType().GetField("BuildPipelineName").SetValue(yoo,
                    overridePipeline ? pipeline : string.Empty);
                builder.GetMethod("SetPackageBuildPipeline").Invoke(null, new object[] {
                    package, overridePipeline ? "ScriptableBuildPipeline" : pipeline
                });
                setCopy.Invoke(null, new[] { (object)package, pipeline, copy });

                var plan = BuildRecipePlanner.Create(profile);
                Assert.That(plan.IsValid, Is.True);
                const string finalId = "yooasset.finalize-player";
                bool finalPlayer = recipe == BuildRecipe.Release && hybridEnabled && copyOption != "None";
                Assert.That(plan.Contains(finalId), Is.EqualTo(finalPlayer));
                if (recipe == BuildRecipe.Release)
                {
                    Assert.That(IndexOf(plan.StepIds, "config"), Is.LessThan(IndexOf(plan.StepIds, "yooasset")));
                    if (!hybridEnabled)
                    {
                        Assert.That(IndexOf(plan.StepIds, "yooasset"), Is.LessThan(IndexOf(plan.StepIds, "core.build-player")));
                    }
                    else
                    {
                        Assert.That(IndexOf(plan.StepIds, "obfuz.rebuild-player"), Is.LessThan(IndexOf(plan.StepIds, "hybridclr")));
                        Assert.That(IndexOf(plan.StepIds, "hybridclr"), Is.LessThan(IndexOf(plan.StepIds, "yooasset")));
                    }
                }
                if (finalPlayer)
                {
                    Assert.That(IndexOf(plan.StepIds, "yooasset"), Is.LessThan(IndexOf(plan.StepIds, finalId)));
                    Assert.That(IndexOf(plan.StepIds, finalId), Is.LessThan(IndexOf(plan.StepIds, "obfuz.finalize-player")));
                    Assert.That(IndexOf(plan.StepIds, "obfuz.finalize-player"), Is.LessThan(IndexOf(plan.StepIds, "core.finalize")));
                    var step = FindStep(finalId);
                    Assert.That(step.CallsBuildPipeline, Is.True);
                    CollectionAssert.AreEquivalent(new[] { "core.build-player", "yooasset" }, step.Dependencies);
                    var context = BuildPipelineContext.Create(profile,
                        new Dictionary<string, IBuildPipelineStep>(), new System.Threading.CancellationToken(true));
                    Assert.That(step.CanRun(context), Is.True);
                    Assert.That(step.Execute(context).Status, Is.EqualTo(BuildStepStatus.Cancelled));
                }
                Assert.That(builder.GetMethod("GetPackageBundledCopyOption").Invoke(null,
                    new object[] { package, pipeline }), Is.EqualTo(copy), "计划解析不应修改原生 Builder 设置。");
            }
            finally
            {
                EditorPrefs.DeleteKey(prefix + "_BuildPipelineName");
                EditorPrefs.DeleteKey(prefix + "_" + pipeline + "_EBundledCopyOption");
                foreach (var entry in profile.Steps) UnityEngine.Object.DestroyImmediate(entry.Configuration);
                UnityEngine.Object.DestroyImmediate(profile);
            }
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
