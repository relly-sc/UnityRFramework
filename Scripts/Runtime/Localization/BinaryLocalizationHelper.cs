using System.Collections.Generic;
using RFramework;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 框架单语言二进制协议解析器（URFL v2，兼容读取 v1）。
    /// JSON 字符串入口由 DictionaryLocalizationHelperBase 保留。
    /// </summary>
    public sealed class BinaryLocalizationHelper : DictionaryLocalizationHelperBase,
        ILocalizationBundleHelper, ILocalizationLocationProvider
    {
        /// <inheritdoc/>
        public string GetLanguageLocation(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                throw new RFrameworkException(
                    "BinaryLocalizationHelper: language code is invalid.");
            }

            return $"Localization/Binary/{language.Trim()}.bytes";
        }

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
