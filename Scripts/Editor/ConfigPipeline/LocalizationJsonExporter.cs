using System.Text;
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
    }
}
