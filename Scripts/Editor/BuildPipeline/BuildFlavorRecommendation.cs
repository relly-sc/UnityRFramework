using System;
using System.Collections.Generic;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建分档的推荐调试参数。分档切换本身不修改 Profile，
    /// 只有用户显式应用推荐参数后才写入当前 Profile。
    /// </summary>
    public sealed class BuildFlavorRecommendation
    {
        private BuildFlavorRecommendation(
            bool developmentBuild,
            bool scriptDebugging,
            bool autoconnectProfiler,
            bool deepProfiling)
        {
            DevelopmentBuild = developmentBuild;
            ScriptDebugging = scriptDebugging;
            AutoconnectProfiler = autoconnectProfiler;
            DeepProfiling = deepProfiling;
        }

        /// <summary>是否启用 Development Build。</summary>
        public bool DevelopmentBuild { get; }

        /// <summary>是否启用脚本调试。</summary>
        public bool ScriptDebugging { get; }

        /// <summary>是否自动连接 Profiler。</summary>
        public bool AutoconnectProfiler { get; }

        /// <summary>是否启用 Deep Profiling。</summary>
        public bool DeepProfiling { get; }

        /// <summary>
        /// 获取指定分档的推荐参数。Deep Profiling 对性能影响明显，所有分档均默认关闭，
        /// 需要专项分析时由用户手动启用。
        /// </summary>
        /// <param name="flavor">构建用途分档。</param>
        /// <returns>对应分档的推荐参数。</returns>
        public static BuildFlavorRecommendation Get(BuildProfileFlavor flavor)
        {
            switch (flavor)
            {
                case BuildProfileFlavor.Qa:
                    return new BuildFlavorRecommendation(
                        true,
                        true,
                        false,
                        false);
                case BuildProfileFlavor.Development:
                    return new BuildFlavorRecommendation(
                        true,
                        true,
                        true,
                        false);
                default:
                    return new BuildFlavorRecommendation(
                        false,
                        false,
                        false,
                        false);
            }
        }

        /// <summary>
        /// 收集当前平台参数与推荐参数之间的差异，用于应用前确认。
        /// </summary>
        /// <param name="settings">当前平台参数。</param>
        /// <returns>逐项差异文本；无差异时为空列表。</returns>
        public IReadOnlyList<string> GetChanges(BuildPlatformSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            List<string> changes = new List<string>();
            AddChange(
                changes,
                "Development Build",
                settings.DevelopmentBuild,
                DevelopmentBuild);
            AddChange(
                changes,
                "Script Debugging",
                settings.ScriptDebugging,
                ScriptDebugging);
            AddChange(
                changes,
                "Autoconnect Profiler",
                settings.AutoconnectProfiler,
                AutoconnectProfiler);
            AddChange(
                changes,
                "Deep Profiling",
                settings.DeepProfiling,
                DeepProfiling);
            return changes;
        }

        /// <summary>
        /// 将推荐参数写入平台设置。该方法只修改数据模型，不写入 Unity PlayerSettings。
        /// </summary>
        /// <param name="settings">待修改的平台参数。</param>
        public void Apply(BuildPlatformSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.DevelopmentBuild = DevelopmentBuild;
            settings.ScriptDebugging = ScriptDebugging;
            settings.AutoconnectProfiler = AutoconnectProfiler;
            settings.DeepProfiling = DeepProfiling;
        }

        private static void AddChange<T>(
            ICollection<string> changes,
            string fieldName,
            T current,
            T recommended)
        {
            if (EqualityComparer<T>.Default.Equals(current, recommended))
            {
                return;
            }

            changes.Add($"{fieldName}: {current} -> {recommended}");
        }
    }
}
