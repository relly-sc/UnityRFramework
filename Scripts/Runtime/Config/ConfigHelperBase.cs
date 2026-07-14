using System;
using System.Collections.Generic;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 配置辅助器基类。
    /// 继承自 MonoBehaviour 并实现 IConfigHelper 接口，使 Runtime 层配置辅助器
    /// 可以通过 <see cref="Helper.CreateHelper{T}(string, T)"/> 统一创建为场景中的 GameObject，
    /// 同时仍能以接口形式注入到 Library 层的 ConfigModule。
    /// </summary>
    public abstract class ConfigHelperBase : MonoBehaviour, IConfigHelper
    {
        /// <summary>
        /// 获取配置行类型对应的表类型。
        /// </summary>
        /// <param name="rowType">配置行类型（如 typeof(ItemConfig)）。</param>
        /// <returns>对应的表类型（如 typeof(TbItem)）。</returns>
        public abstract Type GetTableType(Type rowType);

        /// <summary>
        /// 解析配置原始字节为强类型配置表对象。
        /// 字节格式由实现方决定，与 JSON 字符串入口相互独立。
        /// </summary>
        /// <param name="tableType">表类型（由 GetTableType 返回）。</param>
        /// <param name="bytes">原始字节数据。</param>
        /// <returns>解析后的配置表对象。</returns>
        public abstract object ParseConfig(Type tableType, byte[] bytes);

        /// <summary>
        /// 从 JSON 字符串解析为强类型配置表对象。
        /// 实现方可独立提供 JSON 支持，不要求字节入口也使用 JSON。
        /// 默认实现明确报告不支持；只处理二进制/自定义格式的 Helper 无需实现此入口。
        /// </summary>
        /// <param name="tableType">表类型（由 GetTableType 返回）。</param>
        /// <param name="json">JSON 字符串。</param>
        /// <returns>解析后的配置表对象。</returns>
        public virtual object ParseConfigFromString(Type tableType, string json)
        {
            throw new RFrameworkException(
                $"Config helper '{GetType().Name}' does not support JSON string parsing.");
        }

        /// <summary>
        /// 从已解析的配置表中获取指定 ID 的单条配置行。
        /// </summary>
        /// <typeparam name="T">配置行类型。</typeparam>
        /// <param name="parsedTable">ParseConfig 返回的配置表对象。</param>
        /// <param name="id">配置行 ID。</param>
        /// <returns>配置行实例，不存在时返回 null。</returns>
        public abstract T GetConfig<T>(object parsedTable, int id) where T : class;

        /// <summary>
        /// 检查已解析的配置表中是否包含指定 ID。
        /// </summary>
        /// <param name="parsedTable">ParseConfig 返回的配置表对象。</param>
        /// <param name="id">配置行 ID。</param>
        /// <returns>存在返回 true，否则返回 false。</returns>
        public abstract bool ContainsConfig(object parsedTable, int id);

        /// <summary>
        /// 获取已解析配置表中的所有配置行。
        /// </summary>
        /// <typeparam name="T">配置行类型。</typeparam>
        /// <param name="parsedTable">ParseConfig 返回的配置表对象。</param>
        /// <returns>配置行只读列表。</returns>
        public abstract IReadOnlyList<T> GetAllConfigs<T>(object parsedTable) where T : class;

        /// <summary>
        /// 释放已解析的配置表。
        /// </summary>
        /// <param name="parsedTable">ParseConfig 返回的配置表对象。</param>
        public abstract void ReleaseConfig(object parsedTable);
    }
}
