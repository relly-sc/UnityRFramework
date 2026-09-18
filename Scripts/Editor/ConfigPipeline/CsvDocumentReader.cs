using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RFramework;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 零第三方 CSV 读取器。支持 UTF-8 BOM、引号、转义引号、字段内逗号与换行。
    /// </summary>
    public static class CsvDocumentReader
    {
        /// <summary>
        /// 读取并解析 UTF-8 CSV 文件。
        /// </summary>
        /// <param name="path">CSV 文件路径。</param>
        /// <returns>解析后的 CSV 文档。</returns>
        public static CsvDocument ReadFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                throw new RFrameworkException($"CSV file does not exist: '{path}'.");
            }

            string text = File.ReadAllText(path, Encoding.UTF8);
            return Parse(path, text);
        }

        /// <summary>
        /// 解析内存中的 CSV 文本。
        /// </summary>
        /// <param name="sourcePath">错误信息使用的源路径或逻辑名称。</param>
        /// <param name="text">CSV 文本。</param>
        /// <returns>解析后的 CSV 文档。</returns>
        public static CsvDocument Parse(string sourcePath, string text)
        {
            if (text == null)
            {
                throw new RFrameworkException($"CSV content is null: '{sourcePath}'.");
            }

            if (text.Length > 0 && text[0] == '\uFEFF')
            {
                text = text.Substring(1);
            }

            List<CsvRow> rows = new List<CsvRow>();
            List<string> fields = new List<string>();
            StringBuilder field = new StringBuilder();
            int line = 1;
            int rowStartLine = 1;
            bool inQuotes = false;
            bool afterQuote = false;
            bool rowHasContent = false;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                            afterQuote = true;
                        }
                    }
                    else
                    {
                        field.Append(ch);
                        rowHasContent = true;
                        if (ch == '\n')
                        {
                            line++;
                        }
                    }

                    continue;
                }

                if (afterQuote)
                {
                    if (ch == ' ' || ch == '\t')
                    {
                        continue;
                    }

                    if (ch != ',' && ch != '\r' && ch != '\n')
                    {
                        throw Error(sourcePath, line,
                            "Unexpected character after a closing quote.");
                    }

                    afterQuote = false;
                }

                if (ch == '"')
                {
                    if (field.Length != 0)
                    {
                        throw Error(sourcePath, line,
                            "A quoted field must start with a quote.");
                    }

                    inQuotes = true;
                    rowHasContent = true;
                }
                else if (ch == ',')
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    rowHasContent = true;
                }
                else if (ch == '\r' || ch == '\n')
                {
                    if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }

                    fields.Add(field.ToString());
                    field.Clear();
                    rows.Add(new CsvRow(rowStartLine, fields.ToArray()));
                    fields.Clear();
                    line++;
                    rowStartLine = line;
                    rowHasContent = false;
                }
                else
                {
                    field.Append(ch);
                    if (!char.IsWhiteSpace(ch))
                    {
                        rowHasContent = true;
                    }
                }
            }

            if (inQuotes)
            {
                throw Error(sourcePath, line, "Quoted field is not closed.");
            }

            if (rowHasContent || field.Length > 0 || fields.Count > 0)
            {
                fields.Add(field.ToString());
                rows.Add(new CsvRow(rowStartLine, fields.ToArray()));
            }

            return new CsvDocument(sourcePath, rows);
        }

        private static RFrameworkException Error(string path, int line, string message)
        {
            return new RFrameworkException($"CSV '{path}', line {line}: {message}");
        }
    }
}
