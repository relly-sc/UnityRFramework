using System;
using System.Collections.Generic;
using System.IO;
using RFramework;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 生成的 URFC v2 Codec 使用的一维集合读取工具。
    /// </summary>
    public static class BinaryConfigCollectionUtility
    {
        private const int MaxElementCount = 1000000;

        /// <summary>读取带 Int32 数量前缀的一维数组。</summary>
        /// <param name="reader">已定位到集合数量字段的读取器。</param>
        /// <param name="readElement">读取单个元素的函数。</param>
        /// <typeparam name="T">元素类型。</typeparam>
        /// <returns>读取完成的一维数组。</returns>
        public static T[] ReadArray<T>(
            BinaryReader reader, Func<BinaryReader, T> readElement)
        {
            ValidateArguments(reader, readElement);
            int count = ReadCount(reader);
            T[] result = new T[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = readElement(reader);
            }

            return result;
        }

        /// <summary>读取带 Int32 数量前缀的 List。</summary>
        /// <param name="reader">已定位到集合数量字段的读取器。</param>
        /// <param name="readElement">读取单个元素的函数。</param>
        /// <typeparam name="T">元素类型。</typeparam>
        /// <returns>读取完成的 List。</returns>
        public static List<T> ReadList<T>(
            BinaryReader reader, Func<BinaryReader, T> readElement)
        {
            ValidateArguments(reader, readElement);
            int count = ReadCount(reader);
            List<T> result = new List<T>(count);
            for (int i = 0; i < count; i++)
            {
                result.Add(readElement(reader));
            }

            return result;
        }

        private static int ReadCount(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > MaxElementCount)
            {
                throw new RFrameworkException(
                    $"Binary config collection count '{count}' is invalid.");
            }

            return count;
        }

        private static void ValidateArguments<T>(
            BinaryReader reader, Func<BinaryReader, T> readElement)
        {
            if (reader == null || reader.BaseStream == null || !reader.BaseStream.CanRead
                || readElement == null)
            {
                throw new RFrameworkException("Binary config collection reader is invalid.");
            }
        }
    }
}
