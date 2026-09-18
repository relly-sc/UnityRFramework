using System;
using System.Collections.Generic;
using System.IO;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// ConfigPipeline 自定义标量字段 Codec 注册表。
    /// </summary>
    public static class ConfigFieldCodecRegistry
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, IConfigFieldCodec> CodecsByKeyword =
            new Dictionary<string, IConfigFieldCodec>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<Type, IConfigFieldCodec> CodecsByType =
            new Dictionary<Type, IConfigFieldCodec>();

        /// <summary>
        /// 注册或刷新一个自定义字段 Codec。相同关键字与类型必须始终保持一一对应。
        /// </summary>
        public static void Register(IConfigFieldCodec codec)
        {
            ValidateCodec(codec);
            lock (SyncRoot)
            {
                if (CodecsByKeyword.TryGetValue(codec.TypeKeyword, out IConfigFieldCodec keywordCodec)
                    && keywordCodec.ValueType != codec.ValueType)
                {
                    throw new RFrameworkException(
                        $"Config field codec keyword '{codec.TypeKeyword}' is already mapped to "
                        + $"'{keywordCodec.ValueType.FullName}'.");
                }

                if (CodecsByType.TryGetValue(codec.ValueType, out IConfigFieldCodec typeCodec)
                    && !string.Equals(typeCodec.TypeKeyword, codec.TypeKeyword,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new RFrameworkException(
                        $"Config field codec type '{codec.ValueType.FullName}' is already mapped to "
                        + $"keyword '{typeCodec.TypeKeyword}'.");
                }

                CodecsByKeyword[codec.TypeKeyword] = codec;
                CodecsByType[codec.ValueType] = codec;
            }
        }

        /// <summary>按 CSV 类型关键字查找自定义字段 Codec。</summary>
        public static bool TryGet(string typeKeyword, out IConfigFieldCodec codec)
        {
            if (string.IsNullOrWhiteSpace(typeKeyword))
            {
                codec = null;
                return false;
            }

            lock (SyncRoot)
            {
                return CodecsByKeyword.TryGetValue(typeKeyword.Trim(), out codec);
            }
        }

        /// <summary>按运行时值类型查找自定义字段 Codec。</summary>
        public static bool TryGet(Type valueType, out IConfigFieldCodec codec)
        {
            if (valueType == null)
            {
                codec = null;
                return false;
            }

            lock (SyncRoot)
            {
                return CodecsByType.TryGetValue(valueType, out codec);
            }
        }

        /// <summary>移除指定类型关键字对应的 Codec，主要用于编辑器测试和扩展重载。</summary>
        public static void Unregister(string typeKeyword)
        {
            if (string.IsNullOrWhiteSpace(typeKeyword))
            {
                return;
            }

            lock (SyncRoot)
            {
                if (!CodecsByKeyword.TryGetValue(typeKeyword.Trim(), out IConfigFieldCodec codec))
                {
                    return;
                }

                CodecsByKeyword.Remove(codec.TypeKeyword);
                CodecsByType.Remove(codec.ValueType);
            }
        }

        /// <summary>由生成的整表 Codec 读取一个自定义字段值。</summary>
        /// <typeparam name="T">生成配置行声明的字段类型。</typeparam>
        /// <param name="typeKeyword">自定义字段类型关键字。</param>
        /// <param name="schemaVersion">生成代码记录的字段 Codec 版本。</param>
        /// <param name="reader">已定位到当前字段的 URFC Body 读取器。</param>
        /// <returns>经过类型和版本校验的自定义字段值。</returns>
        public static T ReadBinary<T>(
            string typeKeyword, uint schemaVersion, BinaryReader reader)
        {
            if (reader == null)
            {
                throw new RFrameworkException("Binary config reader is invalid.");
            }

            IConfigFieldCodec codec = GetRequired(typeKeyword);
            if (codec.SchemaVersion != schemaVersion)
            {
                throw new RFrameworkException(
                    $"Config field codec '{typeKeyword}' schema version mismatch. "
                    + $"Expected '{schemaVersion}', registered '{codec.SchemaVersion}'.");
            }

            if (codec.ValueType != typeof(T))
            {
                throw new RFrameworkException(
                    $"Config field codec '{typeKeyword}' returns '{codec.ValueType.FullName}', "
                    + $"but generated code requires '{typeof(T).FullName}'.");
            }

            object value = codec.ReadBinary(reader);
            ValidateValue(codec, value, "binary reader");
            return (T)value;
        }

        internal static IConfigFieldCodec GetRequired(string typeKeyword)
        {
            if (!TryGet(typeKeyword, out IConfigFieldCodec codec))
            {
                throw new RFrameworkException(
                    $"Config field codec '{typeKeyword}' is not registered.");
            }

            return codec;
        }

        internal static void ValidateValue(
            IConfigFieldCodec codec, object value, string operation)
        {
            if (value == null)
            {
                if (!codec.ValueType.IsValueType
                    || Nullable.GetUnderlyingType(codec.ValueType) != null)
                {
                    return;
                }

                throw new RFrameworkException(
                    $"Config field codec '{codec.TypeKeyword}' {operation} returned null for "
                    + $"value type '{codec.ValueType.FullName}'.");
            }

            if (!codec.ValueType.IsInstanceOfType(value))
            {
                throw new RFrameworkException(
                    $"Config field codec '{codec.TypeKeyword}' {operation} returned "
                    + $"'{value.GetType().FullName}', expected '{codec.ValueType.FullName}'.");
            }
        }

        private static void ValidateCodec(IConfigFieldCodec codec)
        {
            if (codec == null || string.IsNullOrWhiteSpace(codec.TypeKeyword)
                || codec.ValueType == null || string.IsNullOrWhiteSpace(codec.CSharpTypeName))
            {
                throw new RFrameworkException("Config field codec metadata is invalid.");
            }

            if (IsBuiltInType(codec.ValueType) || codec.ValueType.IsEnum
                || codec.ValueType.IsArray || codec.ValueType.IsGenericType)
            {
                throw new RFrameworkException(
                    $"Config field codec '{codec.TypeKeyword}' must target a custom scalar type, "
                    + $"not '{codec.ValueType.FullName}'.");
            }

            if (!codec.ValueType.IsVisible)
            {
                throw new RFrameworkException(
                    $"Config field codec value type must be public: "
                    + $"'{codec.ValueType.FullName}'.");
            }

            if (codec.SchemaVersion == 0u)
            {
                throw new RFrameworkException(
                    $"Config field codec '{codec.TypeKeyword}' schema version must be greater "
                    + "than zero.");
            }

            if (!string.Equals(codec.TypeKeyword, codec.TypeKeyword.Trim(), StringComparison.Ordinal))
            {
                throw new RFrameworkException(
                    $"Config field codec keyword contains surrounding whitespace: "
                    + $"'{codec.TypeKeyword}'.");
            }

            if (!IsValidTypeKeyword(codec.TypeKeyword))
            {
                throw new RFrameworkException(
                    $"Config field codec keyword is invalid: '{codec.TypeKeyword}'.");
            }

            if (!IsValidCSharpTypeName(codec.CSharpTypeName))
            {
                throw new RFrameworkException(
                    $"Config field codec C# type name is invalid: '{codec.CSharpTypeName}'.");
            }

            string declaredTypeName = codec.CSharpTypeName.StartsWith(
                "global::", StringComparison.Ordinal)
                ? codec.CSharpTypeName.Substring("global::".Length)
                : codec.CSharpTypeName;
            string actualTypeName = codec.ValueType.FullName?.Replace('+', '.');
            if (!string.Equals(declaredTypeName, actualTypeName, StringComparison.Ordinal))
            {
                throw new RFrameworkException(
                    $"Config field codec C# type name '{codec.CSharpTypeName}' does not match "
                    + $"value type '{actualTypeName}'.");
            }
        }

        private static bool IsBuiltInType(Type type)
        {
            return type == typeof(bool) || type == typeof(byte) || type == typeof(sbyte)
                || type == typeof(short) || type == typeof(ushort) || type == typeof(int)
                || type == typeof(uint) || type == typeof(long) || type == typeof(ulong)
                || type == typeof(float) || type == typeof(double) || type == typeof(decimal)
                || type == typeof(char) || type == typeof(string);
        }

        private static bool IsValidTypeKeyword(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                bool valid = character == '_'
                    || character >= 'A' && character <= 'Z'
                    || character >= 'a' && character <= 'z'
                    || i > 0 && (character == '-' || character == '.'
                        || character >= '0' && character <= '9');
                if (!valid)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidCSharpTypeName(string value)
        {
            string typeName = value.StartsWith("global::", StringComparison.Ordinal)
                ? value.Substring("global::".Length)
                : value;
            string[] parts = typeName.Split('.');
            for (int partIndex = 0; partIndex < parts.Length; partIndex++)
            {
                string part = parts[partIndex];
                if (string.IsNullOrEmpty(part)
                    || !(part[0] == '_' || part[0] >= 'A' && part[0] <= 'Z'
                        || part[0] >= 'a' && part[0] <= 'z'))
                {
                    return false;
                }

                for (int i = 1; i < part.Length; i++)
                {
                    char character = part[i];
                    if (!(character == '_' || character >= 'A' && character <= 'Z'
                        || character >= 'a' && character <= 'z'
                        || character >= '0' && character <= '9'))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            lock (SyncRoot)
            {
                CodecsByKeyword.Clear();
                CodecsByType.Clear();
            }
        }
    }
}
