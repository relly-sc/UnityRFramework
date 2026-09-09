using System;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 标记配置行类型对应的 CSV 文件名或 Excel Sheet 名。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ConfigTableAttribute : Attribute
    {
        /// <summary>
        /// 初始化配置表标记。
        /// </summary>
        /// <param name="tableName">配置表名。</param>
        public ConfigTableAttribute(string tableName)
        {
            TableName = tableName;
        }

        /// <summary>配置表名。</summary>
        public string TableName { get; }
    }
}
