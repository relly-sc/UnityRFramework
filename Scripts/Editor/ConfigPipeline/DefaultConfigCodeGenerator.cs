namespace UnityRFramework.Editor
{
    /// <summary>
    /// 使用框架内置 ConfigCodeGenerator 的默认代码生成策略。
    /// </summary>
    public sealed class DefaultConfigCodeGenerator : IConfigCodeGenerator
    {
        /// <inheritdoc/>
        public string Generate(ConfigTableSchema schema)
        {
            return ConfigCodeGenerator.Generate(schema);
        }
    }
}
