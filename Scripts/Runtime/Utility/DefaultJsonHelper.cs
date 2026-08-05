
using RFramework;
using System;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 使用 Unity JsonUtility 的零第三方 JSON Helper。
    /// 仅支持 JsonUtility 可序列化的字段和对象结构。
    /// </summary>
    public sealed class DefaultJsonHelper : Utility.Json.IJsonHelper
    {
        /// <summary>
        /// 将对象序列化为 JSON 字符串。
        /// </summary>
        /// <param name="value">要序列化的对象。</param>
        /// <returns>序列化后的 JSON 字符串。</returns>
        public string ToJson(object value)
        {
            return JsonUtility.ToJson(value, false);
        }

        /// <summary>
        /// 将 JSON 字符串反序列化为对象。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="json">要反序列化的 JSON 字符串。</param>
        /// <returns>反序列化后的对象。</returns>
        public T ToObject<T>(string json)
        {
            return (T)ToObject(typeof(T), json);
        }

        /// <summary>
        /// 将 JSON 字符串反序列化为对象。
        /// </summary>
        /// <param name="targetType">对象类型。</param>
        /// <param name="json">要反序列化的 JSON 字符串。</param>
        /// <returns>反序列化后的对象。</returns>
        public object ToObject(Type targetType, string json)
        {
            if (targetType == null)
            {
                throw new RFrameworkException("JSON target type cannot be null.");
            }

            return JsonUtility.FromJson(json, targetType);
        }
    }
}
