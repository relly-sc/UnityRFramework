using System;
using System.Collections.Generic;
using System.Linq;
using RFramework;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 管理 Excel Localization 内置与项目自定义导出器。
    /// </summary>
    public static class ExcelLocalizationExporterRegistry
    {
        private static readonly Dictionary<string, IExcelLocalizationExporter> Exporters =
            new Dictionary<string, IExcelLocalizationExporter>(
                StringComparer.OrdinalIgnoreCase);

        static ExcelLocalizationExporterRegistry()
        {
            Register(new ExcelLocalizationJsonExporter());
            Register(new ExcelLocalizationBinaryExporter());
        }

        /// <summary>
        /// 注册一个 Localization 导出器。
        /// </summary>
        /// <param name="exporter">导出器实例。</param>
        /// <param name="replace">是否替换同标识的已有导出器。</param>
        public static void Register(
            IExcelLocalizationExporter exporter, bool replace = false)
        {
            if (exporter == null
                || string.IsNullOrWhiteSpace(exporter.Id)
                || string.IsNullOrWhiteSpace(exporter.DisplayName))
            {
                throw new RFrameworkException(
                    "Excel localization exporter is invalid.");
            }

            string id = exporter.Id.Trim();
            if (Exporters.ContainsKey(id) && !replace)
            {
                throw new RFrameworkException(
                    $"Excel localization exporter '{id}' is already registered.");
            }

            Exporters[id] = exporter;
        }

        /// <summary>
        /// 注销项目自定义 Localization 导出器。
        /// </summary>
        /// <param name="id">导出器标识。</param>
        /// <returns>成功移除时返回 true。</returns>
        public static bool Unregister(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && Exporters.Remove(id.Trim());
        }

        /// <summary>
        /// 查询指定 Localization 导出器。
        /// </summary>
        /// <param name="id">导出器标识。</param>
        /// <param name="exporter">找到的导出器。</param>
        /// <returns>存在时返回 true。</returns>
        public static bool TryGet(
            string id, out IExcelLocalizationExporter exporter)
        {
            return Exporters.TryGetValue(id ?? string.Empty, out exporter);
        }

        /// <summary>
        /// 获取按显示名称排序的全部 Localization 导出器快照。
        /// </summary>
        /// <returns>导出器只读列表。</returns>
        public static IReadOnlyList<IExcelLocalizationExporter> GetAll()
        {
            return Exporters.Values
                .OrderBy(exporter => exporter.DisplayName, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
