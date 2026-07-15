using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using RFramework;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 使用 Dictionary&lt;int, TRow&gt; 存储配置行的 Helper 基类。
    /// 内置 JSON 字符串解析和通用查询；二进制等格式只需实现字节解析入口。
    /// 使用自定义表对象的二进制 Helper 应直接继承 ConfigHelperBase。
    /// </summary>
    public abstract class DictionaryConfigHelperBase : ConfigHelperBase
    {
        /// <inheritdoc/>
        public override Type GetTableType(Type rowType)
        {
            ValidateRowType(rowType);
            return rowType;
        }

        /// <inheritdoc/>
        public override object ParseConfigFromString(Type tableType, string json)
        {
            ValidateRowType(tableType);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new RFrameworkException("DictionaryConfigHelperBase: JSON content is empty.");
            }

            return ParseJsonToIndexedTable(tableType, json);
        }

        /// <inheritdoc/>
        public override T GetConfig<T>(object parsedTable, int id)
        {
            if (parsedTable is Dictionary<int, T> dict)
            {
                dict.TryGetValue(id, out T value);
                return value;
            }

            if (parsedTable != null)
            {
                Log.Warning("Config table type mismatch. Expected Dictionary<int, {0}>.", typeof(T).Name);
            }

            return null;
        }

        /// <inheritdoc/>
        public override bool ContainsConfig(object parsedTable, int id)
        {
            return parsedTable is IDictionary dict && dict.Contains(id);
        }

        /// <inheritdoc/>
        public override IReadOnlyList<T> GetAllConfigs<T>(object parsedTable)
        {
            if (parsedTable is Dictionary<int, T> dict)
            {
                return new List<T>(dict.Values).AsReadOnly();
            }

            return Array.Empty<T>();
        }

        /// <inheritdoc/>
        public override void ReleaseConfig(object parsedTable)
        {
            if (parsedTable is IDictionary dict)
            {
                dict.Clear();
            }
        }

        /// <summary>
        /// 将配置行集合按公开 Id/id 字段或属性建立索引。
        /// </summary>
        protected static object BuildIndexedTable(Type rowType, IEnumerable<object> rows)
        {
            Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(int), rowType);
            IDictionary dictionary = (IDictionary)Activator.CreateInstance(dictionaryType);

            foreach (object row in rows)
            {
                if (row == null)
                {
                    throw new RFrameworkException(
                        $"Config table '{rowType.Name}' contains a null row.");
                }

                int id = GetRowId(rowType, row);
                if (dictionary.Contains(id))
                {
                    throw new RFrameworkException(
                        $"Config table '{rowType.Name}' contains duplicate Id '{id}'.");
                }

                dictionary.Add(id, row);
            }

            return dictionary;
        }

        /// <summary>
        /// 校验默认字典型 Helper 支持的配置行类型。
        /// </summary>
        protected static void ValidateRowType(Type rowType)
        {
            if (rowType == null)
            {
                throw new RFrameworkException("Config row type is invalid.");
            }

            if (!rowType.IsClass || rowType.IsAbstract)
            {
                throw new RFrameworkException(
                    $"Config row type '{rowType.Name}' must be a non-abstract class.");
            }
        }

        /// <summary>
        /// 将 JSON 数组或带 Items 字段的 JSON 对象解析为默认字典表。
        /// </summary>
        protected static object ParseJsonToIndexedTable(Type rowType, string json)
        {
            try
            {
                return BuildIndexedTable(rowType, ConfigJsonReader.ParseRows(rowType, json));
            }
            catch (Exception ex)
            {
                if (ex is RFrameworkException)
                {
                    throw;
                }

                throw new RFrameworkException(
                    $"JSON config for '{rowType.Name}' could not be parsed.", ex);
            }
        }

        private static int GetRowId(Type rowType, object item)
        {
            MemberInfo[] members = rowType.GetMember(
                "Id", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (members.Length == 0)
            {
                throw new RFrameworkException(
                    $"Config row type '{rowType.Name}' has no public Id field or property.");
            }

            object idValue;
            if (members[0] is FieldInfo field)
            {
                idValue = field.GetValue(item);
            }
            else if (members[0] is PropertyInfo property && property.GetMethod != null)
            {
                idValue = property.GetValue(item);
            }
            else
            {
                throw new RFrameworkException(
                    $"Id member on config row type '{rowType.Name}' is not readable.");
            }

            try
            {
                return Convert.ToInt32(idValue, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                throw new RFrameworkException(
                    $"Id member on config row type '{rowType.Name}' must be convertible to Int32.", ex);
            }
        }

    }
}
