namespace UnityRFramework.Editor
{
    /// <summary>
    /// Config 表中的单个字段定义。
    /// </summary>
    public sealed class ConfigFieldSchema
    {
        /// <summary>获取或设置字段名称。</summary>
        public string Name { get; set; }

        /// <summary>获取或设置 CSV 中的规范化类型关键字。</summary>
        public string TypeKeyword { get; set; }

        /// <summary>获取或设置生成代码使用的 C# 类型名称。</summary>
        public string CSharpTypeName { get; set; }

        /// <summary>获取或设置字段注释。</summary>
        public string Comment { get; set; }

        /// <summary>获取或设置字段类型。</summary>
        public ConfigFieldKind Kind { get; set; }
    }
}
