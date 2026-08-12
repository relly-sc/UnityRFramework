namespace UnityRFramework.Editor
{
    /// <summary>
    /// Config CSV 第一版支持的字段类型。
    /// </summary>
    public enum ConfigFieldKind
    {
        /// <summary>布尔值。</summary>
        Boolean,
        /// <summary>8 位无符号整数。</summary>
        Byte,
        /// <summary>8 位有符号整数。</summary>
        SByte,
        /// <summary>16 位有符号整数。</summary>
        Int16,
        /// <summary>16 位无符号整数。</summary>
        UInt16,
        /// <summary>32 位有符号整数。</summary>
        Int32,
        /// <summary>32 位无符号整数。</summary>
        UInt32,
        /// <summary>64 位有符号整数。</summary>
        Int64,
        /// <summary>64 位无符号整数。</summary>
        UInt64,
        /// <summary>32 位浮点数。</summary>
        Single,
        /// <summary>64 位浮点数。</summary>
        Double,
        /// <summary>十进制定点数。</summary>
        Decimal,
        /// <summary>UTF-16 字符。</summary>
        Char,
        /// <summary>UTF-8 编码字符串。</summary>
        String,
        /// <summary>由当前配置表生成的 Int32 枚举。</summary>
        Enum,
        /// <summary>一维数组。</summary>
        Array,
        /// <summary>List 集合。</summary>
        List,
        /// <summary>由项目注册的自定义标量 Codec。</summary>
        Custom
    }
}
