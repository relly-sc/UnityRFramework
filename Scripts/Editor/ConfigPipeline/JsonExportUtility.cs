using System;
using System.Globalization;
using System.IO;
using System.Text;
using RFramework;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// ConfigPipeline JSON 文本转义与增量写入工具。
    /// </summary>
    internal static class JsonExportUtility
    {
        public static void AppendString(StringBuilder builder, string value)
        {
            builder.Append('"');
            string text = value ?? string.Empty;
            for (int i = 0; i < text.Length; i++)
            {
                char character = text[i];
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < 0x20)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            builder.Append('"');
        }

        public static bool WriteTextIfChanged(string path, string content)
        {
            if (string.IsNullOrWhiteSpace(path) || content == null)
            {
                throw new RFrameworkException("JSON output path or content is invalid.");
            }

            if (File.Exists(path)
                && string.Equals(File.ReadAllText(path, Encoding.UTF8), content,
                    StringComparison.Ordinal))
            {
                return false;
            }

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, content, new UTF8Encoding(false));
            return true;
        }
    }
}
