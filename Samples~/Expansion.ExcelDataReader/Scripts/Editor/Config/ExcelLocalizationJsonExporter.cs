using System.Collections.Generic;
using System.Text;
using UnityRFramework.Editor;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 将 Excel Localization 导出为 JsonLocalizationHelper 可读取的 JSON。
    /// </summary>
    public sealed class ExcelLocalizationJsonExporter :
        IExcelLocalizationExporter
    {
        /// <inheritdoc/>
        public string Id => ExcelLocalizationExporterIds.Json;

        /// <inheritdoc/>
        public string DisplayName => "JSON";

        /// <inheritdoc/>
        public IReadOnlyList<ExcelLocalizationOutputFile> Build(
            IReadOnlyList<LocalizationTable> localizations,
            bool exportBundle,
            string bundleName)
        {
            List<ExcelLocalizationOutputFile> outputs =
                new List<ExcelLocalizationOutputFile>(
                    localizations.Count + (exportBundle ? 1 : 0));
            UTF8Encoding encoding = new UTF8Encoding(false);
            for (int i = 0; i < localizations.Count; i++)
            {
                LocalizationTable localization = localizations[i];
                outputs.Add(new ExcelLocalizationOutputFile(
                    $"Json/{localization.Language}.json",
                    encoding.GetBytes(LocalizationJsonExporter.Build(localization))));
            }

            if (exportBundle)
            {
                outputs.Add(new ExcelLocalizationOutputFile(
                    $"Json/{bundleName}.json",
                    encoding.GetBytes(
                        LocalizationJsonExporter.BuildBundle(localizations))));
            }

            return outputs;
        }
    }
}
