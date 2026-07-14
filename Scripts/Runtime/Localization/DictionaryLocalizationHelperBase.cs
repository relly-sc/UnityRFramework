using System;
using System.Collections.Generic;
using RFramework;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 字典型本地化 Helper 基类，提供统一 JSON 解析与释放实现。
    /// JSON 支持顶层数组或 {"Items":[...]}，每项包含 Key、Value 字段。
    /// </summary>
    public abstract class DictionaryLocalizationHelperBase : LocalizationHelperBase
    {
        /// <inheritdoc/>
        public override Dictionary<string, string> ParseLanguageFromString(string language, string json)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                throw new RFrameworkException("Localization language code is invalid.");
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                throw new RFrameworkException($"Localization JSON for '{language}' is empty.");
            }

            return ParseJsonLanguage(language, json);
        }

        /// <inheritdoc/>
        public override void ReleaseLanguage(string language, Dictionary<string, string> languageDict)
        {
            languageDict?.Clear();
        }

        protected static Dictionary<string, string> ParseJsonLanguage(string language, string json)
        {
            string normalizedJson = json.Trim().TrimStart('\uFEFF');
            if (normalizedJson.StartsWith("[", StringComparison.Ordinal))
            {
                normalizedJson = "{\"Items\":" + normalizedJson + "}";
            }

            LocalizationJsonWrapper wrapper;
            try
            {
                wrapper = Utility.Json.ToObject<LocalizationJsonWrapper>(normalizedJson);
            }
            catch (Exception ex)
            {
                throw new RFrameworkException(
                    $"Localization JSON for '{language}' could not be parsed.", ex);
            }

            if (wrapper == null || wrapper.Items == null)
            {
                throw new RFrameworkException(
                    $"Localization JSON parser returned no Items for '{language}'.");
            }

            Dictionary<string, string> result = new Dictionary<string, string>();
            for (int i = 0; i < wrapper.Items.Length; i++)
            {
                LocalizationJsonEntry entry = wrapper.Items[i];
                if (entry == null || string.IsNullOrEmpty(entry.Key))
                {
                    throw new RFrameworkException(
                        $"Localization JSON for '{language}' contains an empty key at index {i}.");
                }

                if (result.ContainsKey(entry.Key))
                {
                    throw new RFrameworkException(
                        $"Localization JSON for '{language}' contains duplicate key '{entry.Key}'.");
                }

                result.Add(entry.Key, entry.Value ?? string.Empty);
            }

            return result;
        }

        [Serializable]
        private sealed class LocalizationJsonWrapper
        {
            public LocalizationJsonEntry[] Items = Array.Empty<LocalizationJsonEntry>();
        }

        [Serializable]
        private sealed class LocalizationJsonEntry
        {
            public string Key;
            public string Value;
        }
    }
}
