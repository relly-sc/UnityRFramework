using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RFramework;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 将 Key/Value CSV 解析为单语言本地化表。
    /// </summary>
    public static class LocalizationCsvParser
    {
        /// <summary>
        /// 解析并校验一个语言 CSV。
        /// </summary>
        /// <param name="document">CSV 文档。</param>
        /// <returns>经过校验的本地化键值表。</returns>
        public static LocalizationTable Parse(CsvDocument document)
        {
            if (document == null || document.Rows.Count == 0)
            {
                throw new RFrameworkException("Localization CSV is empty.");
            }

            CsvRow header = document.Rows[0];
            if (header.Values.Count != 2
                || !string.Equals(header.Values[0].Trim(), "Key", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(header.Values[1].Trim(), "Value", StringComparison.OrdinalIgnoreCase))
            {
                throw Error(document.SourcePath, header.LineNumber,
                    "Localization header must be exactly 'Key,Value'.");
            }

            List<KeyValuePair<string, string>> entries = new List<KeyValuePair<string, string>>();
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 1; i < document.Rows.Count; i++)
            {
                CsvRow row = document.Rows[i];
                if (row.Values.All(string.IsNullOrWhiteSpace))
                {
                    continue;
                }

                if (row.Values.Count != 2)
                {
                    throw Error(document.SourcePath, row.LineNumber,
                        $"Localization row must contain 2 columns, found {row.Values.Count}.");
                }

                string key = row.Values[0].Trim();
                if (string.IsNullOrEmpty(key))
                {
                    throw Error(document.SourcePath, row.LineNumber, "Localization key is empty.");
                }

                if (!keys.Add(key))
                {
                    throw Error(document.SourcePath, row.LineNumber,
                        $"Duplicate localization key '{key}'.");
                }

                entries.Add(new KeyValuePair<string, string>(key, row.Values[1] ?? string.Empty));
            }

            if (entries.Count == 0)
            {
                throw new RFrameworkException(
                    $"Localization CSV '{document.SourcePath}' contains no entries.");
            }

            return new LocalizationTable
            {
                SourcePath = document.SourcePath,
                Language = Path.GetFileNameWithoutExtension(document.SourcePath),
                Entries = entries
            };
        }

        private static RFrameworkException Error(string path, int line, string message)
        {
            return new RFrameworkException($"Localization '{path}', line {line}: {message}");
        }
    }
}
