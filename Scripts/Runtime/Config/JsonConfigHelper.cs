using System;
using System.Text;
using RFramework;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// JSON 配置辅助器。
    /// 字节入口按 UTF-8 JSON 解析；字符串入口由 DictionaryConfigHelperBase 直接解析 JSON。
    /// 两个公开入口相互独立，共享受保护的 JSON 解析核心。
    /// 配置行须符合 JsonUtility 约束（可序列化公开字段），并包含公开 Id/id 字段。
    /// </summary>
    public sealed class JsonConfigHelper : DictionaryConfigHelperBase
    {
        /// <inheritdoc/>
        public override object ParseConfig(Type tableType, byte[] bytes)
        {
            ValidateRowType(tableType);
            if (bytes == null || bytes.Length == 0)
            {
                throw new RFrameworkException("JsonConfigHelper: JSON bytes are empty.");
            }

            string json = Encoding.UTF8.GetString(bytes);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new RFrameworkException("JsonConfigHelper: decoded JSON content is empty.");
            }

            return ParseJsonToIndexedTable(tableType, json);
        }
    }
}
