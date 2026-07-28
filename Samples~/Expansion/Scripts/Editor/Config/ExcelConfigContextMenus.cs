using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 为 Project 视图选中的 Excel 文件或文件夹提供固定路径导出入口。
    /// </summary>
    public static class ExcelConfigContextMenus
    {
        private const string ConfigSelectedFormatsMenu =
            "Assets/UnityRFramework/Excel/Config/按工具所选格式导出";
        private const string ConfigJsonMenu =
            "Assets/UnityRFramework/Excel/Config/导出为 JSON";
        private const string ConfigBinaryMenu =
            "Assets/UnityRFramework/Excel/Config/导出为 Binary";
        private const string LocalizationSelectedFormatsMenu =
            "Assets/UnityRFramework/Excel/Localization/按工具所选格式导出";
        private const string LocalizationJsonMenu =
            "Assets/UnityRFramework/Excel/Localization/导出为 JSON";
        private const string LocalizationBinaryMenu =
            "Assets/UnityRFramework/Excel/Localization/导出为 Binary";

        /// <summary>按 Excel 工具中勾选的格式导出当前选择。</summary>
        [MenuItem(ConfigSelectedFormatsMenu, false, 1)]
        public static void ExportConfigSelectedFormats()
        {
            ExportConfigSelection(null);
        }

        /// <summary>校验当前选择是否包含 Excel 文件。</summary>
        /// <returns>包含可用文件时返回 true。</returns>
        [MenuItem(ConfigSelectedFormatsMenu, true)]
        public static bool ValidateConfigSelectedFormats()
        {
            return HasExcelSelection();
        }

        /// <summary>将当前选择导出为 JSON。</summary>
        [MenuItem(ConfigJsonMenu, false, 2)]
        public static void ExportConfigJson()
        {
            ExportConfigSelection(new[] { ExcelConfigExporterIds.Json });
        }

        /// <summary>校验当前选择是否包含 Excel 文件。</summary>
        /// <returns>包含可用文件时返回 true。</returns>
        [MenuItem(ConfigJsonMenu, true)]
        public static bool ValidateConfigJson()
        {
            return HasExcelSelection();
        }

        /// <summary>将当前选择导出为 URFC v2 二进制。</summary>
        [MenuItem(ConfigBinaryMenu, false, 3)]
        public static void ExportConfigBinary()
        {
            ExportConfigSelection(new[] { ExcelConfigExporterIds.Binary });
        }

        /// <summary>校验当前选择是否包含 Excel 文件。</summary>
        /// <returns>包含可用文件时返回 true。</returns>
        [MenuItem(ConfigBinaryMenu, true)]
        public static bool ValidateConfigBinary()
        {
            return HasExcelSelection();
        }

        /// <summary>按工具设置导出选中的 Localization。</summary>
        [MenuItem(LocalizationSelectedFormatsMenu, false, 4)]
        public static void ExportLocalizationSelectedFormats()
        {
            ExportLocalizationSelection(null);
        }

        /// <summary>校验当前选择是否包含 Excel 文件。</summary>
        /// <returns>包含可用文件时返回 true。</returns>
        [MenuItem(LocalizationSelectedFormatsMenu, true)]
        public static bool ValidateLocalizationSelectedFormats()
        {
            return HasExcelSelection();
        }

        /// <summary>将选中的 Localization 导出为 JSON。</summary>
        [MenuItem(LocalizationJsonMenu, false, 5)]
        public static void ExportLocalizationJson()
        {
            ExportLocalizationSelection(
                new[] { ExcelLocalizationExporterIds.Json });
        }

        /// <summary>校验当前选择是否包含 Excel 文件。</summary>
        /// <returns>包含可用文件时返回 true。</returns>
        [MenuItem(LocalizationJsonMenu, true)]
        public static bool ValidateLocalizationJson()
        {
            return HasExcelSelection();
        }

        /// <summary>将选中的 Localization 导出为 URFL/URLM 二进制。</summary>
        [MenuItem(LocalizationBinaryMenu, false, 6)]
        public static void ExportLocalizationBinary()
        {
            ExportLocalizationSelection(
                new[] { ExcelLocalizationExporterIds.Binary });
        }

        /// <summary>校验当前选择是否包含 Excel 文件。</summary>
        /// <returns>包含可用文件时返回 true。</returns>
        [MenuItem(LocalizationBinaryMenu, true)]
        public static bool ValidateLocalizationBinary()
        {
            return HasExcelSelection();
        }

        private static void ExportConfigSelection(
            IReadOnlyList<string> exporterIds)
        {
            try
            {
                IReadOnlyList<string> paths = GetSelectionPaths();
                ExcelConfigExportOptions options =
                    ExcelConfigToolPreferences.LoadConfig();
                options.OutputDirectory = ExcelConfigExportDefaults.OutputDirectory;
                options.GeneratedCodeDirectory =
                    ExcelConfigExportDefaults.GeneratedCodeDirectory;
                if (exporterIds != null)
                {
                    options.SelectedExporterIds = new List<string>(exporterIds);
                }

                ExcelConfigExportReport report =
                    ExcelConfigExportService.Export(paths, options);
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < report.Messages.Count; i++)
                {
                    builder.AppendLine(report.Messages[i]);
                }

                Debug.Log(builder.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private static void ExportLocalizationSelection(
            IReadOnlyList<string> exporterIds)
        {
            try
            {
                IReadOnlyList<string> paths = GetSelectionPaths();
                ExcelLocalizationExportOptions options =
                    ExcelConfigToolPreferences.LoadLocalization();
                options.OutputDirectory =
                    ExcelLocalizationExportDefaults.OutputDirectory;
                if (exporterIds != null)
                {
                    options.SelectedExporterIds =
                        new List<string>(exporterIds);
                }

                ExcelConfigExportReport report =
                    ExcelLocalizationExportService.Export(paths, options);
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < report.Messages.Count; i++)
                {
                    builder.AppendLine(report.Messages[i]);
                }

                Debug.Log(builder.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private static bool HasExcelSelection()
        {
            return ExcelExportUtility.ContainsExcelFiles(GetSelectionPaths());
        }

        private static IReadOnlyList<string> GetSelectionPaths()
        {
            UnityEngine.Object[] selected =
                Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets);
            List<string> paths = new List<string>(selected.Length);
            for (int i = 0; i < selected.Length; i++)
            {
                string path = AssetDatabase.GetAssetPath(selected[i]);
                if (!string.IsNullOrEmpty(path))
                {
                    paths.Add(path);
                }
            }

            return paths;
        }
    }
}
