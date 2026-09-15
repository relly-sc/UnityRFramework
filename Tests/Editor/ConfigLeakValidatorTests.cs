using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>正式发布配置泄漏检查测试。</summary>
    public sealed class ConfigLeakValidatorTests
    {
        private const string TempRoot = "Assets/Temp/UnityRFrameworkConfigLeakTests";
        private const string ConfigOutput = TempRoot + "/Resources/Config";
        private const string ConfigSource = TempRoot + "/Resources/ConfigSource";
        private const string StreamingRoot = "Assets/StreamingAssets/UnityRFrameworkConfigLeakTests";
        private const string SceneRoot = TempRoot + "/SceneDependency";
        private const string YooCollectedRoot =
            "Assets/UnityRFramework/Samples/Expansion.YooAsset.Demo/Acceptance/"
            + "GameAssets/YooAsset/Raw/ConfigLeakTests";

        [SetUp]
        public void SetUp()
        {
            DeleteTestAssets();
        }

        [TearDown]
        public void TearDown()
        {
            DeleteTestAssets();
        }

        /// <summary>正式构建发现开发 JSON、配置源和私钥文件时必须报告错误及路径。</summary>
        [Test]
        public void ReleaseBuildReportsIncludedConfigLeaksAsErrors()
        {
            CreateAsset(ConfigOutput + "/Json/Item.json", "{\"id\":1}");
            CreateAsset(ConfigSource + "/Item.csv", "id,name\n1,test");
            CreateAsset(StreamingRoot + "/test-private-key.pem", "-----BEGIN PRIVATE KEY-----");
            CreateAsset(
                StreamingRoot + "/embedded-secret.txt",
                "-----BEGIN RSA PRIVATE KEY-----");

            using (ValidationFixture fixture = CreateFixture(false))
            {
                List<BuildValidationIssue> issues = RunValidator(fixture);

                Assert.That(issues, Has.Some.Matches<BuildValidationIssue>(
                    issue => issue.Level == BuildValidationLevel.Error
                        && issue.Message.Contains("Item.json")));
                Assert.That(issues, Has.Some.Matches<BuildValidationIssue>(
                    issue => issue.Message.Contains("Item.csv")));
                Assert.That(issues, Has.Some.Matches<BuildValidationIssue>(
                    issue => issue.Message.Contains("test-private-key.pem")));
                Assert.That(issues, Has.Some.Matches<BuildValidationIssue>(
                    issue => issue.Message.Contains("embedded-secret.txt")));
            }
        }

        /// <summary>Development Build 仍报告证据，但不得因泄漏检查阻止构建。</summary>
        [Test]
        public void DevelopmentBuildReportsIncludedConfigLeaksAsWarnings()
        {
            CreateAsset(ConfigOutput + "/Json/Item.json", "{\"id\":1}");

            using (ValidationFixture fixture = CreateFixture(true))
            {
                List<BuildValidationIssue> issues = RunValidator(fixture);

                Assert.That(issues, Has.Some.Matches<BuildValidationIssue>(
                    issue => issue.Level == BuildValidationLevel.Warning
                        && issue.Message.Contains("Item.json")));
                Assert.That(issues, Has.None.Matches<BuildValidationIssue>(
                    issue => issue.Level == BuildValidationLevel.Error));
            }
        }

        /// <summary>白名单目录只排除目录内文件，不影响其他泄漏证据。</summary>
        [Test]
        public void AllowedPathExcludesOnlyMatchingLeak()
        {
            string allowedPath = ConfigOutput + "/Json/Allowed.json";
            string blockedPath = ConfigOutput + "/Json/Blocked.json";
            CreateAsset(allowedPath, "{\"allowed\":true}");
            CreateAsset(blockedPath, "{\"blocked\":true}");

            using (ValidationFixture fixture = CreateFixture(false))
            {
                AddAllowedPath(fixture.Configuration, allowedPath);
                List<BuildValidationIssue> issues = RunValidator(fixture);

                Assert.That(issues, Has.None.Matches<BuildValidationIssue>(
                    issue => issue.Message.Contains("Allowed.json")));
                Assert.That(issues, Has.Some.Matches<BuildValidationIssue>(
                    issue => issue.Message.Contains("Blocked.json")));
            }
        }

        /// <summary>当前 Recipe 会先清理开发 JSON 时，不应在清理步骤前阻断构建。</summary>
        [Test]
        public void ReleaseRecipeIgnoresJsonThatConfigStepWillRemove()
        {
            CreateAsset(ConfigOutput + "/Json/Generated.json", "{\"generated\":true}");

            using (ValidationFixture fixture = CreateFixture(false))
            {
                fixture.Configuration.ExportJson = false;
                fixture.Profile.Recipe = BuildRecipe.Release;
                List<BuildValidationIssue> issues = RunValidator(fixture);

                Assert.That(issues, Has.None.Matches<BuildValidationIssue>(
                    issue => issue.Message.Contains("Generated.json")));
            }
        }

        /// <summary>只构建 Player 不执行 Config 清理时，关闭 JSON 导出也必须报告已有残留。</summary>
        [Test]
        public void PlayerRecipeReportsJsonBecauseConfigStepWillNotRun()
        {
            CreateAsset(ConfigOutput + "/Json/Stale.json", "{\"stale\":true}");

            using (ValidationFixture fixture = CreateFixture(false))
            {
                fixture.Configuration.ExportJson = false;
                fixture.Profile.Recipe = BuildRecipe.Player;
                List<BuildValidationIssue> issues = RunValidator(fixture);

                Assert.That(issues, Has.Some.Matches<BuildValidationIssue>(
                    issue => issue.Level == BuildValidationLevel.Error
                        && issue.Message.Contains("Stale.json")));
            }
        }

        /// <summary>启用场景的递归依赖会加入待检查路径。</summary>
        [Test]
        public void EnabledSceneDependencyIsChecked()
        {
            string jsonPath = SceneRoot + "/Config/Json/SceneLeak.json";
            CreateAsset(jsonPath, "{\"scene\":true}");
            string[] sceneGuids = AssetDatabase.FindAssets("t:SceneAsset");
            Assert.That(sceneGuids, Is.Not.Empty);
            SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                AssetDatabase.GUIDToAssetPath(sceneGuids[0]));
            UnityRFrameworkBuildProfile profile =
                ScriptableObject.CreateInstance<UnityRFrameworkBuildProfile>();
            try
            {
                profile.Scenes.Add(new BuildSceneEntry
                {
                    Enabled = true,
                    Scene = scene
                });
                HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                ConfigLeakScanner.AddSceneDependencies(
                    profile,
                    paths,
                    _ => new[] { jsonPath });

                Assert.That(paths, Does.Contain(jsonPath));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        /// <summary>安装 YooAsset 时检查当前 Package 的实际收集结果。</summary>
        [Test]
        public void YooAssetCollectedConfigSourceIsCheckedWhenPluginExists()
        {
            if (FindType("YooAsset.Editor.BundleCollectorSettingData") == null
                || !AssetDatabase.IsValidFolder(Path.GetDirectoryName(YooCollectedRoot)?.Replace('\\', '/')))
            {
                Assert.Ignore("当前项目未安装 YooAsset 或未导入对应验收 Package。");
            }

            string csvPath = YooCollectedRoot + "/CollectedLeak.csv";
            CreateAsset(csvPath, "id,name\n1,collected");
            TestYooAssetBuildConfiguration yoo =
                ScriptableObject.CreateInstance<TestYooAssetBuildConfiguration>();
            try
            {
                using (ValidationFixture fixture = CreateFixture(false))
                {
                    fixture.Configuration.Options.ConfigSourceDirectory = YooCollectedRoot;
                    yoo.PackageName = "ExpansionAcceptancePackage";
                    fixture.Profile.Steps.Add(new BuildStepSettings
                    {
                        StepId = "yooasset",
                        Enabled = true,
                        Configuration = yoo
                    });

                    List<BuildValidationIssue> issues = RunValidator(fixture);

                    Assert.That(issues, Has.Some.Matches<BuildValidationIssue>(
                        issue => issue.Message.Contains("CollectedLeak.csv")));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(yoo);
            }
        }

        private static ValidationFixture CreateFixture(bool developmentBuild)
        {
            UnityRFrameworkBuildProfile profile =
                ScriptableObject.CreateInstance<UnityRFrameworkBuildProfile>();
            profile.Platform.DevelopmentBuild = developmentBuild;
            profile.Flavor = developmentBuild
                ? BuildProfileFlavor.Development
                : BuildProfileFlavor.Release;

            ConfigExportBuildConfiguration configuration =
                ScriptableObject.CreateInstance<ConfigExportBuildConfiguration>();
            configuration.Options.ConfigSourceDirectory = ConfigSource;
            configuration.Options.LocalizationSourceDirectory = TempRoot + "/LocalizationSource";
            configuration.Options.ConfigOutputDirectory = ConfigOutput;
            configuration.Options.LocalizationOutputDirectory = TempRoot + "/Resources/Localization";
            SetBoolean(configuration, "ReleaseLeakCheck", true);
            SetBoolean(configuration, "BlockReleaseBuildOnLeak", true);

            profile.Steps.Add(new BuildStepSettings
            {
                StepId = "config",
                Enabled = true,
                Configuration = configuration
            });
            return new ValidationFixture(profile, configuration);
        }

        private static List<BuildValidationIssue> RunValidator(ValidationFixture fixture)
        {
            Type validatorType = typeof(BuildProfileValidator).Assembly.GetType(
                "UnityRFramework.Editor.ConfigLeakValidator");
            Assert.NotNull(validatorType, "ConfigLeakValidator 尚未实现。");
            IBuildValidator validator = (IBuildValidator)Activator.CreateInstance(validatorType);
            List<BuildValidationIssue> issues = new List<BuildValidationIssue>();
            validator.Validate(
                new BuildValidationContext(
                    fixture.Profile,
                    EditorUserBuildSettings.activeBuildTarget,
                    Directory.GetParent(Application.dataPath)?.FullName,
                    string.Empty,
                    string.Empty,
                    string.Empty),
                issues);
            return issues;
        }

        private static void SetBoolean(ScriptableObject target, string propertyName, bool value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            Assert.NotNull(property, $"缺少配置字段 {propertyName}。");
            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddAllowedPath(ScriptableObject target, string assetPath)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty("LeakCheckAllowedPaths");
            Assert.NotNull(property, "缺少泄漏检查白名单字段。");
            property.arraySize++;
            property.GetArrayElementAtIndex(property.arraySize - 1).stringValue = assetPath;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateAsset(string assetPath, string content)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            string fullPath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, content);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void DeleteTestAssets()
        {
            AssetDatabase.DeleteAsset(TempRoot);
            AssetDatabase.DeleteAsset(StreamingRoot);
            AssetDatabase.DeleteAsset(YooCollectedRoot);
        }

        private static Type FindType(string fullName)
        {
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private sealed class ValidationFixture : IDisposable
        {
            public ValidationFixture(
                UnityRFrameworkBuildProfile profile,
                ConfigExportBuildConfiguration configuration)
            {
                Profile = profile;
                Configuration = configuration;
            }

            public UnityRFrameworkBuildProfile Profile { get; }

            public ConfigExportBuildConfiguration Configuration { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Configuration);
                UnityEngine.Object.DestroyImmediate(Profile);
            }
        }
    }

    /// <summary>避免核心测试程序集直接引用 YooAsset 编辑器程序集。</summary>
    public sealed class TestYooAssetBuildConfiguration : ScriptableObject
    {
        public string PackageName = string.Empty;
    }
}
