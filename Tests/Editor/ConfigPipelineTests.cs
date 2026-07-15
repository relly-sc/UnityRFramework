using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using RFramework;
using UnityEngine;
using UnityRFramework.Editor;
using UnityRFramework.Runtime;
using Object = UnityEngine.Object;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// Config 转换工具与二进制协议的 EditMode 测试。
    /// </summary>
    public sealed class ConfigPipelineTests
    {
        /// <summary>验证 CSV 引号、逗号、转义引号和跨行字段。</summary>
        [Test]
        public void CsvReaderHandlesQuotesCommaAndNewline()
        {
            const string csv = "Id,Text\nint,string\n编号,文本\n1,\"hello, \"\"world\"\"\"\n2,\"line1\nline2\"";
            CsvDocument document = CsvDocumentReader.Parse("memory.csv", csv);

            Assert.AreEqual(5, document.Rows.Count);
            Assert.AreEqual("hello, \"world\"", document.Rows[3].Values[1]);
            Assert.AreEqual("line1\nline2", document.Rows[4].Values[1]);
        }

        /// <summary>验证重复 Config Id 会在导出前被拒绝。</summary>
        [Test]
        public void ConfigSchemaRejectsDuplicateId()
        {
            const string csv = "Id,Name\nint,string\n编号,名称\n1,A\n1,B";
            CsvDocument document = CsvDocumentReader.Parse("Role.csv", csv);

            Assert.Throws<RFrameworkException>(() =>
                ConfigSchemaParser.ParseConfig(document, "Game.Config"));
        }

        /// <summary>Verifies that Localization uses the shared three-row header format.</summary>
        [Test]
        public void LocalizationCsvUsesThreeHeaderRows()
        {
            const string csv =
                "Key,Value\nstring,string\nLocalization key,Localized text\nui_login,Login";
            CsvDocument document = CsvDocumentReader.Parse("en-US.csv", csv);

            LocalizationTable table = LocalizationCsvParser.Parse(document);

            Assert.AreEqual("en-US", table.Language);
            Assert.AreEqual(1, table.Entries.Count);
            Assert.AreEqual("ui_login", table.Entries[0].Key);
            Assert.AreEqual("Login", table.Entries[0].Value);
        }

        /// <summary>Verifies that the legacy two-row Localization format is rejected.</summary>
        [Test]
        public void LocalizationCsvRejectsMissingTypeAndCommentRows()
        {
            const string csv = "Key,Value\nui_login,Login";
            CsvDocument document = CsvDocumentReader.Parse("en-US.csv", csv);

            Assert.Throws<RFrameworkException>(() => LocalizationCsvParser.Parse(document));
        }

        /// <summary>验证 Config JSON 导出结果可由 JsonConfigHelper 回读。</summary>
        [Test]
        public void ConfigJsonRoundTripsThroughJsonHelper()
        {
            Utility.Json.SetJsonHelper(new DefaultJsonHelper());
            ConfigTableSchema schema = CreateSchema();
            ConfigSchemaRegistry.Register(
                typeof(TestConfigRow), schema.TableId, schema.SchemaHash);
            string json = ConfigJsonExporter.Build(schema);
            StringAssert.Contains("\"Tables\"", json);
            StringAssert.Contains("\"TestConfigRow\"", json);
            StringAssert.Contains("\"TableId\"", json);
            StringAssert.Contains("\"SchemaHash\"", json);
            StringAssert.Contains("\"Rows\"", json);
            StringAssert.DoesNotContain("\"Items\"", json);
            GameObject owner = new GameObject("JsonConfigHelper Tests");
            try
            {
                JsonConfigHelper helper = owner.AddComponent<JsonConfigHelper>();
                object table = helper.ParseConfig(typeof(TestConfigRow), Encoding.UTF8.GetBytes(json));
                TestConfigRow first = helper.GetConfig<TestConfigRow>(table, 1);
                TestConfigRow second = helper.GetConfig<TestConfigRow>(table, 2);

                Assert.AreEqual("Sword", first.Name);
                Assert.AreEqual(12.5f, first.Price);
                Assert.AreEqual("Shield", second.Name);
                Assert.AreEqual(20f, second.Price);
            }
            finally
            {
                ConfigSchemaRegistry.Unregister(typeof(TestConfigRow));
                Object.DestroyImmediate(owner);
            }
        }

        /// <summary>验证 JSON Helper 仍可读取旧 Items 包装格式。</summary>
        [Test]
        public void ConfigJsonReaderSupportsLegacyItemsEnvelope()
        {
            const string json =
                "{\"Items\":[{\"Id\":1,\"Name\":\"Legacy\",\"Price\":3.5}]}";
            GameObject owner = new GameObject("Legacy Config JSON Tests");
            try
            {
                JsonConfigHelper helper = owner.AddComponent<JsonConfigHelper>();
                object table = helper.ParseConfig(
                    typeof(TestConfigRow), Encoding.UTF8.GetBytes(json));

                Assert.AreEqual("Legacy", helper.GetConfig<TestConfigRow>(table, 1).Name);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        /// <summary>验证带元数据的历史 JSON 只有显式注册迁移器后才能读取。</summary>
        [Test]
        public void ConfigJsonMigratesRegisteredLegacySchema()
        {
            ConfigTableSchema currentSchema = CreateSchema();
            ConfigTableSchema legacySchema = CreateLegacyJsonSchema();
            ConfigSchemaRegistry.Register(
                typeof(TestConfigRow), currentSchema.TableId, currentSchema.SchemaHash);
            string legacyJson = ConfigJsonExporter.Build(legacySchema);
            GameObject owner = new GameObject("JSON Config Migration Tests");
            try
            {
                JsonConfigHelper helper = owner.AddComponent<JsonConfigHelper>();
                Assert.Throws<RFrameworkException>(() => helper.ParseConfigFromString(
                    typeof(TestConfigRow), legacyJson));

                JsonConfigMigrationRegistry.Register(new TestLegacyJsonConfigMigration(
                    legacySchema.SchemaHash, currentSchema.SchemaHash));
                object table = helper.ParseConfigFromString(
                    typeof(TestConfigRow), legacyJson);
                TestConfigRow first = helper.GetConfig<TestConfigRow>(table, 1);

                Assert.AreEqual("Sword", first.Name);
                Assert.AreEqual(0f, first.Price);
            }
            finally
            {
                JsonConfigMigrationRegistry.Unregister(
                    typeof(TestConfigRow), legacySchema.SchemaHash);
                ConfigSchemaRegistry.Unregister(typeof(TestConfigRow));
                Object.DestroyImmediate(owner);
            }
        }

        /// <summary>验证 JSON 迁移器目标必须与当前 Schema 完全一致。</summary>
        [Test]
        public void ConfigJsonRejectsMigrationForAnotherTargetSchema()
        {
            ConfigTableSchema currentSchema = CreateSchema();
            ConfigTableSchema legacySchema = CreateLegacyJsonSchema();
            ConfigSchemaRegistry.Register(
                typeof(TestConfigRow), currentSchema.TableId, currentSchema.SchemaHash);
            JsonConfigMigrationRegistry.Register(new TestLegacyJsonConfigMigration(
                legacySchema.SchemaHash, currentSchema.SchemaHash + 1));
            GameObject owner = new GameObject("JSON Config Migration Target Tests");
            try
            {
                JsonConfigHelper helper = owner.AddComponent<JsonConfigHelper>();
                Assert.Throws<RFrameworkException>(() => helper.ParseConfigFromString(
                    typeof(TestConfigRow), ConfigJsonExporter.Build(legacySchema)));
            }
            finally
            {
                JsonConfigMigrationRegistry.Unregister(
                    typeof(TestConfigRow), legacySchema.SchemaHash);
                ConfigSchemaRegistry.Unregister(typeof(TestConfigRow));
                Object.DestroyImmediate(owner);
            }
        }

        /// <summary>验证多表 JSON 容器会按配置行类型选择对应表。</summary>
        [Test]
        public void ConfigJsonReaderSelectsNamedTableFromMultiTableEnvelope()
        {
            const string json =
                "{\"Tables\":{"
                + "\"Other\":[{\"Id\":9,\"Name\":\"Wrong\",\"Price\":0}],"
                + "\"TestConfigRow\":[{\"Id\":2,\"Name\":\"Selected\",\"Price\":8.5}]}}";
            GameObject owner = new GameObject("Multi-table Config JSON Tests");
            try
            {
                JsonConfigHelper helper = owner.AddComponent<JsonConfigHelper>();
                object table = helper.ParseConfig(
                    typeof(TestConfigRow), Encoding.UTF8.GetBytes(json));

                Assert.AreEqual("Selected", helper.GetConfig<TestConfigRow>(table, 2).Name);
                Assert.IsNull(helper.GetConfig<TestConfigRow>(table, 9));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        /// <summary>验证 JSON 与二进制容器都能合并同一类型的多个分片。</summary>
        [Test]
        public void ConfigBundlesMergeSameTypePartitions()
        {
            ConfigTableSchema low = CreatePartitionSchema(
                "TestConfigRow@Low", new CsvRow(4, new[] { "1", "Sword", "12.5" }));
            ConfigTableSchema high = CreatePartitionSchema(
                "TestConfigRow@High", new CsvRow(4, new[] { "2", "Shield", "20" }));
            ConfigSchemaRegistry.Register(
                typeof(TestConfigRow), low.TableId, low.SchemaHash);
            BinaryConfigCodecRegistry.Register(
                new TestConfigRowCodec(low.TableId, low.SchemaHash));
            GameObject owner = new GameObject("Config Bundle Tests");
            try
            {
                JsonConfigHelper jsonHelper = owner.AddComponent<JsonConfigHelper>();
                IReadOnlyDictionary<Type, object> jsonTables = jsonHelper.ParseConfigBundle(
                    Encoding.UTF8.GetBytes(ConfigJsonExporter.BuildBundle(new[] { low, high })));
                Assert.AreEqual("Sword", jsonHelper.GetConfig<TestConfigRow>(
                    jsonTables[typeof(TestConfigRow)], 1).Name);
                Assert.AreEqual("Shield", jsonHelper.GetConfig<TestConfigRow>(
                    jsonTables[typeof(TestConfigRow)], 2).Name);

                BinaryConfigHelper binaryHelper = owner.AddComponent<BinaryConfigHelper>();
                IReadOnlyDictionary<Type, object> binaryTables = binaryHelper.ParseConfigBundle(
                    ConfigBinaryExporter.BuildBundle(new[] { low, high }));
                Assert.AreEqual("Sword", binaryHelper.GetConfig<TestConfigRow>(
                    binaryTables[typeof(TestConfigRow)], 1).Name);
                Assert.AreEqual("Shield", binaryHelper.GetConfig<TestConfigRow>(
                    binaryTables[typeof(TestConfigRow)], 2).Name);
            }
            finally
            {
                BinaryConfigCodecRegistry.Unregister(typeof(TestConfigRow));
                ConfigSchemaRegistry.Unregister(typeof(TestConfigRow));
                Object.DestroyImmediate(owner);
            }
        }

        /// <summary>验证跨分片重复 Id 会使 JSON 与二进制容器整体失败。</summary>
        [Test]
        public void ConfigBundlesRejectDuplicateIdsAcrossPartitions()
        {
            ConfigTableSchema first = CreatePartitionSchema(
                "TestConfigRow@First", new CsvRow(4, new[] { "1", "Sword", "12.5" }));
            ConfigTableSchema duplicate = CreatePartitionSchema(
                "TestConfigRow@Duplicate", new CsvRow(4, new[] { "1", "Shield", "20" }));
            ConfigSchemaRegistry.Register(
                typeof(TestConfigRow), first.TableId, first.SchemaHash);
            BinaryConfigCodecRegistry.Register(
                new TestConfigRowCodec(first.TableId, first.SchemaHash));
            GameObject owner = new GameObject("Config Bundle Duplicate Tests");
            try
            {
                JsonConfigHelper jsonHelper = owner.AddComponent<JsonConfigHelper>();
                Assert.Throws<RFrameworkException>(() => jsonHelper.ParseConfigBundle(
                    Encoding.UTF8.GetBytes(
                        ConfigJsonExporter.BuildBundle(new[] { first, duplicate }))));

                BinaryConfigHelper binaryHelper = owner.AddComponent<BinaryConfigHelper>();
                Assert.Throws<RFrameworkException>(() => binaryHelper.ParseConfigBundle(
                    ConfigBinaryExporter.BuildBundle(new[] { first, duplicate })));
            }
            finally
            {
                BinaryConfigCodecRegistry.Unregister(typeof(TestConfigRow));
                ConfigSchemaRegistry.Unregister(typeof(TestConfigRow));
                Object.DestroyImmediate(owner);
            }
        }

        /// <summary>验证 Localization JSON 导出、转义和 JsonLocalizationHelper 回读。</summary>
        [Test]
        public void LocalizationJsonRoundTripsThroughJsonHelper()
        {
            Utility.Json.SetJsonHelper(new DefaultJsonHelper());
            LocalizationTable source = new LocalizationTable
            {
                Language = "en",
                SourcePath = "memory.csv",
                Entries = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("ui_login", "Log \"in\""),
                    new KeyValuePair<string, string>("ui_tip", "Line 1\nLine 2")
                }
            };

            string json = LocalizationJsonExporter.Build(source);
            GameObject owner = new GameObject("JsonLocalizationHelper Tests");
            try
            {
                JsonLocalizationHelper helper = owner.AddComponent<JsonLocalizationHelper>();
                Dictionary<string, string> parsed = helper.ParseLanguage(
                    "en", Encoding.UTF8.GetBytes(json));

                Assert.AreEqual("Log \"in\"", parsed["ui_login"]);
                Assert.AreEqual("Line 1\nLine 2", parsed["ui_tip"]);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        /// <summary>验证 URFL v2 导出结果可由 Runtime Helper 回读。</summary>
        [Test]
        public void LocalizationV2RoundTrips()
        {
            LocalizationTable source = new LocalizationTable
            {
                Language = "zh-CN",
                SourcePath = "memory.csv",
                Entries = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("ui_login", "登录"),
                    new KeyValuePair<string, string>("ui_exit", "退出")
                }
            };

            byte[] bytes = LocalizationBinaryExporter.BuildV2(source);
            GameObject owner = new GameObject("BinaryLocalizationHelper Tests");
            try
            {
                BinaryLocalizationHelper helper = owner.AddComponent<BinaryLocalizationHelper>();
                Dictionary<string, string> parsed = helper.ParseLanguage("zh-CN", bytes);
                Assert.AreEqual("登录", parsed["ui_login"]);
                Assert.AreEqual("退出", parsed["ui_exit"]);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        /// <summary>验证 URFL v2 会拒绝内容损坏，并保留 URFL v1 读取兼容。</summary>
        [Test]
        public void LocalizationV2RejectsCorruptBodyAndReadsV1()
        {
            LocalizationTable source = new LocalizationTable
            {
                Language = "en",
                SourcePath = "memory.csv",
                Entries = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("ui_login", "Login")
                }
            };

            byte[] v2 = LocalizationBinaryExporter.BuildV2(source);
            byte[] v1 = LocalizationBinaryExporter.BuildV1(source);
            GameObject owner = new GameObject("BinaryLocalizationHelper CRC Tests");
            try
            {
                BinaryLocalizationHelper helper = owner.AddComponent<BinaryLocalizationHelper>();
                Assert.AreEqual("Login", helper.ParseLanguage("en", v1)["ui_login"]);

                v2[v2.Length - 1] ^= 0x7f;
                Assert.Throws<RFrameworkException>(() => helper.ParseLanguage("en", v2));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        /// <summary>验证 URFC v2 可通过生成 Codec 契约完成闭环读取。</summary>
        [Test]
        public void ConfigV2RoundTripsThroughGeneratedCodecContract()
        {
            ConfigTableSchema schema = CreateSchema();
            BinaryConfigCodecRegistry.Register(
                new TestConfigRowCodec(schema.TableId, schema.SchemaHash));
            byte[] bytes = ConfigBinaryExporter.BuildV2(schema);

            GameObject owner = new GameObject("BinaryConfigHelper Tests");
            try
            {
                BinaryConfigHelper helper = owner.AddComponent<BinaryConfigHelper>();
                object table = helper.ParseConfig(typeof(TestConfigRow), bytes);
                TestConfigRow first = helper.GetConfig<TestConfigRow>(table, 1);
                TestConfigRow second = helper.GetConfig<TestConfigRow>(table, 2);
                Assert.AreEqual("Sword", first.Name);
                Assert.AreEqual(12.5f, first.Price);
                Assert.AreEqual("Shield", second.Name);
                Assert.AreEqual(20f, second.Price);
            }
            finally
            {
                BinaryConfigCodecRegistry.Unregister(typeof(TestConfigRow));
                Object.DestroyImmediate(owner);
            }
        }

        /// <summary>验证 Schema 不一致和 Body 损坏都会阻止配置提交。</summary>
        [Test]
        public void ConfigV2RejectsSchemaMismatchAndCorruptBody()
        {
            ConfigTableSchema schema = CreateSchema();
            byte[] bytes = ConfigBinaryExporter.BuildV2(schema);
            BinaryConfigCodecRegistry.Register(
                new TestConfigRowCodec(schema.TableId, schema.SchemaHash + 1));

            GameObject owner = new GameObject("BinaryConfigHelper Failure Tests");
            try
            {
                BinaryConfigHelper helper = owner.AddComponent<BinaryConfigHelper>();
                Assert.Throws<RFrameworkException>(() =>
                    helper.ParseConfig(typeof(TestConfigRow), bytes));

                BinaryConfigCodecRegistry.Register(
                    new TestConfigRowCodec(schema.TableId, schema.SchemaHash));
                bytes[bytes.Length - 1] ^= 0x7f;
                Assert.Throws<RFrameworkException>(() =>
                    helper.ParseConfig(typeof(TestConfigRow), bytes));
            }
            finally
            {
                BinaryConfigCodecRegistry.Unregister(typeof(TestConfigRow));
                Object.DestroyImmediate(owner);
            }
        }

        /// <summary>验证历史 URFC Schema 只有显式注册迁移器后才能读取。</summary>
        [Test]
        public void ConfigV2MigratesRegisteredLegacySchema()
        {
            ConfigTableSchema currentSchema = CreateSchema();
            ConfigTableSchema legacySchema = CreateLegacySchema();
            byte[] legacyBytes = ConfigBinaryExporter.BuildV2(legacySchema);
            BinaryConfigCodecRegistry.Register(
                new TestConfigRowCodec(currentSchema.TableId, currentSchema.SchemaHash));

            GameObject owner = new GameObject("Binary Config Migration Tests");
            try
            {
                BinaryConfigHelper helper = owner.AddComponent<BinaryConfigHelper>();
                Assert.Throws<RFrameworkException>(() =>
                    helper.ParseConfig(typeof(TestConfigRow), legacyBytes));

                BinaryConfigMigrationRegistry.Register(new TestLegacyConfigMigration(
                    legacySchema.SchemaHash, currentSchema.SchemaHash, 99f));
                object table = helper.ParseConfig(typeof(TestConfigRow), legacyBytes);
                TestConfigRow first = helper.GetConfig<TestConfigRow>(table, 1);
                TestConfigRow second = helper.GetConfig<TestConfigRow>(table, 2);

                Assert.AreEqual("Sword", first.Name);
                Assert.AreEqual(99f, first.Price);
                Assert.AreEqual("Shield", second.Name);
                Assert.AreEqual(99f, second.Price);
            }
            finally
            {
                BinaryConfigMigrationRegistry.Unregister(
                    typeof(TestConfigRow), legacySchema.SchemaHash);
                BinaryConfigCodecRegistry.Unregister(typeof(TestConfigRow));
                Object.DestroyImmediate(owner);
            }
        }

        /// <summary>验证迁移器目标必须与当前 Codec Schema 完全一致。</summary>
        [Test]
        public void ConfigV2RejectsMigrationForAnotherTargetSchema()
        {
            ConfigTableSchema currentSchema = CreateSchema();
            ConfigTableSchema legacySchema = CreateLegacySchema();
            BinaryConfigCodecRegistry.Register(
                new TestConfigRowCodec(currentSchema.TableId, currentSchema.SchemaHash));
            BinaryConfigMigrationRegistry.Register(new TestLegacyConfigMigration(
                legacySchema.SchemaHash, currentSchema.SchemaHash + 1, 0f));

            GameObject owner = new GameObject("Binary Config Migration Target Tests");
            try
            {
                BinaryConfigHelper helper = owner.AddComponent<BinaryConfigHelper>();
                Assert.Throws<RFrameworkException>(() => helper.ParseConfig(
                    typeof(TestConfigRow), ConfigBinaryExporter.BuildV2(legacySchema)));
            }
            finally
            {
                BinaryConfigMigrationRegistry.Unregister(
                    typeof(TestConfigRow), legacySchema.SchemaHash);
                BinaryConfigCodecRegistry.Unregister(typeof(TestConfigRow));
                Object.DestroyImmediate(owner);
            }
        }

        /// <summary>验证枚举、数组、List 和字符串换行的 Schema 与 JSON 闭环。</summary>
        [Test]
        public void ComplexFieldsRoundTripThroughJsonHelper()
        {
            ConfigTableSchema schema = CreateComplexSchema();
            string code = ConfigCodeGenerator.Generate(schema);
            StringAssert.Contains("public enum TestComplexConfigStateEnum", code);
            StringAssert.Contains("BinaryConfigCollectionUtility.ReadArray<int>", code);
            StringAssert.Contains("BinaryConfigCollectionUtility.ReadList<string>", code);
            string[] generatedLines = code.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < generatedLines.Length; i++)
            {
                Assert.LessOrEqual(
                    generatedLines[i].Length, 120,
                    $"Generated complex line {i + 1} is too long.");
            }

            Utility.Json.SetJsonHelper(new DefaultJsonHelper());
            string json = ConfigJsonExporter.Build(schema);
            ConfigSchemaRegistry.Register(
                typeof(TestComplexConfig), schema.TableId, schema.SchemaHash);
            GameObject owner = new GameObject("Complex JsonConfigHelper Tests");
            try
            {
                JsonConfigHelper helper = owner.AddComponent<JsonConfigHelper>();
                object table = helper.ParseConfig(
                    typeof(TestComplexConfig), Encoding.UTF8.GetBytes(json));
                TestComplexConfig row = helper.GetConfig<TestComplexConfig>(table, 1);

                Assert.AreEqual(TestComplexConfigStateEnum.Run, row.State);
                CollectionAssert.AreEqual(new[] { 1, 2, 3 }, row.Levels);
                CollectionAssert.AreEqual(new[] { "alpha", "beta|gamma" }, row.Tags);
                Assert.AreEqual("Line1\nLine2", row.Description);
            }
            finally
            {
                ConfigSchemaRegistry.Unregister(typeof(TestComplexConfig));
                Object.DestroyImmediate(owner);
            }
        }

        /// <summary>验证复杂字段可由 URFC v2 Codec 完整回读。</summary>
        [Test]
        public void ComplexFieldsRoundTripThroughBinaryHelper()
        {
            ConfigTableSchema schema = CreateComplexSchema();
            BinaryConfigCodecRegistry.Register(
                new TestComplexConfigCodec(schema.TableId, schema.SchemaHash));
            byte[] bytes = ConfigBinaryExporter.BuildV2(schema);
            GameObject owner = new GameObject("Complex BinaryConfigHelper Tests");
            try
            {
                BinaryConfigHelper helper = owner.AddComponent<BinaryConfigHelper>();
                object table = helper.ParseConfig(typeof(TestComplexConfig), bytes);
                TestComplexConfig row = helper.GetConfig<TestComplexConfig>(table, 1);

                Assert.AreEqual(TestComplexConfigStateEnum.Run, row.State);
                CollectionAssert.AreEqual(new[] { 1, 2, 3 }, row.Levels);
                CollectionAssert.AreEqual(new[] { "alpha", "beta|gamma" }, row.Tags);
                Assert.AreEqual("Line1\nLine2", row.Description);
            }
            finally
            {
                BinaryConfigCodecRegistry.Unregister(typeof(TestComplexConfig));
                Object.DestroyImmediate(owner);
            }
        }

        /// <summary>验证自定义字段 Codec 可完成 CSV、JSON、URFC 和生成代码闭环。</summary>
        [Test]
        public void CustomFieldCodecRoundTripsThroughJsonAndBinary()
        {
            ConfigFieldCodecRegistry.Register(new TestCustomValueCodec());
            const string csv =
                "Id,Point\nint,point2\n编号,坐标\n1,10:20\n2,-3:7";
            ConfigTableSchema schema = ConfigSchemaParser.ParseConfig(
                CsvDocumentReader.Parse("TestCustom.csv", csv),
                "UnityRFramework.Editor.Tests");
            BinaryConfigCodecRegistry.Register(
                new TestCustomConfigCodec(schema.TableId, schema.SchemaHash));
            ConfigSchemaRegistry.Register(
                typeof(TestCustomConfig), schema.TableId, schema.SchemaHash);
            GameObject owner = new GameObject("Custom Config Codec Tests");
            try
            {
                Assert.AreEqual(ConfigFieldKind.Custom, schema.Fields[1].Kind);
                string generated = ConfigCodeGenerator.Generate(schema);
                StringAssert.Contains(
                    ".ReadBinary<UnityRFramework.Editor.Tests.TestCustomValue>(",
                    generated);
                StringAssert.Contains("\"point2\", 1u, reader)", generated);

                JsonConfigHelper jsonHelper = owner.AddComponent<JsonConfigHelper>();
                string json = ConfigJsonExporter.Build(schema);
                object jsonTable = jsonHelper.ParseConfig(
                    typeof(TestCustomConfig), Encoding.UTF8.GetBytes(json));
                Assert.AreEqual(
                    new TestCustomValue { X = 10, Y = 20 },
                    jsonHelper.GetConfig<TestCustomConfig>(jsonTable, 1).Point);

                BinaryConfigHelper binaryHelper = owner.AddComponent<BinaryConfigHelper>();
                object binaryTable = binaryHelper.ParseConfig(
                    typeof(TestCustomConfig), ConfigBinaryExporter.BuildV2(schema));
                Assert.AreEqual(
                    new TestCustomValue { X = -3, Y = 7 },
                    binaryHelper.GetConfig<TestCustomConfig>(binaryTable, 2).Point);
            }
            finally
            {
                BinaryConfigCodecRegistry.Unregister(typeof(TestCustomConfig));
                ConfigSchemaRegistry.Unregister(typeof(TestCustomConfig));
                ConfigFieldCodecRegistry.Unregister("point2");
                Object.DestroyImmediate(owner);
            }
        }

        /// <summary>验证项目可替换并恢复 ConfigPipeline 代码生成策略。</summary>
        [Test]
        public void CodeGeneratorStrategyCanBeReplacedAndReset()
        {
            try
            {
                ConfigCodeGeneratorRegistry.Set(new TestCodeGenerator());
                Assert.AreEqual(
                    "// custom:TestConfigRow",
                    ConfigCodeGeneratorRegistry.Current.Generate(CreateSchema()));
            }
            finally
            {
                ConfigCodeGeneratorRegistry.Reset();
            }

            StringAssert.Contains(
                "public sealed class TestConfigRow",
                ConfigCodeGeneratorRegistry.Current.Generate(CreateSchema()));
        }

        /// <summary>验证集合中的非法转义会在导出前被拒绝。</summary>
        [Test]
        public void CollectionRejectsUnsupportedEscapeSequence()
        {
            const string csv =
                "Id,Values\nint,List<string>\n编号,值\n1,bad\\qvalue";
            CsvDocument document = CsvDocumentReader.Parse("InvalidCollection.csv", csv);

            Assert.Throws<RFrameworkException>(() =>
                ConfigSchemaParser.ParseConfig(document, "Game.Config"));
        }

        /// <summary>验证 JSON Helper 保留 decimal 字段。</summary>
        [Test]
        public void JsonHelperPreservesDecimalValue()
        {
            TestJsonPrimitiveConfig row = LoadExtendedPrimitiveRow();
            Assert.AreEqual(12.5m, row.DecimalValue);
        }

        /// <summary>验证 JSON Helper 保留 char 字段。</summary>
        [Test]
        public void JsonHelperPreservesCharValue()
        {
            TestJsonPrimitiveConfig row = LoadExtendedPrimitiveRow();
            Assert.AreEqual('Z', row.CharValue);
        }

        /// <summary>验证 JSON Helper 保留 ulong 字段。</summary>
        [Test]
        public void JsonHelperPreservesUInt64Value()
        {
            TestJsonPrimitiveConfig row = LoadExtendedPrimitiveRow();
            Assert.AreEqual(ulong.MaxValue, row.ULongValue);
        }

        private static TestJsonPrimitiveConfig LoadExtendedPrimitiveRow()
        {
            const string csv =
                "Id,DecimalValue,CharValue,ULongValue\n"
                + "int,decimal,char,ulong\n"
                + "编号,定点数,字符,大整数\n"
                + "1,12.5,Z,18446744073709551615";
            ConfigTableSchema schema = ConfigSchemaParser.ParseConfig(
                CsvDocumentReader.Parse("TestJsonPrimitive.csv", csv),
                "UnityRFramework.Editor.Tests");
            ConfigSchemaRegistry.Register(
                typeof(TestJsonPrimitiveConfig), schema.TableId, schema.SchemaHash);
            Utility.Json.SetJsonHelper(new DefaultJsonHelper());
            string json = ConfigJsonExporter.Build(schema);
            GameObject owner = new GameObject("Extended Primitive JSON Tests");
            try
            {
                JsonConfigHelper helper = owner.AddComponent<JsonConfigHelper>();
                object table = helper.ParseConfig(
                    typeof(TestJsonPrimitiveConfig), Encoding.UTF8.GetBytes(json));
                return helper.GetConfig<TestJsonPrimitiveConfig>(table, 1);
            }
            finally
            {
                ConfigSchemaRegistry.Unregister(typeof(TestJsonPrimitiveConfig));
                Object.DestroyImmediate(owner);
            }
        }

        /// <summary>验证生成代码使用静态 Codec，不包含运行时字段反射。</summary>
        [Test]
        public void CodeGeneratorProducesStaticCodecWithoutReflection()
        {
            string code = ConfigCodeGenerator.Generate(CreateSchema());

            StringAssert.Contains("IBinaryConfigCodec", code);
            StringAssert.Contains("[ConfigTable(\"TestConfigRow\")]", code);
            StringAssert.Contains("BinaryConfigCodecRegistry.Register", code);
            StringAssert.Contains("ConfigSchemaRegistry.Register", code);
            StringAssert.DoesNotContain("Activator.CreateInstance", code);
            StringAssert.DoesNotContain("FieldInfo", code);
            string[] lines = code.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                Assert.LessOrEqual(lines[i].Length, 120, $"Generated line {i + 1} is too long.");
            }
        }

        /// <summary>验证生成命名空间留空时输出全局命名空间脚本。</summary>
        [Test]
        public void CodeGeneratorOmitsNamespaceWhenOptionIsBlank()
        {
            const string csv = "Id,Name\nint,string\n编号,名称\n1,Sword";
            ConfigTableSchema schema = ConfigSchemaParser.ParseConfig(
                CsvDocumentReader.Parse("Item.csv", csv), string.Empty);

            string code = ConfigCodeGenerator.Generate(schema);

            Assert.IsEmpty(schema.Namespace);
            StringAssert.DoesNotContain("namespace ", code);
            StringAssert.Contains("public sealed class ItemConfig", code);
        }

        private static ConfigTableSchema CreateSchema()
        {
            ConfigFieldSchema[] fields =
            {
                new ConfigFieldSchema
                {
                    Name = "Id", TypeKeyword = "int", CSharpTypeName = "int",
                    Kind = ConfigFieldKind.Int32
                },
                new ConfigFieldSchema
                {
                    Name = "Name", TypeKeyword = "string", CSharpTypeName = "string",
                    Kind = ConfigFieldKind.String
                },
                new ConfigFieldSchema
                {
                    Name = "Price", TypeKeyword = "float", CSharpTypeName = "float",
                    Kind = ConfigFieldKind.Single
                }
            };
            const string fullTypeName = "UnityRFramework.Editor.Tests.TestConfigRow";
            string identity = fullTypeName + "|Id:int;Name:string;Price:float";
            return new ConfigTableSchema
            {
                SourcePath = "memory.csv",
                TableName = "TestConfigRow",
                Namespace = "UnityRFramework.Editor.Tests",
                RowTypeName = nameof(TestConfigRow),
                Fields = fields,
                Rows = new[]
                {
                    new CsvRow(4, new[] { "1", "Sword", "12.5" }),
                    new CsvRow(5, new[] { "2", "Shield", "20" })
                },
                TableId = BinaryFormatUtility.ComputeFnv1A32(fullTypeName),
                SchemaHash = BinaryFormatUtility.ComputeFnv1A64(identity)
            };
        }

        private static ConfigTableSchema CreatePartitionSchema(
            string segmentName, CsvRow row)
        {
            ConfigTableSchema schema = CreateSchema();
            schema.SegmentName = segmentName;
            schema.Rows = new[] { row };
            return schema;
        }

        private static ConfigTableSchema CreateLegacySchema()
        {
            ConfigFieldSchema[] fields =
            {
                new ConfigFieldSchema
                {
                    Name = "Id", TypeKeyword = "int", CSharpTypeName = "int",
                    Kind = ConfigFieldKind.Int32
                },
                new ConfigFieldSchema
                {
                    Name = "Name", TypeKeyword = "string", CSharpTypeName = "string",
                    Kind = ConfigFieldKind.String
                }
            };
            const string fullTypeName = "UnityRFramework.Editor.Tests.TestConfigRow";
            string identity = fullTypeName + "|Id:int;Name:string";
            return new ConfigTableSchema
            {
                SourcePath = "legacy-memory.csv",
                TableName = "TestConfigRow",
                Namespace = "UnityRFramework.Editor.Tests",
                RowTypeName = nameof(TestConfigRow),
                Fields = fields,
                Rows = new[]
                {
                    new CsvRow(4, new[] { "1", "Sword" }),
                    new CsvRow(5, new[] { "2", "Shield" })
                },
                TableId = BinaryFormatUtility.ComputeFnv1A32(fullTypeName),
                SchemaHash = BinaryFormatUtility.ComputeFnv1A64(identity)
            };
        }

        private static ConfigTableSchema CreateLegacyJsonSchema()
        {
            ConfigFieldSchema[] fields =
            {
                new ConfigFieldSchema
                {
                    Name = "Id", TypeKeyword = "int", CSharpTypeName = "int",
                    Kind = ConfigFieldKind.Int32
                },
                new ConfigFieldSchema
                {
                    Name = "LegacyName", TypeKeyword = "string", CSharpTypeName = "string",
                    Kind = ConfigFieldKind.String
                }
            };
            const string fullTypeName = "UnityRFramework.Editor.Tests.TestConfigRow";
            string identity = fullTypeName + "|Id:int;LegacyName:string";
            return new ConfigTableSchema
            {
                SourcePath = "legacy-json-memory.csv",
                TableName = "TestConfigRow",
                Namespace = "UnityRFramework.Editor.Tests",
                RowTypeName = nameof(TestConfigRow),
                Fields = fields,
                Rows = new[]
                {
                    new CsvRow(4, new[] { "1", "Sword" })
                },
                TableId = BinaryFormatUtility.ComputeFnv1A32(fullTypeName),
                SchemaHash = BinaryFormatUtility.ComputeFnv1A64(identity)
            };
        }

        private static ConfigTableSchema CreateComplexSchema()
        {
            const string csv =
                "Id,State,Levels,Tags,Description\n"
                + "int,enum<Idle=0|Run=2>,int[],List<string>,string\n"
                + "编号,状态,等级,标签,描述\n"
                + "1,Run,1|2|3,alpha|beta\\|gamma,Line1\\nLine2";
            return ConfigSchemaParser.ParseConfig(
                CsvDocumentReader.Parse("TestComplex.csv", csv),
                "UnityRFramework.Editor.Tests");
        }

        private sealed class TestCodeGenerator : IConfigCodeGenerator
        {
            /// <inheritdoc/>
            public string Generate(ConfigTableSchema schema)
            {
                return "// custom:" + schema.TableName;
            }
        }

    }
}
