using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// 构建工具窗口状态持久化、参数差异计算与步骤可用性探测测试。
    /// 覆盖阶段 2 验收标准：窗口状态可恢复、差异可完整预览、第三方缺失不抛异常。
    /// </summary>
    public sealed class BuildWindowTests
    {
        /// <summary>测试使用的 EditorPrefs 键前缀，与 BuildWindowState 保持一致。</summary>
        private const string KeyPrefix = "UnityRFramework.BuildWindow.";

        /// <summary>
        /// 每个用例前清理窗口状态键，避免用例间相互污染。
        /// </summary>
        [SetUp]
        public void CleanStateKeys()
        {
            EditorPrefs.DeleteKey(KeyPrefix + "ProfileGuid");
            DeleteTabStateKeys();
            EditorPrefs.DeleteKey(KeyPrefix + "Folds");
            EditorPrefs.DeleteKey(KeyPrefix + "LastBuild");
        }

        /// <summary>
        /// 每个用例后同样清理窗口状态键。
        /// </summary>
        [TearDown]
        public void CleanStateKeysAfter()
        {
            EditorPrefs.DeleteKey(KeyPrefix + "ProfileGuid");
            DeleteTabStateKeys();
            EditorPrefs.DeleteKey(KeyPrefix + "Folds");
            EditorPrefs.DeleteKey(KeyPrefix + "LastBuild");
        }

        /// <summary>
        /// 窗口状态保存后新实例可完整恢复：GUID、页签、滚动位置与折叠分区。
        /// </summary>
        [Test]
        public void WindowState_SaveLoad_RoundTrip()
        {
            BuildWindowState state = new BuildWindowState();
            state.SelectedProfileGuid = "guid-123";
            state.SelectedTab = 2;
            state.TabScrollPositions[0] = new Vector2(12.5f, 88f);
            state.TabScrollPositions[1] = new Vector2(3f, 45f);
            state.TabScrollPositions[2] = new Vector2(9f, 120f);
            state.SetSectionFolded("Summary", true);
            state.Save();

            BuildWindowState loaded = new BuildWindowState();
            loaded.Load();

            Assert.That(loaded.SelectedProfileGuid, Is.EqualTo("guid-123"));
            Assert.That(loaded.SelectedTab, Is.EqualTo(2));
            Assert.That(loaded.TabScrollPositions[0], Is.EqualTo(new Vector2(12.5f, 88f)));
            Assert.That(loaded.TabScrollPositions[1], Is.EqualTo(new Vector2(3f, 45f)));
            Assert.That(loaded.TabScrollPositions[2], Is.EqualTo(new Vector2(9f, 120f)));
            Assert.That(loaded.IsSectionFolded("Summary"), Is.True);
            Assert.That(loaded.IsSectionFolded("Diffs"), Is.False);
        }

        private static void DeleteTabStateKeys()
        {
            EditorPrefs.DeleteKey(KeyPrefix + "SelectedTab");
            for (int i = 0; i < 3; i++)
            {
                string key = KeyPrefix + "TabScroll." + i;
                EditorPrefs.DeleteKey(key + ".x");
                EditorPrefs.DeleteKey(key + ".y");
            }
        }

        /// <summary>
        /// 折叠状态可反复切换并持久化。
        /// </summary>
        [Test]
        public void WindowState_FoldToggle_Persists()
        {
            BuildWindowState state = new BuildWindowState();
            state.SetSectionFolded("Diffs", true);
            state.Save();

            BuildWindowState loaded = new BuildWindowState();
            loaded.Load();
            Assert.That(loaded.IsSectionFolded("Diffs"), Is.True);

            loaded.SetSectionFolded("Diffs", false);
            loaded.Save();

            BuildWindowState reloaded = new BuildWindowState();
            reloaded.Load();
            Assert.That(reloaded.IsSectionFolded("Diffs"), Is.False);
        }

        /// <summary>
        /// 最近构建摘要可经窗口状态完整往返。
        /// </summary>
        [Test]
        public void LastBuild_ValidData_RoundTrip()
        {
            BuildWindowState state = new BuildWindowState();
            state.LastBuild.HasRecord = true;
            state.LastBuild.ProfileName = "Demo";
            state.LastBuild.Platform = "Android";
            state.LastBuild.Recipe = "Player";
            state.LastBuild.PlayerVersion = "1.0.0（构建号 42）";
            state.LastBuild.Summary = "完成";
            state.LastBuild.Status = "成功";
            state.LastBuild.DurationSeconds = 123.5f;
            state.LastBuild.OutputLabel = "Player 目录";
            state.LastBuild.OutputPath = "Builds/Demo/Android/1.0.0-42";
            state.LastBuild.TimeText = "2026-08-19 14:00";
            state.Save();

            BuildWindowState loaded = new BuildWindowState();
            loaded.Load();

            Assert.That(loaded.LastBuild.HasRecord, Is.True);
            Assert.That(loaded.LastBuild.ProfileName, Is.EqualTo("Demo"));
            Assert.That(loaded.LastBuild.Platform, Is.EqualTo("Android"));
            Assert.That(loaded.LastBuild.Recipe, Is.EqualTo("Player"));
            Assert.That(loaded.LastBuild.PlayerVersion, Is.EqualTo("1.0.0（构建号 42）"));
            Assert.That(loaded.LastBuild.Summary, Is.EqualTo("完成"));
            Assert.That(loaded.LastBuild.Status, Is.EqualTo("成功"));
            Assert.That(loaded.LastBuild.DurationSeconds, Is.EqualTo(123.5f));
            Assert.That(loaded.LastBuild.OutputLabel, Is.EqualTo("Player 目录"));
            Assert.That(loaded.LastBuild.OutputPath, Is.EqualTo("Builds/Demo/Android/1.0.0-42"));
            Assert.That(loaded.LastBuild.TimeText, Is.EqualTo("2026-08-19 14:00"));
        }

        [Test]
        public void LastBuild_HotUpdate_DoesNotShowNextPlayerBuildNumber()
        {
            BuildPipelineState state = new BuildPipelineState
            {
                TaskId = "hot-task",
                ProfileName = "Demo",
                TargetName = "StandaloneWindows64",
                Recipe = BuildRecipe.HotUpdate,
                PublicVersion = "1.2.0",
                BuildNumber = 23,
                CreatedAt = "2026-09-02T14:04:52.0728011+08:00",
                Phase = BuildPipelinePhase.Succeeded
            };
            state.CompletedSteps.Add(new BuildStepRecord
            {
                StepId = "hybridclr",
                Message = "HybridCLR 热更产物已生成：代码版本 1.0.0。"
            });

            BuildWindowLastBuild last = BuildWindowLastBuild.Create(state);

            Assert.That(last.Recipe, Is.EqualTo("热更新"));
            Assert.That(last.PlayerVersion, Is.Empty);
            Assert.That(last.Summary, Does.Contain("代码版本 1.0.0"));
            Assert.That(last.OutputLabel, Is.EqualTo("构建报告"));
            Assert.That(last.OutputPath,
                Does.Contain("2026-09-02_14-04-52-072_HotUpdate"));
            Assert.That(last.OutputPath,
                Does.EndWith(BuildReportWriter.ReportFileName));
        }

        /// <summary>
        /// 损坏的最近构建 JSON 回退到默认空记录，不抛异常。
        /// </summary>
        [Test]
        public void LastBuild_CorruptedJson_FallsBackToEmpty()
        {
            EditorPrefs.SetString(KeyPrefix + "LastBuild", "{corrupted json");

            BuildWindowState loaded = new BuildWindowState();
            loaded.Load();

            Assert.That(loaded.LastBuild.HasRecord, Is.False);
        }

        /// <summary>
        /// 合法 Profile 的差异计算覆盖全部六个分组且不抛异常。
        /// </summary>
        [Test]
        public void Diff_ValidProfile_ReturnsAllSixGroups()
        {
            UnityRFrameworkBuildProfile profile =
                ScriptableObject.CreateInstance<UnityRFrameworkBuildProfile>();
            profile.Platform.CompanyName = "TestCompany";
            profile.Platform.ProductName = "TestProduct";
            profile.Platform.ApplicationIdentifier = "com.test.product";
            profile.Platform.PublicVersion = "1.0.0";
            profile.Platform.BuildNumber = 12;
            profile.Scenes.Add(new BuildSceneEntry());
            profile.Scenes.Add(new BuildSceneEntry());

            List<BuildDiffEntry> entries = BuildSettingsDiff.Compute(profile);

            HashSet<string> groups = new HashSet<string>();
            foreach (BuildDiffEntry entry in entries)
            {
                groups.Add(entry.Group);
            }

            Assert.That(groups, Does.Contain(BuildDiffGroup.Platform));
            Assert.That(groups, Does.Contain(BuildDiffGroup.Identity));
            Assert.That(groups, Does.Contain(BuildDiffGroup.Compile));
            Assert.That(groups, Does.Contain(BuildDiffGroup.Debug));
            Assert.That(groups, Does.Contain(BuildDiffGroup.Scenes));
            Assert.That(groups, Does.Contain(BuildDiffGroup.Output));
        }

        /// <summary>
        /// 空 Profile 的差异计算返回空列表，不抛异常。
        /// </summary>
        [Test]
        public void Diff_NullProfile_ReturnsEmpty()
        {
            Assert.That(BuildSettingsDiff.Compute(null), Is.Empty);
        }

        /// <summary>
        /// 步骤友好名称：已知步骤返回中文名，未知步骤返回原 Id。
        /// </summary>
        [Test]
        public void StepAvailability_FriendlyName_KnownAndUnknown()
        {
            Assert.That(
                BuildStepAvailability.GetFriendlyName("config"),
                Is.EqualTo("Config 配置导出"));
            Assert.That(
                BuildStepAvailability.GetFriendlyName("not.a.step"),
                Is.EqualTo("not.a.step"));
        }

        /// <summary>
        /// 步骤 Id 识别：已知步骤返回 true，未知步骤返回 false。
        /// </summary>
        [Test]
        public void StepAvailability_IsKnown_Matches()
        {
            Assert.That(BuildStepAvailability.IsKnown("yooasset"), Is.True);
            Assert.That(BuildStepAvailability.IsKnown("hybridclr"), Is.True);
            Assert.That(BuildStepAvailability.IsKnown("not.a.step"), Is.False);
        }

        /// <summary>
        /// 核心步骤可用性：UnityRFramework.Editor 程序集必然已加载。
        /// </summary>
        [Test]
        public void StepAvailability_CoreSteps_AreAvailable()
        {
            Assert.That(BuildStepAvailability.IsAvailable("config"), Is.True);
        }

        /// <summary>
        /// 未知步骤判定为不可用并给出原因，不抛反射异常。
        /// </summary>
        [Test]
        public void StepAvailability_UnknownStep_UnavailableWithReason()
        {
            Assert.That(BuildStepAvailability.IsAvailable("not.a.step"), Is.False);
            StringAssert.Contains(
                "没有已加载的实现",
                BuildStepAvailability.GetUnavailableReason("not.a.step"));
        }

        /// <summary>
        /// 已知但未安装第三方的步骤给出“未导入”原因。
        /// </summary>
        [Test]
        public void StepAvailability_MissingThirdParty_HasImportHint()
        {
            string reason = BuildStepAvailability.GetUnavailableReason("yooasset");
            if (!BuildStepAvailability.IsAvailable("yooasset"))
            {
                StringAssert.Contains("未导入", reason);
            }
        }
    }
}
