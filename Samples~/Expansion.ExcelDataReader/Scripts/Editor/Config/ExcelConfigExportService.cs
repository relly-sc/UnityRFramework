using System;
using System.Collections.Generic;
using System.IO;
using RFramework;
using UnityEditor;
using UnityEngine;
using UnityRFramework.Editor;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 统一执行 Excel 文件发现、Sheet 解析、跨表校验、代码生成和格式导出。
    /// </summary>
    public static class ExcelConfigExportService
    {
        /// <summary>
        /// 校验设置中 Excel 目录的全部配置表，不写入文件。
        /// </summary>
        /// <param name="options">Excel 导出设置。</param>
        /// <returns>校验报告。</returns>
        public static ExcelConfigExportReport Validate(ExcelConfigExportOptions options)
        {
            ValidateOptions(options, false);
            return BuildSchemas(new[] { options.SourceDirectory }, options, out _);
        }

        /// <summary>
        /// 导出设置中 Excel 目录的全部配置表。
        /// </summary>
        /// <param name="options">Excel 导出设置。</param>
        /// <returns>导出报告。</returns>
        public static ExcelConfigExportReport Export(ExcelConfigExportOptions options)
        {
            ValidateOptions(options, true);
            return Export(new[] { options.SourceDirectory }, options);
        }

        /// <summary>
        /// 导出指定 Excel 文件或目录集合。
        /// </summary>
        /// <param name="sourcePaths">Excel 文件或包含 Excel 的目录。</param>
        /// <param name="options">Excel 导出设置。</param>
        /// <returns>导出报告。</returns>
        public static ExcelConfigExportReport Export(
            IReadOnlyList<string> sourcePaths, ExcelConfigExportOptions options)
        {
            ValidateOptions(options, true);
            ExcelConfigExportReport report =
                BuildSchemas(sourcePaths, options, out IReadOnlyList<ConfigTableSchema> schemas);
            string outputRoot =
                ExcelExportUtility.ResolveAssetsDirectory(options.OutputDirectory);
            bool changed = false;

            if (options.GenerateCode)
            {
                string codeRoot =
                    ExcelExportUtility.ResolveAssetsDirectory(
                        options.GeneratedCodeDirectory);
                changed |= GenerateCode(codeRoot, schemas, report);
            }

            HashSet<string> outputPaths =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int exporterIndex = 0;
                 exporterIndex < options.SelectedExporterIds.Count;
                 exporterIndex++)
            {
                string exporterId = options.SelectedExporterIds[exporterIndex];
                if (!ExcelConfigExporterRegistry.TryGet(
                    exporterId, out IExcelConfigExporter exporter))
                {
                    throw new RFrameworkException(
                        $"Excel config exporter '{exporterId}' is not registered.");
                }

                IReadOnlyList<ExcelConfigOutputFile> outputs = exporter.Build(schemas);
                if (outputs == null)
                {
                    throw new RFrameworkException(
                        $"Excel config exporter '{exporter.Id}' returned null.");
                }

                for (int outputIndex = 0; outputIndex < outputs.Count; outputIndex++)
                {
                    ExcelConfigOutputFile output = outputs[outputIndex]
                        ?? throw new RFrameworkException(
                            $"Excel config exporter '{exporter.Id}' returned an invalid file.");
                    string outputPath = ExcelExportUtility.ResolveOutputPath(
                        outputRoot, output.RelativePath);
                    if (!outputPaths.Add(outputPath))
                    {
                        throw new RFrameworkException(
                            $"Multiple Excel exporters target the same file "
                            + $"'{ExcelExportUtility.ToProjectPath(outputPath)}'.");
                    }

                    if (ExcelExportUtility.WriteBytesIfChanged(
                        outputPath, output.Data))
                    {
                        changed = true;
                        report.WrittenFileCount++;
                        report.AddMessage(
                            "Written: " + ExcelExportUtility.ToProjectPath(outputPath));
                    }
                }
            }

            if (changed)
            {
                AssetDatabase.Refresh();
            }

            report.AddMessage(
                $"Export complete. {report.WorkbookCount} workbook(s), "
                + $"{report.TableCount} sheet(s), "
                + $"{report.WrittenFileCount} changed output file(s).");
            return report;
        }

        /// <summary>
        /// 判断路径集合中是否存在可导出的 .xlsx 或 .xls 文件。
        /// </summary>
        /// <param name="sourcePaths">文件或目录路径。</param>
        /// <returns>至少存在一个 Excel 文件时返回 true。</returns>
        public static bool ContainsExcelFiles(IReadOnlyList<string> sourcePaths)
        {
            try
            {
                return ExcelExportUtility.ContainsExcelFiles(sourcePaths);
            }
            catch
            {
                return false;
            }
        }

        private static ExcelConfigExportReport BuildSchemas(
            IReadOnlyList<string> sourcePaths,
            ExcelConfigExportOptions options,
            out IReadOnlyList<ConfigTableSchema> schemas)
        {
            List<string> files = ExcelExportUtility.CollectExcelFiles(sourcePaths);
            if (files.Count == 0)
            {
                throw new RFrameworkException("No .xlsx or .xls files were found.");
            }

            List<CsvDocument> documents = new List<CsvDocument>();
            for (int i = 0; i < files.Count; i++)
            {
                documents.AddRange(ExcelWorkbookReader.Read(files[i]));
            }

            schemas = ConfigPipelineService.ParseConfigDocuments(
                documents, options.GeneratedNamespace?.Trim());
            ExcelConfigExportReport report = new ExcelConfigExportReport
            {
                WorkbookCount = files.Count,
                TableCount = schemas.Count
            };
            report.AddMessage(
                $"Validation passed. {files.Count} workbook(s), {schemas.Count} sheet(s).");
            return report;
        }

        private static bool GenerateCode(
            string codeRoot,
            IReadOnlyList<ConfigTableSchema> schemas,
            ExcelConfigExportReport report)
        {
            bool changed = false;
            HashSet<string> generatedTypes =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < schemas.Count; i++)
            {
                ConfigTableSchema schema = schemas[i];
                if (!generatedTypes.Add(schema.FullRowTypeName))
                {
                    continue;
                }

                string codePath = Path.Combine(codeRoot, schema.RowTypeName + ".g.cs");
                string code = ConfigCodeGeneratorRegistry.Current.Generate(schema);
                if (ConfigCodeGenerator.WriteCodeIfChanged(codePath, code))
                {
                    changed = true;
                    report.WrittenFileCount++;
                report.AddMessage(
                    "Written: " + ExcelExportUtility.ToProjectPath(codePath));
                }
            }

            return changed;
        }

        private static void ValidateOptions(
            ExcelConfigExportOptions options, bool requireExporters)
        {
            if (options == null)
            {
                throw new RFrameworkException("Excel config export options are invalid.");
            }

            if (string.IsNullOrWhiteSpace(options.SourceDirectory))
            {
                throw new RFrameworkException("Excel source directory is empty.");
            }

            if (requireExporters
                && (options.SelectedExporterIds == null
                    || options.SelectedExporterIds.Count == 0))
            {
                throw new RFrameworkException(
                    "Select at least one Excel config output format.");
            }

            if (requireExporters)
            {
                HashSet<string> exporterIds =
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < options.SelectedExporterIds.Count; i++)
                {
                    string exporterId = options.SelectedExporterIds[i]?.Trim();
                    if (string.IsNullOrEmpty(exporterId))
                    {
                        throw new RFrameworkException(
                            "Excel config exporter id is empty.");
                    }

                    if (!exporterIds.Add(exporterId))
                    {
                        throw new RFrameworkException(
                            $"Excel config exporter '{exporterId}' is selected more than once.");
                    }
                }

                ExcelExportUtility.ResolveAssetsDirectory(options.OutputDirectory);
                if (options.GenerateCode)
                {
                    ExcelExportUtility.ResolveAssetsDirectory(
                        options.GeneratedCodeDirectory);
                }
            }
        }
    }
}
