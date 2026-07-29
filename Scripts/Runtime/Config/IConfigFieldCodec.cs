using System;
using System.IO;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// ConfigPipeline 自定义标量字段的 CSV、JSON 与 URFC 编解码契约。
    /// </summary>
    public interface IConfigFieldCodec
    {
        /// <summary>获取 CSV 类型行使用的唯一关键字。</summary>
        string TypeKeyword { get; }

        /// <summary>获取该 Codec 对应的运行时值类型。</summary>
        Type ValueType { get; }

        /// <summary>获取生成配置行代码时使用的完整 C# 类型名称。</summary>
        string CSharpTypeName { get; }

        /// <summary>获取自定义字段二进制与 JSON 表示的版本，格式变化时必须递增。</summary>
        uint SchemaVersion { get; }

        /// <summary>将 CSV 单元格转换为运行时值。</summary>
        object ParseCsv(string value);

        /// <summary>将运行时值转换为 JSON 中保存的字符串。</summary>
        string FormatJson(object value);

        /// <summary>将 JSON 中保存的字符串转换为运行时值。</summary>
        object ParseJson(string value);

        /// <summary>将运行时值写入 URFC Body。</summary>
        void WriteBinary(BinaryWriter writer, object value);

        /// <summary>从 URFC Body 读取运行时值。</summary>
        object ReadBinary(BinaryReader reader);
    }
}
