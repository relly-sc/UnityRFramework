using System;
using Newtonsoft.Json;
using RFramework;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 基于 Unity 官方 Newtonsoft Json 包的 JSON 辅助器。
    /// 支持属性、字段、集合、字典和顶层数组等 JsonUtility 不支持的常用 JSON 结构。
    /// </summary>
    [UnityEngine.Scripting.Preserve]
    public sealed class NewtonsoftJsonHelper : Utility.Json.IJsonHelper
    {
        private static readonly JsonSerializerSettings SerializerSettings =
            new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None
            };

        /// <summary>
        /// 将对象序列化为 JSON 字符串。
        /// </summary>
        /// <param name="obj">要序列化的对象。</param>
        /// <returns>序列化后的 JSON 字符串。</returns>
        public string ToJson(object obj)
        {
            return JsonConvert.SerializeObject(obj, SerializerSettings);
        }

        /// <summary>
        /// 将 JSON 字符串反序列化为指定类型的对象。
        /// </summary>
        /// <typeparam name="T">目标对象类型。</typeparam>
        /// <param name="json">要反序列化的 JSON 字符串。</param>
        /// <returns>反序列化后的对象。</returns>
        public T ToObject<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, SerializerSettings);
        }

        /// <summary>
        /// 将 JSON 字符串反序列化为指定运行时类型的对象。
        /// </summary>
        /// <param name="objectType">目标对象类型。</param>
        /// <param name="json">要反序列化的 JSON 字符串。</param>
        /// <returns>反序列化后的对象。</returns>
        public object ToObject(Type objectType, string json)
        {
            if (objectType == null)
            {
                throw new RFrameworkException("Object type is invalid.");
            }

            return JsonConvert.DeserializeObject(json, objectType, SerializerSettings);
        }
    }
}
