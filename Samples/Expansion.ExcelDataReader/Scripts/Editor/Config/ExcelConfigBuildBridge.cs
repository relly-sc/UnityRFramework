using System.Collections.Generic;
using UnityRFramework.Editor;

namespace UnityRFramework.Expansion
{
    /// <summary>供核心构建步骤在可选导入本扩展时调用。</summary>
    public static class ExcelConfigBuildBridge
    {
        public static void Validate(ConfigPipelineOptions source)
        {
            ExcelConfigExportService.Validate(CreateOptions(source, false));
        }

        public static int Export(ConfigPipelineOptions source, bool exportJson)
        {
            ExcelConfigExportReport report = ExcelConfigExportService.Export(
                CreateOptions(source, exportJson));
            return report.WrittenFileCount;
        }

        private static ExcelConfigExportOptions CreateOptions(
            ConfigPipelineOptions source,
            bool exportJson)
        {
            var exporters = new List<string> { ExcelConfigExporterIds.Binary };
            if (exportJson) exporters.Insert(0, ExcelConfigExporterIds.Json);
            return new ExcelConfigExportOptions
            {
                SourceDirectory = source.ConfigSourceDirectory,
                OutputDirectory = source.ConfigOutputDirectory,
                GenerateCode = true,
                GeneratedCodeDirectory = source.GeneratedCodeDirectory,
                GeneratedNamespace = source.GeneratedNamespace,
                SelectedExporterIds = exporters,
                ConfigBinaryProtection = source.ConfigBinaryProtection,
                ConfigProtectionKeyId = source.ConfigProtectionKeyId,
                ConfigProtectionKeyFile = source.ConfigProtectionKeyFile,
                ConfigProtectionSourceRoot = source.ConfigProtectionSourceRoot
            };
        }
    }
}
