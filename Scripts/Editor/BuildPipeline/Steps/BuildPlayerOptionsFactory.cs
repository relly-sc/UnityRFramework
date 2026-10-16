using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// Player 构建选项工厂：将 Profile 与构建上下文统一组装为 BuildPlayerOptions。
    /// BuildOptions 只在本类生成，窗口与各步骤不得散落拼接；
    /// 输出位置按平台解析：Windows 为 .exe，macOS 为 .app，Linux 为无扩展名文件，
    /// Android 为 .apk/.aab，iOS 与 WebGL 为输出目录。
    /// </summary>
    public static class BuildPlayerOptionsFactory
    {
        /// <summary>
        /// 从构建上下文创建 Player 构建选项。
        /// 上下文必须携带有效 Profile 且输出解析成功，否则抛出异常。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        /// <returns>组装完成的构建选项。</returns>
        public static BuildPlayerOptions Create(BuildPipelineContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.Profile == null)
            {
                throw new InvalidOperationException(
                    "构建配置为空，无法组装构建选项。");
            }

            if (context.OutputError.Length > 0)
            {
                throw new InvalidOperationException(
                    $"输出路径解析失败，无法组装构建选项：{context.OutputError}");
            }

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = ResolveScenes(context.Profile),
                target = context.Target,
                locationPathName = ResolveLocationPath(context),
                options = ResolveOptions(context.Profile)
            };
            return options;
        }

        /// <summary>
        /// 解析参与构建的场景路径数组，只包含有效且启用的场景条目。
        /// </summary>
        /// <param name="profile">构建配置。</param>
        /// <returns>场景路径数组；无可用场景时返回空数组。</returns>
        public static string[] ResolveScenes(UnityRFrameworkBuildProfile profile)
        {
            if (profile == null)
            {
                return Array.Empty<string>();
            }

            System.Collections.Generic.List<string> paths =
                new System.Collections.Generic.List<string>();
            for (int i = 0; i < profile.Scenes.Count; i++)
            {
                BuildSceneEntry entry = profile.Scenes[i];
                if (entry == null || !entry.IsValidEnabled())
                {
                    continue;
                }

                paths.Add(entry.ResolvePath());
            }

            return paths.ToArray();
        }

        /// <summary>
        /// 解析输出位置：桌面与 Android 为产物文件完整路径，
        /// iOS 与 WebGL 为工程/站点输出目录。
        /// 文件名模板未携带扩展名时按平台自动补充。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        /// <returns>产物完整路径或 Xcode 输出目录。</returns>
        public static string ResolveLocationPath(BuildPipelineContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (string.IsNullOrEmpty(context.OutputRootAbsolute)
                || string.IsNullOrEmpty(context.OutputDirectory))
            {
                throw new InvalidOperationException(
                    "输出路径未解析成功，无法确定产物位置。");
            }

            string directory =
                Path.Combine(context.OutputRootAbsolute, context.OutputDirectory);

            if (UsesDirectoryOutput(context.Target))
            {
                return directory;
            }

            string fileName = ResolvePlatformFileName(
                context.Profile,
                context.Target,
                context.OutputFileName);

            return Path.Combine(directory, fileName);
        }

        /// <summary>
        /// 判断平台的 BuildPlayer 输出位置是否为目录而非文件。
        /// </summary>
        public static bool UsesDirectoryOutput(BuildTarget target)
        {
            return target == BuildTarget.iOS || target == BuildTarget.WebGL;
        }

        /// <summary>
        /// 为文件型构建目标解析最终文件名并补齐平台扩展名。
        /// </summary>
        public static string ResolvePlatformFileName(
            UnityRFrameworkBuildProfile profile,
            BuildTarget target,
            string fileName)
        {
            if (string.IsNullOrEmpty(fileName) && profile != null)
            {
                fileName = profile.Platform.ProductName;
            }

            if (target == BuildTarget.StandaloneWindows64)
            {
                if (!EndsWithExtension(fileName, ".exe"))
                {
                    fileName += ".exe";
                }
            }
            else if (target == BuildTarget.StandaloneOSX)
            {
                if (!EndsWithExtension(fileName, ".app"))
                {
                    fileName += ".app";
                }
            }
            else if (target == BuildTarget.Android)
            {
                string expectedExtension = profile != null
                    && profile.Platform.AndroidBuildAppBundle
                        ? ".aab"
                        : ".apk";
                if (!EndsWithExtension(fileName, expectedExtension))
                {
                    fileName += expectedExtension;
                }
            }

            return fileName;
        }

        /// <summary>
        /// 判断文件名是否已携带指定扩展名（不区分大小写）。
        /// 不使用 Path.GetExtension，因为版本号模板（如 1.0.0-0）含点，
        /// 会被误判为扩展名。
        /// </summary>
        /// <param name="fileName">文件名。</param>
        /// <param name="extension">目标扩展名，含点前缀。</param>
        /// <returns>已携带目标扩展名时返回 true。</returns>
        private static bool EndsWithExtension(
            string fileName,
            string extension)
        {
            return !string.IsNullOrEmpty(fileName)
                && fileName.EndsWith(
                    extension,
                    StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 按 Profile 生成 BuildOptions：未启用 Development Build 时返回 None；
        /// 调试选项仅在 Development Build 下追加，保证选项组合恒合法。
        /// </summary>
        /// <param name="profile">构建配置。</param>
        /// <returns>组装完成的构建选项。</returns>
        public static BuildOptions ResolveOptions(UnityRFrameworkBuildProfile profile)
        {
            if (profile == null)
            {
                return BuildOptions.None;
            }

            if (!profile.Platform.DevelopmentBuild)
            {
                return BuildOptions.None;
            }

            BuildOptions options = BuildOptions.Development;
            if (profile.Platform.ScriptDebugging)
            {
                options |= BuildOptions.AllowDebugging;
            }

            if (profile.Platform.AutoconnectProfiler)
            {
                options |= BuildOptions.ConnectToHost;
            }

            if (profile.Platform.DeepProfiling)
            {
                options |= BuildOptions.EnableDeepProfilingSupport;
            }

            return options;
        }

        /// <summary>
        /// 校验调试选项与 Development Build 的互斥关系（仅非 Release 档）。
        /// 未启用 Development Build 却配置脚本调试或 Profiler 选项时，
        /// 这些选项会被 Unity 静默忽略，属于配置矛盾。
        /// </summary>
        /// <param name="profile">构建配置。</param>
        /// <returns>存在矛盾时返回 Error 级问题；否则返回 null。</returns>
        public static BuildValidationIssue? ValidateDevelopmentOptions(
            UnityRFrameworkBuildProfile profile)
        {
            if (profile == null
                || profile.Flavor == BuildProfileFlavor.Release
                || profile.Platform.DevelopmentBuild)
            {
                return null;
            }

            if (!profile.Platform.ScriptDebugging
                && !profile.Platform.AutoconnectProfiler
                && !profile.Platform.DeepProfiling)
            {
                return null;
            }

            return BuildValidationIssue.Error(
                "OPTION",
                "已配置脚本调试或 Profiler 选项，但未启用 Development Build；"
                + "这些选项只在 Development Build 下生效，请启用 Development Build "
                + "或关闭调试选项。",
                "构建选项");
        }
    }
}
