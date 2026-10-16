using System;
using System.Collections.Generic;
using System.IO;
using UnityRFramework.Runtime;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>复杂字段 URFC v2 闭环测试读取器。</summary>
    internal sealed class TestComplexConfigCodec : IBinaryConfigCodec
    {
        public TestComplexConfigCodec(uint tableId, ulong schemaHash)
        {
            TableId = tableId;
            SchemaHash = schemaHash;
        }

        public Type RowType => typeof(TestComplexConfig);
        public uint TableId { get; }
        public ulong SchemaHash { get; }

        public object ReadTable(BinaryReader reader, int rowCount)
        {
            Dictionary<int, TestComplexConfig> result =
                new Dictionary<int, TestComplexConfig>(rowCount);
            for (int i = 0; i < rowCount; i++)
            {
                TestComplexConfig row = new TestComplexConfig
                {
                    Id = reader.ReadInt32(),
                    State = (TestComplexConfigStateEnum)reader.ReadInt32(),
                    Levels = BinaryConfigCollectionUtility.ReadArray<int>(
                        reader, valueReader => valueReader.ReadInt32()),
                    Tags = BinaryConfigCollectionUtility.ReadList<string>(
                        reader, valueReader =>
                            BinaryFormatUtility.ReadUtf8String(valueReader, false)),
                    Description = BinaryFormatUtility.ReadUtf8String(reader, false)
                };
                result.Add(row.Id, row);
            }

            return result;
        }
    }
}
