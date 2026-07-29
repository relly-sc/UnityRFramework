using RFramework;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// ConfigPipeline 当前代码生成策略入口。
    /// </summary>
    public static class ConfigCodeGeneratorRegistry
    {
        private static readonly IConfigCodeGenerator DefaultGenerator =
            new DefaultConfigCodeGenerator();
        private static IConfigCodeGenerator current = DefaultGenerator;

        /// <summary>获取当前代码生成策略。</summary>
        public static IConfigCodeGenerator Current => current;

        /// <summary>替换当前代码生成策略。</summary>
        public static void Set(IConfigCodeGenerator generator)
        {
            current = generator
                ?? throw new RFrameworkException("Config code generator is invalid.");
        }

        /// <summary>恢复框架内置代码生成策略。</summary>
        public static void Reset()
        {
            current = DefaultGenerator;
        }
    }
}
