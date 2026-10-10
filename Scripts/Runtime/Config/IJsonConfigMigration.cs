using System;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 将一个历史 Config JSON Schema 转换为当前 Schema JSON。
    /// </summary>
    public interface IJsonConfigMigration
    {
        /// <summary>获取迁移目标的当前配置行类型。</summary>
        Type RowType { get; }

        /// <summary>获取该迁移器能够读取的历史 SchemaHash。</summary>
        ulong SourceSchemaHash { get; }

        /// <summary>获取迁移完成后的当前 SchemaHash。</summary>
        ulong TargetSchemaHash { get; }

        /// <summary>
        /// 转换历史 JSON。返回值必须包含与 TargetSchemaHash 一致的表元数据。
        /// </summary>
        string Migrate(string sourceJson);
    }
}
