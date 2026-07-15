using System;
using System.Collections.Generic;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// Config JSON 和二进制 Helper 共用的当前 Schema 注册表。
    /// </summary>
    public static class ConfigSchemaRegistry
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<Type, ConfigSchemaInfo> Schemas =
            new Dictionary<Type, ConfigSchemaInfo>();

        /// <summary>注册或替换指定配置行类型的当前 Schema。</summary>
        public static void Register(Type rowType, uint tableId, ulong schemaHash)
        {
            if (rowType == null || tableId == 0 || schemaHash == 0)
            {
                throw new RFrameworkException("Config schema registration is invalid.");
            }

            lock (SyncRoot)
            {
                Schemas[rowType] = new ConfigSchemaInfo(rowType, tableId, schemaHash);
            }
        }

        /// <summary>获取指定配置行类型的当前 Schema。</summary>
        public static bool TryGet(Type rowType, out ConfigSchemaInfo schema)
        {
            if (rowType == null)
            {
                schema = default;
                return false;
            }

            lock (SyncRoot)
            {
                return Schemas.TryGetValue(rowType, out schema);
            }
        }

        /// <summary>移除指定配置行类型的当前 Schema。</summary>
        public static void Unregister(Type rowType)
        {
            if (rowType == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                Schemas.Remove(rowType);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            lock (SyncRoot)
            {
                Schemas.Clear();
            }
        }
    }
}
