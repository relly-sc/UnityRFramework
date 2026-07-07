using RFramework.Localization;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 本地化辅助器基类。
    /// </summary>
    public abstract class LocalizationHelperBase : MonoBehaviour, ILocalizationHelper
    {
        /// <inheritdoc cref="ILocalizationHelper.LoadLanguageDict"/>
        public abstract System.Collections.Generic.Dictionary<string, string> LoadLanguageDict(string language);

        /// <inheritdoc cref="ILocalizationHelper.UnloadLanguageDict"/>
        public abstract void UnloadLanguageDict(string language);
    }
}
