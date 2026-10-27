namespace UnityRFramework.Editor
{
    /// <summary>
    /// Config CSV 内联枚举的单个成员定义。
    /// </summary>
    public sealed class ConfigEnumValueSchema
    {
        /// <summary>获取或设置枚举成员名称。</summary>
        public string Name { get; set; }

        /// <summary>获取或设置枚举成员的 Int32 值。</summary>
        public int Value { get; set; }
    }
}
