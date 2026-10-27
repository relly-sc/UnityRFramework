using System.Collections.Generic;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 本地化解析 Helper 基类。资源加载由 LocalizationComponent 统一处理。
    /// </summary>
    public abstract class LocalizationHelperBase : MonoBehaviour, ILocalizationHelper
    {
        /// <summary>
        /// 从原始字节解析指定语言的本地化字典。
        /// </summary>
        /// <param name="language">语言名称。</param>
        /// <param name="bytes">原始文件字节。</param>
        /// <returns>解析后的本地化字典。</returns>
        public abstract Dictionary<string, string> ParseLanguage(string language, byte[] bytes);

        /// <summary>
        /// 从 JSON 字符串解析指定语言的本地化字典。
        /// 默认实现明确报告不支持，纯二进制 Helper 无需重写。
        /// </summary>
        /// <param name="language">语言名称。</param>
        /// <param name="json">JSON 字符串。</param>
        /// <returns>解析后的本地化字典。</returns>
        public virtual Dictionary<string, string> ParseLanguageFromString(string language, string json)
        {
            throw new RFrameworkException(
                $"Localization helper '{GetType().Name}' does not support JSON string parsing.");
        }

        /// <summary>
        /// 释放指定语言的本地化字典。
        /// </summary>
        /// <param name="language">语言名称。</param>
        /// <param name="languageDict">待释放的本地化字典。</param>
        public abstract void ReleaseLanguage(string language, Dictionary<string, string> languageDict);
    }
}
