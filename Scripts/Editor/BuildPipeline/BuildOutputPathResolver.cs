using System;
using System.Collections.Generic;
using System.IO;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建输出路径解析器：组合输出根目录与目录模板为 Player 完整绝对路径，
    /// 并提供 Profile、平台、脚本后端与版本四维目录隔离校验。
    /// 四维隔离保证任一维度不同时产物目录不同，避免连续构建复用旧输出目录造成污染。
    /// </summary>
    public static class BuildOutputPathResolver
    {
        /// <summary>隔离校验分组。</summary>
        public const string IsolationGroup = "输出";

        /// <summary>
        /// 解析 Player 输出目录的完整绝对路径（输出根目录 + 目录模板解析结果）。
        /// 输出解析失败时返回空字符串，调用方不应假设返回值非空。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        /// <returns>Player 输出目录绝对路径；解析失败时返回空字符串。</returns>
        public static string ResolvePlayerDirectoryAbsolute(
            BuildPipelineContext context)
        {
            if (context == null
                || string.IsNullOrEmpty(context.OutputRootAbsolute)
                || string.IsNullOrEmpty(context.OutputDirectory))
            {
                return string.Empty;
            }

            return Path.GetFullPath(
                Path.Combine(context.OutputRootAbsolute, context.OutputDirectory));
        }

        /// <summary>
        /// 按输出策略校验目录模板的隔离维度：
        /// Versioned 必须包含 Profile、平台、脚本后端与版本四个占位符；
        /// Overwrite 仍要求平台与脚本后端隔离，防跨平台与 Mono/IL2CPP 误复用；
        /// Custom 不强制占位符，只保留目录边界与冲突校验。
        /// 任一必需维度缺失均按 Error 处理，阻止构建。
        /// </summary>
        /// <param name="profile">构建配置，可为空。</param>
        /// <returns>隔离问题列表；模板满足当前策略要求时返回空列表。</returns>
        public static List<BuildValidationIssue> ValidateIsolation(
            UnityRFrameworkBuildProfile profile)
        {
            List<BuildValidationIssue> issues =
                new List<BuildValidationIssue>();
            if (profile == null || profile.Output == null)
            {
                return issues;
            }

            string template = profile.Output.DirectoryTemplate;
            if (string.IsNullOrWhiteSpace(template))
            {
                issues.Add(BuildValidationIssue.Error(
                    BuildProfileValidator.OutputCode,
                    "输出目录模板不能为空。",
                    IsolationGroup));
                return issues;
            }

            switch (profile.Output.Strategy)
            {
                case BuildOutputStrategy.Overwrite:
                    ValidatePlaceholder(
                        template,
                        BuildOutputSettings.PlatformPlaceholder,
                        "不同平台会复用同一目录",
                        issues);
                    ValidatePlaceholder(
                        template,
                        BuildOutputSettings.ScriptBackendPlaceholder,
                        "Mono 与 IL2CPP 构建会复用同一目录",
                        issues);
                    break;

                case BuildOutputStrategy.Custom:
                    // 自定义模板不做占位符强制；目录边界由输出根校验保证。
                    break;

                default:
                    ValidatePlaceholder(
                        template,
                        BuildOutputSettings.ProfilePlaceholder,
                        "不同构建配置会复用同一目录",
                        issues);
                    ValidatePlaceholder(
                        template,
                        BuildOutputSettings.PlatformPlaceholder,
                        "不同平台会复用同一目录",
                        issues);
                    ValidatePlaceholder(
                        template,
                        BuildOutputSettings.ScriptBackendPlaceholder,
                        "Mono 与 IL2CPP 构建会复用同一目录",
                        issues);
                    ValidatePlaceholder(
                        template,
                        BuildOutputSettings.VersionPlaceholder,
                        "不同版本会复用同一目录",
                        issues);
                    break;
            }

            return issues;
        }

        /// <summary>
        /// 校验目录模板是否包含指定占位符；缺失时追加 Error 级隔离问题。
        /// </summary>
        /// <param name="template">输出目录模板。</param>
        /// <param name="placeholder">必需占位符。</param>
        /// <param name="reason">缺失导致的具体污染风险描述。</param>
        /// <param name="issues">追加问题条目的目标集合。</param>
        private static void ValidatePlaceholder(
            string template,
            string placeholder,
            string reason,
            ICollection<BuildValidationIssue> issues)
        {
            if (template.IndexOf(placeholder, StringComparison.Ordinal) >= 0)
            {
                return;
            }

            issues.Add(BuildValidationIssue.Error(
                BuildProfileValidator.OutputCode,
                $"输出目录模板未包含 {placeholder} 占位符，{reason}；"
                + $"请在目录模板中加入 {placeholder}。",
                IsolationGroup));
        }
    }
}
