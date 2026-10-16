using System.Text;
using RFramework;
using UnityEngine.Scripting;

namespace UnityRFramework.Runtime
{
    /// <summary>使用框架当前 JSON Helper 的默认存档序列化器。</summary>
    [Preserve]
    public sealed class JsonStorageSerializer : IStorageSerializer
    {
        /// <inheritdoc />
        public byte[] Serialize<T>(T data)
        {
            return Encoding.UTF8.GetBytes(Utility.Json.ToJson(data));
        }

        /// <inheritdoc />
        public T Deserialize<T>(byte[] data)
        {
            return Utility.Json.ToObject<T>(Encoding.UTF8.GetString(data));
        }
    }
}
