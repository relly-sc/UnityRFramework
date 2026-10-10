using System;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 单次构建任务的命令行覆盖值。字段随任务状态持久化，恢复时重新生成
    /// 有效 Profile 副本；覆盖值不会写回源 Profile 资产。
    /// </summary>
    [Serializable]
    public sealed class BuildTaskOverrides
    {
        /// <summary>是否覆盖输出根目录。</summary>
        public bool HasOutputRoot;

        /// <summary>输出根目录覆盖值。</summary>
        public string OutputRoot = string.Empty;

        /// <summary>是否覆盖公共版本号。</summary>
        public bool HasPublicVersion;

        /// <summary>公共版本号覆盖值。</summary>
        public string PublicVersion = string.Empty;

        /// <summary>是否覆盖构建号。</summary>
        public bool HasBuildNumber;

        /// <summary>构建号覆盖值。</summary>
        public int BuildNumber;

        /// <summary>是否覆盖 Clean Build。</summary>
        public bool HasCleanBuild;

        /// <summary>Clean Build 覆盖值。</summary>
        public bool CleanBuild;

        /// <summary>是否包含任意任务覆盖。</summary>
        public bool HasAny
        {
            get
            {
                return HasOutputRoot
                    || HasPublicVersion
                    || HasBuildNumber
                    || HasCleanBuild;
            }
        }

        /// <summary>
        /// 从命令行解析结果创建任务覆盖。
        /// </summary>
        /// <param name="arguments">已解析的命令行参数。</param>
        /// <returns>可持久化任务覆盖。</returns>
        public static BuildTaskOverrides FromArguments(
            BuildCommandLineArguments arguments)
        {
            if (arguments == null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            return new BuildTaskOverrides
            {
                HasOutputRoot = !string.IsNullOrEmpty(arguments.OutputRootOverride),
                OutputRoot = arguments.OutputRootOverride ?? string.Empty,
                HasPublicVersion = !string.IsNullOrEmpty(arguments.VersionOverride),
                PublicVersion = arguments.VersionOverride ?? string.Empty,
                HasBuildNumber = arguments.BuildNumberOverride.HasValue,
                BuildNumber = arguments.BuildNumberOverride.GetValueOrDefault(),
                HasCleanBuild = arguments.CleanBuildOverride.HasValue,
                CleanBuild = arguments.CleanBuildOverride.GetValueOrDefault()
            };
        }

        /// <summary>
        /// 克隆源 Profile 并应用本任务覆盖。源资产及其嵌套设置不被修改。
        /// </summary>
        /// <param name="source">源 Profile。</param>
        /// <returns>仅供本任务使用的内存副本。</returns>
        public UnityRFrameworkBuildProfile CreateEffectiveProfile(
            UnityRFrameworkBuildProfile source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            UnityRFrameworkBuildProfile effective =
                UnityEngine.Object.Instantiate(source);
            effective.name = source.name;
            effective.hideFlags = HideFlags.HideAndDontSave;

            if (HasOutputRoot)
            {
                effective.Output.OutputRoot = OutputRoot;
            }
            if (HasPublicVersion)
            {
                effective.Platform.PublicVersion = PublicVersion;
            }
            if (HasBuildNumber)
            {
                effective.Platform.BuildNumber = BuildNumber;
            }
            if (HasCleanBuild)
            {
                effective.Output.CleanBeforeBuild = CleanBuild;
            }

            return effective;
        }
    }
}
