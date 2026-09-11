using System;
using System.Collections.Generic;
using System.IO;
using UnityRFramework.Runtime;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// URFC v2 闭环测试使用的静态读取器。
    /// </summary>
    internal sealed class TestConfigRowCodec : IBinaryConfigCodec
    {
        /// <summary>
        /// 初始化测试 Codec。
        /// </summary>
        /// <param name="tableId">表标识。</param>
        /// <param name="schemaHash">字段结构哈希。</param>
        public TestConfigRowCodec(uint tableId, ulong schemaHash)
        {
            TableId = tableId;
            SchemaHash = schemaHash;
        }

        /// <inheritdoc/>
        public Type RowType => typeof(TestConfigRow);

        /// <inheritdoc/>
        public uint TableId { get; }

        /// <inheritdoc/>
        public ulong SchemaHash { get; }

        /// <inheritdoc/>
        public object ReadTable(BinaryReader reader, int rowCount)
        {
            Dictionary<int, TestConfigRow> result =
                new Dictionary<int, TestConfigRow>(rowCount);
            for (int i = 0; i < rowCount; i++)
            {
                TestConfigRow row = new TestConfigRow
                {
                    Id = reader.ReadInt32(),
                    Name = BinaryFormatUtility.ReadUtf8String(reader, false),
                    Price = reader.ReadSingle()
                };
                result.Add(row.Id, row);
            }

            return result;
        }
    }
}
