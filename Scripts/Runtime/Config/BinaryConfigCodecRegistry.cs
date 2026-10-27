using System;
using System.Collections.Generic;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// URFC v2 Codec 注册表。生成代码在编辑器域加载和运行前自动注册。
    /// </summary>
    public static class BinaryConfigCodecRegistry
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<Type, IBinaryConfigCodec> Codecs =
            new Dictionary<Type, IBinaryConfigCodec>();

        /// <summary>
        /// 注册或替换指定配置行类型的 URFC v2 Codec。
        /// </summary>
        /// <param name="codec">生成的配置表 Codec。</param>
        public static void Register(IBinaryConfigCodec codec)
        {
            if (codec == null || codec.RowType == null)
            {
                throw new RFrameworkException("Binary config codec or row type is invalid.");
            }

            lock (SyncRoot)
            {
                Codecs[codec.RowType] = codec;
            }
        }

        /// <summary>
        /// 获取指定配置行类型的 URFC v2 Codec。
        /// </summary>
        /// <param name="rowType">配置行类型。</param>
        /// <param name="codec">找到的 Codec。</param>
        /// <returns>存在对应 Codec 时返回 true。</returns>
        public static bool TryGet(Type rowType, out IBinaryConfigCodec codec)
        {
            if (rowType == null)
            {
                codec = null;
                return false;
            }

            lock (SyncRoot)
            {
                return Codecs.TryGetValue(rowType, out codec);
            }
        }

        /// <summary>
        /// 移除指定配置行类型的 Codec，主要用于编辑器测试和重新生成场景。
        /// </summary>
        /// <param name="rowType">配置行类型。</param>
        public static void Unregister(Type rowType)
        {
            if (rowType == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                Codecs.Remove(rowType);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            lock (SyncRoot)
            {
                Codecs.Clear();
            }
        }
    }
}
