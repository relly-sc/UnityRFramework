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
            string json = ConfigJsonExporter.Build(CreateSchema());
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

        /// <summary>验证生成代码使用静态 Codec，不包含运行时字段反射。</summary>
        [Test]
        public void CodeGeneratorProducesStaticCodecWithoutReflection()
        {
            string code = ConfigCodeGenerator.Generate(CreateSchema());

            StringAssert.Contains("IBinaryConfigCodec", code);
            StringAssert.Contains("BinaryConfigCodecRegistry.Register", code);
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

    }
}
