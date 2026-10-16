using System;
using UnityRFramework.Runtime;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// 将历史 LegacyName 字段迁移为当前 Name 字段。
    /// </summary>
    internal sealed class TestLegacyJsonConfigMigration : IJsonConfigMigration
    {
        /// <summary>初始化测试 JSON 迁移器。</summary>
        public TestLegacyJsonConfigMigration(
            ulong sourceSchemaHash, ulong targetSchemaHash)
        {
            SourceSchemaHash = sourceSchemaHash;
            TargetSchemaHash = targetSchemaHash;
        }

        /// <inheritdoc/>
        public Type RowType => typeof(TestConfigRow);

        /// <inheritdoc/>
        public ulong SourceSchemaHash { get; }

        /// <inheritdoc/>
        public ulong TargetSchemaHash { get; }

        /// <inheritdoc/>
        public string Migrate(string sourceJson)
        {
            return sourceJson
                .Replace(
                    SourceSchemaHash.ToString("X16"),
                    TargetSchemaHash.ToString("X16"))
                .Replace("\"LegacyName\"", "\"Name\"");
        }
    }
}
