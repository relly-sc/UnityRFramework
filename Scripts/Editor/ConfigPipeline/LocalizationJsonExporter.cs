using System.Text;
using System.Collections.Generic;
using RFramework;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 将经过校验的 Localization 表导出为 JsonLocalizationHelper 可读取的 JSON。
    /// </summary>
    public static class LocalizationJsonExporter
    {
        public static string Build(LocalizationTable localization)
        {
            if (localization == null)
            {
                throw new RFrameworkException("Localization table is invalid.");
            }

            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("{");
            builder.AppendLine("  \"Items\": [");
            for (int i = 0; i < localization.Entries.Count; i++)
            {
                builder.Append("    { \"Key\": ");
                JsonExportUtility.AppendString(builder, localization.Entries[i].Key);
                builder.Append(", \"Value\": ");
                JsonExportUtility.AppendString(builder, localization.Entries[i].Value);
                builder.Append(" }");
                builder.AppendLine(i + 1 == localization.Entries.Count ? string.Empty : ",");
            }

            builder.AppendLine("  ]");
            builder.Append('}');
            return builder.ToString();
        }

        /// <summary>构建包含多种语言的 JSON 容器。</summary>
        public static string BuildBundle(IReadOnlyList<LocalizationTable> localizations)
        {
            if (localizations == null || localizations.Count == 0)
            {
                throw new RFrameworkException("Localization bundle sources are empty.");
            }

            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("{");
            builder.AppendLine("  \"Languages\": [");
            for (int languageIndex = 0; languageIndex < localizations.Count; languageIndex++)
            {
                LocalizationTable localization = localizations[languageIndex]
                    ?? throw new RFrameworkException(
                        "Localization bundle contains an invalid source.");
                builder.Append("    { \"Language\": ");
                JsonExportUtility.AppendString(builder, localization.Language);
                builder.AppendLine(", \"Items\": [");
                for (int i = 0; i < localization.Entries.Count; i++)
                {
                    builder.Append("      { \"Key\": ");
                    JsonExportUtility.AppendString(builder, localization.Entries[i].Key);
                    builder.Append(", \"Value\": ");
                    JsonExportUtility.AppendString(builder, localization.Entries[i].Value);
                    builder.Append(" }");
                    builder.AppendLine(
                        i + 1 == localization.Entries.Count ? string.Empty : ",");
                }

                builder.Append("    ] }");
                builder.AppendLine(
                    languageIndex + 1 == localizations.Count ? string.Empty : ",");
            }

            builder.AppendLine("  ]");
            builder.Append('}');
            return builder.ToString();
        }
    }
}
