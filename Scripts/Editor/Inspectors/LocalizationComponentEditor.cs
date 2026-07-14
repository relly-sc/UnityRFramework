#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// LocalizationComponent 自定义 Inspector。
    /// </summary>
    [CustomEditor(typeof(Runtime.LocalizationComponent))]
    public sealed class LocalizationComponentEditor : UnityEditor.Editor
    {
        private static readonly string[] LanguageDisplayNames =
        {
            "Unspecified (Chinese Simplified)",
            "Chinese Simplified (zh-CN)",
            "Chinese Traditional (zh-TW)",
            "English (en-US)",
            "Japanese (ja-JP)",
            "Korean (ko-KR)",
            "French (fr-FR)",
            "German (de-DE)",
            "Spanish (es-ES)",
            "Italian (it-IT)",
            "Portuguese Brazil (pt-BR)",
            "Portuguese Portugal (pt-PT)",
            "Russian (ru-RU)",
            "Polish (pl-PL)",
            "Dutch (nl-NL)",
            "Turkish (tr-TR)",
            "Arabic (ar-SA)",
            "Thai (th-TH)",
            "Vietnamese (vi-VN)",
            "Indonesian (id-ID)",
            "Custom..."
        };

        private static readonly string[] LanguageCodes =
        {
            string.Empty,
            "zh-CN",
            "zh-TW",
            "en-US",
            "ja-JP",
            "ko-KR",
            "fr-FR",
            "de-DE",
            "es-ES",
            "it-IT",
            "pt-BR",
            "pt-PT",
            "ru-RU",
            "pl-PL",
            "nl-NL",
            "tr-TR",
            "ar-SA",
            "th-TH",
            "vi-VN",
            "id-ID"
        };

        private SerializedProperty localizationHelperTypeName;
        private SerializedProperty defaultLanguage;
        private SerializedProperty languageAssetRoot;
        private SerializedProperty languageFileExtension;
        private bool useCustomLanguage;

        private void OnEnable()
        {
            localizationHelperTypeName = serializedObject.FindProperty("localizationHelperTypeName");
            defaultLanguage = serializedObject.FindProperty("defaultLanguage");
            languageAssetRoot = serializedObject.FindProperty("languageAssetRoot");
            languageFileExtension = serializedObject.FindProperty("languageFileExtension");
            useCustomLanguage = FindLanguageIndex(defaultLanguage.stringValue) < 0;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Helper", EditorStyles.boldLabel);
            localizationHelperTypeName.stringValue = ComponentEditorUtility.HelperTypePopup(
                "Localization Helper", localizationHelperTypeName.stringValue, typeof(Runtime.LocalizationHelperBase));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            DrawDefaultLanguage();
            EditorGUILayout.PropertyField(languageAssetRoot, new GUIContent(
                "Language Asset Root",
                "语言文件根路径。DefaultResourceHelper 下对应 Resources 内的相对路径。"));
            EditorGUILayout.PropertyField(languageFileExtension, new GUIContent(
                "Language File Extension",
                "DefaultLocalizationHelper 使用 .json；BinaryLocalizationHelper 使用 .bytes。"));

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawDefaultLanguage()
        {
            int customIndex = LanguageDisplayNames.Length - 1;
            int languageIndex = FindLanguageIndex(defaultLanguage.stringValue);
            int selectedIndex = useCustomLanguage || languageIndex < 0 ? customIndex : languageIndex;

            GUIContent label = new GUIContent(
                "Default Language",
                "Start 时通过 ResourceComponent 自动加载并切换。Unspecified 会回退到简体中文 zh-CN。");
            int newIndex = EditorGUILayout.Popup(label, selectedIndex, LanguageDisplayNames);

            if (newIndex == customIndex)
            {
                useCustomLanguage = true;
                defaultLanguage.stringValue = EditorGUILayout.DelayedTextField(
                    new GUIContent("Language Code", "自定义语言代码，例如 en-GB。"),
                    defaultLanguage.stringValue);
                return;
            }

            useCustomLanguage = false;
            defaultLanguage.stringValue = LanguageCodes[newIndex];
        }

        private static int FindLanguageIndex(string languageCode)
        {
            for (int i = 0; i < LanguageCodes.Length; i++)
            {
                if (string.Equals(LanguageCodes[i], languageCode, System.StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}

#endif
