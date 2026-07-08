using System;
using System.Collections.Generic;
using RFramework.Config;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 默认配置辅助器（占位实现）。
    /// 所有方法均抛出 <see cref="NotSupportedException"/>，
    /// 提示在 Expansion 层通过 Luban 等工具提供真实实现。
    /// </summary>
    public sealed class DefaultConfigHelper : ConfigHelperBase
    {
        /// <inheritdoc/>
        public override Type GetTableType(Type rowType)
        {
            throw new NotSupportedException(
                "DefaultConfigHelper: Config helper not set. "
                + "Please provide a real IConfigHelper implementation (e.g. LubanConfigHelper) "
                + "in the Expansion layer.");
        }

        /// <inheritdoc/>
        public override object ParseConfig(Type tableType, byte[] bytes)
        {
            throw new NotSupportedException(
                "DefaultConfigHelper: Config helper not set.");
        }

        /// <inheritdoc/>
        public override T GetConfig<T>(object parsedTable, int id)
        {
            throw new NotSupportedException(
                "DefaultConfigHelper: Config helper not set.");
        }

        /// <inheritdoc/>
        public override bool ContainsConfig(object parsedTable, int id)
        {
            throw new NotSupportedException(
                "DefaultConfigHelper: Config helper not set.");
        }

        /// <inheritdoc/>
        public override IReadOnlyList<T> GetAllConfigs<T>(object parsedTable)
        {
            throw new NotSupportedException(
                "DefaultConfigHelper: Config helper not set.");
        }

        /// <inheritdoc/>
        public override void ReleaseConfig(object parsedTable)
        {
            throw new NotSupportedException(
                "DefaultConfigHelper: Config helper not set.");
        }
    }
}
