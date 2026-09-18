using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO.Compression;
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
        /// 在内存中生成全部格式并报告体积、压缩估算和导出耗时，不写入文件。
        /// </summary>
        public static ConfigPipelineReport Analyze(ConfigPipelineOptions options)
        {
            ValidateOptions(options, true, true);
            ConfigPipelineReport report = new ConfigPipelineReport();
            Stopwatch stopwatch = Stopwatch.StartNew();
            List<ConfigTableSchema> configs = ParseConfigSchemas(options, report);
            List<LocalizationTable> localizations = ParseLocalizations(options, report);

            long sourceBytes = configs.Sum(schema => new FileInfo(schema.SourcePath).Length)
                + localizations.Sum(table => new FileInfo(table.SourcePath).Length);
            long configJsonBytes = configs.Sum(schema =>
                (long)Encoding.UTF8.GetByteCount(ConfigJsonExporter.Build(schema)));
            long configBinaryBytes = configs.Sum(schema =>
                (long)ConfigBinaryExporter.BuildV2(schema).Length);
            byte[] configBundle = ConfigBinaryExporter.BuildBundle(configs);
            long localizationJsonBytes = localizations.Sum(table =>
                (long)Encoding.UTF8.GetByteCount(LocalizationJsonExporter.Build(table)));
            long localizationBinaryBytes = localizations.Sum(table =>
                (long)LocalizationBinaryExporter.BuildV2(table).Length);
            byte[] localizationBundle = LocalizationBinaryExporter.BuildBundle(localizations);
            stopwatch.Stop();

            report.AddMessage("ConfigPipeline analysis (no files written)");
            report.AddMessage($"Source CSV: {sourceBytes} bytes");
            report.AddMessage(
                $"Config singles: JSON {configJsonBytes} bytes, URFC {configBinaryBytes} bytes "
                + $"({FormatRatio(configBinaryBytes, configJsonBytes)})");
            report.AddMessage(
                $"Config bundle: URFM {configBundle.Length} bytes, Deflate estimate "
                + $"{EstimateDeflateLength(configBundle)} bytes");
            report.AddMessage(
                $"Localization singles: JSON {localizationJsonBytes} bytes, URFL "
                + $"{localizationBinaryBytes} bytes "
                + $"({FormatRatio(localizationBinaryBytes, localizationJsonBytes)})");
            report.AddMessage(
                $"Localization bundle: URLM {localizationBundle.Length} bytes, Deflate estimate "
                + $"{EstimateDeflateLength(localizationBundle)} bytes");
            report.AddMessage($"In-memory export time: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");

            long binaryTotal = configBundle.Length + localizationBundle.Length;
            int compressedTotal = EstimateDeflateLength(configBundle)
                + EstimateDeflateLength(localizationBundle);
            double saving = binaryTotal == 0
                ? 0d
                : 1d - (double)compressedTotal / binaryTotal;
            report.AddMessage(binaryTotal < 64 * 1024 || saving < 0.15d
                ? "Recommendation: keep current protocols; compression overhead is not justified yet."
                : $"Recommendation: optional container compression is measurable "
                    + $"({saving:P1} estimated saving); benchmark load CPU before a protocol upgrade.");
            return report;
        }

        private static string FormatRatio(long value, long baseline)
        {
            return baseline <= 0 ? "n/a" : $"{(double)value / baseline:P1} of JSON";
        }

        private static int EstimateDeflateLength(byte[] bytes)
        {
            using (MemoryStream output = new MemoryStream())
            {
                using (DeflateStream stream = new DeflateStream(
                    output, System.IO.Compression.CompressionLevel.Optimal, true))
                {
                    stream.Write(bytes, 0, bytes.Length);
                }

                return checked((int)output.Length);
            }
        }

        /// <summary>
        /// 校验并导出全部 Config 与 Localization 文件。
        /// </summary>
        /// <param name="options">转换工具配置。</param>
        /// <returns>导出报告。</returns>
        public static ConfigPipelineReport ExportAll(ConfigPipelineOptions options)
        {
            return ExportAll(options, null);
        }

        /// <summary>使用调用方提供的密钥源导出全部配置。</summary>
        public static ConfigPipelineReport ExportAll(
            ConfigPipelineOptions options,
            IKeyProvider keyProvider)
        {
            ValidateOptions(options, true, true);
            ConfigProtectionExporter protection =
                ConfigProtectionExporter.Create(options, keyProvider);
            ConfigPipelineReport report = new ConfigPipelineReport();
            List<ConfigTableSchema> configs = ParseConfigSchemas(options, report);
            List<LocalizationTable> localizations = ParseLocalizations(options, report);
            bool changed = ExportConfigs(options, configs, report, protection);
            changed |= ExportLocalizations(options, localizations, report);
            if (changed)
            {
                AssetDatabase.Refresh();
            }

            report.AddMessage(
                $"Export complete. {report.ProcessedFileCount} source file(s), "
                + $"{report.WrittenFileCount} changed output file(s), "
                + $"{report.UnchangedFileCount} unchanged output file(s).");
            return report;
        }

        /// <summary>
        /// 校验并导出全部 Config 代码、JSON 和二进制文件。
        /// </summary>
        /// <param name="options">转换工具配置。</param>
        /// <returns>导出报告。</returns>
        public static ConfigPipelineReport ExportConfig(ConfigPipelineOptions options)
        {
            return ExportConfig(options, null);
        }

        /// <summary>使用调用方提供的密钥源导出 Config。</summary>
        public static ConfigPipelineReport ExportConfig(
            ConfigPipelineOptions options,
            IKeyProvider keyProvider)
        {
            ValidateOptions(options, true, false);
            ConfigProtectionExporter protection =
                ConfigProtectionExporter.Create(options, keyProvider);
            ConfigPipelineReport report = new ConfigPipelineReport();
            List<ConfigTableSchema> configs = ParseConfigSchemas(options, report);
            if (ExportConfigs(options, configs, report, protection))
            {
                AssetDatabase.Refresh();
            }

            report.AddMessage(
                $"Config export complete. {report.ProcessedFileCount} source file(s), "
                + $"{report.WrittenFileCount} changed output file(s), "
                + $"{report.UnchangedFileCount} unchanged output file(s).");
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

            List<CsvDocument> documents = new List<CsvDocument>(files.Length);
            for (int i = 0; i < files.Length; i++)
            {
                documents.Add(CsvDocumentReader.ReadFile(files[i]));
                report.FileProcessed();
            }

            return ParseConfigDocuments(
                documents, options.GeneratedNamespace?.Trim()).ToList();
        }

        /// <summary>
        /// 解析并校验来自 CSV、Excel 或自定义数据源的 Config 文档集合。
        /// </summary>
        /// <param name="documents">待解析的内存文档。</param>
        /// <param name="namespaceName">生成代码使用的命名空间，可留空。</param>
        /// <returns>完成跨表校验的配置表定义。</returns>
        public static IReadOnlyList<ConfigTableSchema> ParseConfigDocuments(
            IReadOnlyList<CsvDocument> documents, string namespaceName)
        {
            if (documents == null || documents.Count == 0)
            {
                throw new RFrameworkException("Config source documents are empty.");
            }

            List<ConfigTableSchema> result =
                new List<ConfigTableSchema>(documents.Count);
            HashSet<string> generatedTypeNames =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<uint, string> tableIds = new Dictionary<uint, string>();
            Dictionary<string, ConfigTableSchema> logicalTables =
                new Dictionary<string, ConfigTableSchema>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> segmentNames =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < documents.Count; i++)
            {
                if (documents[i] == null)
                {
                    throw new RFrameworkException(
                        $"Config source document at index {i} is invalid.");
                }

                ConfigTableSchema schema = ConfigSchemaParser.ParseConfig(
                    documents[i], namespaceName?.Trim());
                if (!segmentNames.Add(schema.SegmentName))
                {
                    throw new RFrameworkException(
                        $"Duplicate config segment name '{schema.SegmentName}'.");
                }

                if (logicalTables.TryGetValue(
                    schema.FullRowTypeName, out ConfigTableSchema firstPartition))
                {
                    if (firstPartition.TableId != schema.TableId
                        || firstPartition.SchemaHash != schema.SchemaHash)
                    {
                        throw new RFrameworkException(
                            $"Config partitions '{firstPartition.SegmentName}' and "
                            + $"'{schema.SegmentName}' do not use the same schema.");
                    }
                }
                else
                {
                    logicalTables.Add(schema.FullRowTypeName, schema);
                    if (!generatedTypeNames.Add(schema.FullRowTypeName))
                    {
                        throw new RFrameworkException(
                            $"Duplicate generated type '{schema.FullRowTypeName}'.");
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
                }

                result.Add(schema);
            }

            ValidatePartitionIds(result);

            return result;
        }

        private static void ValidatePartitionIds(IReadOnlyList<ConfigTableSchema> schemas)
        {
            Dictionary<string, HashSet<int>> idsByType =
                new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < schemas.Count; i++)
            {
                ConfigTableSchema schema = schemas[i];
                if (!idsByType.TryGetValue(schema.FullRowTypeName, out HashSet<int> ids))
                {
                    ids = new HashSet<int>();
                    idsByType.Add(schema.FullRowTypeName, ids);
                }

                int idIndex = -1;
                for (int fieldIndex = 0; fieldIndex < schema.Fields.Count; fieldIndex++)
                {
                    if (string.Equals(
                        schema.Fields[fieldIndex].Name, "Id", StringComparison.OrdinalIgnoreCase))
                    {
                        idIndex = fieldIndex;
                        break;
                    }
                }

                for (int rowIndex = 0; rowIndex < schema.Rows.Count; rowIndex++)
                {
                    int id = int.Parse(
                        schema.Rows[rowIndex].Values[idIndex],
                        System.Globalization.CultureInfo.InvariantCulture);
                    if (!ids.Add(id))
                    {
                        throw new RFrameworkException(
                            $"Config type '{schema.FullRowTypeName}' contains duplicate Id '{id}' "
                            + $"across partitions, including '{schema.SegmentName}'.");
                    }
                }
            }
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

            List<CsvDocument> documents = new List<CsvDocument>(files.Length);
            for (int i = 0; i < files.Length; i++)
            {
                documents.Add(CsvDocumentReader.ReadFile(files[i]));
                report.FileProcessed();
            }

            return ParseLocalizationDocuments(documents).ToList();
        }

        /// <summary>
        /// 解析并校验来自 CSV、Excel 或自定义数据源的 Localization 文档集合。
        /// </summary>
        /// <param name="documents">待解析的内存文档。</param>
        /// <returns>语言代码唯一的本地化表集合。</returns>
        public static IReadOnlyList<LocalizationTable> ParseLocalizationDocuments(
            IReadOnlyList<CsvDocument> documents)
        {
            if (documents == null || documents.Count == 0)
            {
                throw new RFrameworkException(
                    "Localization source documents are empty.");
            }

            List<LocalizationTable> result =
                new List<LocalizationTable>(documents.Count);
            HashSet<string> languages =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < documents.Count; i++)
            {
                if (documents[i] == null)
                {
                    throw new RFrameworkException(
                        $"Localization source document at index {i} is invalid.");
                }

                LocalizationTable localization =
                    LocalizationCsvParser.Parse(documents[i]);
                if (!languages.Add(localization.Language))
                {
                    throw new RFrameworkException(
                        $"Duplicate localization language '{localization.Language}'.");
                }

                result.Add(localization);
            }

            return result;
        }

        private static bool ExportConfigs(
            ConfigPipelineOptions options,
            IReadOnlyList<ConfigTableSchema> configs,
            ConfigPipelineReport report,
            ConfigProtectionExporter protection)
        {
            string codeRoot = ResolveDirectory(options.GeneratedCodeDirectory, false);
            string outputRoot = ResolveDirectory(options.ConfigOutputDirectory, false);
            string jsonRoot = Path.Combine(outputRoot, JsonOutputFolderName);
            string binaryRoot = Path.Combine(outputRoot, BinaryOutputFolderName);
            HashSet<string> codeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> jsonFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> binaryFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool changed = false;
            HashSet<string> generatedTypes =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < configs.Count; i++)
            {
                ConfigTableSchema schema = configs[i];
                string codeFile = schema.RowTypeName + ".g.cs";
                string jsonFile = schema.SegmentName + ".json";
                string binaryFile = schema.SegmentName + ".bytes";
                jsonFiles.Add(jsonFile);
                binaryFiles.Add(binaryFile);

                if (generatedTypes.Add(schema.FullRowTypeName))
                {
                    codeFiles.Add(codeFile);
                    string codePath = Path.Combine(codeRoot, codeFile);
                    if (ConfigCodeGenerator.WriteCodeIfChanged(
                        codePath, ConfigCodeGeneratorRegistry.Current.Generate(schema)))
                    {
                        changed = true;
                        report.FileWritten(ToProjectPath(codePath));
                    }
                    else
                    {
                        report.FileUnchanged(ToProjectPath(codePath));
                    }
                }

                string jsonPath = Path.Combine(jsonRoot, jsonFile);
                if (JsonExportUtility.WriteTextIfChanged(
                    jsonPath, ConfigJsonExporter.Build(schema)))
                {
                    changed = true;
                    report.FileWritten(ToProjectPath(jsonPath));
                }
                else
                {
                    report.FileUnchanged(ToProjectPath(jsonPath));
                }

                string binaryPath = Path.Combine(binaryRoot, binaryFile);
                byte[] releaseBytes = options.ConfigReleaseFormat
                    == ConfigReleaseDataFormat.JsonContent
                    ? Encoding.UTF8.GetBytes(ConfigJsonExporter.Build(schema))
                    : ConfigBinaryExporter.BuildV2(schema);
                ConfigPayloadFormat releaseFormat = options.ConfigReleaseFormat
                    == ConfigReleaseDataFormat.JsonContent
                    ? ConfigPayloadFormat.Json
                    : ConfigPayloadFormat.BinarySingleTable;
                if (protection.WriteBytesIfChanged(
                    binaryPath,
                    binaryFile,
                    ConfigPayloadType.Single,
                    releaseFormat,
                    releaseBytes))
                {
                    changed = true;
                    report.FileWritten(ToProjectPath(binaryPath));
                }
                else
                {
                    report.FileUnchanged(ToProjectPath(binaryPath));
                }
            }

            if (options.ExportConfigBundle)
            {
                string bundleName = ValidateBundleName(options.ConfigBundleName);
                string jsonFile = bundleName + ".json";
                string binaryFile = bundleName + ".bytes";
                jsonFiles.Add(jsonFile);
                binaryFiles.Add(binaryFile);
                string jsonPath = Path.Combine(jsonRoot, jsonFile);
                if (JsonExportUtility.WriteTextIfChanged(
                    jsonPath, ConfigJsonExporter.BuildBundle(configs)))
                {
                    changed = true;
                    report.FileWritten(ToProjectPath(jsonPath));
                }
                else
                {
                    report.FileUnchanged(ToProjectPath(jsonPath));
                }

                string binaryPath = Path.Combine(binaryRoot, binaryFile);
                byte[] releaseBytes = options.ConfigReleaseFormat
                    == ConfigReleaseDataFormat.JsonContent
                    ? Encoding.UTF8.GetBytes(ConfigJsonExporter.BuildBundle(configs))
                    : ConfigBinaryExporter.BuildBundle(configs);
                ConfigPayloadFormat releaseFormat = options.ConfigReleaseFormat
                    == ConfigReleaseDataFormat.JsonContent
                    ? ConfigPayloadFormat.Json
                    : ConfigPayloadFormat.BinaryTableBundle;
                if (protection.WriteBytesIfChanged(
                    binaryPath,
                    binaryFile,
                    ConfigPayloadType.Bundle,
                    releaseFormat,
                    releaseBytes))
                {
                    changed = true;
                    report.FileWritten(ToProjectPath(binaryPath));
                }
                else
                {
                    report.FileUnchanged(ToProjectPath(binaryPath));
                }
            }

            binaryFiles.Add(ConfigProtectionExporter.ManifestName);
            string protectionManifestPath = Path.Combine(
                binaryRoot, ConfigProtectionExporter.ManifestName);
            if (protection.WriteManifest(binaryRoot))
            {
                changed = true;
                report.FileWritten(ToProjectPath(protectionManifestPath));
            }
            else
            {
                report.FileUnchanged(ToProjectPath(protectionManifestPath));
            }

            report.AddMessage(
                options.ConfigBinaryProtection == ConfigProtectionMode.None
                    ? "Release Config bytes protection: disabled."
                    : $"Release Config bytes protection: encrypted and authenticated; "
                        + $"format={options.ConfigReleaseFormat}; "
                        + $"KeyId={options.ConfigProtectionKeyId}; "
                        + $"runtime path prefix={options.ConfigProtectionSourceRoot}.");

            changed |= SynchronizeManifest(
                codeRoot, ConfigCodeManifestName, codeFiles, report);
            changed |= SynchronizeManifest(
                jsonRoot, ConfigJsonManifestName, jsonFiles, report);
            changed |= SynchronizeManifest(
                binaryRoot, ConfigBinaryManifestName, binaryFiles, report);
            return changed;
        }

        private static string ValidateBundleName(string value)
        {
            string name = value?.Trim();
            if (string.IsNullOrEmpty(name)
                || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || name.IndexOf('/') >= 0
                || name.IndexOf('\\') >= 0)
            {
                throw new RFrameworkException("Config bundle file name is invalid.");
            }

            return name;
        }

        private static bool ExportLocalizations(
            ConfigPipelineOptions options,
            IReadOnlyList<LocalizationTable> localizations,
            ConfigPipelineReport report)
        {
            string outputRoot = ResolveDirectory(options.LocalizationOutputDirectory, false);
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

            if (options.ExportLocalizationBundle)
            {
                string bundleName = ValidateOutputFileName(
                    options.LocalizationBundleName, "Localization bundle");
                string jsonFile = bundleName + ".json";
                string binaryFile = bundleName + ".bytes";
                jsonFiles.Add(jsonFile);
                binaryFiles.Add(binaryFile);
                string jsonPath = Path.Combine(jsonRoot, jsonFile);
                if (JsonExportUtility.WriteTextIfChanged(
                    jsonPath, LocalizationJsonExporter.BuildBundle(localizations)))
                {
                    changed = true;
                    report.FileWritten(ToProjectPath(jsonPath));
                }

                string binaryPath = Path.Combine(binaryRoot, binaryFile);
                if (ConfigBinaryExporter.WriteBytesIfChanged(
                    binaryPath, LocalizationBinaryExporter.BuildBundle(localizations)))
                {
                    changed = true;
                    report.FileWritten(ToProjectPath(binaryPath));
                }
            }

            changed |= SynchronizeManifest(
                jsonRoot, LocalizationJsonManifestName, jsonFiles, report);
            changed |= SynchronizeManifest(
                binaryRoot, LocalizationBinaryManifestName, binaryFiles, report);
            return changed;
        }

        private static string ValidateOutputFileName(string value, string displayName)
        {
            string name = value?.Trim();
            if (string.IsNullOrEmpty(name)
                || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || name.IndexOf('/') >= 0
                || name.IndexOf('\\') >= 0)
            {
                throw new RFrameworkException($"{displayName} file name is invalid.");
            }

            return name;
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
