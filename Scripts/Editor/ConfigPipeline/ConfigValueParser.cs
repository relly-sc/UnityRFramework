using System;
using System.Collections.Generic;
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
        private const int MaxCollectionElementCount = 1000000;

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
                ParseValue(field, value);
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
                parsed = ParseValue(field, value);
            }
            catch (Exception ex)
            {
                throw new RFrameworkException(
                    $"Data '{sourcePath}', line {lineNumber}, field '{field.Name}' could not be written.", ex);
            }

            if (field.Kind == ConfigFieldKind.Array || field.Kind == ConfigFieldKind.List)
            {
                IReadOnlyList<object> elements = (IReadOnlyList<object>)parsed;
                writer.Write(elements.Count);
                for (int i = 0; i < elements.Count; i++)
                {
                    WriteScalar(writer, field.ElementKind, elements[i]);
                }

                return;
            }

            if (field.Kind == ConfigFieldKind.Enum)
            {
                writer.Write((int)parsed);
                return;
            }

            WriteScalar(writer, field.Kind, parsed);
        }

        /// <summary>
        /// 将一个已经过 Schema 校验的 CSV 字段转换为对应的 CLR 值。
        /// </summary>
        public static object ParseValue(ConfigFieldSchema field, string value)
        {
            if (field == null)
            {
                throw new RFrameworkException("Config field schema is invalid.");
            }

            if (field.Kind == ConfigFieldKind.Enum)
            {
                return ParseEnum(field, value);
            }

            if (field.Kind == ConfigFieldKind.Array || field.Kind == ConfigFieldKind.List)
            {
                List<string> tokens = SplitCollection(value);
                if (tokens.Count > MaxCollectionElementCount)
                {
                    throw new RFrameworkException(
                        $"Collection field '{field.Name}' exceeds {MaxCollectionElementCount} elements.");
                }

                List<object> result = new List<object>(tokens.Count);
                for (int i = 0; i < tokens.Count; i++)
                {
                    result.Add(ParseScalar(field.ElementKind, tokens[i], false));
                }

                return result;
            }

            return ParseScalar(field.Kind, value);
        }

        private static object ParseScalar(
            ConfigFieldKind kind, string value, bool decodeStringEscapes = true)
        {
            string trimmed = value?.Trim() ?? string.Empty;
            switch (kind)
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
                {
                    string characterValue = decodeStringEscapes
                        ? DecodeStringEscapes(value)
                        : value;
                    if (characterValue == null || characterValue.Length != 1)
                    {
                        throw new FormatException();
                    }

                    return characterValue[0];
                }
                case ConfigFieldKind.String:
                    return decodeStringEscapes
                        ? DecodeStringEscapes(value)
                        : value ?? string.Empty;
                default:
                    throw new RFrameworkException($"Unsupported scalar field kind '{kind}'.");
            }
        }

        private static int ParseEnum(ConfigFieldSchema field, string value)
        {
            string token = value?.Trim() ?? string.Empty;
            IReadOnlyList<ConfigEnumValueSchema> values = field.EnumValues;
            if (values == null || values.Count == 0)
            {
                throw new RFrameworkException(
                    $"Enum field '{field.Name}' has no declared members.");
            }

            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i].Name, token, StringComparison.OrdinalIgnoreCase))
                {
                    return values[i].Value;
                }
            }

            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out int numericValue))
            {
                for (int i = 0; i < values.Count; i++)
                {
                    if (values[i].Value == numericValue)
                    {
                        return numericValue;
                    }
                }
            }

            throw new RFrameworkException(
                $"Value '{value}' is not declared by enum field '{field.Name}'.");
        }

        private static List<string> SplitCollection(string value)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(value))
            {
                return result;
            }

            System.Text.StringBuilder token = new System.Text.StringBuilder();
            bool escaping = false;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (escaping)
                {
                    switch (character)
                    {
                        case '|': token.Append('|'); break;
                        case '\\': token.Append('\\'); break;
                        case 'n': token.Append('\n'); break;
                        case 'r': token.Append('\r'); break;
                        case 't': token.Append('\t'); break;
                        case 'e': break;
                        default:
                            throw new FormatException(
                                $"Unsupported collection escape sequence '\\{character}'.");
                    }
                    escaping = false;
                }
                else if (character == '\\')
                {
                    escaping = true;
                }
                else if (character == '|')
                {
                    result.Add(token.ToString());
                    token.Length = 0;
                }
                else
                {
                    token.Append(character);
                }
            }

            if (escaping)
            {
                throw new FormatException("Collection value ends with an incomplete escape sequence.");
            }

            result.Add(token.ToString());
            return result;
        }

        private static string DecodeStringEscapes(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf('\\') < 0)
            {
                return value ?? string.Empty;
            }

            System.Text.StringBuilder result = new System.Text.StringBuilder(value.Length);
            bool escaping = false;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (!escaping)
                {
                    if (character == '\\')
                    {
                        escaping = true;
                    }
                    else
                    {
                        result.Append(character);
                    }

                    continue;
                }

                switch (character)
                {
                    case 'n': result.Append('\n'); break;
                    case 'r': result.Append('\r'); break;
                    case 't': result.Append('\t'); break;
                    case '\\': result.Append('\\'); break;
                    case '|': result.Append('|'); break;
                    default:
                        throw new FormatException(
                            $"Unsupported string escape sequence '\\{character}'.");
                }

                escaping = false;
            }

            if (escaping)
            {
                throw new FormatException("String value ends with an incomplete escape sequence.");
            }

            return result.ToString();
        }

        private static void WriteScalar(
            BinaryWriter writer, ConfigFieldKind kind, object parsed)
        {
            switch (kind)
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
                    throw new RFrameworkException($"Unsupported scalar field kind '{kind}'.");
            }
        }
    }
}
