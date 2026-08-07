using System.Collections.Generic;
using System.Text;
using UnityRFramework.Editor;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 将 Excel 配置表导出为 JsonConfigHelper 可读取的 JSON。
    /// </summary>
    public sealed class ExcelConfigJsonExporter : IExcelConfigExporter
    {
        /// <inheritdoc/>
        public string Id => ExcelConfigExporterIds.Json;

        /// <inheritdoc/>
        public string DisplayName => "JSON";

        /// <inheritdoc/>
        public IReadOnlyList<ExcelConfigOutputFile> Build(
            IReadOnlyList<ConfigTableSchema> schemas)
        {
            List<ExcelConfigOutputFile> outputs =
                new List<ExcelConfigOutputFile>(schemas.Count);
            for (int i = 0; i < schemas.Count; i++)
            {
                ConfigTableSchema schema = schemas[i];
                outputs.Add(new ExcelConfigOutputFile(
                    $"Json/{schema.SegmentName}.json",
                    new UTF8Encoding(false).GetBytes(ConfigJsonExporter.Build(schema))));
            }

            return outputs;
        }
    }
}
