using System;
using UnityEngine.Serialization;

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

        /// <summary>Config 统一输出根目录，内部自动分为 Json/Binary。</summary>
        [FormerlySerializedAs("ConfigBinaryDirectory")]
        public string ConfigOutputDirectory = "Assets/Resources/Config";

        /// <summary>Localization 统一输出根目录，内部自动分为 Json/Binary。</summary>
        [FormerlySerializedAs("LocalizationBinaryDirectory")]
        public string LocalizationOutputDirectory = "Assets/Resources/Localization";

        /// <summary>生成的 Config 行类型和 Codec 所使用的命名空间；留空时使用全局命名空间。</summary>
        public string GeneratedNamespace = "Game.Config";
    }
}
