using System.Collections.Generic;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 框架单语言二进制协议解析器（URFL v2，兼容读取 v1）。
    /// JSON 字符串入口由 DictionaryLocalizationHelperBase 保留。
    /// </summary>
    public sealed class BinaryLocalizationHelper : DictionaryLocalizationHelperBase,
        RFramework.ILocalizationBundleHelper
    {
        /// <inheritdoc/>
        public override Dictionary<string, string> ParseLanguage(string language, byte[] bytes)
        {
            return BinaryTableUtility.ReadLocalization(language, bytes);
        }

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, Dictionary<string, string>> ParseLanguageBundle(
            byte[] bytes)
        {
            return BinaryTableUtility.ReadLocalizationBundle(bytes);
        }
    }
}
