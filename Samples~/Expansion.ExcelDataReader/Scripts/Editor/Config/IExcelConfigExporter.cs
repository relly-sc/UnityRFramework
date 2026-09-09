using System.Collections.Generic;
using UnityRFramework.Editor;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 将已通过 ConfigPipeline 校验的 Excel 配置表导出为项目自定义格式。
    /// </summary>
    public interface IExcelConfigExporter
    {
        /// <summary>获取持久化设置使用的唯一标识。</summary>
        string Id { get; }

        /// <summary>获取编辑器窗口显示名称。</summary>
        string DisplayName { get; }

        /// <summary>
        /// 构建一个或多个输出文件。
        /// </summary>
        /// <param name="schemas">全部已校验配置表，可用于生成多表容器。</param>
        /// <returns>相对于输出根目录的文件集合。</returns>
        IReadOnlyList<ExcelConfigOutputFile> Build(
            IReadOnlyList<ConfigTableSchema> schemas);
    }
}
