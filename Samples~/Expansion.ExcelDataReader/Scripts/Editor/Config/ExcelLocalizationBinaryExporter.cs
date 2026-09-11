using System.Collections.Generic;
using UnityRFramework.Editor;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 将 Excel Localization 导出为 URFL v2 和 URLM v1 二进制。
    /// </summary>
    public sealed class ExcelLocalizationBinaryExporter :
        IExcelLocalizationExporter
    {
        /// <inheritdoc/>
        public string Id => ExcelLocalizationExporterIds.Binary;

        /// <inheritdoc/>
        public string DisplayName => "Binary (URFL v2 / URLM v1)";

        /// <inheritdoc/>
        public IReadOnlyList<ExcelLocalizationOutputFile> Build(
            IReadOnlyList<LocalizationTable> localizations,
            bool exportBundle,
            string bundleName)
        {
            List<ExcelLocalizationOutputFile> outputs =
                new List<ExcelLocalizationOutputFile>(
                    localizations.Count + (exportBundle ? 1 : 0));
            for (int i = 0; i < localizations.Count; i++)
            {
                LocalizationTable localization = localizations[i];
                outputs.Add(new ExcelLocalizationOutputFile(
                    $"Binary/{localization.Language}.bytes",
                    LocalizationBinaryExporter.BuildV2(localization)));
            }

            if (exportBundle)
            {
                outputs.Add(new ExcelLocalizationOutputFile(
                    $"Binary/{bundleName}.bytes",
                    LocalizationBinaryExporter.BuildBundle(localizations)));
            }

            return outputs;
        }
    }
}
