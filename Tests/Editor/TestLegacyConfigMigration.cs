using System;
using System.Collections.Generic;
using System.IO;
using RFramework;
using UnityRFramework.Runtime;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// 将缺少 Price 字段的历史 TestConfigRow Schema 迁移为当前配置表。
    /// </summary>
    internal sealed class TestLegacyConfigMigration : IBinaryConfigMigration
    {
        private readonly float defaultPrice;

        /// <summary>
        /// 初始化测试迁移器。
        /// </summary>
        public TestLegacyConfigMigration(
            ulong sourceSchemaHash, ulong targetSchemaHash, float defaultPrice)
        {
            SourceSchemaHash = sourceSchemaHash;
            TargetSchemaHash = targetSchemaHash;
            this.defaultPrice = defaultPrice;
        }

        /// <inheritdoc/>
        public Type RowType => typeof(TestConfigRow);

        /// <inheritdoc/>
        public ulong SourceSchemaHash { get; }

        /// <inheritdoc/>
        public ulong TargetSchemaHash { get; }

        /// <inheritdoc/>
        public object ReadAndMigrate(BinaryReader reader, int rowCount)
        {
            Dictionary<int, TestConfigRow> result =
                new Dictionary<int, TestConfigRow>(rowCount);
            for (int i = 0; i < rowCount; i++)
            {
                TestConfigRow row = new TestConfigRow
                {
                    Id = reader.ReadInt32(),
                    Name = BinaryFormatUtility.ReadUtf8String(reader, false),
                    Price = defaultPrice
                };
                if (result.ContainsKey(row.Id))
                {
                    throw new RFrameworkException(
                        $"Legacy TestConfigRow contains duplicate Id '{row.Id}'.");
                }

                result.Add(row.Id, row);
            }

            return result;
        }
    }
}
