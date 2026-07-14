using System;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// Config 转换工具的输入和输出路径配置。
    /// </summary>
    [Serializable]
    public sealed class ConfigPipelineOptions
    {
        /// <summary>Config CSV 源目录。</summary>
        public string ConfigSourceDirectory = "Assets/ConfigSource/Config";

        /// <summary>Localization CSV 源目录。</summary>
        public string LocalizationSourceDirectory = "Assets/ConfigSource/Localization";

        /// <summary>Config 行类型和 Codec 代码输出目录。</summary>
        public string GeneratedCodeDirectory = "Assets/Generated/UnityRFramework/Config";

        /// <summary>URFC 二进制输出目录。</summary>
        public string ConfigBinaryDirectory = "Assets/Resources/Config";

        /// <summary>URFL 二进制输出目录。</summary>
        public string LocalizationBinaryDirectory = "Assets/Resources/Localization";

        /// <summary>生成的 Config 行类型和 Codec 所使用的命名空间。</summary>
        public string GeneratedNamespace = "Game.Config";
    }
}
