using System.Collections.Generic;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 框架单语言二进制协议解析器（URFL v1）。
    /// JSON 字符串入口由 DictionaryLocalizationHelperBase 保留。
    /// </summary>
    public sealed class BinaryLocalizationHelper : DictionaryLocalizationHelperBase
    {
        /// <inheritdoc/>
        public override Dictionary<string, string> ParseLanguage(string language, byte[] bytes)
        {
            return BinaryTableUtility.ReadLocalization(language, bytes);
        }
    }
}
