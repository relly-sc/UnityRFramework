using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// Demo 配置与本地化数据的一键导出入口。
    /// </summary>
    public static class DemoConfigPipelineExporter
    {
        /// <summary>Demo Config 在 StreamingAssets 中的运行时目录。</summary>
        private const string StreamingConfigDirectory = "Assets/StreamingAssets/Config";

        /// <summary>Demo Localization 在 StreamingAssets 中的运行时目录。</summary>
        private const string StreamingLocalizationDirectory =
            "Assets/StreamingAssets/Localization";

        /// <summary>Demo 一键导出菜单路径。</summary>
        private const string MenuPath = "UnityRFramework/Demo/Export Config and Localization";

        [MenuItem(MenuPath)]
        public static void Export()
        {
            ConfigPipelineOptions options = new ConfigPipelineOptions
            {
                ConfigSourceDirectory =
                    "Assets/UnityRFramework/Samples/Demo/ConfigSource/Config",
                LocalizationSourceDirectory =
                    "Assets/UnityRFramework/Samples/Demo/ConfigSource/Localization",
                GeneratedCodeDirectory =
                    "Assets/UnityRFramework/Samples/Demo/Generated/UnityRFramework/Config",
                ConfigOutputDirectory =
                    "Assets/UnityRFramework/Samples/Demo/GameAssets/Resources/Config",
                LocalizationOutputDirectory =
                    "Assets/UnityRFramework/Samples/Demo/GameAssets/Resources/Localization",
                GeneratedNamespace = "Game.Config"
            };

            ConfigPipelineReport report = ConfigPipelineService.ExportAll(options);
            SynchronizeDirectory(
                options.ConfigOutputDirectory, StreamingConfigDirectory, report);
            SynchronizeDirectory(
                options.LocalizationOutputDirectory, StreamingLocalizationDirectory, report);
            report.AddMessage("StreamingAssets synchronization completed.");
            AssetDatabase.Refresh();
            Debug.Log(BuildReportText(report));
        }

        /// <summary>构建适合 Unity Console 阅读的 Demo 导出报告。</summary>
        /// <param name="report">导出报告。</param>
        /// <returns>格式化报告文本。</returns>
        private static string BuildReportText(ConfigPipelineReport report)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("[Demo] Config and Localization export completed.");
            builder.Append("Processed source files: ")
                .AppendLine(report.ProcessedFileCount.ToString());
            builder.Append("Written output files (including StreamingAssets): ")
                .AppendLine(report.WrittenFileCount.ToString());
            for (int i = 0; i < report.Messages.Count; i++)
            {
                builder.AppendLine(report.Messages[i]);
            }

            return builder.ToString();
        }

        /// <summary>
        /// 将 Demo 生成目录按相对路径镜像到 StreamingAssets。
        /// 内容未变化的文件不会重复写入，源目录中已删除的旧产物也会同步删除。
        /// </summary>
        /// <param name="sourceProjectPath">Assets 下的生成目录。</param>
        /// <param name="targetProjectPath">Assets 下的运行时目录。</param>
        /// <param name="report">导出报告。</param>
        private static void SynchronizeDirectory(
            string sourceProjectPath, string targetProjectPath, ConfigPipelineReport report)
        {
            string sourceRoot = ResolveAbsolutePath(sourceProjectPath);
            string targetRoot = ResolveAbsolutePath(targetProjectPath);
            if (!Directory.Exists(sourceRoot))
            {
                throw new DirectoryNotFoundException(
                    $"Demo generated directory does not exist: '{sourceProjectPath}'.");
            }

            Directory.CreateDirectory(targetRoot);
            HashSet<string> sourceRelativePaths =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] sourceFiles = Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories);
            for (int i = 0; i < sourceFiles.Length; i++)
            {
                string sourceFile = sourceFiles[i];
                if (sourceFile.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string relativePath = GetRelativePath(sourceRoot, sourceFile);
                sourceRelativePaths.Add(relativePath);
                string targetFile = Path.Combine(targetRoot, relativePath);
                string targetDirectory = Path.GetDirectoryName(targetFile);
                if (!string.IsNullOrEmpty(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                if (FilesEqual(sourceFile, targetFile))
                {
                    continue;
                }

                File.Copy(sourceFile, targetFile, true);
                report.FileWritten(ToProjectPath(targetFile));
            }

            string[] targetFiles = Directory.GetFiles(targetRoot, "*", SearchOption.AllDirectories);
            for (int i = 0; i < targetFiles.Length; i++)
            {
                string targetFile = targetFiles[i];
                if (targetFile.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string relativePath = GetRelativePath(targetRoot, targetFile);
                if (sourceRelativePaths.Contains(relativePath))
                {
                    continue;
                }

                string staleProjectPath = ToProjectPath(targetFile);
                if (!AssetDatabase.DeleteAsset(staleProjectPath))
                {
                    throw new IOException(
                        $"Failed to delete stale Demo output '{staleProjectPath}'.");
                }

                report.AddMessage("Deleted: " + staleProjectPath);
            }
        }

        /// <summary>比较两个文件的字节内容是否完全一致。</summary>
        /// <param name="firstPath">第一个文件路径。</param>
        /// <param name="secondPath">第二个文件路径。</param>
        /// <returns>内容一致返回 true，否则返回 false。</returns>
        private static bool FilesEqual(string firstPath, string secondPath)
        {
            if (!File.Exists(secondPath))
            {
                return false;
            }

            byte[] first = File.ReadAllBytes(firstPath);
            byte[] second = File.ReadAllBytes(secondPath);
            if (first.Length != second.Length)
            {
                return false;
            }

            for (int i = 0; i < first.Length; i++)
            {
                if (first[i] != second[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>将目录内文件转换为使用正斜杠的相对路径。</summary>
        /// <param name="rootPath">根目录绝对路径。</param>
        /// <param name="filePath">文件绝对路径。</param>
        /// <returns>相对路径。</returns>
        private static string GetRelativePath(string rootPath, string filePath)
        {
            string normalizedRoot = Path.GetFullPath(rootPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string normalizedFile = Path.GetFullPath(filePath);
            if (!normalizedFile.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException($"File '{filePath}' is outside '{rootPath}'.");
            }

            return normalizedFile.Substring(normalizedRoot.Length)
                .Replace('\\', '/');
        }

        /// <summary>将 Assets 项目路径转换为绝对路径。</summary>
        /// <param name="projectPath">Assets 项目路径。</param>
        /// <returns>绝对路径。</returns>
        private static string ResolveAbsolutePath(string projectPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new DirectoryNotFoundException("Unity project root is unavailable.");
            }

            return Path.GetFullPath(Path.Combine(projectRoot, projectPath));
        }

        /// <summary>将工程内绝对路径转换为 Assets 项目路径。</summary>
        /// <param name="absolutePath">工程内绝对路径。</param>
        /// <returns>Assets 项目路径。</returns>
        private static string ToProjectPath(string absolutePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new DirectoryNotFoundException("Unity project root is unavailable.");
            }

            string root = Path.GetFullPath(projectRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(absolutePath);
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException($"Path '{absolutePath}' is outside the Unity project.");
            }

            return path.Substring(root.Length).Replace('\\', '/');
        }
    }
}
