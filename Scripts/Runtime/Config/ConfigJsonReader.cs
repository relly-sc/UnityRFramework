using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using RFramework;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 无第三方依赖地读取 ConfigPipeline 生成的受限 JSON 结构。
    /// </summary>
    internal static class ConfigJsonReader
    {
        private const int MaxDepth = 64;
        private const int MaxCollectionCount = 1000000;

        /// <summary>
        /// 读取目标配置行类型对应的 JSON 表。
        /// </summary>
        /// <param name="rowType">配置行类型。</param>
        /// <param name="json">JSON 文本。</param>
        /// <returns>强类型配置行集合。</returns>
        public static IReadOnlyList<object> ParseRows(Type rowType, string json)
        {
            return ParseRows(rowType, json, true, false);
        }

        /// <summary>
        /// 读取带 Schema 元数据的多表 JSON，并按配置行类型合并分片行。
        /// </summary>
        public static IReadOnlyDictionary<Type, IReadOnlyList<object>> ParseBundleRows(string json)
        {
            return ParseBundleRows(json, 0);
        }

        private static IReadOnlyDictionary<Type, IReadOnlyList<object>> ParseBundleRows(
            string json, int migrationDepth)
        {
            if (migrationDepth > 32)
            {
                throw new RFrameworkException("Config JSON bundle migration exceeded 32 steps.");
            }

            object root = new Parser(json).Parse();
            if (!(root is Dictionary<string, object> rootObject)
                || !rootObject.TryGetValue("Tables", out object tablesValue)
                || !(tablesValue is Dictionary<string, object> tables)
                || tables.Count == 0)
            {
                throw new RFrameworkException(
                    "Config JSON bundle must contain a non-empty 'Tables' object.");
            }

            Dictionary<Type, List<object>> grouped = new Dictionary<Type, List<object>>();
            foreach (KeyValuePair<string, object> pair in tables)
            {
                if (!(pair.Value is Dictionary<string, object> envelope)
                    || !envelope.TryGetValue("TableId", out object tableIdValue)
                    || !envelope.TryGetValue("SchemaHash", out object schemaHashValue)
                    || !envelope.TryGetValue("Rows", out object rowsValue))
                {
                    throw new RFrameworkException(
                        $"Config JSON bundle table '{pair.Key}' has invalid metadata.");
                }

                uint tableId = ParseHexUInt32(tableIdValue, "TableId");
                ulong schemaHash = ParseHexUInt64(schemaHashValue, "SchemaHash");
                if (!ConfigSchemaRegistry.TryGet(tableId, out ConfigSchemaInfo schema))
                {
                    throw new RFrameworkException(
                        $"No Config Schema is registered for TableId '{tableId:X8}'.");
                }

                if (schemaHash != schema.SchemaHash)
                {
                    if (!JsonConfigMigrationRegistry.TryGet(
                        schema.RowType, schemaHash, out IJsonConfigMigration migration))
                    {
                        throw new RFrameworkException(
                            $"Config JSON bundle schema mismatch for '{pair.Key}'. "
                            + $"File '{schemaHash:X16}', current '{schema.SchemaHash:X16}'.");
                    }

                    if (migration.TargetSchemaHash != schema.SchemaHash)
                    {
                        throw new RFrameworkException(
                            $"Config JSON migration target mismatch for '{pair.Key}'.");
                    }

                    string migrated = migration.Migrate(json);
                    if (string.IsNullOrWhiteSpace(migrated)
                        || string.Equals(migrated, json, StringComparison.Ordinal))
                    {
                        throw new RFrameworkException(
                            $"Config JSON migration for '{pair.Key}' made no progress.");
                    }

                    return ParseBundleRows(migrated, migrationDepth + 1);
                }

                if (!grouped.TryGetValue(schema.RowType, out List<object> rows))
                {
                    rows = new List<object>();
                    grouped.Add(schema.RowType, rows);
                }

                rows.AddRange(ParseRowArray(schema.RowType, rowsValue));
            }

            Dictionary<Type, IReadOnlyList<object>> result =
                new Dictionary<Type, IReadOnlyList<object>>(grouped.Count);
            foreach (KeyValuePair<Type, List<object>> pair in grouped)
            {
                result.Add(pair.Key, pair.Value);
            }

            return result;
        }

        private static IReadOnlyList<object> ParseRows(
            Type rowType, string json, bool allowMigration, bool requireSchemaMetadata)
        {
            object root = new Parser(json).Parse();
            if (root is Dictionary<string, object> rootObject)
            {
                if (rootObject.TryGetValue("Tables", out object tablesValue))
                {
                    root = SelectTable(rowType, tablesValue);
                }
                else if (!rootObject.TryGetValue("Items", out root))
                {
                    throw new RFrameworkException(
                        "Config JSON object has neither a 'Tables' nor legacy 'Items' field.");
                }
            }

            if (root is Dictionary<string, object> tableEnvelope)
            {
                return ParseVersionedTable(
                    rowType, json, tableEnvelope, allowMigration);
            }

            if (requireSchemaMetadata)
            {
                throw new RFrameworkException(
                    "Migrated Config JSON must contain TableId, SchemaHash and Rows metadata.");
            }

            return ParseRowArray(rowType, root);
        }

        private static IReadOnlyList<object> ParseVersionedTable(
            Type rowType,
            string sourceJson,
            IReadOnlyDictionary<string, object> envelope,
            bool allowMigration)
        {
            if (!envelope.TryGetValue("TableId", out object tableIdValue)
                || !envelope.TryGetValue("SchemaHash", out object schemaHashValue)
                || !envelope.TryGetValue("Rows", out object rowsValue))
            {
                throw new RFrameworkException(
                    "Config JSON table metadata must contain TableId, SchemaHash and Rows.");
            }

            uint sourceTableId = ParseHexUInt32(tableIdValue, "TableId");
            ulong sourceSchemaHash = ParseHexUInt64(schemaHashValue, "SchemaHash");
            if (!ConfigSchemaRegistry.TryGet(rowType, out ConfigSchemaInfo currentSchema))
            {
                throw new RFrameworkException(
                    $"No current Config Schema is registered for '{rowType.FullName}'.");
            }

            if (sourceTableId != currentSchema.TableId)
            {
                throw new RFrameworkException(
                    $"Config JSON table id mismatch for '{rowType.Name}'. "
                    + $"File '{sourceTableId:X8}', current '{currentSchema.TableId:X8}'.");
            }

            if (sourceSchemaHash == currentSchema.SchemaHash)
            {
                return ParseRowArray(rowType, rowsValue);
            }

            if (!allowMigration || !JsonConfigMigrationRegistry.TryGet(
                rowType, sourceSchemaHash, out IJsonConfigMigration migration))
            {
                throw new RFrameworkException(
                    $"Config JSON schema mismatch for '{rowType.Name}'. "
                    + $"File '{sourceSchemaHash:X16}', current "
                    + $"'{currentSchema.SchemaHash:X16}', and no migration is registered.");
            }

            if (migration.TargetSchemaHash != currentSchema.SchemaHash)
            {
                throw new RFrameworkException(
                    $"Config JSON migration target mismatch for '{rowType.Name}'. "
                    + $"Migration '{migration.TargetSchemaHash:X16}', current "
                    + $"'{currentSchema.SchemaHash:X16}'.");
            }

            string migratedJson = migration.Migrate(sourceJson);
            if (string.IsNullOrWhiteSpace(migratedJson))
            {
                throw new RFrameworkException(
                    $"Config JSON migration for '{rowType.Name}' returned empty content.");
            }

            return ParseRows(rowType, migratedJson, false, true);
        }

        private static IReadOnlyList<object> ParseRowArray(Type rowType, object root)
        {
            if (!(root is List<object> items))
            {
                throw new RFrameworkException(
                    "Config JSON table must be an array of row objects.");
            }

            if (items.Count > MaxCollectionCount)
            {
                throw new RFrameworkException(
                    $"Config JSON contains too many rows. Maximum is {MaxCollectionCount}.");
            }

            List<object> rows = new List<object>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                if (!(items[i] is Dictionary<string, object> values))
                {
                    throw new RFrameworkException($"Config JSON row {i + 1} must be an object.");
                }

                rows.Add(CreateRow(rowType, values, i + 1));
            }

            return rows;
        }

        private static uint ParseHexUInt32(object value, string fieldName)
        {
            ulong parsed = ParseHexUInt64(value, fieldName);
            if (parsed > uint.MaxValue)
            {
                throw new RFrameworkException(
                    $"Config JSON metadata '{fieldName}' exceeds UInt32 range.");
            }

            return (uint)parsed;
        }

        private static ulong ParseHexUInt64(object value, string fieldName)
        {
            if (!(value is string text)
                || !ulong.TryParse(
                    text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture,
                    out ulong parsed)
                || parsed == 0)
            {
                throw new RFrameworkException(
                    $"Config JSON metadata '{fieldName}' must be a non-zero hexadecimal string.");
            }

            return parsed;
        }

        private static object SelectTable(Type rowType, object tablesValue)
        {
            if (!(tablesValue is Dictionary<string, object> tables))
            {
                throw new RFrameworkException("Config JSON 'Tables' field must be an object.");
            }

            if (tables.Count == 0)
            {
                throw new RFrameworkException("Config JSON 'Tables' field is empty.");
            }

            ConfigTableAttribute attribute = rowType.GetCustomAttribute<ConfigTableAttribute>();
            string tableName = attribute?.TableName;
            if (string.IsNullOrWhiteSpace(tableName))
            {
                tableName = rowType.Name.EndsWith("Config", StringComparison.Ordinal)
                    ? rowType.Name.Substring(0, rowType.Name.Length - "Config".Length)
                    : rowType.Name;
            }

            if (tables.TryGetValue(tableName, out object selected))
            {
                return selected;
            }

            if (tables.Count == 1)
            {
                foreach (KeyValuePair<string, object> pair in tables)
                {
                    return pair.Value;
                }
            }

            throw new RFrameworkException(
                $"Config JSON contains multiple tables but has no table '{tableName}'.");
        }

        private static object CreateRow(
            Type rowType, IReadOnlyDictionary<string, object> values, int rowNumber)
        {
            object row;
            try
            {
                row = Activator.CreateInstance(rowType);
            }
            catch (Exception ex)
            {
                throw new RFrameworkException(
                    $"Config row type '{rowType.Name}' could not be created.", ex);
            }

            FieldInfo[] fields = rowType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (!values.TryGetValue(field.Name, out object value))
                {
                    continue;
                }

                try
                {
                    field.SetValue(row, ConvertValue(value, field.FieldType, field.Name));
                }
                catch (Exception ex)
                {
                    if (ex is RFrameworkException)
                    {
                        throw;
                    }

                    throw new RFrameworkException(
                        $"Config JSON row {rowNumber}, field '{field.Name}' could not be converted.", ex);
                }
            }

            return row;
        }

        private static object ConvertValue(object value, Type targetType, string fieldName)
        {
            if (value == null)
            {
                if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null)
                {
                    return null;
                }

                throw new RFrameworkException(
                    $"Config JSON field '{fieldName}' can not assign null to '{targetType.Name}'.");
            }

            Type nullableType = Nullable.GetUnderlyingType(targetType);
            if (nullableType != null)
            {
                return ConvertValue(value, nullableType, fieldName);
            }

            if (ConfigFieldCodecRegistry.TryGet(targetType, out IConfigFieldCodec customCodec))
            {
                string customText = RequireString(value, fieldName);
                object customValue = customCodec.ParseJson(customText);
                ValidateCustomValue(customCodec, customValue, fieldName);
                return customValue;
            }

            if (targetType == typeof(string))
            {
                return RequireString(value, fieldName);
            }

            if (targetType == typeof(char))
            {
                string text = RequireString(value, fieldName);
                if (text.Length != 1)
                {
                    throw new RFrameworkException(
                        $"Config JSON field '{fieldName}' must contain exactly one character.");
                }

                return text[0];
            }

            if (targetType == typeof(bool))
            {
                if (value is bool boolean)
                {
                    return boolean;
                }

                throw new RFrameworkException($"Config JSON field '{fieldName}' must be a boolean.");
            }

            if (targetType.IsEnum)
            {
                string enumValue = GetScalarText(value, fieldName);
                if (long.TryParse(enumValue, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out long numericValue))
                {
                    return Enum.ToObject(targetType, numericValue);
                }

                return Enum.Parse(targetType, enumValue, false);
            }

            if (targetType.IsArray)
            {
                Type elementType = targetType.GetElementType();
                List<object> source = RequireArray(value, fieldName);
                Array result = Array.CreateInstance(elementType, source.Count);
                for (int i = 0; i < source.Count; i++)
                {
                    result.SetValue(ConvertValue(source[i], elementType, fieldName), i);
                }

                return result;
            }

            if (targetType.IsGenericType
                && targetType.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type elementType = targetType.GetGenericArguments()[0];
                List<object> source = RequireArray(value, fieldName);
                IList result = (IList)Activator.CreateInstance(targetType);
                for (int i = 0; i < source.Count; i++)
                {
                    result.Add(ConvertValue(source[i], elementType, fieldName));
                }

                return result;
            }

            string scalar = GetScalarText(value, fieldName);
            try
            {
                if (targetType == typeof(byte)) return byte.Parse(scalar, CultureInfo.InvariantCulture);
                if (targetType == typeof(sbyte)) return sbyte.Parse(scalar, CultureInfo.InvariantCulture);
                if (targetType == typeof(short)) return short.Parse(scalar, CultureInfo.InvariantCulture);
                if (targetType == typeof(ushort)) return ushort.Parse(scalar, CultureInfo.InvariantCulture);
                if (targetType == typeof(int)) return int.Parse(scalar, CultureInfo.InvariantCulture);
                if (targetType == typeof(uint)) return uint.Parse(scalar, CultureInfo.InvariantCulture);
                if (targetType == typeof(long)) return long.Parse(scalar, CultureInfo.InvariantCulture);
                if (targetType == typeof(ulong)) return ulong.Parse(scalar, CultureInfo.InvariantCulture);
                if (targetType == typeof(float))
                    return float.Parse(scalar, NumberStyles.Float, CultureInfo.InvariantCulture);
                if (targetType == typeof(double))
                    return double.Parse(scalar, NumberStyles.Float, CultureInfo.InvariantCulture);
                if (targetType == typeof(decimal))
                    return decimal.Parse(scalar, NumberStyles.Float, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                throw new RFrameworkException(
                    $"Config JSON field '{fieldName}' has invalid value '{scalar}'.", ex);
            }

            throw new RFrameworkException(
                $"Config JSON field '{fieldName}' uses unsupported type '{targetType.FullName}'.");
        }

        private static void ValidateCustomValue(
            IConfigFieldCodec codec, object value, string fieldName)
        {
            if (value == null)
            {
                if (!codec.ValueType.IsValueType
                    || Nullable.GetUnderlyingType(codec.ValueType) != null)
                {
                    return;
                }

                throw new RFrameworkException(
                    $"Config JSON field '{fieldName}' codec '{codec.TypeKeyword}' returned null "
                    + $"for value type '{codec.ValueType.FullName}'.");
            }

            if (!codec.ValueType.IsInstanceOfType(value))
            {
                throw new RFrameworkException(
                    $"Config JSON field '{fieldName}' codec '{codec.TypeKeyword}' returned "
                    + $"'{value.GetType().FullName}', expected '{codec.ValueType.FullName}'.");
            }
        }

        private static string RequireString(object value, string fieldName)
        {
            if (value is string text)
            {
                return text;
            }

            throw new RFrameworkException($"Config JSON field '{fieldName}' must be a string.");
        }

        private static List<object> RequireArray(object value, string fieldName)
        {
            if (!(value is List<object> items))
            {
                throw new RFrameworkException($"Config JSON field '{fieldName}' must be an array.");
            }

            if (items.Count > MaxCollectionCount)
            {
                throw new RFrameworkException(
                    $"Config JSON field '{fieldName}' exceeds {MaxCollectionCount} elements.");
            }

            return items;
        }

        private static string GetScalarText(object value, string fieldName)
        {
            if (value is JsonNumber number)
            {
                return number.Value;
            }

            if (value is string text)
            {
                return text;
            }

            throw new RFrameworkException(
                $"Config JSON field '{fieldName}' must be a number or string.");
        }

        private sealed class JsonNumber
        {
            public JsonNumber(string value)
            {
                Value = value;
            }

            public string Value { get; }
        }

        private sealed class Parser
        {
            private readonly string text;
            private int index;

            public Parser(string text)
            {
                this.text = text?.Trim().TrimStart('\uFEFF')
                    ?? throw new RFrameworkException("Config JSON is null.");
            }

            public object Parse()
            {
                object value = ParseValue(0);
                SkipWhitespace();
                if (index != text.Length)
                {
                    throw Error("Unexpected trailing content.");
                }

                return value;
            }

            private object ParseValue(int depth)
            {
                if (depth > MaxDepth)
                {
                    throw Error($"JSON nesting exceeds {MaxDepth} levels.");
                }

                SkipWhitespace();
                if (index >= text.Length)
                {
                    throw Error("Unexpected end of JSON.");
                }

                switch (text[index])
                {
                    case '{': return ParseObject(depth + 1);
                    case '[': return ParseArray(depth + 1);
                    case '"': return ParseString();
                    case 't': ReadLiteral("true"); return true;
                    case 'f': ReadLiteral("false"); return false;
                    case 'n': ReadLiteral("null"); return null;
                    default: return ParseNumber();
                }
            }

            private Dictionary<string, object> ParseObject(int depth)
            {
                Dictionary<string, object> result = new Dictionary<string, object>(
                    StringComparer.Ordinal);
                index++;
                SkipWhitespace();
                if (ReadIf('}')) return result;

                while (true)
                {
                    SkipWhitespace();
                    if (index >= text.Length || text[index] != '"')
                        throw Error("JSON object key must be a string.");
                    string key = ParseString();
                    SkipWhitespace();
                    Require(':');
                    if (!result.TryAdd(key, ParseValue(depth)))
                        throw Error($"Duplicate JSON object key '{key}'.");
                    SkipWhitespace();
                    if (ReadIf('}')) return result;
                    Require(',');
                }
            }

            private List<object> ParseArray(int depth)
            {
                List<object> result = new List<object>();
                index++;
                SkipWhitespace();
                if (ReadIf(']')) return result;

                while (true)
                {
                    if (result.Count >= MaxCollectionCount)
                        throw Error($"JSON array exceeds {MaxCollectionCount} elements.");
                    result.Add(ParseValue(depth));
                    SkipWhitespace();
                    if (ReadIf(']')) return result;
                    Require(',');
                }
            }

            private string ParseString()
            {
                index++;
                System.Text.StringBuilder builder = new System.Text.StringBuilder();
                while (index < text.Length)
                {
                    char character = text[index++];
                    if (character == '"') return builder.ToString();
                    if (character < 0x20) throw Error("JSON string contains a control character.");
                    if (character != '\\')
                    {
                        builder.Append(character);
                        continue;
                    }

                    if (index >= text.Length) throw Error("Incomplete JSON escape sequence.");
                    character = text[index++];
                    switch (character)
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u': builder.Append(ParseUnicodeEscape()); break;
                        default: throw Error($"Unsupported JSON escape sequence '\\{character}'.");
                    }
                }

                throw Error("Unterminated JSON string.");
            }

            private char ParseUnicodeEscape()
            {
                if (index + 4 > text.Length) throw Error("Incomplete JSON unicode escape.");
                string hex = text.Substring(index, 4);
                index += 4;
                if (!ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                    out ushort value))
                    throw Error($"Invalid JSON unicode escape '{hex}'.");
                return (char)value;
            }

            private JsonNumber ParseNumber()
            {
                int start = index;
                if (ReadIf('-')) { }
                ReadDigits(true);
                if (ReadIf('.')) ReadDigits(true);
                if (ReadIf('e') || ReadIf('E'))
                {
                    if (!ReadIf('+')) ReadIf('-');
                    ReadDigits(true);
                }

                if (start == index) throw Error("Expected a JSON value.");
                return new JsonNumber(text.Substring(start, index - start));
            }

            private void ReadDigits(bool requireOne)
            {
                int start = index;
                while (index < text.Length && char.IsDigit(text[index])) index++;
                if (requireOne && start == index) throw Error("Invalid JSON number.");
            }

            private void ReadLiteral(string literal)
            {
                if (index + literal.Length > text.Length
                    || string.CompareOrdinal(text, index, literal, 0, literal.Length) != 0)
                    throw Error($"Expected JSON literal '{literal}'.");
                index += literal.Length;
            }

            private void Require(char character)
            {
                SkipWhitespace();
                if (!ReadIf(character)) throw Error($"Expected '{character}'.");
            }

            private bool ReadIf(char character)
            {
                if (index >= text.Length || text[index] != character) return false;
                index++;
                return true;
            }

            private void SkipWhitespace()
            {
                while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
            }

            private RFrameworkException Error(string message)
            {
                return new RFrameworkException($"Config JSON at character {index}: {message}");
            }
        }
    }
}
