namespace UnityRFramework.Editor
{
    /// <summary>
    /// ConfigPipeline 配置行和整表 Codec 代码生成策略。
    /// </summary>
    public interface IConfigCodeGenerator
    {
        /// <summary>为一张已经校验的配置表生成完整 C# 源代码。</summary>
        string Generate(ConfigTableSchema schema);
    }
}
