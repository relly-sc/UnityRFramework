using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建输出目录与文件名配置，包含占位符模板解析与目录边界校验。
    /// 输出根目录禁止落在 Assets、Packages 或项目关键源码目录，防止污染工程。
    /// </summary>
    [Serializable]
    public sealed class BuildOutputSettings
    {
        /// <summary>{Profile} 占位符：Profile 资产名称。</summary>
        public const string ProfilePlaceholder = "{Profile}";

        /// <summary>{ProductName} 占位符：产品名称。</summary>
        public const string ProductNamePlaceholder = "{ProductName}";

        /// <summary>{Platform} 占位符：平台显示名称。</summary>
        public const string PlatformPlaceholder = "{Platform}";

        /// <summary>{Version} 占位符：公共版本号。</summary>
        public const string VersionPlaceholder = "{Version}";

        /// <summary>{BuildNumber} 占位符：平台构建号。</summary>
        public const string BuildNumberPlaceholder = "{BuildNumber}";

        /// <summary>{ScriptBackend} 占位符：脚本后端名称。</summary>
        public const string ScriptBackendPlaceholder = "{ScriptBackend}";

        /// <summary>{Date} 占位符：构建日期，格式 yyyyMMdd。</summary>
        public const string DatePlaceholder = "{Date}";

        /// <summary>{Time} 占位符：构建时刻，格式 HHmmss。</summary>
        public const string TimePlaceholder = "{Time}";

        /// <summary>禁止作为输出根目录的顶层目录集合（相对项目根）。</summary>
        private static readonly HashSet<string> ForbiddenRootDirectories =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Assets",
                "Packages",
                "ProjectSettings",
                "Library",
                "Temp",
                "Logs"
            };

        /// <summary>输出根目录，相对项目根目录解析；禁止落在 Assets、Packages 等关键目录。</summary>
        [Tooltip("输出根目录，相对项目根目录解析。")]
        public string OutputRoot = "Builds";

        /// <summary>Player 输出目录模板，支持 Profile、Platform、ScriptBackend、Version、BuildNumber 等占位符。
        /// 模板必须包含 Profile、Platform、ScriptBackend 与 Version 四个隔离维度占位符，
        /// 保证不同配置、平台、脚本后端或版本不会复用同一输出目录。</summary>
        [Tooltip("Player 输出目录模板，必须包含 Profile、Platform、ScriptBackend、Version 占位符。")]
        public string DirectoryTemplate =
            "{Profile}/{Platform}/{ScriptBackend}/{Version}-{BuildNumber}";

        /// <summary>输出文件名模板，Windows 可执行文件与 Android 安装包使用。</summary>
        [Tooltip("输出文件名模板。")]
        public string FileNameTemplate =
            "{ProductName}-{Platform}-{Version}-{BuildNumber}";

        /// <summary>是否在构建前清理目标目录；清理只允许在输出根目录边界内执行。</summary>
        [Tooltip("是否在构建前清理目标目录。")]
        public bool CleanBeforeBuild;

        /// <summary>
        /// 解析输出根目录的绝对路径，并校验其不落在禁止目录内。
        /// </summary>
        /// <param name="projectRoot">Unity 工程根目录绝对路径。</param>
        /// <returns>输出根目录的绝对路径。</returns>
        public string ResolveRootAbsolute(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException(
                    "输出根目录解析需要非空的工程根目录。",
                    nameof(projectRoot));
            }

            string absoluteRoot = Path.IsPathRooted(OutputRoot)
                ? Path.GetFullPath(OutputRoot)
                : Path.GetFullPath(Path.Combine(projectRoot, OutputRoot));

            ValidateRootBoundary(projectRoot, absoluteRoot);
            return absoluteRoot;
        }

        /// <summary>
        /// 使用构建令牌解析输出目录模板。
        /// </summary>
        /// <param name="token">构建输出令牌。</param>
        /// <returns>替换占位符后的相对目录路径。</returns>
        public string ResolveDirectory(BuildOutputToken token)
        {
            if (string.IsNullOrWhiteSpace(DirectoryTemplate))
            {
                throw new InvalidOperationException("输出目录模板不能为空。");
            }

            return ReplacePlaceholders(DirectoryTemplate, token);
        }

        /// <summary>
        /// 使用构建令牌解析输出文件名模板。
        /// </summary>
        /// <param name="token">构建输出令牌。</param>
        /// <returns>替换占位符后的文件名（不含扩展名）。</returns>
        public string ResolveFileName(BuildOutputToken token)
        {
            if (string.IsNullOrWhiteSpace(FileNameTemplate))
            {
                throw new InvalidOperationException("输出文件名模板不能为空。");
            }

            return ReplacePlaceholders(FileNameTemplate, token);
        }

        /// <summary>
        /// 校验输出根目录边界，禁止落在工程关键目录内。
        /// </summary>
        /// <param name="projectRoot">Unity 工程根目录绝对路径。</param>
        /// <param name="absoluteRoot">待校验的输出根目录绝对路径。</param>
        private static void ValidateRootBoundary(
            string projectRoot,
            string absoluteRoot)
        {
            string normalizedProject = NormalizePath(projectRoot);
            string normalizedRoot = NormalizePath(absoluteRoot);

            if (string.Equals(
                    normalizedProject,
                    normalizedRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "输出根目录不能是 Unity 工程根目录本身。");
            }

            foreach (string forbidden in ForbiddenRootDirectories)
            {
                string forbiddenPath = NormalizePath(
                    Path.Combine(projectRoot, forbidden));
                if (IsWithin(normalizedRoot, forbiddenPath)
                    || IsWithin(forbiddenPath, normalizedRoot))
                {
                    throw new InvalidOperationException(
                        $"输出根目录 '{absoluteRoot}' 与禁止目录 '{forbidden}' "
                        + "存在包含关系，不允许在工程关键目录内输出构建产物。");
                }
            }
        }

        /// <summary>
        /// 替换模板中的全部占位符。
        /// </summary>
        /// <param name="template">原始模板。</param>
        /// <param name="token">构建输出令牌。</param>
        /// <returns>替换后的字符串。</returns>
        private static string ReplacePlaceholders(
            string template,
            BuildOutputToken token)
        {
            return template
                .Replace(ProfilePlaceholder, token.ProfileName)
                .Replace(ProductNamePlaceholder, token.ProductName)
                .Replace(PlatformPlaceholder, token.PlatformName)
                .Replace(VersionPlaceholder, token.Version)
                .Replace(BuildNumberPlaceholder, token.BuildNumber.ToString())
                .Replace(ScriptBackendPlaceholder, token.ScriptBackend)
                .Replace(DatePlaceholder, token.Time.ToString("yyyyMMdd"))
                .Replace(TimePlaceholder, token.Time.ToString("HHmmss"));
        }

        /// <summary>
        /// 归一化路径，统一分隔符并移除末尾分隔符。
        /// </summary>
        /// <param name="path">原始路径。</param>
        /// <returns>归一化后的路径。</returns>
        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        /// <summary>
        /// 判断子路径是否位于父路径内部。
        /// </summary>
        /// <param name="child">子路径。</param>
        /// <param name="parent">父路径。</param>
        /// <returns>子路径位于父路径内部或相等时返回 true。</returns>
        private static bool IsWithin(string child, string parent)
        {
            return child.Equals(parent, StringComparison.OrdinalIgnoreCase)
                || child.StartsWith(
                    parent + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)
                || child.StartsWith(
                    parent + Path.AltDirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 构建输出令牌，承载输出目录与文件名模板所需的全部占位数据。
    /// </summary>
    public readonly struct BuildOutputToken
    {
        /// <summary>
        /// 创建构建输出令牌。
        /// </summary>
        /// <param name="profileName">Profile 资产名称。</param>
        /// <param name="productName">产品名称。</param>
        /// <param name="platformName">平台显示名称。</param>
        /// <param name="version">公共版本号。</param>
        /// <param name="buildNumber">平台构建号。</param>
        /// <param name="scriptBackend">脚本后端名称。</param>
        /// <param name="time">构建时刻。</param>
        public BuildOutputToken(
            string profileName,
            string productName,
            string platformName,
            string version,
            int buildNumber,
            string scriptBackend,
            DateTime time)
        {
            ProfileName = profileName ?? string.Empty;
            ProductName = productName ?? string.Empty;
            PlatformName = platformName ?? string.Empty;
            Version = version ?? string.Empty;
            BuildNumber = buildNumber;
            ScriptBackend = scriptBackend ?? string.Empty;
            Time = time;
        }

        /// <summary>获取 Profile 资产名称。</summary>
        public string ProfileName { get; }

        /// <summary>获取产品名称。</summary>
        public string ProductName { get; }

        /// <summary>获取平台显示名称。</summary>
        public string PlatformName { get; }

        /// <summary>获取公共版本号。</summary>
        public string Version { get; }

        /// <summary>获取平台构建号。</summary>
        public int BuildNumber { get; }

        /// <summary>获取脚本后端名称。</summary>
        public string ScriptBackend { get; }

        /// <summary>获取构建时刻。</summary>
        public DateTime Time { get; }
    }
}
