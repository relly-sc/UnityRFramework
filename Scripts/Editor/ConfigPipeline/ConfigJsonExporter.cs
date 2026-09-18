using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RFramework;
using UnityRFramework.Runtime;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 将经过校验的 Config 表导出为 JsonConfigHelper 可读取的 JSON。
    /// </summary>
    public static class ConfigJsonExporter
    {
        public static string Build(ConfigTableSchema schema)
        {
            if (schema == null)
            {
                throw new RFrameworkException("Config schema is invalid.");
            }

            return BuildBundle(new[] { schema });
        }

        /// <summary>构建包含一张或多张 Config 分片的 JSON 容器。</summary>
        public static string BuildBundle(IReadOnlyList<ConfigTableSchema> schemas)
        {
            if (schemas == null || schemas.Count == 0)
            {
                throw new RFrameworkException("Config bundle schemas are empty.");
            }

            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("{");
            builder.AppendLine("  \"Tables\": {");
            for (int schemaIndex = 0; schemaIndex < schemas.Count; schemaIndex++)
            {
                ConfigTableSchema schema = schemas[schemaIndex]
                    ?? throw new RFrameworkException("Config bundle contains an invalid schema.");
                builder.Append("    ");
                JsonExportUtility.AppendString(builder,
                    string.IsNullOrEmpty(schema.SegmentName)
                        ? schema.TableName
                        : schema.SegmentName);
                builder.AppendLine(": {");
                builder.Append("      \"TableId\": \"")
                    .Append(schema.TableId.ToString("X8", CultureInfo.InvariantCulture))
                    .AppendLine("\",");
                builder.Append("      \"SchemaHash\": \"")
                    .Append(schema.SchemaHash.ToString("X16", CultureInfo.InvariantCulture))
                    .AppendLine("\",");
                builder.AppendLine("      \"Rows\": [");
                for (int rowIndex = 0; rowIndex < schema.Rows.Count; rowIndex++)
                {
                    CsvRow row = schema.Rows[rowIndex];
                    builder.AppendLine("        {");
                    for (int fieldIndex = 0; fieldIndex < schema.Fields.Count; fieldIndex++)
                    {
                        ConfigFieldSchema field = schema.Fields[fieldIndex];
                        builder.Append("          ");
                        JsonExportUtility.AppendString(builder, field.Name);
                        builder.Append(": ");
                        AppendValue(builder, field, row.Values[fieldIndex], schema.SourcePath,
                            row.LineNumber);
                        builder.AppendLine(
                            fieldIndex + 1 == schema.Fields.Count ? string.Empty : ",");
                    }

                    builder.Append("        }");
                    builder.AppendLine(rowIndex + 1 == schema.Rows.Count ? string.Empty : ",");
                }

                builder.AppendLine("      ]");
                builder.Append("    }");
                builder.AppendLine(schemaIndex + 1 == schemas.Count ? string.Empty : ",");
            }

            builder.AppendLine("  }");
            builder.Append('}');
            return builder.ToString();
        }

        private static void AppendValue(
            StringBuilder builder,
            ConfigFieldSchema field,
            string value,
            string sourcePath,
            int lineNumber)
        {
            object parsed;
            try
            {
                parsed = ConfigValueParser.ParseValue(field, value);
            }
            catch (Exception ex)
            {
                throw new RFrameworkException(
                    $"Data '{sourcePath}', line {lineNumber}, field '{field.Name}' "
                    + "could not be written as JSON.", ex);
            }

            if (field.Kind == ConfigFieldKind.Array || field.Kind == ConfigFieldKind.List)
            {
                IReadOnlyList<object> elements = (IReadOnlyList<object>)parsed;
                builder.Append('[');
                for (int i = 0; i < elements.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(", ");
                    }

                    AppendScalar(builder, field.ElementKind, elements[i]);
                }

                builder.Append(']');
                return;
            }

            if (field.Kind == ConfigFieldKind.Enum)
            {
                builder.Append((int)parsed);
                return;
            }

            if (field.Kind == ConfigFieldKind.Custom)
            {
                if (!ConfigFieldCodecRegistry.TryGet(
                    field.TypeKeyword, out IConfigFieldCodec codec))
                {
                    throw new RFrameworkException(
                        $"Config field codec '{field.TypeKeyword}' is not registered.");
                }

                string jsonValue = codec.FormatJson(parsed);
                if (jsonValue == null)
                {
                    throw new RFrameworkException(
                        $"Config field codec '{field.TypeKeyword}' returned a null JSON value.");
                }

                JsonExportUtility.AppendString(builder, jsonValue);
                return;
            }

            AppendScalar(builder, field.Kind, parsed);
        }

        private static void AppendScalar(
            StringBuilder builder, ConfigFieldKind kind, object parsed)
        {
            switch (kind)
            {
                case ConfigFieldKind.Boolean:
                    builder.Append((bool)parsed ? "true" : "false");
                    break;
                case ConfigFieldKind.Single:
                    builder.Append(((float)parsed).ToString("R", CultureInfo.InvariantCulture));
                    break;
                case ConfigFieldKind.Double:
                    builder.Append(((double)parsed).ToString("R", CultureInfo.InvariantCulture));
                    break;
                case ConfigFieldKind.Decimal:
                    builder.Append(((decimal)parsed).ToString(CultureInfo.InvariantCulture));
                    break;
                case ConfigFieldKind.Char:
                    JsonExportUtility.AppendString(builder, ((char)parsed).ToString());
                    break;
                case ConfigFieldKind.String:
                    JsonExportUtility.AppendString(builder, (string)parsed);
                    break;
                default:
                    builder.Append(Convert.ToString(parsed, CultureInfo.InvariantCulture));
                    break;
            }
        }
    }
}
