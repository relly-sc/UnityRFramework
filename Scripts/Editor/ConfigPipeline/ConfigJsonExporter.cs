using System;
using System.Globalization;
using System.Text;
using RFramework;

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

            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("{");
            builder.AppendLine("  \"Items\": [");
            for (int rowIndex = 0; rowIndex < schema.Rows.Count; rowIndex++)
            {
                CsvRow row = schema.Rows[rowIndex];
                builder.AppendLine("    {");
                for (int fieldIndex = 0; fieldIndex < schema.Fields.Count; fieldIndex++)
                {
                    ConfigFieldSchema field = schema.Fields[fieldIndex];
                    builder.Append("      ");
                    JsonExportUtility.AppendString(builder, field.Name);
                    builder.Append(": ");
                    AppendValue(builder, field, row.Values[fieldIndex], schema.SourcePath,
                        row.LineNumber);
                    builder.AppendLine(fieldIndex + 1 == schema.Fields.Count ? string.Empty : ",");
                }

                builder.Append("    }");
                builder.AppendLine(rowIndex + 1 == schema.Rows.Count ? string.Empty : ",");
            }

            builder.AppendLine("  ]");
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

            switch (field.Kind)
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
