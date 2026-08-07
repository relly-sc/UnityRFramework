using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 保存 Excel 配置表工具的项目外编辑器偏好。
    /// </summary>
    internal static class ExcelConfigToolPreferences
    {
        private const string ConfigPreferencesKey =
            "UnityRFramework.Expansion.ExcelConfig.Options";

        private const string LocalizationPreferencesKey =
            "UnityRFramework.Expansion.ExcelLocalization.Options";

        /// <summary>
        /// 加载 Config 工具设置。
        /// </summary>
        /// <returns>有效的工具设置。</returns>
        internal static ExcelConfigExportOptions LoadConfig()
        {
            ExcelConfigExportOptions options = new ExcelConfigExportOptions();
            string json = EditorPrefs.GetString(
                ConfigPreferencesKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    JsonUtility.FromJsonOverwrite(json, options);
                }
                catch
                {
                    options = new ExcelConfigExportOptions();
                }
            }

            if (options.SelectedExporterIds == null)
            {
                options.SelectedExporterIds = new List<string>();
            }

            return options;
        }

        /// <summary>
        /// 保存 Config 工具设置。
        /// </summary>
        /// <param name="options">待保存设置。</param>
        internal static void SaveConfig(ExcelConfigExportOptions options)
        {
            if (options != null)
            {
                EditorPrefs.SetString(
                    ConfigPreferencesKey, JsonUtility.ToJson(options));
            }
        }

        /// <summary>
        /// 加载 Localization 工具设置。
        /// </summary>
        /// <returns>有效的工具设置。</returns>
        internal static ExcelLocalizationExportOptions LoadLocalization()
        {
            ExcelLocalizationExportOptions options =
                new ExcelLocalizationExportOptions();
            string json = EditorPrefs.GetString(
                LocalizationPreferencesKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    JsonUtility.FromJsonOverwrite(json, options);
                }
                catch
                {
                    options = new ExcelLocalizationExportOptions();
                }
            }

            if (options.SelectedExporterIds == null)
            {
                options.SelectedExporterIds = new List<string>();
            }

            return options;
        }

        /// <summary>
        /// 保存 Localization 工具设置。
        /// </summary>
        /// <param name="options">待保存设置。</param>
        internal static void SaveLocalization(
            ExcelLocalizationExportOptions options)
        {
            if (options != null)
            {
                EditorPrefs.SetString(
                    LocalizationPreferencesKey, JsonUtility.ToJson(options));
            }
        }
    }
}
