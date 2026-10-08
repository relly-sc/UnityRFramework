using System;
using System.Collections.Generic;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建命令行参数解析结果。
    /// 命令入口只接受 Profile 定位与白名单覆盖项；敏感值（密码、密钥、令牌）
    /// 一律不允许出现在命令行中，只能通过环境变量提供（见 <see cref="BuildSecretProvider"/>）。
    /// 支持两种取值形式：空格分隔（-urfProfile 名称）与等号内联（-urfProfile=名称）。
    /// 解析为纯函数，不访问工程资源，便于单元测试。
    /// </summary>
    public sealed class BuildCommandLineArguments
    {
        /// <summary>参数键：Profile 定位（GUID、资产路径或唯一名称），必需。</summary>
        public const string ProfileArgument = "-urfProfile";

        /// <summary>参数键：覆盖输出根目录，可选。</summary>
        public const string OutputRootArgument = "-urfOutputRoot";

        /// <summary>参数键：覆盖公共版本号，可选。</summary>
        public const string VersionArgument = "-urfVersion";

        /// <summary>参数键：覆盖平台构建号，可选。</summary>
        public const string BuildNumberArgument = "-urfBuildNumber";

        /// <summary>参数键：覆盖是否 Clean Build，可选，取值 true / false。</summary>
        public const string CleanBuildArgument = "-urfCleanBuild";

        /// <summary>统一参数前缀，用于从 Unity 原生命令行参数中识别本框架参数。</summary>
        public const string ArgumentPrefix = "-urf";

        /// <summary>密码类关键字：参数键包含任一关键字即拒绝，防止敏感值进入命令行与进程列表。</summary>
        private static readonly string[] ForbiddenKeywords =
            { "password", "passwd", "pwd", "secret", "token" };

        /// <summary>Profile 定位引用：GUID、资产路径或唯一名称；未提供时为空字符串。</summary>
        public string ProfileReference { get; private set; } = string.Empty;

        /// <summary>输出根目录覆盖值；未提供时为空字符串。</summary>
        public string OutputRootOverride { get; private set; } = string.Empty;

        /// <summary>公共版本号覆盖值；未提供时为空字符串。</summary>
        public string VersionOverride { get; private set; } = string.Empty;

        /// <summary>平台构建号覆盖值；未提供时为 null。</summary>
        public int? BuildNumberOverride { get; private set; }

        /// <summary>是否 Clean Build 覆盖值；未提供时为 null。</summary>
        public bool? CleanBuildOverride { get; private set; }

        /// <summary>解析错误列表；为空表示参数合法。</summary>
        public List<string> Errors { get; } = new List<string>();

        /// <summary>参数是否合法（无任何解析错误）。</summary>
        public bool IsValid
        {
            get { return Errors.Count == 0; }
        }

        /// <summary>
        /// 解析完整命令行参数数组（含 Unity 原生参数），提取本框架参数。
        /// </summary>
        /// <param name="commandLineArgs">完整命令行参数数组，通常来自 Environment.GetCommandLineArgs。</param>
        /// <returns>解析结果；错误记录在 Errors 中。</returns>
        public static BuildCommandLineArguments Parse(string[] commandLineArgs)
        {
            BuildCommandLineArguments result = new BuildCommandLineArguments();
            if (commandLineArgs == null)
            {
                result.Errors.Add("命令行参数数组为空，无法解析构建参数。");
                return result;
            }

            HashSet<string> seenKeys =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < commandLineArgs.Length; i++)
            {
                string token = commandLineArgs[i];
                if (string.IsNullOrEmpty(token))
                {
                    continue;
                }

                // 安全铁律：任何以 '-' 开头的参数键包含密码类关键字即拒绝，
                // 敏感值只能通过环境变量提供，不允许出现在命令行与进程列表中。
                if (token.StartsWith("-", StringComparison.Ordinal))
                {
                    string keyPart = ExtractKeyPart(token);
                    if (ContainsForbiddenKeyword(keyPart))
                    {
                        result.Errors.Add(
                            $"命令行不允许出现密码类参数 '{keyPart}'，"
                            + "敏感值只能通过环境变量提供。");
                        continue;
                    }
                }

                if (!IsFrameworkToken(token))
                {
                    continue;
                }

                // 提取参数键与取值：支持 '-urfKey value' 与 '-urfKey=value' 两种形式。
                string key;
                string value;
                int separatorIndex = token.IndexOf('=');
                if (separatorIndex >= 0)
                {
                    key = token.Substring(0, separatorIndex);
                    value = token.Substring(separatorIndex + 1);
                }
                else
                {
                    key = token;
                    if (i + 1 >= commandLineArgs.Length
                        || IsFrameworkToken(commandLineArgs[i + 1]))
                    {
                        result.Errors.Add($"参数 '{key}' 缺少取值。");
                        continue;
                    }

                    value = commandLineArgs[++i];
                }

                if (!seenKeys.Add(key))
                {
                    result.Errors.Add($"参数 '{key}' 重复出现。");
                    continue;
                }

                ApplyArgument(result, key, value);
            }

            if (!seenKeys.Contains(ProfileArgument))
            {
                result.Errors.Add(
                    $"缺少必需参数 '{ProfileArgument}'，"
                    + "取值为 Profile 的 GUID、资产路径或唯一名称。");
            }

            return result;
        }

        /// <summary>
        /// 判断令牌是否为本框架参数（以 -urf 前缀开头，大小写不敏感）。
        /// </summary>
        /// <param name="token">待判断的命令行令牌。</param>
        /// <returns>是本框架参数时返回 true。</returns>
        private static bool IsFrameworkToken(string token)
        {
            return token.StartsWith(ArgumentPrefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 提取令牌的键部分：截去 '=' 之后的内容。
        /// </summary>
        /// <param name="token">原始令牌。</param>
        /// <returns>键部分；不含 '=' 时返回原令牌。</returns>
        private static string ExtractKeyPart(string token)
        {
            int separatorIndex = token.IndexOf('=');
            return separatorIndex >= 0
                ? token.Substring(0, separatorIndex)
                : token;
        }

        /// <summary>
        /// 判断参数键是否包含密码类关键字（大小写不敏感）。
        /// </summary>
        /// <param name="key">参数键。</param>
        /// <returns>包含任一关键字时返回 true。</returns>
        private static bool ContainsForbiddenKeyword(string key)
        {
            string lower = key.ToLowerInvariant();
            for (int i = 0; i < ForbiddenKeywords.Length; i++)
            {
                if (lower.Contains(ForbiddenKeywords[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 将单个参数键值应用到解析结果；未知键与非法取值记录错误。
        /// </summary>
        /// <param name="result">解析结果实例。</param>
        /// <param name="key">参数键（原始大小写）。</param>
        /// <param name="value">参数取值。</param>
        private static void ApplyArgument(
            BuildCommandLineArguments result,
            string key,
            string value)
        {
            if (string.Equals(key, ProfileArgument, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    result.Errors.Add($"参数 '{key}' 的取值不能为空。");
                    return;
                }

                result.ProfileReference = value.Trim();
                return;
            }

            if (string.Equals(key, OutputRootArgument, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    result.Errors.Add($"参数 '{key}' 的取值不能为空。");
                    return;
                }

                result.OutputRootOverride = value.Trim();
                return;
            }

            if (string.Equals(key, VersionArgument, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    result.Errors.Add($"参数 '{key}' 的取值不能为空。");
                    return;
                }

                result.VersionOverride = value.Trim();
                return;
            }

            if (string.Equals(key, BuildNumberArgument, StringComparison.OrdinalIgnoreCase))
            {
                int buildNumber;
                if (!int.TryParse(value, out buildNumber) || buildNumber <= 0)
                {
                    result.Errors.Add(
                        $"参数 '{key}' 的取值 '{value}' 非法，必须是正整数。");
                    return;
                }

                result.BuildNumberOverride = buildNumber;
                return;
            }

            if (string.Equals(key, CleanBuildArgument, StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                {
                    result.CleanBuildOverride = true;
                    return;
                }

                if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
                {
                    result.CleanBuildOverride = false;
                    return;
                }

                result.Errors.Add(
                    $"参数 '{key}' 的取值 '{value}' 非法，必须是 true 或 false。");
                return;
            }

            result.Errors.Add($"未知构建参数 '{key}'。");
        }
    }
}
