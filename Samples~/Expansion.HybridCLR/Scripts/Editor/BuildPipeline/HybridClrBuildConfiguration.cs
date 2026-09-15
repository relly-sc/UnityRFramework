using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// HybridCLR 构建步骤私有配置资产。
    /// </summary>
    public sealed class HybridClrBuildConfiguration : ScriptableObject
    {
        [Tooltip("热更产物输出根目录。")]
        public string OutputAssetRoot = "Assets/GameAssets/HotUpdate";

        [Tooltip("必填。热更新程序集中实现 IHotUpdateEntry 接口的入口类型全名，格式为命名空间+类名，例如 UnityRFramework.Sample.HotUpdateEntry。")]
        public string EntryTypeName = string.Empty;

        [Tooltip("业务代码版本号；留空时自动生成。")]
        public string CodeVersion = string.Empty;

        [Tooltip("是否包含 Portable PDB。")]
        public bool IncludePdb;

    }
}
