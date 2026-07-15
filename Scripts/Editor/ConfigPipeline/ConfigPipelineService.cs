using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RFramework;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// Config/Localization CSV 校验、代码生成、JSON 和二进制导出的统一 Editor 入口。
    /// </summary>
    public static class ConfigPipelineService
    {
        private const string ConfigCodeManifestName = "UnityRFramework.ConfigCode.manifest";
        private const string ConfigJsonManifestName = "UnityRFramework.ConfigJson.manifest";
        private const string ConfigBinaryManifestName = "UnityRFramework.ConfigBinary.manifest";
        private const string LocalizationJsonManifestName =
            "UnityRFramework.LocalizationJson.manifest";
        private const string LocalizationBinaryManifestName =
            "UnityRFramework.LocalizationBinary.manifest";
        private const string JsonOutputFolderName = "Json";
        private const string BinaryOutputFolderName = "Binary";

        /// <summary>
        /// 校验全部 Config 与 Localization CSV，不写入输出文件。
        /// </summary>
        /// <param name="options">转换工具配置。</param>
        /// <returns>校验报告。</returns>
        public static ConfigPipelineReport ValidateAll(ConfigPipelineOptions options)
        {
            ValidateOptions(options, true, true);
            ConfigPipelineReport report = new ConfigPipelineReport();
            ParseConfigSchemas(options, report);
            ParseLocalizations(options, report);
            report.AddMessage($"Validation passed. {report.ProcessedFileCount} CSV file(s).");
            return report;
        }

        /// <summary>
        /// 校验并导出全部 Config 与 Localization 文件。
        /// </summary>
        /// <param name="options">转换工具配置。</param>
        /// <returns>导出报告。</returns>
        public static ConfigPipelineReport ExportAll(ConfigPipelineOptions options)
        {
            ValidateOptions(options, true, true);
            ConfigPipelineReport report = new ConfigPipelineReport();
            List<ConfigTableSchema> configs = ParseConfigSchemas(options, report);
            List<LocalizationTable> localizations = ParseLocalizations(options, report);
            bool changed = ExportConfigs(options, configs, report);
            changed |= ExportLocalizations(options, localizations, report);
            if (changed)
            {
                AssetDatabase.Refresh();
            }

            report.AddMessage(
                $"Export complete. {report.ProcessedFileCount} source file(s), "
                + $"{report.WrittenFileCount} changed output file(s).");
            return report;
        }

        /// <summary>
        /// 校验并导出全部 Config 代码、JSON 和二进制文件。
        /// </summary>
        /// <param name="options">转换工具配置。</param>
        /// <returns>导出报告。</returns>
        public static ConfigPipelineReport ExportConfig(ConfigPipelineOptions options)
        {
            ValidateOptions(options, true, false);
            ConfigPipelineReport report = new ConfigPipelineReport();
            List<ConfigTableSchema> configs = ParseConfigSchemas(options, report);
            if (ExportConfigs(options, configs, report))
            {
                AssetDatabase.Refresh();
            }

            report.AddMessage(
                $"Config export complete. {report.ProcessedFileCount} source file(s), "
                + $"{report.WrittenFileCount} changed output file(s).");
            return report;
        }

        /// <summary>
        /// 校验并导出全部 Localization JSON 和二进制文件。
        /// </summary>
        /// <param name="options">转换工具配置。</param>
        /// <returns>导出报告。</returns>
        public static ConfigPipelineReport ExportLocalization(ConfigPipelineOptions options)
        {
            ValidateOptions(options, false, true);
            ConfigPipelineReport report = new ConfigPipelineReport();
            List<LocalizationTable> localizations = ParseLocalizations(options, report);
            if (ExportLocalizations(options, localizations, report))
            {
                AssetDatabase.Refresh();
            }

            report.AddMessage(
                $"Localization export complete. {report.ProcessedFileCount} source file(s), "
                + $"{report.WrittenFileCount} changed output file(s).");
            return report;
        }

        private static List<ConfigTableSchema> ParseConfigSchemas(
            ConfigPipelineOptions options, ConfigPipelineReport report)
        {
            string sourceRoot = ResolveDirectory(options.ConfigSourceDirectory, true);
            string[] files = Directory.GetFiles(sourceRoot, "*.csv", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            if (files.Length == 0)
            {
                throw new RFrameworkException(
                    $"No config CSV files found in '{options.ConfigSourceDirectory}'.");
            }

            List<ConfigTableSchema> result = new List<ConfigTableSchema>(files.Length);
            HashSet<string> generatedTypeNames =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<uint, string> tableIds = new Dictionary<uint, string>();
            for (int i = 0; i < files.Length; i++)
            {
                ConfigTableSchema schema = ConfigSchemaParser.ParseConfig(
                    CsvDocumentReader.ReadFile(files[i]), options.GeneratedNamespace?.Trim());
                if (!generatedTypeNames.Add(schema.FullRowTypeName))
                {
                    throw new RFrameworkException(
                        $"Duplicate generated config type '{schema.FullRowTypeName}'.");
                }

                for (int fieldIndex = 0; fieldIndex < schema.Fields.Count; fieldIndex++)
                {
                    ConfigFieldSchema field = schema.Fields[fieldIndex];
                    if (field.Kind != ConfigFieldKind.Enum)
                    {
                        continue;
                    }

                    string enumTypeName = string.IsNullOrEmpty(schema.Namespace)
                        ? field.CSharpTypeName
                        : schema.Namespace + "." + field.CSharpTypeName;
                    if (!generatedTypeNames.Add(enumTypeName))
                    {
                        throw new RFrameworkException(
                            $"Duplicate generated enum type '{enumTypeName}'.");
                    }
                }

                if (tableIds.TryGetValue(schema.TableId, out string other))
                {
                    throw new RFrameworkException(
                        $"Config TableId collision: '{schema.FullRowTypeName}' and '{other}' "
                        + $"both use '{schema.TableId:X8}'. Rename one table.");
                }

                tableIds.Add(schema.TableId, schema.FullRowTypeName);
                result.Add(schema);
                report.FileProcessed();
            }

            return result;
        }

        private static List<LocalizationTable> ParseLocalizations(
            ConfigPipelineOptions options, ConfigPipelineReport report)
        {
            string sourceRoot = ResolveDirectory(options.LocalizationSourceDirectory, true);
            string[] files = Directory.GetFiles(sourceRoot, "*.csv", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            if (files.Length == 0)
            {
                throw new RFrameworkException(
                    $"No localization CSV files found in '{options.LocalizationSourceDirectory}'.");
            }

            List<LocalizationTable> result = new List<LocalizationTable>(files.Length);
            HashSet<string> languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < files.Length; i++)
            {
                LocalizationTable localization = LocalizationCsvParser.Parse(
                    CsvDocumentReader.ReadFile(files[i]));
                if (!languages.Add(localization.Language))
                {
                    throw new RFrameworkException(
                        $"Duplicate localization language '{localization.Language}'.");
                }

                result.Add(localization);
                report.FileProcessed();
            }

            return result;
        }

        private static bool ExportConfigs(
            ConfigPipelineOptions options,
            IReadOnlyList<ConfigTableSchema> configs,
            ConfigPipelineReport report)
        {
            string codeRoot = ResolveDirectory(options.GeneratedCodeDirectory, false);
            string outputRoot = ResolveDirectory(options.ConfigOutputDirectory, false);
            RemoveLegacyOutputs(
                outputRoot,
                new[] { ConfigJsonManifestName, ConfigBinaryManifestName },
                report);
            string jsonRoot = Path.Combine(outputRoot, JsonOutputFolderName);
            string binaryRoot = Path.Combine(outputRoot, BinaryOutputFolderName);
            HashSet<string> codeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> jsonFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> binaryFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool changed = false;

            for (int i = 0; i < configs.Count; i++)
            {
                ConfigTableSchema schema = configs[i];
                string codeFile = schema.RowTypeName + ".g.cs";
                string jsonFile = schema.TableName + ".json";
                string binaryFile = schema.TableName + ".bytes";
                codeFiles.Add(codeFile);
                jsonFiles.Add(jsonFile);
                binaryFiles.Add(binaryFile);

                string codePath = Path.Combine(codeRoot, codeFile);
                if (ConfigCodeGenerator.WriteCodeIfChanged(
                    codePath, ConfigCodeGeneratorRegistry.Current.Generate(schema)))
                {
                    changed = true;
                    report.FileWritten(ToProjectPath(codePath));
                }

                string jsonPath = Path.Combine(jsonRoot, jsonFile);
                if (JsonExportUtility.WriteTextIfChanged(
                    jsonPath, ConfigJsonExporter.Build(schema)))
                {
                    changed = true;
                    report.FileWritten(ToProjectPath(jsonPath));
                }

                string binaryPath = Path.Combine(binaryRoot, binaryFile);
                if (ConfigBinaryExporter.WriteBytesIfChanged(
                    binaryPath, ConfigBinaryExporter.BuildV2(schema)))
                {
                    changed = true;
                    report.FileWritten(ToProjectPath(binaryPath));
                }
            }

            changed |= SynchronizeManifest(
                codeRoot, ConfigCodeManifestName, codeFiles, report);
            changed |= SynchronizeManifest(
                jsonRoot, ConfigJsonManifestName, jsonFiles, report);
            changed |= SynchronizeManifest(
                binaryRoot, ConfigBinaryManifestName, binaryFiles, report);
            return changed;
        }

        private static bool ExportLocalizations(
            ConfigPipelineOptions options,
            IReadOnlyList<LocalizationTable> localizations,
            ConfigPipelineReport report)
        {
            string outputRoot = ResolveDirectory(options.LocalizationOutputDirectory, false);
            RemoveLegacyOutputs(
                outputRoot,
                new[] { LocalizationJsonManifestName, LocalizationBinaryManifestName },
                report);
            string jsonRoot = Path.Combine(outputRoot, JsonOutputFolderName);
            string binaryRoot = Path.Combine(outputRoot, BinaryOutputFolderName);
            HashSet<string> jsonFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> binaryFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool changed = false;
            for (int i = 0; i < localizations.Count; i++)
            {
                LocalizationTable localization = localizations[i];
                string jsonFile = localization.Language + ".json";
                jsonFiles.Add(jsonFile);
                string jsonPath = Path.Combine(jsonRoot, jsonFile);
                if (JsonExportUtility.WriteTextIfChanged(
                    jsonPath, LocalizationJsonExporter.Build(localization)))
                {
                    changed = true;
                    report.FileWritten(ToProjectPath(jsonPath));
                }

                string binaryFile = localization.Language + ".bytes";
                binaryFiles.Add(binaryFile);
                string outputPath = Path.Combine(binaryRoot, binaryFile);
                if (ConfigBinaryExporter.WriteBytesIfChanged(
                    outputPath, LocalizationBinaryExporter.BuildV2(localization)))
                {
                    changed = true;
                    report.FileWritten(ToProjectPath(outputPath));
                }
            }

            changed |= SynchronizeManifest(
                jsonRoot, LocalizationJsonManifestName, jsonFiles, report);
            changed |= SynchronizeManifest(
                binaryRoot, LocalizationBinaryManifestName, binaryFiles, report);
            return changed;
        }

        private static void RemoveLegacyOutputs(
            string outputRoot, IReadOnlyList<string> manifestNames, ConfigPipelineReport report)
        {
            for (int manifestIndex = 0; manifestIndex < manifestNames.Count; manifestIndex++)
            {
                string manifestPath = Path.Combine(outputRoot, manifestNames[manifestIndex]);
                if (!File.Exists(manifestPath))
                {
                    continue;
                }

                string[] previous = File.ReadAllLines(manifestPath, Encoding.UTF8);
                for (int fileIndex = 0; fileIndex < previous.Length; fileIndex++)
                {
                    string relative = previous[fileIndex].Trim();
                    if (string.IsNullOrEmpty(relative))
                    {
                        continue;
                    }

                    string legacyPath = Path.GetFullPath(Path.Combine(outputRoot, relative));
                    EnsureWithinDirectory(outputRoot, legacyPath);
                    DeleteGeneratedFile(legacyPath, report);
                }

                DeleteGeneratedFile(manifestPath, report);
            }
        }

        private static void DeleteGeneratedFile(string fullPath, ConfigPipelineReport report)
        {
            if (!File.Exists(fullPath))
            {
                return;
            }

            string projectPath = ToProjectPath(fullPath);
            if (projectPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                if (!AssetDatabase.DeleteAsset(projectPath) && File.Exists(fullPath))
                {
                    throw new RFrameworkException(
                        $"Failed to remove legacy generated file '{projectPath}'.");
                }
            }
            else
            {
                File.Delete(fullPath);
            }

            report.AddMessage("Removed legacy generated file: " + projectPath);
        }

        private static bool SynchronizeManifest(
            string outputRoot,
            string manifestName,
            HashSet<string> currentFiles,
            ConfigPipelineReport report)
        {
            Directory.CreateDirectory(outputRoot);
            string manifestPath = Path.Combine(outputRoot, manifestName);
            bool changed = false;
            if (File.Exists(manifestPath))
            {
                string[] previous = File.ReadAllLines(manifestPath, Encoding.UTF8);
                for (int i = 0; i < previous.Length; i++)
                {
                    string relative = previous[i].Trim();
                    if (string.IsNullOrEmpty(relative) || currentFiles.Contains(relative))
                    {
                        continue;
                    }

                    string stalePath = Path.GetFullPath(Path.Combine(outputRoot, relative));
                    EnsureWithinDirectory(outputRoot, stalePath);
                    if (File.Exists(stalePath))
                    {
                        string projectPath = ToProjectPath(stalePath);
                        if (projectPath.StartsWith("Assets/", StringComparison.Ordinal))
                        {
                            if (!AssetDatabase.DeleteAsset(projectPath) && File.Exists(stalePath))
                            {
                                throw new RFrameworkException(
                                    $"Failed to remove stale generated file '{projectPath}'.");
                            }
                        }
                        else
                        {
                            File.Delete(stalePath);
                        }

                        changed = true;
                        report.AddMessage("Removed stale generated file: " + projectPath);
                    }
                }
            }

            string manifest = string.Join("\n",
                currentFiles.OrderBy(path => path, StringComparer.Ordinal));
            if (!File.Exists(manifestPath)
                || !string.Equals(File.ReadAllText(manifestPath, Encoding.UTF8), manifest,
                    StringComparison.Ordinal))
            {
                File.WriteAllText(manifestPath, manifest, new UTF8Encoding(false));
                changed = true;
            }

            return changed;
        }

        private static void ValidateOptions(
            ConfigPipelineOptions options, bool requireConfigSource, bool requireLocalizationSource)
        {
            if (options == null)
            {
                throw new RFrameworkException("Config pipeline options are invalid.");
            }

            ResolveDirectory(options.ConfigSourceDirectory, requireConfigSource);
            ResolveDirectory(options.LocalizationSourceDirectory, requireLocalizationSource);
            ResolveDirectory(options.GeneratedCodeDirectory, false);
            string configOutputRoot = ResolveDirectory(options.ConfigOutputDirectory, false);
            string localizationOutputRoot = ResolveDirectory(
                options.LocalizationOutputDirectory, false);
            if (string.Equals(
                configOutputRoot, localizationOutputRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new RFrameworkException(
                    "Config and Localization output directories must be different.");
            }
        }

        private static string ResolveDirectory(string projectPath, bool mustExist)
        {
            if (string.IsNullOrWhiteSpace(projectPath)
                || !(projectPath.Equals("Assets", StringComparison.Ordinal)
                    || projectPath.StartsWith("Assets/", StringComparison.Ordinal)))
            {
                throw new RFrameworkException(
                    $"Config pipeline path must be project-relative and under Assets: '{projectPath}'.");
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            string fullPath = Path.GetFullPath(Path.Combine(
                projectRoot ?? throw new RFrameworkException("Unity project root is invalid."),
                projectPath.Replace('/', Path.DirectorySeparatorChar)));
            string assetsRoot = Path.GetFullPath(Application.dataPath);
            EnsureWithinDirectory(assetsRoot, fullPath);
            if (mustExist && !Directory.Exists(fullPath))
            {
                throw new RFrameworkException($"Source directory does not exist: '{projectPath}'.");
            }

            return fullPath;
        }

        private static void EnsureWithinDirectory(string root, string path)
        {
            string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string normalizedPath = Path.GetFullPath(path);
            if (!normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(normalizedPath.TrimEnd(Path.DirectorySeparatorChar),
                    normalizedRoot.TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new RFrameworkException(
                    $"Path '{path}' escapes configured directory '{root}'.");
            }
        }

        private static string ToProjectPath(string fullPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                return fullPath.Replace('\\', '/');
            }

            string normalizedRoot = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string normalizedPath = Path.GetFullPath(fullPath);
            return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                ? normalizedPath.Substring(normalizedRoot.Length).Replace('\\', '/')
                : normalizedPath.Replace('\\', '/');
        }
    }
}
