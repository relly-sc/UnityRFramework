using System.Collections.Generic;
using RFramework.Localization;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 默认本地化辅助器。从 Resources 目录加载 CSV 格式的语言包。
    /// CSV 格式：首列 key，后续每列对应一种语言（列头为语言代码）。
    /// </summary>
    public class DefaultLocalizationHelper : LocalizationHelperBase
    {
        /// <inheritdoc/>
        public override Dictionary<string, string> LoadLanguageDict(string language)
        {
            if (string.IsNullOrEmpty(language))
            {
                return null;
            }

            // 从 Resources 加载 CSV 文件
            TextAsset csvAsset = Resources.Load<TextAsset>($"Localization/{language}");
            if (csvAsset == null)
            {
                Log.Warning("Localization file '{0}' not found in Resources/Localization/.", language);
                return new Dictionary<string, string>();
            }

            Dictionary<string, string> dict = new Dictionary<string, string>();
            string[] lines = csvAsset.text.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                {
                    continue;
                }

                int commaIndex = line.IndexOf(',');
                if (commaIndex <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, commaIndex).Trim();
                string value = line.Substring(commaIndex + 1).Trim();

                // 处理引号包裹的值
                if (value.Length >= 2 && value.StartsWith("\"") && value.EndsWith("\""))
                {
                    value = value.Substring(1, value.Length - 2);
                }

                dict[key] = value;
            }

            return dict;
        }

        /// <inheritdoc/>
        public override void UnloadLanguageDict(string language)
        {
            // Resources 加载的资源由 Unity 自动管理，无需手动卸载
        }
    }
}
