using System;
using System.Collections.Generic;
using System.Text;
using RFramework;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 默认 JSON 本地化解析器。每个文件只包含一种语言的 Key/Value 表。
    /// </summary>
    public sealed class DefaultLocalizationHelper : DictionaryLocalizationHelperBase
    {
        /// <inheritdoc/>
        public override Dictionary<string, string> ParseLanguage(string language, byte[] bytes)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                throw new RFrameworkException("DefaultLocalizationHelper: language code is invalid.");
            }

            if (bytes == null || bytes.Length == 0)
            {
                throw new RFrameworkException(
                    $"DefaultLocalizationHelper: JSON bytes for '{language}' are empty.");
            }

            string json = Encoding.UTF8.GetString(bytes);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new RFrameworkException(
                    $"DefaultLocalizationHelper: decoded JSON for '{language}' is empty.");
            }

            return ParseJsonLanguage(language, json);
        }
    }
}
