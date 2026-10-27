using System;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 配置行类型对应的稳定表标识和当前字段结构哈希。
    /// </summary>
    public readonly struct ConfigSchemaInfo
    {
        /// <summary>初始化配置 Schema 信息。</summary>
        public ConfigSchemaInfo(Type rowType, uint tableId, ulong schemaHash)
        {
            RowType = rowType;
            TableId = tableId;
            SchemaHash = schemaHash;
        }

        /// <summary>获取配置行类型。</summary>
        public Type RowType { get; }

        /// <summary>获取稳定表标识。</summary>
        public uint TableId { get; }

        /// <summary>获取当前字段结构哈希。</summary>
        public ulong SchemaHash { get; }
    }
}
