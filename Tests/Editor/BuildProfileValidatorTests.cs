using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// 测试用第三方校验器：仅追加 Warning，验证注册表自动发现与主校验器合并执行。
    /// 放在测试程序集中，Id 带 test 前缀标识用途。
    /// </summary>
    public sealed class PassThroughValidator : IBuildValidator
    {
        /// <summary>获取校验器唯一 Id。</summary>
        public string Id
        {
            get
            {
                return "test.pass-through";
            }
        }

        /// <summary>
        /// 向集合追加一条测试用 Warning。
        /// </summary>
        /// <param name="context">校验上下文。</param>
        /// <param name="issues">追加问题条目的目标集合。</param>
        public void Validate(
            BuildValidationContext context,
            ICollection<BuildValidationIssue> issues)
        {
            issues.Add(BuildValidationIssue.Warning(
                "TEST",
                "测试校验器已执行。",
                BuildProfileValidator.GroupThirdParty));
        }
    }

    /// <summary>
    /// 构建前校验与参数应用测试。
    /// 覆盖阶段 3 验收标准：所有可预知错误在 BuildPlayer 前失败、校验失败不改变任何状态。
    /// </summary>
    public sealed class BuildProfileValidatorTests
    {
        /// <summary>测试用工程根目录绝对路径。</summary>
        private static string ProjectRoot =>
            Path.GetDirectoryName(Application.dataPath) ?? string.Empty;

        /// <summary>干净工程状态：无播放、无编译、无导入、无构建。</summary>
        private static BuildEnvironmentState CleanState =>
            new BuildEnvironmentState
            {
                ActiveTarget = EditorUserBuildSettings.activeBuildTarget
            };

        /// <summary>
        /// 创建一份填充了合法标识与平台的测试 Profile，并从项目资产库加载第一个场景。
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
        /// 每次测试前使校验器注册表缓存失效，避免跨测试残留。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            BuildValidatorRegistry.InvalidateCache();
        }

        /// <summary>
        /// null Profile 校验应返回 Error 且不可构建。
        /// </summary>
        [Test]
        public void NullProfile_Validate_ReturnsErrorAndCannotBuild()
        {
            BuildValidationResult result =
                BuildProfileValidator.Validate(null, CleanState);

            Assert.That(result.CanBuild, Is.False);
            Assert.That(result.Errors, Is.Not.Empty);
        }

        /// <summary>
        /// 合法 Profile 在干净工程状态下校验应可构建。
        /// </summary>
        [Test]
        public void ValidProfile_Validate_CanBuild()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();

            BuildValidationResult result =
                BuildProfileValidator.Validate(profile, CleanState);

            Assert.That(result.CanBuild, Is.True);
            Assert.That(result.Errors, Is.Empty);
        }

        /// <summary>
        /// 启用资源健康检查后，合法 Profile 仍应可构建（健康问题只报 Warning）。
        /// </summary>
        [Test]
        public void ValidProfile_IncludeAssetHealth_StillCanBuild()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();

            BuildValidationResult result =
                BuildProfileValidator.Validate(profile, CleanState, true);

            Assert.That(result.CanBuild, Is.True);
        }

        /// <summary>
        /// 场景列表为空时校验应报 Error。
        /// </summary>
        [Test]
        public void Profile_NoEnabledScene_ReportsError()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();
            profile.Scenes.Clear();

            BuildValidationResult result =
                BuildProfileValidator.Validate(profile, CleanState);

            Assert.That(result.CanBuild, Is.False);
            Assert.That(
                result.Errors,
                Has.Some.Property("Code").EqualTo(
                    BuildProfileValidator.ProfileCode));
        }

        /// <summary>
        /// Android 架构不含 ARM64 时应报 Error。
        /// </summary>
        [Test]
        public void AndroidProfile_MissingArm64_ReportsError()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();
            profile.Platform.Target = BuildTarget.Android;
            profile.Platform.AndroidArchitecture = AndroidArchitecture.ARMv7;

            BuildValidationResult result =
                BuildProfileValidator.Validate(profile, CleanState);

            Assert.That(result.CanBuild, Is.False);
            Assert.That(
                result.Errors,
                Has.Some.Property("Code").EqualTo(
                    BuildProfileValidator.AndroidCode));
        }

        /// <summary>
        /// Android 配置了 Keystore 但密码环境变量缺失时应报 Error，且描述不包含密码本体。
        /// </summary>
        [Test]
        public void AndroidProfile_WithKeystoreMissingEnvVar_ReportsError()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();
            profile.Platform.Target = BuildTarget.Android;
            profile.Platform.AndroidArchitecture = AndroidArchitecture.ARM64;
            profile.Platform.AndroidKeystoreName = "keystore.keystore";
            profile.Platform.AndroidKeystoreAlias = "alias";
            profile.Platform.AndroidKeystorePassEnvVar =
                "URF_TEST_MISSING_KS_" + Guid.NewGuid().ToString("N");
            profile.Platform.AndroidKeyAliasPassEnvVar =
                "URF_TEST_MISSING_ALIAS_" + Guid.NewGuid().ToString("N");

            BuildValidationResult result =
                BuildProfileValidator.Validate(profile, CleanState);

            Assert.That(result.CanBuild, Is.False);
            Assert.That(
                result.Errors,
                Has.Some.Property("Code").EqualTo(
                    BuildProfileValidator.AndroidCode));
            for (int i = 0; i < result.Errors.Count; i++)
            {
                string message = result.Errors[i].Message;
                Assert.That(
                    message,
                    Does.Not.Contain("=secret="),
                    "校验报告不得包含密码本体。");
                Assert.That(
                    message,
                    Does.Not.Contain(profile.Platform.AndroidKeystoreAlias + "@"),
                    "校验报告不得包含密钥派生内容。");
            }
        }

        /// <summary>
        /// Windows 输出文件名带非法扩展名时应报 Warning 且不阻止构建。
        /// </summary>
        [Test]
        public void WindowsProfile_InvalidExtension_ReportsWarning()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();
            profile.Platform.Target = BuildTarget.StandaloneWindows64;
            profile.Output.FileNameTemplate = "{ProductName}-bad.dll";

            BuildValidationResult result =
                BuildProfileValidator.Validate(profile, CleanState);

            Assert.That(result.CanBuild, Is.True);
            Assert.That(
                result.Warnings,
                Has.Some.Property("Code").EqualTo(
                    BuildProfileValidator.OutputCode));
        }

        /// <summary>
        /// Android 输出 AAB 但文件名模板扩展名为 .apk 时应报 Error。
        /// </summary>
        [Test]
        public void AndroidProfile_AabButApkExtension_ReportsError()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();
            profile.Platform.Target = BuildTarget.Android;
            profile.Platform.AndroidArchitecture = AndroidArchitecture.ARM64;
            profile.Platform.AndroidBuildAppBundle = true;
            profile.Output.FileNameTemplate = "{ProductName}.apk";

            BuildValidationResult result =
                BuildProfileValidator.Validate(profile, CleanState);

            Assert.That(result.CanBuild, Is.False);
            Assert.That(
                result.Errors,
                Has.Some.Property("Code").EqualTo(
                    BuildProfileValidator.OutputCode));
        }

        /// <summary>
        /// Android 目标且版本号含点号时，默认文件名模板（无显式扩展名）
        /// 不应把版本号中的点误判为扩展名而报 Error。
        /// 回归用例：修复前 Path.GetExtension 会把
        /// "TestProduct-Android-1.0.0-12" 的 ".0-12" 误判为扩展名。
        /// </summary>
        [Test]
        public void AndroidProfile_DefaultTemplateWithDottedVersion_NoExtensionError()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();
            profile.Platform.Target = BuildTarget.Android;
            profile.Platform.AndroidArchitecture = AndroidArchitecture.ARM64;

            BuildValidationResult result =
                BuildProfileValidator.Validate(profile, CleanState);

            Assert.That(result.CanBuild, Is.True);
            Assert.That(
                result.Errors,
                Has.None.Property("Code").EqualTo(
                    BuildProfileValidator.OutputCode));
        }

        /// <summary>
        /// iOS 文件名模板带扩展名时应报 Warning 且不阻止构建（仅生成 Xcode 工程）。
        /// </summary>
        [Test]
        public void IosProfile_WithFileExtension_ReportsWarning()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();
            profile.Platform.Target = BuildTarget.iOS;
            profile.Output.FileNameTemplate = "{ProductName}.ipa";

            BuildValidationResult result =
                BuildProfileValidator.Validate(profile, CleanState);

            Assert.That(result.CanBuild, Is.True);
            Assert.That(
                result.Warnings,
                Has.Some.Property("Code").EqualTo(
                    BuildProfileValidator.OutputCode));
        }

        /// <summary>
        /// 目录模板缺少脚本后端与版本占位符时应报 Error 并阻止构建。
        /// 阶段 7 起四维隔离为静态强校验：任一维度缺失即不允许复用目录，
        /// 不再依赖目标目录已存在这一运行时条件。
        /// </summary>
        [Test]
        public void Profile_DirectoryTemplate_MissingIsolationPlaceholders_ReportsError()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();
            profile.Platform.Target = BuildTarget.StandaloneWindows64;
            profile.Output.DirectoryTemplate = "{Profile}/{Platform}";

            BuildValidationResult result =
                BuildProfileValidator.Validate(profile, CleanState);

            Assert.That(result.CanBuild, Is.False);
            Assert.That(
                result.Errors,
                Has.Some.Property("Code").EqualTo(
                    BuildProfileValidator.OutputCode));
            Assert.That(
                result.Errors,
                Has.Some.Property("Message").Contains(
                    BuildOutputSettings.ScriptBackendPlaceholder));
            Assert.That(
                result.Errors,
                Has.Some.Property("Message").Contains(
                    BuildOutputSettings.VersionPlaceholder));
        }

        /// <summary>
        /// 目录模板包含全部四个隔离占位符时不应报输出隔离 Error。
        /// </summary>
        [Test]
        public void Profile_DirectoryTemplate_FullIsolationPlaceholders_NoOutputError()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();
            profile.Platform.Target = BuildTarget.StandaloneWindows64;
            profile.Output.DirectoryTemplate =
                "{Profile}/{Platform}/{ScriptBackend}/{Version}-{BuildNumber}";

            BuildValidationResult result =
                BuildProfileValidator.Validate(profile, CleanState);

            Assert.That(result.CanBuild, Is.True);
            Assert.That(
                result.Errors,
                Has.None.Property("Code").EqualTo(
                    BuildProfileValidator.OutputCode));
        }

        /// <summary>
        /// 宏定义含空格或分号时应报 Error。
        /// </summary>
        [Test]
        public void Profile_InvalidDefineSymbol_ReportsError()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();
            profile.Platform.DefineSymbols.Add("BAD MACRO");

            BuildValidationResult result =
                BuildProfileValidator.Validate(profile, CleanState);

            Assert.That(result.CanBuild, Is.False);
            Assert.That(
                result.Errors,
                Has.Some.Property("Code").EqualTo(
                    BuildProfileValidator.DefineCode));
        }

        /// <summary>
        /// 宏定义含空条目时应报 Warning 且不阻止构建。
        /// </summary>
        [Test]
        public void Profile_EmptyDefineSymbol_ReportsWarning()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();
            profile.Platform.DefineSymbols.Add(string.Empty);

            BuildValidationResult result =
                BuildProfileValidator.Validate(profile, CleanState);

            Assert.That(result.CanBuild, Is.True);
            Assert.That(
                result.Warnings,
                Has.Some.Property("Code").EqualTo(
                    BuildProfileValidator.DefineCode));
        }

        /// <summary>
        /// 处于 Play Mode 时校验应报 Error。
        /// </summary>
        [Test]
        public void Environment_Playing_ReportsError()
        {
            BuildEnvironmentState state = CleanState;
            state.IsPlaying = true;

            BuildValidationResult result =
                BuildProfileValidator.Validate(CreateValidProfile(), state);

            Assert.That(result.CanBuild, Is.False);
            Assert.That(
                result.Errors,
                Has.Some.Property("Code").EqualTo(
                    BuildProfileValidator.EnvironmentCode));
        }

        /// <summary>
        /// 正在编译脚本时校验应报 Error。
        /// </summary>
        [Test]
        public void Environment_Compiling_ReportsError()
        {
            BuildEnvironmentState state = CleanState;
            state.IsCompiling = true;

            BuildValidationResult result =
                BuildProfileValidator.Validate(CreateValidProfile(), state);

            Assert.That(result.CanBuild, Is.False);
            Assert.That(
                result.Errors,
                Has.Some.Property("Code").EqualTo(
                    BuildProfileValidator.EnvironmentCode));
        }

        /// <summary>
        /// 正在导入资源时校验应报 Error。
        /// </summary>
        [Test]
        public void Environment_Updating_ReportsError()
        {
            BuildEnvironmentState state = CleanState;
            state.IsUpdating = true;

            BuildValidationResult result =
                BuildProfileValidator.Validate(CreateValidProfile(), state);

            Assert.That(result.CanBuild, Is.False);
            Assert.That(
                result.Errors,
                Has.Some.Property("Code").EqualTo(
                    BuildProfileValidator.EnvironmentCode));
        }

        /// <summary>
        /// 已有构建任务进行中时校验应报 Error。
        /// </summary>
        [Test]
        public void Environment_BuildingPlayer_ReportsError()
        {
            BuildEnvironmentState state = CleanState;
            state.IsBuildingPlayer = true;

            BuildValidationResult result =
                BuildProfileValidator.Validate(CreateValidProfile(), state);

            Assert.That(result.CanBuild, Is.False);
            Assert.That(
                result.Errors,
                Has.Some.Property("Code").EqualTo(
                    BuildProfileValidator.EnvironmentCode));
        }

        /// <summary>
        /// 注册表应自动发现测试程序集中的校验器，并合并执行其校验结果。
        /// </summary>
        [Test]
        public void Registry_DiscoversAndRunsTestValidator()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();

            BuildValidationResult result =
                BuildProfileValidator.Validate(profile, CleanState);

            Assert.That(
                result.Warnings,
                Has.Some.Property("Code").EqualTo("TEST"));
        }

        /// <summary>
        /// 注册表缓存失效后可重新扫描并仍返回稳定结果。
        /// </summary>
        [Test]
        public void Registry_InvalidateCache_StillReturnsValidators()
        {
            BuildValidatorRegistry.InvalidateCache();

            IReadOnlyList<IBuildValidator> validators =
                BuildValidatorRegistry.GetAll();

            Assert.That(validators, Is.Not.Null);
        }

        /// <summary>
        /// null Profile 应用参数应失败。
        /// </summary>
        [Test]
        public void Apply_NullProfile_ReturnsFailure()
        {
            BuildApplyResult result = BuildProfileApplier.Apply(null);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors, Is.Not.Empty);
        }

        /// <summary>
        /// 校验失败时应用参数应拒绝执行，且不改变任何 PlayerSettings 或场景。
        /// </summary>
        [Test]
        public void Apply_InvalidProfile_ReturnsFailureWithoutSideEffects()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();
            profile.Platform.CompanyName = string.Empty;

            string oldCompany = PlayerSettings.companyName;
            string oldProduct = PlayerSettings.productName;
            string oldVersion = PlayerSettings.bundleVersion;
            EditorBuildSettingsScene[] oldScenes = EditorBuildSettings.scenes;

            BuildApplyResult result = BuildProfileApplier.Apply(profile);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors, Is.Not.Empty);
            Assert.That(PlayerSettings.companyName, Is.EqualTo(oldCompany));
            Assert.That(PlayerSettings.productName, Is.EqualTo(oldProduct));
            Assert.That(PlayerSettings.bundleVersion, Is.EqualTo(oldVersion));
            Assert.That(
                GetSceneStatePaths(EditorBuildSettings.scenes),
                Is.EqualTo(GetSceneStatePaths(oldScenes)));
        }

        /// <summary>
        /// 提取场景列表的路径与启用状态描述，用于内容比较（EditorBuildSettings 每次返回新实例）。
        /// </summary>
        /// <param name="scenes">场景列表。</param>
        /// <returns>形如 "路径:启用" 的描述数组。</returns>
        private static string[] GetSceneStatePaths(
            EditorBuildSettingsScene[] scenes)
        {
            string[] result = new string[scenes.Length];
            for (int i = 0; i < scenes.Length; i++)
            {
                result[i] = $"{scenes[i].path}:{scenes[i].enabled}";
            }

            return result;
        }

        /// <summary>
        /// 合法 Profile 应用参数应成功并产出脱敏报告；完成后还原标识字段避免污染工程。
        /// 目标平台固定为 Windows x86_64，避免跟随活动平台引入与其他测试的顺序耦合。
        /// </summary>
        [Test]
        public void Apply_ValidProfile_SucceedsAndReportsRedacted()
        {
            UnityRFrameworkBuildProfile profile = CreateValidProfile();
            profile.Platform.Target = BuildTarget.StandaloneWindows64;

            string oldCompany = PlayerSettings.companyName;
            string oldProduct = PlayerSettings.productName;
            string oldVersion = PlayerSettings.bundleVersion;
            EditorBuildSettingsScene[] oldScenes = EditorBuildSettings.scenes;
            bool oldDevelopment = EditorUserBuildSettings.development;
            bool oldAllowDebugging = EditorUserBuildSettings.allowDebugging;
            bool oldConnectProfiler = EditorUserBuildSettings.connectProfiler;
            bool oldDeepProfiling =
                EditorUserBuildSettings.buildWithDeepProfilingSupport;
            FullScreenMode oldFullscreen = PlayerSettings.fullScreenMode;

            try
            {
                BuildApplyResult result = BuildProfileApplier.Apply(profile);

                string diagnostic = result.Errors.Count > 0
                    ? string.Join(" | ", result.Errors)
                    : "（无错误）报告："
                        + string.Join(" | ", result.ReportLines);
                Assert.That(
                    result.Succeeded,
                    Is.True,
                    $"应用应成功；实际失败原因：{diagnostic}");
                Assert.That(result.ReportLines, Is.Not.Empty);
            }
            finally
            {
                PlayerSettings.companyName = oldCompany;
                PlayerSettings.productName = oldProduct;
                PlayerSettings.bundleVersion = oldVersion;
                EditorBuildSettings.scenes = oldScenes;
                EditorUserBuildSettings.development = oldDevelopment;
                EditorUserBuildSettings.allowDebugging = oldAllowDebugging;
                EditorUserBuildSettings.connectProfiler = oldConnectProfiler;
                EditorUserBuildSettings.buildWithDeepProfilingSupport =
                    oldDeepProfiling;
                PlayerSettings.fullScreenMode = oldFullscreen;
            }
        }
    }
}
