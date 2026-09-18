using System;
using System.Collections.Generic;
using System.IO;
using UnityRFramework.Runtime;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// 包含自定义字段的 URFC v2 测试整表 Codec。
    /// </summary>
    internal sealed class TestCustomConfigCodec : IBinaryConfigCodec
    {
        /// <summary>
        /// 初始化包含自定义字段的测试整表 Codec。
        /// </summary>
        /// <param name="tableId">表标识。</param>
        /// <param name="schemaHash">字段结构哈希。</param>
        public TestCustomConfigCodec(uint tableId, ulong schemaHash)
        {
            TableId = tableId;
            SchemaHash = schemaHash;
        }

        /// <inheritdoc/>
        public Type RowType => typeof(TestCustomConfig);

        /// <inheritdoc/>
        public uint TableId { get; }

        /// <inheritdoc/>
        public ulong SchemaHash { get; }

        /// <inheritdoc/>
        public object ReadTable(BinaryReader reader, int rowCount)
        {
            Dictionary<int, TestCustomConfig> result =
                new Dictionary<int, TestCustomConfig>(rowCount);
            for (int i = 0; i < rowCount; i++)
            {
                TestCustomConfig row = new TestCustomConfig
                {
                    Id = reader.ReadInt32(),
                    Point = ConfigFieldCodecRegistry.ReadBinary<TestCustomValue>(
                        "point2", 1u, reader)
                };
                result.Add(row.Id, row);
            }

            return result;
        }
    }
}
