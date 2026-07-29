using System;
using System.Collections.Generic;
using System.Text;
using RFramework;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// JSON 本地化解析器。每个文件只包含一种语言的 Key/Value 表。
    /// </summary>
    public sealed class JsonLocalizationHelper : DictionaryLocalizationHelperBase,
        ILocalizationBundleHelper, ILocalizationLocationProvider
    {
        /// <inheritdoc/>
        public string GetLanguageLocation(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                throw new RFrameworkException(
                    "JsonLocalizationHelper: language code is invalid.");
            }

            return $"Localization/Json/{language.Trim()}.json";
        }

        /// <inheritdoc/>
        public override Dictionary<string, string> ParseLanguage(string language, byte[] bytes)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                throw new RFrameworkException("JsonLocalizationHelper: language code is invalid.");
            }

            if (bytes == null || bytes.Length == 0)
            {
                throw new RFrameworkException(
                    $"JsonLocalizationHelper: JSON bytes for '{language}' are empty.");
            }

            string json = Encoding.UTF8.GetString(bytes);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new RFrameworkException(
                    $"JsonLocalizationHelper: decoded JSON for '{language}' is empty.");
            }

            return ParseJsonLanguage(language, json);
        }

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, Dictionary<string, string>> ParseLanguageBundle(
            byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                throw new RFrameworkException(
                    "JsonLocalizationHelper: JSON bundle bytes are empty.");
            }

            return ParseJsonLanguageBundle(Encoding.UTF8.GetString(bytes));
        }
    }
}
