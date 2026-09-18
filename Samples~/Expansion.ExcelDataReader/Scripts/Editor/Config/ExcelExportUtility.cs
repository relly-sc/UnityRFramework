using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 为 Config 与 Localization Excel 导出流程提供统一的文件和路径操作。
    /// </summary>
    internal static class ExcelExportUtility
    {
        /// <summary>
        /// 递归收集路径中的 Excel 文件。
        /// </summary>
        /// <param name="sourcePaths">Excel 文件或目录集合。</param>
        /// <returns>去重并排序后的绝对路径。</returns>
        internal static List<string> CollectExcelFiles(
            IReadOnlyList<string> sourcePaths)
        {
            if (sourcePaths == null || sourcePaths.Count == 0)
            {
                throw new RFrameworkException("Excel source paths are empty.");
            }

            HashSet<string> files =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < sourcePaths.Count; i++)
            {
                string absolutePath = ResolvePath(sourcePaths[i]);
                if (File.Exists(absolutePath))
                {
                    if (IsExcelFile(absolutePath))
                    {
                        files.Add(absolutePath);
                    }

                    continue;
                }

                if (!Directory.Exists(absolutePath))
                {
                    throw new RFrameworkException(
                        $"Excel source path does not exist: '{sourcePaths[i]}'.");
                }

                string[] candidates =
                    Directory.GetFiles(absolutePath, "*.*", SearchOption.AllDirectories);
                for (int candidateIndex = 0;
                     candidateIndex < candidates.Length;
                     candidateIndex++)
                {
                    if (IsExcelFile(candidates[candidateIndex]))
                    {
                        files.Add(Path.GetFullPath(candidates[candidateIndex]));
                    }
                }
            }

            List<string> result = files.ToList();
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        /// <summary>
        /// 判断路径集合中是否包含 Excel 文件。
        /// </summary>
        /// <param name="sourcePaths">文件或目录集合。</param>
        /// <returns>至少包含一个 Excel 文件时返回 true。</returns>
        internal static bool ContainsExcelFiles(IReadOnlyList<string> sourcePaths)
        {
            try
            {
                return CollectExcelFiles(sourcePaths).Count > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 将 Assets 内路径解析为绝对目录。
        /// </summary>
        /// <param name="path">工程相对路径或绝对路径。</param>
        /// <returns>经过边界校验的绝对路径。</returns>
        internal static string ResolveAssetsDirectory(string path)
        {
            string absolutePath = ResolvePath(path);
            string assetsRoot = Path.GetFullPath(Application.dataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!absolutePath.Equals(assetsRoot, StringComparison.OrdinalIgnoreCase)
                && !absolutePath.StartsWith(
                    assetsRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new RFrameworkException(
                    $"Excel output directory must be inside Assets: '{path}'.");
            }

            return absolutePath;
        }

        /// <summary>
        /// 将导出器相对路径限制在输出根目录内。
        /// </summary>
        /// <param name="root">输出根目录。</param>
        /// <param name="relativePath">导出器返回的相对路径。</param>
        /// <returns>完整输出路径。</returns>
        internal static string ResolveOutputPath(string root, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)
                || Path.IsPathRooted(relativePath))
            {
                throw new RFrameworkException(
                    $"Excel exporter output path is invalid: '{relativePath}'.");
            }

            string normalizedRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string outputPath = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!outputPath.StartsWith(
                normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new RFrameworkException(
                    $"Excel exporter output escapes the output directory: '{relativePath}'.");
            }

            return outputPath;
        }

        /// <summary>
        /// 仅在内容变化时写入字节。
        /// </summary>
        /// <param name="path">目标文件路径。</param>
        /// <param name="data">完整文件内容。</param>
        /// <returns>发生写入时返回 true。</returns>
        internal static bool WriteBytesIfChanged(string path, byte[] data)
        {
            if (data == null)
            {
                throw new RFrameworkException(
                    $"Excel exporter returned null data for '{path}'.");
            }

            if (File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(data))
            {
                return false;
            }

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(path, data);
            return true;
        }

        /// <summary>
        /// 将绝对路径转换为工程相对路径。
        /// </summary>
        /// <param name="absolutePath">绝对路径。</param>
        /// <returns>工程内路径或标准化绝对路径。</returns>
        internal static string ToProjectPath(string absolutePath)
        {
            string root = Path.GetFullPath(GetProjectRoot())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(absolutePath);
            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? path.Substring(root.Length).Replace('\\', '/')
                : path.Replace('\\', '/');
        }

        private static bool IsExcelFile(string path)
        {
            if (Path.GetFileName(path).StartsWith("~$", StringComparison.Ordinal))
            {
                return false;
            }

            string extension = Path.GetExtension(path);
            return extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".xls", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new RFrameworkException("Path is empty.");
            }

            if (Path.IsPathRooted(path))
            {
                return Path.GetFullPath(path);
            }

            return Path.GetFullPath(Path.Combine(GetProjectRoot(), path));
        }

        private static string GetProjectRoot()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new RFrameworkException("Unity project root is unavailable.");
            }

            return projectRoot;
        }
    }
}
