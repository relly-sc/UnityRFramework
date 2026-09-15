using System.Collections.Generic;
using UnityRFramework.Editor;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 将已通过 ConfigPipeline 校验的 Excel Localization 导出为项目自定义格式。
    /// </summary>
    public interface IExcelLocalizationExporter
    {
        /// <summary>获取持久化设置使用的唯一标识。</summary>
        string Id { get; }

        /// <summary>获取编辑器窗口显示名称。</summary>
        string DisplayName { get; }

        /// <summary>
        /// 构建单语言文件及可选的多语言容器。
        /// </summary>
        /// <param name="localizations">全部已校验语言表。</param>
        /// <param name="exportBundle">是否导出多语言容器。</param>
        /// <param name="bundleName">不含扩展名的多语言容器名称。</param>
        /// <returns>相对于输出根目录的文件集合。</returns>
        IReadOnlyList<ExcelLocalizationOutputFile> Build(
            IReadOnlyList<LocalizationTable> localizations,
            bool exportBundle,
            string bundleName);
    }
}
