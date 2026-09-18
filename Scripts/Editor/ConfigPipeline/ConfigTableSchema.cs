using System.Collections.Generic;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 一张 Config 表经过校验后的完整定义。
    /// </summary>
    public sealed class ConfigTableSchema
    {
        /// <summary>获取或设置源 CSV 路径。</summary>
        public string SourcePath { get; set; }

        /// <summary>获取或设置表名。</summary>
        public string TableName { get; set; }

        /// <summary>获取或设置容器中的分片名，同时也是单表输出文件名。</summary>
        public string SegmentName { get; set; }

        /// <summary>获取或设置生成代码的命名空间。</summary>
        public string Namespace { get; set; }

        /// <summary>获取或设置配置行类型名称。</summary>
        public string RowTypeName { get; set; }

        /// <summary>获取或设置确定性的表标识。</summary>
        public uint TableId { get; set; }

        /// <summary>获取或设置字段结构哈希。</summary>
        public ulong SchemaHash { get; set; }

        /// <summary>获取或设置字段定义。</summary>
        public IReadOnlyList<ConfigFieldSchema> Fields { get; set; }

        /// <summary>获取或设置数据行。</summary>
        public IReadOnlyList<CsvRow> Rows { get; set; }

        /// <summary>获取配置行完整类型名。</summary>
        public string FullRowTypeName => string.IsNullOrEmpty(Namespace)
            ? RowTypeName
            : Namespace + "." + RowTypeName;
    }
}
