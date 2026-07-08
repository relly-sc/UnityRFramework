using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using RFramework.Config;
using UnityEngine;
using RFramework;
namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 默认配置辅助器（基于 JSON + JsonUtility）。
    /// 配置行为约定：
    /// 1. 配置行类必须是 <c>[Serializable]</c>，且包含 <c>public int Id</c> 字段
    /// 2. JSON 格式为数组：<c>[{"Id":1,...},{"Id":2,...}]</c>
    /// 3. 生产项目建议切换为 Expansion 层的 LubanConfigHelper
    /// </summary>
    public sealed class DefaultConfigHelper : ConfigHelperBase
    {
        /// <inheritdoc/>
        public override Type GetTableType(Type rowType)
        {
            // JSON 模式下没有 Luban 的 TbItem 表类型，直接返回行类型自身
            return rowType;
        }

        /// <inheritdoc/>
        public override object ParseConfig(Type tableType, byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return null;
            }

            string json = Encoding.UTF8.GetString(bytes).Trim();
            return string.IsNullOrEmpty(json) ? null : ParseConfigFromString(tableType, json);
        }

        /// <inheritdoc/>
        public override object ParseConfigFromString(Type tableType, string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            // JsonUtility 不支持直接反序列化顶层数组，包裹一层 Items
            json = json.Trim();
            if (json.StartsWith("["))
            {
                json = "{\"Items\":" + json + "}";
            }

            // 创建泛型包装类型 ConfigArrayWrapper<TRow>
            Type wrapperType = typeof(ConfigArrayWrapper<>).MakeGenericType(tableType);
            object wrapper = Utility.Json.ToObject(wrapperType, json);
            System.Reflection.FieldInfo itemsField = wrapperType.GetField("Items");
            Array items = itemsField.GetValue(wrapper) as Array;

            if (items == null || items.Length == 0)
            {
                return null;
            }

            // 按 Id 索引构建字典
            Type dictType = typeof(Dictionary<,>).MakeGenericType(typeof(int), tableType);
            IDictionary dict = (IDictionary)Activator.CreateInstance(dictType);

            foreach (object item in items)
            {
                int id = GetRowId(tableType, item);
                dict[id] = item;
            }

            return dict;
        }

        /// <inheritdoc/>
        public override T GetConfig<T>(object parsedTable, int id)
        {
            if (parsedTable == null)
            {
                return null;
            }

            var dict = parsedTable as Dictionary<int, T>;
            if (dict == null)
            {
                Log.Warning("DefaultConfigHelper: config table type mismatch.");
                return null;
            }

            dict.TryGetValue(id, out T value);
            return value;
        }

        /// <inheritdoc/>
        public override bool ContainsConfig(object parsedTable, int id)
        {
            if (parsedTable == null)
            {
                return false;
            }

            Type valueType = parsedTable.GetType().GetGenericArguments()[1];
            Type dictType = typeof(Dictionary<,>).MakeGenericType(typeof(int), valueType);
            IDictionary dict = parsedTable as IDictionary;

            return dict != null && dict.Contains(id);
        }

        /// <inheritdoc/>
        public override IReadOnlyList<T> GetAllConfigs<T>(object parsedTable)
        {
            if (parsedTable == null)
            {
                return new List<T>().AsReadOnly();
            }

            var dict = parsedTable as Dictionary<int, T>;
            if (dict == null)
            {
                return new List<T>().AsReadOnly();
            }

            return new List<T>(dict.Values).AsReadOnly();
        }

        /// <inheritdoc/>
        public override void ReleaseConfig(object parsedTable)
        {
            if (parsedTable != null)
            {
                IDictionary dict = parsedTable as IDictionary;
                if (dict != null)
                {
                    dict.Clear();
                }
            }
        }

        /// <summary>
        /// 通过反射获取配置行的 Id 字段值。
        /// 先在类型上查找 public 实例字段，再尝试属性。
        /// </summary>
        private static int GetRowId(Type rowType, object item)
        {
            // 按约定查找名为 Id / id 的公开字段或属性
            System.Reflection.MemberInfo[] members = rowType.GetMember("Id",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            if (members.Length == 0)
            {
                members = rowType.GetMember("id",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            }

            if (members.Length == 0)
            {
                throw new Exception(
                    $"DefaultConfigHelper: config row type '{rowType.Name}' has no public 'Id' field or property. "
                    + "JSON config requires each row to have a public int Id.");
            }

            object idValue;
            if (members[0] is System.Reflection.FieldInfo fi)
            {
                idValue = fi.GetValue(item);
            }
            else if (members[0] is System.Reflection.PropertyInfo pi)
            {
                idValue = pi.GetValue(item);
            }
            else
            {
                throw new Exception(
                    $"DefaultConfigHelper: 'Id' member on '{rowType.Name}' is not a field or property.");
            }

            return Convert.ToInt32(idValue);
        }

        /// <summary>
        /// JsonUtility 数组反序列化包装类。
        /// 因为 JsonUtility 不支持顶层数组，需要包裹一层对象。
        /// </summary>
        [Serializable]
        private class ConfigArrayWrapper<T>
        {
            public T[] Items;
        }
    }
}
