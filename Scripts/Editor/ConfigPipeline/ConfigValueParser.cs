using System;
using System.Globalization;
using System.IO;
using RFramework;
using UnityRFramework.Runtime;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// Config CSV 字段值的类型校验和二进制写入器。
    /// </summary>
    public static class ConfigValueParser
    {
        /// <summary>
        /// 校验一个 CSV 字段值能否转换为声明类型。
        /// </summary>
        /// <param name="field">字段定义。</param>
        /// <param name="value">CSV 原始值。</param>
        /// <param name="sourcePath">源文件路径。</param>
        /// <param name="lineNumber">源文件行号。</param>
        public static void Validate(
            ConfigFieldSchema field, string value, string sourcePath, int lineNumber)
        {
            try
            {
                Parse(field, value);
            }
            catch (Exception ex) when (!(ex is RFrameworkException))
            {
                throw new RFrameworkException(
                    $"Data '{sourcePath}', line {lineNumber}, field '{field.Name}': "
                    + $"value '{value}' is not a valid {field.CSharpTypeName}.", ex);
            }
        }

        /// <summary>
        /// 按字段类型把 CSV 值写入 URFC v2 Body。
        /// </summary>
        /// <param name="writer">二进制写入器。</param>
        /// <param name="field">字段定义。</param>
        /// <param name="value">CSV 原始值。</param>
        /// <param name="sourcePath">源文件路径。</param>
        /// <param name="lineNumber">源文件行号。</param>
        public static void Write(
            BinaryWriter writer, ConfigFieldSchema field, string value, string sourcePath, int lineNumber)
        {
            object parsed;
            try
            {
                parsed = Parse(field, value);
            }
            catch (Exception ex)
            {
                throw new RFrameworkException(
                    $"Data '{sourcePath}', line {lineNumber}, field '{field.Name}' could not be written.", ex);
            }

            switch (field.Kind)
            {
                case ConfigFieldKind.Boolean: writer.Write((bool)parsed); break;
                case ConfigFieldKind.Byte: writer.Write((byte)parsed); break;
                case ConfigFieldKind.SByte: writer.Write((sbyte)parsed); break;
                case ConfigFieldKind.Int16: writer.Write((short)parsed); break;
                case ConfigFieldKind.UInt16: writer.Write((ushort)parsed); break;
                case ConfigFieldKind.Int32: writer.Write((int)parsed); break;
                case ConfigFieldKind.UInt32: writer.Write((uint)parsed); break;
                case ConfigFieldKind.Int64: writer.Write((long)parsed); break;
                case ConfigFieldKind.UInt64: writer.Write((ulong)parsed); break;
                case ConfigFieldKind.Single: writer.Write((float)parsed); break;
                case ConfigFieldKind.Double: writer.Write((double)parsed); break;
                case ConfigFieldKind.Decimal: writer.Write((decimal)parsed); break;
                case ConfigFieldKind.Char: writer.Write((ushort)(char)parsed); break;
                case ConfigFieldKind.String:
                    BinaryFormatUtility.WriteUtf8String(writer, (string)parsed, false);
                    break;
                default:
                    throw new RFrameworkException($"Unsupported field kind '{field.Kind}'.");
            }
        }

        private static object Parse(ConfigFieldSchema field, string value)
        {
            string trimmed = value?.Trim() ?? string.Empty;
            switch (field.Kind)
            {
                case ConfigFieldKind.Boolean:
                    if (string.Equals(trimmed, "1", StringComparison.Ordinal))
                    {
                        return true;
                    }

                    if (string.Equals(trimmed, "0", StringComparison.Ordinal))
                    {
                        return false;
                    }

                    return bool.Parse(trimmed);
                case ConfigFieldKind.Byte:
                    return byte.Parse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture);
                case ConfigFieldKind.SByte:
                    return sbyte.Parse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture);
                case ConfigFieldKind.Int16:
                    return short.Parse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture);
                case ConfigFieldKind.UInt16:
                    return ushort.Parse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture);
                case ConfigFieldKind.Int32:
                    return int.Parse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture);
                case ConfigFieldKind.UInt32:
                    return uint.Parse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture);
                case ConfigFieldKind.Int64:
                    return long.Parse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture);
                case ConfigFieldKind.UInt64:
                    return ulong.Parse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture);
                case ConfigFieldKind.Single:
                {
                    float result = float.Parse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture);
                    if (float.IsNaN(result) || float.IsInfinity(result))
                    {
                        throw new FormatException();
                    }

                    return result;
                }
                case ConfigFieldKind.Double:
                {
                    double result = double.Parse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture);
                    if (double.IsNaN(result) || double.IsInfinity(result))
                    {
                        throw new FormatException();
                    }

                    return result;
                }
                case ConfigFieldKind.Decimal:
                    return decimal.Parse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture);
                case ConfigFieldKind.Char:
                    if (value == null || value.Length != 1)
                    {
                        throw new FormatException();
                    }

                    return value[0];
                case ConfigFieldKind.String:
                    return value ?? string.Empty;
                default:
                    throw new RFrameworkException($"Unsupported field kind '{field.Kind}'.");
            }
        }
    }
}
