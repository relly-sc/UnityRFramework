using System;
using RFramework;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 框架单配置表二进制协议解析器（URFC v1/v2）。
    /// JSON 字符串入口由 DictionaryConfigHelperBase 保留。
    /// </summary>
    public sealed class BinaryConfigHelper : DictionaryConfigHelperBase
    {
        /// <inheritdoc/>
        public override object ParseConfig(Type tableType, byte[] bytes)
        {
            ValidateRowType(tableType);
            ushort version = BinaryTableUtility.GetConfigVersion(bytes);
            if (version == BinaryFormatUtility.ConfigReflectionVersion)
            {
                return BuildIndexedTable(tableType, BinaryTableUtility.ReadConfigRows(tableType, bytes));
            }

            if (version == BinaryFormatUtility.ConfigGeneratedVersion)
            {
                return BinaryTableUtility.ReadGeneratedConfigTable(tableType, bytes);
            }

            throw new RFrameworkException(
                $"Binary config version '{version}' is not supported. Expected version 1 or 2.");
        }
    }
}
