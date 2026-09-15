using System;
using System.Collections.Generic;
using System.IO;
using RFramework;
using UnityEditor;
using UnityRFramework.Editor;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 执行 Excel Localization 发现、校验和多格式导出。
    /// </summary>
    public static class ExcelLocalizationExportService
    {
        /// <summary>
        /// 校验设置目录中的全部 Localization 工作簿。
        /// </summary>
        /// <param name="options">Localization 导出设置。</param>
        /// <returns>校验报告。</returns>
        public static ExcelConfigExportReport Validate(
            ExcelLocalizationExportOptions options)
        {
            ValidateOptions(options, false);
            return BuildTables(
                new[] { options.SourceDirectory }, out _);
        }

        /// <summary>
        /// 导出设置目录中的全部 Localization 工作簿。
        /// </summary>
        /// <param name="options">Localization 导出设置。</param>
        /// <returns>导出报告。</returns>
        public static ExcelConfigExportReport Export(
            ExcelLocalizationExportOptions options)
        {
            ValidateOptions(options, true);
            return Export(new[] { options.SourceDirectory }, options);
        }

        /// <summary>
        /// 导出指定 Excel 文件或目录集合。
        /// </summary>
        /// <param name="sourcePaths">Excel 文件或目录集合。</param>
        /// <param name="options">Localization 导出设置。</param>
        /// <returns>导出报告。</returns>
        public static ExcelConfigExportReport Export(
            IReadOnlyList<string> sourcePaths,
            ExcelLocalizationExportOptions options)
        {
            ValidateOptions(options, true);
            ExcelConfigExportReport report =
                BuildTables(
                    sourcePaths,
                    out IReadOnlyList<LocalizationTable> localizations);
            string outputRoot =
                ExcelExportUtility.ResolveAssetsDirectory(options.OutputDirectory);
            string bundleName = options.ExportBundle
                ? ValidateBundleName(options.BundleName)
                : string.Empty;
            HashSet<string> outputPaths =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool changed = false;

            for (int exporterIndex = 0;
                 exporterIndex < options.SelectedExporterIds.Count;
                 exporterIndex++)
            {
                string exporterId = options.SelectedExporterIds[exporterIndex];
                if (!ExcelLocalizationExporterRegistry.TryGet(
                    exporterId, out IExcelLocalizationExporter exporter))
                {
                    throw new RFrameworkException(
                        $"Excel localization exporter '{exporterId}' is not registered.");
                }

                IReadOnlyList<ExcelLocalizationOutputFile> outputs =
                    exporter.Build(
                        localizations, options.ExportBundle, bundleName);
                if (outputs == null)
                {
                    throw new RFrameworkException(
                        $"Excel localization exporter '{exporter.Id}' returned null.");
                }

                for (int outputIndex = 0;
                     outputIndex < outputs.Count;
                     outputIndex++)
                {
                    ExcelLocalizationOutputFile output = outputs[outputIndex]
                        ?? throw new RFrameworkException(
                            $"Excel localization exporter '{exporter.Id}' "
                            + "returned an invalid file.");
                    string outputPath = ExcelExportUtility.ResolveOutputPath(
                        outputRoot, output.RelativePath);
                    if (!outputPaths.Add(outputPath))
                    {
                        throw new RFrameworkException(
                            "Multiple Excel localization exporters target the same "
                            + $"file '{ExcelExportUtility.ToProjectPath(outputPath)}'.");
                    }

                    if (ExcelExportUtility.WriteBytesIfChanged(
                        outputPath, output.Data))
                    {
                        changed = true;
                        report.WrittenFileCount++;
                        report.AddMessage(
                            "Written: "
                            + ExcelExportUtility.ToProjectPath(outputPath));
                    }
                }
            }

            if (changed)
            {
                AssetDatabase.Refresh();
            }

            report.AddMessage(
                $"Export complete. {report.WorkbookCount} workbook(s), "
                + $"{report.TableCount} language(s), "
                + $"{report.WrittenFileCount} changed output file(s).");
            return report;
        }

        private static ExcelConfigExportReport BuildTables(
            IReadOnlyList<string> sourcePaths,
            out IReadOnlyList<LocalizationTable> localizations)
        {
            List<string> files =
                ExcelExportUtility.CollectExcelFiles(sourcePaths);
            if (files.Count == 0)
            {
                throw new RFrameworkException(
                    "No .xlsx or .xls localization files were found.");
            }

            List<CsvDocument> documents = new List<CsvDocument>();
            for (int i = 0; i < files.Count; i++)
            {
                documents.AddRange(ExcelWorkbookReader.Read(files[i]));
            }

            localizations =
                ConfigPipelineService.ParseLocalizationDocuments(documents);
            ExcelConfigExportReport report = new ExcelConfigExportReport
            {
                WorkbookCount = files.Count,
                TableCount = localizations.Count
            };
            report.AddMessage(
                $"Validation passed. {files.Count} workbook(s), "
                + $"{localizations.Count} language(s).");
            return report;
        }

        private static void ValidateOptions(
            ExcelLocalizationExportOptions options,
            bool requireExporters)
        {
            if (options == null)
            {
                throw new RFrameworkException(
                    "Excel localization export options are invalid.");
            }

            if (string.IsNullOrWhiteSpace(options.SourceDirectory))
            {
                throw new RFrameworkException(
                    "Excel localization source directory is empty.");
            }

            if (requireExporters
                && (options.SelectedExporterIds == null
                    || options.SelectedExporterIds.Count == 0))
            {
                throw new RFrameworkException(
                    "Select at least one Excel localization output format.");
            }

            if (!requireExporters)
            {
                return;
            }

            HashSet<string> exporterIds =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < options.SelectedExporterIds.Count; i++)
            {
                string exporterId = options.SelectedExporterIds[i]?.Trim();
                if (string.IsNullOrEmpty(exporterId))
                {
                    throw new RFrameworkException(
                        "Excel localization exporter id is empty.");
                }

                if (!exporterIds.Add(exporterId))
                {
                    throw new RFrameworkException(
                        $"Excel localization exporter '{exporterId}' "
                        + "is selected more than once.");
                }
            }

            ExcelExportUtility.ResolveAssetsDirectory(options.OutputDirectory);
            if (options.ExportBundle)
            {
                ValidateBundleName(options.BundleName);
            }
        }

        private static string ValidateBundleName(string value)
        {
            string name = value?.Trim();
            if (string.IsNullOrEmpty(name)
                || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || name.IndexOf('/') >= 0
                || name.IndexOf('\\') >= 0)
            {
                throw new RFrameworkException(
                    "Localization bundle file name is invalid.");
            }

            return name;
        }
    }
}
