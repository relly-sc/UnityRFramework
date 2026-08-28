using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// Obfuz 构建步骤私有配置资产。
    /// </summary>
    public sealed class ObfuzBuildConfiguration : ScriptableObject
    {
        [Tooltip("是否启用 Obfuz 混淆。")]
        public bool Enable = true;

        [Tooltip("逗号分隔的混淆 Pass 名称。")]
        public string EnabledPasses = "All";
    }
}
