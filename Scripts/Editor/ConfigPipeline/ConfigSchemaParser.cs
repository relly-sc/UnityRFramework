using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using RFramework;
using UnityRFramework.Runtime;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 将 CSV 转换为经过校验的 Config 表定义。
    /// </summary>
    public static class ConfigSchemaParser
    {
        private const int MaxIdentifierLength = 64;
        private const int MaxNamespaceLength = 100;

        private static readonly Dictionary<string, (ConfigFieldKind Kind, string CSharpType)> Types =
            new Dictionary<string, (ConfigFieldKind, string)>(StringComparer.OrdinalIgnoreCase)
            {
                { "bool", (ConfigFieldKind.Boolean, "bool") },
                { "boolean", (ConfigFieldKind.Boolean, "bool") },
                { "byte", (ConfigFieldKind.Byte, "byte") },
                { "sbyte", (ConfigFieldKind.SByte, "sbyte") },
                { "short", (ConfigFieldKind.Int16, "short") },
                { "int16", (ConfigFieldKind.Int16, "short") },
                { "ushort", (ConfigFieldKind.UInt16, "ushort") },
                { "uint16", (ConfigFieldKind.UInt16, "ushort") },
                { "int", (ConfigFieldKind.Int32, "int") },
                { "int32", (ConfigFieldKind.Int32, "int") },
                { "uint", (ConfigFieldKind.UInt32, "uint") },
                { "uint32", (ConfigFieldKind.UInt32, "uint") },
                { "long", (ConfigFieldKind.Int64, "long") },
                { "int64", (ConfigFieldKind.Int64, "long") },
                { "ulong", (ConfigFieldKind.UInt64, "ulong") },
                { "uint64", (ConfigFieldKind.UInt64, "ulong") },
                { "float", (ConfigFieldKind.Single, "float") },
                { "single", (ConfigFieldKind.Single, "float") },
                { "double", (ConfigFieldKind.Double, "double") },
                { "decimal", (ConfigFieldKind.Decimal, "decimal") },
                { "char", (ConfigFieldKind.Char, "char") },
                { "string", (ConfigFieldKind.String, "string") }
            };

        private static readonly HashSet<string> CSharpKeywords = new HashSet<string>
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char",
            "checked", "class", "const", "continue", "decimal", "default", "delegate", "do",
            "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
            "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int",
            "interface", "internal", "is", "lock", "long", "namespace", "new", "null",
            "object", "operator", "out", "override", "params", "private", "protected", "public",
            "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc",
            "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof",
            "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void",
            "volatile", "while"
        };

        /// <summary>
        /// 解析并校验一张 Config CSV。
        /// </summary>
        /// <param name="document">CSV 文档。</param>
        /// <param name="namespaceName">生成代码使用的命名空间。</param>
        /// <returns>经过校验的配置表定义。</returns>
        public static ConfigTableSchema ParseConfig(CsvDocument document, string namespaceName)
        {
            if (document == null || document.Rows.Count < 3)
            {
                throw new RFrameworkException(
                    "Config CSV requires field, type and comment header rows.");
            }

            ValidateNamespace(namespaceName);
            CsvRow nameRow = document.Rows[0];
            CsvRow typeRow = document.Rows[1];
            CsvRow commentRow = document.Rows[2];
            int fieldCount = nameRow.Values.Count;
            if (fieldCount == 0 || typeRow.Values.Count != fieldCount)
            {
                throw Error(document.SourcePath, typeRow.LineNumber,
                    "Field and type rows must contain the same non-zero number of columns.");
            }

            List<ConfigFieldSchema> fields = new List<ConfigFieldSchema>(fieldCount);
            HashSet<string> fieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < fieldCount; i++)
            {
                string fieldName = nameRow.Values[i].Trim();
                ValidateIdentifier(fieldName, "field", document.SourcePath, nameRow.LineNumber);
                if (!fieldNames.Add(fieldName))
                {
                    throw Error(document.SourcePath, nameRow.LineNumber,
                        $"Duplicate field '{fieldName}'.");
                }

                string typeKeyword = typeRow.Values[i].Trim();
                if (!Types.TryGetValue(
                    typeKeyword,
                    out (ConfigFieldKind Kind, string CSharpType) type))
                {
                    throw Error(document.SourcePath, typeRow.LineNumber,
                        $"Unsupported field type '{typeKeyword}' for '{fieldName}'.");
                }

                fields.Add(new ConfigFieldSchema
                {
                    Name = fieldName,
                    TypeKeyword = typeKeyword.ToLowerInvariant(),
                    CSharpTypeName = type.CSharpType,
                    Kind = type.Kind,
                    Comment = i < commentRow.Values.Count ? commentRow.Values[i].Trim() : string.Empty
                });
            }

            ConfigFieldSchema idField = fields.FirstOrDefault(
                field => string.Equals(field.Name, "Id", StringComparison.OrdinalIgnoreCase));
            if (idField == null || idField.Kind != ConfigFieldKind.Int32)
            {
                throw Error(document.SourcePath, nameRow.LineNumber,
                    "Config schema must contain exactly one Int32 Id field.");
            }

            List<CsvRow> rows = new List<CsvRow>();
            HashSet<int> ids = new HashSet<int>();
            for (int rowIndex = 3; rowIndex < document.Rows.Count; rowIndex++)
            {
                CsvRow row = document.Rows[rowIndex];
                if (IsBlank(row))
                {
                    continue;
                }

                if (row.Values.Count != fieldCount)
                {
                    throw Error(document.SourcePath, row.LineNumber,
                        $"Expected {fieldCount} columns, found {row.Values.Count}.");
                }

                for (int fieldIndex = 0; fieldIndex < fields.Count; fieldIndex++)
                {
                    ConfigValueParser.Validate(
                        fields[fieldIndex], row.Values[fieldIndex], document.SourcePath, row.LineNumber);
                }

                int idIndex = fields.IndexOf(idField);
                int id = int.Parse(row.Values[idIndex].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture);
                if (!ids.Add(id))
                {
                    throw Error(document.SourcePath, row.LineNumber, $"Duplicate Id '{id}'.");
                }

                rows.Add(row);
            }

            string tableName = Path.GetFileNameWithoutExtension(document.SourcePath);
            ValidateIdentifier(tableName, "table", document.SourcePath, 1);
            string rowTypeName = tableName.EndsWith("Config", StringComparison.Ordinal)
                ? tableName
                : tableName + "Config";
            string fullTypeName = string.IsNullOrEmpty(namespaceName)
                ? rowTypeName
                : namespaceName + "." + rowTypeName;
            string schemaIdentity = BuildSchemaIdentity(fullTypeName, fields);

            return new ConfigTableSchema
            {
                SourcePath = document.SourcePath,
                TableName = tableName,
                Namespace = namespaceName,
                RowTypeName = rowTypeName,
                Fields = fields,
                Rows = rows,
                TableId = BinaryFormatUtility.ComputeFnv1A32(fullTypeName),
                SchemaHash = BinaryFormatUtility.ComputeFnv1A64(schemaIdentity)
            };
        }

        private static string BuildSchemaIdentity(
            string fullTypeName, IReadOnlyList<ConfigFieldSchema> fields)
        {
            return fullTypeName + "|" + string.Join(";", fields.Select(
                field => field.Name + ":" + field.CSharpTypeName));
        }

        private static void ValidateNamespace(string namespaceName)
        {
            if (string.IsNullOrWhiteSpace(namespaceName))
            {
                return;
            }

            if (namespaceName.Length > MaxNamespaceLength)
            {
                throw new RFrameworkException(
                    $"Generated namespace exceeds {MaxNamespaceLength} characters: '{namespaceName}'.");
            }

            string[] parts = namespaceName.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                ValidateIdentifier(parts[i], "namespace", namespaceName, 1);
            }
        }

        private static void ValidateIdentifier(string value, string kind, string path, int line)
        {
            if (value != null && value.Length > MaxIdentifierLength)
            {
                throw Error(path, line,
                    $"C# {kind} identifier exceeds {MaxIdentifierLength} characters: '{value}'.");
            }

            if (string.IsNullOrEmpty(value)
                || !(value[0] == '_' || value[0] >= 'A' && value[0] <= 'Z'
                    || value[0] >= 'a' && value[0] <= 'z'))
            {
                throw Error(path, line, $"Invalid C# {kind} identifier '{value}'.");
            }

            for (int i = 1; i < value.Length; i++)
            {
                char ch = value[i];
                if (!(ch == '_' || ch >= 'A' && ch <= 'Z' || ch >= 'a' && ch <= 'z'
                    || ch >= '0' && ch <= '9'))
                {
                    throw Error(path, line, $"Invalid C# {kind} identifier '{value}'.");
                }
            }

            if (CSharpKeywords.Contains(value))
            {
                throw Error(path, line, $"C# keyword '{value}' can not be used as a {kind} name.");
            }
        }

        private static bool IsBlank(CsvRow row)
        {
            return row.Values.All(string.IsNullOrWhiteSpace);
        }

        private static RFrameworkException Error(string path, int line, string message)
        {
            return new RFrameworkException($"Data schema '{path}', line {line}: {message}");
        }
    }
}
