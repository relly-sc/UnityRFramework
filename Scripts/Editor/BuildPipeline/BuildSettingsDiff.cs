using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 差异条目级别，决定差异列表的展示颜色。
    /// </summary>
    public enum BuildDiffLevel
    {
        /// <summary>信息：仅展示，无需处理。</summary>
        Info,

        /// <summary>警告：值得注意但不阻止构建。</summary>
        Warning,

        /// <summary>不一致：Profile 与当前项目参数不同，应用参数后消除。</summary>
        Mismatch
    }

    /// <summary>
    /// 差异分组常量，窗口按分组折叠展示。
    /// </summary>
    public static class BuildDiffGroup
    {
        /// <summary>平台分组：活动构建目标。</summary>
        public const string Platform = "平台";

        /// <summary>标识分组：公司名、产品名与应用标识。</summary>
        public const string Identity = "标识";

        /// <summary>编译分组：脚本后端、兼容级别与裁剪等编译参数。</summary>
        public const string Compile = "编译";

        /// <summary>调试分组：Development Build 与调试相关开关。</summary>
        public const string Debug = "调试";

        /// <summary>场景分组：Profile 场景与 Build Settings 场景的差异。</summary>
        public const string Scenes = "场景";

        /// <summary>输出分组：输出根目录与清理策略。</summary>
        public const string Output = "输出";
    }

    /// <summary>
    /// 单条参数差异描述：当前项目值、Profile 期望值与差异级别。
    /// </summary>
    public readonly struct BuildDiffEntry
    {
        /// <summary>
        /// 创建差异条目。
        /// </summary>
        /// <param name="group">分组名，见 <see cref="BuildDiffGroup"/>。</param>
        /// <param name="label">字段显示名。</param>
        /// <param name="current">当前 PlayerSettings 值。</param>
        /// <param name="expected">Profile 期望值。</param>
        /// <param name="level">差异级别。</param>
        public BuildDiffEntry(
            string group,
            string label,
            string current,
            string expected,
            BuildDiffLevel level)
        {
            Group = group ?? string.Empty;
            Label = label ?? string.Empty;
            Current = current ?? string.Empty;
            Expected = expected ?? string.Empty;
            Level = level;
        }

        /// <summary>获取差异分组名。</summary>
        public string Group { get; }

        /// <summary>获取字段显示名。</summary>
        public string Label { get; }

        /// <summary>获取当前 PlayerSettings 值。</summary>
        public string Current { get; }

        /// <summary>获取 Profile 期望值。</summary>
        public string Expected { get; }

        /// <summary>获取差异级别。</summary>
        public BuildDiffLevel Level { get; }
    }

    /// <summary>
    /// 计算当前 PlayerSettings 与 Profile 期望参数的差异列表。
    /// 仅读取项目当前状态，不修改任何参数，供窗口在构建前预览差异。
    /// </summary>
    public static class BuildSettingsDiff
    {
        /// <summary>
        /// 计算 Profile 与当前项目参数的完整差异列表。
        /// </summary>
        /// <param name="profile">目标构建 Profile。</param>
        /// <returns>按分组聚合的差异条目列表；Profile 为 null 时返回空列表。</returns>
        public static List<BuildDiffEntry> Compute(UnityRFrameworkBuildProfile profile)
        {
            List<BuildDiffEntry> entries = new List<BuildDiffEntry>();
            if (profile == null)
            {
                return entries;
            }

            BuildTargetGroup group =
                BuildPipeline.GetBuildTargetGroup(profile.Platform.Target);

            ComputePlatform(profile, entries);
            ComputeIdentity(profile, group, entries);
            ComputeCompile(profile, group, entries);
            ComputeDebug(profile, entries);
            ComputeScenes(profile, entries);
            ComputeOutput(profile, entries);

            return entries;
        }

        /// <summary>
        /// 计算平台分组差异：活动构建目标与 Profile 目标平台。
        /// </summary>
        /// <param name="profile">目标 Profile。</param>
        /// <param name="entries">追加差异条目的目标列表。</param>
        private static void ComputePlatform(
            UnityRFrameworkBuildProfile profile,
            List<BuildDiffEntry> entries)
        {
            BuildTarget active = EditorUserBuildSettings.activeBuildTarget;
            BuildTarget expected = profile.Platform.Target;

            entries.Add(new BuildDiffEntry(
                BuildDiffGroup.Platform,
                "活动构建目标",
                active.ToString(),
                expected.ToString(),
                active == expected ? BuildDiffLevel.Info : BuildDiffLevel.Mismatch));
        }

        /// <summary>
        /// 计算标识分组差异：公司名、产品名与应用标识。
        /// </summary>
        /// <param name="profile">目标 Profile。</param>
        /// <param name="group">Profile 目标平台对应的 BuildTargetGroup。</param>
        /// <param name="entries">追加差异条目的目标列表。</param>
        private static void ComputeIdentity(
            UnityRFrameworkBuildProfile profile,
            BuildTargetGroup group,
            List<BuildDiffEntry> entries)
        {
            AddTextDiff(
                entries,
                BuildDiffGroup.Identity,
                "公司名",
                PlayerSettings.companyName,
                profile.Platform.CompanyName);

            AddTextDiff(
                entries,
                BuildDiffGroup.Identity,
                "产品名",
                PlayerSettings.productName,
                profile.Platform.ProductName);

            string currentIdentifier =
                PlayerSettings.GetApplicationIdentifier(group);
            AddTextDiff(
                entries,
                BuildDiffGroup.Identity,
                "应用标识",
                currentIdentifier,
                profile.Platform.ApplicationIdentifier);
        }

        /// <summary>
        /// 计算编译分组差异：脚本后端、兼容级别、裁剪与 IL2CPP 参数。
        /// </summary>
        /// <param name="profile">目标 Profile。</param>
        /// <param name="group">Profile 目标平台对应的 BuildTargetGroup。</param>
        /// <param name="entries">追加差异条目的目标列表。</param>
        private static void ComputeCompile(
            UnityRFrameworkBuildProfile profile,
            BuildTargetGroup group,
            List<BuildDiffEntry> entries)
        {
            AddTextDiff(
                entries,
                BuildDiffGroup.Compile,
                "脚本后端",
                PlayerSettings.GetScriptingBackend(group).ToString(),
                profile.Platform.ScriptingBackend.ToString());

            AddTextDiff(
                entries,
                BuildDiffGroup.Compile,
                "API 兼容级别",
                PlayerSettings.GetApiCompatibilityLevel(group).ToString(),
                profile.Platform.ApiCompatibilityLevel.ToString());

            AddTextDiff(
                entries,
                BuildDiffGroup.Compile,
                "托管裁剪级别",
                PlayerSettings.GetManagedStrippingLevel(group).ToString(),
                profile.Platform.ManagedStrippingLevel.ToString());

            NamedBuildTarget namedTarget = NamedBuildTarget.FromBuildTargetGroup(group);
            AddTextDiff(
                entries,
                BuildDiffGroup.Compile,
                "IL2CPP 代码生成",
                PlayerSettings.GetIl2CppCodeGeneration(namedTarget).ToString(),
                profile.Platform.Il2CppCodeGeneration.ToString());

            AddTextDiff(
                entries,
                BuildDiffGroup.Compile,
                "IL2CPP 编译器配置",
                PlayerSettings.GetIl2CppCompilerConfiguration(group).ToString(),
                profile.Platform.CppCompilerConfiguration.ToString());

            AddTextDiff(
                entries,
                BuildDiffGroup.Compile,
                "增量式 GC",
                PlayerSettings.gcIncremental.ToString(),
                profile.Platform.IncrementalGC.ToString());
        }

        /// <summary>
        /// 计算调试分组差异：Development Build 与调试开关。
        /// </summary>
        /// <param name="profile">目标 Profile。</param>
        /// <param name="entries">追加差异条目的目标列表。</param>
        private static void ComputeDebug(
            UnityRFrameworkBuildProfile profile,
            List<BuildDiffEntry> entries)
        {
            AddBoolDiff(
                entries,
                BuildDiffGroup.Debug,
                "Development Build",
                EditorUserBuildSettings.development,
                profile.Platform.DevelopmentBuild);

            AddBoolDiff(
                entries,
                BuildDiffGroup.Debug,
                "脚本调试",
                EditorUserBuildSettings.allowDebugging,
                profile.Platform.ScriptDebugging);

            AddBoolDiff(
                entries,
                BuildDiffGroup.Debug,
                "自动连接 Profiler",
                EditorUserBuildSettings.connectProfiler,
                profile.Platform.AutoconnectProfiler);

            AddBoolDiff(
                entries,
                BuildDiffGroup.Debug,
                "Deep Profiling",
                EditorUserBuildSettings.buildWithDeepProfilingSupport,
                profile.Platform.DeepProfiling);
        }

        /// <summary>
        /// 计算场景分组差异：Profile 场景列表与 Build Settings 启用场景的差异。
        /// </summary>
        /// <param name="profile">目标 Profile。</param>
        /// <param name="entries">追加差异条目的目标列表。</param>
        private static void ComputeScenes(
            UnityRFrameworkBuildProfile profile,
            List<BuildDiffEntry> entries)
        {
            HashSet<string> profilePaths =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int enabledCount = 0;
            foreach (BuildSceneEntry entry in profile.Scenes)
            {
                if (entry == null || !entry.Enabled)
                {
                    continue;
                }

                enabledCount++;
                string path = entry.ResolvePath();
                if (!string.IsNullOrEmpty(path))
                {
                    profilePaths.Add(path);
                }
            }

            HashSet<string> buildSettingsPaths =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene != null && scene.enabled)
                {
                    buildSettingsPaths.Add(scene.path);
                }
            }

            entries.Add(new BuildDiffEntry(
                BuildDiffGroup.Scenes,
                "Profile 启用场景数",
                enabledCount.ToString(),
                profile.Scenes.Count.ToString(),
                BuildDiffLevel.Info));

            bool countsMatch = buildSettingsPaths.Count == enabledCount;
            entries.Add(new BuildDiffEntry(
                BuildDiffGroup.Scenes,
                "Build Settings 启用场景数",
                buildSettingsPaths.Count.ToString(),
                enabledCount.ToString(),
                countsMatch ? BuildDiffLevel.Info : BuildDiffLevel.Mismatch));

            foreach (string path in profilePaths)
            {
                if (!buildSettingsPaths.Contains(path))
                {
                    entries.Add(new BuildDiffEntry(
                        BuildDiffGroup.Scenes,
                        "缺失于 Build Settings",
                        "（未加入）",
                        path,
                        BuildDiffLevel.Mismatch));
                }
            }

            foreach (string path in buildSettingsPaths)
            {
                if (!profilePaths.Contains(path))
                {
                    entries.Add(new BuildDiffEntry(
                        BuildDiffGroup.Scenes,
                        "不在 Profile 中",
                        path,
                        "（未列入）",
                        BuildDiffLevel.Warning));
                }
            }
        }

        /// <summary>
        /// 计算输出分组差异：输出根目录解析结果与清理策略。
        /// </summary>
        /// <param name="profile">目标 Profile。</param>
        /// <param name="entries">追加差异条目的目标列表。</param>
        private static void ComputeOutput(
            UnityRFrameworkBuildProfile profile,
            List<BuildDiffEntry> entries)
        {
            string rootAbsolute;
            try
            {
                rootAbsolute = profile.Output.ResolveRootAbsolute(GetProjectRoot());
            }
            catch (Exception exception)
            {
                entries.Add(new BuildDiffEntry(
                    BuildDiffGroup.Output,
                    "输出根目录",
                    "（校验失败）",
                    exception.Message,
                    BuildDiffLevel.Warning));
                return;
            }

            entries.Add(new BuildDiffEntry(
                BuildDiffGroup.Output,
                "输出根目录",
                rootAbsolute,
                profile.Output.OutputRoot,
                BuildDiffLevel.Info));

            bool exists = Directory.Exists(rootAbsolute);
            entries.Add(new BuildDiffEntry(
                BuildDiffGroup.Output,
                "根目录存在",
                exists ? "存在" : "不存在",
                "构建前自动创建",
                exists ? BuildDiffLevel.Info : BuildDiffLevel.Info));

            if (profile.Output.CleanBeforeBuild)
            {
                entries.Add(new BuildDiffEntry(
                    BuildDiffGroup.Output,
                    "构建前清理",
                    "启用",
                    "目标目录将被清空后写入新产物",
                    BuildDiffLevel.Warning));
            }
        }

        /// <summary>
        /// 追加一条字符串差异；相等时记为 Info，不等时记为 Mismatch。
        /// </summary>
        /// <param name="entries">追加差异条目的目标列表。</param>
        /// <param name="group">分组名。</param>
        /// <param name="label">字段显示名。</param>
        /// <param name="current">当前值。</param>
        /// <param name="expected">期望值。</param>
        private static void AddTextDiff(
            List<BuildDiffEntry> entries,
            string group,
            string label,
            string current,
            string expected)
        {
            bool same = string.Equals(current, expected, StringComparison.Ordinal);
            entries.Add(new BuildDiffEntry(
                group,
                label,
                DisplayText(current),
                DisplayText(expected),
                same ? BuildDiffLevel.Info : BuildDiffLevel.Mismatch));
        }

        /// <summary>
        /// 追加一条布尔差异；相等时记为 Info，不等时记为 Mismatch。
        /// </summary>
        /// <param name="entries">追加差异条目的目标列表。</param>
        /// <param name="group">分组名。</param>
        /// <param name="label">字段显示名。</param>
        /// <param name="current">当前值。</param>
        /// <param name="expected">期望值。</param>
        private static void AddBoolDiff(
            List<BuildDiffEntry> entries,
            string group,
            string label,
            bool current,
            bool expected)
        {
            AddTextDiff(entries, group, label, current.ToString(), expected.ToString());
        }

        /// <summary>
        /// 空值显示为占位文本，避免差异列表出现空白行。
        /// </summary>
        /// <param name="value">原始值。</param>
        /// <returns>非空值原样返回；空值返回“（空）”。</returns>
        private static string DisplayText(string value)
        {
            return string.IsNullOrEmpty(value) ? "（空）" : value;
        }

        /// <summary>
        /// 获取 Unity 工程根目录绝对路径。
        /// </summary>
        /// <returns>工程根目录，以分隔符结尾。</returns>
        private static string GetProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }
    }
}
