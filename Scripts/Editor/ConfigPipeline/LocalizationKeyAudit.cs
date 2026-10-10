using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UnityRFramework.Editor
{
    /// <summary>检查可静态确定的本地化键引用，不修改源文件。</summary>
    public static class LocalizationKeyAudit
    {
        private const string FieldMarker = "@LocalizationKey";
        private static readonly Regex GetStringCall = new Regex(
            @"\bGameEntry\s*\.\s*Localization\s*\.\s*GetString\s*\(",
            RegexOptions.CultureInvariant);
        private static readonly Regex LiteralArgument = new Regex(
            @"^\s*""(?<key>[^""\\]*)""\s*(?:,|\))",
            RegexOptions.CultureInvariant);

        /// <summary>合并语言表、显式标记的 Config 字段和代码中的固定键引用。</summary>
        public static void Analyze(
            IReadOnlyList<ConfigTableSchema> configs,
            IReadOnlyList<LocalizationTable> languages,
            IReadOnlyList<KeyValuePair<string, string>> codeSources,
            IReadOnlyList<KeyValuePair<string, string>> prefabReferences,
            IEnumerable<string> reservedLines,
            ConfigPipelineReport report)
        {
            if (configs == null || languages == null || codeSources == null
                || prefabReferences == null
                || reservedLines == null || report == null)
            {
                throw new ArgumentNullException("Localization audit input is null.");
            }

            Dictionary<string, string> references =
                new Dictionary<string, string>(StringComparer.Ordinal);
            List<string> dynamicCalls = new List<string>();
            foreach (KeyValuePair<string, string> prefab in prefabReferences)
            {
                if (!string.IsNullOrWhiteSpace(prefab.Key)
                    && !references.ContainsKey(prefab.Key))
                {
                    references.Add(prefab.Key, prefab.Value);
                }
            }

            foreach (ConfigTableSchema config in configs)
            {
                for (int column = 0; column < config.Fields.Count; column++)
                {
                    ConfigFieldSchema field = config.Fields[column];
                    if (field.Comment == null
                        || field.Comment.IndexOf(FieldMarker, StringComparison.Ordinal) < 0)
                    {
                        continue;
                    }

                    if (field.Kind != ConfigFieldKind.String)
                    {
                        throw new InvalidOperationException(
                            $"{config.SourcePath}: {FieldMarker} requires a string field: {field.Name}.");
                    }

                    foreach (CsvRow row in config.Rows)
                    {
                        string key = row.Values[column]?.Trim();
                        if (!string.IsNullOrEmpty(key) && !references.ContainsKey(key))
                        {
                            references.Add(key,
                                $"{config.SourcePath}:{row.LineNumber} ({field.Name})");
                        }
                    }
                }
            }

            foreach (KeyValuePair<string, string> source in codeSources)
            {
                string text = source.Value ?? string.Empty;
                bool[] codePositions = FindCodePositions(text);
                int previousIndex = 0;
                int line = 1;
                foreach (Match call in GetStringCall.Matches(text))
                {
                    for (int i = previousIndex; i < call.Index; i++)
                    {
                        if (text[i] == '\n') line++;
                    }

                    previousIndex = call.Index;
                    if (!codePositions[call.Index])
                    {
                        continue;
                    }

                    string location = $"{source.Key}:{line}";
                    Match literal = LiteralArgument.Match(text.Substring(call.Index + call.Length));
                    if (literal.Success)
                    {
                        string key = literal.Groups["key"].Value;
                        if (!references.ContainsKey(key)) references.Add(key, location);
                    }
                    else
                    {
                        dynamicCalls.Add(location);
                    }
                }
            }

            HashSet<string> reserved = new HashSet<string>(StringComparer.Ordinal);
            foreach (string rawLine in reservedLines)
            {
                string key = rawLine?.Trim();
                if (!string.IsNullOrEmpty(key) && !key.StartsWith("#", StringComparison.Ordinal))
                {
                    reserved.Add(key);
                }
            }

            SortedSet<string> allKeys = new SortedSet<string>(references.Keys, StringComparer.Ordinal);
            allKeys.UnionWith(reserved);
            List<Dictionary<string, string>> tables = new List<Dictionary<string, string>>();
            foreach (LocalizationTable language in languages)
            {
                Dictionary<string, string> entries = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, string> entry in language.Entries)
                {
                    entries.Add(entry.Key, entry.Value);
                    allKeys.Add(entry.Key);
                }

                tables.Add(entries);
            }

            List<string> missing = new List<string>();
            List<string> empty = new List<string>();
            foreach (string key in allKeys)
            {
                for (int i = 0; i < languages.Count; i++)
                {
                    if (!tables[i].TryGetValue(key, out string value))
                    {
                        missing.Add($"{languages[i].Language}: {key}"
                            + (references.TryGetValue(key, out string location)
                                ? $" ({location})" : string.Empty));
                    }
                    else if (string.IsNullOrWhiteSpace(value))
                    {
                        empty.Add($"{languages[i].Language}: {key}");
                    }
                }
            }

            string[] unused = allKeys.Where(key => !references.ContainsKey(key) && !reserved.Contains(key))
                .ToArray();
            report.AddMessage($"本地化键检查：{languages.Count} 种语言，{codeSources.Count} 个代码文件，"
                + $"{prefabReferences.Count} 个 Prefab 键组件，"
                + $"{references.Count} 个静态引用，{dynamicCalls.Count} 处动态调用。");
            AddSection(report, "缺失键", missing);
            AddSection(report, "空译文", empty);
            AddSection(report, "可能未使用（先人工确认，不自动删除）", unused);
            AddSection(report, "无法静态确认的调用（请将对应键加入 ReservedKeys.txt）", dynamicCalls);
        }

        private static void AddSection(
            ConfigPipelineReport report, string title, IEnumerable<string> entries)
        {
            string[] values = entries.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            report.AddMessage($"{title}：{values.Length}");
            foreach (string value in values) report.AddMessage("  " + value);
        }

        private static bool[] FindCodePositions(string text)
        {
            bool[] result = new bool[text.Length];
            for (int i = 0; i < text.Length;)
            {
                if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '/')
                {
                    i = text.IndexOf('\n', i + 2);
                    if (i < 0) break;
                }
                else if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '*')
                {
                    i = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    i = i < 0 ? text.Length : i + 2;
                }
                else if (text[i] == '@' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    i += 2;
                    while (i < text.Length)
                    {
                        if (text[i++] != '"') continue;
                        if (i < text.Length && text[i] == '"') i++;
                        else break;
                    }
                }
                else if (text[i] == '"' || text[i] == '\'')
                {
                    char quote = text[i++];
                    while (i < text.Length)
                    {
                        if (text[i] == '\\') i = Math.Min(i + 2, text.Length);
                        else if (text[i++] == quote) break;
                    }
                }
                else
                {
                    result[i++] = true;
                }
            }

            return result;
        }
    }
}
