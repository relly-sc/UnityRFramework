using System;
using System.IO;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// URFC v2 生成代码读取器。每张配置表由生成代码提供一个实现。
    /// </summary>
    public interface IBinaryConfigCodec
    {
        /// <summary>获取该 Codec 对应的配置行类型。</summary>
        Type RowType { get; }

        /// <summary>获取配置表确定性标识。</summary>
        uint TableId { get; }

        /// <summary>获取配置表字段结构哈希。</summary>
        ulong SchemaHash { get; }

        /// <summary>
        /// 从 URFC v2 Body 读取完整配置表。
        /// </summary>
        /// <param name="reader">已定位到 Body 起点的二进制读取器。</param>
        /// <param name="rowCount">Header 中声明的配置行数。</param>
        /// <returns>供 IConfigHelper 查询的强类型表对象。</returns>
        object ReadTable(BinaryReader reader, int rowCount);
    }
}
