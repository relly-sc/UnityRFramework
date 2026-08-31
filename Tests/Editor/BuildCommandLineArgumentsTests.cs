using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// 构建命令行参数解析、Profile 定位、任务覆盖与退出码映射的单元测试。
    /// </summary>
    public sealed class BuildCommandLineArgumentsTests
    {
        /// <summary>
        /// 参数数组为 null 时应判定非法并记录错误。
        /// </summary>
        [Test]
        public void Parse_NullArray_IsInvalid()
        {
            BuildCommandLineArguments result =
                BuildCommandLineArguments.Parse(null);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Is.Not.Empty);
        }

        /// <summary>
        /// 空参数数组应报缺少 -urfProfile 错误。
        /// </summary>
        [Test]
        public void Parse_EmptyArray_ReportsMissingProfile()
        {
            BuildCommandLineArguments result =
                BuildCommandLineArguments.Parse(new string[0]);

            Assert.That(result.IsValid, Is.False);
            Assert.That(
                result.Errors,
                Has.Some.Contain("-urfProfile"));
        }

        /// <summary>
        /// 只提供 Profile 参数时应合法且无覆盖项。
        /// </summary>
        [Test]
        public void Parse_ProfileOnly_IsValid()
        {
            BuildCommandLineArguments result =
                BuildCommandLineArguments.Parse(new[] { "-urfProfile", "WindowsRelease" });

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.ProfileReference, Is.EqualTo("WindowsRelease"));
            Assert.That(result.OutputRootOverride, Is.Empty);
            Assert.That(result.VersionOverride, Is.Empty);
            Assert.That(result.BuildNumberOverride, Is.Null);
            Assert.That(result.CleanBuildOverride, Is.Null);
        }

        /// <summary>
        /// 完整参数应正确解析，且忽略 Unity 原生参数。
        /// </summary>
        [Test]
        public void Parse_AllOverrides_IgnoresUnityArguments()
        {
            BuildCommandLineArguments result = BuildCommandLineArguments.Parse(new[]
            {
                "Unity.exe",
                "-batchmode",
                "-projectPath",
                "D:/Project",
                "-executeMethod",
                "UnityRFramework.Editor.UnityRFrameworkBuildCommand.ExecuteFromCommandLine",
                "-urfProfile",
                "WindowsRelease",
                "-urfOutputRoot",
                "D:/CiBuilds",
                "-urfVersion",
                "2.1.0",
                "-urfBuildNumber",
                "42",
                "-urfCleanBuild",
                "true"
            });

            Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.ProfileReference, Is.EqualTo("WindowsRelease"));
            Assert.That(result.OutputRootOverride, Is.EqualTo("D:/CiBuilds"));
            Assert.That(result.VersionOverride, Is.EqualTo("2.1.0"));
            Assert.That(result.BuildNumberOverride, Is.EqualTo(42));
            Assert.That(result.CleanBuildOverride, Is.True);
        }

        /// <summary>
        /// 等号内联形式（-urfKey=value）应正确解析。
        /// </summary>
        [Test]
        public void Parse_InlineEqualsForm_IsValid()
        {
            BuildCommandLineArguments result = BuildCommandLineArguments.Parse(new[]
            {
                "-urfProfile=WindowsRelease",
                "-urfBuildNumber=7",
                "-urfCleanBuild=false"
            });

            Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.ProfileReference, Is.EqualTo("WindowsRelease"));
            Assert.That(result.BuildNumberOverride, Is.EqualTo(7));
            Assert.That(result.CleanBuildOverride, Is.False);
        }

        /// <summary>
        /// 参数键缺少取值时应报错误。
        /// </summary>
        [Test]
        public void Parse_MissingValue_ReportsError()
        {
            BuildCommandLineArguments result = BuildCommandLineArguments.Parse(new[]
            {
                "-urfProfile",
                "-urfCleanBuild"
            });

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Contain("缺少取值"));
        }

        /// <summary>
        /// 同一参数键重复出现时应报错误。
        /// </summary>
        [Test]
        public void Parse_DuplicateKey_ReportsError()
        {
            BuildCommandLineArguments result = BuildCommandLineArguments.Parse(new[]
            {
                "-urfProfile",
                "A",
                "-urfProfile",
                "B"
            });

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Contain("重复出现"));
        }

        /// <summary>
        /// 未知的 -urf 前缀参数应报错误。
        /// </summary>
        [Test]
        public void Parse_UnknownFrameworkKey_ReportsError()
        {
            BuildCommandLineArguments result = BuildCommandLineArguments.Parse(new[]
            {
                "-urfProfile",
                "A",
                "-urfMagic",
                "x"
            });

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Contain("未知构建参数"));
        }

        /// <summary>
        /// 参数键包含 password 关键字时应被拒绝。
        /// </summary>
        [Test]
        public void Parse_PasswordLikeKey_IsRejected()
        {
            BuildCommandLineArguments result = BuildCommandLineArguments.Parse(new[]
            {
                "-urfProfile",
                "Android",
                "-urfKeystorePassword",
                "abc123"
            });

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Contain("密码类参数"));
        }

        /// <summary>
        /// 参数键包含 token / secret 关键字时应被拒绝（内联形式同样生效）。
        /// </summary>
        [Test]
        public void Parse_SecretKeywordInKey_IsRejected()
        {
            BuildCommandLineArguments tokenResult = BuildCommandLineArguments.Parse(new[]
            {
                "-urfProfile",
                "A",
                "-urfApiToken=value"
            });
            BuildCommandLineArguments secretResult = BuildCommandLineArguments.Parse(new[]
            {
                "-urfProfile",
                "A",
                "-urfClientSecret",
                "value"
            });

            Assert.That(tokenResult.IsValid, Is.False);
            Assert.That(tokenResult.Errors, Has.Some.Contain("密码类参数"));
            Assert.That(secretResult.IsValid, Is.False);
            Assert.That(secretResult.Errors, Has.Some.Contain("密码类参数"));
        }

        /// <summary>
        /// 构建号取值非法（非整数、零、负数）时应报错误。
        /// </summary>
        [Test]
        public void Parse_InvalidBuildNumber_ReportsError()
        {
            string[] invalidValues = { "abc", "0", "-3" };
            foreach (string value in invalidValues)
            {
                BuildCommandLineArguments result = BuildCommandLineArguments.Parse(new[]
                {
                    "-urfProfile",
                    "A",
                    "-urfBuildNumber",
                    value
                });

                Assert.That(result.IsValid, Is.False, $"取值 '{value}' 应判非法。");
                Assert.That(result.Errors, Has.Some.Contain("必须是正整数"));
            }
        }

        /// <summary>
        /// Clean Build 取值非法时应报错误。
        /// </summary>
        [Test]
        public void Parse_InvalidCleanBuildValue_ReportsError()
        {
            BuildCommandLineArguments result = BuildCommandLineArguments.Parse(new[]
            {
                "-urfProfile",
                "A",
                "-urfCleanBuild",
                "yes"
            });

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Contain("必须是 true 或 false"));
        }

        /// <summary>
        /// Profile 取值为空白时应报错误。
        /// </summary>
        [Test]
        public void Parse_EmptyProfileValue_ReportsError()
        {
            BuildCommandLineArguments result = BuildCommandLineArguments.Parse(new[]
            {
                "-urfProfile",
                "   "
            });

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Contain("取值不能为空"));
        }

        /// <summary>
        /// Clean Build 取值应大小写不敏感。
        /// </summary>
        [Test]
        public void Parse_CleanBuildValue_CaseInsensitive()
        {
            BuildCommandLineArguments trueResult = BuildCommandLineArguments.Parse(new[]
            {
                "-urfProfile",
                "A",
                "-urfCleanBuild",
                "TRUE"
            });
            BuildCommandLineArguments falseResult = BuildCommandLineArguments.Parse(new[]
            {
                "-urfProfile",
                "A",
                "-urfCleanBuild",
                "False"
            });

            Assert.That(trueResult.IsValid, Is.True, string.Join("; ", trueResult.Errors));
            Assert.That(trueResult.CleanBuildOverride, Is.True);
            Assert.That(falseResult.IsValid, Is.True, string.Join("; ", falseResult.Errors));
            Assert.That(falseResult.CleanBuildOverride, Is.False);
        }

        /// <summary>
        /// 定位引用为空时应返回 null 并给出错误。
        /// </summary>
        [Test]
        public void ResolveProfile_EmptyReference_FailsWithError()
        {
            string error;
            UnityRFrameworkBuildProfile profile =
                UnityRFrameworkBuildCommand.ResolveProfile(string.Empty, out error);

            Assert.That(profile, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        /// <summary>
        /// 按资产路径应能定位临时 Profile。
        /// </summary>
        [Test]
        public void ResolveProfile_ByAssetPath_Loads()
        {
            UnityRFrameworkBuildProfile created = CreateTempProfile();
            try
            {
                string path = AssetDatabase.GetAssetPath(created);
                string error;
                UnityRFrameworkBuildProfile profile =
                    UnityRFrameworkBuildCommand.ResolveProfile(path, out error);

                Assert.That(profile, Is.SameAs(created));
                Assert.That(error, Is.Empty);
            }
            finally
            {
                DeleteTempProfile(created);
            }
        }

        /// <summary>
        /// 按 GUID 应能定位临时 Profile。
        /// </summary>
        [Test]
        public void ResolveProfile_ByGuid_Loads()
        {
            UnityRFrameworkBuildProfile created = CreateTempProfile();
            try
            {
                string guid = BuildProfileEditorUtility.GetGuid(created);
                Assert.That(guid, Is.Not.Empty);
                string error;
                UnityRFrameworkBuildProfile profile =
                    UnityRFrameworkBuildCommand.ResolveProfile(guid, out error);

                Assert.That(profile, Is.SameAs(created));
                Assert.That(error, Is.Empty);
            }
            finally
            {
                DeleteTempProfile(created);
            }
        }

        /// <summary>
        /// 按唯一名称应能定位临时 Profile（大小写不敏感）。
        /// </summary>
        [Test]
        public void ResolveProfile_ByName_Loads()
        {
            UnityRFrameworkBuildProfile created = CreateTempProfile();
            try
            {
                string error;
                UnityRFrameworkBuildProfile profile =
                    UnityRFrameworkBuildCommand.ResolveProfile(
                        created.name.ToLowerInvariant(),
                        out error);

                Assert.That(profile, Is.SameAs(created));
                Assert.That(error, Is.Empty);
            }
            finally
            {
                DeleteTempProfile(created);
            }
        }

        /// <summary>
        /// 名称不存在时应返回 null 并给出错误。
        /// </summary>
        [Test]
        public void ResolveProfile_UnknownName_Fails()
        {
            string error;
            UnityRFrameworkBuildProfile profile =
                UnityRFrameworkBuildCommand.ResolveProfile(
                    "No_Such_Profile_Name",
                    out error);

            Assert.That(profile, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        /// <summary>
        /// 资产路径不存在时应返回 null 并给出错误。
        /// </summary>
        [Test]
        public void ResolveProfile_MissingAssetPath_Fails()
        {
            string error;
            UnityRFrameworkBuildProfile profile =
                UnityRFrameworkBuildCommand.ResolveProfile(
                    "Assets/UnityRFramework/BuildProfiles/Missing.profile",
                    out error);

            Assert.That(profile, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        /// <summary>
        /// GUID 格式正确但资产不存在时应返回 null 并给出错误。
        /// </summary>
        [Test]
        public void ResolveProfile_UnknownGuid_Fails()
        {
            string error;
            UnityRFrameworkBuildProfile profile =
                UnityRFrameworkBuildCommand.ResolveProfile(
                    "0123456789abcdef0123456789abcdef",
                    out error);

            Assert.That(profile, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }

        /// <summary>
        /// 任务覆盖只写入有效副本，源 Profile 保持不变。
        /// </summary>
        [Test]
        public void TaskOverrides_CreateEffectiveProfile_DoesNotModifySource()
        {
            UnityRFrameworkBuildProfile profile = CreateStandaloneProfile();
            profile.Output.OutputRoot = "Builds";
            profile.Platform.PublicVersion = "1.2.3";
            profile.Platform.BuildNumber = 10;
            profile.Output.CleanBeforeBuild = false;
            BuildCommandLineArguments arguments = BuildCommandLineArguments.Parse(new[]
            {
                "-urfProfile",
                "P",
                "-urfOutputRoot",
                "D:/CiBuilds",
                "-urfVersion",
                "9.9.9",
                "-urfBuildNumber",
                "55",
                "-urfCleanBuild",
                "true"
            });
            Assert.That(arguments.IsValid, Is.True, string.Join("; ", arguments.Errors));

            BuildTaskOverrides taskOverrides =
                BuildTaskOverrides.FromArguments(arguments);
            UnityRFrameworkBuildProfile effective =
                taskOverrides.CreateEffectiveProfile(profile);

            Assert.That(profile.Output.OutputRoot, Is.EqualTo("Builds"));
            Assert.That(profile.Platform.PublicVersion, Is.EqualTo("1.2.3"));
            Assert.That(profile.Platform.BuildNumber, Is.EqualTo(10));
            Assert.That(profile.Output.CleanBeforeBuild, Is.False);
            Assert.That(effective.Output.OutputRoot, Is.EqualTo("D:/CiBuilds"));
            Assert.That(effective.Platform.PublicVersion, Is.EqualTo("9.9.9"));
            Assert.That(effective.Platform.BuildNumber, Is.EqualTo(55));
            Assert.That(effective.Output.CleanBeforeBuild, Is.True);

            UnityEngine.Object.DestroyImmediate(effective);
        }

        /// <summary>
        /// 无覆盖时仍返回独立副本，避免任务步骤修改源 Profile。
        /// </summary>
        [Test]
        public void TaskOverrides_NoOverrides_ReturnsIndependentCopy()
        {
            UnityRFrameworkBuildProfile profile = CreateStandaloneProfile();
            profile.Output.OutputRoot = "Builds";
            profile.Platform.BuildNumber = 3;

            BuildCommandLineArguments arguments =
                BuildCommandLineArguments.Parse(new[] { "-urfProfile", "P" });
            BuildTaskOverrides taskOverrides =
                BuildTaskOverrides.FromArguments(arguments);
            UnityRFrameworkBuildProfile effective =
                taskOverrides.CreateEffectiveProfile(profile);

            Assert.That(effective, Is.Not.SameAs(profile));
            Assert.That(effective.Output.OutputRoot, Is.EqualTo("Builds"));
            effective.Platform.BuildNumber = 99;
            Assert.That(profile.Platform.BuildNumber, Is.EqualTo(3));

            UnityEngine.Object.DestroyImmediate(effective);
        }

        /// <summary>
        /// 执行结果应正确映射为退出码：成功 0、取消 4、失败 3。
        /// </summary>
        [Test]
        public void MapResultToExitCode_MapsAllOutcomes()
        {
            BuildRunResult success = new BuildRunResult(true, false, "ok", null);
            BuildRunResult cancelled = new BuildRunResult(false, true, "cancel", null);
            BuildRunResult failed = new BuildRunResult(false, false, "fail", null);

            Assert.That(
                UnityRFrameworkBuildCommand.MapResultToExitCode(success),
                Is.EqualTo(BuildCommandExitCodes.Success));
            Assert.That(
                UnityRFrameworkBuildCommand.MapResultToExitCode(cancelled),
                Is.EqualTo(BuildCommandExitCodes.Cancelled));
            Assert.That(
                UnityRFrameworkBuildCommand.MapResultToExitCode(failed),
                Is.EqualTo(BuildCommandExitCodes.BuildFailure));
        }

        /// <summary>
        /// 执行结果为 null 时应映射为构建失败退出码。
        /// </summary>
        [Test]
        public void MapResultToExitCode_Null_ReturnsBuildFailure()
        {
            Assert.That(
                UnityRFrameworkBuildCommand.MapResultToExitCode(null),
                Is.EqualTo(BuildCommandExitCodes.BuildFailure));
        }

        /// <summary>
        /// 创建未保存的独立 Profile 实例，用于任务覆盖测试。
        /// </summary>
        /// <returns>独立 Profile 实例。</returns>
        private static UnityRFrameworkBuildProfile CreateStandaloneProfile()
        {
            return ScriptableObject.CreateInstance<UnityRFrameworkBuildProfile>();
        }

        /// <summary>
        /// 创建临时 Profile 资产用于定位测试。
        /// </summary>
        /// <returns>已保存的临时 Profile。</returns>
        private static UnityRFrameworkBuildProfile CreateTempProfile()
        {
            string name = "CmdArgsTest_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            return BuildProfileEditorUtility.CreateProfile(name);
        }

        /// <summary>
        /// 删除临时 Profile 资产。
        /// </summary>
        /// <param name="profile">临时 Profile。</param>
        private static void DeleteTempProfile(UnityRFrameworkBuildProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(profile);
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
