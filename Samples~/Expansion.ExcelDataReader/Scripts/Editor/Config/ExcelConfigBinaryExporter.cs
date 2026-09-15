using System.Collections.Generic;
using UnityRFramework.Editor;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 将 Excel 配置表导出为 BinaryConfigHelper 可读取的 URFC v2 二进制。
    /// </summary>
    public sealed class ExcelConfigBinaryExporter : IExcelConfigExporter
    {
        /// <inheritdoc/>
        public string Id => ExcelConfigExporterIds.Binary;

        /// <inheritdoc/>
        public string DisplayName => "Binary (URFC v2)";

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
                    $"Binary/{schema.SegmentName}.bytes",
                    ConfigBinaryExporter.BuildV2(schema)));
            }

            return outputs;
        }
    }
}
