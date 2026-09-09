using System.Collections.Generic;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 一个语言 CSV 经过校验后的键值集合。
    /// </summary>
    public sealed class LocalizationTable
    {
        /// <summary>获取或设置源 CSV 路径。</summary>
        public string SourcePath { get; set; }

        /// <summary>获取或设置语言代码。</summary>
        public string Language { get; set; }

        /// <summary>获取或设置本地化键值集合。</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Entries { get; set; }
    }
}
