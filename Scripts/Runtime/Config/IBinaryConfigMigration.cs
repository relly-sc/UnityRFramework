using System;
using System.IO;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 将一个历史 URFC v2 Schema 的 Body 读取并转换为当前配置表对象。
    /// </summary>
    public interface IBinaryConfigMigration
    {
        /// <summary>获取迁移目标的当前配置行类型。</summary>
        Type RowType { get; }

        /// <summary>获取该迁移器能够读取的历史 SchemaHash。</summary>
        ulong SourceSchemaHash { get; }

        /// <summary>获取迁移完成后的当前 SchemaHash。</summary>
        ulong TargetSchemaHash { get; }

        /// <summary>
        /// 按历史字段布局读取 Body，并返回供 IConfigHelper 查询的当前强类型表对象。
        /// </summary>
        /// <param name="reader">已定位到 URFC Body 起点的读取器。</param>
        /// <param name="rowCount">Header 中声明的配置行数。</param>
        /// <returns>迁移后的当前配置表对象。</returns>
        object ReadAndMigrate(BinaryReader reader, int rowCount);
    }
}
