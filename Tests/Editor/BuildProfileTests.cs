using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// 构建配置数据模型的序列化、复制、校验与敏感信息提供者测试。
    /// 覆盖阶段 1 验收标准：Profile 序列化稳定、复制独立、敏感密码不入资产。
    /// </summary>
    public sealed class BuildProfileTests
    {
        /// <summary>测试用工程根目录绝对路径。</summary>
        private static string ProjectRoot =>
            Path.GetDirectoryName(Application.dataPath) ?? string.Empty;

        /// <summary>
        /// 创建一份填充了合法标识与平台的测试 Profile，并从项目资产库加载第一个场景。
        /// </summary>
        /// <returns>处于合法状态的测试 Profile。</returns>
        private static UnityRFrameworkBuildProfile CreateValidProfile()
        {
            UnityRFrameworkBuildProfile profile =
                ScriptableObject.CreateInstance<UnityRFrameworkBuildProfile>();
            profile.Platform.CompanyName = "TestCompany";
            profile.Platform.ProductName = "TestProduct";
            profile.Platform.ApplicationIdentifier = "com.test.product";
            profile.Platform.PublicVersion = "1.0.0";
            profile.Platform.BuildNumber = 12;
            profile.Platform.ScriptingBackend = ScriptingImplementation.IL2CPP;

            string[] sceneGuids = AssetDatabase.FindAssets("t:SceneAsset");
            if (sceneGuids.Length > 0)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[0]);
                BuildSceneEntry entry = new BuildSceneEntry
                {
                    Scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath)
                };
                profile.Scenes.Add(entry);
            }

            return profile;
        }

        /// <summary>
        /// 默认 Profile 缺少公司、产品与包名，校验应返回错误。
        /// </summary>
        [Test]
        public void DefaultProfile_Validate_ReportsMissingIdentity()
        {
            UnityRFrameworkBuildProfile profile =
                ScriptableObject.CreateInstance<UnityRFrameworkBuildProfile>();

            List<string> errors = profile.Validate();

            Assert.That(errors, Is.Not.Empty);
            Assert.That(
                errors,
                Has.Some.Contains("公司名称"));
            Assert.That(
                errors,
                Has.Some.Contains("产品名称"));
            Assert.That(
                errors,
                Has.Some.Contains("应用标识"));
        }

        /// <summary>
        /// 填齐合法配置后校验应无错误。
        /// </summary>
        [Test]
        public void ValidProfile_Validate_ReturnsNoErrors()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();
            if (profile.Scenes.Count == 0)
            {
                Assert.Ignore("项目中没有可用的场景资产，跳过场景相关校验测试。");
            }

            List<string> errors = profile.Validate();

            Assert.That(errors, Is.Empty);
        }

        /// <summary>
        /// Profile 序列化往返后平台关键字段保持稳定。
        /// </summary>
        [Test]
        public void Profile_SerializeRoundTrip_FieldsPreserved()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();
            profile.Recipe = BuildRecipe.Assets;
            profile.Output.OutputRoot = "Builds";
            string json = JsonUtility.ToJson(profile);

            UnityRFrameworkBuildProfile restored =
                ScriptableObject.CreateInstance<UnityRFrameworkBuildProfile>();
            JsonUtility.FromJsonOverwrite(json, restored);

            Assert.That(
                restored.Platform.Target,
                Is.EqualTo(profile.Platform.Target));
            Assert.That(
                restored.Platform.CompanyName,
                Is.EqualTo(profile.Platform.CompanyName));
            Assert.That(
                restored.Platform.PublicVersion,
                Is.EqualTo(profile.Platform.PublicVersion));
            Assert.That(
                restored.Platform.BuildNumber,
                Is.EqualTo(profile.Platform.BuildNumber));
            Assert.That(
                restored.Platform.ScriptingBackend,
                Is.EqualTo(profile.Platform.ScriptingBackend));
            Assert.That(
                restored.Output.OutputRoot,
                Is.EqualTo(profile.Output.OutputRoot));
            Assert.That(restored.Recipe, Is.EqualTo(BuildRecipe.Assets));
        }

        /// <summary>
        /// 版本 1 Profile 迁移后应升级版本并使用 Release Recipe。
        /// </summary>
        [Test]
        public void VersionOneProfile_Migrate_UsesReleaseRecipe()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();
            profile.SerializedVersion = 1;

            profile.Migrate();

            Assert.That(
                profile.SerializedVersion,
                Is.EqualTo(UnityRFrameworkBuildProfile.CurrentSerializedVersion));
            Assert.That(profile.Recipe, Is.EqualTo(BuildRecipe.Release));
        }

        /// <summary>
        /// 版本 2 Profile 应迁移废弃的 .NET Standard 2.0，
        /// 并将旧的附加宏合并到唯一的公共宏列表。
        /// </summary>
        [Test]
        public void VersionTwoProfile_Migrate_NormalizesApiAndDefines()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();
            profile.SerializedVersion = 2;
            profile.Platform.ApiCompatibilityLevel =
                ApiCompatibilityLevel.NET_Standard_2_0;
            profile.Platform.DefineSymbols = null;
            profile.Platform.LegacyAdditionalDefineSymbols.Add("LEGACY_FEATURE");

            profile.Migrate();

            Assert.That(
                profile.Platform.ApiCompatibilityLevel,
                Is.EqualTo(ApiCompatibilityLevel.NET_Standard));
            Assert.That(
                profile.Platform.DefineSymbols,
                Does.Contain("LEGACY_FEATURE"));
            Assert.That(
                profile.Platform.LegacyAdditionalDefineSymbols,
                Is.Empty);
        }

        /// <summary>
        /// 持久化的版本 1 Profile 迁移后应创建 Config 配置子资产，
        /// 复制 Profile 资产后配置子资产也必须独立。
        /// </summary>
        [Test]
        public void ProfileAsset_MigrateAndCopy_ConfigurationIsIndependent()
        {
            string suffix = Guid.NewGuid().ToString("N");
            string sourcePath =
                $"{UnityRFrameworkBuildProfile.DefaultAssetDirectory}/ProfileMigration_{suffix}.asset";
            string copyPath =
                $"{UnityRFrameworkBuildProfile.DefaultAssetDirectory}/ProfileMigration_{suffix}_Copy.asset";
            UnityRFrameworkBuildProfile source =
                ScriptableObject.CreateInstance<UnityRFrameworkBuildProfile>();
            source.SerializedVersion = 1;
            source.ConfigExport.Options.ConfigSourceDirectory =
                "Assets/ConfigSource/Migrated";
            source.Steps.Add(new BuildStepSettings
            {
                StepId = "config",
                Enabled = true
            });

            try
            {
                AssetDatabase.CreateAsset(source, sourcePath);

                bool changed = BuildProfileEditorUtility.MigrateProfile(source);
                ConfigExportBuildConfiguration sourceConfiguration =
                    BuildStepConfigLocator.GetConfiguration<ConfigExportBuildConfiguration>(
                        source,
                        "config");

                Assert.That(changed, Is.True);
                Assert.That(sourceConfiguration, Is.Not.Null);
                Assert.That(
                    sourceConfiguration.Options.ConfigSourceDirectory,
                    Is.EqualTo("Assets/ConfigSource/Migrated"));
                Assert.That(
                    AssetDatabase.GetAssetPath(sourceConfiguration),
                    Is.EqualTo(sourcePath));

                Assert.That(AssetDatabase.CopyAsset(sourcePath, copyPath), Is.True);
                AssetDatabase.ImportAsset(copyPath);
                UnityRFrameworkBuildProfile copy =
                    AssetDatabase.LoadAssetAtPath<UnityRFrameworkBuildProfile>(copyPath);
                ConfigExportBuildConfiguration copyConfiguration =
                    BuildStepConfigLocator.GetConfiguration<ConfigExportBuildConfiguration>(
                        copy,
                        "config");

                Assert.That(copyConfiguration, Is.Not.Null);
                Assert.That(copyConfiguration, Is.Not.SameAs(sourceConfiguration));
                copyConfiguration.Options.ConfigSourceDirectory =
                    "Assets/ConfigSource/Copy";
                Assert.That(
                    sourceConfiguration.Options.ConfigSourceDirectory,
                    Is.EqualTo("Assets/ConfigSource/Migrated"));
            }
            finally
            {
                AssetDatabase.DeleteAsset(copyPath);
                AssetDatabase.DeleteAsset(sourcePath);
            }
        }

        /// <summary>
        /// 当前版本 Profile 缺少配置时迁移器不得隐式创建，缺失应由校验器报告。
        /// </summary>
        [Test]
        public void CurrentProfile_Migrate_DoesNotCreateMissingConfiguration()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();
            profile.Steps.Add(new BuildStepSettings
            {
                StepId = "config",
                Enabled = true
            });

            bool changed = BuildProfileEditorUtility.MigrateProfile(profile);

            Assert.That(changed, Is.False);
            Assert.That(profile.Steps[0].Configuration, Is.Null);
        }

        /// <summary>
        /// 用户显式创建步骤配置时应生成归属于 Profile 的独立配置子资产。
        /// </summary>
        [Test]
        public void CreateStepConfiguration_Explicitly_CreatesProfileSubAsset()
        {
            string suffix = Guid.NewGuid().ToString("N");
            string path =
                $"{UnityRFrameworkBuildProfile.DefaultAssetDirectory}/ProfileConfiguration_{suffix}.asset";
            UnityRFrameworkBuildProfile profile = CreateValidProfile();
            profile.Steps.Add(new BuildStepSettings
            {
                StepId = "config",
                Enabled = true
            });

            try
            {
                AssetDatabase.CreateAsset(profile, path);

                ScriptableObject configuration =
                    BuildProfileEditorUtility.CreateStepConfiguration(
                        profile,
                        "config");

                Assert.That(configuration, Is.TypeOf<ConfigExportBuildConfiguration>());
                Assert.That(profile.Steps[0].Configuration, Is.SameAs(configuration));
                Assert.That(AssetDatabase.GetAssetPath(configuration), Is.EqualTo(path));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        /// <summary>
        /// 复制 Profile 后修改副本的列表与嵌套配置，不共享原实例。
        /// </summary>
        [Test]
        public void CopyProfile_IndependentModification_DoesNotAffectSource()
        {
            UnityRFrameworkBuildProfile source = CreateValidProfile();
            source.Scenes.Add(new BuildSceneEntry());
            source.Steps.Add(new BuildStepSettings { StepId = "step.a" });
            int sourceSceneCount = source.Scenes.Count;

            UnityRFrameworkBuildProfile copy =
                (UnityRFrameworkBuildProfile)ScriptableObject.Instantiate(source);

            copy.Scenes.Add(new BuildSceneEntry());
            copy.Steps[0].StepId = "step.b";
            copy.Platform.BuildNumber = 99;

            Assert.That(source.Scenes.Count, Is.EqualTo(sourceSceneCount));
            Assert.That(copy.Scenes.Count, Is.EqualTo(sourceSceneCount + 1));
            Assert.That(source.Steps[0].StepId, Is.EqualTo("step.a"));
            Assert.That(copy.Steps[0].StepId, Is.EqualTo("step.b"));
            Assert.That(source.Platform.BuildNumber, Is.EqualTo(12));
            Assert.That(copy.Platform.BuildNumber, Is.EqualTo(99));
        }

        /// <summary>
        /// 公共版本号只接受主版本.次版本.修订号格式。
        /// </summary>
        [Test]
        public void Platform_InvalidVersion_Fails()
        {
            Assert.That(
                BuildPlatformSettings.IsValidPublicVersion("1.0.0"),
                Is.True);
            Assert.That(
                BuildPlatformSettings.IsValidPublicVersion("0.1"),
                Is.False);
            Assert.That(
                BuildPlatformSettings.IsValidPublicVersion("1.0.0.0"),
                Is.False);
            Assert.That(
                BuildPlatformSettings.IsValidPublicVersion("a.b.c"),
                Is.False);
        }

        /// <summary>
        /// 应用标识必须为三段式合法包名。
        /// </summary>
        [Test]
        public void Platform_InvalidIdentifier_Fails()
        {
            Assert.That(
                BuildPlatformSettings.IsValidApplicationIdentifier("com.test.product"),
                Is.True);
            Assert.That(
                BuildPlatformSettings.IsValidApplicationIdentifier("com.test"),
                Is.False);
            Assert.That(
                BuildPlatformSettings.IsValidApplicationIdentifier("1com.test.product"),
                Is.False);
            Assert.That(
                BuildPlatformSettings.IsValidApplicationIdentifier("com..product"),
                Is.False);
        }

        /// <summary>
        /// 平台白名单仅包含构建工具明确支持的六个常用目标。
        /// </summary>
        [Test]
        public void Platform_SupportedTargets_MatchCommonTargets()
        {
            BuildTarget[] targets =
            {
                BuildTarget.StandaloneWindows64,
                BuildTarget.StandaloneLinux64,
                BuildTarget.StandaloneOSX,
                BuildTarget.Android,
                BuildTarget.iOS,
                BuildTarget.WebGL
            };

            foreach (BuildTarget target in targets)
            {
                Assert.That(
                    BuildPlatformSettings.IsSupportedTarget(target),
                    Is.True,
                    $"平台 {target} 应在构建工具白名单中。");
            }

            Assert.That(
                BuildPlatformSettings.IsSupportedTarget(BuildTarget.NoTarget),
                Is.False);
        }

        /// <summary>
        /// iOS 与 WebGL 不允许配置为 Mono，避免进入必然失败的构建流程。
        /// </summary>
        [TestCase(BuildTarget.iOS)]
        [TestCase(BuildTarget.WebGL)]
        public void Platform_Il2CppOnlyTarget_WithMono_ReportsError(
            BuildTarget target)
        {
            BuildPlatformSettings settings = CreateValidProfile().Platform;
            settings.Target = target;
            settings.ScriptingBackend = ScriptingImplementation.Mono2x;

            List<string> errors = settings.Validate();

            Assert.That(errors, Has.Some.Contains("只支持 IL2CPP"));
        }

        /// <summary>
        /// 正式档 Profile 启用调试选项时校验应报错。
        /// </summary>
        [Test]
        public void ReleaseFlavor_DebugOptions_ReportError()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();
            profile.Flavor = BuildProfileFlavor.Release;
            profile.Platform.DevelopmentBuild = true;
            profile.Platform.ScriptDebugging = true;

            List<string> errors = profile.Validate();

            Assert.That(
                errors,
                Has.Some.Contains("Development Build"));
            Assert.That(
                errors,
                Has.Some.Contains("脚本调试"));
        }

        /// <summary>
        /// 正式档推荐参数应关闭全部调试选项，并保留用户选择的脚本后端。
        /// </summary>
        [Test]
        public void ReleaseFlavorRecommendation_DisablesDebugOptions()
        {
            BuildPlatformSettings settings = new BuildPlatformSettings
            {
                ScriptingBackend = ScriptingImplementation.Mono2x,
                DevelopmentBuild = true,
                ScriptDebugging = true,
                AutoconnectProfiler = true,
                DeepProfiling = true
            };

            BuildFlavorRecommendation.Get(BuildProfileFlavor.Release).Apply(settings);

            Assert.That(settings.ScriptingBackend, Is.EqualTo(ScriptingImplementation.Mono2x));
            Assert.That(settings.DevelopmentBuild, Is.False);
            Assert.That(settings.ScriptDebugging, Is.False);
            Assert.That(settings.AutoconnectProfiler, Is.False);
            Assert.That(settings.DeepProfiling, Is.False);
        }

        /// <summary>
        /// 测试档推荐参数应启用开发构建与脚本调试但不自动连接 Profiler。
        /// </summary>
        [Test]
        public void QaFlavorRecommendation_EnablesDebuggingWithoutProfiler()
        {
            BuildPlatformSettings settings = new BuildPlatformSettings();

            BuildFlavorRecommendation.Get(BuildProfileFlavor.Qa).Apply(settings);

            Assert.That(settings.DevelopmentBuild, Is.True);
            Assert.That(settings.ScriptDebugging, Is.True);
            Assert.That(settings.AutoconnectProfiler, Is.False);
            Assert.That(settings.DeepProfiling, Is.False);
        }

        /// <summary>
        /// 开发档推荐参数应启用调试并自动连接 Profiler，Deep Profiling 仍按需开启。
        /// </summary>
        [Test]
        public void DevelopmentFlavorRecommendation_UsesFastIterationSettings()
        {
            BuildPlatformSettings settings = new BuildPlatformSettings();

            BuildFlavorRecommendation.Get(BuildProfileFlavor.Development).Apply(settings);

            Assert.That(settings.DevelopmentBuild, Is.True);
            Assert.That(settings.ScriptDebugging, Is.True);
            Assert.That(settings.AutoconnectProfiler, Is.True);
            Assert.That(settings.DeepProfiling, Is.False);
        }

        /// <summary>
        /// 推荐参数已应用后不应继续报告差异。
        /// </summary>
        [Test]
        public void FlavorRecommendation_AfterApply_HasNoChanges()
        {
            BuildPlatformSettings settings = new BuildPlatformSettings();
            BuildFlavorRecommendation recommendation =
                BuildFlavorRecommendation.Get(BuildProfileFlavor.Development);

            Assert.That(recommendation.GetChanges(settings), Is.Not.Empty);
            recommendation.Apply(settings);

            Assert.That(recommendation.GetChanges(settings), Is.Empty);
        }

        /// <summary>
        /// 输出根目录落在工程关键目录内时解析应抛异常。
        /// </summary>
        [Test]
        public void OutputRoot_ForbiddenDirectory_Throws()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();

            profile.Output.OutputRoot = "Assets";
            Assert.Throws<InvalidOperationException>(
                () => profile.Output.ResolveRootAbsolute(ProjectRoot));

            profile.Output.OutputRoot = "Packages";
            Assert.Throws<InvalidOperationException>(
                () => profile.Output.ResolveRootAbsolute(ProjectRoot));

            profile.Output.OutputRoot = "Builds";
            Assert.DoesNotThrow(
                () => profile.Output.ResolveRootAbsolute(ProjectRoot));
        }

        /// <summary>
        /// 输出目录模板占位符替换结果正确。
        /// </summary>
        [Test]
        public void OutputTemplate_Placeholders_Resolved()
        {
            BuildOutputSettings settings = new BuildOutputSettings();
            BuildOutputToken token = new BuildOutputToken(
                "Demo",
                "TestProduct",
                "Windows",
                "1.2.3",
                42,
                "IL2CPP",
                new DateTime(2026, 8, 19, 14, 30, 0));

            string directory = settings.ResolveDirectory(token);
            string fileName = settings.ResolveFileName(token);

            Assert.That(directory, Is.EqualTo("Demo/Windows/IL2CPP/1.2.3-42"));
            Assert.That(
                fileName,
                Is.EqualTo("TestProduct-Windows-1.2.3-42"));
        }

        /// <summary>
        /// 空或非法环境变量名在查询前即被拒绝。
        /// </summary>
        [Test]
        public void SecretProvider_InvalidNames_Throw()
        {
            Assert.Throws<ArgumentException>(
                () => BuildSecretProvider.ReadSecret(string.Empty));
            Assert.Throws<ArgumentException>(
                () => BuildSecretProvider.ReadSecret("1INVALID"));
        }

        /// <summary>
        /// 未设置的环境变量读取应抛出明确异常。
        /// </summary>
        [Test]
        public void SecretProvider_MissingVariable_Throws()
        {
            string uniqueName =
                "URF_UNSET_SECRET_" + Guid.NewGuid().ToString("N");
            Environment.SetEnvironmentVariable(uniqueName, null);
            try
            {
                Assert.Throws<InvalidOperationException>(
                    () => BuildSecretProvider.ReadSecret(uniqueName));
            }
            finally
            {
                Environment.SetEnvironmentVariable(uniqueName, null);
            }
        }

        /// <summary>
        /// 已设置的环境变量应返回原值，并可用于读取敏感信息。
        /// </summary>
        [Test]
        public void SecretProvider_ValidVariable_ReturnsValue()
        {
            string uniqueName =
                "URF_SET_SECRET_" + Guid.NewGuid().ToString("N");
            const string secret = "p@ssw0rd";
            Environment.SetEnvironmentVariable(uniqueName, secret);
            try
            {
                Assert.That(
                    BuildSecretProvider.ReadSecret(uniqueName),
                    Is.EqualTo(secret));
            }
            finally
            {
                Environment.SetEnvironmentVariable(uniqueName, null);
            }
        }

        /// <summary>
        /// 脱敏输出不包含原始敏感值。
        /// </summary>
        [Test]
        public void SecretProvider_Mask_DoesNotLeakValue()
        {
            Assert.That(
                BuildSecretProvider.Mask("p@ssw0rd"),
                Is.EqualTo("******"));
            Assert.That(
                BuildSecretProvider.Mask(string.Empty),
                Is.EqualTo(string.Empty));
        }
    }
}
