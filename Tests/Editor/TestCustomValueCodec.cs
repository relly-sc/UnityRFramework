using System;
using System.Globalization;
using System.IO;
using RFramework;
using UnityRFramework.Runtime;

namespace UnityRFramework.Editor.Tests
{
    /// <summary>
    /// 自定义 point2 标量的 CSV、JSON 与 URFC 测试 Codec。
    /// </summary>
    internal sealed class TestCustomValueCodec : IConfigFieldCodec
    {
        /// <inheritdoc/>
        public string TypeKeyword => "point2";

        /// <inheritdoc/>
        public Type ValueType => typeof(TestCustomValue);

        /// <inheritdoc/>
        public string CSharpTypeName =>
            "UnityRFramework.Editor.Tests.TestCustomValue";

        /// <inheritdoc/>
        public uint SchemaVersion => 1u;

        /// <inheritdoc/>
        public object ParseCsv(string value)
        {
            return Parse(value);
        }

        /// <inheritdoc/>
        public string FormatJson(object value)
        {
            TestCustomValue point = RequireValue(value);
            return point.X.ToString(CultureInfo.InvariantCulture) + ":"
                + point.Y.ToString(CultureInfo.InvariantCulture);
        }

        /// <inheritdoc/>
        public object ParseJson(string value)
        {
            return Parse(value);
        }

        /// <inheritdoc/>
        public void WriteBinary(BinaryWriter writer, object value)
        {
            if (writer == null)
            {
                throw new RFrameworkException("Test custom value writer is invalid.");
            }

            TestCustomValue point = RequireValue(value);
            writer.Write(point.X);
            writer.Write(point.Y);
        }

        /// <inheritdoc/>
        public object ReadBinary(BinaryReader reader)
        {
            if (reader == null)
            {
                throw new RFrameworkException("Test custom value reader is invalid.");
            }

            return new TestCustomValue { X = reader.ReadInt32(), Y = reader.ReadInt32() };
        }

        private static TestCustomValue Parse(string value)
        {
            string[] parts = (value ?? string.Empty).Split(':');
            if (parts.Length != 2
                || !int.TryParse(parts[0], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int x)
                || !int.TryParse(parts[1], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int y))
            {
                throw new RFrameworkException(
                    $"Test custom point value '{value}' must use X:Y.");
            }

            return new TestCustomValue { X = x, Y = y };
        }

        private static TestCustomValue RequireValue(object value)
        {
            if (!(value is TestCustomValue point))
            {
                throw new RFrameworkException("Test custom point value has an invalid type.");
            }

            return point;
        }
    }
}
